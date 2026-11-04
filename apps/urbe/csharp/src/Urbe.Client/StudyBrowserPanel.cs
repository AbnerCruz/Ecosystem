using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

public sealed record WebReaderQuote(string Url, string Text, string? VideoId = null, int? Second = null);

/// <summary>Native browser chrome around platform web content. No page-to-vault bridge exists.</summary>
public sealed class StudyBrowserPanel : Grid
{
    private readonly TextBox _address = new() { PlaceholderText = "Endereço ou pesquisa", MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly ContentControl _content = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(12, 4, 12, 0) };
    private readonly Ui.UrbeIconButton _back = new(UrbeTheme.Icons.Back, "Voltar"), _forward = new(UrbeTheme.Icons.Forward, "Avançar");
    private readonly TextBox _timestamp = new() { Text = "0", Width = 72, MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly StackPanel _video = new() { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(12, 4) };
    private IStudyWebView? _browser;
    private string? _videoId;
    public event Action<WebReaderQuote>? QuoteRequested;
    public event Action<string, string>? QuestionRequested;
    public string Address => _browser?.Address ?? _address.Text ?? "";

    public StudyBrowserPanel()
    {
        IsVisible = false;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto");
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        Avalonia.Automation.AutomationProperties.SetName(_address, "Endereço");
        _address.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { Navigate(_address.Text ?? ""); e.Handled = true; } };
        _back.Click += (_, _) => _browser?.GoBack();
        _forward.Click += (_, _) => _browser?.GoForward();
        var reload = new Ui.UrbeIconButton(UrbeTheme.Icons.Restore, "Recarregar", 20); reload.Click += (_, _) => _browser?.ReloadPage();
        var close = new Ui.UrbeIconButton(UrbeTheme.Icons.Close, "Fechar", 20); close.Click += (_, _) => Close();
        var go = new Ui.UrbeIconButton(UrbeTheme.Icons.SearchUi, "Abrir", 20); go.Click += (_, _) => Navigate(_address.Text ?? "");
        async Task<string?> SelectionAsync()
        {
            if (_browser is not { } browser) return null;
            string address = Address;
            try
            {
                var selected = await browser.SelectedTextAsync();
                if (!ReferenceEquals(browser, _browser) || Address != address) return null;
                if (string.IsNullOrWhiteSpace(selected)) { _status.Text = "Selecione um trecho na página primeiro."; return null; }
                return selected;
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or ObjectDisposedException)
            { _status.Text = "A seleção não está disponível. Tente de novo quando a página terminar de carregar."; return null; }
        }
        // the page's actions: the two about a selection as chips, the rest in ⋯
        var quote = new Ui.UrbeButton("Adicionar seleção à nota", Ui.ButtonKind.Ghost, UrbeTheme.Icons.Notes) { MinHeight = 40, Padding = new Thickness(10, 0) };
        quote.Click += async (_, _) => { var address = Address; if (await SelectionAsync() is { } text) QuoteRequested?.Invoke(new(address, text)); };
        var ask = new Ui.UrbeButton("Perguntar à IA", Ui.ButtonKind.Ghost, UrbeTheme.Icons.Assistant) { MinHeight = 40, Padding = new Thickness(10, 0) };
        ask.Click += async (_, _) => { var address = Address; if (await SelectionAsync() is { } text) QuestionRequested?.Invoke(address, text); };
        var more = new Ui.UrbeIconButton(UrbeTheme.Icons.MoreUi, "Mais ações", 20);
        more.Click += (_, _) =>
        {
            var items = new List<MenuItem>();
            void Item(string header, Action run) { var i = new MenuItem { Header = header }; i.Click += (_, _) => run(); items.Add(i); }
            Item("Salvar referência na nota", () => { if (ReaderUris.Web(Address) is not null) QuoteRequested?.Invoke(new(Address, "Referência web")); });
            Item("Copiar endereço", () => _ = Ui.TextActions.CopyAsync(this, Address));
            Item("Abrir no navegador do sistema", () => _ = OpenExternallyAsync());
            new ContextMenu { ItemsSource = items }.Open(more);
        };
        var header = new DockPanel { Margin = new Thickness(4, 6, 4, 2), LastChildFill = true };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Children = { close, _back, _forward } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Children = { go, reload } };
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        header.Children.Add(left);
        header.Children.Add(right);
        header.Children.Add(_address);
        Children.Add(header);
        SetRow(_video, 1); Children.Add(_video);
        _video.Children.Add(new TextBlock { Text = "Instante (s)", VerticalAlignment = VerticalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) });
        _video.Children.Add(_timestamp);
        var mark = new Ui.UrbeButton("Anotar instante", Ui.ButtonKind.Ghost, UrbeTheme.Icons.Clock) { MinHeight = 40, Padding = new Thickness(10, 0) };
        mark.Click += (_, _) =>
        {
            if (_videoId is not { } id || !int.TryParse(_timestamp.Text, out int second) || second < 0) return;
            QuoteRequested?.Invoke(new("https://www.youtube.com/watch?v=" + id + "&t=" + second, "Vídeo no instante " + second + " s", id, second));
        };
        _video.Children.Add(mark);
        SetRow(_content, 2); Children.Add(_content);
        var chips = new DockPanel { Margin = new Thickness(4, 0, 4, 4) };
        DockPanel.SetDock(more, Dock.Right);
        chips.Children.Add(more);
        chips.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { quote, ask } }
        });
        var bottom = new StackPanel { Children = { _status, chips }, Background = UrbeTheme.Brush(UrbeTheme.Surface) };
        SetRow(bottom, 3); Children.Add(bottom);
    }

    private async Task OpenExternallyAsync()
    {
        if (ReaderUris.Web(Address) is { } uri && TopLevel.GetTopLevel(this)?.Launcher is { } launcher) await launcher.LaunchUriAsync(uri);
    }

    public bool Open(string? address = null)
    {
        IsVisible = true;
        if (address is null) return true;
        return Navigate(address);
    }

    public bool Navigate(string address)
    {
        address = address.Trim();
        if (!address.Contains(':') && !address.Contains("//", StringComparison.Ordinal))
            address = address.Contains('.') && !address.Any(char.IsWhiteSpace) ? "https://" + address : "https://duckduckgo.com/?q=" + Uri.EscapeDataString(address);
        if (ReaderUris.Web(address) is not { } uri)
        { _status.Text = "Use uma URL http:// ou https:// sem credenciais no endereço."; return false; }
        _videoId = ReaderUris.YouTube(address)?.Id;
        _video.IsVisible = _videoId is not null;
        if (ReaderUris.YouTube(address) is { } video)
        {
            _timestamp.Text = video.Second.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // Official player page: no downloaded media, hidden playback, transcript scraping or replacement controls.
            uri = new Uri("https://www.youtube.com/embed/" + video.Id + "?start=" + video.Second);
        }
        _address.Text = uri.AbsoluteUri;
        _status.Text = _videoId is null ? (uri.Scheme == "https" ? "" : "Conexão sem criptografia (HTTP).")
            : "Player oficial do YouTube. Se o vídeo não tocar aqui, use ⋯ → Abrir no navegador do sistema.";
        _status.IsVisible = _status.Text.Length > 0;
        if (_browser is null)
        {
            if (StudyWebViews.CreateBrowser?.Invoke(uri.AbsoluteUri) is not { } control || control is not IStudyWebView browser)
            { _status.Text = "O navegador interno não está disponível neste host."; return false; }
            _browser = browser; _content.Content = control;
            browser.NavigationChanged += Changed;
        }
        else _browser.Navigate(uri.AbsoluteUri);
        return true;
    }
    private void Changed()
    {
        if (_browser is null) return;
        _address.Text = _browser.Address;
        var video = ReaderUris.YouTube(_browser.Address);
        _videoId = video?.Id;
        _video.IsVisible = video is not null;
        if (video is { } currentVideo) _timestamp.Text = currentVideo.Second.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _back.IsEnabled = _browser.CanGoBack; _forward.IsEnabled = _browser.CanGoForward;
        if (_browser.Error is { } error) { _status.Text = error; _status.IsVisible = true; }
    }
    public bool Back()
    {
        if (!IsVisible) return false;
        if (_browser?.CanGoBack == true) _browser.GoBack(); else Close();
        return true;
    }
    public void Close()
    {
        if (_browser is not null) _browser.NavigationChanged -= Changed;
        if (_content.Content is Control active) ReaderActivity.SetActive(active, false);
        _browser = null;
        if (_content.Content is Control native) native.IsVisible = false;
        _content.Content = null;
        IsVisible = false;
    }
}
