using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Web.WebView2.Core;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Desktop;

/// <summary>Content-only WebView2 host. Local documents and remote browsing have separate profiles and policies.</summary>
internal sealed class WindowsStudyWebView(string content, bool local, Action<string>? readerNavigate = null, ReaderContentEngine readerEngine = ReaderContentEngine.None, ContentRequest? media = null) : NativeControlHost, IStudyWebView, IHtmlReloadable, IReaderSlideRenderer, IReaderComputation, IReaderTextLocation, IReaderActivity, IReaderMediaPosition
{
    private IntPtr _hwnd;
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _web;
    private string _content = content;
    private int? _readerPart;
    private string? _readerAnchor;
    private string? _readerAppearance;
    private static bool IsLocalDocument(string uri) => uri == "https://urbe.reader.invalid/content" || uri.StartsWith("https://urbe.reader.invalid/content#",StringComparison.Ordinal);
    private (int Offset, int Length, double Scroll)? _readerLocation;
    public string Address { get; private set; } = local ? "about:blank" : content;
    public bool CanGoBack => _web?.CanGoBack == true;
    public bool CanGoForward => _web?.CanGoForward == true;
    public string? Error { get; private set; }
    public event Action? NavigationChanged;

    public static void Register()
    {
        if (!OperatingSystem.IsWindows()) return;
        StudyWebViews.CreateBrowser = address => new WindowsStudyWebView(address, false);
        ContentViewers.Factory = (request, _) =>
        {
            if (request.Kind is VaultFileKinds.Audio or VaultFileKinds.Video && request.Storage is FolderVaultStorage)
            {
                string tag = request.Kind == VaultFileKinds.Audio ? "audio" : "video";
                string html = "<!doctype html><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; media-src https://urbe.reader.invalid/media; style-src 'unsafe-inline'; script-src 'none'\"><style>html,body{margin:0;background:#111;height:100%}audio,video{width:100%;height:100%;object-fit:contain}</style><" + tag + " controls preload='metadata' src='https://urbe.reader.invalid/media'></" + tag + ">";
                return Task.FromResult<Control?>(new WindowsStudyWebView(html, true, media: request));
            }
            return Task.FromResult<Control?>(request.Kind == VaultFileKinds.Html && request.Bytes is { } bytes && FilePreviewPanel.Text(bytes) is { } content
                ? new WindowsStudyWebView(content, true, request.ReaderNavigate, request.ReaderEngine) : null);
        };
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        _hwnd = CreateWindowEx(0, "STATIC", "", 0x50000000, 0, 0, 1, 1, parent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero) throw new InvalidOperationException("Não foi possível criar o visualizador Windows.");
        _ = InitializeAsync(_hwnd);
        return new PlatformHandle(_hwnd, "HWND");
    }

