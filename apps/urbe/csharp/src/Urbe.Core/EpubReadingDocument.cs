using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using VersOne.Epub;

namespace Urbe.Core;

public sealed record ReaderChapter(string Key, string Title);
public sealed record ReaderChapterContent(string Html, string Text, bool FixedLayout);
public interface IReaderPublication : IDisposable
{
    string Title { get; }
    string Sha256 { get; }
    string Unit { get; }
    string PartLabel { get; }
    string? FidelityWarning => null;
    IReadOnlyList<ReaderChapter> Chapters { get; }
    ReaderChapterContent ReadChapter(int index, double fontSize = 18, double lineHeight = 1.6);
    string ReadText(int index);
}

/// <summary>Lazy EPUB spine reader. Scripts, remote assets, archive escapes and DRM never gain host access.</summary>
public sealed class EpubReadingDocument : IReaderPublication
{
    private readonly ReaderArchive _archive;
    private readonly EpubBookRef _book;
    private readonly List<EpubLocalTextContentFileRef> _spine;
    public string Title => _book.Title;
    public string Unit => "chapter";
    public string PartLabel => "Capítulo";
    public string Sha256 { get; }
    public IReadOnlyList<ReaderChapter> Chapters { get; }
    public bool FixedLayout { get; }

    public EpubReadingDocument(byte[] bytes)
    {
        _archive = new ReaderArchive(bytes);
        try
        {
            var container = _archive.Xml("META-INF/container.xml");
            var opfPath = container.Descendants().FirstOrDefault(e => e.Name.LocalName == "rootfile")?.Attribute("full-path")?.Value
                ?? throw new InvalidDataException("EPUB sem pacote OPF.");
            var opf = _archive.Xml(opfPath);
            FixedLayout = opf.Descendants().Any(e => e.Name.LocalName == "meta" && (string?)e.Attribute("property") == "rendition:layout" && e.Value == "pre-paginated");
            if (_archive.Contains("META-INF/encryption.xml"))
            {
                var encryption = _archive.Xml("META-INF/encryption.xml");
                if (encryption.Descendants().Any(e => e.Name.LocalName == "EncryptionMethod" &&
                    (string?)e.Attribute("Algorithm") is not ("http://www.idpf.org/2008/embedding" or "http://ns.adobe.com/pdf/enc#RC")))
                    throw new NotSupportedException("Este EPUB usa DRM. Abra-o no aplicativo autorizado pelo fornecedor.");
            }
            foreach(var item in opf.Descendants().Where(e=>e.Name.LocalName=="item" && (((string?)e.Attribute("properties"))?.Split(' ').Contains("nav")==true || (string?)e.Attribute("media-type")=="application/x-dtbncx+xml")))
            {
                var navigation=ReaderArchive.Resolve(opfPath,(string?)item.Attribute("href")??"")??throw new InvalidDataException("Caminho de navegação EPUB inválido.");
                _archive.Xml(navigation); // Validate DTD/resolver policy before the upstream navigation parser sees it.
            }
            _book = EpubReader.OpenBook(new MemoryStream(bytes, writable: false));
            _spine = _book.GetReadingOrder();
            if (_spine.Count == 0) throw new InvalidDataException("EPUB sem capítulos no spine.");
            var titles = new Dictionary<string, string>(StringComparer.Ordinal);
            void Visit(IEnumerable<EpubNavigationItemRef> items)
            {
                foreach (var item in items)
                {
                    if (item.Link is { } link && !string.IsNullOrWhiteSpace(item.Title)) titles.TryAdd(link.ContentFilePath, item.Title);
                    Visit(item.NestedItems);
                }
            }
            Visit(_book.GetNavigation() ?? []);
            Chapters = _spine.Select((chapter, i) => new ReaderChapter(chapter.FilePath, titles.GetValueOrDefault(chapter.FilePath, "Capítulo " + (i + 1)))).ToArray();
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
        catch { _archive.Dispose(); throw; }
    }

    public ReaderChapterContent ReadChapter(int index, double fontSize = 18, double lineHeight = 1.6)
    {
        if (index < 0 || index >= _spine.Count) throw new ArgumentOutOfRangeException(nameof(index));
        string path = _spine[index].FilePath;
        var document = _archive.Xml(path);
        string plain = Plain(document);
        foreach (var element in document.Descendants().ToArray())
        {
            string name = element.Name.LocalName.ToLowerInvariant();
            if (name == "meta" && ((string?)element.Attribute("http-equiv"))?.Equals("refresh", StringComparison.OrdinalIgnoreCase) == true) { element.Remove(); continue; }
            if (name is "script" or "iframe" or "object" or "embed" or "form" or "input" or "button" or "base") { element.Remove(); continue; }
            foreach (var attribute in element.Attributes().ToArray())
            {
                string key = attribute.Name.LocalName.ToLowerInvariant();
                if (key.StartsWith("on", StringComparison.Ordinal) || key is "srcset" or "action" or "formaction") { attribute.Remove(); continue; }
                if (key == "style") attribute.Value = RewriteCss(attribute.Value, path);
                if (key is not ("src" or "href" or "poster")) continue;
                if (name == "a" && key == "href")
                {
                    string href = attribute.Value;
                    if (href.StartsWith('#')) continue;
                    string? resolved = ReaderArchive.Resolve(path, href);
                    int chapter = resolved is null ? -1 : _spine.FindIndex(p => p.FilePath == resolved);
                    if (chapter >= 0) attribute.Value = "https://urbe.reader.invalid/chapter/" + chapter + (href.Contains('#') ? "#" + href.Split('#', 2)[1] : "");
                    else if (ReaderUris.Web(href) is null) attribute.Remove();
                    continue;
                }
                if (name == "link" && key == "href") continue;
                attribute.Value = AssetData(path, attribute.Value);
            }
            if (name == "link" && (string?)element.Attribute("rel") == "stylesheet")
            {
                var css = ReaderArchive.Resolve(path, (string?)element.Attribute("href") ?? "");
                if (css is not null && _archive.Contains(css)) element.ReplaceWith(new XElement(element.Name.Namespace + "style", RewriteCss(_archive.Text(css), css)));
                else element.Remove();
            }
            else if (name == "style") element.Value = RewriteCss(element.Value, path);
        }
        XNamespace html = "http://www.w3.org/1999/xhtml";
        var head = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "head");
        if (head is not null)
        {
            head.AddFirst(new XElement(html + "meta", new XAttribute("http-equiv", "Content-Security-Policy"),
                new XAttribute("content", "default-src 'none'; img-src data:; font-src data:; style-src 'unsafe-inline'; script-src 'none'; connect-src 'none'; object-src 'none'; frame-src 'none'; form-action 'none'; base-uri 'none'")));
            if (!FixedLayout) head.Add(new XElement(html + "style", FormattableString.Invariant($"html{{font-size:{Math.Clamp(fontSize, 10, 48)}px!important}}body{{font-size:1rem!important;line-height:{Math.Clamp(lineHeight, 1, 3)}!important;margin:5%!important}}img,svg{{max-width:100%}}")));
        }
        return new(document.ToString(SaveOptions.DisableFormatting), plain, FixedLayout);
    }

