using Android.App;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using Lunet.Studio;
using Lunet.Studio.Music;
using Path = System.IO.Path;

namespace Lunet.Android;

/// <summary>Music Studio and sound designer entry points (LUNET-500). Sources live next to the WAVs the game loads.</summary>
public sealed partial class MainActivity
{
    internal const string MusicSongExtension = ".song.json";
    internal const string SoundEffectExtension = ".sfx.json";
    private const string AudioFolder = "Content/Audio";
    private MusicStudioView? _musicStudio;

    private string UniqueAudioName(string baseName, string extension)
    {
        var safe = new string(ProjectPaletteIdentifier(baseName).ToLowerInvariant().Take(32).ToArray());
        if (safe.Length == 0) safe = "som";
        var name = safe;
        for (int i = 2; File.Exists(Path.Combine(_project!.Directory, AudioFolder, name + extension)) ||
                        File.Exists(Path.Combine(_project.Directory, AudioFolder, name + ".wav")); i++)
            name = safe + "-" + i;
        return name;
    }

    /// <summary>"Tema do Chefe" → "tema-do-chefe": ASCII, safe as a file name.</summary>
    private static string ProjectPaletteIdentifier(string text)
    {
        var pascal = Lunet.Studio.ProjectPalette.Identifier(text);
        var builder = new System.Text.StringBuilder();
        foreach (var c in pascal)
        {
            if (char.IsUpper(c) && builder.Length > 0) builder.Append('-');
            builder.Append(c);
        }
        return builder.ToString();
    }

