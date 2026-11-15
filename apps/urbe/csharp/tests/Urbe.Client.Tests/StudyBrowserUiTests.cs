using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private sealed class FakeStudyWeb : Border, IStudyWebView
    {
        private readonly List<string> _history = [];
        private int _index = -1;
        public string Address => _index < 0 ? "" : _history[_index];
        public bool CanGoBack => _index > 0;
        public bool CanGoForward => _index < _history.Count - 1;
        public event Action? NavigationChanged;
        public void Navigate(string address) { _history.RemoveRange(_index + 1, _history.Count - _index - 1); _history.Add(address); _index++; NavigationChanged?.Invoke(); }
        public void GoBack() { if (CanGoBack) { _index--; NavigationChanged?.Invoke(); } }
        public void GoForward() { if (CanGoForward) { _index++; NavigationChanged?.Invoke(); } }
        public void ReloadPage() { }
        public Task<string> SelectedTextAsync() => Task.FromResult("Trecho selecionado no site.");
    }
    [Fact]
    public async Task StudyBrowser_NavigatesBackAndQuotesOnlyExplicitSelection() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var old = StudyWebViews.CreateBrowser;
        try
        {
            StudyWebViews.CreateBrowser = address => { var web = new FakeStudyWeb(); web.Navigate(address); return web; };
            var panel = new StudyBrowserPanel();
            Assert.True(panel.Open("https://example.org/one"));
            Assert.True(panel.Navigate("https://example.org/two"));
            Assert.True(panel.Back());
            Assert.EndsWith("/one", panel.Address);
            Assert.False(panel.Navigate("file:///private/vault.md"));
            WebReaderQuote? result = null;
            panel.QuoteRequested += quote => result = quote;
            ClickButtonWithText(panel, "Adicionar seleção à nota");
            await Task.Yield();
            Assert.Equal("Trecho selecionado no site.", result!.Text);
            Assert.Equal("https://example.org/one", result.Url);
            panel.Close(); Assert.False(panel.IsVisible);
            return true;
        }
        finally { StudyWebViews.CreateBrowser = old; }
    }, CancellationToken.None));

    private sealed class FakeVideoWeb : Border, IStudyWebView, IReaderMediaPosition
    {
        public double? Second { get; set; }
        public string Address { get; private set; } = "";
        public bool CanGoBack => false;
        public bool CanGoForward => false;
        public event Action? NavigationChanged;
        public void Navigate(string address) { Address = address; NavigationChanged?.Invoke(); }
        public void GoBack() { }
        public void GoForward() { }
        public void ReloadPage() { }
        public Task<string> SelectedTextAsync() => Task.FromResult("");
        public Task<double?> ReadMediaSecondAsync() => Task.FromResult(Second);
        public Task SeekMediaAsync(double seconds) => Task.CompletedTask;
    }

    [Fact]
    public async Task StudyBrowser_MarkingAVideoTakesThePlayersSecondWithoutTypingIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var old = StudyWebViews.CreateBrowser;
        try
        {
            FakeVideoWeb? web = null;
            StudyWebViews.CreateBrowser = address => { web = new FakeVideoWeb(); web.Navigate(address); return web; };
            var panel = new StudyBrowserPanel();
            Assert.True(panel.Open("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=10"));
            WebReaderQuote? result = null;
            panel.QuoteRequested += quote => result = quote;
            web!.Second = 205.7;
            ClickButtonWithText(panel, "Anotar instante");
            for (int i = 0; i < 5; i++) await Task.Yield();
            Assert.Equal(205, result!.Second);
            Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=205", result.Url);
            Assert.Equal("Vídeo em 3:25", result.Text);
            // no player time (the page did not load its video): the second typed in the field
            web.Second = null;
            panel.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Width == 72).Text = "3727";
            ClickButtonWithText(panel, "Anotar instante");
            for (int i = 0; i < 5; i++) await Task.Yield();
            Assert.Equal("Vídeo em 1:02:07", result.Text);
            return true;
        }
        finally { StudyWebViews.CreateBrowser = old; }
    }, CancellationToken.None));
}
