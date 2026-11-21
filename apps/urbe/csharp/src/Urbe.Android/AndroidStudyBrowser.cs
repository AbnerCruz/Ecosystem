using Android.Content;
using Android.Webkit;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Text.Json;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.AndroidHost;

internal sealed class AndroidStudyBrowser(string address) : NativeControlHost, IStudyWebView, IReaderActivity, IReaderMediaPosition
{
    private WebView? _web;
    public string Address { get; private set; } = address;
    public string? Error { get; private set; }
    public bool CanGoBack => _web?.CanGoBack() == true;
    public bool CanGoForward => _web?.CanGoForward() == true;
    public event Action? NavigationChanged;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = (Context?)MainActivity.Current ?? Android.App.Application.Context;
        _web = new WebView(context);
        _web.Settings.JavaScriptEnabled = true;
        _web.Settings.DomStorageEnabled = true;
        _web.Settings.AllowFileAccess = false;
        _web.Settings.AllowContentAccess = false;
        _web.Settings.SetSupportZoom(true);
        _web.Settings.BuiltInZoomControls = true;
        _web.Settings.DisplayZoomControls = false;
        _web.Focusable = _web.FocusableInTouchMode = true;
        _web.SetWebViewClient(new Navigation(this));
        _web.SetWebChromeClient(new Chrome());
        LoadAddress(_web);
        return new AndroidViewControlHandle(_web);
    }

    public void Navigate(string address)
    {
        if (ReaderUris.Web(address) is not { } uri) return;
        Address = uri.AbsoluteUri; Error = null; if (_web is { } web) LoadAddress(web);
    }
    private void LoadAddress(WebView web)
    {
        if (ReaderUris.IsYouTubeEmbed(Address))
            web.LoadUrl(Address, new Dictionary<string, string> { ["Referer"] = "https://" + (web.Context?.PackageName ?? "app.urbe.nativepreview") + "/" });
        else web.LoadUrl(Address);
    }
    // The platform may dispose the Java peer before Avalonia tears down its host.
    // Do not let cleanup or a late activity transition crash the UI dispatcher.
    private static void WithLiveWebView(Action operation)
    {
        try { operation(); }
        catch (ObjectDisposedException) { /* Android already disposed this native peer. */ }
    }

    public void SetReaderActive(bool active)
    {
        if (_web is not { } web) return;
        WithLiveWebView(() =>
        {
            if (active) web.OnResume();
            else { web.EvaluateJavascript(ReaderActivity.PauseScript, null); web.OnPause(); }
        });
    }
    public void GoBack() { if (CanGoBack) _web!.GoBack(); }
    public void GoForward() { if (CanGoForward) _web!.GoForward(); }
    public void ReloadPage() => _web?.Reload();
    public Task<string> SelectedTextAsync()
    {
        if (_web is null) return Task.FromResult("");
        return ReadSelectionAsync(_web);
    }
    internal static Task<string> ReadSelectionAsync(WebView web) => ReadStringAsync(web, "String(window.getSelection ? window.getSelection().toString() : '')");
    internal static Task<string> ReadStringAsync(WebView web, string script)
    {
        var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        web.EvaluateJavascript(script, new Selection(done));
        return done.Task.WaitAsync(TimeSpan.FromSeconds(3));
    }

    public async Task<string> PageHtmlAsync()
    {
        if (_web is null) return "";
        try { return await ReadStringAsync(_web, StudyPageScripts.Html); }
        catch (TimeoutException) { return ""; }
    }

    /// <summary>The second the page's player is at (the official YouTube player included), or null without one.</summary>
    public async Task<double?> ReadMediaSecondAsync()
    {
        if (_web is null) return null;
        try { return double.TryParse(await ReadStringAsync(_web, "String(" + MediaPositionScripts.Read + ")"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : null; }
        catch (TimeoutException) { return null; }
    }

    public Task SeekMediaAsync(double seconds)
    {
        if (_web is not null && double.IsFinite(seconds) && seconds >= 0) _web.EvaluateJavascript(MediaPositionScripts.Seek(seconds), null);
        return Task.CompletedTask;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        var web = _web;
        _web = null; // invalidate callbacks before any native teardown begins
        if (web is null) return;
        // Each method is independent: Android may have disposed the WebView already,
        // or StopLoading may dispose it while the host still needs to finish teardown.
        WithLiveWebView(web.StopLoading);
        WithLiveWebView(web.OnPause);
        WithLiveWebView(web.Destroy);
        WithLiveWebView(web.Dispose);
    }

    private void Changed(WebView? view, string? error = null)
    {
        if (view is null || !ReferenceEquals(_web, view)) return;
        try { Address = view.Url ?? Address; }
        catch (ObjectDisposedException) { return; }
        Error = error;
        Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_web, view)) NavigationChanged?.Invoke();
        });
    }
    private sealed class Navigation(AndroidStudyBrowser host) : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request) => ReaderUris.Web(request?.Url?.ToString() ?? "") is null;
        public override void OnPageFinished(WebView? view, string? url) => host.Changed(view);
        public override void OnReceivedError(WebView? view, IWebResourceRequest? request, WebResourceError? error)
        { if (request?.IsForMainFrame == true) host.Changed(view, "A página não pôde ser carregada: " + error?.Description); }
    }
    private sealed class Chrome : WebChromeClient
    {
        public override void OnPermissionRequest(PermissionRequest? request) => request?.Deny();
    }
    private sealed class Selection(TaskCompletionSource<string> done) : Java.Lang.Object, IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value)
        {
            try { done.TrySetResult(JsonSerializer.Deserialize<string>(value?.ToString() ?? "null") ?? ""); }
            catch (JsonException) { done.TrySetResult(""); }
        }
    }
}
