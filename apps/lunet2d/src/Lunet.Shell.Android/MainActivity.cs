using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls.ApplicationLifetimes;
using Lunet.Client;

namespace Lunet.ShellAndroid;

/// <summary>The Avalonia application of the Android head: the shared Lunet app, with the projects folder of this app.</summary>
public sealed class AndroidLunetApp : LunetApp
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            var files = Android.App.Application.Context.FilesDir?.AbsolutePath ?? Path.GetTempPath();
            // Projects live in the app's own storage, like the current Lunet app (study 25/11 §1).
            single.MainView = new LunetShell(Path.Combine(files, "projects"));
        }
        base.OnFrameworkInitializationCompleted();
    }
}

[Application(Label = "Lunet Nova", Icon = "@mipmap/icon")]
public sealed class LunetAndroidApplication : AvaloniaAndroidApplication<AndroidLunetApp>
{
    public LunetAndroidApplication(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer) { }
}

/// <summary>
/// The only activity. adjustResize lets the system shrink the window for the keyboard up to Android 14; on 15+ the
/// window is edge-to-edge and <see cref="LunetShell"/> fits the keyboard itself (KeyboardFit). Back goes to the shell.
/// </summary>
[Activity(
    Label = "Lunet Nova",
    Theme = "@style/LunetTheme",
    Icon = "@mipmap/icon",
    MainLauncher = true,
    Exported = true,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard |
        ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public sealed class MainActivity : AvaloniaMainActivity
{
#if LUNET_EMULATOR
    /// <summary>
    /// CI emulator only (tools/ci-android-shell.sh): with the extra <c>lunet.e2e</c>, create a game, compile it with
    /// Roslyn and run it inside the shell, then log the result — the journey a person makes with three taps.
    /// </summary>
    protected override void OnResume()
    {
        base.OnResume();
        if (Intent?.GetBooleanExtra("lunet.e2e", false) != true) return;
        Intent.RemoveExtra("lunet.e2e");
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(1500);
            if (Avalonia.Application.Current?.ApplicationLifetime is not ISingleViewApplicationLifetime { MainView: LunetShell shell }) return;
            try
            {
                shell.CreateProject();
                Android.Util.Log.Info("lunet-e2e", "projeto criado: " + shell.State.Project);
                shell.RunGame();
                await Task.Delay(4000);
                Android.Util.Log.Info("lunet-e2e", $"estado={shell.Game.State} quadros={shell.Game.FramesRendered} problemas={shell.Game.Problems.Count}");
                // The IDE keyboard: back to the code, type with it, and report whether the system keyboard stepped aside.
                shell.StopGame();
                shell.State.SelectTab(Lunet.Client.Shell.WorkspaceTab.Code);
                var editor = shell.WorkspaceView.Code;
                // The CI emulator announces a hardware keyboard (the Lunet keyboard rightly steps aside for it):
                // report what was detected, then act as a phone without one, the case the keyboard is for.
                Android.Util.Log.Info("lunet-e2e", $"teclado-fisico-detectado={shell.HardwareKeyboard}");
                shell.HardwareKeyboard = false;
                shell.PreferSystemKeyboard = false;
                editor.TextBox.Focus();
                editor.ShowKeyboard();
                editor.TextBox.CaretIndex = 0;
                foreach (var key in "// ola".Select(c => new Lunet.Client.Shell.KeyDef(c == ' ' ? "espaço" : c.ToString(), c == ' ' ? Lunet.Client.Shell.KeyAction.Space : Lunet.Client.Shell.KeyAction.Text)))
                    editor.Keyboard?.Press(key);
                await Task.Delay(1500);
                Android.Util.Log.Info("lunet-e2e", $"teclado={(editor.KeyboardVisible ? "visivel" : "oculto")} ime={(Avalonia.Input.InputMethod.GetIsInputMethodEnabled(editor.TextBox) ? "on" : "off")} texto={(editor.TextBox.Text ?? "").Split('\n')[0]}");
            }
            catch (Exception ex) { Android.Util.Log.Error("lunet-e2e", "falhou: " + ex); }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
#endif

    private static LunetShell? Shell =>
        Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: LunetShell shell } ? shell : null;

    /// <summary>
    /// Device facts the shell needs (study 25/11 §4): a physical keyboard types directly (the Lunet keyboard steps
    /// aside); with a screen reader on, the system keyboard is the default (TalkBack works best with it).
    /// </summary>
    private void ApplyDevice(Android.Content.Res.Configuration? config)
    {
        if (Shell is not { } shell || config is null) return;
        shell.HardwareKeyboard = config.Keyboard == Android.Content.Res.KeyboardType.Qwerty
            && config.HardKeyboardHidden == Android.Content.Res.HardKeyboardHidden.No;
        if (GetSystemService(AccessibilityService) is Android.Views.Accessibility.AccessibilityManager { IsTouchExplorationEnabled: true })
            shell.PreferSystemKeyboard = true;
    }

    protected override void OnPostResume()
    {
        base.OnPostResume();
        ApplyDevice(Resources?.Configuration);
    }

    public override void OnConfigurationChanged(Android.Content.Res.Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplyDevice(newConfig);
    }

    protected override void OnPause()
    {
        // Leaving the app saves the open documents (verified write) — Android may kill the process afterwards.
        if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: LunetShell shell })
            shell.SaveAll();
        base.OnPause();
    }
}
