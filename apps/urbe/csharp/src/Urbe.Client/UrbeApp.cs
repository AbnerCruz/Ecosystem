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

    /// <summary>A file in the app's own data for the Notas state kept on the device (favourites, recents); null keeps it for the session.</summary>
    public static string? LocalStatePath { get; set; }

    /// <summary>UC-78: a file in the app's own data with the names of the last vault opened (never a note's text); null keeps none.</summary>
    public static string? OpeningNamesPath { get; set; }

    /// <summary>A folder of the app's own data for the Assistente (conversations, providers and keys, memory); null keeps them for the session. Never the vault.</summary>
    public static string? AssistantFolder { get; set; }

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        CrashLog.Install();
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
                // workspace.js flushPending on pagehide: a pending save of the notes' record is written before the window closes
                bool flushed = false;
                window.Closing += async (_, e) =>
                {
                    if (flushed || view.NotesSave is not { HasPending: true }) return;
                    e.Cancel = true;
                    try { await view.FlushPendingAsync(); }
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

    /// <summary>The view of the running app (the Android head flushes its pending saves on pause).</summary>
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
    /// pending save of the notes' record is started now. Fire-and-forget: the platform does not wait for it.
    /// </summary>
    public static void FlushPendingOnBackground()
    {
        if (ActiveView is not { } view) return;
        _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try { await view.Editor.FlushAsync(); }
            catch { /* shown by the editor's save state */ }
            try { await view.FlushPendingAsync(); }
            catch { /* shown by the save state */ }
        });
    }
}

/// <summary>
/// The 1.8.4 Tutorial folder (apps/urbe/tutorial), embedded read-only, in the order of tutorial.js C.files
/// (build-tutorial.mjs: ordinal sort of the paths). The immutable reference, used by the oracles and tests; the app
/// shows its own documentation (<see cref="AppDocs"/>, UC-64). Nothing is written anywhere.
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
