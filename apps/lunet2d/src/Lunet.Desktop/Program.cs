using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace Lunet.Desktop;

/// <summary>Desktop host of the LUNET-482 spike (DEC-0062 A): Windows, Linux and macOS with one Avalonia shell.</summary>
public static class Program
{
    /// <summary>Started before Avalonia initializes, to report cold start to first frame.</summary>
    public static readonly Stopwatch SinceLaunch = Stopwatch.StartNew();

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--measure")
            return SpikeMeasurement.Run(args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "lunet-measure"));
        return AppBuilder.Configure<DesktopApp>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}

public sealed class DesktopApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var root = desktop.Args is { Length: > 0 } a ? a[0]
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Lunet");
            desktop.MainWindow = new MainWindow(root);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
