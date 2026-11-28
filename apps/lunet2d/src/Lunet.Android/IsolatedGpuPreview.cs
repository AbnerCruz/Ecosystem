using Android.App;
using Android.Content;
using Avalonia.Controls;
using Lunet.Client.Shell;
using Lunet.Compiler;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>A full-screen Android Preview whose EGL and game code are owned by the :preview process.</summary>
internal sealed class IsolatedGpuPreview : IGpuGamePreview
{
    internal const int RequestCode = 4150;
    private readonly Activity _activity;
    private readonly string _session = Guid.NewGuid().ToString("N");
    private readonly long _started = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private bool _returned;

    public IsolatedGpuPreview(Activity activity, CompileResult compiled, LunetProject project, bool highRefresh)
    {
        _activity = activity;
        var directory = IsolatedPreviewFiles.Directory(activity, _session);
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllBytes(IsolatedPreviewFiles.Assembly(activity, _session), compiled.Assembly!);
            if (compiled.Symbols is not null)
                File.WriteAllBytes(IsolatedPreviewFiles.Symbols(activity, _session), compiled.Symbols);

            using var intent = new Intent(activity, typeof(IsolatedPreviewActivity));
            intent.PutExtra(IsolatedPreviewFiles.ExtraSession, _session);
            intent.PutExtra(IsolatedPreviewFiles.ExtraContent, Path.Combine(project.Directory, "Content"));
            intent.PutExtra(IsolatedPreviewFiles.ExtraSaves, Path.Combine(project.Directory, ".lunet", "saves"));
            intent.PutExtra(IsolatedPreviewFiles.ExtraAudioCache, Path.Combine(activity.CacheDir!.AbsolutePath, "audio"));
            intent.PutExtra(IsolatedPreviewFiles.ExtraHighRefresh, highRefresh);
            activity.StartActivityForResult(intent, RequestCode);
        }
        catch
        {
            CleanUp();
            throw;
        }
    }

    public Control View { get; } = new Border();
    public string Backend => "GPU · OpenGL ES 3";
    public long FramesRendered => 0; // Android owns the render loop in :preview.
    public void SetPaused(bool paused) { }
    public void Step() { }
    public void Restart() { }
    public void ToggleInspector() { }
    public void ToggleProfiler() { }
    public void Key(Lunet.Input.Keys key, bool down) { }
    public void SetAppVisible(bool visible) { }

    public void Dispose()
    {
        if (_returned) return;
        _activity.FinishActivity(RequestCode);
    }

    /// <summary>Collects this run only. A killed Activity returns Canceled without the deliberate exit result.</summary>
    public string? Collect(Result resultCode, Intent? result, Action<string> console)
    {
        _returned = true;
        try
        {
            var log = IsolatedPreviewFiles.Log(_activity, _session);
            if (File.Exists(log))
            {
                foreach (var line in File.ReadLines(log).TakeLast(400))
                {
                    var bar = line.IndexOf('|');
                    if (bar < 1 || !int.TryParse(line[..bar], out var level)) continue;
                    console((level == 2 ? "[erro] " : level == 1 ? "[aviso] " : "") + line[(bar + 1)..].Replace("\\n", "\n"));
                }
            }
            if (resultCode == Result.Ok) return null;
            var reason = result?.GetStringExtra(IsolatedPreviewFiles.ExtraFailure);
            if (!string.IsNullOrWhiteSpace(reason)) return reason;
            return UnexpectedExit();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Java.Lang.Exception)
        {
            console("[aviso] Não foi possível ler o diagnóstico do preview: " + ex.Message);
            return resultCode == Result.Ok ? null : "O processo do preview encerrou inesperadamente.";
        }
        finally { CleanUp(); }
    }

    private string UnexpectedExit()
    {
        const string message = "O processo do preview encerrou inesperadamente. O editor continua aberto.";
        var marker = IsolatedPreviewFiles.Marker(_activity, _session);
        if (OperatingSystem.IsAndroidVersionAtLeast(30) && File.Exists(marker) &&
            int.TryParse(File.ReadAllText(marker), out var pid) &&
            _activity.GetSystemService(global::Android.Content.Context.ActivityService) is ActivityManager manager &&
            manager.GetHistoricalProcessExitReasons(_activity.PackageName, pid, 1).FirstOrDefault() is { } exit && exit.Timestamp >= _started)
            return $"{message} Android: {exit.Reason} (PID {pid}, status {exit.Status}). {exit.Description}".TrimEnd();
        return message;
    }

    private void CleanUp()
    {
        try { Directory.Delete(IsolatedPreviewFiles.Directory(_activity, _session), recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
