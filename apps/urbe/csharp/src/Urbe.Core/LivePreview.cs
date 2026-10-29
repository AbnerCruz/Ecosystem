using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// CST-141: the HTML being edited, as a page that runs on its own in the isolated viewer, the way a Live Server shows
/// it. The page has no access to the vault (no files, no bridge), so what it uses from the vault goes inside it: style
/// sheets (with their own url()), scripts, ES modules (each import rewritten to an embedded copy), pictures and media,
/// and the text files of its folder for fetch() and XMLHttpRequest. What is read is the text as it is in the editor,
/// saved or not. A small script of the Urbe shows the page's errors inside the page, never in the editor.
/// </summary>
public static partial class LivePreview
{
    /// <param name="htmlPath">The page's path in the vault.</param>
    /// <param name="html">The page's text now (as in the editor).</param>
    /// <param name="readText">The text of a vault path (as in the editor when the file is open), or null.</param>
    /// <param name="readBytes">The bytes of a vault path, or null.</param>
    /// <param name="vaultTextFiles">The vault's text files; those in the page's folder answer fetch().</param>
    public static string Build(string htmlPath, string html, Func<string, string?> readText, Func<string, byte[]?> readBytes, IEnumerable<string>? vaultTextFiles = null)
    {
        ArgumentNullException.ThrowIfNull(htmlPath);
        html ??= "";
        var modules = new Dictionary<string, string>(StringComparer.Ordinal);
        string? DataOf(string path)
        {
            var bytes = readBytes(path);
            return bytes is null ? null : "data:" + MimeOf(path) + ";base64," + Convert.ToBase64String(bytes);
        }

        // <link rel="stylesheet" href="…"> → <style> with the sheet, its url() resolved from the sheet's own folder
        html = LinkTag().Replace(html, m =>
        {
            var tag = m.Value;
            if (Attr(tag, "rel") is not { } rel || !rel.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("stylesheet", StringComparer.OrdinalIgnoreCase)) return tag;
            if (Attr(tag, "href") is not { } href || Resolve(htmlPath, href) is not { } path || readText(path) is not { } css) return tag;
            var media = Attr(tag, "media") is { } md ? " media=\"" + Escape(md) + "\"" : "";
            return "<style data-urbe-src=\"" + Escape(href) + "\"" + media + ">" + Css(css, path, DataOf).Replace("</style", "<\\/style", StringComparison.OrdinalIgnoreCase) + "</style>";
        });

        // <script src="…"></script> → the script itself; a module with its imports embedded
        html = ScriptTag().Replace(html, m =>
        {
            var open = m.Groups["open"].Value;
            if (Attr(open, "src") is not { } src || Resolve(htmlPath, src) is not { } path || readText(path) is not { } code) return m.Value;
            bool module = string.Equals(Attr(open, "type"), "module", StringComparison.OrdinalIgnoreCase);
            if (module) code = Module(path, code, readText, modules, []);
            var type = Attr(open, "type") is { } t ? " type=\"" + Escape(t) + "\"" : "";
            return "<script" + type + " data-urbe-src=\"" + Escape(src) + "\">" + Unclosable(code) + "</script>";
        });

        // inline modules import from the page's folder too
        html = InlineScript().Replace(html, m =>
        {
            var open = m.Groups["open"].Value;
            if (Attr(open, "src") is not null || !string.Equals(Attr(open, "type"), "module", StringComparison.OrdinalIgnoreCase)) return m.Value;
            return open + Unclosable(Module(htmlPath, m.Groups["body"].Value, readText, modules, [htmlPath])) + "</script>";
        });

        // <style> blocks of the page
        html = StyleBlock().Replace(html, m => m.Groups["open"].Value + Css(m.Groups["body"].Value, htmlPath, DataOf) + "</style>");

        // pictures, media and the like: src, poster, srcset and the page's icon
        html = UrlAttribute().Replace(html, m =>
        {
            var value = m.Groups["value"].Value;
            if (Resolve(htmlPath, value) is not { } path || DataOf(path) is not { } data) return m.Value;
            return m.Groups["name"].Value + "=" + m.Groups["q"].Value + data + m.Groups["q"].Value;
        });
        html = SrcsetAttribute().Replace(html, m =>
        {
            var parts = m.Groups["value"].Value.Split(',').Select(part =>
            {
                var bits = part.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (bits.Length == 0 || Resolve(htmlPath, bits[0]) is not { } path || DataOf(path) is not { } data) return part.Trim();
                return data + (bits.Length > 1 ? " " + bits[1] : "");
            });
            return m.Groups["name"].Value + "=" + m.Groups["q"].Value + string.Join(", ", parts) + m.Groups["q"].Value;
        });

        return Inject(html, LiveScript(htmlPath, vaultTextFiles, DataOf));
    }

