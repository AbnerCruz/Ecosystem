using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Urbe.Client;

/// <summary>Avalonia application shared by the Desktop and Android heads (ADR-0032).</summary>
public sealed class UrbeApp : Application
{
    /// <summary>Injected by each native head; remains null for headless/desktop previews.</summary>
    public static Func<IUrbeVaultStorage>? VaultStorageFactory { get; set; }

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                var view = CreateMainView();
                var window = new Window
                {
                    Title = "Urbe",
                    Width = 1280,
                    Height = 800,
                    Background = UrbeTheme.Brush(UrbeTheme.Bg),
                    Content = view
                };
                // workspace.js flushPending on pagehide: a pending map save is written before the window closes
                bool flushed = false;
                window.Closing += async (_, e) =>
                {
                    if (flushed || view.MapSave is not { HasPending: true }) return;
                    e.Cancel = true;
                    try { await view.FlushMapAsync(); }
                    catch { /* the error state was shown; closing must not be blocked forever */ }
                    flushed = true;
                    window.Close();
                };
                desktop.MainWindow = window;
                break;
            case IActivityApplicationLifetime activity:
                activity.MainViewFactory = CreateMainView;
                break;
            case ISingleViewApplicationLifetime single:
                single.MainView = CreateMainView();
                break;
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>The view of the running app (the Android head flushes its map on pause).</summary>
    public static MainView? ActiveView { get; private set; }

    public static MainView CreateMainView()
    {
        var view = new MainView();
        ActiveView = view;
        _ = view.InitializeAsync();
        return view;
    }

    /// <summary>
    /// workspace.js flushPending (visibilitychange hidden): the app goes to the background, so a
    /// pending map save is started now. Fire-and-forget: the platform does not wait for it.
    /// </summary>
    public static void FlushPendingOnBackground()
    {
        if (ActiveView is not { } view) return;
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try { await view.FlushMapAsync(); }
            catch { /* shown by the save state */ }
        });
    }
}

/// <summary>
/// Preview content: the 1.8.4 Tutorial folder (apps/urbe/tutorial), embedded read-only, in the
/// order of tutorial.js C.files (build-tutorial.mjs: ordinal sort of the paths).
/// Opening a real vault folder is the next UC-19/UC-25 slice; nothing is written anywhere.
/// </summary>
public static class TutorialNotes
{
    private const string Prefix = "tutorial/";

    public static IReadOnlyList<CityNote> Load()
    {
        var assembly = typeof(TutorialNotes).Assembly;
        var notes = new List<CityNote>();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var normalized = name.Replace('\\', '/');
            if (!normalized.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            notes.Add(new CityNote("Tutorial/" + normalized[Prefix.Length..], reader.ReadToEnd().Replace("\r\n", "\n")));
        }
        return notes.OrderBy(n => n.Path, StringComparer.Ordinal).ToList();
    }
}
