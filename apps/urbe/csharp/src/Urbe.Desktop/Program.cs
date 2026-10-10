using Avalonia;
using Urbe.Client;

namespace Urbe.Desktop;

public static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<UrbeApp>().UsePlatformDetect();
}
