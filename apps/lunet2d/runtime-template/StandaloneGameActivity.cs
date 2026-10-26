using System.Numerics;
using Android.App;
using Android.Content.PM;
using Android.Opengl;
using Android.OS;
using Android.Views;
using Android.Widget;
using Lunet.Android.Gles;
using Lunet.Input;
using Lunet.Storage;

namespace Lunet.Android;

/// <summary>Minimal standalone game host for the F9 packaging experiment; no editor UI.</summary>
[Activity(Label = "Lunet Game", MainLauncher = true, Exported = true,
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class StandaloneGameActivity : Activity
{
    private GLSurfaceView? _surface;
    private PreviewRenderer? _renderer;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try
        {
            using var assemblyStream = Assets!.Open("game/Game.dll");
            using var buffer = new MemoryStream();
            assemblyStream.CopyTo(buffer);

            _renderer = new PreviewRenderer(
                buffer.ToArray(), null, new PackagedContentSource(Assets),
                () => new AndroidAudioBackend(Path.Combine(CacheDir!.AbsolutePath, "audio"),
                    message => global::Android.Util.Log.Error("LunetGame", message)),
                new DirectorySaveStore(Path.Combine(FilesDir!.AbsolutePath, "saves")),
                new AndroidHaptics(this),
                (level, message) => global::Android.Util.Log.Info("LunetGame", level + ": " + message));

            _surface = new GLSurfaceView(this);
            _surface.SetEGLContextClientVersion(3);
            _surface.PreserveEGLContextOnPause = true;
            _surface.SetRenderer(_renderer);
            _surface.RenderMode = Rendermode.Continuously;
            _surface.Touch += OnTouch;
            _renderer.SetDisplay(Resources!.DisplayMetrics!.Density, 0, 0, 0, 0);
            SetContentView(_surface);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("LunetGame", ex.ToString());
            Toast.MakeText(this, "Falha ao iniciar jogo: " + ex.Message, ToastLength.Long)?.Show();
            Finish();
        }
    }

    private void OnTouch(object? sender, View.TouchEventArgs args)
    {
        if (args.Event is not { } e || _renderer is null) return;
        var touches = new TouchPoint[e.PointerCount];
        var action = e.ActionMasked;
        var changed = e.ActionIndex;
        for (int i = 0; i < touches.Length; i++)
        {
            var phase = TouchPhase.Moved;
            if (action is MotionEventActions.Down or MotionEventActions.PointerDown && i == changed)
                phase = TouchPhase.Pressed;
            else if (action is MotionEventActions.Up or MotionEventActions.PointerUp && i == changed)
                phase = TouchPhase.Released;
            else if (action == MotionEventActions.Cancel)
                phase = TouchPhase.Cancelled;
            touches[i] = new TouchPoint(e.GetPointerId(i), phase, new Vector2(e.GetX(i), e.GetY(i)));
        }
        _renderer.SetTouches(touches);
        args.Handled = true;
    }

    protected override void OnPause()
    {
        _renderer?.SetAppPaused(true);
        _surface?.OnPause();
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _surface?.OnResume();
        _renderer?.SetAppPaused(false);
    }

    protected override void OnDestroy()
    {
        if (_surface is not null) _surface.Touch -= OnTouch;
        _renderer?.Shutdown();
        base.OnDestroy();
    }
}
