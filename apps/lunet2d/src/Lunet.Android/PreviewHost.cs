using Android.App;
using Android.Graphics;
using Android.Hardware;
using Android.Opengl;
using Android.OS;
using Android.Views;
using Android.Widget;
using Lunet.Android.Gles;
using Lunet.Input;
using AndroidColor = Android.Graphics.Color;

namespace Lunet.Android;

/// <summary>
/// Tela do Preview: superfície OpenGL, controles (Stop, Restart, Pause, Step), Inspector, console sobreposto, sensores, toque,
/// teclado e controle. Usada pelo Preview rápido (no processo do IDE) e pelo Preview isolado (processo separado).
/// </summary>
internal sealed class PreviewHost : FrameLayout, ISensorEventListener
{
    private readonly Activity _activity;
    private readonly PreviewRenderer _renderer;
    private readonly GLSurfaceView _glView;
    private readonly InspectorPanel _inspector;
    private readonly TextView _console;
    private readonly TextView _profiler;
    private readonly Handler _profilerHandler = new(Looper.MainLooper!);
    private bool _profilerVisible;
    private int _gc0, _gc1, _gc2;
    private readonly bool _highRefresh;
    private SensorManager? _sensors;
    private bool _paused;
    private bool _disposed;
    private GamepadButtons _padButtons;
    private System.Numerics.Vector2 _padLeft, _padRight;
    private float _padLeftTrigger, _padRightTrigger;
    private bool _padSeen;

    public PreviewHost(Activity activity, PreviewRenderer renderer, Func<IReadOnlyList<string>> projectFiles, Action onStop, bool highRefresh) : base(activity)
    {
        _activity = activity;
        _renderer = renderer;
        _highRefresh = highRefresh;
        var density = activity.Resources!.DisplayMetrics!.Density;

        _glView = new GLSurfaceView(activity);
        _glView.SetEGLContextClientVersion(3);
        _glView.PreserveEGLContextOnPause = true; // ao voltar do segundo plano o jogo continua, se o driver mantiver o contexto
        _glView.SetRenderer(renderer);
        _glView.RenderMode = Rendermode.Continuously;
        _glView.Touch += OnTouch;
        AddView(_glView, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

        var controls = new LinearLayout(activity) { Orientation = Orientation.Horizontal };
        controls.SetBackgroundColor(AndroidColor.Argb(140, 0, 0, 0));
        controls.AddView(MakeButton("■ Stop", onStop));
        controls.AddView(MakeButton("↻", renderer.RequestRestart));
        Button? pause = null;
        pause = MakeButton("⏸", () =>
        {
            _paused = !_paused;
            renderer.SetPaused(_paused);
            pause!.Text = _paused ? "▶" : "⏸";
        });
        controls.AddView(pause);
        controls.AddView(MakeButton("⏭", renderer.RequestStep));
        controls.AddView(MakeButton("🔍", ToggleInspector));
        controls.AddView(MakeButton("▥ Perf", ToggleProfiler));
        AddView(controls, new LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Top | GravityFlags.Left));

        _inspector = new InspectorPanel(activity, () => renderer.CurrentGame, projectFiles) { Visibility = ViewStates.Gone };
        AddView(_inspector, new LayoutParams((int)(320 * density), ViewGroup.LayoutParams.MatchParent, GravityFlags.Right));

        _profiler = new TextView(activity) { TextSize = 12, Clickable = false, Focusable = false };
        _profiler.SetTextColor(AndroidColor.White);
        _profiler.SetBackgroundColor(AndroidColor.Argb(228, 15, 22, 33));
        _profiler.SetPadding((int)(10 * density), (int)(10 * density), (int)(10 * density), (int)(10 * density));
        _profiler.Visibility = ViewStates.Gone;
        AddView(_profiler, new LayoutParams((int)(238 * density),
            ViewGroup.LayoutParams.WrapContent, GravityFlags.Top | GravityFlags.Right)
        {
            TopMargin = (int)(56 * density),
            RightMargin = (int)(8 * density)
        });

        _console = new TextView(activity) { TextSize = 11, Clickable = false, Focusable = false };
        _console.SetTextColor(AndroidColor.White);
        _console.SetBackgroundColor(AndroidColor.Argb(110, 0, 0, 0));
        AddView(_console, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Bottom));