    private async Task InitializeAsync(IntPtr hwnd)
    {
        try
        {
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Urbe", local ? "reader-local-webview" : "reader-browser-webview");
            var environment = await CoreWebView2Environment.CreateAsync(null, profile);
            var controller = await environment.CreateCoreWebView2ControllerAsync(hwnd);
            if (_hwnd != hwnd) { controller.Close(); return; }
            _controller = controller; _web = controller.CoreWebView2;
            var settings = _web.Settings;
            settings.AreHostObjectsAllowed = false;
            settings.IsWebMessageEnabled = false;
            settings.IsScriptEnabled = !local || readerEngine != ReaderContentEngine.None;
            settings.IsStatusBarEnabled = false;
            settings.AreDevToolsEnabled = false;
            _web.Profile.IsPasswordAutosaveEnabled = false;
            _web.Profile.IsGeneralAutofillEnabled = false;
            _web.NavigationStarting += (_, e) =>
            {
                if (local && readerNavigate is not null && e.IsUserInitiated && !e.Uri.StartsWith("about:blank", StringComparison.Ordinal))
                    { if (!IsLocalDocument(e.Uri)) readerNavigate(e.Uri); }
                if (local ? !(e.Uri.StartsWith("about:blank", StringComparison.Ordinal) || IsLocalDocument(e.Uri)) : ReaderUris.Web(e.Uri) is null) e.Cancel = true;
            };
            _web.NavigationCompleted += (_, e) =>
            {
                Error = e.IsSuccess ? null : "A página não pôde ser carregada: " + e.WebErrorStatus;
                Address = _web.Source; NavigationChanged?.Invoke();
                if (e.IsSuccess && _readerPart is { } index) _ = ShowReaderPartAsync(index);
                if(e.IsSuccess&&_readerAppearance is { } appearance)_ = _web.ExecuteScriptAsync(appearance);
                if (e.IsSuccess && _readerLocation is { } p) { _readerLocation=null; _ = _web.ExecuteScriptAsync(ReaderTextLocator.RestoreScript(p.Offset,p.Length,p.Scroll)); }
                if (e.IsSuccess && _readerAnchor is { } anchor) { _readerAnchor=null; _ = _web.ExecuteScriptAsync(ReaderTextLocator.AnchorScript(anchor)); }
            };
            _web.SourceChanged += (_, _) => { Address = _web.Source; NavigationChanged?.Invoke(); };
            _web.HistoryChanged += (_, _) => NavigationChanged?.Invoke();
            _web.NewWindowRequested += (_, e) => { e.Handled = true; if (!local && e.IsUserInitiated) Navigate(e.Uri); };
            _web.DownloadStarting += (_, e) => e.Cancel = true;
            _web.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            _web.ProcessFailed += (_, _) => { Error = "O processo do navegador foi encerrado. Reabra esta fonte."; NavigationChanged?.Invoke(); };
            _web.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            _web.WebResourceRequested += async (_, e) =>
            {
                if (local && media?.Storage is FolderVaultStorage vault && e.Request.Uri == "https://urbe.reader.invalid/media")
                {
                    using var pending = e.GetDeferral();
                    Stream? stream = null;
                    try
                    {
                        stream = await vault.OpenReaderStreamAsync(media.Path);
                        if (stream is null) { e.Response = environment.CreateWebResourceResponse(Stream.Null, 404, "Not found", ""); return; }
                        string? rangeHeader = e.Request.Headers.Contains("Range") ? e.Request.Headers.GetHeader("Range") : null;
                        var range = ReaderByteRange.Parse(rangeHeader, stream.Length);
                        if (range is null)
                        { e.Response = environment.CreateWebResourceResponse(Stream.Null, 416, "Range not satisfiable", "Content-Range: bytes */" + stream.Length); return; }
                        string headers = "Content-Type: " + VaultFileKinds.Mime(media.Name) + "\r\nAccept-Ranges: bytes\r\nCache-Control: no-store\r\nContent-Length: " + range.Count;
                        if (range.Partial) headers += "\r\nContent-Range: bytes " + range.Offset + "-" + (range.Offset + range.Count - 1) + "/" + stream.Length;
                        e.Response = environment.CreateWebResourceResponse(new ReaderRangeStream(stream, range.Offset, range.Count), range.Partial ? 206 : 200, range.Partial ? "Partial Content" : "OK", headers);
                        stream = null; // WebView response owns this bounded stream.
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
                    { e.Response = environment.CreateWebResourceResponse(Stream.Null, 404, "Source unavailable", ""); }
                    finally { stream?.Dispose(); pending.Complete(); }
                    return;
                }
                if (!local && ReaderUris.YouTube(e.Request.Uri) is not null && new Uri(e.Request.Uri).AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal))
                    e.Request.Headers.SetHeader("Referer", "https://urbe.desktop/");
                if (local && e.Request.Uri == "https://urbe.reader.invalid/content")
                {
                    e.Response = environment.CreateWebResourceResponse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_content)), 200, "OK", "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store");
                    return;
                }
                if (Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri) &&
                    (uri.Scheme is "file" or "content" or "filesystem" || local && uri.Scheme is "http" or "https"))
                    e.Response = environment.CreateWebResourceResponse(Stream.Null, 403, "Blocked", "Content-Type: text/plain");
            };
            SizeChanged += (_, _) => Dispatcher.UIThread.Post(Resize);
            _controller.IsVisible = IsEffectivelyVisible;
            Resize();
            if (local) _web.Navigate("https://urbe.reader.invalid/content"); else _web.Navigate(Address);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Error = "WebView2 indisponível neste Windows. Verifique o Microsoft Edge WebView2 Runtime."; NavigationChanged?.Invoke(); }
    }

    private void Resize()
    {
        if (_controller is null || _hwnd == IntPtr.Zero) return;
        GetClientRect(_hwnd, out var rect);
        _controller.Bounds = new System.Drawing.Rectangle(0, 0, Math.Max(1, rect.Right), Math.Max(1, rect.Bottom));
    }

    public void Navigate(string address)
    {
        if (local || ReaderUris.Web(address) is not { } uri) return;
        Address = uri.AbsoluteUri;
        _web?.Navigate(Address);
    }
    public void SetReaderActive(bool active)
    {
        if (_controller is { } controller) controller.IsVisible = active;
        if (!active && _web is { } web) _ = web.ExecuteScriptAsync(ReaderActivity.PauseScript);
    }
    public void GoBack() { if (CanGoBack) _web!.GoBack(); }
    public void GoForward() { if (CanGoForward) _web!.GoForward(); }
    public void ReloadPage() => _web?.Reload();
    public async Task<string> SelectedTextAsync()
    {
        if (_web is null) return "";
        try { return JsonSerializer.Deserialize<string>(await _web.ExecuteScriptAsync("String(window.getSelection ? window.getSelection().toString() : '')")) ?? ""; }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or JsonException) { return ""; }
    }
    public void Reload(string html) { if (local) { _content = html; _web?.Navigate("https://urbe.reader.invalid/content"); } }
    public async Task<double?> ReadMediaSecondAsync()
    {
        if(_web is null)return null;
        try{return JsonSerializer.Deserialize<double?>(await _web.ExecuteScriptAsync("(()=>{let m=document.querySelector('audio,video');return m&&m.readyState>0?m.currentTime:null})()"));}
        catch(Exception ex)when(ex is COMException or JsonException or InvalidOperationException){return null;}
    }
    public Task SeekMediaAsync(double seconds)
    {
        if(_web is null||!double.IsFinite(seconds)||seconds<0)return Task.CompletedTask;
        return _web.ExecuteScriptAsync("(()=>{let m=document.querySelector('audio,video');if(m){let seek=()=>{m.currentTime="+seconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"};if(m.readyState>0)seek();else m.addEventListener('loadedmetadata',seek,{once:true})}})()");
    }
    public Task ApplyReaderAppearanceAsync(string theme,double margin,bool reflow)
    {_readerAppearance=ReaderTextLocator.AppearanceScript(theme,margin,reflow);return _web is { } web?web.ExecuteScriptAsync(_readerAppearance):Task.CompletedTask;}
    public Task GoToReaderAnchorAsync(string id)
    {
        _readerAnchor=id;
        return _web is { } web ? web.ExecuteScriptAsync(ReaderTextLocator.AnchorScript(id)) : Task.CompletedTask;
    }
    public async Task<ReaderTextPosition?> ReadTextPositionAsync(bool selection)
    {
        if (_web is null) return null;
        string json = JsonSerializer.Deserialize<string>(await _web.ExecuteScriptAsync(ReaderTextLocator.ReadScript(selection))) ?? "null";
        return ReaderTextLocator.Parse(json);
    }
    public Task RestoreTextPositionAsync(int offset, int length, double scroll)
    {
        _readerLocation = (offset, length, scroll); _readerAnchor=null;
        return _web is { } web ? web.ExecuteScriptAsync(ReaderTextLocator.RestoreScript(offset, length, scroll)) : Task.CompletedTask;
    }
    public async Task<string> ReadComputationAsync() => _web is { } web
        ? JsonSerializer.Deserialize<string>(await web.ExecuteScriptAsync(OcrReaderHtml.ResultScript)) ?? "null" : "null";
    public void Post(string json) { }
    public Task ShowReaderPartAsync(int index)
    {
        _readerPart = Math.Max(0, index);
        return _web is { } web ? web.ExecuteScriptAsync(PresentationReaderHtml.ShowPartScript(_readerPart.Value)) : Task.CompletedTask;
    }
    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _web = null; _controller?.Close(); _controller = null;
        DestroyWindow(control.Handle); _hwnd = IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
}
