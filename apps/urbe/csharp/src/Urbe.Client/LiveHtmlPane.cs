using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-141: "Prévia" of an HTML being edited. The page (LivePreview) is built from the code as it is in the editor
/// and shown in the platform's isolated viewer; every change of the code shows in it a moment later, without saving
/// and without a refresh button. On a computer the same page also opens in the browser, which reloads with the code.
/// </summary>
public sealed class LiveHtmlPane : Border
{
    private readonly ContentControl _body = new();
    private readonly Button _browser;
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private readonly Dictionary<string, byte[]?> _files = new(StringComparer.Ordinal);
    private string? _path;
    private Func<string>? _code;
    private Func<IEnumerable<CityNote>>? _notes;
    private LiveBrowserServer? _server;
    private int _version;

    public LiveHtmlPane()
    {
        IsVisible = false;
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line);
        BorderThickness = new Thickness(1, 0, 0, 0);
        _browser = new Button { Content = "Abrir no navegador", Padding = new Thickness(12, 6), FontSize = 13, IsVisible = !OperatingSystem.IsAndroid() };
        AutomationPropertiesName(_browser, "Abrir a prévia no navegador");
        _browser.Click += (_, _) => OpenInBrowser();
        var title = new TextBlock { Text = "Prévia ao vivo", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center };
        var head = new DockPanel { Margin = new Thickness(12, 6, 8, 6) };
        DockPanel.SetDock(_browser, Dock.Right);
        head.Children.Add(_browser);
        head.Children.Add(title);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        grid.Children.Add(head);
        Grid.SetRow(_body, 1);
        grid.Children.Add(_body);
        Child = grid;
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = RebuildAsync(); };
        DetachedFromVisualTree += (_, _) => Close();
    }

    private static void AutomationPropertiesName(Control c, string name) => Avalonia.Automation.AutomationProperties.SetName(c, name);

    /// <summary>The vault's bytes of a file the page uses (pictures, media); null when missing.</summary>
    public Func<string, Task<byte[]?>>? ReadFile { get; set; }

    /// <summary>Opens an address in the browser (tests replace it).</summary>
    public Action<string> OpenBrowser { get; set; } = address =>
    {
        try { using var _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(address) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException) { }
    };

    /// <summary>The page shown now (tests).</summary>
    public string? Shown { get; private set; }

    /// <summary>Where the browser shows the page, once it was opened there.</summary>
    public string? BrowserAddress => _server?.Address;

    /// <summary>Starts showing <paramref name="path"/>: its code from <paramref name="code"/>, the other files from <paramref name="notes"/>.</summary>
    public void Show(string path, Func<string> code, Func<IEnumerable<CityNote>> notes)
    {
        if (_path != path) { _files.Clear(); _body.Content = null; }
        _path = path;
        _code = code;
        _notes = notes;
        IsVisible = true;
        _ = RebuildAsync();
    }

    /// <summary>The pane leaves the screen (Código); a browser that shows the page goes on following the code.</summary>
    public void Hide()
    {
        IsVisible = false;
        (_body.Content as IDisposable)?.Dispose();
        _body.Content = null;
        if (_server is null) Close();
    }

    /// <summary>Another file or the editor closed: the preview and the browser's page stop.</summary>
    public void Close()
    {
        IsVisible = false;
        _debounce.Stop();
        (_body.Content as IDisposable)?.Dispose();
        _body.Content = null;
        _path = null;
        Shown = null;
        StopBrowser();
    }

    /// <summary>The code changed: the page follows a moment after the typing pauses.</summary>
    public void CodeChanged()
    {
        if (!IsVisible && _server is null) return;
        _debounce.Stop();
        _debounce.Start();
    }

    private async Task RebuildAsync()
    {
        if (_path is not { } path || _code is null || _notes is null) return;
        var version = ++_version;
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var note in _notes()) texts[note.Path] = note.Content;
        texts[path] = _code();
        var missing = new HashSet<string>(StringComparer.Ordinal);
        string Build() => LivePreview.Build(path, texts[path], p => texts.GetValueOrDefault(p),
            p => texts.TryGetValue(p, out var t) ? Encoding.UTF8.GetBytes(t) : _files.TryGetValue(p, out var b) ? b : Missing(p), texts.Keys);
        byte[]? Missing(string p) { missing.Add(p); return null; }
        string html;
        try
        {
            html = Build();
            // the pictures and media it uses are read from the vault once, then the page is made again with them
            if (missing.Count > 0 && ReadFile is { } read)
            {
                foreach (var p in missing) _files[p] = await read(p);
                if (version != _version) return;
                html = Build();
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            html = "<pre style=\"color:#c33;padding:12px\">A prévia não pôde ser montada: " + System.Net.WebUtility.HtmlEncode(ex.Message) + "</pre>";
        }
        if (version != _version) return;
        Shown = html;
        _server?.Publish(html);
        await PresentAsync(path, html, version);
    }

    private async Task PresentAsync(string path, string html, int version)
    {
        if (_body.Content is IHtmlReloadable live) { live.Reload(html); return; }
        Control? viewer = null;
        try { viewer = await ContentViewers.CreateAsync(new ContentRequest(null, path, System.IO.Path.GetFileName(path), VaultFileKinds.Html, Encoding.UTF8.GetBytes(html))); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException) { viewer = null; }
        if (version != _version) { (viewer as IDisposable)?.Dispose(); return; }
        if (viewer is not null) { _body.Content = viewer; return; }
        if (_body.Content is TextBlock) return;
        _body.Content = new TextBlock
        {
            Text = "Este computador mostra a prévia no navegador: toque em Abrir no navegador. Ela recarrega sozinha a cada mudança do código.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16), FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text3)
        };
    }

    private void OpenInBrowser()
    {
        _server ??= new LiveBrowserServer();
        if (Shown is { } html) _server.Publish(html);
        OpenBrowser(_server.Address);
    }

    private void StopBrowser()
    {
        _server?.Dispose();
        _server = null;
    }
}