        RequestHighRefreshRate(highRefresh);
        _glView.LayoutChange += (_, _) => UpdateDisplayInfo();
        _glView.Post(UpdateDisplayInfo);
        StartSensors();
    }

    private Button MakeButton(string label, Action action)
    {
        var button = new Button(_activity) { Text = label };
        button.SetAllCaps(false);
        button.Click += (_, _) => action();
        return button;
    }

    public void ToggleInspector()
    {
        if (_inspector.Visibility != ViewStates.Visible) HideProfiler();
        _inspector.Visibility = _inspector.Visibility == ViewStates.Visible ? ViewStates.Gone : ViewStates.Visible;
    }

    private void ToggleProfiler()
    {
        if (_profilerVisible)
        {
            HideProfiler();
            return;
        }
        _inspector.Visibility = ViewStates.Gone;
        _profilerVisible = true;
        _profiler.Visibility = ViewStates.Visible;
        _gc0 = GC.CollectionCount(0);
        _gc1 = GC.CollectionCount(1);
        _gc2 = GC.CollectionCount(2);
        _renderer.SetProfilingEnabled(true);
        _profilerHandler.RemoveCallbacksAndMessages(null);
        RefreshProfiler();
    }

    private void HideProfiler()
    {
        _profilerVisible = false;
        _profiler.Visibility = ViewStates.Gone;
        _renderer.SetProfilingEnabled(false);
        _profilerHandler.RemoveCallbacksAndMessages(null);
    }

    private void RefreshProfiler()
    {
        if (_disposed || !_profilerVisible) return;
        var current = _renderer.Performance;
        if (current.Samples == 0)
            _profiler.Text = "PROFILER  |  coletando...";
        else
        {
            double heapMb = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            string paused = _renderer.ProfilerIsPaused ? "  [PAUSADO]" : "";
            _profiler.Text =
                $"PROFILER  |  {current.Samples} quadros{paused}\n" +
                $"FPS: {current.FramesPerSecond:F1}  Frame: {current.FrameMilliseconds:F2} ms\n" +
                $"CPU Tick: {current.CpuTickMilliseconds:F2} ms\n" +
                (current.PhasedSamples == 0 ? "CPU Update/Draw: nao medidos\n"
                    : $"CPU Update: {current.UpdateCpuMilliseconds:F2} ms  ({current.UpdateStepsPerFrame:F1} passos)\n" +
                      $"CPU Draw: {current.DrawCpuMilliseconds:F2} ms\n") +
                $"Draw calls: {current.DrawCallsPerFrame:F1}  Tris: {current.TrianglesPerFrame:F0}\n" +
                $"Alloc GL: {current.AllocatedBytesPerFrame:F0} B/frame\n" +
                $"Heap .NET: {heapMb:F1} MiB\n" +
                $"GC (0/1/2): {GC.CollectionCount(0) - _gc0}/{GC.CollectionCount(1) - _gc1}/{GC.CollectionCount(2) - _gc2}\n" +
                $"Frames > 33 ms: {current.SlowFrames}/{current.Samples}\n" +
                "GPU e audio underruns: nao medidos";
        }
        _profilerHandler.PostDelayed(RefreshProfiler, 300);
    }

    /// <summary>Mostra as últimas linhas do console sobre o jogo.</summary>
    public void SetConsole(string text) => _console.Text = text;

    public void Pause()
    {
        _profilerHandler.RemoveCallbacksAndMessages(null);
        _renderer.SetAppPaused(true);
        _glView.OnPause();
        StopSensors();
    }

    public void Resume()
    {
        if (_profilerVisible) _profilerHandler.Post(RefreshProfiler);
        _glView.OnResume();
        _renderer.SetAppPaused(false);
        StartSensors();
    }

    /// <summary>Encerra o jogo e libera os recursos gráficos.</summary>
    public void Shutdown()
    {
        if (_disposed) return;
        _disposed = true;
        HideProfiler();
        StopSensors();
        RequestHighRefreshRate(false);
        _glView.Touch -= OnTouch;
        var renderer = _renderer;
        _glView.QueueEvent(() => renderer.Shutdown());
        _glView.OnPause();
    }

    // ---------- Tela ----------

    /// <summary>Pede ao sistema o modo de tela de maior taxa de atualização (mesma resolução), para 90/120 Hz onde houver.</summary>
    private void RequestHighRefreshRate(bool enable)
    {
        if (enable && !_highRefresh) return;
        try
        {
            var window = _activity.Window;
            var attributes = window?.Attributes;
            var display = _activity.WindowManager?.DefaultDisplay;
            if (window is null || attributes is null || display is null) return;
            if (!enable)
            {
                attributes.PreferredDisplayModeId = 0;
            }
            else
            {
                var current = display.GetMode();
                var best = display.GetSupportedModes()?
                    .Where(m => m.PhysicalWidth == current.PhysicalWidth && m.PhysicalHeight == current.PhysicalHeight)
                    .OrderByDescending(m => m.RefreshRate)
                    .FirstOrDefault();
                if (best is null) return;
                attributes.PreferredDisplayModeId = best.ModeId;
            }
            window.Attributes = attributes;
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or InvalidOperationException)
        {
            // Não é essencial: sem alta taxa, o jogo roda em 60 Hz.
        }
    }

    /// <summary>Envia ao jogo a densidade da tela e os recuos seguros (recorte de câmera, cantos arredondados).</summary>
    private void UpdateDisplayInfo()
    {
        int left = 0, top = 0, right = 0, bottom = 0;
        var cutout = _glView.RootWindowInsets?.DisplayCutout;
        if (cutout is not null)
        {
            var location = new int[2];
            _glView.GetLocationInWindow(location);
            left = Math.Max(0, cutout.SafeInsetLeft - location[0]);
            top = Math.Max(0, cutout.SafeInsetTop - location[1]);
            right = cutout.SafeInsetRight;
            bottom = cutout.SafeInsetBottom;
        }
        _renderer.SetDisplay(Resources!.DisplayMetrics!.Density, left, top, right, bottom);
    }

    // ---------- Sensores ----------

    private void StartSensors()
    {
        if (_sensors is not null || _disposed) return;
        _sensors = _activity.GetSystemService(global::Android.Content.Context.SensorService) as SensorManager;
        var accelerometer = _sensors?.GetDefaultSensor(SensorType.Accelerometer);
        if (accelerometer is not null) _sensors!.RegisterListener(this, accelerometer, SensorDelay.Game);
        var gyroscope = _sensors?.GetDefaultSensor(SensorType.Gyroscope);
        if (gyroscope is not null) _sensors!.RegisterListener(this, gyroscope, SensorDelay.Game);
    }

    private void StopSensors()
    {
        _sensors?.UnregisterListener(this);
        _sensors = null;
    }

    public void OnAccuracyChanged(Sensor? sensor, SensorStatus accuracy) { }

    public void OnSensorChanged(SensorEvent? e)
    {
        if (e?.Values is not { Count: >= 3 } v) return;
        var value = new System.Numerics.Vector3(v[0], v[1], v[2]);
        if (e.Sensor?.Type == SensorType.Gyroscope) _renderer.SetGyroscope(value);
        else _renderer.SetAccelerometer(value);
    }

    // ---------- Entrada ----------

    public bool OnGenericMotion(MotionEvent? e)
    {
        if (e is null || (e.Source & InputSourceType.Joystick) != InputSourceType.Joystick || e.Action != MotionEventActions.Move) return false;
        _padSeen = true;
        _padLeft = new System.Numerics.Vector2(e.GetAxisValue(global::Android.Views.Axis.X), e.GetAxisValue(global::Android.Views.Axis.Y));
        _padRight = new System.Numerics.Vector2(e.GetAxisValue(global::Android.Views.Axis.Z), e.GetAxisValue(global::Android.Views.Axis.Rz));
        _padLeftTrigger = Math.Max(e.GetAxisValue(global::Android.Views.Axis.Ltrigger), e.GetAxisValue(global::Android.Views.Axis.Brake));
        _padRightTrigger = Math.Max(e.GetAxisValue(global::Android.Views.Axis.Rtrigger), e.GetAxisValue(global::Android.Views.Axis.Gas));
        var hatX = e.GetAxisValue(global::Android.Views.Axis.HatX);
        var hatY = e.GetAxisValue(global::Android.Views.Axis.HatY);
        SetPadButton(GamepadButtons.DPadLeft, hatX < -0.5f);
        SetPadButton(GamepadButtons.DPadRight, hatX > 0.5f);
        SetPadButton(GamepadButtons.DPadUp, hatY < -0.5f);
        SetPadButton(GamepadButtons.DPadDown, hatY > 0.5f);
        PushGamepad();
        return true;
    }

    private void SetPadButton(GamepadButtons button, bool down) =>
        _padButtons = down ? _padButtons | button : _padButtons & ~button;

    private void PushGamepad() =>
        _renderer.SetGamepad(new GamepadState(_padSeen, _padButtons, _padLeft, _padRight, _padLeftTrigger, _padRightTrigger));

    public bool OnKey(Keycode code, bool down, KeyEvent? e)
    {
        var fromGamepad = e is not null && (e.Source & InputSourceType.Gamepad) == InputSourceType.Gamepad;
        var button = code switch
        {
            Keycode.ButtonA => GamepadButtons.A,
            Keycode.ButtonB => GamepadButtons.B,
            Keycode.ButtonX => GamepadButtons.X,
            Keycode.ButtonY => GamepadButtons.Y,
            Keycode.ButtonL1 => GamepadButtons.LeftShoulder,
            Keycode.ButtonR1 => GamepadButtons.RightShoulder,
            Keycode.ButtonStart => GamepadButtons.Start,
            Keycode.ButtonSelect => GamepadButtons.Back,
            Keycode.ButtonThumbl => GamepadButtons.LeftStick,
            Keycode.ButtonThumbr => GamepadButtons.RightStick,
            Keycode.DpadLeft when fromGamepad => GamepadButtons.DPadLeft,
            Keycode.DpadRight when fromGamepad => GamepadButtons.DPadRight,
            Keycode.DpadUp when fromGamepad => GamepadButtons.DPadUp,
            Keycode.DpadDown when fromGamepad => GamepadButtons.DPadDown,
            _ => GamepadButtons.None,
        };
        if (button != GamepadButtons.None)
        {
            _padSeen = true;
            SetPadButton(button, down);
            PushGamepad();
            if (code is not (Keycode.DpadLeft or Keycode.DpadRight or Keycode.DpadUp or Keycode.DpadDown)) return true;
        }
        var key = code switch
        {
            >= Keycode.A and <= Keycode.Z => (Keys)((int)Keys.A + (code - Keycode.A)),
            >= Keycode.Num0 and <= Keycode.Num9 => (Keys)((int)Keys.D0 + (code - Keycode.Num0)),
            Keycode.Space => Keys.Space,
            Keycode.Enter => Keys.Enter,
            Keycode.Escape => Keys.Escape,
            Keycode.Tab => Keys.Tab,
            Keycode.ShiftLeft or Keycode.ShiftRight => Keys.Shift,
            Keycode.DpadLeft => Keys.Left,
            Keycode.DpadRight => Keys.Right,
            Keycode.DpadUp => Keys.Up,
            Keycode.DpadDown => Keys.Down,
            _ => Keys.None,
        };
        if (key == Keys.None) return false;
        _renderer.SetKey(key, down);
        return true;
    }

    private void OnTouch(object? sender, View.TouchEventArgs args)
    {
        var e = args.Event;
        if (e is null) return;
        var touches = new List<TouchPoint>(e.PointerCount);
        var action = e.ActionMasked;
        var index = e.ActionIndex;
        for (var i = 0; i < e.PointerCount; i++)
        {
            var phase = TouchPhase.Moved;
            if (action is MotionEventActions.Down or MotionEventActions.PointerDown && i == index) phase = TouchPhase.Pressed;
            else if (action is MotionEventActions.Up or MotionEventActions.PointerUp && i == index) phase = TouchPhase.Released;
            else if (action == MotionEventActions.Cancel) phase = TouchPhase.Cancelled;
            touches.Add(new TouchPoint(e.GetPointerId(i), phase, new System.Numerics.Vector2(e.GetX(i), e.GetY(i))));
        }
        _renderer.SetTouches(touches.ToArray());
        args.Handled = true;
    }
}
