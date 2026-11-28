using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Lunet.Android.Gles;
using Lunet.Content;
using Lunet.Core;
using Lunet.Storage;

namespace Lunet.Android;

/// <summary>Arquivos trocados entre o IDE e o Preview isolado (pasta de cache compartilhada).</summary>
internal static class IsolatedPreviewFiles
{
    public const string ExtraContent = "content";
    public const string ExtraSaves = "saves";
    public const string ExtraAudioCache = "audioCache";
    public const string ExtraSession = "previewSession";
    public const string ExtraHighRefresh = "highRefresh";
    public const string ExtraFailure = "previewFailure";

    public static string Directory(global::Android.Content.Context context, string? session = null)
    {
        var root = Path.Combine(context.CacheDir!.AbsolutePath, "isolated");
        return Guid.TryParseExact(session, "N", out var id) ? Path.Combine(root, id.ToString("N")) : root;
    }

    public static string Assembly(global::Android.Content.Context context, string? session = null) => Path.Combine(Directory(context, session), "game.dll");

    /// <summary>Última captura textual do Profiler, auxiliar e descartável como o console.</summary>
    public static string Profiler(global::Android.Content.Context context, string? session = null) => Path.Combine(Directory(context, session), "profiler.txt");

    public static string Symbols(global::Android.Content.Context context, string? session = null) => Path.Combine(Directory(context, session), "game.pdb");

    /// <summary>Existe enquanto o Preview isolado roda; se sobrar depois que ele fecha, o processo morreu.</summary>
    public static string Marker(global::Android.Content.Context context, string? session = null) => Path.Combine(Directory(context, session), "running");

    /// <summary>Linhas do console do jogo: "nível|mensagem" (quebras de linha viram \n).</summary>
    public static string Log(global::Android.Content.Context context, string? session = null) => Path.Combine(Directory(context, session), "log.txt");
}

/// <summary>
/// Preview isolado (§10): roda o jogo em outro processo do Android. Se o jogo travar, estourar a memória ou derrubar o processo,
/// o IDE continua vivo e mostra o que aconteceu.
/// </summary>
[Activity(Label = "Lunet Preview", Theme = "@android:style/Theme.Material.NoActionBar", Process = ":preview", Exported = false, ExcludeFromRecents = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenLayout)]
public sealed class IsolatedPreviewActivity : Activity
{
    private readonly object _logLock = new();
    private PreviewHost? _host;
    private readonly List<string> _tail = [];

    private string? _session;
    private string? _failure;
    private AndroidGameImagePicker? _picker;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _session = Intent?.GetStringExtra(IsolatedPreviewFiles.ExtraSession);
        try
        {
            var marker = IsolatedPreviewFiles.Marker(this, _session);
            var pid = Process.MyPid().ToString();
            if (_session is not null && File.Exists(marker) && File.ReadAllText(marker) != pid)
            {
                // Android may recreate the top Activity after losing its process. Return to the IDE once instead
                // of opening the same game in a crash loop, and keep the old PID for the exit report.
                SetResult(Result.Canceled);
                base.Finish();
                return;
            }
            File.WriteAllText(marker, pid);
            var content = Intent?.GetStringExtra(IsolatedPreviewFiles.ExtraContent);
            var saves = Intent?.GetStringExtra(IsolatedPreviewFiles.ExtraSaves);
            var audio = Intent?.GetStringExtra(IsolatedPreviewFiles.ExtraAudioCache);
            if (content is null || saves is null || audio is null || Path.GetDirectoryName(content) is not { } projectPath)
            {
                Fail("O jogo compilado não pôde ser aberto pelo preview.");
                return;
            }

            var symbolsPath = IsolatedPreviewFiles.Symbols(this, _session);
            _picker = new AndroidGameImagePicker(this);
            var renderer = new PreviewRenderer(
                File.ReadAllBytes(IsolatedPreviewFiles.Assembly(this, _session)),
                File.Exists(symbolsPath) ? File.ReadAllBytes(symbolsPath) : null,
                new ProjectContentSource(projectPath),
                () => new AndroidAudioBackend(audio, message => RunOnUiThread(() => Write(LogLevel.Error, message))),
                new DirectorySaveStore(saves), new AndroidHaptics(this),
                (level, message) => RunOnUiThread(() => Write(level, message)),
                _picker, onFatal: message => RunOnUiThread(() => Fail(message)));
            var highRefresh = Intent?.GetBooleanExtra(IsolatedPreviewFiles.ExtraHighRefresh, true) ?? true;
            _host = new PreviewHost(this, renderer, () =>
                new ProjectStore(Path.GetDirectoryName(projectPath)!).Open(Path.GetFileName(projectPath)).ListFiles(),
                Finish, highRefresh);
            Window?.AddFlags(WindowManagerFlags.Fullscreen);
            SetContentView(_host);
#if LUNET_EMULATOR
            _ = ObservePreviewFrames(renderer);
#endif
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Java.Lang.Exception)
        {
            Fail("Não foi possível abrir o preview: " + ex.Message);
        }
    }

    private void Fail(string message)
    {
        if (IsFinishing) return;
        _failure = message;
        Write(LogLevel.Error, message);
        Finish();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode == AndroidGameImagePicker.RequestCode)
            _picker?.Complete(resultCode == Result.Ok ? data?.Data : null);
    }

