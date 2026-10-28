using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Platform;
using Urbe.Client;
using Urbe.Core;
using AndroidUri = Android.Net.Uri;
using AView = Android.Views.View;

namespace Urbe.AndroidHost;

/// <summary>
/// The content viewers of the Android head (owner, ADD-0023/DEC-0046): what the Urbe shows inside its own panels.
/// HTML in the system's WebView, isolated (no file or content access, no bridge to the app, no leaving the page);
/// audio and video in the system's player straight from the vault; PDF pages drawn by the system's PdfRenderer
/// (<see cref="AndroidPdfDocument"/>, shown by <see cref="PdfPagesView"/>). The app's interface stays native
/// (ADR-0032): these are content inside a native panel.
/// </summary>
internal static class AndroidContentViewers
{
    public static void Register()
    {
        ContentViewers.Factory = CreateAsync;
        ContentViewers.PdfReader = OpenPdfAsync;
        ContentViewers.Printer = PrintAsync;
    }

    /// <summary>
    /// printPdf (UrbeNative.printHtml in the 1.8.4 APK): the page in an off-screen WebView, then the system's print
    /// dialog for it — "Salvar como PDF" saves the file where the person chooses; a book keeps its page size and margins.
    /// </summary>
    private static Task PrintAsync(string title, string html)
    {
        var activity = MainActivity.Current ?? throw new InvalidOperationException("O aplicativo não está aberto.");
        var done = new TaskCompletionSource();
        activity.RunOnUiThread(() =>
        {
            try
            {
                var web = new WebView(activity);
                web.Settings.JavaScriptEnabled = true;
                web.Settings.AllowFileAccess = false;
                web.Settings.AllowContentAccess = false;
                web.SetWebViewClient(new PrintWhenLoaded(activity, title, done));
                web.LoadDataWithBaseURL(null, html, "text/html", "utf-8", null);
                Kept.Add(web);
            }
            catch (Exception ex) { done.TrySetException(ex); }
        });
        return done.Task;
    }

    // the off-screen WebView must live until the print job has read the page
    private static readonly List<WebView> Kept = [];

    private sealed class PrintWhenLoaded(Activity activity, string title, TaskCompletionSource done) : WebViewClient
    {
        private bool _printed;

        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request) => true;

