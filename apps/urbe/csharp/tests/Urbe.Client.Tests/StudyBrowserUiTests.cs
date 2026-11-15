using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;

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

    private sealed class FakeArticleWeb : Border, IStudyWebView
    {
        public string Html { get; set; } = "<html><head><title>Fotossíntese</title></head><body><nav>menu</nav><article><h1>Fotossíntese</h1><p>As plantas usam luz.</p></article></body></html>";
        public string Address { get; private set; } = "";
        public bool CanGoBack => false;
        public bool CanGoForward => false;
        public event Action? NavigationChanged;
        public void Navigate(string address) { Address = address; NavigationChanged?.Invoke(); }
        public void GoBack() { }
        public void GoForward() { }
        public void ReloadPage() { }
        public Task<string> SelectedTextAsync() => Task.FromResult("");
        public Task<string> PageHtmlAsync() => Task.FromResult(Html);
    }

    [Fact]
    public async Task StudyBrowser_ReaderModeShowsTheArticleNativelyAndSavingHandsItsTextOver() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var old = StudyWebViews.CreateBrowser;
        try
        {
            FakeArticleWeb? web = null;
            StudyWebViews.CreateBrowser = address => { web = new FakeArticleWeb(); web.Navigate(address); return web; };
            var panel = new StudyBrowserPanel();
            var window = new Window { Width = 360, Height = 700, Content = panel };
            window.Show();
            Assert.True(panel.Open("https://example.org/bio"));
            async Task Menu(string item)
            {
                var more = panel.GetLogicalDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Mais ações" || ToolTip.GetTip(b) as string == "Mais ações");
                more.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                for (int i = 0; i < 5; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Yield(); }
                var menu = more.ContextMenu!.ItemsSource!.Cast<MenuItem>().First(m => m.Header as string == item);
                more.ContextMenu.Close();
                menu.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                for (int i = 0; i < 10; i++) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); await Task.Yield(); }
            }
            web!.Html = "<title>Fotossíntese</title><article><h1>Fotossíntese</h1><p>As plantas usam <a href='/luz'>luz</a> para transformar água e gás carbônico em açúcar.</p><h2>Etapas</h2><ul><li>Fase clara</li><li>Fase escura</li></ul></article>";
            await Menu("Modo leitura");
            Assert.False(web.IsVisible);
            if (Environment.GetEnvironmentVariable("URBE_SHOT") is { } shot)
            {
                for (int i = 0; i < 10; i++) { Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
                Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window)!.Save(shot, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
            var texts = panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Inlines is { Count: > 0 } inl ? inl.Text : t.Text).ToList();
            Assert.Contains(texts, t => t?.StartsWith("As plantas usam luz", StringComparison.Ordinal) == true);
            Assert.DoesNotContain("menu", texts);
            Assert.Single(texts, t => t == "Fotossíntese");
            // a selection in the native text is what goes to the note
            WebReaderQuote? quoted = null;
            panel.QuoteRequested += q => quoted = q;
            var paragraph = panel.GetLogicalDescendants().OfType<SelectableTextBlock>().First(t => t.Inlines?.Text?.StartsWith("As plantas", StringComparison.Ordinal) == true);
            paragraph.SelectionStart = 0; paragraph.SelectionEnd = 10;
            ClickButtonWithText(panel, "Adicionar seleção à nota");
            for (int i = 0; i < 5; i++) await Task.Yield();
            Assert.Equal("As plantas", quoted!.Text);
            Assert.Equal("https://example.org/bio", quoted.Url);
            await Menu("Ver a página");
            Assert.True(web.IsVisible);
            (string Url, WebClip.Page Page)? saved = null;
            panel.SavePageRequested += (url, page) => saved = (url, page);
            await Menu("Salvar página como nota");
            Assert.Equal("https://example.org/bio", saved!.Value.Url);
            Assert.Equal("Fotossíntese", saved.Value.Page.Title);
            web.Html = "";
            saved = null;
            await Menu("Salvar página como nota");
            Assert.Null(saved);
            Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "A página ainda não terminou de carregar.");
            window.Close();
            return true;
        }
        finally { StudyWebViews.CreateBrowser = old; }
    }, CancellationToken.None));

    [Fact]
    public async Task StudyBrowser_SavingAPageMakesANoteWithItsSourceAndStaysOnThePage() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            var page = WebClip.Read("https://example.org/bio", "<title>Fotossíntese</title><article><p>As plantas usam luz.</p></article>");
            view.StudyBrowser.Open();
            await view.SaveWebPageAsync("https://example.org/bio", page);
            await Settle(window, view);
            var file = Directory.GetFiles(vault, "Fotossíntese.md", SearchOption.AllDirectories).Single();
            var text = File.ReadAllText(file);
            Assert.Contains("source: https://example.org/bio", text);
            Assert.Contains("As plantas usam luz.", text);
            Assert.True(view.StudyBrowser.IsVisible);
            // the same title again: the name is asked, never written over the first
            var second = view.SaveWebPageAsync("https://example.org/bio", page);
            await Settle(window, view);
            Assert.True(view.Dialogs.IsOpen);
            view.Dialogs.Cancel();
            await second;
            Assert.Single(Directory.GetFiles(vault, "Fotossíntese*.md", SearchOption.AllDirectories));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