#if LUNET_EMULATOR
    private async Task ObservePreviewFrames(PreviewRenderer renderer)
    {
        for (var attempt = 0; attempt < 80 && !IsFinishing; attempt++)
        {
            if (renderer.FramesDrawn > 0)
            {
                global::Android.Util.Log.Info("lunet-e2e", $"quadros={renderer.FramesDrawn} previewPid={Process.MyPid()}");
                return;
            }
            await Task.Delay(250);
        }
    }
#endif

    private void Write(LogLevel level, string message)
    {
#if LUNET_EMULATOR
        if (message == "LUNET_G6_UPDATE_BLOQUEADO")
            global::Android.Util.Log.Info("lunet-e2e", message);
#endif
        var prefix = level switch { LogLevel.Error => "[erro] ", LogLevel.Warning => "[aviso] ", _ => "" };
        _tail.Add(prefix + message);
        if (_tail.Count > 6) _tail.RemoveAt(0);
        _host?.SetConsole(string.Join('\n', _tail));
        try
        {
            lock (_logLock) File.AppendAllText(IsolatedPreviewFiles.Log(this, _session), $"{(int)level}|{message.Replace("\r", "").Replace("\n", "\\n")}\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // O console em arquivo é auxiliar: perder uma linha não pode derrubar o jogo.
        }
    }

    protected override void OnPause()
    {
        if (_host is { } host) StopWithDeadline(host.Pause);
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _host?.Resume();
    }

    public override void Finish()
    {
        // Gravar antes de retornar ao IDE: OnDestroy pode acontecer depois do OnResume dele.
        if (_host?.CaptureProfilerReport() is { } report)
        {
            try { File.WriteAllText(IsolatedPreviewFiles.Profiler(this, _session), report); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Write(LogLevel.Warning, "Não foi possível retornar a captura do Profiler: " + ex.Message);
            }
        }
        if (_host is { } host) StopWithDeadline(host.Shutdown);
        if (_failure is { } failure)
        {
            using var result = new Intent();
            result.PutExtra(IsolatedPreviewFiles.ExtraFailure, failure);
            SetResult(Result.Canceled, result);
        }
        else SetResult(Result.Ok);
        // A marca deve sair antes de retornar à IDE.
        try { File.Delete(IsolatedPreviewFiles.Marker(this, _session)); }
        catch (IOException) { /* O IDE já informa quando a marca não pode ser removida. */ }
        base.Finish();
    }

    protected override void OnDestroy()
    {
        if (_host is { } host) StopWithDeadline(host.Shutdown);
        base.OnDestroy();
    }

    /// <summary>GLSurfaceView waits for its GL thread. A game stuck in Update never acknowledges that wait.</summary>
    private static void StopWithDeadline(Action stop)
    {
        using var finished = new CancellationTokenSource();
        _ = EndStuckProcess(finished.Token);
        try { stop(); }
        finally { finished.Cancel(); }
    }

    private static async Task EndStuckProcess(CancellationToken finished)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(3), finished).ConfigureAwait(false); }
        catch (OperationCanceledException) when (finished.IsCancellationRequested) { return; }
        if (finished.IsCancellationRequested) return;
        global::Android.Util.Log.Warn("LunetPreview", "A thread do preview não respondeu ao comando de saída.");
        Process.KillProcess(Process.MyPid());
    }

    public override bool OnKeyDown(Keycode keyCode, KeyEvent? e) => _host?.OnKey(keyCode, true, e) == true || base.OnKeyDown(keyCode, e);

    public override bool OnKeyUp(Keycode keyCode, KeyEvent? e) => _host?.OnKey(keyCode, false, e) == true || base.OnKeyUp(keyCode, e);

    public override bool OnGenericMotionEvent(MotionEvent? e) => _host?.OnGenericMotion(e) == true || base.OnGenericMotionEvent(e);

    public override void OnBackPressed() => Finish();
}