        public override void OnPageFinished(WebView? view, string? url)
        {
            if (_printed || view is null) return;
            _printed = true;
            try
            {
                if (activity.GetSystemService(Context.PrintService) is not Android.Print.PrintManager printer)
                    throw new InvalidOperationException("Este aparelho não tem impressão.");
                var job = string.IsNullOrWhiteSpace(title) ? "Página" : title;
                printer.Print(job, view.CreatePrintDocumentAdapter(job), new Android.Print.PrintAttributes.Builder().Build());
                done.TrySetResult();
            }
            catch (Exception ex) { done.TrySetException(ex); }
            finally
            {
                // released a while later: the system has copied the document by then
                view.PostDelayed(() =>
                {
                    Kept.Remove(view);
                    try { if (view.Handle != IntPtr.Zero) view.Destroy(); }
                    catch (ObjectDisposedException) { }
                }, 120_000);
            }
        }
    }

    private static async Task<Control?> CreateAsync(ContentRequest request, CancellationToken cancellationToken)
    {
        switch (request.Kind)
        {
            case VaultFileKinds.Html when request.Bytes is { } bytes && FilePreviewPanel.Text(bytes) is { } html:
            {
                // Only a validated Atlas Map exported HTML gets a host capability. Studio and arbitrary HTML remain isolated.
                var atlas = AtlasMapSession.TryCreate(request);
                return new AndroidHtmlHost(context => Page(context, html, request.Messages, atlas), view =>
                {
                    atlas?.Revoke();
                    if (view is WebView web)
                    {
                        if (atlas is not null) web.RemoveJavascriptInterface("UrbeAtlas");
                        web.Destroy();
                    }
                }, request.Messages is not null, atlas);
            }
            case VaultFileKinds.Audio or VaultFileKinds.Video when request.Storage is AndroidVaultStorage vault
                                                                 && await vault.DocumentOfAsync(request.Path, cancellationToken) is { } uri:
                return new AndroidViewHost(context => Player(context, uri), view => (view.FindViewWithTag("player") as VideoView)?.StopPlayback());
            default:
                return null;
        }
    }

    /// <summary>A page of HTML from the vault: JavaScript on (the HTML products need it), nothing else reachable.</summary>
    private static AView Page(Context context, string html, Action<string>? messages = null, AtlasMapSession? atlas = null)
    {
        var web = new WebView(context);
        web.SetBackgroundColor(Color.White);
        var settings = web.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        settings.AllowFileAccess = false;
        settings.AllowContentAccess = false;
        settings.SetSupportZoom(true);
        settings.BuiltInZoomControls = true;
        settings.DisplayZoomControls = false;
        if (atlas is { } approved)
        {
            // No external frames/network/forms. The Atlas uses one inline script and a blob: worker for offline physics.
            // A fresh CSP is installed at each open (the prior host meta, if present, is replaced).
            const string csp = "<meta data-urbe-atlas-csp=\"v1\" http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src data: blob:; worker-src blob:; connect-src 'none'; frame-src 'none'; object-src 'none'; form-action 'none'; base-uri 'none'\">";
            // Never trust a CSP sentinel supplied inside editable HTML. Remove only the prior host meta
            // and insert a fresh enforced policy on every open; forged marker strings cannot skip it.
            html = System.Text.RegularExpressions.Regex.Replace(html,
                """<meta\s+data-urbe-atlas-csp="v1"[^>]*>""", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
            html = html.Replace("<head>", "<head>" + csp, StringComparison.OrdinalIgnoreCase);
            var bridge = new AtlasMapJavascriptBridge(web, approved);
            web.AddJavascriptInterface(bridge, "UrbeAtlas");
            web.SetWebViewClient(new AtlasStayOnPage(bridge));
        }
        else web.SetWebViewClient(new StayOnPage());
        if (messages is not null)
        {
            // the studio's preview: what the page posts to its parent (here, itself) goes out by the console, nothing else
            web.SetWebChromeClient(new PreviewMessages(messages));
            const string relay = "<script>addEventListener('message',function(e){try{var m=e.data;if(m&&!m.host&&(m.urbePage==='select'||m.urbePage==='text'||m.urbePage==='comment'))console.log('urbe-msg:'+JSON.stringify(m))}catch(_){}})</script>";
            html = html.Contains("</body>", StringComparison.Ordinal) ? html.Replace("</body>", relay + "</body>", StringComparison.Ordinal) : html + relay;
        }
        if (messages is not null || atlas is not null)
        {
            // writing in the preview or Atlas Map fields: the page's text takes the keyboard (the app's own view holds it otherwise)
            web.Focusable = true;
            web.FocusableInTouchMode = true;
            web.Touch += (_, e) =>
            {
                if (e.Event?.Action == MotionEventActions.Down && !web.HasFocus) web.RequestFocus();
                e.Handled = false;
            };
        }
        web.LoadDataWithBaseURL(null, html, "text/html", "utf-8", null);
        return web;
    }

    private sealed class PreviewMessages(Action<string> messages) : WebChromeClient
    {
        public override bool OnConsoleMessage(ConsoleMessage? consoleMessage)
        {
            var text = consoleMessage?.Message();
            if (text is not null && text.StartsWith("urbe-msg:", StringComparison.Ordinal))
            {
                var json = text["urbe-msg:".Length..];
                Avalonia.Threading.Dispatcher.UIThread.Post(() => messages(json));
                return true;
            }
            return base.OnConsoleMessage(consoleMessage);
        }
    }

    /// <summary>Only the already-loaded approved document may receive an AtlasHost adapter.</summary>
    private sealed class AtlasStayOnPage(AtlasMapJavascriptBridge bridge) : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request) => true;

        public override void OnPageFinished(WebView? view, string? url)
        {
            if (view is null || view.Handle == IntPtr.Zero) return;
            // The exported map captures initialHTML during boot. Injecting here keeps the per-session
            // nonce and native object out of any subsequently saved HTML.
            view.EvaluateJavascript(bridge.AdapterScript, null);
        }
    }

    /// <summary>A link or form of the page does not take the panel away from the file.</summary>
    private sealed class StayOnPage : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView? view, IWebResourceRequest? request) => true;
    }

    /// <summary>Audio or video of the vault in the system's player, with its controls.</summary>
    private static AView Player(Context context, AndroidUri uri)
    {
        var frame = new FrameLayout(context);
        frame.SetBackgroundColor(Color.Black);
        var video = new VideoView(context) { Tag = "player" };
        frame.AddView(video, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent, GravityFlags.Center));
        var controls = new MediaController(context);
        controls.SetAnchorView(frame);
        video.SetMediaController(controls);
        video.Prepared += (_, _) => controls.Show(0);
        video.SetVideoURI(uri);
        return frame;
    }

    private static async Task<IPdfDocument?> OpenPdfAsync(ContentRequest request, CancellationToken cancellationToken)
    {
        if (request.Storage is not AndroidVaultStorage vault) return null;
        ParcelFileDescriptor? fd = null;
        try
        {
            if (await vault.DocumentOfAsync(request.Path, cancellationToken) is { } uri)
                fd = Android.App.Application.Context.ContentResolver?.OpenFileDescriptor(uri, "r");
        }
        catch (Java.Lang.Exception) { fd = null; }
        // a provider whose files cannot be seeked: the bytes already read, from the app's own cache
        if (fd is null && request.Bytes is { } bytes)
        {
            var cache = System.IO.Path.Combine(Android.App.Application.Context.CacheDir?.AbsolutePath ?? System.IO.Path.GetTempPath(), "preview.pdf");
            await File.WriteAllBytesAsync(cache, bytes, cancellationToken);
            fd = ParcelFileDescriptor.Open(new Java.IO.File(cache), ParcelFileMode.ReadOnly);
        }
        if (fd is null) return null;
        try { return new AndroidPdfDocument(fd); }
        catch (Java.Lang.Exception) { fd.Close(); return null; }
    }
}

