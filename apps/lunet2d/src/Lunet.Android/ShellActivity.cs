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
        _shell = new LunetShell(ProjectsRoot(this)) { OpenRoute = OpenRoute };
        Content = _shell;
        ApplyDevice(Resources?.Configuration);
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

    /// <summary>A physical keyboard types directly; with a screen reader the system keyboard is the default.</summary>
    private void ApplyDevice(global::Android.Content.Res.Configuration? config)
    {
        if (_shell is null || config is null) return;
        _shell.HardwareKeyboard = config.Keyboard == global::Android.Content.Res.KeyboardType.Qwerty
            && config.HardKeyboardHidden == global::Android.Content.Res.HardKeyboardHidden.No;
        if (GetSystemService(AccessibilityService) is global::Android.Views.Accessibility.AccessibilityManager { IsTouchExplorationEnabled: true })
            _shell.PreferSystemKeyboard = true;
    }

#if LUNET_EMULATOR
    /// <summary>
    /// CI emulator only (tools/ci-android-shell.sh): with the extra <c>lunet.e2e</c>, create a game, compile and run it
    /// in the shell, then type with the Lunet keyboard — the journey a person makes with a few taps.
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
                shell.PreferSystemKeyboard = false;
                editor.TextBox.Focus();
                editor.ShowKeyboard();
                editor.TextBox.CaretIndex = 0;
                foreach (var c in "// ola")
                    editor.Keyboard?.Press(new Lunet.Client.Shell.KeyDef(c == ' ' ? "espaço" : c.ToString(),
                        c == ' ' ? Lunet.Client.Shell.KeyAction.Space : Lunet.Client.Shell.KeyAction.Text));
                await Task.Delay(1500);
                global::Android.Util.Log.Info("lunet-e2e", $"teclado={(editor.KeyboardVisible ? "visivel" : "oculto")} ime={(Avalonia.Input.InputMethod.GetIsInputMethodEnabled(editor.TextBox) ? "on" : "off")} texto={(editor.TextBox.Text ?? "").Split('\n')[0]}");
            }
            catch (Exception ex) { global::Android.Util.Log.Error("lunet-e2e", "falhou: " + ex); }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
#endif
}
