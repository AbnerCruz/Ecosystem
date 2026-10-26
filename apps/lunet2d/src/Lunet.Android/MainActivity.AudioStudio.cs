using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using Lunet.Studio;
using AndroidUri = global::Android.Net.Uri;
using Path = System.IO.Path;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private const int ImportAudioRequestCode = 4105;
    private WaveAudioStudioView? _audioStudio;

    private void ShowAudioStudio()
    {
        if (_project is null) return;
        SaveCurrent();
        var sounds = _project.ListFiles()
            .Where(p => (p.StartsWith("Content/", StringComparison.OrdinalIgnoreCase) ||
                         p.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) &&
                        p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)).ToArray();
        var options = new[] { "+ Criar efeito sonoro", "Importar WAV do aparelho" }.Concat(sounds).ToArray();
        new AlertDialog.Builder(this)!.SetTitle("Audio Studio — WAV PCM")!
            .SetItems(options, (_, e) =>
            {
                if (e.Which == 0) AskNewSound();
                else if (e.Which == 1) ImportAudioFromDevice();
                else OpenWaveAudio(sounds[e.Which - 2]);
            })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
    }

    private void AskNewSound()
    {
        var content = Vertical();
        content.SetPadding(Dp(16), Dp(8), Dp(16), Dp(6));
        var frequencyLabel = new TextView(this) { TextSize = 13 };
        var frequency = new SeekBar(this) { Max = 4960, Progress = 400 };
        void RefreshFrequency() => frequencyLabel.Text = $"Frequência: {40 + frequency.Progress} Hz";
        frequency.ProgressChanged += (_, _) => RefreshFrequency();
        RefreshFrequency();
        content.AddView(frequencyLabel);
        content.AddView(frequency);

        var durationLabel = new TextView(this) { TextSize = 13 };
        var duration = new SeekBar(this) { Max = 495, Progress = 45 };
        void RefreshDuration() => durationLabel.Text = $"Duração: {(5 + duration.Progress) / 100.0:0.00} s";
        duration.ProgressChanged += (_, _) => RefreshDuration();
        RefreshDuration();
        content.AddView(durationLabel);
        content.AddView(duration);

        var waves = new RadioGroup(this) { Orientation = Orientation.Vertical };
        var values = new[] { (WaveShape.Sine, "Senoidal"), (WaveShape.Square, "Quadrada"), (WaveShape.Triangle, "Triangular"), (WaveShape.Noise, "Ruído") };
        WaveShape selected = WaveShape.Sine;
        foreach (var (wave, label) in values)
        {
            var control = new RadioButton(this) { Text = label, Id = View.GenerateViewId(), Checked = wave == selected };
            control.Click += (_, _) => selected = wave;
            waves.AddView(control);
        }
        content.AddView(waves);
        var scroll = new ScrollView(this);
        scroll.AddView(content);
        new AlertDialog.Builder(this)!.SetTitle("Criar efeito no Lunet")!
            .SetView(scroll)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Criar", (_, _) =>
            {
                try
                {
                    var audio = WaveAudioDocument.CreateEffect(40 + frequency.Progress,
                        (5 + duration.Progress) / 100.0, selected);
                    OpenWaveAudio(audio, "efeito");
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
                {
                    Toast.MakeText(this, ex.Message, ToastLength.Long)?.Show();
                }
            })!.Show();
    }

    private void OpenWaveAudio(string path)
    {
        if (_project is null) return;
        try
        {
            var fullPath = SafeAssetFilePath(_project, path);
            if (new FileInfo(fullPath).Length > 24L * 1024 * 1024)
                throw new InvalidDataException("WAV muito grande para edição nesta versão (máximo 24 MiB).");
            OpenWaveAudio(WaveAudioDocument.Parse(File.ReadAllBytes(fullPath)), Path.GetFileNameWithoutExtension(path));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or ProjectException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível editar o WAV: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void OpenWaveAudio(WaveAudioDocument doc, string name)
    {
        if (_project is null) return;
        var project = _project;
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        var view = new WaveAudioStudioView(this, doc, Path.Combine(CacheDir!.AbsolutePath, "audio-studio"),
            bytes =>
            {
                // Uma nova cópia por gravação: nunca sobrescrever um arquivo já existente do usuário.
                string safe = new string(name.Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').Take(32).ToArray());
                if (safe.Length == 0) safe = "som";
                string filename = safe + "-edit-" + Guid.NewGuid().ToString("N")[..8] + ".wav";
                using var stream = new MemoryStream(bytes, writable: false);
                project.Import("Content/Audio", filename, stream);
                RefreshExplorer();
            },
            () => dialog.Dismiss());
        _audioStudio = view;
        dialog.SetContentView(view);
        dialog.DismissEvent += (_, _) =>
        {
            view.Dispose();
            if (ReferenceEquals(_audioStudio, view)) _audioStudio = null;
        };
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }

    private void ImportAudioFromDevice()
    {
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("audio/*");
        intent.PutExtra(Intent.ExtraMimeTypes, new[] { "audio/wav", "audio/x-wav", "audio/wave", "audio/vnd.wave", "application/octet-stream" });
        StartActivityForResult(intent, ImportAudioRequestCode);
    }

    private void CompleteAudioImport(AndroidUri uri)
    {
        if (_project is null) return;
        try
        {
            string? filename = null;
            using (var cursor = ContentResolver?.Query(uri, null, null, null, null))
            {
                if (cursor is not null && cursor.MoveToFirst())
                {
                    int index = cursor.GetColumnIndex(global::Android.Provider.IOpenableColumns.DisplayName);
                    if (index >= 0) filename = cursor.GetString(index);
                }
            }
            filename = Path.GetFileName(filename ?? "som.wav");
            if (!filename.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O editor aceita somente WAV PCM. Para MP3/OGG, use o player do Assets.");
            using var input = ContentResolver?.OpenInputStream(uri)
                ?? throw new IOException("Arquivo não pôde ser aberto.");
            // Valida antes de importar: não salvar um arquivo inválido ou grande acidentalmente.
            using var source = new MemoryStream();
            var buffer = new byte[65536];
            int n;
            while ((n = input.Read(buffer)) > 0)
            {
                if (source.Length + n > 24L * 1024 * 1024)
                    throw new InvalidDataException("WAV muito grande para edição (máximo 24 MiB).");
                source.Write(buffer, 0, n);
            }
            var decoded = WaveAudioDocument.Parse(source.ToArray());
            string savedName = Path.GetFileNameWithoutExtension(filename) + "-" + Guid.NewGuid().ToString("N")[..8] + ".wav";
            source.Position = 0;
            var relative = _project.Import("Content/Audio", savedName, source);
            RefreshExplorer();
            OpenWaveAudio(decoded, Path.GetFileNameWithoutExtension(relative));
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException or ProjectException or global::Java.Lang.SecurityException)
        {
            new AlertDialog.Builder(this)!.SetTitle("Falha ao importar WAV")!.SetMessage(ex.Message)!
                .SetPositiveButton("OK", (_, _) => { })!.Show();
        }
    }
}
