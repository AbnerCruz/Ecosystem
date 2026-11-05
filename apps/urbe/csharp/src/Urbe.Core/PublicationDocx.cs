using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>
/// CST-166: a Word document (.docx, Office Open XML) from the publication's live notes, by semantics: real heading
/// styles (navigable, in the TOC), real lists and numbering, tables, embedded images, quotes and code as styles. It
/// opens and edits in Word, LibreOffice and Google Docs. What it cannot keep goes to <see cref="PublicationExport.Limitations"/>.
/// </summary>
public static class PublicationDocx
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace CT = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace CP = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace DC = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace DCTerms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>A4 text width with 2 cm margins, in EMU (an image never runs past the margin).</summary>
    private const long TextWidthEmu = 6120000;
    private const long EmuPerPixel = 9525;

    public static async Task<PublicationExport> ExportAsync(PublicationProject project, PublicationSourceCatalog catalog,
        Func<string, Task<byte[]?>>? readAsset = null, CancellationToken cancellationToken = default) =>
        Write(await PublicationOffice.ReadAsync(project, catalog, readAsset, cancellationToken));

    public static PublicationExport Write(OfficeDocument doc)
    {
        var rels = new List<XElement>
        {
            Relationship("rIdStyles", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles", "styles.xml"),
            Relationship("rIdNumbering", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering", "numbering.xml"),
            Relationship("rIdSettings", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/settings", "settings.xml"),
        };
        var media = new List<(string Path, byte[] Bytes)>();
        var lists = new Dictionary<int, (int NumId, bool Ordered)>();
        var body = new XElement(W + "body");
        int drawing = 0;
        body.Add(Para("Title", new XElement(W + "r", Text(doc.Title))));
        var book = doc.Layout == "book";
        if (book)
        {
            body.Add(Para("TOCHeading", new XElement(W + "r", Text("Sumário"))));
            body.Add(new XElement(W + "p",
                new XElement(W + "r", new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "begin"), new XAttribute(W + "dirty", "true"))),
                new XElement(W + "r", new XElement(W + "instrText", new XAttribute(XNamespace.Xml + "space", "preserve"), " TOC \\o \"1-3\" \\h \\z \\u ")),
                new XElement(W + "r", new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "separate"))),
                new XElement(W + "r", Text("Atualize o sumário (F9 no Word; Ferramentas → Atualizar no LibreOffice).")),
                new XElement(W + "r", new XElement(W + "fldChar", new XAttribute(W + "fldCharType", "end")))));
        }
        for (int c = 0; c < doc.Chapters.Count; c++)
        {
            var chapter = doc.Chapters[c];
            if (c > 0 && book) body.Add(new XElement(W + "p", new XElement(W + "r", new XElement(W + "br", new XAttribute(W + "type", "page")))));
            foreach (var block in chapter.Blocks) body.Add(Block(block));
        }
        body.Add(new XElement(W + "sectPr",
            new XElement(W + "pgSz", new XAttribute(W + "w", 11906), new XAttribute(W + "h", 16838)),
            new XElement(W + "pgMar", new XAttribute(W + "top", 1134), new XAttribute(W + "right", 1134), new XAttribute(W + "bottom", 1134),
                new XAttribute(W + "left", 1134), new XAttribute(W + "header", 709), new XAttribute(W + "footer", 709), new XAttribute(W + "gutter", 0))));
        var document = new XDocument(new XElement(W + "document",
            new XAttribute(XNamespace.Xmlns + "w", W), new XAttribute(XNamespace.Xmlns + "r", R), new XAttribute(XNamespace.Xmlns + "wp", WP),
            new XAttribute(XNamespace.Xmlns + "a", A), new XAttribute(XNamespace.Xmlns + "pic", Pic), body));

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var types = new XElement(CT + "Types",
                new XElement(CT + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(CT + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(CT + "Default", new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")),
                new XElement(CT + "Default", new XAttribute("Extension", "jpg"), new XAttribute("ContentType", "image/jpeg")),
                new XElement(CT + "Default", new XAttribute("Extension", "gif"), new XAttribute("ContentType", "image/gif")),
                Override("/word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"),
                Override("/word/styles.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"),
                Override("/word/numbering.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"),
                Override("/word/settings.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"),
                Override("/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml"));
            Xml(zip, "[Content_Types].xml", types);
            Xml(zip, "_rels/.rels", new XElement(Rel + "Relationships",
                Relationship("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "word/document.xml"),
                Relationship("rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml")));
            Xml(zip, "docProps/core.xml", new XElement(CP + "coreProperties",
                new XAttribute(XNamespace.Xmlns + "cp", CP), new XAttribute(XNamespace.Xmlns + "dc", DC), new XAttribute(XNamespace.Xmlns + "dcterms", DCTerms), new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XElement(DC + "title", PublicationOffice.XmlSafe(doc.Title)), new XElement(DC + "language", "pt-BR"), new XElement(DC + "creator", "Urbe")));
            Xml(zip, "word/document.xml", document.Root!);
            Xml(zip, "word/styles.xml", Styles());
            Xml(zip, "word/numbering.xml", Numbering(lists));
            Xml(zip, "word/settings.xml", new XElement(W + "settings", new XAttribute(XNamespace.Xmlns + "w", W),
                book ? new XElement(W + "updateFields", new XAttribute(W + "val", "true")) : null,
                new XElement(W + "defaultTabStop", new XAttribute(W + "val", 709)), new XElement(W + "compat",
                    new XElement(W + "compatSetting", new XAttribute(W + "name", "compatibilityMode"), new XAttribute(W + "uri", "http://schemas.microsoft.com/office/word"), new XAttribute(W + "val", 15)))));
            Xml(zip, "word/_rels/document.xml.rels", new XElement(Rel + "Relationships", rels));
            foreach (var (path, bytes) in media) Bytes(zip, "word/" + path, bytes);
        }
        return new PublicationExport(stream.ToArray(), doc.Limitations);

        IEnumerable<XElement> Block(OfficeBlock block)
        {
            switch (block)
            {
                case OfficeHeading h: yield return Para("Heading" + Math.Clamp(h.Level, 1, 6), Runs(h.Runs)); break;
                case OfficeParagraph p:
                    yield return Para(p.Style switch { "quote" => "Quote", "math" => "Formula", _ => null }, Runs(p.Runs));
                    break;
                case OfficeListItem li:
                    if (!lists.TryGetValue(li.ListId, out var list)) lists[li.ListId] = list = (lists.Count + 1, li.Ordered);
                    var props = new XElement(W + "pPr", new XElement(W + "pStyle", new XAttribute(W + "val", "ListParagraph")),
                        new XElement(W + "numPr", new XElement(W + "ilvl", new XAttribute(W + "val", li.Level)), new XElement(W + "numId", new XAttribute(W + "val", list.NumId))));
                    var runs = li.Checked is { } done ? new[] { Run(new OfficeRun(done ? "☑ " : "☐ ")) }.Concat(Runs(li.Runs)) : Runs(li.Runs);
                    yield return new XElement(W + "p", props, runs);
                    break;
                case OfficeCode code:
                    var lines = code.Text.Replace("\r", "").Split('\n');
                    var content = new List<XElement>();
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (i > 0) content.Add(new XElement(W + "r", new XElement(W + "br")));
                        content.Add(new XElement(W + "r", Text(lines[i])));
                    }
                    yield return Para("Code", content);
                    break;
                case OfficeTable table: yield return Table(table); yield return new XElement(W + "p"); break;
                case OfficeImage image: yield return Image(image); break;
                case OfficeRule:
                    yield return new XElement(W + "p", new XElement(W + "pPr", new XElement(W + "pBdr",
                        new XElement(W + "bottom", new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 6), new XAttribute(W + "space", 1), new XAttribute(W + "color", "999999")))));
                    break;
            }
        }

        XElement Table(OfficeTable table)
        {
            var columns = table.Rows.Max(r => r.Count);
            var width = (int)(TextWidthEmu / 635 / columns); // twips per column
            var t = new XElement(W + "tbl", new XElement(W + "tblPr", new XElement(W + "tblStyle", new XAttribute(W + "val", "TableGrid")),
                    new XElement(W + "tblW", new XAttribute(W + "w", 5000), new XAttribute(W + "type", "pct")),
                    new XElement(W + "tblLook", new XAttribute(W + "firstRow", table.HeaderRow ? 1 : 0), new XAttribute(W + "val", table.HeaderRow ? "0020" : "0000"))),
                new XElement(W + "tblGrid", Enumerable.Range(0, columns).Select(_ => new XElement(W + "gridCol", new XAttribute(W + "w", width)))));
            for (int r = 0; r < table.Rows.Count; r++)
            {
                var header = r == 0 && table.HeaderRow;
                var tr = new XElement(W + "tr", header ? new XElement(W + "trPr", new XElement(W + "tblHeader")) : null);
                foreach (var cell in table.Rows[r])
                    tr.Add(new XElement(W + "tc", new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", width), new XAttribute(W + "type", "dxa"))),
                        new XElement(W + "p", Runs(header ? cell.Select(x => x with { Bold = true }).ToList() : cell))));
                t.Add(tr);
            }
            return t;
        }

        XElement Image(OfficeImage image)
        {
            var id = "rIdImg" + (++drawing);
            var path = "media/image" + drawing + image.Extension;
            media.Add((path, image.Bytes));
            rels.Add(Relationship(id, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", path));
            long cx = image.Width * EmuPerPixel, cy = image.Height * EmuPerPixel;
            if (cx > TextWidthEmu) { cy = cy * TextWidthEmu / cx; cx = TextWidthEmu; }
            var alt = PublicationOffice.XmlSafe(image.Alt);
            return new XElement(W + "p", new XElement(W + "pPr", new XElement(W + "jc", new XAttribute(W + "val", "center"))),
                new XElement(W + "r", new XElement(W + "drawing", new XElement(WP + "inline",
                    new XElement(WP + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
                    new XElement(WP + "docPr", new XAttribute("id", drawing), new XAttribute("name", "Imagem " + drawing), new XAttribute("descr", alt)),
                    new XElement(A + "graphic", new XElement(A + "graphicData", new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/picture"),
                        new XElement(Pic + "pic",
                            new XElement(Pic + "nvPicPr", new XElement(Pic + "cNvPr", new XAttribute("id", drawing), new XAttribute("name", "Imagem " + drawing), new XAttribute("descr", alt)), new XElement(Pic + "cNvPicPr")),
                            new XElement(Pic + "blipFill", new XElement(A + "blip", new XAttribute(R + "embed", id)), new XElement(A + "stretch", new XElement(A + "fillRect"))),
                            new XElement(Pic + "spPr", new XElement(A + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(A + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy))),
                                new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst"))))))))));
        }

        IEnumerable<XElement> Runs(IReadOnlyList<OfficeRun> runs)
        {
            int i = 0;
            while (i < runs.Count)
            {
                if (runs[i].Href is { } href)
                {
                    var id = "rIdLink" + rels.Count;
                    rels.Add(new XElement(Rel + "Relationship", new XAttribute("Id", id),
                        new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink"),
                        new XAttribute("Target", href), new XAttribute("TargetMode", "External")));
                    var link = new XElement(W + "hyperlink", new XAttribute(R + "id", id));
                    while (i < runs.Count && runs[i].Href == href) link.Add(Run(runs[i++], "Hyperlink"));
                    yield return link;
                }
                else yield return Run(runs[i++]);
            }
        }
    }

    private static XElement Run(OfficeRun run) => Run(run, null);

    private static XElement Run(OfficeRun run, string? style)
    {
        if (run.Break) return new XElement(W + "r", new XElement(W + "br"));
        var props = new XElement(W + "rPr");
        if (style is not null) props.Add(new XElement(W + "rStyle", new XAttribute(W + "val", style)));
        else if (run.Code) props.Add(new XElement(W + "rStyle", new XAttribute(W + "val", "InlineCode")));
        else if (run.Math) props.Add(new XElement(W + "rStyle", new XAttribute(W + "val", "MathTex")));
        if (run.Bold) props.Add(new XElement(W + "b"));
        if (run.Italic) props.Add(new XElement(W + "i"));
        if (run.Strike) props.Add(new XElement(W + "strike"));
        return new XElement(W + "r", props.HasElements ? props : null, Text(run.Text));
    }

    private static XElement Text(string text) =>
        new(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), PublicationOffice.XmlSafe(text));

    private static XElement Para(string? style, params object?[] content) =>
        new(W + "p", style is null ? null : new XElement(W + "pPr", new XElement(W + "pStyle", new XAttribute(W + "val", style))), content);

    private static XElement Styles()
    {
        XElement Style(string id, string name, string type = "paragraph", string? basedOn = "Normal", object? ppr = null, object? rpr = null, bool heading = false) =>
            new(W + "style", new XAttribute(W + "type", type), new XAttribute(W + "styleId", id), new XElement(W + "name", new XAttribute(W + "val", name)),
                basedOn is null ? null : new XElement(W + "basedOn", new XAttribute(W + "val", basedOn)),
                type == "paragraph" ? new XElement(W + "next", new XAttribute(W + "val", "Normal")) : null,
                new XElement(W + "qFormat"), ppr, rpr);
        var styles = new XElement(W + "styles", new XAttribute(XNamespace.Xmlns + "w", W),
            new XElement(W + "docDefaults",
                new XElement(W + "rPrDefault", new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Calibri"), new XAttribute(W + "hAnsi", "Calibri"), new XAttribute(W + "cs", "Calibri")),
                    new XElement(W + "sz", new XAttribute(W + "val", 22)), new XElement(W + "lang", new XAttribute(W + "val", "pt-BR")))),
                new XElement(W + "pPrDefault", new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", 160), new XAttribute(W + "line", 276), new XAttribute(W + "lineRule", "auto"))))),
            new XElement(W + "style", new XAttribute(W + "type", "paragraph"), new XAttribute(W + "default", 1), new XAttribute(W + "styleId", "Normal"), new XElement(W + "name", new XAttribute(W + "val", "Normal")), new XElement(W + "qFormat")),
            Style("Title", "Title", ppr: new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", 240))), rpr: new XElement(W + "rPr", new XElement(W + "sz", new XAttribute(W + "val", 52)), new XElement(W + "color", new XAttribute(W + "val", "6B4F2A")))),
            Style("TOCHeading", "TOC Heading", rpr: new XElement(W + "rPr", new XElement(W + "b"), new XElement(W + "sz", new XAttribute(W + "val", 28)))));
        int[] sizes = [36, 30, 26, 24, 22, 22];
        for (int level = 1; level <= 6; level++)
            styles.Add(Style("Heading" + level, "heading " + level,
                ppr: new XElement(W + "pPr", new XElement(W + "keepNext"), new XElement(W + "spacing", new XAttribute(W + "before", 240), new XAttribute(W + "after", 80)), new XElement(W + "outlineLvl", new XAttribute(W + "val", level - 1))),
                rpr: new XElement(W + "rPr", new XElement(W + "b"), new XElement(W + "color", new XAttribute(W + "val", "6B4F2A")), new XElement(W + "sz", new XAttribute(W + "val", sizes[level - 1])))));
        styles.Add(Style("Quote", "Quote", ppr: new XElement(W + "pPr", new XElement(W + "pBdr", new XElement(W + "left", new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 18), new XAttribute(W + "space", 8), new XAttribute(W + "color", "999999"))),
            new XElement(W + "ind", new XAttribute(W + "left", 567))), rpr: new XElement(W + "rPr", new XElement(W + "i"), new XElement(W + "color", new XAttribute(W + "val", "555555")))));
        styles.Add(Style("Code", "Code", ppr: new XElement(W + "pPr", new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "color", "auto"), new XAttribute(W + "fill", "F3F1EC")),
            new XElement(W + "spacing", new XAttribute(W + "after", 160), new XAttribute(W + "line", 240), new XAttribute(W + "lineRule", "auto"))),
            rpr: new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Consolas"), new XAttribute(W + "hAnsi", "Consolas"), new XAttribute(W + "cs", "Consolas")), new XElement(W + "sz", new XAttribute(W + "val", 19)))));
        styles.Add(Style("Formula", "Formula", ppr: new XElement(W + "pPr", new XElement(W + "jc", new XAttribute(W + "val", "center")))));
        styles.Add(Style("ListParagraph", "List Paragraph", ppr: new XElement(W + "pPr", new XElement(W + "spacing", new XAttribute(W + "after", 60)), new XElement(W + "contextualSpacing"))));
        styles.Add(Style("InlineCode", "Inline Code", "character", null, rpr: new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Consolas"), new XAttribute(W + "hAnsi", "Consolas")), new XElement(W + "shd", new XAttribute(W + "val", "clear"), new XAttribute(W + "color", "auto"), new XAttribute(W + "fill", "F3F1EC")))));
        styles.Add(Style("MathTex", "Math TeX", "character", null, rpr: new XElement(W + "rPr", new XElement(W + "rFonts", new XAttribute(W + "ascii", "Cambria Math"), new XAttribute(W + "hAnsi", "Cambria Math")), new XElement(W + "i"))));
        styles.Add(Style("Hyperlink", "Hyperlink", "character", null, rpr: new XElement(W + "rPr", new XElement(W + "color", new XAttribute(W + "val", "0563C1")), new XElement(W + "u", new XAttribute(W + "val", "single")))));
        styles.Add(new XElement(W + "style", new XAttribute(W + "type", "table"), new XAttribute(W + "styleId", "TableGrid"), new XElement(W + "name", new XAttribute(W + "val", "Table Grid")),
            new XElement(W + "tblPr", new XElement(W + "tblBorders", new[] { "top", "left", "bottom", "right", "insideH", "insideV" }.Select(side =>
                new XElement(W + side, new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4), new XAttribute(W + "space", 0), new XAttribute(W + "color", "999999")))),
                new XElement(W + "tblCellMar", new XElement(W + "left", new XAttribute(W + "w", 108), new XAttribute(W + "type", "dxa")), new XElement(W + "right", new XAttribute(W + "w", 108), new XAttribute(W + "type", "dxa"))))));
        return styles;
    }

    private static XElement Numbering(Dictionary<int, (int NumId, bool Ordered)> lists)
    {
        XElement Abstract(int id, bool ordered) => new(W + "abstractNum", new XAttribute(W + "abstractNumId", id),
            new XElement(W + "multiLevelType", new XAttribute(W + "val", "hybridMultilevel")),
            Enumerable.Range(0, 9).Select(level => new XElement(W + "lvl", new XAttribute(W + "ilvl", level),
                new XElement(W + "start", new XAttribute(W + "val", 1)),
                new XElement(W + "numFmt", new XAttribute(W + "val", ordered ? (level % 3 == 1 ? "lowerLetter" : level % 3 == 2 ? "lowerRoman" : "decimal") : "bullet")),
                new XElement(W + "lvlText", new XAttribute(W + "val", ordered ? "%" + (level + 1) + "." : (level % 3 == 0 ? "•" : level % 3 == 1 ? "◦" : "▪"))),
                new XElement(W + "lvlJc", new XAttribute(W + "val", "left")),
                new XElement(W + "pPr", new XElement(W + "ind", new XAttribute(W + "left", 720 + level * 360), new XAttribute(W + "hanging", 360))))));
        var numbering = new XElement(W + "numbering", new XAttribute(XNamespace.Xmlns + "w", W), Abstract(0, false), Abstract(1, true));
        foreach (var (numId, ordered) in lists.Values.OrderBy(l => l.NumId))
            numbering.Add(new XElement(W + "num", new XAttribute(W + "numId", numId), new XElement(W + "abstractNumId", new XAttribute(W + "val", ordered ? 1 : 0)),
                // each ordered list counts from 1 again
                ordered ? new XElement(W + "lvlOverride", new XAttribute(W + "ilvl", 0), new XElement(W + "startOverride", new XAttribute(W + "val", 1))) : null));
        return numbering;
    }

    private static XElement Relationship(string id, string type, string target) =>
        new(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", type), new XAttribute("Target", target));

    private static XElement Override(string part, string type) =>
        new(CT + "Override", new XAttribute("PartName", part), new XAttribute("ContentType", type));

    private static void Xml(ZipArchive zip, string name, XElement root) =>
        Bytes(zip, name, Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" + root.ToString(SaveOptions.DisableFormatting)));

    private static void Bytes(ZipArchive zip, string name, byte[] bytes)
    { using var target = zip.CreateEntry(name, CompressionLevel.Optimal).Open(); target.Write(bytes); }
}
