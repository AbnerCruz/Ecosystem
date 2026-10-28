using System.Security.Cryptography;
using System.Text.Json;
using Android.Webkit;
using Java.Interop;
using Urbe.Client;

namespace Urbe.AndroidHost;

/// <summary>
/// The only JavaScript-visible operation is a validated message routed to a single exported Atlas Map session.
/// Android's AddJavascriptInterface is exposed to every frame; the Atlas viewer also applies a no-frames CSP,
/// blocks navigation, and requires this per-viewer nonce. Other HTML and Studio never receive this object.
/// </summary>
internal sealed class AtlasMapJavascriptBridge : Java.Lang.Object
{
    private readonly WeakReference<WebView> _web;
    private readonly AtlasMapSession _session;
    private readonly string _nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private int _revoked;

    public AtlasMapJavascriptBridge(WebView web, AtlasMapSession session)
    {
        _web = new WeakReference<WebView>(web);
        _session = session;
    }

    /// <summary>Installed after the original exported page has booted: no nonce/shim leaks into AtlasMap.exportHTML().</summary>
    public string AdapterScript
    {
        get
        {
            var nonce = JsonSerializer.Serialize(_nonce);
            return """
                (function(){
                  'use strict';
                  if(!window.AtlasMap || window.AtlasHost) return;
                  const secret = NONCE;
                  let sequence = 0;
                  const pending = new Map();
                  window.__urbeAtlasReply = function(message) {
                    if(!message || !Number.isSafeInteger(message.id)) return;
                    const item = pending.get(message.id);
                    if(!item) return;
                    pending.delete(message.id);
                    if(message.ok === true) item.resolve(message.result);
                    else item.reject(new Error(message.error || 'O Urbe não confirmou a operação'));
                  };
                  function invoke(op, request) {
                    return new Promise((resolve, reject) => {
                      const id = ++sequence;
                      pending.set(id, {resolve,reject});
                      try {
                        UrbeAtlas.postMessage(JSON.stringify({nonce:secret,id,op,request}));
                      } catch(error) {
                        pending.delete(id);
                        reject(error);
                      }
                    });
                  }
                  window.AtlasHost = Object.freeze({
                    version:1,
                    openNote:request=>invoke('openNote',request),
                    saveMap:request=>invoke('saveMap',request)
                  });
                })();
                """.Replace("NONCE", nonce, StringComparison.Ordinal);
        }
    }

    [JavascriptInterface]
    [Export("postMessage")]
    public void PostMessage(string message)
    {
        if (Volatile.Read(ref _revoked) != 0 || string.IsNullOrEmpty(message) ||
            message.Length > AtlasMapSession.MaximumBytes * 2) return;
        // Android calls JavascriptInterface on a dedicated background thread; never touch UI here.
        _ = HandleAsync(message);
    }

    private async Task HandleAsync(string message)
    {
        int id = -1;
        try
        {
            using var json = JsonDocument.Parse(message);
            var root = json.RootElement;
            if (!root.TryGetProperty("nonce", out var token) || token.GetString() != _nonce ||
                !root.TryGetProperty("id", out var idNode) || !idNode.TryGetInt32(out id) || id < 1 ||
                !root.TryGetProperty("op", out var operation) ||
                !root.TryGetProperty("request", out var request) || request.ValueKind != JsonValueKind.Object)
                return;
            if (Volatile.Read(ref _revoked) != 0) return;
            var mapId = request.TryGetProperty("mapId", out var mid) && mid.ValueKind == JsonValueKind.String ? mid.GetString() ?? "" : "";
            if (operation.GetString() == "openNote")
            {
                var path = request.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
                var ok = await _session.OpenNoteAsync(mapId, path);
                Reply(id, ok, mapId, 0, ok ? null : "Nota ausente, caminho inválido ou navegação recusada.");
            }
            else if (operation.GetString() == "saveMap")
            {
                var revision = request.TryGetProperty("revision", out var rv) && rv.TryGetInt64(out var n) ? n : -1;
                var html = request.TryGetProperty("html", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() ?? "" : "";
                var result = await _session.SaveAsync(mapId, revision, html);
                Reply(id, result.Ok, result.MapId, result.Revision, result.Error);
            }
            else Reply(id, false, "", 0, "Operação não autorizada.");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or IOException)
        {
            if (id >= 1) Reply(id, false, "", 0, "Mensagem do mapa inválida.");
        }
    }

    private void Reply(int id, bool ok, string mapId, long revision, string? error)
    {
        if (Volatile.Read(ref _revoked) != 0 || !_web.TryGetTarget(out var web)) return;
        var response = JsonSerializer.Serialize(new { id, ok, result = new { ok, mapId, revision }, error });
        try
        {
            web.Post(() =>
            {
                try { if (Volatile.Read(ref _revoked) == 0 && web.Handle != IntPtr.Zero)
                    web.EvaluateJavascript("window.__urbeAtlasReply(" + response + ")", null); }
                catch (ObjectDisposedException) { }
            });
        }
        catch (ObjectDisposedException) { }
    }

    public void Revoke()
    {
        Interlocked.Exchange(ref _revoked, 1);
        _session.Revoke();
    }
}
