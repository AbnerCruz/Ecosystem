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

    /// <summary>UC-24/UC-25: the file of this platform in a release (null: no update check here).</summary>
    public static Func<string, string>? UpdateAsset { get; set; }

    /// <summary>The installed version (e.g. 2.0.0-beta.18), or null when the head does not know it.</summary>
    public static string? InstalledVersion { get; set; }

    /// <summary>UC-66: the desktop head adds the tray icon and the global shortcut to the window once it exists.</summary>
    public static Action<Window>? DesktopWindowReady { get; set; }

    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        Ui.TextActions.Install();
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
                SaveBeforeClosing(window, view);
                desktop.MainWindow = window;
                DesktopWindowReady?.Invoke(window);
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

    /// <summary>
    /// CST-013: closing the window (the ✕, Alt+F4, the system shutting down) waits for what is not written yet: the
    /// open note's text typed since the last autosave, then the notes' record (workspace.js flushPending on pagehide).
    /// Only the record was waited for: the last seconds of typing were lost with the window. When the note cannot be
    /// saved, the window stays open unless the person chooses to close without saving.
    /// </summary>
    public static void SaveBeforeClosing(Window window, MainView view)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(view);
        bool done = false;
        window.Closing += async (_, e) =>
        {
            bool typed = view.Editor.Current is not null && view.Editor.HasChanges;
            if (done || (!typed && view.NotesSave is not { HasPending: true })) return;
            e.Cancel = true;
            if (typed && !await view.Editor.SaveAsync() &&
                !await view.Dialogs.ConfirmAsync("A nota não foi salva",
                    "O que foi escrito desde a última gravação de “" + System.IO.Path.GetFileNameWithoutExtension(view.Editor.Current!.Path) +
                    "” não está no vault. Se fechar agora, esse texto se perde.", "Fechar sem salvar", danger: true, cancel: "Continuar no Urbe"))
                return;
            try { await view.FlushPendingAsync(); }
            catch { /* the error state was shown; closing must not be blocked forever */ }
            done = true;
            window.Close();
        };
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
