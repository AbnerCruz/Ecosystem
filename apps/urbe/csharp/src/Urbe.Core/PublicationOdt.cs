using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>
/// CST-166: an OpenDocument text (.odt) from the publication's live notes, by semantics: outline headings, nested
/// lists, tables, embedded images, quote and code paragraph styles. Same reading as <see cref="PublicationDocx"/>.
/// </summary>
public static class PublicationOdt
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
    private static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace Meta = "urn:oasis:names:tc:opendocument:xmlns:meta:1.0";
    private static readonly XNamespace DC = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
    private const string Mime = "application/vnd.oasis.opendocument.text";
    /// <summary>A4 text width with 2 cm margins.</summary>
    private const double TextWidthCm = 17;

    public static async Task<PublicationExport> ExportAsync(PublicationProject project, PublicationSourceCatalog catalog,
        Func<string, Task<byte[]?>>? readAsset = null, CancellationToken cancellationToken = default) =>
        Write(await PublicationOffice.ReadAsync(project, catalog, readAsset, cancellationToken));

    public static PublicationExport Write(OfficeDocument doc)
    {
        var pictures = new List<(string Path, string Mime, byte[] Bytes)>();
        int tables = 0;
        var text = new XElement(Office + "text");
        text.Add(new XElement(Text + "p", new XAttribute(Text + "style-name", "Title"), Safe(doc.Title)));
        var book = doc.Layout == "book";
        if (book)
            text.Add(new XElement(Text + "table-of-content", new XAttribute(Text + "name", "Sumário"),
                new XElement(Text + "table-of-content-source", new XAttribute(Text + "outline-level", 3),
                    new XElement(Text + "index-title-template", "Sumário")),
                new XElement(Text + "index-body", new XElement(Text + "p", "Atualize o sumário: Ferramentas → Atualizar → Tudo."))));
        for (int c = 0; c < doc.Chapters.Count; c++)
        {
            var blocks = PublicationOffice.Flatten(doc.Chapters[c].Blocks).ToList();
            var first = true;
            for (int i = 0; i < blocks.Count; i++)
            {
                XElement element;
                if (blocks[i] is OfficeListItem)
                {
                    int end = i;
                    while (end < blocks.Count && blocks[end] is OfficeListItem) end++;
                    element = Lists(blocks.Skip(i).Take(end - i).Cast<OfficeListItem>().ToList(), text);
                    i = end - 1;
                    if (first && c > 0 && book) element.AddFirst(new XAttribute(Text + "continue-numbering", "false"));
                }
                else
                {
                    element = Block(blocks[i]);
                    // a chapter starts on a new page in a book
                    if (first && c > 0 && book && element.Name.Namespace == Text && element.Name.LocalName is "p" or "h")
                        element.SetAttributeValue(Text + "style-name", PageBreakStyle(element.Attribute(Text + "style-name")?.Value));
                    text.Add(element);
                }
                first = false;
            }
        }

        var content = new XElement(Office + "document-content", Namespaces(), new XAttribute(Office + "version", "1.3"),
            new XElement(Office + "automatic-styles", AutomaticStyles(doc)),
            new XElement(Office + "body", text));
        var styles = new XElement(Office + "document-styles", Namespaces(), new XAttribute(Office + "version", "1.3"), CommonStyles(), PageLayout());
        var meta = new XElement(Office + "document-meta", Namespaces(), new XAttribute(Office + "version", "1.3"),
            new XElement(Office + "meta", new XElement(DC + "title", Safe(doc.Title)), new XElement(DC + "language", "pt-BR"), new XElement(Meta + "generator", "Urbe")));
        var manifest = new XElement(Manifest + "manifest", new XAttribute(XNamespace.Xmlns + "manifest", Manifest), new XAttribute(Manifest + "version", "1.3"),
            FileEntry("/", Mime), FileEntry("content.xml", "text/xml"), FileEntry("styles.xml", "text/xml"), FileEntry("meta.xml", "text/xml"),
            pictures.Select(p => FileEntry(p.Path, p.Mime)));

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Bytes(zip, "mimetype", Encoding.ASCII.GetBytes(Mime), CompressionLevel.NoCompression);
            Xml(zip, "content.xml", content);
            Xml(zip, "styles.xml", styles);
            Xml(zip, "meta.xml", meta);
            foreach (var (path, _, bytes) in pictures) Bytes(zip, path, bytes, CompressionLevel.NoCompression);
            Xml(zip, "META-INF/manifest.xml", manifest);
        }
        return new PublicationExport(stream.ToArray(), doc.Limitations);

        XElement Block(OfficeBlock block) => block switch
        {
            OfficeHeading h => new XElement(Text + "h", new XAttribute(Text + "style-name", "Heading_20_" + Math.Clamp(h.Level, 1, 6)),
                new XAttribute(Text + "outline-level", Math.Clamp(h.Level, 1, 6)), Runs(h.Runs)),
            OfficeParagraph p => new XElement(Text + "p", new XAttribute(Text + "style-name", p.Style switch { "quote" => "Quotations", "math" => "Formula", "box" => "Box", "boxtitle" => "Box_20_Title", _ => "Text_20_body" }), Runs(p.Runs)),
            OfficeCode code => new XElement(Text + "p", new XAttribute(Text + "style-name", "Preformatted_20_Text"), CodeLines(code.Text)),
            OfficeTable table => TableElement(table),
            OfficeImage image => ImageElement(image),
            OfficeRule => new XElement(Text + "p", new XAttribute(Text + "style-name", "Horizontal_20_Line")),
            _ => new XElement(Text + "p")
        };

        XElement TableElement(OfficeTable table)
        {
            var columns = table.Rows.Max(r => r.Count);
            var t = new XElement(Table + "table", new XAttribute(Table + "name", "Tabela" + (++tables)), new XAttribute(Table + "style-name", "Tbl"),
                new XElement(Table + "table-column", new XAttribute(Table + "number-columns-repeated", columns), new XAttribute(Table + "style-name", "TblCol")));
            for (int r = 0; r < table.Rows.Count; r++)
            {
                var header = r == 0 && table.HeaderRow;
                var row = new XElement(Table + "table-row");
                foreach (var cell in table.Rows[r])
                    row.Add(new XElement(Table + "table-cell", new XAttribute(Table + "style-name", "TblCell"), new XAttribute(Office + "value-type", "string"),
                        new XElement(Text + "p", new XAttribute(Text + "style-name", header ? "Table_20_Heading" : "Table_20_Contents"), Runs(cell))));
                t.Add(header ? new XElement(Table + "table-header-rows", row) : row);
            }
            return t;
        }

        XElement ImageElement(OfficeImage image)
        {
            var path = "Pictures/image" + (pictures.Count + 1) + image.Extension;
            pictures.Add((path, image.Mime, image.Bytes));
            // 96 px per inch, never wider than the text
            double w = image.Width / 96.0 * 2.54, h = image.Height / 96.0 * 2.54;
            if (w > TextWidthCm) { h = h * TextWidthCm / w; w = TextWidthCm; }
            return new XElement(Text + "p", new XAttribute(Text + "style-name", "Figure"),
                new XElement(Draw + "frame", new XAttribute(Draw + "name", "Imagem" + pictures.Count), new XAttribute(Text + "anchor-type", "as-char"),
                    new XAttribute(Svg + "width", Cm(w)), new XAttribute(Svg + "height", Cm(h)),
                    new XElement(Draw + "image", new XAttribute(XLink + "href", path), new XAttribute(XLink + "type", "simple"), new XAttribute(XLink + "show", "embed"), new XAttribute(XLink + "actuate", "onLoad")),
                    new XElement(Svg + "desc", Safe(image.Alt))));
        }
    }

    /// <summary>Consecutive list items as nested ODF lists added to <paramref name="into"/>: a deeper item goes inside the item above it.</summary>
    private static XElement Lists(IReadOnlyList<OfficeListItem> items, XElement into)
    {
        var stack = new List<(XElement List, int Level, int ListId)>();
        XElement? root = null;
        foreach (var item in items)
        {
            while (stack.Count > 0 && (stack[^1].Level > item.Level || stack[^1].Level == item.Level && stack[^1].ListId != item.ListId)) stack.RemoveAt(stack.Count - 1);
            if (stack.Count == 0 || stack[^1].Level < item.Level)
            {
                var list = new XElement(Text + "list", new XAttribute(Text + "style-name", item.Ordered ? "LNumber" : "LBullet"));
                if (stack.Count == 0) { root = list; into.Add(list); }
                else
                {
                    var parentItem = stack[^1].List.Elements(Text + "list-item").LastOrDefault();
                    if (parentItem is null) { parentItem = new XElement(Text + "list-item"); stack[^1].List.Add(parentItem); }
                    parentItem.Add(list);
                }
                stack.Add((list, item.Level, item.ListId));
            }
            var check = item.Checked is { } done ? (done ? "☑ " : "☐ ") : null;
            stack[^1].List.Add(new XElement(Text + "list-item", new XElement(Text + "p", new XAttribute(Text + "style-name", "List_20_Contents"), check, Runs(item.Runs))));
        }
        // added to the body in place; the outermost list is returned
        return root!;
    }

    private static IEnumerable<object> Runs(IReadOnlyList<OfficeRun> runs)
    {
        foreach (var run in runs)
        {
            if (run.Break) { yield return new XElement(Text + "line-break"); continue; }
            object body = Spaces(run.Text);
            var style = run.Code ? "SCode" : run.Math ? "SMath" : StyleFor(run);
            XElement span = style is null ? new XElement(Text + "span", body) : new XElement(Text + "span", new XAttribute(Text + "style-name", style), body);
            if (run.Href is { } href)
                yield return new XElement(Text + "a", new XAttribute(XLink + "type", "simple"), new XAttribute(XLink + "href", href), span);
            else yield return span;
        }
    }

    private static string? StyleFor(OfficeRun run) =>
        (run.Bold, run.Italic, run.Strike) switch
        {
            (false, false, false) => null,
            _ => "T" + (run.Bold ? "b" : "") + (run.Italic ? "i" : "") + (run.Strike ? "s" : "")
        };

    /// <summary>ODF collapses runs of spaces like HTML; they are kept with text:s.</summary>
    private static IEnumerable<object> Spaces(string value)
    {
        value = PublicationOffice.XmlSafe(value);
        var sb = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\t') { if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); } yield return new XElement(Text + "tab"); continue; }
            if (value[i] == ' ' && i + 1 < value.Length && value[i + 1] == ' ')
            {
                sb.Append(' ');
                int n = 0;
                while (i + 1 < value.Length && value[i + 1] == ' ') { n++; i++; }
                yield return sb.ToString(); sb.Clear();
                yield return new XElement(Text + "s", new XAttribute(Text + "c", n));
                continue;
            }
            sb.Append(value[i]);
        }
        if (sb.Length > 0) yield return sb.ToString();
    }

    private static IEnumerable<object> CodeLines(string code)
    {
        var lines = code.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) yield return new XElement(Text + "line-break");
            foreach (var part in Spaces(lines[i])) yield return part;
        }
    }

    private static string PageBreakStyle(string? style) => style switch
    {
        null or "Text_20_body" => "PBody",
        _ when style.StartsWith("Heading_20_", StringComparison.Ordinal) => "PH" + style[^1],
        _ => "PBody"
    };

    private static IEnumerable<XElement> AutomaticStyles(OfficeDocument doc)
    {
        XElement TextStyle(string name, params XAttribute[] props) => new(Style + "style", new XAttribute(Style + "name", name), new XAttribute(Style + "family", "text"),
            new XElement(Style + "text-properties", props));
        foreach (var combo in new[] { "b", "i", "s", "bi", "bs", "is", "bis" })
            yield return TextStyle("T" + combo, combo.SelectMany(c => c switch
            {
                'b' => new[] { new XAttribute(Fo + "font-weight", "bold") },
                'i' => new[] { new XAttribute(Fo + "font-style", "italic") },
                _ => new[] { new XAttribute(Style + "text-line-through-style", "solid") }
            }).ToArray());
        yield return TextStyle("SCode", new XAttribute(Style + "font-name", "Liberation Mono"), new XAttribute(Fo + "font-family", "'Liberation Mono'"), new XAttribute(Fo + "background-color", "#f3f1ec"));
        yield return TextStyle("SMath", new XAttribute(Fo + "font-style", "italic"), new XAttribute(Fo + "font-family", "'Cambria Math'"));
        yield return new XElement(Style + "style", new XAttribute(Style + "name", "PBody"), new XAttribute(Style + "family", "paragraph"), new XAttribute(Style + "parent-style-name", "Text_20_body"),
            new XElement(Style + "paragraph-properties", new XAttribute(Fo + "break-before", "page")));
        for (int level = 1; level <= 6; level++)
            yield return new XElement(Style + "style", new XAttribute(Style + "name", "PH" + level), new XAttribute(Style + "family", "paragraph"), new XAttribute(Style + "parent-style-name", "Heading_20_" + level),
                new XElement(Style + "paragraph-properties", new XAttribute(Fo + "break-before", "page")));
        yield return new XElement(Style + "style", new XAttribute(Style + "name", "Tbl"), new XAttribute(Style + "family", "table"),
            new XElement(Style + "table-properties", new XAttribute(Style + "width", Cm(TextWidthCm)), new XAttribute(Table + "align", "margins"), new XAttribute(Fo + "margin-bottom", "0.3cm")));
        yield return new XElement(Style + "style", new XAttribute(Style + "name", "TblCol"), new XAttribute(Style + "family", "table-column"));
        yield return new XElement(Style + "style", new XAttribute(Style + "name", "TblCell"), new XAttribute(Style + "family", "table-cell"),
            new XElement(Style + "table-cell-properties", new XAttribute(Fo + "border", "0.5pt solid #999999"), new XAttribute(Fo + "padding", "0.1cm")));
        XElement ListStyle(string name, bool ordered) => new(Text + "list-style", new XAttribute(Style + "name", name),
            Enumerable.Range(1, 10).Select(level => ordered
                ? new XElement(Text + "list-level-style-number", new XAttribute(Text + "level", level), new XAttribute(Style + "num-suffix", "."),
                    new XAttribute(Style + "num-format", level % 3 == 2 ? "a" : level % 3 == 0 ? "i" : "1"), ListLevel(level))
                : new XElement(Text + "list-level-style-bullet", new XAttribute(Text + "level", level),
                    new XAttribute(Text + "bullet-char", level % 3 == 1 ? "•" : level % 3 == 2 ? "◦" : "▪"), ListLevel(level))));
        yield return ListStyle("LBullet", false);
        yield return ListStyle("LNumber", true);
    }

    private static XElement ListLevel(int level) => new(Style + "list-level-properties", new XAttribute(Text + "list-level-position-and-space-mode", "label-alignment"),
        new XElement(Style + "list-level-label-alignment", new XAttribute(Text + "label-followed-by", "listtab"),
            new XAttribute(Fo + "text-indent", "-0.6cm"), new XAttribute(Fo + "margin-left", Cm(0.6 * level + 0.4))));

    private static XElement CommonStyles()
    {
        XElement Para(string name, string display, string? parent = "Standard", XElement? paragraph = null, XElement? textProps = null, int? outline = null) =>
            new(Style + "style", new XAttribute(Style + "name", name), new XAttribute(Style + "display-name", display), new XAttribute(Style + "family", "paragraph"),
                parent is null ? null : new XAttribute(Style + "parent-style-name", parent),
                outline is { } o ? new XAttribute(Style + "default-outline-level", o) : null,
                new XAttribute(Style + "next-style-name", "Text_20_body"), paragraph, textProps);
        var styles = new XElement(Office + "styles",
            new XElement(Style + "default-style", new XAttribute(Style + "family", "paragraph"),
                new XElement(Style + "text-properties", new XAttribute(Style + "font-name", "Liberation Serif"), new XAttribute(Fo + "font-size", "11pt"), new XAttribute(Fo + "language", "pt"), new XAttribute(Fo + "country", "BR"))),
            Para("Standard", "Standard", null),
            Para("Text_20_body", "Text body", paragraph: new XElement(Style + "paragraph-properties", new XAttribute(Fo + "margin-bottom", "0.25cm"), new XAttribute(Fo + "line-height", "130%"))),
            Para("Title", "Title", textProps: new XElement(Style + "text-properties", new XAttribute(Fo + "font-size", "26pt"), new XAttribute(Fo + "color", "#6b4f2a")),
                paragraph: new XElement(Style + "paragraph-properties", new XAttribute(Fo + "margin-bottom", "0.5cm"))),
            Para("Quotations", "Quotations", "Text_20_body", new XElement(Style + "paragraph-properties", new XAttribute(Fo + "margin-left", "1cm"), new XAttribute(Fo + "border-left", "1.5pt solid #999999"), new XAttribute(Fo + "padding-left", "0.3cm")),
                new XElement(Style + "text-properties", new XAttribute(Fo + "font-style", "italic"), new XAttribute(Fo + "color", "#555555"))),
            Para("Preformatted_20_Text", "Preformatted Text", "Standard", new XElement(Style + "paragraph-properties", new XAttribute(Fo + "background-color", "#f3f1ec"), new XAttribute(Fo + "margin-bottom", "0.25cm"), new XAttribute(Fo + "padding", "0.15cm")),
                new XElement(Style + "text-properties", new XAttribute(Style + "font-name", "Liberation Mono"), new XAttribute(Fo + "font-family", "'Liberation Mono'"), new XAttribute(Fo + "font-size", "9.5pt"))),
            Para("Box", "Box", "Text_20_body", new XElement(Style + "paragraph-properties", new XAttribute(Fo + "background-color", "#f6f3ee"), new XAttribute(Fo + "border-left", "3pt solid #9c7b45"), new XAttribute(Fo + "padding-left", "0.3cm"), new XAttribute(Fo + "margin-left", "0.3cm"), new XAttribute(Fo + "margin-bottom", "0.1cm"))),
            Para("Box_20_Title", "Box Title", "Box", textProps: new XElement(Style + "text-properties", new XAttribute(Fo + "font-weight", "bold"), new XAttribute(Fo + "color", "#6b4f2a"))),
            Para("Formula", "Formula", "Text_20_body", new XElement(Style + "paragraph-properties", new XAttribute(Fo + "text-align", "center"))),
            Para("Figure", "Figure", "Text_20_body", new XElement(Style + "paragraph-properties", new XAttribute(Fo + "text-align", "center"))),
            Para("List_20_Contents", "List Contents", "Text_20_body", new XElement(Style + "paragraph-properties", new XAttribute(Fo + "margin-bottom", "0.1cm"))),
            Para("Table_20_Contents", "Table Contents"),
            Para("Table_20_Heading", "Table Heading", "Table_20_Contents", textProps: new XElement(Style + "text-properties", new XAttribute(Fo + "font-weight", "bold"))),
            Para("Horizontal_20_Line", "Horizontal Line", "Standard", new XElement(Style + "paragraph-properties", new XAttribute(Fo + "border-bottom", "0.5pt solid #999999"), new XAttribute(Fo + "margin-bottom", "0.3cm"))));
        double[] sizes = [18, 15, 13, 12, 11, 11];
        for (int level = 1; level <= 6; level++)
            styles.Add(Para("Heading_20_" + level, "Heading " + level, "Standard",
                new XElement(Style + "paragraph-properties", new XAttribute(Fo + "margin-top", "0.4cm"), new XAttribute(Fo + "margin-bottom", "0.15cm"), new XAttribute(Fo + "keep-with-next", "always")),
                new XElement(Style + "text-properties", new XAttribute(Fo + "font-size", sizes[level - 1].ToString(CultureInfo.InvariantCulture) + "pt"), new XAttribute(Fo + "font-weight", "bold"), new XAttribute(Fo + "color", "#6b4f2a")), level));
        return styles;
    }

    private static IEnumerable<XElement> PageLayout()
    {
        yield return new XElement(Office + "automatic-styles", new XElement(Style + "page-layout", new XAttribute(Style + "name", "A4"),
            new XElement(Style + "page-layout-properties", new XAttribute(Fo + "page-width", "21cm"), new XAttribute(Fo + "page-height", "29.7cm"),
                new XAttribute(Fo + "margin-top", "2cm"), new XAttribute(Fo + "margin-bottom", "2cm"), new XAttribute(Fo + "margin-left", "2cm"), new XAttribute(Fo + "margin-right", "2cm"))));
        yield return new XElement(Office + "master-styles", new XElement(Style + "master-page", new XAttribute(Style + "name", "Standard"), new XAttribute(Style + "page-layout-name", "A4")));
    }

    private static IEnumerable<XAttribute> Namespaces() =>
    [
        new(XNamespace.Xmlns + "office", Office), new(XNamespace.Xmlns + "style", Style), new(XNamespace.Xmlns + "text", Text),
        new(XNamespace.Xmlns + "table", Table), new(XNamespace.Xmlns + "draw", Draw), new(XNamespace.Xmlns + "fo", Fo),
        new(XNamespace.Xmlns + "xlink", XLink), new(XNamespace.Xmlns + "svg", Svg), new(XNamespace.Xmlns + "meta", Meta), new(XNamespace.Xmlns + "dc", DC),
    ];

    private static string Cm(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "cm";
    private static string Safe(string value) => PublicationOffice.XmlSafe(value);

    private static XElement FileEntry(string path, string mime) =>
        new(Manifest + "file-entry", new XAttribute(Manifest + "full-path", path), new XAttribute(Manifest + "media-type", mime),
            path == "/" ? new XAttribute(Manifest + "version", "1.3") : null);

    private static void Xml(ZipArchive zip, string name, XElement root) =>
        Bytes(zip, name, Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + root.ToString(SaveOptions.DisableFormatting)), CompressionLevel.Optimal);

    private static void Bytes(ZipArchive zip, string name, byte[] bytes, CompressionLevel level)
    { using var target = zip.CreateEntry(name, level).Open(); target.Write(bytes); }
}