/// <summary>A PDF read by Android's PdfRenderer; one page is open at a time, as the renderer requires.</summary>
internal sealed class AndroidPdfDocument : IPdfDocument
{
    private readonly ParcelFileDescriptor _fd;
    private readonly PdfRenderer _renderer;
    private readonly Avalonia.Size[] _sizes;

    public AndroidPdfDocument(ParcelFileDescriptor fd)
    {
        _fd = fd;
        _renderer = new PdfRenderer(fd);
        _sizes = new Avalonia.Size[_renderer.PageCount];
        for (int i = 0; i < _sizes.Length; i++)
        {
            using var page = _renderer.OpenPage(i);
            _sizes[i] = new Avalonia.Size(page.Width, page.Height);
            page.Close();
        }
    }

    public int PageCount => _sizes.Length;

    public Avalonia.Size PageSize(int index) => _sizes[index];

    public (byte[] Rgba, int Width, int Height) Render(int index, int width)
    {
        var size = _sizes[index];
        int height = Math.Max(1, (int)Math.Round(width * size.Height / Math.Max(1, size.Width)));
        using var page = _renderer.OpenPage(index);
        using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!)!;
        bitmap.EraseColor(Color.White.ToArgb());
        page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);
        page.Close();
        // ARGB_8888 is RGBA in memory; on white the alpha is full, so premultiplied and straight are the same
        var buffer = Java.Nio.ByteBuffer.Allocate(width * height * 4);
        bitmap.CopyPixelsToBuffer(buffer);
        buffer.Rewind();
        var rgba = new byte[width * height * 4];
        buffer.Get(rgba);
        return (rgba, width, height);
    }

    public void Dispose()
    {
        _renderer.Close();
        _fd.Close();
    }
}

