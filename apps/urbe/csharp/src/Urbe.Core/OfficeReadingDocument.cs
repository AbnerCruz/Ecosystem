using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>Read-only semantic documents, common slide objects and image archives. Complex objects are identified explicitly.</summary>
public sealed class OfficeReadingDocument : IReaderPublication
{
    private readonly ReaderArchive _archive;
    private readonly string _extension;
    private readonly byte[] _source;
    public bool IsPowerPoint => _extension == ".pptx";
    public ReadOnlyMemory<byte> SourceBytes => _source;
    private readonly List<string> _parts = [];
    private readonly List<List<XElement>> _wordParts = [];
    private readonly Dictionary<string, string> _wordImages = new(StringComparer.Ordinal);
    private double _slideWidth = 12192000, _slideHeight = 6858000;
    public string Title { get; }
    public string Sha256 { get; }
    public string Unit => _extension is ".pptx" or ".odp" ? "slide" : _extension == ".cbz" ? "image" : "chapter";
    public string PartLabel => Unit == "slide" ? "Slide" : Unit == "image" ? "Imagem" : "Seção";
    public string? FidelityWarning { get; private set; }
    public IReadOnlyList<ReaderChapter> Chapters { get; }

    public OfficeReadingDocument(string path, byte[] bytes)
    {
        Title = Path.GetFileName(path);
        _source = bytes;
        _extension = Path.GetExtension(path).ToLowerInvariant();
        Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        _archive = new ReaderArchive(bytes);
        try
        {
            if (_extension == ".pptx")
            {
                var presentation = _archive.Xml("ppt/presentation.xml");
                var size = presentation.Descendants().FirstOrDefault(e => e.Name.LocalName == "sldSz");
                _slideWidth = Number(size?.Attribute("cx"), _slideWidth); _slideHeight = Number(size?.Attribute("cy"), _slideHeight);
                var rels = Relationships("ppt/presentation.xml");
                foreach (var slide in presentation.Descendants().Where(e => e.Name.LocalName == "sldId"))
                {
                    var id = slide.Attributes().FirstOrDefault(a => a.Name.LocalName == "id" && a.Name.NamespaceName.Contains("relationships", StringComparison.Ordinal))?.Value;
                    if (id is null || !rels.TryGetValue(id, out var target)) throw new InvalidDataException("Apresentação com slide ausente.");
                    _parts.Add(target);
                }
            }
            else if (_extension == ".docx")
            {
                foreach (var rel in Relationships("word/document.xml")) _wordImages[rel.Key] = rel.Value;
                var document = _archive.Xml("word/document.xml");
                var body = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "body") ?? throw new InvalidDataException("DOCX sem corpo.");
                List<XElement> section = [];
                foreach (var element in body.Elements())
                {
                    bool heading = element.Descendants().Any(e => e.Name.LocalName == "pStyle" && e.Attributes().Any(a => a.Name.LocalName == "val" && a.Value is "Heading1" or "Titulo1"));
                    if (section.Count > 0 && (heading || section.Count >= 100)) { _wordParts.Add(section); section = []; }
                    if (element.Name.LocalName != "sectPr") section.Add(element);
                }
                if (section.Count > 0) _wordParts.Add(section);
                _parts.AddRange(Enumerable.Range(0, _wordParts.Count).Select(i => "section/" + i));
                FidelityWarning = "Leitura semântica DOCX; paginação e objetos complexos podem diferir do Word.";
            }
            else if (_extension == ".cbz")
                _parts.AddRange(_archive.Paths.Where(ComicBook.IsPage).Order(NaturalOrder.Instance));
            else if (_extension is ".odt" or ".odp")
            {
                var document = _archive.Xml("content.xml");
                if (_extension == ".odp") _parts.AddRange(document.Descendants().Where(e => e.Name.LocalName == "page").Select((_, i) => "page/" + i));
                else
                {
                    var text = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "text") ?? throw new InvalidDataException("ODT sem texto.");
                    _wordParts.AddRange(text.Elements().Chunk(100).Select(c => c.ToList()));
                    _parts.AddRange(Enumerable.Range(0, _wordParts.Count).Select(i => "section/" + i));
                }
                FidelityWarning = "Leitura semântica ODF; objetos e paginação podem diferir do aplicativo de origem.";
            }
            else throw new NotSupportedException("Este formato legado exige um aplicativo compatível; o original não foi convertido.");
            if (_parts.Count == 0) throw new InvalidDataException("Este arquivo não contém conteúdo legível neste formato.");
            Chapters = _parts.Select((p, i) => new ReaderChapter(p, PartLabel + " " + (i + 1))).ToArray();
        }
        catch { _archive.Dispose(); throw; }
    }

    public ReaderChapterContent ReadChapter(int index, double fontSize = 18, double lineHeight = 1.6)
    {
        if (index < 0 || index >= _parts.Count) throw new ArgumentOutOfRangeException(nameof(index));
        string body;
        if (_extension == ".pptx") body = "<p>" + H(ReadText(index)).Replace("\n", "</p><p>", StringComparison.Ordinal) + "</p>";
        else if (_extension == ".cbz") body = "<img style='max-width:100%;height:auto' src='" + ImageData(_parts[index]) + "' alt='" + H(Path.GetFileName(_parts[index])) + "'/>";
        else if (_extension is ".odt" or ".odp")
        {
            var elements = _extension == ".odt" ? _wordParts[index] : _archive.Xml("content.xml").Descendants().Where(e => e.Name.LocalName == "page").ElementAt(index).Elements().ToList();
            body = string.Concat(elements.Select(OdfElement));
        }
        else body = string.Concat(_wordParts[index].Select(WordElement));
        bool fixedLayout = _extension is ".pptx" or ".cbz";
        string html = "<!doctype html><html><head><meta charset='utf-8'/><meta name='viewport' content='width=device-width,initial-scale=1'/>" +
            "<meta http-equiv='Content-Security-Policy' content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'none'; connect-src 'none'; frame-src 'none'; object-src 'none'; form-action 'none'; base-uri 'none'\"/>" +
            "<style>body{margin:5%;font-family:serif;font-size:" + N(Math.Clamp(fontSize, 10, 48)) + "px;line-height:" + N(Math.Clamp(lineHeight, 1, 3)) + "}img,svg{max-width:100%}table{border-collapse:collapse;max-width:100%}td,th{border:1px solid #888;padding:.4em}pre{white-space:pre-wrap}</style></head><body>" + body + "</body></html>";
        return new(html, ReadText(index), fixedLayout);
    }
    public string ReadText(int index)
    {
        if (_extension == ".cbz") return "";
        var elements = _extension == ".pptx" ? _archive.Xml(_parts[index]).Descendants().Where(e => e.Name.LocalName == "t")
            : _extension == ".docx" ? _wordParts[index].SelectMany(e => e.DescendantsAndSelf()).Where(e => e.Name.LocalName == "t")
            : _extension == ".odp" ? _archive.Xml("content.xml").Descendants().Where(e => e.Name.LocalName == "page").ElementAt(index).Descendants().Where(e => e.Name.LocalName is "p" or "h")
            : _wordParts[index].SelectMany(e => e.DescendantsAndSelf()).Where(e => e.Name.LocalName is "p" or "h");
        return string.Join(_extension == ".docx" ? " " : "\n", elements.Select(e => e.Value));
    }

    private string WordElement(XElement element)
    {
        string name = element.Name.LocalName;
        if (name == "tbl") return "<table>" + string.Concat(element.Elements().Where(e => e.Name.LocalName == "tr").Select(row => "<tr>" + string.Concat(row.Elements().Where(e => e.Name.LocalName == "tc").Select(cell => "<td>" + string.Concat(cell.Elements().Select(WordElement)) + "</td>")) + "</tr>")) + "</table>";
        if (name != "p") return "";
        string tag = "p";
        var style = element.Descendants().FirstOrDefault(e => e.Name.LocalName == "pStyle")?.Attributes().FirstOrDefault(a => a.Name.LocalName == "val")?.Value;
        if (style is not null && Regex.Match(style, @"(?:Heading|Titulo)([1-6])", RegexOptions.IgnoreCase) is { Success: true } heading) tag = "h" + heading.Groups[1].Value;
        var content = new StringBuilder();
        foreach (var run in element.Descendants().Where(e => e.Name.LocalName == "r"))
        {
            string text = string.Concat(run.Descendants().Where(e => e.Name.LocalName is "t" or "tab" or "br").Select(e => e.Name.LocalName == "t" ? H(e.Value) : e.Name.LocalName == "br" ? "<br/>" : "&emsp;"));
            if (run.Descendants().Any(e => e.Name.LocalName == "b")) text = "<strong>" + text + "</strong>";
            if (run.Descendants().Any(e => e.Name.LocalName == "i")) text = "<em>" + text + "</em>";
            content.Append(text);
            foreach (var picture in run.Descendants().Where(e => e.Name.LocalName == "blip"))
            {
                var id = picture.Attributes().FirstOrDefault(a => a.Name.LocalName == "embed")?.Value;
                if (id is not null && _wordImages.TryGetValue(id, out var image)) content.Append("<img src='" + ImageData(image) + "' alt='Imagem do documento'/>");
            }
            if (run.Descendants().Any(e => e.Name.LocalName == "oMath")) content.Append("<span>[Equação Office: conferir no aplicativo de origem]</span>");
        }
        if (element.Descendants().Any(e => e.Name.LocalName is "oMath" or "oMathPara"))
            content.Append("<span role='note'>[Equação Office: conferir no aplicativo de origem]</span>");
        return "<" + tag + ">" + content + "</" + tag + ">";
    }
    private string OdfElement(XElement element)
    {
        string name = element.Name.LocalName;
        if (name == "table") return "<table>" + string.Concat(element.Descendants().Where(e => e.Name.LocalName == "table-row").Select(row => "<tr>" + string.Concat(row.Elements().Where(e => e.Name.LocalName == "table-cell").Select(cell => "<td>" + H(cell.Value) + "</td>")) + "</tr>")) + "</table>";
        if (name is "p" or "h") return "<" + (name == "h" ? "h2" : "p") + ">" + H(element.Value) + "</" + (name == "h" ? "h2" : "p") + ">";
        if (name == "image")
        {
            var href = element.Attributes().FirstOrDefault(a => a.Name.LocalName == "href")?.Value;
            return href is not null && ReaderArchive.Resolve("content.xml", href) is { } path && _archive.Contains(path) ? "<img src='" + ImageData(path) + "'/>" : "";
        }
        return string.Concat(element.Elements().Select(OdfElement));
    }

    private Dictionary<string, string> Relationships(string path)
    {
        var relPath = path[..(path.LastIndexOf('/') + 1)] + "_rels/" + Path.GetFileName(path) + ".rels";
        if (!_archive.Contains(relPath)) return new(StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rel in _archive.Xml(relPath).Descendants().Where(e => e.Name.LocalName == "Relationship"))
            if ((string?)rel.Attribute("TargetMode") != "External" && (string?)rel.Attribute("Id") is { } id &&
                ReaderArchive.Resolve(path, (string?)rel.Attribute("Target") ?? "") is { } target && _archive.Contains(target)) result[id] = target;
        return result;
    }
    private string ImageData(string path) => "data:" + VaultFileKinds.Mime(path) + ";base64," + Convert.ToBase64String(_archive.Bytes(path));
    private static double Number(XAttribute? attribute, double fallback) => double.TryParse(attribute?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) && Math.Abs(value) <= 1e12 ? value : fallback;
    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string H(string text) => WebUtility.HtmlEncode(text);
    public void Dispose() => _archive.Dispose();
}
