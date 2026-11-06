using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>A slide as the deck is split: a title and what goes under it.</summary>
public sealed record PptxSlide(string Title, IReadOnlyList<OfficeBlock> Text, OfficeImage? Image, OfficeTable? Table, bool Continued)
{
    /// <summary>The speaker notes (a <c>:::notas</c> container of the note, under this slide).</summary>
    public IReadOnlyList<OfficeBlock> Notes { get; init; } = [];
}

/// <summary>
/// CST-168: an editable PowerPoint deck (.pptx) from the publication's live notes. Editability first: titles, text,
/// bullets, numbered lists, tables and pictures are native PowerPoint objects (no slide is a picture of the HTML).
/// A note starts a slide, a # or ## heading starts a slide and is its title, and --- starts a new slide (the Marp
/// convention). Text that does not fit continues on a next slide marked "(cont.)" instead of being cut or shrunk
/// out of sight. What a slide cannot keep is reported.
/// </summary>
public static class PublicationPptx
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace CT = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace CP = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace DC = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Asvg = "http://schemas.microsoft.com/office/drawing/2016/SVG/main";

    private const long Inch = 914400;
    private const long SlideW = 12192000, SlideH = 6858000; // 16:9
    private const long Margin = Inch / 2;
    private const long BodyTop = (long)(1.45 * Inch), BodyH = SlideH - BodyTop - (long)(0.4 * Inch);
    private const int BodyPt = 20, CodePt = 14, TitlePt = 32;
    /// <summary>Lines of body text a full-width slide holds at <see cref="BodyPt"/> (1.2 line height).</summary>
    private static readonly int Capacity = (int)(BodyH / (double)Inch * 72 / (BodyPt * 1.5));

    public static async Task<PublicationExport> ExportAsync(PublicationProject project, PublicationSourceCatalog catalog,
        Func<string, Task<byte[]?>>? readAsset = null, CancellationToken cancellationToken = default, Func<string, int, int, OfficeRaster?>? rasterize = null,
        Func<string, string?>? data = null) =>
        Write(await PublicationOffice.ReadAsync(project, catalog, readAsset, cancellationToken, rasterize, data));

    /// <summary>The slides the deck will have (also what the studio previews and the tests read).</summary>
    public static IReadOnlyList<PptxSlide> Slides(OfficeDocument doc, ICollection<string>? limitations = null)
    {
        var slides = new List<PptxSlide>();
        foreach (var chapter in doc.Chapters)
        {
            string title = chapter.Title;
            bool titled = false, continued = false;
            var text = new List<OfficeBlock>();
            OfficeImage? image = null;
            OfficeTable? table = null;
            var notes = new List<OfficeBlock>();
            int lines = 0;
            void Push(bool keepTitle)
            {
                if (text.Count > 0 || image is not null || table is not null || titled)
                    slides.Add(new PptxSlide(title, text.ToArray(), image, table, continued) { Notes = notes.ToArray() });
                else if (notes.Count > 0 && slides.Count > 0) slides[^1] = slides[^1] with { Notes = [.. slides[^1].Notes, .. notes] };
                text.Clear(); image = null; table = null; lines = 0; notes.Clear();
                continued = keepTitle;
                if (!keepTitle) { title = ""; titled = false; }
            }
            foreach (var block in chapter.Blocks.SelectMany(b => b is OfficeBox box && !NoteContainers.IsSpeakerNotes(box.Type) ? PublicationOffice.Flatten([b]) : [b]))
            {
                switch (block)
                {
                    case OfficeHeading h when h.Level <= 2:
                        Push(false);
                        title = Plain(h.Runs); titled = true; continued = false;
                        break;
                    case OfficeRule:
                        Push(false);
                        break;
                    case OfficeBox speaker:
                        notes.AddRange(PublicationOffice.Flatten(speaker.Blocks));
                        break;
                    case OfficeImage img:
                        if (image is not null || table is not null || lines > Capacity / 2) Push(true);
                        image = img;
                        break;
                    case OfficeTable t:
                        if (table is not null || image is not null || lines + t.Rows.Count + 1 > Capacity) Push(true);
                        table = t;
                        lines += t.Rows.Count + 1;
                        break;
                    default:
                        var need = Lines(block, image is null ? 12.3 : 6.0);
                        if (lines > 0 && lines + need > Capacity) { Push(true); limitations?.Add("Texto que não cabe num slide continua no slide seguinte, marcado “(cont.)”."); }
                        text.Add(block);
                        lines += need;
                        break;
                }
            }
            Push(false);
        }
        return slides;
    }

    public static PublicationExport Write(OfficeDocument doc)
    {
        var limitations = new SortedSet<string>(doc.Limitations, StringComparer.Ordinal);
        var slides = Slides(doc, limitations);
        if (slides.Count == 0) throw new InvalidDataException("As notas da publicação não têm conteúdo para slides.");
        if (doc.Chapters.SelectMany(c => c.Blocks).OfType<OfficeHeading>().Any(h => h.Level > 2))
            limitations.Add("Títulos ### e menores ficam como texto em negrito dentro do slide.");

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var types = new XElement(CT + "Types",
                Default("rels", "application/vnd.openxmlformats-package.relationships+xml"), Default("xml", "application/xml"),
                Default("png", "image/png"), Default("jpg", "image/jpeg"), Default("gif", "image/gif"), Default("svg", "image/svg+xml"),
                Override("/ppt/presentation.xml", "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"),
                Override("/ppt/slideMasters/slideMaster1.xml", "application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml"),
                Override("/ppt/slideLayouts/slideLayout1.xml", "application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml"),
                Override("/ppt/theme/theme1.xml", "application/vnd.openxmlformats-officedocument.theme+xml"),
                Override("/ppt/theme/theme2.xml", "application/vnd.openxmlformats-officedocument.theme+xml"),
                Override("/ppt/notesMasters/notesMaster1.xml", "application/vnd.openxmlformats-officedocument.presentationml.notesMaster+xml"),
                Override("/docProps/core.xml", "application/vnd.openxmlformats-package.core-properties+xml"));
            for (int i = 1; i <= slides.Count; i++)
            {
                types.Add(Override($"/ppt/slides/slide{i}.xml", "application/vnd.openxmlformats-officedocument.presentationml.slide+xml"));
                if (slides[i - 1].Notes.Count > 0)
                    types.Add(Override($"/ppt/notesSlides/notesSlide{i}.xml", "application/vnd.openxmlformats-officedocument.presentationml.notesSlide+xml"));
            }
            Xml(zip, "[Content_Types].xml", types);
            Xml(zip, "_rels/.rels", Rels(
                ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "ppt/presentation.xml", false),
                ("rId2", "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties", "docProps/core.xml", false)));
            Xml(zip, "docProps/core.xml", new XElement(CP + "coreProperties", new XAttribute(XNamespace.Xmlns + "cp", CP), new XAttribute(XNamespace.Xmlns + "dc", DC),
                new XElement(DC + "title", PublicationOffice.XmlSafe(doc.Title)), new XElement(DC + "language", "pt-BR"), new XElement(DC + "creator", "Urbe")));

            var presentationRels = new List<(string, string, string, bool)>
            {
                ("rIdMaster", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster", "slideMasters/slideMaster1.xml", false),
                ("rIdTheme", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme", "theme/theme1.xml", false),
                ("rIdNotesMaster", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesMaster", "notesMasters/notesMaster1.xml", false),
            };
            var slideIds = new XElement(P + "sldIdLst");
            for (int i = 1; i <= slides.Count; i++)
            {
                presentationRels.Add(("rIdSlide" + i, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide", $"slides/slide{i}.xml", false));
                slideIds.Add(new XElement(P + "sldId", new XAttribute("id", 255 + i), new XAttribute(R + "id", "rIdSlide" + i)));
            }
            Xml(zip, "ppt/presentation.xml", new XElement(P + "presentation", Ns(),
                new XElement(P + "sldMasterIdLst", new XElement(P + "sldMasterId", new XAttribute("id", 2147483648L), new XAttribute(R + "id", "rIdMaster"))),
                new XElement(P + "notesMasterIdLst", new XElement(P + "notesMasterId", new XAttribute(R + "id", "rIdNotesMaster"))),
                slideIds,
                new XElement(P + "sldSz", new XAttribute("cx", SlideW), new XAttribute("cy", SlideH)),
                new XElement(P + "notesSz", new XAttribute("cx", 6858000), new XAttribute("cy", 9144000))));
            Xml(zip, "ppt/_rels/presentation.xml.rels", Rels(presentationRels.ToArray()));
            Xml(zip, "ppt/theme/theme1.xml", Theme());
            Xml(zip, "ppt/theme/theme2.xml", Theme());
            Xml(zip, "ppt/notesMasters/notesMaster1.xml", new XElement(P + "notesMaster", Ns(),
                new XElement(P + "cSld", EmptyTree(NotesPlaceholder(2, "sldImg", null, null), NotesPlaceholder(3, "body", 1, null))),
                new XElement(P + "clrMap", new XAttribute("bg1", "lt1"), new XAttribute("tx1", "dk1"), new XAttribute("bg2", "lt2"), new XAttribute("tx2", "dk2"),
                    new XAttribute("accent1", "accent1"), new XAttribute("accent2", "accent2"), new XAttribute("accent3", "accent3"), new XAttribute("accent4", "accent4"),
                    new XAttribute("accent5", "accent5"), new XAttribute("accent6", "accent6"), new XAttribute("hlink", "hlink"), new XAttribute("folHlink", "folHlink"))));
            Xml(zip, "ppt/notesMasters/_rels/notesMaster1.xml.rels", Rels(
                ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme", "../theme/theme2.xml", false)));
            Xml(zip, "ppt/slideMasters/slideMaster1.xml", Master());
            Xml(zip, "ppt/slideMasters/_rels/slideMaster1.xml.rels", Rels(
                ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout", "../slideLayouts/slideLayout1.xml", false),
                ("rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme", "../theme/theme1.xml", false)));
            Xml(zip, "ppt/slideLayouts/slideLayout1.xml", new XElement(P + "sldLayout", Ns(), new XAttribute("preserve", "1"),
                new XElement(P + "cSld", new XAttribute("name", "Título e conteúdo"), EmptyTree(TitlePlaceholder(2, "Título", null))),
                new XElement(P + "clrMapOvr", new XElement(A + "masterClrMapping"))));
            Xml(zip, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", Rels(
                ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster", "../slideMasters/slideMaster1.xml", false)));

            int media = 0;
            for (int i = 0; i < slides.Count; i++)
            {
                var rels = new List<(string, string, string, bool)>
                { ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout", "../slideLayouts/slideLayout1.xml", false) };
                var (xml, pictures) = SlideXml(slides[i], rels, ref media);
                if (slides[i].Notes.Count > 0)
                {
                    rels.Add(("rIdNotes", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesSlide", $"../notesSlides/notesSlide{i + 1}.xml", false));
                    var notesRels = new List<(string, string, string, bool)>
                    {
                        ("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/notesMaster", "../notesMasters/notesMaster1.xml", false),
                        ("rId2", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide", $"../slides/slide{i + 1}.xml", false),
                    };
                    var body = new XElement(P + "txBody", new XElement(A + "bodyPr"), new XElement(A + "lstStyle"));
                    foreach (var block in slides[i].Notes) foreach (var p in Paragraphs(block, notesRels, 12)) body.Add(p);
                    Xml(zip, $"ppt/notesSlides/notesSlide{i + 1}.xml", new XElement(P + "notes", Ns(),
                        new XElement(P + "cSld", EmptyTree(NotesPlaceholder(2, "sldImg", null, null), NotesPlaceholder(3, "body", 1, body))),
                        new XElement(P + "clrMapOvr", new XElement(A + "masterClrMapping"))));
                    Xml(zip, $"ppt/notesSlides/_rels/notesSlide{i + 1}.xml.rels", Rels(notesRels.ToArray()));
                }
                Xml(zip, $"ppt/slides/slide{i + 1}.xml", xml);
                Xml(zip, $"ppt/slides/_rels/slide{i + 1}.xml.rels", Rels(rels.ToArray()));
                foreach (var (path, bytes) in pictures) Bytes(zip, "ppt/" + path, bytes);
            }
        }
        return new PublicationExport(stream.ToArray(), limitations.ToArray());
    }

    private static (XElement Slide, List<(string Path, byte[] Bytes)> Pictures) SlideXml(PptxSlide slide, List<(string, string, string, bool)> rels, ref int media)
    {
        var pictures = new List<(string, byte[])>();
        var shapes = new List<XElement>();
        int id = 2;
        var title = slide.Title + (slide.Continued && slide.Title.Length > 0 ? " (cont.)" : "");
        if (title.Length > 0) shapes.Add(TitlePlaceholder(id++, title, title));
        long textW = slide.Image is null ? SlideW - 2 * Margin : (long)(6.1 * Inch);
        long y = title.Length > 0 ? BodyTop : Margin;
        long available = SlideH - y - (long)(0.4 * Inch);
        if (slide.Text.Count > 0)
        {
            int lines = slide.Text.Sum(b => Lines(b, textW / (double)Inch));
            long h = slide.Table is null ? available : Math.Min(available, (long)(lines * BodyPt * 1.5 / 72 * Inch) + Inch / 5);
            var body = new XElement(P + "txBody", new XElement(A + "bodyPr", new XAttribute("wrap", "square"), new XAttribute("lIns", 0), new XAttribute("rIns", 0),
                new XElement(A + "normAutofit")), new XElement(A + "lstStyle"));
            foreach (var block in slide.Text) foreach (var p in Paragraphs(block, rels)) body.Add(p);
            shapes.Add(new XElement(P + "sp",
                new XElement(P + "nvSpPr", new XElement(P + "cNvPr", new XAttribute("id", id), new XAttribute("name", "Texto " + id++)), new XElement(P + "cNvSpPr", new XAttribute("txBox", 1)), new XElement(P + "nvPr")),
                new XElement(P + "spPr", Xfrm(Margin, y, textW, h), Geometry()), body));
            if (slide.Table is not null) y += h;
        }
        if (slide.Table is { } table)
        {
            var columns = table.Rows.Max(r => r.Count);
            long width = SlideW - 2 * Margin, colW = width / columns, rowH = (long)(0.45 * Inch);
            var tbl = new XElement(A + "tbl", new XElement(A + "tblPr", new XAttribute("firstRow", table.HeaderRow ? 1 : 0), new XAttribute("bandRow", 1)),
                new XElement(A + "tblGrid", Enumerable.Range(0, columns).Select(_ => new XElement(A + "gridCol", new XAttribute("w", colW)))));
            for (int r = 0; r < table.Rows.Count; r++)
            {
                var header = r == 0 && table.HeaderRow;
                var tr = new XElement(A + "tr", new XAttribute("h", rowH));
                foreach (var cell in table.Rows[r])
                    tr.Add(new XElement(A + "tc", new XElement(A + "txBody", new XElement(A + "bodyPr"), new XElement(A + "lstStyle"),
                            new XElement(A + "p", Runs(header ? cell.Select(c => c with { Bold = true }).ToList() : cell, 16, rels), EndRun(16))),
                        new XElement(A + "tcPr", Line("lnL"), Line("lnR"), Line("lnT"), Line("lnB"),
                            header ? new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "EFE8DC"))) : null)));
                tbl.Add(tr);
            }
            shapes.Add(new XElement(P + "graphicFrame",
                new XElement(P + "nvGraphicFramePr", new XElement(P + "cNvPr", new XAttribute("id", id), new XAttribute("name", "Tabela " + id++)),
                    new XElement(P + "cNvGraphicFramePr", new XElement(A + "graphicFrameLocks", new XAttribute("noGrp", 1))), new XElement(P + "nvPr")),
                new XElement(P + "xfrm", new XElement(A + "off", new XAttribute("x", Margin), new XAttribute("y", y)), new XElement(A + "ext", new XAttribute("cx", width), new XAttribute("cy", rowH * table.Rows.Count))),
                new XElement(A + "graphic", new XElement(A + "graphicData", new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/table"), tbl))));
        }
        if (slide.Image is { } image)
        {
            var path = "media/image" + (++media) + image.Extension;
            pictures.Add((path, image.Bytes));
            var rid = "rIdImg" + media;
            rels.Add((rid, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", "../" + path, false));
            XElement? svgExt = null;
            if (image.Svg is { } svg && image.Mime != "image/svg+xml")
            {
                var svgPath = "media/image" + media + ".svg";
                pictures.Add((svgPath, Encoding.UTF8.GetBytes(svg)));
                rels.Add(("rIdSvg" + media, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", "../" + svgPath, false));
                svgExt = new XElement(A + "extLst", new XElement(A + "ext", new XAttribute("uri", "{96DAC541-7B7A-43D3-8B79-37D633B846F1}"),
                    new XElement(Asvg + "svgBlip", new XAttribute(XNamespace.Xmlns + "asvg", Asvg), new XAttribute(R + "embed", "rIdSvg" + media))));
            }
            long boxX = slide.Text.Count > 0 ? (long)(6.9 * Inch) : Margin, boxW = slide.Text.Count > 0 ? SlideW - boxX - Margin : SlideW - 2 * Margin;
            long boxY = title.Length > 0 ? BodyTop : Margin, boxH = SlideH - boxY - (long)(0.4 * Inch);
            double scale = Math.Min(boxW / (double)image.Width, boxH / (double)image.Height);
            long w = (long)(image.Width * scale), h = (long)(image.Height * scale);
            // a small picture is not blown up past twice its size (it would only blur)
            if (scale > 2 * 9525) { w = image.Width * 2 * 9525; h = image.Height * 2 * 9525; }
            long x = boxX + (boxW - w) / 2, top = boxY + (boxH - h) / 2;
            shapes.Add(new XElement(P + "pic",
                new XElement(P + "nvPicPr", new XElement(P + "cNvPr", new XAttribute("id", id), new XAttribute("name", "Imagem " + id++), new XAttribute("descr", PublicationOffice.XmlSafe(image.Alt))),
                    new XElement(P + "cNvPicPr", new XElement(A + "picLocks", new XAttribute("noChangeAspect", 1))), new XElement(P + "nvPr")),
                new XElement(P + "blipFill", new XElement(A + "blip", new XAttribute(R + "embed", rid), svgExt), new XElement(A + "stretch", new XElement(A + "fillRect"))),
                new XElement(P + "spPr", Xfrm(x, top, w, h), Geometry())));
        }
        var xml = new XElement(P + "sld", Ns(), new XElement(P + "cSld", EmptyTree(shapes.ToArray())), new XElement(P + "clrMapOvr", new XElement(A + "masterClrMapping")));
        return (xml, pictures);
    }

    private static IEnumerable<XElement> Paragraphs(OfficeBlock block, List<(string, string, string, bool)> rels, int size = BodyPt)
    {
        switch (block)
        {
            case OfficeHeading h:
                yield return new XElement(A + "p", new XElement(A + "pPr", new XElement(A + "buNone")), Runs(h.Runs.Select(r => r with { Bold = true }).ToList(), size + 2, rels), EndRun(size + 2));
                break;
            case OfficeParagraph p:
                var props = new XElement(A + "pPr", p.Style == "math" ? new XAttribute("algn", "ctr") : null, p.Style is "quote" or "box" or "boxtitle" ? new XAttribute("marL", 457200) : null, new XElement(A + "buNone"));
                var runs = p.Style == "quote" ? p.Runs.Select(r => r with { Italic = true }).ToList() : p.Runs;
                yield return new XElement(A + "p", props, Runs(runs, size, rels, p.Style == "quote" ? "666666" : null), EndRun(size));
                break;
            case OfficeListItem li:
                var bullet = li.Ordered ? new XElement(A + "buAutoNum", new XAttribute("type", "arabicPeriod")) : new XElement(A + "buChar", new XAttribute("char", li.Level % 2 == 0 ? "•" : "◦"));
                var items = li.Checked is { } done ? new[] { new OfficeRun(done ? "☑ " : "☐ ") }.Concat(li.Runs).ToList() : li.Runs;
                yield return new XElement(A + "p", new XElement(A + "pPr", new XAttribute("marL", 342900 * (li.Level + 1)), new XAttribute("lvl", Math.Min(li.Level, 8)), new XAttribute("indent", -342900),
                    li.Checked is null ? bullet : new XElement(A + "buNone")), Runs(items, size, rels), EndRun(size));
                break;
            case OfficeCode code:
                foreach (var line in code.Text.Replace("\r", "").Split('\n'))
                    yield return new XElement(A + "p", new XElement(A + "pPr", new XElement(A + "buNone")),
                        new XElement(A + "r", RunProps(new OfficeRun(line, Code: true), Math.Min(CodePt, size), null), new XElement(A + "t", PublicationOffice.XmlSafe(line))), EndRun(Math.Min(CodePt, size)));
                break;
        }
    }

    private static IEnumerable<XElement> Runs(IReadOnlyList<OfficeRun> runs, int size, List<(string, string, string, bool)> rels, string? color = null)
    {
        foreach (var run in runs)
        {
            if (run.Break) { yield return new XElement(A + "br", new XElement(A + "rPr", new XAttribute("lang", "pt-BR"), new XAttribute("sz", size * 100))); continue; }
            var props = RunProps(run, size, color);
            if (run.Href is { } href)
            {
                var id = "rIdLink" + rels.Count;
                rels.Add((id, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink", href, true));
                props.Add(new XElement(A + "hlinkClick", new XAttribute(R + "id", id)));
            }
            yield return new XElement(A + "r", props, new XElement(A + "t", PublicationOffice.XmlSafe(run.Text)));
        }
    }

    private static XElement RunProps(OfficeRun run, int size, string? color)
    {
        var props = new XElement(A + "rPr", new XAttribute("lang", "pt-BR"), new XAttribute("sz", size * 100), new XAttribute("dirty", 0));
        if (run.Bold) props.Add(new XAttribute("b", 1));
        if (run.Italic || run.Math) props.Add(new XAttribute("i", 1));
        if (run.Strike) props.Add(new XAttribute("strike", "sngStrike"));
        if (color is not null) props.Add(new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", color))));
        if (run.Code) props.Add(new XElement(A + "latin", new XAttribute("typeface", "Consolas")), new XElement(A + "cs", new XAttribute("typeface", "Consolas")));
        else if (run.Math) props.Add(new XElement(A + "latin", new XAttribute("typeface", "Cambria Math")));
        // every run names its font: some readers carry a code font over to the runs after it
        else props.Add(new XElement(A + "latin", new XAttribute("typeface", "+mn-lt")), new XElement(A + "cs", new XAttribute("typeface", "+mn-cs")));
        return props;
    }

    private static XElement EndRun(int size) => new(A + "endParaRPr", new XAttribute("lang", "pt-BR"), new XAttribute("sz", size * 100), new XAttribute("dirty", 0));

    /// <summary>Lines a block takes on a text box <paramref name="widthInches"/> wide (a rough measure to split slides).</summary>
    private static int Lines(OfficeBlock block, double widthInches)
    {
        int PerLine(int pt) => Math.Max(10, (int)(widthInches * 72 / (pt * 0.52)));
        int Wrap(string text, int pt) => Math.Max(1, (int)Math.Ceiling(text.Length / (double)PerLine(pt)));
        return block switch
        {
            OfficeHeading h => Wrap(Plain(h.Runs), BodyPt + 2),
            OfficeParagraph p => Wrap(Plain(p.Runs), BodyPt),
            OfficeListItem li => Wrap(Plain(li.Runs), BodyPt),
            OfficeCode c => (int)Math.Ceiling(c.Text.Split('\n').Sum(l => Wrap(l, CodePt)) * CodePt / (double)BodyPt),
            _ => 1
        };
    }

    private static string Plain(IReadOnlyList<OfficeRun> runs) => string.Concat(runs.Select(r => r.Break ? "\n" : r.Text));

    private static XElement TitlePlaceholder(int id, string name, string? text) =>
        new(P + "sp",
            new XElement(P + "nvSpPr", new XElement(P + "cNvPr", new XAttribute("id", id), new XAttribute("name", "Título " + id)),
                new XElement(P + "cNvSpPr", new XElement(A + "spLocks", new XAttribute("noGrp", 1))), new XElement(P + "nvPr", new XElement(P + "ph", new XAttribute("type", "title")))),
            new XElement(P + "spPr", Xfrm(Margin, (long)(0.3 * Inch), SlideW - 2 * Margin, (long)(1.0 * Inch))),
            new XElement(P + "txBody", new XElement(A + "bodyPr", new XAttribute("anchor", "b"), new XElement(A + "normAutofit")), new XElement(A + "lstStyle"),
                new XElement(A + "p", text is null ? null : new XElement(A + "r",
                    new XElement(A + "rPr", new XAttribute("lang", "pt-BR"), new XAttribute("sz", TitlePt * 100), new XAttribute("b", 1), new XAttribute("dirty", 0),
                        new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "6B4F2A")))),
                    new XElement(A + "t", PublicationOffice.XmlSafe(text))), EndRun(TitlePt))));

    private static XElement NotesPlaceholder(int id, string type, int? idx, XElement? body) =>
        new(P + "sp",
            new XElement(P + "nvSpPr", new XElement(P + "cNvPr", new XAttribute("id", id), new XAttribute("name", type + " " + id)),
                new XElement(P + "cNvSpPr", new XElement(A + "spLocks", new XAttribute("noGrp", 1))),
                new XElement(P + "nvPr", new XElement(P + "ph", new XAttribute("type", type), idx is { } i ? new XAttribute("idx", i) : null))),
            type == "sldImg"
                ? new XElement(P + "spPr", Xfrm(381000, 685800, 6096000, 3429000), Geometry())
                : new XElement(P + "spPr", Xfrm(685800, 4343400, 5486400, 4114800), Geometry()),
            type == "body" ? body ?? new XElement(P + "txBody", new XElement(A + "bodyPr"), new XElement(A + "lstStyle"), new XElement(A + "p")) : null);

    private static XElement EmptyTree(params XElement[] shapes) => new(P + "spTree",
        new XElement(P + "nvGrpSpPr", new XElement(P + "cNvPr", new XAttribute("id", 1), new XAttribute("name", "")), new XElement(P + "cNvGrpSpPr"), new XElement(P + "nvPr")),
        new XElement(P + "grpSpPr", new XElement(A + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(A + "ext", new XAttribute("cx", 0), new XAttribute("cy", 0)),
            new XElement(A + "chOff", new XAttribute("x", 0), new XAttribute("y", 0)), new XElement(A + "chExt", new XAttribute("cx", 0), new XAttribute("cy", 0)))),
        shapes);

    private static XElement Xfrm(long x, long y, long w, long h) => new(A + "xfrm",
        new XElement(A + "off", new XAttribute("x", x), new XAttribute("y", y)), new XElement(A + "ext", new XAttribute("cx", Math.Max(1, w)), new XAttribute("cy", Math.Max(1, h))));

    private static XElement Geometry() => new(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst"));

    private static XElement Line(string side) => new(A + side, new XAttribute("w", 9525), new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "999999"))));

    private static XElement Master() => new(P + "sldMaster", Ns(),
        new XElement(P + "cSld", new XElement(P + "bg", new XElement(P + "bgPr", new XElement(A + "solidFill", new XElement(A + "srgbClr", new XAttribute("val", "FFFFFF"))), new XElement(A + "effectLst"))),
            EmptyTree(TitlePlaceholder(2, "Título", null))),
        new XElement(P + "clrMap", new XAttribute("bg1", "lt1"), new XAttribute("tx1", "dk1"), new XAttribute("bg2", "lt2"), new XAttribute("tx2", "dk2"),
            new XAttribute("accent1", "accent1"), new XAttribute("accent2", "accent2"), new XAttribute("accent3", "accent3"), new XAttribute("accent4", "accent4"),
            new XAttribute("accent5", "accent5"), new XAttribute("accent6", "accent6"), new XAttribute("hlink", "hlink"), new XAttribute("folHlink", "folHlink")),
        new XElement(P + "sldLayoutIdLst", new XElement(P + "sldLayoutId", new XAttribute("id", 2147483649L), new XAttribute(R + "id", "rId1"))));

    private static XElement Theme()
    {
        XElement Color(string name, string hex) => new(A + name, new XElement(A + "srgbClr", new XAttribute("val", hex)));
        XElement Solid() => new(A + "solidFill", new XElement(A + "schemeClr", new XAttribute("val", "phClr")));
        XElement Ln(int w) => new(A + "ln", new XAttribute("w", w), Solid());
        XElement Fonts(string kind, string face) => new(A + kind, new XElement(A + "latin", new XAttribute("typeface", face)), new XElement(A + "ea", new XAttribute("typeface", "")), new XElement(A + "cs", new XAttribute("typeface", "")));
        return new XElement(A + "theme", new XAttribute(XNamespace.Xmlns + "a", A), new XAttribute("name", "Urbe"),
            new XElement(A + "themeElements",
                new XElement(A + "clrScheme", new XAttribute("name", "Urbe"),
                    new XElement(A + "dk1", new XElement(A + "srgbClr", new XAttribute("val", "25272C"))), new XElement(A + "lt1", new XElement(A + "srgbClr", new XAttribute("val", "FFFFFF"))),
                    Color("dk2", "3B3328"), Color("lt2", "F3F1EC"), Color("accent1", "6B4F2A"), Color("accent2", "075985"), Color("accent3", "312E81"),
                    Color("accent4", "9C7B45"), Color("accent5", "4D7C0F"), Color("accent6", "B45309"), Color("hlink", "0563C1"), Color("folHlink", "954F72")),
                new XElement(A + "fontScheme", new XAttribute("name", "Urbe"), Fonts("majorFont", "Calibri"), Fonts("minorFont", "Calibri")),
                new XElement(A + "fmtScheme", new XAttribute("name", "Urbe"),
                    new XElement(A + "fillStyleLst", Solid(), Solid(), Solid()),
                    new XElement(A + "lnStyleLst", Ln(6350), Ln(12700), Ln(19050)),
                    new XElement(A + "effectStyleLst", new XElement(A + "effectStyle", new XElement(A + "effectLst")), new XElement(A + "effectStyle", new XElement(A + "effectLst")), new XElement(A + "effectStyle", new XElement(A + "effectLst"))),
                    new XElement(A + "bgFillStyleLst", Solid(), Solid(), Solid()))),
            new XElement(A + "objectDefaults"), new XElement(A + "extraClrSchemeLst"));
    }

    private static IEnumerable<XAttribute> Ns() =>
        [new(XNamespace.Xmlns + "a", A), new(XNamespace.Xmlns + "r", R), new(XNamespace.Xmlns + "p", P)];

    private static XElement Rels(params (string Id, string Type, string Target, bool External)[] rels) =>
        new(Rel + "Relationships", rels.Select(r => new XElement(Rel + "Relationship", new XAttribute("Id", r.Id), new XAttribute("Type", r.Type), new XAttribute("Target", r.Target),
            r.External ? new XAttribute("TargetMode", "External") : null)));

    private static XElement Default(string extension, string type) => new(CT + "Default", new XAttribute("Extension", extension), new XAttribute("ContentType", type));
    private static XElement Override(string part, string type) => new(CT + "Override", new XAttribute("PartName", part), new XAttribute("ContentType", type));

    private static void Xml(ZipArchive zip, string name, XElement root) =>
        Bytes(zip, name, Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n" + root.ToString(SaveOptions.DisableFormatting)));

    private static void Bytes(ZipArchive zip, string name, byte[] bytes)
    { using var target = zip.CreateEntry(name, CompressionLevel.Optimal).Open(); target.Write(bytes); }
}
