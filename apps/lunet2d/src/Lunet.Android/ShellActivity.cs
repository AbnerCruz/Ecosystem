using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Lunet.Client;
using Lunet.Core;

namespace Lunet.Android;

/// <summary>
/// The entry of Lunet (ADR 0013 / ADD-0036, one app): the Avalonia shell. It is an <see cref="AvaloniaActivity"/>,
/// the base Avalonia documents for embedding into an existing Android app, so Avalonia starts only when this screen
/// opens — never in the <c>:gameplay</c> and <c>:preview</c> processes. It works on the same projects as the screens
/// still served by <see cref="MainActivity"/>, which it opens by route for what has not moved yet.
/// </summary>
[Activity(
    Label = "Lunet",
    Theme = "@style/LunetTheme",
    Icon = "@mipmap/icon",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard |
        ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public sealed class ShellActivity : AvaloniaActivity
{
    private LunetShell? _shell;

    /// <summary>The shell's Avalonia app (the shared Lunet look) for this Android app.</summary>
    private sealed class AndroidLunetApp : LunetApp;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // AvaloniaActivity.OnCreate requires a running Avalonia app, even when this screen only forwards a request.
        if (Avalonia.Application.Current is null)
            AppBuilder.Configure<AndroidLunetApp>().UseAndroid().SetupWithoutStarting();
#if LUNET_UI_DEMO
        // tools/ci-ui-screens.sh captures the screens still served by MainActivity: forward its demo requests there.
        if (Intent?.GetStringExtra("lunet.demo") is { Length: > 0 })
        {
            base.OnCreate(savedInstanceState);
            var demo = new Intent(this, typeof(MainActivity));
            demo.PutExtras(Intent!);
            StartActivity(demo);
            Finish();
            return;
        }
#endif
        base.OnCreate(savedInstanceState);
        CreateShell();
        // In an embedded AvaloniaActivity the system Back arrives here, not on the shell's TopLevel: route it, so
        // Back closes the keyboard, then the open panel or sheet, then the page, and only then leaves the app.
        BackRequested += (_, e) => { if (_shell?.Back() == true) e.Handled = true; };
        ApplyDevice(Resources?.Configuration);
        // UX P7 (LUNET-537 D7): keys and confirmations are felt, with the system's own haptic constants.
        Lunet.Client.Ui.Haptics.KeyTap = () => Window?.DecorView?.PerformHapticFeedback(FeedbackConstants.KeyboardTap);
        Lunet.Client.Ui.Haptics.Confirm = () => Window?.DecorView?.PerformHapticFeedback(
            OperatingSystem.IsAndroidVersionAtLeast(30) ? FeedbackConstants.Confirm : FeedbackConstants.ContextClick);
    }

    private string? _projectsRoot;

    /// <summary>Builds the shell over the current projects folder (again when Ajustes → Pasta de projetos changed it).</summary>
    private void CreateShell()
    {
        // M7: the AI uses the same settings file, Keystore vault and conversations as the previous screens: one key for all of Lunet.
        var ai = Path.Combine(FilesDir!.AbsolutePath, "ai");
        _projectsRoot = ProjectsRoot(this);
        _shell = new LunetShell(_projectsRoot)
        {
            OpenRoute = OpenRoute,
            // P3: the same declared progress as the previous Aprender screen (MainActivity.Learn).
            LearningProgressPath = Path.Combine(FilesDir!.AbsolutePath, "learning", "progress.json"),
            // A6: the same Biblioteca as the previous screen (MainActivity.AssetLibrary).
            LibraryPath = Path.Combine(FilesDir!.AbsolutePath, "Library"),
            // P4: the game runs full screen in the :gameplay process, prepared by the existing pipeline.
            PlayGame = game => OpenRoute("play-game", game),
            // A4: the sound Studio plays its WAV through MediaPlayer (a temp file in the cache).
            PlaySound = PlayWav,
            // P7: the same settings.json as the previous screens.
            SettingsPath = Path.Combine(FilesDir!.AbsolutePath, "settings.json"),
            KeepScreenOn = on => { if (on) Window?.AddFlags(WindowManagerFlags.KeepScreenOn); else Window?.ClearFlags(WindowManagerFlags.KeepScreenOn); },
            AiHost = new Lunet.Client.Views.AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(ai, "settings.json"),
                new AndroidKeystoreVault(Path.Combine(NoBackupFilesDir!.AbsolutePath, "secure"))), ai),
        };
        Content = _shell;
        // The code bar shows and hides the system keyboard (Avalonia exposes no call for it).
        _shell.SystemKeyboard = show =>
        {
            var view = Window?.DecorView?.FindFocus() ?? Window?.DecorView;
            if (view is null || GetSystemService(InputMethodService) is not global::Android.Views.InputMethods.InputMethodManager ime) return;
            if (show) ime.ShowSoftInput(view, global::Android.Views.InputMethods.ShowFlags.Implicit);
            else ime.HideSoftInputFromWindow(view.WindowToken, global::Android.Views.InputMethods.HideSoftInputFlags.None);
        };
    }

    private global::Android.Media.MediaPlayer? _player;

    private void PlayWav(byte[] wav)
    {
        try
        {
            var file = Path.Combine(CacheDir!.AbsolutePath, "studio-preview.wav");
            File.WriteAllBytes(file, wav);
            _player?.Release();
            _player = new global::Android.Media.MediaPlayer();
            _player.SetDataSource(file);
            _player.Prepare();
            _player.Start();
        }
        catch (Exception ex) when (ex is IOException or Java.Lang.Exception) { global::Android.Util.Log.Warn("Lunet.Sound", ex.Message); }
    }

    /// <summary>
    /// The projects folder the current Lunet uses (MainActivity.ProjectFolder): the shared working copy when the
    /// person chose one and it exists, else the app's private <c>Projects</c>. Same games in both screens.
    /// </summary>
    internal static string ProjectsRoot(global::Android.Content.Context context)
    {
        var files = context.FilesDir!.AbsolutePath;
        try
        {
            var settings = new SettingsStore(Path.Combine(files, "settings.json")).Load();
            if (settings.UseSharedProjectFolder && settings.ProjectWorkingCopy is { } copy
                && Directory.Exists(Path.Combine(files, "ProjectWorkingCopies", copy)))
                return Path.Combine(files, "ProjectWorkingCopies", copy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { }
        return Path.Combine(files, "Projects");
    }

    /// <summary>Opens a screen still served by MainActivity, after saving what the shell has open.</summary>
    private void OpenRoute(string route, string? project)
    {
        _shell?.SaveAll();
        _shell?.StopGame();
        StartActivity(MainActivity.RouteIntent(this, route, project));
    }

    protected override void OnResume()
    {
        base.OnResume();
        // Ajustes → Pasta de projetos may have switched the folder: the shell reopens over the new one.
        if (_shell is not null && ProjectsRoot(this) != _projectsRoot)
        {
            _shell.SaveAll();
            _shell.Shutdown();
            CreateShell();
            ApplyDevice(Resources?.Configuration);
            return;
        }
        // Back from a routed screen: those screens may have changed the games (AI edits, Git, Studios).
        _shell?.ReturnedFromRoute();
    }

    protected override void OnPause()
    {
        _shell?.SaveAll(); // Android may kill the process after this
        base.OnPause();
    }

    public override void OnConfigurationChanged(global::Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplyDevice(newConfig);
    }

    /// <summary>A physical keyboard has its own symbols: the code bar steps aside.</summary>
    private void ApplyDevice(global::Android.Content.Res.Configuration? config)
    {
        if (_shell is null || config is null) return;
        _shell.HardwareKeyboard = config.Keyboard == global::Android.Content.Res.KeyboardType.Qwerty
            && config.HardKeyboardHidden == global::Android.Content.Res.HardKeyboardHidden.No;
    }

#if LUNET_EMULATOR
    /// <summary>
    /// CI emulator only (tools/ci-android-shell.sh): with the extra <c>lunet.e2e</c>, create a game, compile and run it
    /// in the shell, then use the code bar over the system keyboard — the journey a person makes with a few taps.
    /// </summary>
    protected override void OnPostResume()
    {
        base.OnPostResume();
        if (Intent?.GetBooleanExtra("lunet.e2e", false) != true || _shell is not { } shell) return;
        Intent.RemoveExtra("lunet.e2e");
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(1500);
            try
            {
                shell.CreateProject();
                global::Android.Util.Log.Info("lunet-e2e", "projeto criado: " + shell.State.Project);
                shell.RunGame();
                await Task.Delay(4000);
                global::Android.Util.Log.Info("lunet-e2e", $"estado={shell.Game.State} quadros={shell.Game.FramesRendered} problemas={shell.Game.Problems.Count}");
                shell.StopGame();
                shell.State.SelectTab(Lunet.Client.Shell.WorkspaceTab.Code);
                var editor = shell.WorkspaceView.Code;
                global::Android.Util.Log.Info("lunet-e2e", $"teclado-fisico-detectado={shell.HardwareKeyboard}");
                shell.HardwareKeyboard = false;
                editor.Surface.Focus();
                editor.ShowKeyboard();
                editor.Surface.Caret = 0;
                editor.Keyboard?.Shortcut("{");
                await Task.Delay(1500);
                global::Android.Util.Log.Info("lunet-e2e", $"barra={(editor.KeyboardVisible ? "visivel" : "oculta")} ime={(Avalonia.Input.InputMethod.GetIsInputMethodEnabled(editor.Surface.InputTarget) ? "on" : "off")} texto={(editor.Surface.Text).Split('\n')[0]}");
            }
            catch (Exception ex) { global::Android.Util.Log.Error("lunet-e2e", "falhou: " + ex); }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
#endif
}
