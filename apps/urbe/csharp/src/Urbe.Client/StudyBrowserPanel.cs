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
    private readonly TextBox _address = new() { PlaceholderText = "URL do site ou vídeo YouTube", MinHeight = 48 };
    private readonly ContentControl _content = new();
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private readonly Ui.UrbeButton _back = new("←"), _forward = new("→");
    private readonly TextBox _timestamp = new() { Text = "0", Width = 88, MinHeight = 48 };
    private readonly WrapPanel _video = new();
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
        var go = new Ui.UrbeButton("Abrir");
        Avalonia.Automation.AutomationProperties.SetName(_back, "Voltar"); Avalonia.Automation.AutomationProperties.SetName(_forward, "Avançar");
        _address.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { Navigate(_address.Text ?? ""); e.Handled = true; } };
        go.Click += (_, _) => Navigate(_address.Text ?? "");
        _back.Click += (_, _) => _browser?.GoBack();
        _forward.Click += (_, _) => _browser?.GoForward();
        var reload = new Ui.UrbeButton("↻"); reload.Click += (_, _) => _browser?.ReloadPage();
        var close = new Ui.UrbeButton("Fechar"); close.Click += (_, _) => Close();
        var external = new Ui.UrbeButton("Abrir externamente");
        external.Click += async (_, _) => { if (ReaderUris.Web(Address) is { } uri && TopLevel.GetTopLevel(this)?.Launcher is { } launcher) await launcher.LaunchUriAsync(uri); };
        var copy = new Ui.UrbeButton("Copiar URL");
        copy.Click += async (_, _) => { await Ui.TextActions.CopyAsync(this, Address); };
        var reference = new Ui.UrbeButton("Salvar referência"); reference.Click += (_, _) => { if (ReaderUris.Web(Address) is not null) QuoteRequested?.Invoke(new(Address, "Referência web")); };
        var ask = new Ui.UrbeButton("Perguntar à IA");
        ask.Click += async (_, _) =>
        {
            if (_browser is not { } browser) return; string address = Address;
            try { string selected = await browser.SelectedTextAsync(); if (ReferenceEquals(browser, _browser) && Address == address && !string.IsNullOrWhiteSpace(selected)) QuestionRequested?.Invoke(address, selected); }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or ObjectDisposedException) { _status.Text = "Selecione o trecho após carregar a página."; }
        };
        var quote = new Ui.UrbeButton("Adicionar seleção à nota");
        quote.Click += async (_, _) =>
        {
            if (_browser is null) return;
            try
            {
                var browser = _browser; string sourceAddress = Address;
                var selection = await browser.SelectedTextAsync();
                if (!ReferenceEquals(browser, _browser) || sourceAddress != Address) return;
                if (string.IsNullOrWhiteSpace(selection)) { _status.Text = "Selecione um trecho na página antes de adicionar à nota."; return; }
                QuoteRequested?.Invoke(new(sourceAddress, selection));
            }
            catch (Exception ex) when (ex is TimeoutException or InvalidOperationException or ObjectDisposedException)
            { _status.Text = "A seleção não está disponível. Tente novamente após a página carregar."; }
        };
        var header = new DockPanel { Margin = new Thickness(8) };
        var actions = new WrapPanel { Children = { _back, _forward, reload, go, close } };
        DockPanel.SetDock(actions, Dock.Right); header.Children.Add(actions); header.Children.Add(_address);
        Children.Add(header);
        SetRow(_video, 1); Children.Add(_video);
        _video.Children.Add(new TextBlock { Text = "Instante (segundos)", VerticalAlignment = VerticalAlignment.Center });
        _video.Children.Add(_timestamp);
        var mark = new Ui.UrbeButton("Anotar instante");
        mark.Click += (_, _) =>
        {
            if (_videoId is not { } id || !int.TryParse(_timestamp.Text, out int second) || second < 0) return;
            QuoteRequested?.Invoke(new("https://www.youtube.com/watch?v=" + id + "&t=" + second, "Vídeo no instante " + second + " s", id, second));
        };
        _video.Children.Add(mark);
        SetRow(_content, 2); Children.Add(_content);
        var bottom = new StackPanel { Margin = new Thickness(8), Children = { _status, new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { quote, reference, ask, copy, external } } } } };
        SetRow(bottom, 3); Children.Add(bottom);
        SizeChanged += (_, e) =>
        {
            bool narrow = e.NewSize.Width < 720;
            DockPanel.SetDock(actions, narrow ? Dock.Top : Dock.Right);
        };
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
        _status.Text = _videoId is null ? (uri.Scheme == "https" ? "Conexão HTTPS" : "Conexão HTTP")
            : "Player oficial do YouTube. Reprodução depende da rede e das permissões do vídeo; login ou bloqueio podem exigir abrir no YouTube.";
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
        if (_browser.Error is { } error) _status.Text = error;
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