    /// <summary>
    /// The vault path an address of the page means: relative to the page's folder, or to the vault's root when it starts
    /// with "/" (the root of a Live Server); query and fragment dropped, %20 decoded. Null for another site, a data:
    /// address, a fragment alone or a path that leaves the vault.
    /// </summary>
    public static string? Resolve(string fromPath, string address)
    {
        var a = (address ?? "").Trim();
        if (a.Length == 0 || a.StartsWith('#') || a.StartsWith("//", StringComparison.Ordinal) || Scheme().IsMatch(a)) return null;
        var cut = a.IndexOfAny(['?', '#']);
        if (cut >= 0) a = a[..cut];
        if (a.Length == 0) return null;
        try { a = Uri.UnescapeDataString(a); } catch (UriFormatException) { return null; }
        var parts = new List<string>();
        if (!a.StartsWith('/'))
            parts.AddRange(Folder(fromPath).Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var segment in a.Split('/'))
        {
            if (segment.Length == 0 || segment == ".") continue;
            if (segment == "..") { if (parts.Count == 0) return null; parts.RemoveAt(parts.Count - 1); continue; }
            parts.Add(segment);
        }
        return parts.Count == 0 ? null : string.Join('/', parts);
    }

    private static string Folder(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";

    private static string MimeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".js" or ".mjs" or ".cjs" => "text/javascript",
        ".css" => "text/css",
        ".json" => "application/json",
        ".html" or ".htm" => "text/html",
        ".svg" => "image/svg+xml",
        ".txt" or ".md" or ".csv" or ".xml" => "text/plain",
        var e when VaultTextFiles.Extensions.Contains(e) => "text/plain",
        _ => VaultFileKinds.Mime(path)
    };

    private static string Css(string css, string cssPath, Func<string, string?> dataOf) =>
        CssUrl().Replace(css, m =>
        {
            var value = m.Groups["value"].Value.Trim();
            return Resolve(cssPath, value) is { } path && dataOf(path) is { } data ? "url(\"" + data + "\")" : m.Value;
        });

    /// <summary>A module whose relative imports point to embedded copies of the modules they name (a cycle is left as written).</summary>
    private static string Module(string path, string code, Func<string, string?> readText, Dictionary<string, string> done, HashSet<string> stack)
    {
        stack.Add(path);
        var result = ImportSpecifier().Replace(code, m =>
        {
            var spec = m.Groups["spec"].Value;
            if (Resolve(path, spec) is not { } target || stack.Contains(target)) return m.Value;
            if (!done.TryGetValue(target, out var url))
            {
                if (readText(target) is not { } text) return m.Value;
                url = "data:text/javascript;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(Module(target, text, readText, done, [.. stack])));
                done[target] = url;
            }
            return m.Groups["head"].Value + m.Groups["q"].Value + url + m.Groups["q"].Value;
        });
        stack.Remove(path);
        return result;
    }

    private static string Unclosable(string code) => Regex.Replace(code, "</script", "<\\/script", RegexOptions.IgnoreCase);

    /// <summary>The value of an attribute of an opening tag, or null.</summary>
    private static string? Attr(string tag, string name)
    {
        var m = Regex.Match(tag, @"\s" + Regex.Escape(name) + @"\s*=\s*(?:""(?<v>[^""]*)""|'(?<v>[^']*)'|(?<v>[^\s>""']+))", RegexOptions.IgnoreCase);
        return m.Success ? System.Net.WebUtility.HtmlDecode(m.Groups["v"].Value) : null;
    }

