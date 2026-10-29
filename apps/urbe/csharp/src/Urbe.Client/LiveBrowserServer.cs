using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Urbe.Client;

/// <summary>
/// CST-141, on a computer: the page being edited served to the browser from this machine only (localhost), the way a
/// Live Server does. Each new version tells the open tabs to reload (Server-Sent Events). The page is the same one the
/// in-app viewer shows (LivePreview: everything it uses inside it), so the server gives no access to the vault.
/// </summary>
public sealed class LiveBrowserServer : IDisposable
{
    private const string Events = "/__urbe/events";
    private const string Reloader = "<script>(function(){var s=new EventSource('" + Events + "');s.onmessage=function(){location.reload()};})();</script>";

    private readonly HttpListener _listener = new();
    private readonly List<HttpListenerResponse> _tabs = [];
    private readonly object _gate = new();
    private string _html = "";

    public LiveBrowserServer()
    {
        int port;
        // a free port of this machine, asked of the system
        using (var probe = new TcpListener(IPAddress.Loopback, 0)) { probe.Start(); port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop(); }
        Address = "http://localhost:" + port + "/";
        _listener.Prefixes.Add(Address);
        _listener.Start();
        _ = ServeAsync();
    }

    /// <summary>Where the browser opens the page.</summary>
    public string Address { get; }

    /// <summary>The page now; the open tabs reload to it.</summary>
    public void Publish(string html)
    {
        List<HttpListenerResponse> tabs;
        lock (_gate) { _html = html; tabs = [.. _tabs]; }
        var message = Encoding.UTF8.GetBytes("data: reload\n\n");
        foreach (var tab in tabs)
        {
            try { tab.OutputStream.Write(message); tab.OutputStream.Flush(); }
            catch (Exception ex) when (ex is IOException or HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                lock (_gate) _tabs.Remove(tab);
            }
        }
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync().ConfigureAwait(false); }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }
            try
            {
                var response = context.Response;
                if (context.Request.Url?.AbsolutePath == Events)
                {
                    response.ContentType = "text/event-stream";
                    response.Headers["Cache-Control"] = "no-cache";
                    response.SendChunked = true;
                    var hello = Encoding.UTF8.GetBytes(": urbe\n\n");
                    response.OutputStream.Write(hello);
                    response.OutputStream.Flush();
                    lock (_gate) _tabs.Add(response);
                    continue;
                }
                string html;
                lock (_gate) html = _html;
                var body = Encoding.UTF8.GetBytes(WithReloader(html));
                response.ContentType = "text/html; charset=utf-8";
                response.Headers["Cache-Control"] = "no-store";
                response.ContentLength64 = body.Length;
                await response.OutputStream.WriteAsync(body).ConfigureAwait(false);
                response.Close();
            }
            catch (Exception ex) when (ex is IOException or HttpListenerException or ObjectDisposedException or InvalidOperationException) { }
        }
    }

    private static string WithReloader(string html)
    {
        var at = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return at >= 0 ? html[..at] + Reloader + html[at..] : html + Reloader;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var tab in _tabs) { try { tab.Close(); } catch (Exception ex) when (ex is IOException or HttpListenerException or ObjectDisposedException or InvalidOperationException) { } }
            _tabs.Clear();
        }
        try { _listener.Stop(); _listener.Close(); } catch (ObjectDisposedException) { }
    }
}