    public string ReadText(int index) => Plain(_archive.Xml(_spine[index].FilePath));
    // Search, native fallback and DOM locators must count the same characters.
    // XElement.Value is already XML-decoded; decoding a second time corrupts literal entities.
    private static string Plain(XDocument document) => string.Concat(document.Descendants()
        .FirstOrDefault(e => e.Name.LocalName == "body")?.DescendantNodes().OfType<XText>()
        .Where(n => !n.Ancestors().Any(e => e.Name.LocalName.ToLowerInvariant() is
            "script" or "style" or "noscript" or "iframe" or "object" or "embed" or "form" or "input" or "button" or "base"))
        .Select(n => n.Value) ?? []);

    private string RewriteCss(string css, string from)
    {
        css = Regex.Replace(css, "@import[^;]+;", "", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        return Regex.Replace(css, "url\\(\\s*['\"]?([^)'\"]+)['\"]?\\s*\\)", m => "url('" + AssetData(from, m.Groups[1].Value.Trim()) + "')", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }
    private string AssetData(string from, string relative)
    {
        if (relative.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return ""; // only archive-owned assets are admitted
        var path = ReaderArchive.Resolve(from, relative);
        if (path is null || !_archive.Contains(path)) return "";
        string mime = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".svg" => "image/svg+xml", ".webp" => "image/webp",
            ".woff" => "font/woff", ".woff2" => "font/woff2", ".ttf" => "font/ttf", ".otf" => "font/otf", _ => "application/octet-stream"
        };
        return "data:" + mime + ";base64," + Convert.ToBase64String(_archive.Bytes(path));
    }
    public void Dispose() { _book.Dispose(); _archive.Dispose(); }
}