    private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value);

    /// <summary>The Urbe's script, first in the page: the page's errors in a panel of the page; fetch() and XHR of the folder's files.</summary>
    private static string LiveScript(string htmlPath, IEnumerable<string>? vaultTextFiles, Func<string, string?> dataOf)
    {
        var folder = Folder(htmlPath);
        // no reflection (the Android build is trimmed): the map is written as JSON nodes
        var files = new System.Text.Json.Nodes.JsonObject();
        foreach (var file in (vaultTextFiles ?? []).Distinct(StringComparer.Ordinal))
            if ((folder.Length == 0 ? !file.StartsWith('.') : file.StartsWith(folder + "/", StringComparison.Ordinal)) && dataOf(file) is { } data)
                files[file] = data;
        var map = files.ToJsonString();
        var dir = System.Text.Json.Nodes.JsonValue.Create(folder).ToJsonString();
        return "<script id=\"urbe-live\">(function(){" +
            "var F=" + map + ",D=" + dir + ";" +
            "function key(u){if(typeof u!=='string'||/^[a-z][a-z0-9+.-]*:/i.test(u)||u.slice(0,2)==='//')return null;u=u.split('#')[0].split('?')[0];try{u=decodeURIComponent(u)}catch(e){}" +
            "var p=u.charAt(0)==='/'?[]:(D?D.split('/'):[]);u.split('/').forEach(function(s){if(!s||s==='.')return;if(s==='..')p.pop();else p.push(s)});return p.join('/')}" +
            "function to(u){var k=key(u);return k!==null&&Object.prototype.hasOwnProperty.call(F,k)?F[k]:u}" +
            "var of=window.fetch;if(of)window.fetch=function(i,o){if(typeof i==='string')i=to(i);else if(i&&i.url&&to(i.url)!==i.url)i=to(i.url);return of.call(this,i,o)};" +
            "var xo=XMLHttpRequest.prototype.open;XMLHttpRequest.prototype.open=function(m,u){var a=Array.prototype.slice.call(arguments);a[1]=to(u);return xo.apply(this,a)};" +
            "function show(t){var b=document.getElementById('urbe-live-erros');if(!b){b=document.createElement('div');b.id='urbe-live-erros';" +
            "b.style.cssText='position:fixed;left:8px;right:8px;bottom:8px;max-height:40%;overflow:auto;z-index:2147483647;background:#2b1215;color:#ffd7d7;font:12px/1.45 monospace;padding:8px 10px;border-radius:8px;border:1px solid #a33;white-space:pre-wrap';" +
            "(document.body||document.documentElement).appendChild(b)}var l=document.createElement('div');l.textContent=t;b.appendChild(l)}" +
            "window.addEventListener('error',function(e){if(e.message)show(e.message+(e.lineno?' (linha '+e.lineno+')':''));else if(e.target&&e.target.tagName)show('Não carregou: '+(e.target.src||e.target.href||e.target.tagName))},true);" +
            "window.addEventListener('unhandledrejection',function(e){var r=e.reason;show('Promessa rejeitada: '+(r&&r.message?r.message:String(r)))});" +
            "var ce=console.error;console.error=function(){try{show(Array.prototype.map.call(arguments,function(a){return a&&a.message?a.message:String(a)}).join(' '))}catch(x){}return ce&&ce.apply(console,arguments)};" +
            "})();</script>";
    }

    /// <summary>The script goes first in the head (or right after the doctype or html tag when there is none).</summary>
    private static string Inject(string html, string script)
    {
        foreach (var anchor in new[] { HeadOpen(), HtmlOpen(), Doctype() })
            if (anchor.Match(html) is { Success: true } m) return html[..(m.Index + m.Length)] + script + html[(m.Index + m.Length)..];
        return script + html;
    }

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9+.-]*:")] private static partial Regex Scheme();
    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex LinkTag();
    [GeneratedRegex(@"(?<open><script\b[^>]*\bsrc\s*=[^>]*>)\s*</script\s*>", RegexOptions.IgnoreCase)] private static partial Regex ScriptTag();
    [GeneratedRegex(@"(?<open><script\b[^>]*>)(?<body>.*?)</script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex InlineScript();
    [GeneratedRegex(@"(?<open><style\b[^>]*>)(?<body>.*?)</style\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex StyleBlock();
    [GeneratedRegex(@"(?<=\s)(?<name>(?:src|poster|data)|(?<=<link\b[^>]*\brel\s*=\s*[""']?(?:shortcut\s+)?icon[""']?[^>]*)href)\s*=\s*(?<q>[""'])(?<value>[^""']*)\k<q>", RegexOptions.IgnoreCase)] private static partial Regex UrlAttribute();
    [GeneratedRegex(@"(?<=\s)(?<name>srcset)\s*=\s*(?<q>[""'])(?<value>[^""']*)\k<q>", RegexOptions.IgnoreCase)] private static partial Regex SrcsetAttribute();
    [GeneratedRegex(@"url\(\s*(?<q>[""']?)(?<value>[^""')]+)\k<q>\s*\)", RegexOptions.IgnoreCase)] private static partial Regex CssUrl();
    [GeneratedRegex(@"(?<head>\bimport\s*(?:[\w*{}\s,]+\sfrom\s*)?|\bexport\s+[\w*{}\s,]+\sfrom\s*|\bimport\s*\(\s*)(?<q>[""'])(?<spec>(?:\.{1,2}/|/)[^""']+)\k<q>")] private static partial Regex ImportSpecifier();
    [GeneratedRegex(@"<head\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex HeadOpen();
    [GeneratedRegex(@"<html\b[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex HtmlOpen();
    [GeneratedRegex(@"<!doctype[^>]*>", RegexOptions.IgnoreCase)] private static partial Regex Doctype();
}
