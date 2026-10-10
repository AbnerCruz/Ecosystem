using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Avalonia.Android;
using Urbe.Client;

namespace Urbe.AndroidHost;

[Application(Label = "Urbe nativo (prévia)", Icon = "@drawable/icon")]
public sealed class UrbeAndroidApplication(IntPtr javaReference, JniHandleOwnership transfer)
    : AvaloniaAndroidApplication<UrbeApp>(javaReference, transfer);

[Activity(
    Label = "Urbe nativo (prévia)",
    Theme = "@style/UrbeTheme",
    Icon = "@drawable/icon",
    MainLauncher = true,
    // Soft keyboard resizes the view so the editor caret stays visible (1.8.4 REQ-107 behaviour).
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Keyboard |
        ConfigChanges.KeyboardHidden | ConfigChanges.Density)]
public sealed class MainActivity : AvaloniaMainActivity
{
}
