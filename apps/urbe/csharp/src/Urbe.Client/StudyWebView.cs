using Avalonia.Controls;

namespace Urbe.Client;

public enum StudyAnnotationKind { Citation, Highlight, Bookmark, Comment }

/// <summary>Remote browser content has navigation and user-initiated selection, never vault capabilities.</summary>
public interface IReaderTextSelection
{
    Task<string> SelectedTextAsync();
}
public interface IReaderComputation
{
    Task<string> ReadComputationAsync();
}
public interface IReaderSlideRenderer
{
    Task ShowReaderPartAsync(int index);
}

public interface IStudyWebView : IReaderTextSelection
{
    string Address { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    string? Error => null;
    event Action? NavigationChanged;
    void Navigate(string address);
    void GoBack();
    void GoForward();
    void ReloadPage();
    /// <summary>The page as loaded now (its HTML), read only when the person asks for Modo leitura or to keep it; "" when unknown.</summary>
    Task<string> PageHtmlAsync() => Task.FromResult("");
}

public static class StudyPageScripts
{
    public const string Html = "document.documentElement ? document.documentElement.outerHTML : ''";
}

public static class StudyWebViews
{
    public static Func<string, Control?>? CreateBrowser { get; set; }
}
