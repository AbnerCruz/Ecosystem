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

internal sealed class AndroidStudyBrowser(string address) : NativeControlHost, IStudyWebView, IReaderActivity
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
        if (ReaderUris.YouTube(Address) is not null && new Uri(Address).AbsolutePath.StartsWith("/embed/", StringComparison.Ordinal))
            web.LoadUrl(Address, new Dictionary<string, string> { ["Referer"] = "https://" + (web.Context?.PackageName ?? "app.urbe.nativepreview") + "/" });
        else web.LoadUrl(Address);
    }
    public void SetReaderActive(bool active)
    { if (_web is { } web) { if (active) web.OnResume(); else { web.EvaluateJavascript(ReaderActivity.PauseScript, null); web.OnPause(); } } }
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

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (_web is { } web)
        {
            _web = null;
            web.StopLoading(); web.OnPause(); web.Destroy(); web.Dispose();
        }
    }

    private void Changed(WebView? view, string? error = null)
    {
        Address = view?.Url ?? Address; Error = error;
        Dispatcher.UIThread.Post(() => NavigationChanged?.Invoke());
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