/// <summary>An Android view inside an Avalonia panel (NativeControlHost), released when the panel lets it go.</summary>
/// <summary>An HTML page whose WebView loads new HTML in place (the studio's preview).</summary>
internal sealed class AndroidHtmlHost(Func<Context, AView> make, Action<AView>? release, bool relay, AtlasMapSession? atlas = null) : AndroidViewHost(make, release), IHtmlReloadable, IAtlasMapPendingEdits
{
    // Native close/navigation guard for a pending Atlas Map. No HTML reload: editing, camera and map state remain.
    // The exported runtime is the source of truth for dirty state, never a stale duplicate in the host.
    public async Task<bool> HasPendingEditsAsync()
    {
        if (atlas is null) return false;
        if (View is not WebView web || web.Handle == IntPtr.Zero) return false;
        return await EvaluateBooleanAsync(web, "Boolean(window.AtlasMap && window.AtlasMap.isDirty && window.AtlasMap.isDirty())");
    }

    public async Task<bool> SavePendingEditsAsync()
    {
        if (atlas is null) return true;
        if (View is not WebView web || web.Handle == IntPtr.Zero) return false;
        if (!await HasPendingEditsAsync()) return true;
        // The map's own save handles the V1 acknowledgement, including a 15s timeout. It only clears
        // dirty after a native verified write. Poll the state; failures/collisions preserve pending edits.
        web.Post(() =>
        {
            try { if (web.Handle != IntPtr.Zero) web.EvaluateJavascript("window.AtlasMap && window.AtlasMap.save && window.AtlasMap.save()", null); }
            catch (ObjectDisposedException) { }
        });
        for (int i = 0; i < 45; i++)
        {
            await Task.Delay(400);
            if (View is not WebView current || current.Handle == IntPtr.Zero) return false;
            try { if (!await HasPendingEditsAsync()) return true; }
            catch (TimeoutException) { return false; }
        }
        return false;
    }

    private static async Task<bool> EvaluateBooleanAsync(WebView web, string expression)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        web.Post(() =>
        {
            try
            {
                if (web.Handle == IntPtr.Zero) { tcs.TrySetException(new ObjectDisposedException(nameof(WebView))); return; }
                web.EvaluateJavascript(expression, new BooleanResultCallback(value =>
                    tcs.TrySetResult(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))));
            }
            catch (Exception ex) when (ex is ObjectDisposedException or Java.Lang.IllegalStateException)
            {
                tcs.TrySetException(ex);
            }
        });
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(4));
    }

    private sealed class BooleanResultCallback(Action<string?> onValue) : Java.Lang.Object, Java.Lang.IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => onValue(value?.ToString());
    }

    public void Reload(string html)
    {
        if (View is not WebView web || web.Handle == IntPtr.Zero) return;
        if (relay)
        {
            const string script = "<script>addEventListener('message',function(e){try{var m=e.data;if(m&&!m.host&&(m.urbePage==='select'||m.urbePage==='text'||m.urbePage==='comment'))console.log('urbe-msg:'+JSON.stringify(m))}catch(_){}})</script>";
            html = html.Contains("</body>", StringComparison.Ordinal) ? html.Replace("</body>", script + "</body>", StringComparison.Ordinal) : html + script;
        }
        // keep where the person was reading
        int y = web.ScrollY;
        web.LoadDataWithBaseURL(null, html, "text/html", "utf-8", null);
        web.PostDelayed(() => { try { if (web.Handle != IntPtr.Zero) web.ScrollTo(0, y); } catch (ObjectDisposedException) { } }, 150);
    }

    public void Post(string json)
    {
        if (View is not WebView web || web.Handle == IntPtr.Zero) return;
        try { web.EvaluateJavascript("postMessage(" + json + ",'*')", null); }
        catch (ObjectDisposedException) { }
    }
}

internal class AndroidViewHost(Func<Context, AView> make, Action<AView>? release = null) : NativeControlHost
{
    private AView? _view;

    protected AView? View => _view;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = (Context?)MainActivity.Current ?? Android.App.Application.Context;
        _view = make(context);
        return new AndroidViewControlHandle(_view);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        // the view can already be gone on the Java side (the control was moved or the page closed twice): releasing a
        // disposed peer threw ObjectDisposedException and brought the app down (relatório de erros, 15/10/2026)
        if (_view is { } view && view.Handle != IntPtr.Zero)
        {
            try { release?.Invoke(view); }
            catch (ObjectDisposedException) { }
        }
        _view = null;
        try { base.DestroyNativeControlCore(control); }
        catch (ObjectDisposedException) { }
    }
}
