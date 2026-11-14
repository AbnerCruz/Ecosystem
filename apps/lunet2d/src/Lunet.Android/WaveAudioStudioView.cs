using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using Path = System.IO.Path;
using AColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>Editor Android touch-first para efeitos WAV PCM, sem alterar o arquivo-fonte.</summary>
internal sealed class WaveAudioStudioView : LinearLayout, IDisposable
{
    private readonly WaveAudioDocument _audio;
    private readonly Action<byte[]> _save;
    private readonly Action _close;
    private readonly string _cacheDirectory;
    private readonly AudioPreviewView _preview;
    private readonly SeekBar _start;
    private readonly SeekBar _end;
    private readonly SeekBar _gain;
    private readonly SeekBar _loopStart;
    private readonly SeekBar _loopEnd;
    private readonly CheckBox _loop;
    private readonly TextView _details;
    private bool _disposed;

    public WaveAudioStudioView(Context context, WaveAudioDocument audio, string cacheDirectory,
        Action<byte[]> save, Action close) : base(context)
    {
        _audio = audio;
        _save = save;
        _close = close;
        _cacheDirectory = cacheDirectory;
        Orientation = Orientation.Vertical;
        SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
        SetBackgroundColor(LunetUi.Background);

        var head = new LinearLayout(context) { Orientation = Orientation.Horizontal };
        var title = LunetUi.Label(context, "∿ Áudio", LunetUi.TextTitle + 2, LunetUi.Text, bold: true);
        head.AddView(title, new LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        head.AddView(Button("Fechar", () => { _preview?.Stop(); _close(); }));
        AddView(head);

        _details = new TextView(context) { TextSize = 13 };
        _details.SetTextColor(LunetUi.TextMuted);
        AddView(_details);

        // Prévia do que será salvo: recorte e volume atuais, com cursor e toque na onda para ouvir dali.
        _preview = new AudioPreviewView(context, audio.GetPeaks(320), audio.DurationSeconds, cacheDirectory, PreviewBytes);
        AddView(_preview, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        var saveButton = Button("Salvar cópia WAV", Save);
        var saveParams = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(8) };
        AddView(saveButton, saveParams);

        var scroll = new ScrollView(context);
        var settings = new LinearLayout(context) { Orientation = Orientation.Vertical };
        settings.SetPadding(Dp(4), Dp(12), Dp(4), Dp(12));
        scroll.AddView(settings);
        AddView(scroll, new LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        _start = Slider(settings, "Início do recorte", 0);
        _end = Slider(settings, "Fim do recorte", 1000);
        _gain = Slider(settings, "Volume (0–200%)", 100, maximum: 200);
        _loop = new CheckBox(context) { Text = "Gravar pontos de loop no WAV (SMPL)", Checked = false };
        _loop.SetTextColor(LunetUi.Text);
        settings.AddView(_loop);
        _loopStart = Slider(settings, "Início do loop", 0);
        _loopEnd = Slider(settings, "Fim do loop", 1000);
        settings.AddView(new TextView(context)
        {
            Text = "O original é preservado. A cópia é salva em Content/Audio. Reprodução em loop usa a região selecionada; pontos SMPL ficam gravados para ferramentas compatíveis.",
            TextSize = 12
        });

        _start.ProgressChanged += (_, _) => Refresh();
        _end.ProgressChanged += (_, _) => Refresh();
        _gain.ProgressChanged += (_, _) => Refresh();
        _loopStart.ProgressChanged += (_, _) => Refresh();
        _loopEnd.ProgressChanged += (_, _) => Refresh();
        _loop.CheckedChange += (_, _) => Refresh();
        Refresh();
    }

    private int Dp(int value) => (int)(value * Context!.Resources!.DisplayMetrics!.Density);
    private Button Button(string label, Action action)
    {
        Button? result = null;
        result = LunetUi.Button(Context!, label, () => { }, label.StartsWith("Salvar", StringComparison.Ordinal) ? ButtonStyle.Primary : ButtonStyle.Secondary);
        result.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or UnauthorizedAccessException)
            { Toast.MakeText(Context, ex.Message, ToastLength.Long)?.Show(); }
        };
        return result;
    }

    private SeekBar Slider(LinearLayout parent, string name, int progress, int maximum = 1000)
    {
        var label = LunetUi.Label(Context!, name, LunetUi.TextLabel, LunetUi.TextMuted);
        label.SetPadding(0, Dp(10), 0, 0);
        parent.AddView(label);
        var slider = new SeekBar(Context) { Max = maximum, Progress = progress };
        parent.AddView(slider);
        return slider;
    }

    private static string Time(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.fff");

    private void Refresh()
    {
        if (_disposed) return;
        int start = _start.Progress, end = _end.Progress;
        if (end <= start)
        {
            if (start == 1000) { _start.Progress = 999; start = 999; }
            _end.Progress = start + 1;
            end = _end.Progress;
        }
        int ls = Math.Clamp(_loopStart.Progress, start, end - 1);
        int le = Math.Clamp(_loopEnd.Progress, ls + 1, end);
        if (ls != _loopStart.Progress) _loopStart.Progress = ls;
        if (le != _loopEnd.Progress) _loopEnd.Progress = le;
        _loopStart.Enabled = _loop.Checked;
        _loopEnd.Enabled = _loop.Checked;
        double duration = _audio.DurationSeconds;
        _details.Text = $"{_audio.SampleRate} Hz · {_audio.Channels} canal(is) · {Time(duration)}\n" +
            $"Recorte {Time(duration * start / 1000.0)}–{Time(duration * end / 1000.0)} · {_gain.Progress}% volume" +
            (_loop.Checked ? $" · loop {Time(duration * ls / 1000.0)}–{Time(duration * le / 1000.0)}" : "");
        _preview.SetOverlay(start / 1000f, end / 1000f, _loop.Checked, ls / 1000f, le / 1000f);
        // Com loop ligado, a prévia repete só a região do loop; sem ele, toca o trecho recortado inteiro.
        if (_loop.Checked) _preview.SetWindow(ls / 1000.0, le / 1000.0, duration * (le - ls) / 1000.0);
        else _preview.SetWindow(start / 1000.0, end / 1000.0, duration * (end - start) / 1000.0);
        _preview.SetLooping(_loop.Checked);
        _preview.ContentChanged();
    }

    private byte[] PreviewBytes() => _loop.Checked
        ? _audio.Render(_audio.DurationSeconds * _loopStart.Progress / 1000.0,
            _audio.DurationSeconds * _loopEnd.Progress / 1000.0, _gain.Progress / 100f)
        : Output(includeLoop: false);

    private byte[] Output(bool includeLoop)
    {
        double duration = _audio.DurationSeconds;
        double start = duration * _start.Progress / 1000.0;
        double end = duration * _end.Progress / 1000.0;
        double? ls = includeLoop && _loop.Checked ? duration * _loopStart.Progress / 1000.0 : null;
        double? le = includeLoop && _loop.Checked ? duration * _loopEnd.Progress / 1000.0 : null;
        return _audio.Render(start, end, _gain.Progress / 100f, ls, le);
    }

    private void Save()
    {
        if (_disposed) return;
        _save(Output(includeLoop: true));
        Toast.MakeText(Context, "Cópia WAV salva em Content/Audio.", ToastLength.Short)?.Show();
    }

    /// <summary>Interrompe reprodução quando o aplicativo perde foco.</summary>
    public void Suspend() => _preview.Stop();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _preview.Dispose();
    }
}