    private void NewSong()
    {
        var progressions = MusicTheory.Progressions;
        var items = new[] { "Vazia (uma faixa)" }
            .Concat(progressions.Select(p => "Começo pronto: " + p.Name)).ToArray();
        new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Nova música")!
            .SetItems(items, (_, e) =>
            {
                MusicSong song;
                if (e.Which == 0) song = MusicSong.Empty("Música");
                else
                {
                    var progression = progressions[e.Which - 1];
                    bool minor = progression.Id is "sad" or "tension";
                    song = MusicSong.Starter("Tema", progression.Id, minor ? "minor" : "major", minor ? 57 : 60, minor ? 96 : 118, System.Environment.TickCount);
                }
                var name = UniqueAudioName(song.Name, MusicSongExtension);
                song.Name = name;
                var path = AudioFolder + "/" + name + MusicSongExtension;
                _project!.WriteText(path, song.ToJson());
                RefreshExplorer();
                OpenMusicStudio(path, song);
            })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void OpenSong(string path)
    {
        try { OpenMusicStudio(path, MusicSong.Parse(_project!.ReadText(path))); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or ProjectException)
        {
            Toast.MakeText(this, "Não foi possível abrir a música: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void OpenMusicStudio(string path, MusicSong song)
    {
        var project = _project!;
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        var wavPath = path[..^MusicSongExtension.Length] + ".wav";
        var view = new MusicStudioView(this, song, Path.Combine(CacheDir!.AbsolutePath, "music-studio"),
            current => project.WriteText(path, current.ToJson()),
            current => ExportSong(current, wavPath),
            () => dialog.Dismiss());
        _musicStudio = view;
        dialog.SetContentView(view);
        dialog.SetCancelable(false);
        dialog.DismissEvent += (_, _) => { if (ReferenceEquals(_musicStudio, view)) _musicStudio = null; RefreshExplorer(); };
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }

    private void ExportSong(MusicSong song, string wavPath)
    {
        var project = _project!;
        var progress = Toast.MakeText(this, "Exportando a música…", ToastLength.Long);
        progress?.Show();
        Task.Run(() => new MusicRenderer().RenderWav(MusicSong.Parse(song.ToJson()))).ContinueWith(task => RunOnUiThread(() =>
        {
            progress?.Cancel();
            if (task.Exception is { } error)
            {
                Toast.MakeText(this, "Falha ao exportar: " + error.GetBaseException().Message, ToastLength.Long)?.Show();
                return;
            }
            using (var stream = new MemoryStream(task.Result, writable: false))
            {
                // The WAV is derived from the .song.json, like a sprite sheet from its source: replaced on every export.
                var full = Path.Combine(project.Directory, wavPath);
                if (File.Exists(full)) File.Delete(full);
                project.Import(Path.GetDirectoryName(wavPath)!.Replace('\\', '/'), Path.GetFileName(wavPath), stream);
            }
            RefreshExplorer();
            var load = wavPath["Content/".Length..];
            new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                .SetTitle("Música pronta para o jogo")!
                .SetMessage($"{wavPath} · {song.DurationSeconds:0.0} s{(song.Loop ? " · com loop" : "")}\n\n" +
                            "No LoadContent:\n" +
                            $"var musica = Content.LoadMusic(\"{load}\");\n\n" +
                            "Para tocar:\n" +
                            $"Audio.PlayMusic(musica, loop: {(song.Loop ? "true" : "false")}, fadeInSeconds: 1);\n\n" +
                            "Exporte de novo sempre que mudar a música.")!
                .SetPositiveButton("OK", (_, _) => { })!.Show();
        }), TaskScheduler.Default);
    }

    // ---------- Sound designer ----------

    private void OpenDesignedSound(string path)
    {
        try { OpenSoundDesigner(path, SoundDesigner.Parse(_project!.ReadText(path))); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or ProjectException)
        {
            Toast.MakeText(this, "Não foi possível abrir o efeito: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void OpenSoundDesigner(string? path, SoundEffect start)
    {
        var effect = start;
        int seed = System.Environment.TickCount;
        var content = LunetUi.Column(this, 8);
        content.SetPadding(Dp(16), Dp(8), Dp(16), Dp(8));
        AudioPreviewView? preview = null;
        var sliders = LunetUi.Column(this, 2);
        void Update(bool play)
        {
            try
            {
                var samples = SoundDesigner.Render(effect);
                var wav = WaveAudioDocument.Parse(WavEncoder.Encode(samples, 1, SoundDesigner.SampleRate));
                preview?.SetPeaks(wav.GetPeaks(240));
                preview?.SetWindow(0, 1, wav.DurationSeconds);
                if (play) preview?.Play(); else preview?.ContentChanged();
            }
            catch (InvalidDataException) { }
        }

        Button waveButton = null!;
        var categories = LunetUi.Row(this, 6);
        foreach (var (id, label) in SoundDesigner.Categories)
            categories.AddView(LunetUi.Chip(this, label, () => { effect = SoundDesigner.Generate(id, ++seed); BuildSliders(); Update(play: true); }));
        var categoryScroll = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        categoryScroll.AddView(categories);
        content.AddView(LunetUi.Label(this, "Toque numa categoria para gerar; toque de novo para outro som.", LunetUi.TextCaption, LunetUi.TextMuted));
        content.AddView(categoryScroll);

        var actions = LunetUi.Row(this, 6);
        actions.AddView(LunetUi.Button(this, "Variação", () => { effect = SoundDesigner.Mutate(effect, ++seed); BuildSliders(); Update(play: true); }, ButtonStyle.Secondary));
        actions.AddView(LunetUi.Button(this, "Onda: " + effect.Wave, () => { }, ButtonStyle.Quiet));
        waveButton = (Button)actions.GetChildAt(1)!;
        waveButton.Click += (_, _) =>
        {
            var waves = Enum.GetValues<Waveform>();
            effect = effect with { Wave = waves[(Array.IndexOf(waves, effect.Wave) + 1) % waves.Length] };
            waveButton.Text = "Onda: " + effect.Wave;
            Update(play: true);
        };
        content.AddView(actions);

        void BuildSliders()
        {
            waveButton.Text = "Onda: " + effect.Wave;
            sliders.RemoveAllViews();
            void Slider(string label, float value, float min, float max, Func<SoundEffect, float, SoundEffect> set, bool log = false)
            {
                var text = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextMuted);
                float ToProgress(float v) => log ? (float)(Math.Log(v / min) / Math.Log(max / min)) : (v - min) / (max - min);
                float FromProgress(float p) => log ? (float)(min * Math.Pow(max / min, p)) : min + p * (max - min);
                var bar = new SeekBar(this) { Max = 1000, Progress = (int)(Math.Clamp(ToProgress(value), 0, 1) * 1000) };
                void Label(float v) => text.Text = $"{label}: {v:0.###}";
                Label(value);
                bar.ProgressChanged += (_, e) =>
                {
                    if (!e.FromUser) return;
                    var v = FromProgress(e.Progress / 1000f);
                    Label(v);
                    effect = set(effect, v);
                    Update(play: false);
                };
                sliders.AddView(text);
                sliders.AddView(bar);
            }
            Slider("Frequência inicial (Hz)", effect.StartHz, 40, 4000, (e, v) => e with { StartHz = v }, log: true);
            Slider("Deslize de tom (semitons/s)", effect.Slide, -120, 120, (e, v) => e with { Slide = v });
            Slider("Salto de tom (semitons)", effect.ArpeggioSemitones, -12, 12, (e, v) => e with { ArpeggioSemitones = MathF.Round(v) });
            Slider("Vibrato", effect.VibratoDepth, 0, 1, (e, v) => e with { VibratoDepth = v });
            Slider("Ataque (s)", effect.Attack, 0, 0.5f, (e, v) => e with { Attack = v });
            Slider("Duração (s)", effect.Sustain, 0, 1.5f, (e, v) => e with { Sustain = v });
            Slider("Impacto (punch)", effect.Punch, 0, 1, (e, v) => e with { Punch = v });
            Slider("Decaimento (s)", effect.Decay, 0.01f, 2, (e, v) => e with { Decay = v });
            Slider("Repetição (s, 0 = não)", effect.Repeat, 0, 0.5f, (e, v) => e with { Repeat = v < 0.02f ? 0 : v });
            Slider("Brilho (passa-baixa Hz)", effect.LowPass, 200, 20000, (e, v) => e with { LowPass = v }, log: true);
            Slider("Retrô (bit crush)", effect.Crush, 0, 1, (e, v) => e with { Crush = v });
            Slider("Volume", effect.Volume, 0, 1, (e, v) => e with { Volume = v });
        }

        var first = WaveAudioDocument.Parse(SoundDesigner.RenderWav(effect));
        preview = new AudioPreviewView(this, first.GetPeaks(240), first.DurationSeconds,
            Path.Combine(CacheDir!.AbsolutePath, "audio-studio"), () => SoundDesigner.RenderWav(effect), waveHeightDp: 88);
        content.AddView(preview, 0);
        BuildSliders();
        content.AddView(sliders);
        var scroll = new ScrollView(this);
        scroll.AddView(content);
        var dialog = new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
            .SetTitle("Desenhar efeito sonoro")!
            .SetView(scroll)!
            .SetNegativeButton("Fechar", (_, _) => { })!
            .SetPositiveButton("Salvar no jogo", (_, _) => SaveDesignedSound(path, effect))!
            .Create();
        dialog.DismissEvent += (_, _) => preview.Dispose();
        dialog.Show();
    }

    private void SaveDesignedSound(string? path, SoundEffect effect)
    {
        var project = _project!;
        try
        {
            var name = path is null ? UniqueAudioName(effect.Name, SoundEffectExtension) : Path.GetFileName(path)[..^SoundEffectExtension.Length];
            var source = AudioFolder + "/" + name + SoundEffectExtension;
            project.WriteText(source, SoundDesigner.ToJson(effect with { Name = name }));
            var wav = SoundDesigner.RenderWav(effect);
            var full = Path.Combine(project.Directory, AudioFolder, name + ".wav");
            if (File.Exists(full)) File.Delete(full);
            using (var stream = new MemoryStream(wav, writable: false)) project.Import(AudioFolder, name + ".wav", stream);
            RefreshExplorer();
            new AlertDialog.Builder(this, global::Android.Resource.Style.ThemeDeviceDefaultDialogAlert)!
                .SetTitle("Efeito salvo")!
                .SetMessage($"{AudioFolder}/{name}.wav (fonte editável: {source})\n\nNo LoadContent:\n" +
                            $"var som = Content.LoadSound(\"Audio/{name}.wav\");\n\nPara tocar:\nsom.Play();")!
                .SetPositiveButton("OK", (_, _) => { })!.Show();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ProjectException or UnauthorizedAccessException)
        {
            Toast.MakeText(this, "Não foi possível salvar: " + ex.Message, ToastLength.Long)?.Show();
        }
    }
}
