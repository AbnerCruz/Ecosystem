using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Urbe.Client;

/// <summary>Avalonia application shared by the Desktop and Android heads (ADR-0032).</summary>
public sealed class UrbeApp : Application
{
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
                desktop.MainWindow = new Window
                {
                    Title = "Urbe",
                    Width = 1280,
                    Height = 800,
                    Background = UrbeTheme.Brush(UrbeTheme.Bg),
                    Content = CreateMainView()
                };
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

    public static MainView CreateMainView()
    {
        var view = new MainView();
        _ = view.LoadAsync(TutorialNotes.Load());
        return view;
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
