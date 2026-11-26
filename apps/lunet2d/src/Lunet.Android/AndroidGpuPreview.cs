using Android.App;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using Lunet.Android.Gles;
using Lunet.Client.Shell;
using Lunet.Compiler;
using Lunet.Content;
using Lunet.Core;
using Lunet.Storage;

namespace Lunet.Android;

/// <summary>
/// M5: the GLES Preview of the previous screens (PreviewHost + PreviewRenderer, unchanged: same input, sensors,
/// gamepad, Inspector, profiler, audio and lifecycle) inside the shell's Jogo tab, through Avalonia's NativeControlHost.
/// The previous screen's own control bar and console are hidden (gameplay mode): the shell's bar and Console serve.
/// </summary>
internal sealed class AndroidGpuPreview : IGpuGamePreview
{
    private readonly PreviewHost _host;
    private readonly PreviewRenderer _renderer;
    private bool _disposed;

    public AndroidGpuPreview(Activity activity, CompileResult compiled, LunetProject project, bool highRefresh, Action<string> log, Action<string> fault, Action stop)
    {
        _renderer = new PreviewRenderer(compiled.Assembly!, compiled.Symbols,
            new ProjectContentSource(project.Directory),
            () => new AndroidAudioBackend(Path.Combine(activity.CacheDir!.AbsolutePath, "audio"), message => activity.RunOnUiThread(() => log("[erro] " + message))),
            new DirectorySaveStore(Path.Combine(project.Directory, ".lunet", "saves")),
            new AndroidHaptics(activity),
            (level, message) => activity.RunOnUiThread(() => log((level == LogLevel.Error ? "[erro] " : level == LogLevel.Warning ? "[aviso] " : "") + message)),
            new AndroidGameImagePicker(activity),
            onFatal: fault);
        _host = new PreviewHost(activity, _renderer, project.ListFiles, stop, highRefresh, gameplay: true);
        View = new NativeView(_host);
    }

    public Control View { get; }
    public string Backend => "GPU · OpenGL ES 3";

    public void SetPaused(bool paused) => _renderer.SetPaused(paused);
    public void Step() => _renderer.RequestStep();
    public void Restart() => _renderer.RequestRestart();
    public void ToggleInspector() => _host.ToggleInspector();
    public void ToggleProfiler() => _host.ToggleProfiler();
    public void Key(Lunet.Input.Keys key, bool down) => _renderer.SetKey(key, down);

    public void SetAppVisible(bool visible)
    {
        if (_disposed) return;
        if (visible) _host.Resume(); else _host.Pause();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _host.Shutdown(); // queues the renderer's shutdown on the GL thread, then pauses the surface
    }

    /// <summary>The Android view as an Avalonia control (Avalonia keeps it positioned over its own surface).</summary>
    private sealed class NativeView(global::Android.Views.View view) : NativeControlHost
    {
        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent) => new AndroidViewControlHandle(view);
        protected override void DestroyNativeControlCore(IPlatformHandle control) { } // the preview owns its view
    }
}
