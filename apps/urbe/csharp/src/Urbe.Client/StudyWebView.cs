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
}

public static class StudyWebViews
{
    public static Func<string, Control?>? CreateBrowser { get; set; }
}
