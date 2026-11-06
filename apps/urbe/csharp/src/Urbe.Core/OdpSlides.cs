using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>
/// CST-237: an OpenDocument presentation (.odp) read into the same slide model as <see cref="PptxDeck"/>, so the native
/// slide reader draws it: page size from the master's page layout; per page its background, frames with text (spans with
/// size, bold, italic, underline, colour, font; paragraph alignment; lists), pictures, tables as a grid, rectangles,
/// ellipses and lines with solid fill and stroke, rotation, the master page's decorations underneath, placeholders that
/// take their place from the master, and the speaker notes. Styles follow their parent chain (automatic styles of the
/// content, the styles of styles.xml). Charts, formulas, embedded objects and media are marked and named, never
/// silently dropped; everything goes through the bounded <see cref="ReaderArchive"/> (no DTD, no path outside the package).
/// </summary>
public static class OdpSlides
{
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";
    private static readonly XNamespace Fo = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";
    private static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Presentation = "urn:oasis:names:tc:opendocument:xmlns:presentation:1.0";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    public static PptxDeck Open(byte[] bytes)
    {
        if (FileSignatures.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, 4096))) != FileSignature.Zip)
            throw new InvalidDataException("O arquivo não é uma apresentação .odp: o conteúdo parece " + FileSignatures.Describe(FileSignatures.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, 4096)))) + ".");
        var archive = new ReaderArchive(bytes);
        try
        {
            if (archive.Contains("META-INF/manifest.xml") && archive.Text("META-INF/manifest.xml").Contains("encryption-data", StringComparison.Ordinal))
                throw new NotSupportedException("Esta apresentação está protegida por senha; o Urbe não a lê. Abra em outro app.");
            if (!archive.Contains("content.xml")) throw new InvalidDataException("O pacote não tem content.xml; não é uma apresentação OpenDocument.");
            var content = archive.Xml("content.xml").Root ?? throw new InvalidDataException("Apresentação vazia.");
            var stylesRoot = archive.Contains("styles.xml") ? archive.Xml("styles.xml").Root : null;
            var styles = new Styles(content, stylesRoot);
            var presentation = content.Element(Office + "body")?.Element(Office + "presentation") ?? throw new InvalidDataException("O documento não é uma apresentação.");
            var pages = presentation.Elements(Draw + "page").ToArray();
            if (pages.Length == 0) throw new InvalidDataException("A apresentação não tem slides.");
            if (pages.Length > PptxDeck.MaxSlides) throw new InvalidDataException("A apresentação tem mais de " + PptxDeck.MaxSlides + " slides; o leitor não a abre por inteiro.");
            var (width, height) = styles.PageSize(pages[0].Attribute(Draw + "master-page-name")?.Value);
            var slides = new PptxReadSlide[pages.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                try { slides[i] = ReadPage(i, pages[i], styles, archive); }
                catch (Exception ex) when (ex is InvalidDataException or FormatException or OverflowException)
                { slides[i] = new PptxReadSlide(i + 1, PptxColor.White, null, [], "", "", ["o slide inteiro: " + ex.Message], false); }
            }
            var notices = new List<string>();
            if (archive.Paths.Any(p => p.StartsWith("Basic/", StringComparison.Ordinal) || p.StartsWith("Scripts/", StringComparison.Ordinal))) notices.Add("macros (nunca são executadas)");
            return new PptxDeck(archive, (long)Math.Round(width * PptxDeck.EmuPerPoint), (long)Math.Round(height * PptxDeck.EmuPerPoint), slides, notices);
        }
        catch { archive.Dispose(); throw; }
    }

    private sealed class Page
    {
        public List<PptxElement> Elements { get; } = [];
        public SortedSet<string> NotRendered { get; } = new(StringComparer.Ordinal);
        public StringBuilder Text { get; } = new();
        public int Count;
    }

    private static PptxReadSlide ReadPage(int index, XElement page, Styles styles, ReaderArchive archive)
    {
        var state = new Page();
        string? masterName = page.Attribute(Draw + "master-page-name")?.Value;
        var master = styles.Master(masterName);
        var background = styles.Fill(page.Attribute(Draw + "style-name")?.Value, "drawing-page")
            ?? styles.Fill(master?.Attribute(Draw + "style-name")?.Value, "drawing-page") ?? PptxColor.White;
        // the master's decorations first (its placeholders are only positions for the page's own frames)
        if (master is not null)
            foreach (var shape in master.Elements())
                if (shape.Attribute(Presentation + "placeholder")?.Value != "true" && shape.Attribute(Presentation + "class") is null)
                    ReadShape(shape, styles, archive, state, master, 0, collectText: false);
        foreach (var shape in page.Elements())
            if (shape.Name != Presentation + "notes") ReadShape(shape, styles, archive, state, master, 0, collectText: true);
        var notes = page.Element(Presentation + "notes") is { } n
            ? string.Join("\n", n.Descendants(Draw + "text-box").SelectMany(b => b.Elements()).Select(PlainText).Where(t => t.Length > 0))
            : "";
        if (page.Descendants().Any(e => e.Name.NamespaceName == "urn:oasis:names:tc:opendocument:xmlns:animation:1.0"))
            state.NotRendered.Add("animações");
        if (styles.Transition(page.Attribute(Draw + "style-name")?.Value)) state.NotRendered.Add("transição de slide");
        bool hidden = styles.Hidden(page.Attribute(Draw + "style-name")?.Value);
        return new PptxReadSlide(index + 1, background, null, state.Elements, state.Text.ToString().Trim(), notes, state.NotRendered.ToArray(), hidden);
    }

    private static void ReadShape(XElement e, Styles styles, ReaderArchive archive, Page page, XElement? master, int depth, bool collectText)
    {
        if (depth > PptxDeck.MaxDepth || ++page.Count > PptxDeck.MaxElementsPerSlide) throw new InvalidDataException("O slide tem objetos demais.");
        string? style = e.Attribute(Draw + "style-name")?.Value ?? e.Attribute(Presentation + "style-name")?.Value;
        if (e.Name == Draw + "g") { foreach (var child in e.Elements()) ReadShape(child, styles, archive, page, master, depth + 1, collectText); return; }
        var (x, y, w, h) = Geometry(e);
        double rotation = Rotation(e, ref x, ref y);
        if (w <= 0 && h <= 0 && e.Attribute(Presentation + "class")?.Value is { } cls && master is not null &&
            master.Elements().FirstOrDefault(m => m.Attribute(Presentation + "class")?.Value == cls) is { } placeholder)
            (x, y, w, h) = Geometry(placeholder);
        if (e.Name == Draw + "line")
        {
            double x1 = Length(e.Attribute(Svg + "x1")), y1 = Length(e.Attribute(Svg + "y1")), x2 = Length(e.Attribute(Svg + "x2")), y2 = Length(e.Attribute(Svg + "y2"));
            page.Elements.Add(new PptxShape(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1), 0, x2 < x1, y2 < y1, "line", null,
                styles.Stroke(style) ?? PptxColor.Black, styles.StrokeWidth(style), null));
            return;
        }
        if (e.Name == Draw + "frame")
        {
            if (e.Element(Draw + "image") is { } image && Media(image, archive) is { } media && e.Element(Draw + "object") is null)
            { page.Elements.Add(new PptxPicture(x, y, w, h, rotation, false, false, media)); return; }
            if (e.Element(Table + "table") is { } table) { page.Elements.Add(ReadTable(table, x, y, w, h, styles, page, collectText)); return; }
            if (e.Element(Draw + "object") is not null || e.Element(Draw + "object-ole") is not null)
            {
                string label = e.Element(Draw + "object")?.Attribute(XLink + "href")?.Value is { } href && archive.Contains(href.TrimStart('.', '/') + "/content.xml") &&
                               archive.Text(href.TrimStart('.', '/') + "/content.xml") is var inner ? inner.Contains("math:math", StringComparison.Ordinal) || inner.Contains("<math", StringComparison.Ordinal) ? "Equação" : inner.Contains("chart:chart", StringComparison.Ordinal) ? "Gráfico" : "Objeto incorporado" : "Objeto incorporado";
                page.NotRendered.Add(label.ToLowerInvariant() switch { "equação" => "equações", "gráfico" => "gráficos", _ => "objetos incorporados" });
                if (e.Element(Draw + "image") is { } fallback && Media(fallback, archive) is { } poster) page.Elements.Add(new PptxPicture(x, y, w, h, rotation, false, false, poster));
                else page.Elements.Add(new PptxMissing(x, y, w, h, rotation, label + " — não desenhado aqui"));
                return;
            }
            if (e.Element(Draw + "plugin") is not null) { page.NotRendered.Add("vídeo ou áudio incorporado"); page.Elements.Add(new PptxMissing(x, y, w, h, rotation, "Mídia — não reproduzida aqui")); return; }
            if (e.Element(Draw + "image") is { } broken) { page.NotRendered.Add("imagens em formato não suportado ou vinculadas"); page.Elements.Add(new PptxMissing(x, y, w, h, rotation, "Imagem — não desenhada aqui")); return; }
            if (e.Element(Draw + "text-box") is { } box)
            {
                var text = ReadText(box.Elements(), styles, style, page, collectText);
                page.Elements.Add(new PptxShape(x, y, w, h, rotation, false, false, "rect", styles.Fill(style, "presentation") ?? styles.Fill(style, "graphic"),
                    styles.Stroke(style), styles.StrokeWidth(style), text));
            }
            return;
        }
        string? geometry = e.Name.LocalName switch
        {
            "rect" => styles.CornerRadius(e) > 0 ? "roundRect" : "rect",
            "ellipse" or "circle" => "ellipse",
            "custom-shape" => e.Element(Draw + "enhanced-geometry")?.Attribute(Draw + "type")?.Value switch
            {
                "rectangle" or "ooxml-rect" or "non-primitive" or null => "rect",
                "round-rectangle" or "ooxml-roundRect" => "roundRect",
                "ellipse" or "circle" or "ooxml-ellipse" => "ellipse",
                "isosceles-triangle" or "ooxml-triangle" => "triangle",
                "diamond" or "ooxml-diamond" => "diamond",
                "right-arrow" or "ooxml-rightArrow" => "rightArrow",
                var other => other
            },
            _ => null
        };
        if (geometry is null)
        {
            if (e.Name.Namespace == Draw && e.Name.LocalName is "polygon" or "polyline" or "path" or "connector" or "measure" or "caption")
            { page.NotRendered.Add("formas livres"); page.Elements.Add(new PptxMissing(x, y, w, h, rotation, "Forma — não desenhada aqui")); }
            return;
        }
        if (geometry is not ("rect" or "roundRect" or "ellipse" or "triangle" or "diamond" or "rightArrow")) { page.NotRendered.Add("formas desconhecidas (desenhadas como retângulo)"); geometry = "rect"; }
        var body = e.Elements().Any(c => c.Name == Text + "p" || c.Name == Text + "list") ? ReadText(e.Elements(), styles, style, page, collectText) : null;
        page.Elements.Add(new PptxShape(x, y, w, h, rotation, false, false, geometry, styles.Fill(style, "graphic"),
            styles.Stroke(style), styles.StrokeWidth(style), body));
    }

    private static PptxTable ReadTable(XElement table, double x, double y, double w, double h, Styles styles, Page page, bool collectText)
    {
        var rows = table.Descendants(Table + "table-row").ToArray();
        int columns = Math.Max(1, rows.Select(r => r.Elements().Where(c => c.Name == Table + "table-cell" || c.Name == Table + "covered-table-cell")
            .Sum(c => int.TryParse(c.Attribute(Table + "number-columns-repeated")?.Value, out int n) ? Math.Clamp(n, 1, 64) : 1)).DefaultIfEmpty(1).Max());
        var cells = new List<PptxCell>();
        var columnStyles = table.Elements(Table + "table-column").SelectMany(c => Enumerable.Repeat(c.Attribute(Table + "default-cell-style-name")?.Value,
            int.TryParse(c.Attribute(Table + "number-columns-repeated")?.Value, out int n) ? Math.Clamp(n, 1, 64) : 1)).ToArray();
        for (int r = 0; r < rows.Length; r++)
        {
            int c = 0;
            string? rowStyle = rows[r].Attribute(Table + "default-cell-style-name")?.Value;
            foreach (var cell in rows[r].Elements())
            {
                if (cell.Name == Table + "covered-table-cell") { c++; continue; }
                if (cell.Name != Table + "table-cell") continue;
                int span = int.TryParse(cell.Attribute(Table + "number-columns-spanned")?.Value, out int cs) ? Math.Clamp(cs, 1, columns) : 1;
                int rowSpan = int.TryParse(cell.Attribute(Table + "number-rows-spanned")?.Value, out int rs) ? Math.Clamp(rs, 1, rows.Length) : 1;
                cells.Add(new PptxCell(r, c, rowSpan, span, ReadText(cell.Elements(), styles, null, page, collectText), styles.Fill(cell.Attribute(Table + "style-name")?.Value ?? rowStyle ?? (c < columnStyles.Length ? columnStyles[c] : null), "table-cell")));
                c += span;
            }
        }
        double rowHeight = h / Math.Max(1, rows.Length), columnWidth = w / columns;
        return new PptxTable(x, y, w, h, Enumerable.Repeat(columnWidth, columns).ToArray(), Enumerable.Repeat(rowHeight, Math.Max(1, rows.Length)).ToArray(), cells);
    }

    private static PptxText ReadText(IEnumerable<XElement> blocks, Styles styles, string? frameStyle, Page page, bool collectText)
    {
        var paragraphs = new List<PptxParagraph>();
        void Paragraph(XElement p, int level, string? bullet)
        {
            string? pStyle = p.Attribute(Text + "style-name")?.Value;
            var look = styles.TextLook(frameStyle, pStyle);
            var runs = new List<PptxRun>();
            void Walk(XElement node, Look current)
            {
                foreach (var part in node.Nodes())
                {
                    if (part is XText t) { if (t.Value.Length > 0) runs.Add(current.Run(Collapse(t.Value))); continue; }
                    if (part is not XElement child) continue;
                    if (child.Name == Text + "span") Walk(child, styles.TextLook(frameStyle, pStyle, child.Attribute(Text + "style-name")?.Value));
                    else if (child.Name == Text + "a") Walk(child, current);
                    else if (child.Name == Text + "s") runs.Add(current.Run(new string(' ', int.TryParse(child.Attribute(Text + "c")?.Value, out int n) ? Math.Clamp(n, 1, 200) : 1)));
                    else if (child.Name == Text + "tab") runs.Add(current.Run("\t"));
                    else if (child.Name == Text + "line-break") runs.Add(current.Run("") with { LineBreak = true });
                    else if (child.Name.Namespace == Text) Walk(child, current);
                }
            }
            Walk(p, look);
            var paragraph = new PptxParagraph(runs, styles.Align(frameStyle, pStyle), level * 18, bullet is null ? 0 : -14, bullet, null, 0, look.Size * .2, 1, null, look.Size);
            paragraphs.Add(paragraph);
            if (collectText && paragraph.Text.Trim().Length > 0) page.Text.Append(paragraph.Text).Append('\n');
        }
        void List(XElement list, int level)
        {
            if (level > 9) return;
            int number = 0;
            bool numbered = styles.Numbered(list.Attribute(Text + "style-name")?.Value);
            foreach (var item in list.Elements(Text + "list-item").Concat(list.Elements(Text + "list-header")))
                foreach (var child in item.Elements())
                {
                    if (child.Name == Text + "list") List(child, level + 1);
                    else if (child.Name == Text + "p" || child.Name == Text + "h") Paragraph(child, level + 1, item.Name.LocalName == "list-header" ? null : numbered ? (++number) + "." : "•");
                }
        }
        foreach (var block in blocks)
        {
            if (block.Name == Text + "p" || block.Name == Text + "h") Paragraph(block, 0, null);
            else if (block.Name == Text + "list") List(block, 0);
        }
        var (left, top, right, bottom) = styles.Padding(frameStyle);
        return new PptxText(paragraphs, styles.Anchor(frameStyle), left, top, right, bottom, styles.Wraps(frameStyle));
    }

    private static string PlainText(XElement block) => block.Name == Text + "list"
        ? string.Join("\n", block.Descendants().Where(d => d.Name == Text + "p" || d.Name == Text + "h").Select(PlainText))
        : Collapse(string.Concat(block.DescendantNodes().Select(n => n switch
        {
            XText t => t.Value,
            XElement { Name.LocalName: "line-break" } => "\n",
            XElement { Name.LocalName: "s" } => " ",
            XElement { Name.LocalName: "tab" } => "\t",
            _ => ""
        }))).Trim();

    private static string Collapse(string s) => s.Replace('\n', ' ').Replace('\r', ' ');

    private static string? Media(XElement image, ReaderArchive archive)
    {
        var href = image.Attribute(XLink + "href")?.Value;
        if (string.IsNullOrEmpty(href) || href.Contains("://", StringComparison.Ordinal) || href.StartsWith('/')) return null;
        var path = href.StartsWith("./", StringComparison.Ordinal) ? href[2..] : href;
        if (!archive.Contains(path)) return null;
        var head = archive.Bytes(path);
        return FileSignatures.IsImage(FileSignatures.Detect(head)) ? path : null;
    }

    private static (double X, double Y, double W, double H) Geometry(XElement e) =>
        (Length(e.Attribute(Svg + "x")), Length(e.Attribute(Svg + "y")), Length(e.Attribute(Svg + "width")), Length(e.Attribute(Svg + "height")));

    /// <summary>draw:transform "rotate (θ) translate (x y)": θ in radians, counter-clockwise, about the top-left corner.</summary>
    private static double Rotation(XElement e, ref double x, ref double y)
    {
        var transform = e.Attribute(Draw + "transform")?.Value;
        if (transform is null) return 0;
        double angle = 0;
        var rotate = System.Text.RegularExpressions.Regex.Match(transform, @"rotate\s*\(\s*([-+0-9.eE]+)\s*\)");
        if (rotate.Success && double.TryParse(rotate.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double radians)) angle = -radians * 180 / Math.PI;
        var translate = System.Text.RegularExpressions.Regex.Match(transform, @"translate\s*\(\s*([^\s,)]+)[\s,]+([^\s)]+)\s*\)");
        if (translate.Success) { x = Length(translate.Groups[1].Value); y = Length(translate.Groups[2].Value); }
        if (!double.IsFinite(angle)) angle = 0;
        return ((angle % 360) + 360) % 360;
    }

    private static double Length(XAttribute? a) => a is null ? 0 : Length(a.Value);

    /// <summary>An ODF length ("2.5cm", "10mm", "1in", "12pt", "100px") in points; 0 when it is not one.</summary>
    public static double Length(string value)
    {
        value = value.Trim();
        int unit = value.Length;
        while (unit > 0 && char.IsLetter(value[unit - 1])) unit--;
        if (!double.TryParse(value[..unit], NumberStyles.Float, CultureInfo.InvariantCulture, out double n) || !double.IsFinite(n)) return 0;
        double points = value[unit..].ToLowerInvariant() switch
        {
            "cm" => n * 72 / 2.54, "mm" => n * 72 / 25.4, "in" => n * 72, "pt" => n, "pc" => n * 12, "px" => n * .75, "" => n * 72 / 2540, _ => 0
        };
        return Math.Clamp(points, -100000, 100000);
    }

    /// <summary>A text look: size (points), bold, italic, underline, strike, colour, font.</summary>
    private sealed record Look(double Size, bool Bold, bool Italic, bool Underline, bool Strike, PptxColor Color, string? Font)
    {
        public PptxRun Run(string text) => new(text, Size, Bold, Italic, Underline, Strike, Color, Font);
    }

    /// <summary>The styles of the package: automatic styles of content.xml, the styles and automatic styles of styles.xml, masters and page layouts.</summary>
    private sealed class Styles
    {
        private readonly Dictionary<(string Family, string Name), XElement> _styles = [];
        private readonly Dictionary<string, XElement> _byName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, XElement> _defaults = new(StringComparer.Ordinal);
        private readonly Dictionary<string, XElement> _listStyles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, XElement> _layouts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, XElement> _masters = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _fonts = new(StringComparer.Ordinal);

        public Styles(XElement content, XElement? styles)
        {
            foreach (var root in new[] { styles, content })
            {
                if (root is null) continue;
                foreach (var group in root.Elements().Where(g => g.Name == Office + "styles" || g.Name == Office + "automatic-styles" || g.Name == Office + "master-styles" || g.Name == Office + "font-face-decls"))
                    foreach (var s in group.Elements())
                    {
                        if (s.Name == Style + "default-style") { _defaults[s.Attribute(Style + "family")?.Value ?? ""] = s; continue; }
                        string? name = s.Attribute(Style + "name")?.Value;
                        if (name is null) continue;
                        if (s.Name == Style + "style") { _styles[(s.Attribute(Style + "family")?.Value ?? "", name)] = s; _byName.TryAdd(name, s); }
                        else if (s.Name == Text + "list-style") _listStyles[name] = s;
                        else if (s.Name == Style + "page-layout") _layouts[name] = s;
                        else if (s.Name == Style + "master-page") _masters[name] = s;
                        else if (s.Name == Style + "font-face") _fonts[name] = s.Attribute(Svg + "font-family")?.Value?.Trim('\'', '"') ?? name;
                    }
            }
        }

        public XElement? Master(string? name) => name is not null && _masters.TryGetValue(name, out var m) ? m : _masters.Values.FirstOrDefault();

        public (double Width, double Height) PageSize(string? master)
        {
            var layoutName = Master(master)?.Attribute(Style + "page-layout-name")?.Value;
            var props = layoutName is not null && _layouts.TryGetValue(layoutName, out var layout) ? layout.Element(Style + "page-layout-properties") : null;
            double w = Length(props?.Attribute(Fo + "page-width")), h = Length(props?.Attribute(Fo + "page-height"));
            // between one and 56 inches, as for PowerPoint; anything else is a broken size: 28 × 15.75 cm (16:9)
            if (w is < 72 or > 4032 || h is < 72 or > 4032) { w = 28 * 72 / 2.54; h = 15.75 * 72 / 2.54; }
            return (w, h);
        }

        /// <summary>A property along the parent chain of a style, ending at the family's default style.</summary>
        private string? Property(string? name, string family, string element, XNamespace ns, string attribute)
        {
            for (int depth = 0; name is not null && depth < 16; depth++)
            {
                var style = _styles.GetValueOrDefault((family, name)) ?? _byName.GetValueOrDefault(name);
                if (style is null) break;
                // LibreOffice also writes properties in its own namespace (loext:graphic-properties on table cells)
                if (style.Elements().FirstOrDefault(c => c.Name.LocalName == element)?.Attribute(ns + attribute)?.Value is { } value) return value;
                family = style.Attribute(Style + "family")?.Value ?? family;
                name = style.Attribute(Style + "parent-style-name")?.Value;
            }
            return _defaults.GetValueOrDefault(family)?.Elements().FirstOrDefault(c => c.Name.LocalName == element)?.Attribute(ns + attribute)?.Value;
        }

        public PptxColor? Fill(string? name, string family)
        {
            if (name is null && family != "graphic") return null;
            string element = family == "table-cell" ? "table-cell-properties" : family == "drawing-page" ? "drawing-page-properties" : "graphic-properties";
            if (family == "table-cell" && Property(name, family, element, Fo, "background-color") is { } background && background != "transparent") return Color(background);
            if (family == "table-cell") element = "graphic-properties";
            var fill = Property(name, family, element, Draw, "fill");
            var color = Property(name, family, element, Draw, "fill-color");
            // gradients, hatches and bitmaps are read as their base colour (said as a simplification by the reader)
            return fill switch { "solid" or "gradient" or "hatch" or "bitmap" => color is null ? null : Color(color), _ => null };
        }

        public PptxColor? Stroke(string? name)
        {
            if (name is null) return null;
            var stroke = Property(name, "graphic", "graphic-properties", Draw, "stroke") ?? Property(name, "presentation", "graphic-properties", Draw, "stroke");
            if (stroke is null or "none") return null;
            var color = Property(name, "graphic", "graphic-properties", Svg, "stroke-color") ?? Property(name, "presentation", "graphic-properties", Svg, "stroke-color");
            return color is null ? PptxColor.Black : Color(color);
        }

        public double StrokeWidth(string? name) =>
            Math.Clamp(Length(Property(name, "graphic", "graphic-properties", Svg, "stroke-width") ?? "0.75pt"), .5, 72);

        public double CornerRadius(XElement e) => Length(e.Attribute(Draw + "corner-radius"));

        /// <summary>The text insets of a frame or shape (ODF default 0.125 cm × 0.25 cm, as LibreOffice).</summary>
        public (double Left, double Top, double Right, double Bottom) Padding(string? name)
        {
            double Get(string side, double fallback) => (Property(name, "graphic", "graphic-properties", Fo, "padding-" + side) ?? Property(name, "presentation", "graphic-properties", Fo, "padding-" + side)) is { } v ? Math.Clamp(Length(v), 0, 200) : fallback;
            return (Get("left", 7.1), Get("top", 3.5), Get("right", 7.1), Get("bottom", 3.5));
        }

        public bool Wraps(string? name) =>
            (Property(name, "graphic", "graphic-properties", Fo, "wrap-option") ?? Property(name, "presentation", "graphic-properties", Fo, "wrap-option")) != "no-wrap";

        public bool Transition(string? name) => name is not null &&
            (Property(name, "drawing-page", "drawing-page-properties", Presentation, "transition-type") is not null ||
             Property(name, "drawing-page", "drawing-page-properties", Presentation, "transition-style") is not null);

        public bool Hidden(string? name) => name is not null &&
            Property(name, "drawing-page", "drawing-page-properties", Presentation, "visibility") == "hidden";

        public PptxAnchor Anchor(string? name) =>
            (Property(name, "presentation", "graphic-properties", Draw, "textarea-vertical-align") ?? Property(name, "graphic", "graphic-properties", Draw, "textarea-vertical-align")) switch
            { "middle" => PptxAnchor.Middle, "bottom" => PptxAnchor.Bottom, _ => PptxAnchor.Top };

        public PptxTextAlign Align(string? frame, string? paragraph) =>
            (Property(paragraph, "paragraph", "paragraph-properties", Fo, "text-align") ?? Property(frame, "presentation", "paragraph-properties", Fo, "text-align")
             ?? Property(frame, "graphic", "paragraph-properties", Fo, "text-align")) switch
            {
                "center" => PptxTextAlign.Center, "end" or "right" => PptxTextAlign.Right, "justify" => PptxTextAlign.Justify, _ => PptxTextAlign.Left
            };

        public bool Numbered(string? list) => list is not null && _listStyles.TryGetValue(list, out var s) && s.Elements(Text + "list-level-style-number").Any() && !s.Elements(Text + "list-level-style-bullet").Any();

        public Look TextLook(string? frame, string? paragraph, string? span = null)
        {
            string? Get(string attribute, XNamespace ns) =>
                Property(span, "text", "text-properties", ns, attribute) ?? Property(paragraph, "paragraph", "text-properties", ns, attribute)
                ?? Property(frame, "presentation", "text-properties", ns, attribute) ?? Property(frame, "graphic", "text-properties", ns, attribute);
            double size = Get("font-size", Fo) is { } s && !s.EndsWith('%') ? Length(s) : 18;
            if (size is <= 1 or > 400) size = 18;
            string? font = Get("font-name", Style) is { } name ? _fonts.GetValueOrDefault(name, name) : Get("font-family", Fo)?.Trim('\'', '"');
            return new Look(size, Get("font-weight", Fo) is "bold" or "600" or "700" or "800" or "900", Get("font-style", Fo) is "italic" or "oblique",
                Get("text-underline-style", Style) is { } u && u != "none", Get("text-line-through-style", Style) is { } t && t != "none",
                Get("color", Fo) is { } c && Color(c) is { } color ? color : PptxColor.Black, font);
        }

        public static PptxColor? Color(string value)
        {
            value = value.Trim();
            if (value.Length != 7 || value[0] != '#' || !int.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)) return null;
            return new PptxColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }
    }
}
