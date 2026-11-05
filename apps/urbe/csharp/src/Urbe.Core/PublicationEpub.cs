using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>CST-165: EPUB3 package generated from semantic live notes, never an HTML file renamed as EPUB.</summary>
public static class PublicationEpub
{
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";
    private static readonly XNamespace Opf = "http://www.idpf.org/2007/opf";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Epub = "http://www.idpf.org/2007/ops";

    public static async Task<byte[]> ExportAsync(PublicationProject project, PublicationSourceCatalog catalog,
        Func<string, Task<byte[]?>>? readAsset = null, CancellationToken cancellationToken = default, Func<string, string?>? data = null)
    {
        PublicationProjectCodec.Validate(project);
        if (project.Sections.Count == 0) throw new InvalidDataException("Adicione capítulos antes de exportar o livro.");
        var files = new Dictionary<string, (string Mime, byte[] Bytes)>(StringComparer.Ordinal);
        var sources = new List<PublicationSourceView>();
        var chapters = new List<(string File, string Title, XDocument Document)>();
        foreach (var piece in project.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = catalog.Read(piece.Source) ?? throw new InvalidDataException("Nota de origem indisponível: " + piece.Source.DocumentId);
            sources.Add(source);
            var content = NoteMarks.StripComments(MarkdownEngine.SplitFrontmatter(source.Markdown).Body);
            var body = new XElement(Xhtml + "body");
            var roots = NoteContainers.ParseRich(content);
            foreach (var node in roots) body.Add(await ConvertNode(node, source));
            var title = Path.GetFileNameWithoutExtension(source.Path);
            chapters.Add(($"chapter-{chapters.Count + 1}.xhtml", title, Document(title, body)));
        }

        // Asset reads can suspend. Never combine chapters from an obsolete revision with newly saved sources.
        foreach (var source in sources)
        {
            var current = catalog.Read(new PublicationSourceReference(source.DocumentId, source.Fragment));
            if (current is null || current.Revision != source.Revision || current.Markdown != source.Markdown)
                throw new InvalidOperationException("Uma fonte mudou durante a exportação. Exporte novamente para usar a revisão atual.");
        }
        var nav = new XElement(Xhtml + "nav", new XAttribute(Epub + "type", "toc"), new XAttribute("id", "toc"),
            new XElement(Xhtml + "h1", "Sumário"), new XElement(Xhtml + "ol", chapters.Select(c =>
                new XElement(Xhtml + "li", new XElement(Xhtml + "a", new XAttribute("href", c.File), c.Title)))));
        var manifest = new XElement(Opf + "manifest",
            new XElement(Opf + "item", new XAttribute("id", "nav"), new XAttribute("href", "nav.xhtml"), new XAttribute("media-type", "application/xhtml+xml"), new XAttribute("properties", "nav")),
            new XElement(Opf + "item", new XAttribute("id", "style"), new XAttribute("href", "style.css"), new XAttribute("media-type", "text/css")));
        var spine = new XElement(Opf + "spine");
        for (int i = 0; i < chapters.Count; i++)
        {
            var id = "chapter-" + (i + 1);
            manifest.Add(new XElement(Opf + "item", new XAttribute("id", id), new XAttribute("href", chapters[i].File), new XAttribute("media-type", "application/xhtml+xml")));
            spine.Add(new XElement(Opf + "itemref", new XAttribute("idref", id)));
        }
        foreach (var (path, asset) in files)
            manifest.Add(new XElement(Opf + "item", new XAttribute("id", "asset-" + manifest.Elements().Count()), new XAttribute("href", path), new XAttribute("media-type", asset.Mime)));
        var package = new XDocument(new XElement(Opf + "package", new XAttribute("version", "3.0"), new XAttribute("unique-identifier", "pub-id"), new XAttribute(XNamespace.Xml + "lang", "pt-BR"),
            new XElement(Opf + "metadata", new XAttribute(XNamespace.Xmlns + "dc", Dc),
                new XElement(Dc + "identifier", new XAttribute("id", "pub-id"), project.PublicationId),
                new XElement(Dc + "title", project.Title), new XElement(Dc + "language", "pt-BR"),
                new XElement(Opf + "meta", new XAttribute("property", "dcterms:modified"), DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture))), manifest, spine));
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "mimetype", Encoding.ASCII.GetBytes("application/epub+zip"), CompressionLevel.NoCompression);
            XNamespace container = "urn:oasis:names:tc:opendocument:xmlns:container";
            WriteXml(zip, "META-INF/container.xml", new XDocument(new XElement(container + "container", new XAttribute("version", "1.0"),
                new XElement(container + "rootfiles", new XElement(container + "rootfile", new XAttribute("full-path", "EPUB/package.opf"), new XAttribute("media-type", "application/oebps-package+xml"))))));
            WriteXml(zip, "EPUB/package.opf", package);
            WriteXml(zip, "EPUB/nav.xhtml", Document(project.Title, new XElement(Xhtml + "body", nav)));
            Write(zip, "EPUB/style.css", Encoding.UTF8.GetBytes("body{font-family:serif;line-height:1.5;margin:5%}img{max-width:100%;height:auto}pre{white-space:pre-wrap;overflow-wrap:anywhere}table{border-collapse:collapse}td,th{border:1px solid;padding:.4em}h1,h2,h3{break-after:avoid}p{orphans:3;widows:3}.ubox{border-left:4px solid #9c7b45;background:#f6f3ee;padding:.4em 1em;margin:1em 0}.ubox-title{margin:0 0 .3em}"));
            foreach (var chapter in chapters) WriteXml(zip, "EPUB/" + chapter.File, chapter.Document);
            foreach (var (path, asset) in files) Write(zip, "EPUB/" + path, asset.Bytes);
        }
        return stream.ToArray();

        async Task<XNode> ConvertNode(RichNode node, PublicationSourceView source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Tag == RichNode.TextTag) return new XText(node.Text);
            if (node.Tag == "input") return new XElement(Xhtml + "span", node.Attrs.ContainsKey("checked") ? "☑ " : "☐ ");
            if (node.Tag == "pre" && NoteProgress.IsLanguage(node.Attr("data-lang")))
            {
                var result = NoteProgress.Evaluate(Plain(node), source.Markdown, id => catalog.Read(new(id))?.Markdown);
                if (!result.IsValid) throw new InvalidDataException(result.Error);
                return new XElement(Xhtml + "p", result.Label + ": " + result.Summary);
            }
            if (node.Tag == "pre" && Data.DataBlock.IsLanguage(node.Attr("data-lang")))
            {
                try
                {
                    var result = Data.DataBlock.Run(Plain(node), data ?? (_ => null));
                    return new XElement(Xhtml + "table", new XAttribute("class", "urbe-data"),
                        new XElement(Xhtml + "tr", result.Table.Columns.Select(c => new XElement(Xhtml + "th", c))),
                        result.Table.Rows.Select(r => new XElement(Xhtml + "tr", r.Select(v => new XElement(Xhtml + "td", v.Text)))));
                }
                catch (InvalidDataException ex) { return new XElement(Xhtml + "p", "Dados indisponíveis: " + ex.Message); }
            }
            if (node.Tag == "pre" && Calculus.Calculator.IsLanguage(node.Attr("data-lang")))
                return new XElement(Xhtml + "table", new XAttribute("class", "urbe-calc"), Calculus.Calculator.Run(Plain(node)).Where(l => l.Input.Trim().Length > 0).Select(l =>
                    new XElement(Xhtml + "tr", new XElement(Xhtml + "td", new XElement(Xhtml + "code", l.Input.Trim())), new XElement(Xhtml + "td", l.Result ?? l.Error ?? ""))));
            if (node.Tag == "pre" && FunctionPlot.IsPlotLanguage(node.Attr("data-lang")))
            {
                var plotPath = "assets/plot-" + files.Count + ".svg";
                files[plotPath] = ("image/svg+xml", Encoding.UTF8.GetBytes(FunctionPlot.Parse(Plain(node)).ToSvg()));
                return new XElement(Xhtml + "img", new XAttribute("src", plotPath), new XAttribute("alt", "Gráfico: " + Plain(node).Replace('\n', ' ')));
            }
            if (node.Attr("data-tex") is { Length: > 0 } tex)
            {
                var math = MathRenderer.RenderSvg(tex, node.Tag == "div", 18);
                if (!math.Success) throw new InvalidDataException("Fórmula não renderizada: " + math.Diagnostic);
                var path = "assets/math-" + files.Count + ".svg";
                files[path] = ("image/svg+xml", Encoding.UTF8.GetBytes(math.Svg ?? ""));
                return new XElement(Xhtml + "img", new XAttribute("src", path), new XAttribute("alt", tex));
            }
            if (node.Tag == "img")
            {
                var raw = node.Attr("src");
                var path = ResolveAsset(source.Path, raw);
                if (readAsset is null || await readAsset(path) is not { Length: > 0 } bytes)
                    throw new InvalidDataException("Imagem indisponível no vault: " + path);
                var (extension, mime) = ImageType(bytes);
                var packaged = "assets/image-" + files.Count + extension;
                files[packaged] = (mime, bytes);
                return new XElement(Xhtml + "img", new XAttribute("src", packaged), new XAttribute("alt", node.Attr("alt")));
            }
            var tag = node.Tag is "aside" or "p" or "div" or "section" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "strong" or "em" or "s" or "code" or "pre" or "ul" or "ol" or "li" or "table" or "thead" or "tbody" or "tr" or "th" or "td" or "blockquote" or "br" or "hr" or "a" ? node.Tag : "span";
            var element = new XElement(Xhtml + tag);
            if (tag == "a" && Uri.TryCreate(node.Attr("href"), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "mailto")
                element.SetAttributeValue("href", uri.AbsoluteUri);
            if (node.Attr("id") is { Length: > 0 } id) element.SetAttributeValue("id", id);
            if (tag == "aside" && node.HasClass("ubox")) { element.SetAttributeValue("class", "ubox"); element.SetAttributeValue("data-type", node.Attr("data-type")); }
            if (tag == "p" && node.HasClass("ubox-title")) element.SetAttributeValue("class", "ubox-title");
            foreach (var child in node.Children) element.Add(await ConvertNode(child, source));
            return element;
        }
    }

    private static XDocument Document(string title, XElement body) => new(new XElement(Xhtml + "html",
        new XAttribute(XNamespace.Xml + "lang", "pt-BR"), new XAttribute("lang", "pt-BR"), new XAttribute(XNamespace.Xmlns + "epub", Epub),
        new XElement(Xhtml + "head", new XElement(Xhtml + "title", title), new XElement(Xhtml + "link", new XAttribute("rel", "stylesheet"), new XAttribute("href", "style.css"), new XAttribute("type", "text/css"))), body));

    private static string Plain(RichNode node) => node.Tag == RichNode.TextTag ? node.Text : string.Concat(node.Children.Select(Plain));
    private static void WriteXml(ZipArchive zip, string name, XDocument doc) => Write(zip, name, Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + doc));
    private static void Write(ZipArchive zip, string name, byte[] bytes, CompressionLevel compression = CompressionLevel.Optimal)
    { using var target = zip.CreateEntry(name, compression).Open(); target.Write(bytes); }

    internal static string ResolveAsset(string source, string relative)
    {
        if (relative.Contains(':') || relative.StartsWith('/') || relative.Contains('\\'))
            throw new InvalidDataException("EPUB requer imagens locais relativas ao vault; importe a imagem antes de exportar.");
        var parts = new List<string>((Path.GetDirectoryName(source)?.Replace('\\', '/') ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries));
        foreach (var part in Uri.UnescapeDataString(relative).Split('/'))
        {
            if (part is "" or ".") continue;
            if (part == "..") { if (parts.Count == 0) throw new InvalidDataException("Imagem fora do vault."); parts.RemoveAt(parts.Count - 1); }
            else if (part.Contains(':') || part.Contains('\\')) throw new InvalidDataException("Caminho de imagem inválido.");
            else parts.Add(part);
        }
        return string.Join('/', parts);
    }

    internal static (string Extension, string Mime) ImageType(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return (".png", "image/png");
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255) return (".jpg", "image/jpeg");
        if (bytes.Length >= 6 && Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a") return (".gif", "image/gif");
        throw new InvalidDataException("Imagem EPUB sem formato PNG, JPEG ou GIF válido.");
    }
}
