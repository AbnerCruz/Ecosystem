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
            }
            catch (Exception ex) { Android.Util.Log.Error("lunet-e2e", "falhou: " + ex); }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
#endif

    protected override void OnPause()
    {
        // Leaving the app saves the open documents (verified write) — Android may kill the process afterwards.
        if (Avalonia.Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime { MainView: LunetShell shell })
            shell.SaveAll();
        base.OnPause();
    }
}
