using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Urbe.Core;

/// <summary>An RGBA colour of a slide.</summary>
public readonly record struct PptxColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly PptxColor Black = new(0, 0, 0), White = new(255, 255, 255);
}

public enum PptxTextAlign { Left, Center, Right, Justify }
public enum PptxAnchor { Top, Middle, Bottom }

/// <summary>A piece of text in one look; sizes are in points.</summary>
public sealed record PptxRun(string Text, double Size, bool Bold, bool Italic, bool Underline, bool Strike, PptxColor Color, string? Font, bool LineBreak = false);

/// <summary>A paragraph: runs, alignment, indents and spacing in points; <see cref="LineSpacing"/> is a multiple of the line (1 = single) unless <see cref="LineSpacingPoints"/> is set.</summary>
public sealed record PptxParagraph(IReadOnlyList<PptxRun> Runs, PptxTextAlign Align, double MarginLeft, double Indent, string? Bullet, PptxColor? BulletColor,
    double SpaceBefore, double SpaceAfter, double LineSpacing, double? LineSpacingPoints, double EmptySize)
{
    public string Text => string.Concat(Runs.Select(r => r.LineBreak ? "\n" : r.Text));
}

/// <summary>The text of a shape or a table cell, with its insets (points), anchor and the autofit scale already applied.</summary>
public sealed record PptxText(IReadOnlyList<PptxParagraph> Paragraphs, PptxAnchor Anchor, double InsetLeft, double InsetTop, double InsetRight, double InsetBottom, bool Wrap)
{
    public string PlainText => string.Join("\n", Paragraphs.Select(p => p.Text));
}

/// <summary>One command of a custom geometry, in the path's own coordinates: M, L, C (3 points), Q (2 points), Z.</summary>
public sealed record PptxPathCommand(char Kind, IReadOnlyList<(double X, double Y)> Points);
public sealed record PptxPath(double Width, double Height, IReadOnlyList<PptxPathCommand> Commands, bool Filled, bool Stroked);

/// <summary>Something drawn on a slide, in points from the slide's top-left corner; <see cref="Rotation"/> in degrees, clockwise.</summary>
public abstract record PptxElement(double X, double Y, double Width, double Height, double Rotation, bool FlipH, bool FlipV);

/// <summary>A preset (rect, roundRect, ellipse, triangle, diamond, line…) or custom geometry, with a solid fill, an outline and text.</summary>
public sealed record PptxShape(double X, double Y, double Width, double Height, double Rotation, bool FlipH, bool FlipV,
    string Geometry, PptxColor? Fill, PptxColor? Line, double LineWidth, PptxText? Text, IReadOnlyList<PptxPath>? Paths = null, double Corner = 0.16667)
    : PptxElement(X, Y, Width, Height, Rotation, FlipH, FlipV);

/// <summary>A picture of the package (<see cref="Media"/> is its path in the zip); the crop is a fraction of each side.</summary>
public sealed record PptxPicture(double X, double Y, double Width, double Height, double Rotation, bool FlipH, bool FlipV, string Media,
    double CropLeft = 0, double CropTop = 0, double CropRight = 0, double CropBottom = 0)
    : PptxElement(X, Y, Width, Height, Rotation, FlipH, FlipV);

public sealed record PptxCell(int Row, int Column, int RowSpan, int ColumnSpan, PptxText Text, PptxColor? Fill);

/// <summary>A table as a grid: column widths and row heights in points, the cells that are not merged into another.</summary>
public sealed record PptxTable(double X, double Y, double Width, double Height, IReadOnlyList<double> Columns, IReadOnlyList<double> Rows, IReadOnlyList<PptxCell> Cells)
    : PptxElement(X, Y, Width, Height, 0, false, false);

/// <summary>An object the reader does not draw (a chart, SmartArt, a video without a poster, an EMF picture): its place is marked and named.</summary>
public sealed record PptxMissing(double X, double Y, double Width, double Height, double Rotation, string Label)
    : PptxElement(X, Y, Width, Height, Rotation, false, false);

/// <summary>
/// One slide, ready to draw: background, what is on it (master and layout decorations first), its text and speaker
/// notes, and what of it is not drawn here, said in words (never a silent omission).
/// </summary>
public sealed record PptxReadSlide(int Number, PptxColor Background, string? BackgroundPicture, IReadOnlyList<PptxElement> Elements, string Text, string Notes,
    IReadOnlyList<string> NotRendered, bool Hidden);

/// <summary>A match of the deck's search: the slide (from 0), whether it is in the speaker notes, and a short excerpt.</summary>
public sealed record PptxSearchHit(int Slide, bool InNotes, string Excerpt);

/// <summary>
/// CST-237: a PowerPoint deck (.pptx) read for the native reader. Slide size and order come from presentation.xml; each
/// slide is read when asked (and kept): text bodies with runs (size, bold, italic, colour, alignment, bullets), shapes
/// (preset and custom geometry, solid fill and line, theme style references), pictures (with crop), tables as a grid,
/// groups, placeholders that take position and text style from their layout and master, the master and layout
/// decorations, background and speaker notes. Everything goes through <see cref="ReaderArchive"/> (no path outside the
/// package, decompression budget) with limits on slides, shapes and nesting. What is not drawn (animations,
/// transitions, charts, SmartArt, equations, video, OLE, EMF, macros, embedded fonts) is listed, per slide or for the
/// deck. The legacy binary .ppt (and an encrypted .pptx, which is the same container) is <see cref="NotSupportedException"/>.
/// </summary>
public sealed class PptxDeck : IDisposable
{
    public const int MaxSlides = 5000;
    public const int MaxElementsPerSlide = 10000;
    public const int MaxDepth = 32;
    public const double EmuPerPoint = 12700;

    private readonly ReaderArchive _archive;
    private readonly string[] _slidePaths;
    private readonly PptxReadSlide?[] _slides;
    private readonly Dictionary<string, Part> _parts = new(StringComparer.Ordinal);
    private readonly XElement? _defaultTextStyle;
    private readonly object _gate = new();

    /// <summary>The slide size in EMU (as stored) and in points.</summary>
    public long WidthEmu { get; }
    public long HeightEmu { get; }
    public double Width => WidthEmu / EmuPerPoint;
    public double Height => HeightEmu / EmuPerPoint;
    public int SlideCount => _slidePaths.Length;
    /// <summary>"16:9", "4:3", "16:10" or "retrato"/"personalizado".</summary>
    public string AspectLabel { get; }
    /// <summary>What of the whole deck is not used here (macros, embedded fonts).</summary>
    public IReadOnlyList<string> DeckNotices { get; }

    private PptxDeck(ReaderArchive archive, long width, long height, string[] slides, XElement? defaultTextStyle, IReadOnlyList<string> notices)
    {
        _archive = archive;
        WidthEmu = width; HeightEmu = height;
        _slidePaths = slides;
        _slides = new PptxReadSlide?[slides.Length];
        _defaultTextStyle = defaultTextStyle;
        DeckNotices = notices;
        AspectLabel = Aspect(width, height);
    }

    /// <summary>A deck read by another reader (an OpenDocument presentation, <see cref="OdpSlides"/>): its slides are given whole.</summary>
    internal PptxDeck(ReaderArchive archive, long width, long height, PptxReadSlide[] slides, IReadOnlyList<string> notices)
        : this(archive, width, height, slides.Select((_, i) => "\0read:" + i).ToArray(), null, notices)
    {
        slides.CopyTo(_slides, 0);
    }

    /// <summary>An OpenDocument presentation (.odp) for the same reader.</summary>
    public static PptxDeck OpenOdp(byte[] bytes) => OdpSlides.Open(bytes);

    public static string Aspect(long width, long height)
    {
        double ratio = width / (double)Math.Max(1, height);
        if (Math.Abs(ratio - 16.0 / 9) < .02) return "16:9";
        if (Math.Abs(ratio - 4.0 / 3) < .02) return "4:3";
        if (Math.Abs(ratio - 16.0 / 10) < .02) return "16:10";
        return ratio < 1 ? "retrato" : "personalizado";
    }

    public static PptxDeck Open(byte[] bytes)
    {
        ReadOnlySpan<byte> head = bytes.AsSpan(0, Math.Min(bytes.Length, 8));
        if (head.SequenceEqual((ReadOnlySpan<byte>)[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]))
            throw new NotSupportedException("Esta apresentação está no formato binário antigo do PowerPoint (.ppt) ou protegida por senha; o Urbe não a lê. Abra em outro app ou salve como .pptx.");
        if (FileSignatures.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, 4096))) != FileSignature.Zip)
            throw new InvalidDataException("O arquivo não é uma apresentação .pptx: o conteúdo parece " + FileSignatures.Describe(FileSignatures.Detect(bytes.AsSpan(0, Math.Min(bytes.Length, 4096)))) + ".");
        var archive = new ReaderArchive(bytes);
        try
        {
            if (!archive.Contains("ppt/presentation.xml")) throw new InvalidDataException("O pacote não tem ppt/presentation.xml; não é uma apresentação do PowerPoint.");
            var presentation = archive.Xml("ppt/presentation.xml").Root ?? throw new InvalidDataException("Apresentação vazia.");
            var size = Child(presentation, "sldSz");
            long width = Emu(size?.Attribute("cx"), 12192000), height = Emu(size?.Attribute("cy"), 6858000);
            // PowerPoint keeps slides between 1 and 56 inches; anything else is a broken or hostile size
            if (width is < 914400 or > 51206400 || height is < 914400 or > 51206400) { width = 12192000; height = 6858000; }
            var rels = Relationships(archive, "ppt/presentation.xml");
            var slides = new List<string>();
            foreach (var id in Child(presentation, "sldIdLst")?.Elements().Where(e => e.Name.LocalName == "sldId") ?? [])
            {
                if (slides.Count >= MaxSlides) throw new InvalidDataException("A apresentação tem mais de " + MaxSlides + " slides; o leitor não a abre por inteiro.");
                if (RelId(id) is { } rid && rels.TryGetValue(rid, out var rel) && !rel.External && archive.Contains(rel.Target)) slides.Add(rel.Target);
            }
            if (slides.Count == 0) throw new InvalidDataException("A apresentação não tem slides.");
            var notices = new List<string>();
            if (archive.Paths.Any(p => p.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))) notices.Add("macros (nunca são executadas)");
            if (Child(presentation, "embeddedFontLst")?.Elements().Any() == true) notices.Add("fontes incorporadas (o texto usa as fontes do aparelho)");
            return new PptxDeck(archive, width, height, slides.ToArray(), Child(presentation, "defaultTextStyle"), notices);
        }
        catch { archive.Dispose(); throw; }
    }

    /// <summary>Slide <paramref name="index"/> (from 0), read once and kept.</summary>
    public PptxReadSlide Slide(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, SlideCount);
        lock (_gate)
        {
            if (_slides[index] is { } known) return known;
            try { return _slides[index] = ReadSlide(index); }
            catch (Exception ex) when (ex is System.Xml.XmlException or InvalidDataException or FormatException or OverflowException)
            {
                // one broken slide is said on its own; the rest of the deck reads
                return _slides[index] = new PptxReadSlide(index + 1, PptxColor.White, null, [], "", "", ["o slide inteiro: " + ex.Message], false);
            }
        }
    }

    /// <summary>The bytes of a picture of the package, only when they really are a picture the reader can decode; else null.</summary>
    public byte[]? Picture(string media)
    {
        byte[] bytes;
        lock (_gate)
        {
            if (!_archive.Contains(media)) return null;
            try { bytes = _archive.Bytes(media); }
            catch (InvalidDataException) { return null; }
        }
        return FileSignatures.IsImage(FileSignatures.Detect(bytes)) ? bytes : null;
    }

    /// <summary>Accent- and case-insensitive search in every slide's text and speaker notes, in slide order.</summary>
    public IReadOnlyList<PptxSearchHit> Search(string query, CancellationToken cancellationToken = default)
    {
        var hits = new List<PptxSearchHit>();
        if (string.IsNullOrWhiteSpace(query)) return hits;
        for (int i = 0; i < SlideCount && hits.Count < TextSearch.MaxMatches; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slide = Slide(i);
            foreach (var (text, notes) in new[] { (slide.Text, false), (slide.Notes, true) })
            {
                var flat = text.Replace('\n', ' ');
                foreach (int at in TextSearch.FindAll(TextSearch.Fold(flat), query, cancellationToken))
                {
                    int from = Math.Max(0, at - 30), to = Math.Min(flat.Length, at + query.Length + 50);
                    hits.Add(new PptxSearchHit(i, notes, (from > 0 ? "…" : "") + flat[from..to].Trim() + (to < flat.Length ? "…" : "")));
                    if (hits.Count >= TextSearch.MaxMatches) break;
                }
            }
        }
        return hits;
    }

    public void Dispose()
    {
        lock (_gate) _archive.Dispose();
    }

    // ---------------------------------------------------------------- package

    private sealed record Rel(string Type, string Target, bool External);

    /// <summary>A part with its relationships, and for a master its theme and colour map.</summary>
    private sealed class Part(string path, XElement root, Dictionary<string, Rel> rels)
    {
        public string Path { get; } = path;
        public XElement Root { get; } = root;
        public Dictionary<string, Rel> Rels { get; } = rels;
    }

    private Part Load(string path)
    {
        if (_parts.TryGetValue(path, out var part)) return part;
        var root = _archive.Xml(path).Root ?? throw new InvalidDataException("Parte vazia: " + path);
        return _parts[path] = new Part(path, root, Relationships(_archive, path));
    }

    private Part? Related(Part part, string typeSuffix) =>
        part.Rels.Values.FirstOrDefault(r => !r.External && r.Type.EndsWith(typeSuffix, StringComparison.Ordinal) && _archive.Contains(r.Target)) is { } rel ? Load(rel.Target) : null;

    private static Dictionary<string, Rel> Relationships(ReaderArchive archive, string path)
    {
        var relPath = path[..(path.LastIndexOf('/') + 1)] + "_rels/" + System.IO.Path.GetFileName(path) + ".rels";
        var result = new Dictionary<string, Rel>(StringComparer.Ordinal);
        if (!archive.Contains(relPath)) return result;
        foreach (var rel in archive.Xml(relPath).Descendants().Where(e => e.Name.LocalName == "Relationship"))
        {
            if ((string?)rel.Attribute("Id") is not { } id) continue;
            bool external = (string?)rel.Attribute("TargetMode") == "External";
            string target = (string?)rel.Attribute("Target") ?? "";
            if (!external)
            {
                if (target.StartsWith('/')) target = target.TrimStart('/');
                else target = ReaderArchive.Resolve(path, target) ?? "";
                if (target.Length == 0) continue;
            }
            result[id] = new Rel((string?)rel.Attribute("Type") ?? "", target, external);
        }
        return result;
    }

    // ---------------------------------------------------------------- slide

    /// <summary>What a slide is read with: its layout and master, the theme, and what is not drawn.</summary>
    private sealed class Context
    {
        public required Dictionary<string, PptxColor> Scheme { get; init; }
        public required Dictionary<string, string> ColorMap { get; init; }
        public string MajorFont { get; init; } = "";
        public string MinorFont { get; init; } = "";
        public Part? Layout { get; init; }
        public Part? Master { get; init; }
        public required Part Current { get; set; }
        public int SlideNumber { get; init; }
        public SortedSet<string> NotRendered { get; } = new(StringComparer.Ordinal);
        public List<PptxElement> Elements { get; } = [];
        public StringBuilder Text { get; } = new();
        public bool CollectText { get; set; }
    }

    private PptxReadSlide ReadSlide(int index)
    {
        var slide = Load(_slidePaths[index]);
        var layout = Related(slide, "/slideLayout");
        var master = layout is null ? null : Related(layout, "/slideMaster");
        var theme = master is null ? null : Related(master, "/theme");
        var scheme = new Dictionary<string, PptxColor>(StringComparer.Ordinal)
        {
            ["dk1"] = PptxColor.Black, ["lt1"] = PptxColor.White, ["dk2"] = new(0x44, 0x54, 0x6A), ["lt2"] = new(0xE7, 0xE6, 0xE6),
            ["accent1"] = new(0x44, 0x72, 0xC4), ["accent2"] = new(0xED, 0x7D, 0x31), ["accent3"] = new(0xA5, 0xA5, 0xA5),
            ["accent4"] = new(0xFF, 0xC0, 0x00), ["accent5"] = new(0x5B, 0x9B, 0xD5), ["accent6"] = new(0x70, 0xAD, 0x47),
            ["hlink"] = new(0x05, 0x63, 0xC1), ["folHlink"] = new(0x95, 0x4F, 0x72)
        };
        string major = "", minor = "";
        if (theme is not null)
        {
            var colors = theme.Root.Descendants().FirstOrDefault(e => e.Name.LocalName == "clrScheme");
            var empty = new Context { Scheme = scheme, ColorMap = new(), Current = theme };
            foreach (var entry in colors?.Elements() ?? [])
                if (entry.Elements().FirstOrDefault() is { } value && Color(value, empty) is { } c) scheme[entry.Name.LocalName] = c;
            var fonts = theme.Root.Descendants().FirstOrDefault(e => e.Name.LocalName == "fontScheme");
            major = Child(Child(fonts, "majorFont"), "latin")?.Attribute("typeface")?.Value ?? "";
            minor = Child(Child(fonts, "minorFont"), "latin")?.Attribute("typeface")?.Value ?? "";
        }
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["bg1"] = "lt1", ["tx1"] = "dk1", ["bg2"] = "lt2", ["tx2"] = "dk2" };
        if (Child(master?.Root, "clrMap") is { } clrMap)
            foreach (var a in clrMap.Attributes()) map[a.Name.LocalName] = a.Value;
        var ctx = new Context { Scheme = scheme, ColorMap = map, MajorFont = major, MinorFont = minor, Layout = layout, Master = master, Current = slide, SlideNumber = index + 1 };

        // background: the slide's own, or the layout's, or the master's
        PptxColor background = PptxColor.White;
        string? backgroundPicture = null;
        foreach (var part in new[] { slide, layout, master })
        {
            if (part is null || Child(Child(part.Root, "cSld"), "bg") is not { } bg) continue;
            ctx.Current = part;
            if (Child(bg, "bgPr") is { } bgPr)
            {
                var fill = Fill(bgPr, ctx, out var picture);
                if (picture is not null) backgroundPicture = picture;
                background = fill ?? PptxColor.White;
            }
            else if (Child(bg, "bgRef") is { } bgRef) background = Color(bgRef.Elements().FirstOrDefault(), ctx) ?? PptxColor.White;
            break;
        }

        bool showMaster = Attr(slide.Root, "showMasterSp") != "0";
        if (master is not null && showMaster && Attr(layout?.Root, "showMasterSp") != "0") { ctx.Current = master; Walk(Tree(master), Transform.Identity, ctx, 0); }
        if (layout is not null && showMaster) { ctx.Current = layout; Walk(Tree(layout), Transform.Identity, ctx, 0); }
        ctx.Current = slide;
        ctx.CollectText = true;
        Walk(Tree(slide), Transform.Identity, ctx, 0);

        // what moves or plays is not shown by a reader of still slides
        var all = slide.Root.Descendants().ToList();
        if (all.Any(e => e.Name.LocalName == "transition")) ctx.NotRendered.Add("transição");
        int effects = all.Count(e => e.Name.LocalName == "cTn" && e.Attribute("presetClass") is not null);
        if (effects > 0 || all.Any(e => e.Name.LocalName is "anim" or "animEffect" or "animMotion" or "set")) ctx.NotRendered.Add(effects > 1 ? "animações (" + effects + " efeitos)" : "animação");

        string notes = "";
        if (Related(slide, "/notesSlide") is { } notesPart)
            notes = string.Join("\n", Tree(notesPart)?.Descendants().Where(e => e.Name.LocalName == "sp" && Placeholder(e) is { Type: "body" })
                .Select(sp => string.Join("\n", Child(sp, "txBody")?.Elements().Where(p => p.Name.LocalName == "p").Select(PlainParagraph) ?? []).Trim())
                .Where(t => t.Length > 0) ?? []);
        return new PptxReadSlide(index + 1, background, backgroundPicture, ctx.Elements.ToArray(), ctx.Text.ToString().Trim(), notes,
            ctx.NotRendered.ToArray(), Attr(slide.Root, "show") == "0");
    }

    private static XElement? Tree(Part part) => Child(Child(part.Root, "cSld"), "spTree");

    private static string PlainParagraph(XElement p) =>
        string.Concat(p.Elements().Select(e => e.Name.LocalName switch { "r" or "fld" => Child(e, "t")?.Value ?? "", "br" => "\n", _ => "" }));

    /// <summary>The mapping from a group's child coordinates (EMU) to the slide's (EMU).</summary>
    private readonly record struct Transform(double Ox, double Oy, double Sx, double Sy, double Rotation)
    {
        public static readonly Transform Identity = new(0, 0, 1, 1, 0);
        public (double X, double Y, double W, double H) Map(double x, double y, double w, double h) => (Ox + x * Sx, Oy + y * Sy, w * Sx, h * Sy);
    }

    private void Walk(XElement? container, Transform transform, Context ctx, int depth)
    {
        if (container is null) return;
        if (depth > MaxDepth) throw new InvalidDataException("Grupos aninhados demais.");
        foreach (var element in container.Elements())
        {
            if (ctx.Elements.Count >= MaxElementsPerSlide) { ctx.NotRendered.Add("objetos além de " + MaxElementsPerSlide); return; }
            switch (element.Name.LocalName)
            {
                case "sp": Shape(element, transform, ctx); break;
                case "cxnSp": Shape(element, transform, ctx, connector: true); break;
                case "pic": Pic(element, transform, ctx); break;
                case "graphicFrame": Frame(element, transform, ctx, depth); break;
                case "grpSp":
                {
                    if (Hidden(element)) break;
                    var xfrm = Child(Child(element, "grpSpPr"), "xfrm");
                    var off = Child(xfrm, "off"); var ext = Child(xfrm, "ext"); var chOff = Child(xfrm, "chOff"); var chExt = Child(xfrm, "chExt");
                    double ox = Num(off, "x"), oy = Num(off, "y"), w = Num(ext, "cx"), h = Num(ext, "cy");
                    double cx = Num(chOff, "x"), cy = Num(chOff, "y"), cw = Num(chExt, "cx", w), ch = Num(chExt, "cy", h);
                    double sx = cw > 0 ? w / cw : 1, sy = ch > 0 ? h / ch : 1;
                    double rot = Num(xfrm, "rot") / 60000;
                    if (rot != 0) ctx.NotRendered.Add("rotação de grupo (aproximada)");
                    var (gx, gy, _, _) = transform.Map(ox - cx * sx, oy - cy * sy, 0, 0);
                    Walk(element, new Transform(gx, gy, transform.Sx * sx, transform.Sy * sy, transform.Rotation + rot), ctx, depth + 1);
                    break;
                }
                case "AlternateContent":
                {
                    var choice = element.Elements().FirstOrDefault(e => e.Name.LocalName == "Choice");
                    var fallback = element.Elements().FirstOrDefault(e => e.Name.LocalName == "Fallback");
                    bool math = choice?.Descendants().Any(e => e.Name.LocalName is "m" or "oMath" or "oMathPara") == true;
                    if (math) ctx.NotRendered.Add(fallback?.HasElements == true ? "equação (mostrada como a imagem de reserva do arquivo)" : "equação");
                    if (fallback is not null) Walk(fallback, transform, ctx, depth + 1);
                    else if (choice is not null && !math) ctx.NotRendered.Add("objeto de uma versão mais nova do PowerPoint");
                    break;
                }
                case "contentPart": ctx.NotRendered.Add("tinta digital"); break;
            }
        }
    }

    private static bool Hidden(XElement element) =>
        element.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal)) is { } nv && Child(nv, "cNvPr") is { } c && Attr(c, "hidden") is "1" or "true";

    private sealed record Ph(string Type, string? Index);

    private static Ph? Placeholder(XElement shape)
    {
        var nv = shape.Elements().FirstOrDefault(e => e.Name.LocalName.StartsWith("nv", StringComparison.Ordinal));
        var ph = Child(Child(nv, "nvPr"), "ph");
        return ph is null ? null : new Ph(Attr(ph, "type") ?? "obj", Attr(ph, "idx"));
    }

    /// <summary>The master's kind of placeholder a slide's one inherits from.</summary>
    private static string MasterType(string type) => type switch
    {
        "ctrTitle" or "title" => "title",
        "subTitle" or "obj" or "body" or "tbl" or "chart" or "dgm" or "media" or "clipArt" or "pic" => "body",
        _ => type
    };

    private static XElement? FindPlaceholder(Part? part, Ph ph, bool master)
    {
        if (part is null || Tree(part) is not { } tree) return null;
        var candidates = tree.Descendants().Where(e => e.Name.LocalName == "sp" && Placeholder(e) is not null).ToList();
        if (!master && ph.Index is { } idx && candidates.FirstOrDefault(e => Placeholder(e)!.Index == idx) is { } byIndex) return byIndex;
        if (!master && candidates.FirstOrDefault(e => Placeholder(e)!.Type == ph.Type) is { } byType) return byType;
        string wanted = MasterType(ph.Type);
        return candidates.FirstOrDefault(e => MasterType(Placeholder(e)!.Type) == wanted);
    }

    private void Shape(XElement sp, Transform transform, Context ctx, bool connector = false)
    {
        if (Hidden(sp)) return;
        var ph = Placeholder(sp);
        // a layout's or master's placeholders are prompts for the slide, never drawn themselves
        if (ph is not null && !ctx.CollectText) return;
        XElement? layoutPh = null, masterPh = null;
        if (ph is not null)
        {
            layoutPh = FindPlaceholder(ctx.Layout, ph, master: false);
            masterPh = FindPlaceholder(ctx.Master, ph, master: true);
        }
        var spPr = Child(sp, "spPr");
        var xfrm = Child(spPr, "xfrm") ?? Child(Child(layoutPh, "spPr"), "xfrm") ?? Child(Child(masterPh, "spPr"), "xfrm");
        if (xfrm is null) return;
        var box = Box(xfrm, transform);

        // fill and outline: the shape's own, or its theme style's reference
        var style = Child(sp, "style");
        PptxColor? fill = null;
        string? texture = null;
        if (spPr is not null && spPr.Elements().Any(e => e.Name.LocalName is "noFill" or "solidFill" or "gradFill" or "blipFill" or "pattFill" or "grpFill"))
            fill = Fill(spPr, ctx, out texture);
        else if (Child(style, "fillRef") is { } fillRef && Num(fillRef, "idx") > 0) fill = Color(fillRef.Elements().FirstOrDefault(), ctx);
        if (texture is not null) ctx.NotRendered.Add("preenchimento com imagem em forma");
        PptxColor? line = null;
        double lineWidth = 0;
        var ln = Child(spPr, "ln");
        if (ln is not null && Child(ln, "noFill") is not null) line = null;
        else if (Child(ln, "solidFill") is { } lineFill) line = Color(lineFill.Elements().FirstOrDefault(), ctx);
        else if (Child(ln, "gradFill") is { } lineGrad) line = Color(lineGrad.Descendants().FirstOrDefault(e => e.Name.LocalName == "gs")?.Elements().FirstOrDefault(), ctx);
        else if (Child(style, "lnRef") is { } lnRef && Num(lnRef, "idx") > 0) { line = Color(lnRef.Elements().FirstOrDefault(), ctx); lineWidth = Num(lnRef, "idx") * 6350 / EmuPerPoint; }
        if (ln?.Attribute("w") is not null) lineWidth = Num(ln, "w") / EmuPerPoint;
        else if (line is not null && lineWidth == 0) lineWidth = .75;
        if (spPr?.Elements().Any(e => e.Name.LocalName is "effectLst" && e.HasElements || e.Name.LocalName is "scene3d" or "sp3d") == true) ctx.NotRendered.Add("sombras e efeitos");

        string geometry = connector ? "line" : "rect";
        List<PptxPath>? paths = null;
        double corner = 0.16667;
        if (Child(spPr, "prstGeom") is { } preset)
        {
            geometry = Attr(preset, "prst") ?? "rect";
            if (geometry == "roundRect" && Child(preset, "avLst")?.Elements().FirstOrDefault(g => Attr(g, "name") == "adj") is { } adj &&
                Attr(adj, "fmla") is { } fmla && fmla.StartsWith("val ", StringComparison.Ordinal) &&
                double.TryParse(fmla[4..], NumberStyles.Float, CultureInfo.InvariantCulture, out double a)) corner = Math.Clamp(a / 100000, 0, .5);
        }
        else if (Child(spPr, "custGeom") is { } custom) { geometry = "custom"; paths = Paths(custom, ctx); }
        if (geometry is "straightConnector1" or "line" or "bentConnector2" or "bentConnector3" or "curvedConnector3") geometry = "line";
        else if (geometry is not ("rect" or "roundRect" or "ellipse" or "triangle" or "rtTriangle" or "diamond" or "custom" or "snipRect" or "flowChartProcess" or "flowChartAlternateProcess" or "rightArrow" or "leftArrow"))
        {
            if (fill is not null || line is not null) ctx.NotRendered.Add("forma “" + geometry + "” (desenhada como retângulo)");
            geometry = "rect";
        }

        PptxText? text = null;
        if (Child(sp, "txBody") is { } body)
        {
            var chain = new List<XElement?> { Child(body, "lstStyle"), Child(Child(layoutPh, "txBody"), "lstStyle"), Child(Child(masterPh, "txBody"), "lstStyle") };
            var txStyles = Child(ctx.Master?.Root, "txStyles");
            if (ph is not null) chain.Add(Child(txStyles, MasterType(ph.Type) == "title" ? "titleStyle" : MasterType(ph.Type) == "body" ? "bodyStyle" : "otherStyle"));
            else chain.Add(_defaultTextStyle);
            var bodies = new[] { Child(body, "bodyPr"), Child(Child(layoutPh, "txBody"), "bodyPr"), Child(Child(masterPh, "txBody"), "bodyPr") };
            PptxColor? fontColor = Child(style, "fontRef") is { } fontRef ? Color(fontRef.Elements().FirstOrDefault(), ctx) : null;
            text = Text(body, chain, bodies, fontColor, ctx, ph is not null && MasterType(ph.Type) == "title");
            // an empty placeholder shows nothing in a slide show (its prompt is the layout's)
            if (text.Paragraphs.All(p => p.Runs.All(r => r.Text.Trim().Length == 0))) text = null;
            if (text is not null && ctx.CollectText) ctx.Text.AppendLine(text.PlainText);
        }
        if (fill is null && line is null && text is null) return;
        ctx.Elements.Add(new PptxShape(box.X, box.Y, box.W, box.H, box.Rotation, box.FlipH, box.FlipV, geometry, fill, line, lineWidth, text, paths, corner));
    }

    private sealed record Placed(double X, double Y, double W, double H, double Rotation, bool FlipH, bool FlipV);

    private static Placed Box(XElement xfrm, Transform transform)
    {
        var off = Child(xfrm, "off"); var ext = Child(xfrm, "ext");
        var (x, y, w, h) = transform.Map(Num(off, "x"), Num(off, "y"), Math.Max(0, Num(ext, "cx")), Math.Max(0, Num(ext, "cy")));
        return new Placed(x / EmuPerPoint, y / EmuPerPoint, w / EmuPerPoint, h / EmuPerPoint, Num(xfrm, "rot") / 60000 + transform.Rotation,
            Attr(xfrm, "flipH") is "1" or "true", Attr(xfrm, "flipV") is "1" or "true");
    }

    private static List<PptxPath> Paths(XElement custom, Context ctx)
    {
        var result = new List<PptxPath>();
        foreach (var path in Child(custom, "pathLst")?.Elements().Where(e => e.Name.LocalName == "path") ?? [])
        {
            var commands = new List<PptxPathCommand>();
            foreach (var c in path.Elements())
            {
                var points = c.Elements().Where(e => e.Name.LocalName == "pt").Select(p => (Num(p, "x"), Num(p, "y"))).ToArray();
                char kind = c.Name.LocalName switch { "moveTo" => 'M', "lnTo" => 'L', "cubicBezTo" => 'C', "quadBezTo" => 'Q', "close" => 'Z', _ => '?' };
                if (kind == '?') { ctx.NotRendered.Add("arcos de forma livre (aproximados)"); continue; }
                if (kind is 'M' or 'L' && points.Length < 1 || kind == 'C' && points.Length < 3 || kind == 'Q' && points.Length < 2) continue;
                commands.Add(new PptxPathCommand(kind, points));
            }
            result.Add(new PptxPath(Num(path, "w"), Num(path, "h"), commands, Attr(path, "fill") != "none", Attr(path, "stroke") is not ("0" or "false")));
        }
        return result;
    }

    private void Pic(XElement pic, Transform transform, Context ctx)
    {
        if (Hidden(pic)) return;
        var spPr = Child(pic, "spPr");
        var xfrm = Child(spPr, "xfrm");
        var ph = Placeholder(pic);
        if (xfrm is null && ph is not null)
            xfrm = Child(Child(FindPlaceholder(ctx.Layout, ph, false), "spPr"), "xfrm") ?? Child(Child(FindPlaceholder(ctx.Master, ph, true), "spPr"), "xfrm");
        if (xfrm is null) return;
        var box = Box(xfrm, transform);
        var nvPr = Child(Child(pic, "nvPicPr"), "nvPr");
        bool video = nvPr?.Descendants().Any(e => e.Name.LocalName is "videoFile" or "quickTimeFile") == true;
        bool audio = nvPr?.Descendants().Any(e => e.Name.LocalName is "audioFile" or "audioCd" or "wavAudioFile") == true;
        if (!video && !audio && nvPr?.Descendants().Any(e => e.Name.LocalName == "media") == true) video = true;
        if (video) ctx.NotRendered.Add("vídeo incorporado (só a imagem de capa)");
        if (audio) ctx.NotRendered.Add("áudio incorporado");
        var blipFill = Child(pic, "blipFill");
        var blip = Child(blipFill, "blip");
        string? embed = blip?.Attributes().FirstOrDefault(a => a.Name.LocalName == "embed")?.Value;
        string? link = blip?.Attributes().FirstOrDefault(a => a.Name.LocalName == "link")?.Value;
        if (embed is null || !ctx.Current.Rels.TryGetValue(embed, out var rel) || rel.External)
        {
            ctx.NotRendered.Add(link is not null ? "imagem vinculada fora do arquivo" : "imagem ausente no pacote");
            ctx.Elements.Add(new PptxMissing(box.X, box.Y, box.W, box.H, box.Rotation, link is not null ? "Imagem vinculada" : "Imagem ausente"));
            return;
        }
        string extension = System.IO.Path.GetExtension(rel.Target).ToLowerInvariant();
        if (extension is ".emf" or ".wmf" or ".tif" or ".tiff" or ".wdp" or ".jxr")
        {
            ctx.NotRendered.Add("imagem " + extension.TrimStart('.').ToUpperInvariant());
            ctx.Elements.Add(new PptxMissing(box.X, box.Y, box.W, box.H, box.Rotation, "Imagem " + extension.TrimStart('.').ToUpperInvariant()));
            return;
        }
        var crop = Child(blipFill, "srcRect");
        ctx.Elements.Add(new PptxPicture(box.X, box.Y, box.W, box.H, box.Rotation, box.FlipH, box.FlipV, rel.Target,
            Math.Clamp(Num(crop, "l") / 100000, -1, 1), Math.Clamp(Num(crop, "t") / 100000, -1, 1), Math.Clamp(Num(crop, "r") / 100000, -1, 1), Math.Clamp(Num(crop, "b") / 100000, -1, 1)));
    }

    private void Frame(XElement frame, Transform transform, Context ctx, int depth)
    {
        if (Hidden(frame)) return;
        var xfrm = Child(frame, "xfrm");
        if (xfrm is null) return;
        var box = Box(xfrm, transform);
        var data = Child(Child(frame, "graphic"), "graphicData");
        string uri = Attr(data, "uri") ?? "";
        if (Child(data, "tbl") is { } table) { Table(table, box, ctx); return; }
        if (uri.EndsWith("/chart", StringComparison.Ordinal) || uri.Contains("chartex", StringComparison.OrdinalIgnoreCase))
        { ctx.NotRendered.Add("gráfico"); ctx.Elements.Add(new PptxMissing(box.X, box.Y, box.W, box.H, 0, "Gráfico")); return; }
        if (uri.EndsWith("/diagram", StringComparison.Ordinal))
        {
            ctx.NotRendered.Add("SmartArt");
            ctx.Elements.Add(new PptxMissing(box.X, box.Y, box.W, box.H, 0, "SmartArt"));
            if (ctx.CollectText) foreach (var t in DiagramText(data, ctx)) ctx.Text.AppendLine(t);
            return;
        }
        if (uri.EndsWith("/ole", StringComparison.Ordinal))
        {
            // an OLE object usually carries a picture of itself: drawn, and said
            var fallbackPic = data!.Descendants().FirstOrDefault(e => e.Name.LocalName == "pic");
            ctx.NotRendered.Add(fallbackPic is not null ? "objeto incorporado (OLE, mostrado como imagem)" : "objeto incorporado (OLE)");
            if (fallbackPic is not null)
            {
                int before = ctx.Elements.Count;
                Pic(fallbackPic, transform, ctx);
                if (ctx.Elements.Count > before && ctx.Elements[^1] is PptxPicture p && Child(Child(fallbackPic, "spPr"), "xfrm") is null)
                    ctx.Elements[^1] = p with { X = box.X, Y = box.Y, Width = box.W, Height = box.H };
            }
            else ctx.Elements.Add(new PptxMissing(box.X, box.Y, box.W, box.H, 0, "Objeto incorporado"));
            return;
        }
        ctx.NotRendered.Add("objeto não reconhecido");
        ctx.Elements.Add(new PptxMissing(box.X, box.Y, box.W, box.H, 0, "Objeto"));
    }

    /// <summary>The words of a SmartArt (its data part), so a search still finds the slide.</summary>
    private IEnumerable<string> DiagramText(XElement? data, Context ctx)
    {
        var relIds = Child(data, "relIds");
        var id = relIds?.Attributes().FirstOrDefault(a => a.Name.LocalName == "dm")?.Value;
        if (id is null || !ctx.Current.Rels.TryGetValue(id, out var rel) || rel.External || !_archive.Contains(rel.Target)) return [];
        return _archive.Xml(rel.Target).Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value).Where(t => t.Trim().Length > 0).ToArray();
    }

    private void Table(XElement table, Placed box, Context ctx)
    {
        var columns = Child(table, "tblGrid")?.Elements().Where(e => e.Name.LocalName == "gridCol").Select(c => Num(c, "w") / EmuPerPoint).ToList() ?? [];
        var rows = table.Elements().Where(e => e.Name.LocalName == "tr").ToList();
        if (columns.Count == 0 || rows.Count == 0) return;
        if (columns.Count * rows.Count > MaxElementsPerSlide) { ctx.NotRendered.Add("tabela grande demais"); ctx.Elements.Add(new PptxMissing(box.X, box.Y, box.W, box.H, 0, "Tabela")); return; }
        var heights = rows.Select(r => Num(r, "h") / EmuPerPoint).ToList();
        if (Child(Child(table, "tblPr"), "tableStyleId") is not null) ctx.NotRendered.Add("estilo da tabela (simplificado)");
        var cells = new List<PptxCell>();
        var chain = new List<XElement?> { _defaultTextStyle };
        for (int r = 0; r < rows.Count; r++)
        {
            int c = 0;
            foreach (var tc in rows[r].Elements().Where(e => e.Name.LocalName == "tc"))
            {
                if (c >= columns.Count) break;
                bool merged = Attr(tc, "hMerge") is "1" or "true" || Attr(tc, "vMerge") is "1" or "true";
                if (!merged)
                {
                    var tcPr = Child(tc, "tcPr");
                    var body = Child(tc, "txBody");
                    var insets = new XElement("bodyPr",
                        new XAttribute("lIns", Num(tcPr, "marL", 91440)), new XAttribute("rIns", Num(tcPr, "marR", 91440)),
                        new XAttribute("tIns", Num(tcPr, "marT", 45720)), new XAttribute("bIns", Num(tcPr, "marB", 45720)),
                        new XAttribute("anchor", Attr(tcPr, "anchor") ?? "t"));
                    var text = body is null ? new PptxText([], PptxAnchor.Top, 7.2, 3.6, 7.2, 3.6, true)
                        : Text(body, [Child(body, "lstStyle"), .. chain], [insets], null, ctx, false);
                    if (ctx.CollectText) ctx.Text.AppendLine(text.PlainText);
                    PptxColor? fill = tcPr is not null && tcPr.Elements().Any(e => e.Name.LocalName is "solidFill" or "noFill" or "gradFill") ? Fill(tcPr, ctx, out _) : null;
                    cells.Add(new PptxCell(r, c, Math.Max(1, (int)Num(tc, "rowSpan", 1)), Math.Max(1, (int)Num(tc, "gridSpan", 1)), text, fill));
                }
                c++;
            }
        }
        ctx.Elements.Add(new PptxTable(box.X, box.Y, Math.Max(box.W, columns.Sum()), Math.Max(box.H, heights.Sum()), columns, heights, cells));
    }

    // ---------------------------------------------------------------- text

    private PptxText Text(XElement body, IReadOnlyList<XElement?> chain, IReadOnlyList<XElement?> bodies, PptxColor? fontColor, Context ctx, bool title)
    {
        string? BodyAttr(string name) => bodies.Select(b => Attr(b, name)).FirstOrDefault(v => v is not null);
        double Inset(string name, double fallback) => BodyAttr(name) is { } v && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && double.IsFinite(n) ? n / EmuPerPoint : fallback;
        var anchor = BodyAttr("anchor") switch { "ctr" => PptxAnchor.Middle, "b" => PptxAnchor.Bottom, _ => PptxAnchor.Top };
        string? vert = BodyAttr("vert");
        if (vert is not null and not "horz") ctx.NotRendered.Add("texto vertical");
        if (BodyAttr("numCol") is { } cols && cols != "1") ctx.NotRendered.Add("colunas de texto");
        var autofit = bodies.Select(b => Child(b, "normAutofit")).FirstOrDefault(a => a is not null);
        double fontScale = autofit is not null ? Num(autofit, "fontScale", 100000) / 100000 : 1;
        double spacingReduction = autofit is not null ? Num(autofit, "lnSpcReduction") / 100000 : 0;
        fontScale = Math.Clamp(fontScale, .05, 1);
        var paragraphs = new List<PptxParagraph>();
        var numbers = new Dictionary<int, int>();
        foreach (var p in body.Elements().Where(e => e.Name.LocalName == "p"))
        {
            var pPr = Child(p, "pPr");
            int level = Math.Clamp((int)Num(pPr, "lvl"), 0, 8);
            var levels = chain.Select(s => Child(s, "lvl" + (level + 1) + "pPr")).ToList();
            var props = new List<XElement?> { pPr };
            props.AddRange(levels);
            string? P(string name) => props.Select(x => Attr(x, name)).FirstOrDefault(v => v is not null);
            XElement? PChild(string name) => props.Select(x => Child(x, name)).FirstOrDefault(v => v is not null);
            var defaults = props.Select(x => Child(x, "defRPr")).ToList();
            var align = P("algn") switch { "ctr" => PptxTextAlign.Center, "r" => PptxTextAlign.Right, "just" or "dist" => PptxTextAlign.Justify, _ => PptxTextAlign.Left };
            double marL = (P("marL") is { } m ? Parse(m) : 0) / EmuPerPoint, indent = (P("indent") is { } ind ? Parse(ind) : 0) / EmuPerPoint;
            var runs = new List<PptxRun>();
            foreach (var r in p.Elements())
            {
                string name = r.Name.LocalName;
                if (name == "br") { runs.Add(Run("", Child(r, "rPr"), defaults, fontColor, ctx, fontScale, title) with { LineBreak = true }); continue; }
                if (name is not ("r" or "fld")) continue;
                string value = Child(r, "t")?.Value ?? "";
                if (name == "fld" && Attr(r, "type") == "slidenum") value = ctx.SlideNumber.ToString(CultureInfo.InvariantCulture);
                if (Child(r, "rPr")?.Descendants().Any(e => e.Name.LocalName is "gradFill") == true) ctx.NotRendered.Add("texto em degradê (mostrado com uma cor)");
                runs.Add(Run(value, Child(r, "rPr"), defaults, fontColor, ctx, fontScale, title));
            }
            var end = Run("", Child(p, "endParaRPr"), defaults, fontColor, ctx, fontScale, title);
            // bullets: the first level in the chain that says anything about them decides
            string? bullet = null;
            PptxColor? bulletColor = null;
            bool hasText = runs.Any(r => r.Text.Length > 0);
            foreach (var source in props)
            {
                if (source is null) continue;
                if (Child(source, "buNone") is not null) break;
                if (Child(source, "buChar") is { } ch) { bullet = Attr(ch, "char") is { Length: > 0 } c ? Bullet(c) : "•"; break; }
                if (Child(source, "buAutoNum") is { } auto)
                {
                    int start = (int)Num(auto, "startAt", 1);
                    int n = numbers.TryGetValue(level, out var k) ? k + 1 : start;
                    if (hasText) numbers[level] = n;
                    bullet = AutoNumber(Attr(auto, "type") ?? "arabicPeriod", n);
                    break;
                }
            }
            if (!hasText) bullet = null;
            foreach (var deeper in numbers.Keys.Where(l => l > level).ToArray()) numbers.Remove(deeper);
            if (PChild("buClr")?.Elements().FirstOrDefault() is { } bc) bulletColor = Color(bc, ctx);
            double SpacePts(string name)
            {
                var s = PChild(name);
                if (Child(s, "spcPts") is { } pts) return Num(pts, "val") / 100;
                if (Child(s, "spcPct") is { } pct) return Num(pct, "val") / 100000 * (runs.FirstOrDefault()?.Size ?? end.Size) * 1.2;
                return 0;
            }
            double lineSpacing = 1;
            double? linePoints = null;
            if (PChild("lnSpc") is { } ln)
            {
                if (Child(ln, "spcPct") is { } pct) lineSpacing = Num(pct, "val", 100000) / 100000;
                else if (Child(ln, "spcPts") is { } pts) linePoints = Num(pts, "val") / 100;
            }
            lineSpacing = Math.Max(.1, lineSpacing * (1 - spacingReduction));
            paragraphs.Add(new PptxParagraph(runs, align, marL, indent, bullet, bulletColor, SpacePts("spcBef"), SpacePts("spcAft"), lineSpacing, linePoints, end.Size));
        }
        return new PptxText(paragraphs, anchor, Inset("lIns", 7.2), Inset("tIns", 3.6), Inset("rIns", 7.2), Inset("bIns", 3.6), BodyAttr("wrap") != "none");
    }

    /// <summary>Symbol-font bullets (Wingdings "§", "Ø") are not in the device's fonts: the common ones are drawn as the shape they stand for.</summary>
    private static string Bullet(string c) => c switch { "§" => "■", "Ø" => "➢", "ü" => "✓", "q" => "❑", "v" => "❖", "Ÿ" => "•", "l" => "●", "n" => "■", "o" => "□", "w" => "◆", _ => c };

    private static string AutoNumber(string type, int n)
    {
        string number = type.StartsWith("alphaLc", StringComparison.Ordinal) ? Letters(n).ToLowerInvariant()
            : type.StartsWith("alphaUc", StringComparison.Ordinal) ? Letters(n)
            : type.StartsWith("romanLc", StringComparison.Ordinal) ? Roman(n).ToLowerInvariant()
            : type.StartsWith("romanUc", StringComparison.Ordinal) ? Roman(n)
            : n.ToString(CultureInfo.InvariantCulture);
        return type.EndsWith("ParenBoth", StringComparison.Ordinal) ? "(" + number + ")" : type.EndsWith("ParenR", StringComparison.Ordinal) ? number + ")"
            : type.EndsWith("Plain", StringComparison.Ordinal) ? number : number + ".";
    }

    private static string Letters(int n)
    {
        var s = "";
        for (n = Math.Max(1, n); n > 0; n = (n - 1) / 26) s = (char)('A' + (n - 1) % 26) + s;
        return s;
    }

    private static string Roman(int n)
    {
        if (n is < 1 or > 3999) return n.ToString(CultureInfo.InvariantCulture);
        (int, string)[] map = [(1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")];
        var s = new StringBuilder();
        foreach (var (v, r) in map) while (n >= v) { s.Append(r); n -= v; }
        return s.ToString();
    }

    private PptxRun Run(string text, XElement? rPr, IReadOnlyList<XElement?> defaults, PptxColor? fontColor, Context ctx, double fontScale, bool title)
    {
        var sources = new List<XElement?> { rPr };
        sources.AddRange(defaults);
        string? A(string name) => sources.Select(x => Attr(x, name)).FirstOrDefault(v => v is not null);
        double size = (A("sz") is { } sz ? Parse(sz) / 100 : 18) * fontScale;
        if (!double.IsFinite(size) || size <= 0) size = 18;
        size = Math.Clamp(size, 1, 4000);
        PptxColor color = fontColor ?? Resolve("tx1", ctx);
        foreach (var s in sources)
        {
            if (Child(s, "solidFill") is { } fill && Color(fill.Elements().FirstOrDefault(), ctx) is { } c) { color = c; break; }
            if (Child(s, "gradFill") is { } grad && Color(grad.Descendants().FirstOrDefault(e => e.Name.LocalName == "gs")?.Elements().FirstOrDefault(), ctx) is { } g) { color = g; break; }
            if (Child(s, "noFill") is not null) { color = color with { A = 0 }; break; }
        }
        string? font = sources.Select(x => Attr(Child(x, "latin"), "typeface")).FirstOrDefault(v => v is not null);
        font = font switch { "+mj-lt" => ctx.MajorFont, "+mn-lt" => ctx.MinorFont, null => title ? ctx.MajorFont : ctx.MinorFont, _ => font };
        if (string.IsNullOrWhiteSpace(font)) font = null;
        string? caps = A("cap");
        if (caps == "all") text = text.ToUpperInvariant();
        return new PptxRun(text, size, A("b") is "1" or "true", A("i") is "1" or "true", A("u") is { } u && u != "none", A("strike") is { } st && st != "noStrike", color, font);
    }

    // ---------------------------------------------------------------- colour

    /// <summary>A solid colour of a fill (gradient: its first stop; picture: <paramref name="picture"/>); null for no fill.</summary>
    private PptxColor? Fill(XElement owner, Context ctx, out string? picture)
    {
        picture = null;
        foreach (var e in owner.Elements())
        {
            switch (e.Name.LocalName)
            {
                case "noFill": return null;
                case "solidFill": return Color(e.Elements().FirstOrDefault(), ctx);
                case "gradFill":
                    ctx.NotRendered.Add("degradê (mostrado com uma cor)");
                    var stops = e.Descendants().Where(s => s.Name.LocalName == "gs").Select(s => Color(s.Elements().FirstOrDefault(), ctx)).Where(c => c is not null).Select(c => c!.Value).ToList();
                    if (stops.Count == 0) return null;
                    return new PptxColor((byte)stops.Average(c => c.R), (byte)stops.Average(c => c.G), (byte)stops.Average(c => c.B), (byte)stops.Average(c => c.A));
                case "pattFill":
                    ctx.NotRendered.Add("preenchimento com padrão (mostrado com uma cor)");
                    return Color(Child(e, "fgClr")?.Elements().FirstOrDefault(), ctx);
                case "blipFill":
                    var embed = Child(e, "blip")?.Attributes().FirstOrDefault(a => a.Name.LocalName == "embed")?.Value;
                    if (embed is not null && ctx.Current.Rels.TryGetValue(embed, out var rel) && !rel.External) picture = rel.Target;
                    return PptxColor.White;
            }
        }
        return null;
    }

    private static PptxColor Resolve(string name, Context ctx)
    {
        if (ctx.ColorMap.TryGetValue(name, out var mapped)) name = mapped;
        return ctx.Scheme.TryGetValue(name, out var c) ? c : PptxColor.Black;
    }

    private static PptxColor? Color(XElement? element, Context ctx)
    {
        if (element is null) return null;
        PptxColor? baseColor = element.Name.LocalName switch
        {
            "srgbClr" => Hex(Attr(element, "val")),
            "sysClr" => Hex(Attr(element, "lastClr")) ?? (Attr(element, "val") == "window" ? PptxColor.White : PptxColor.Black),
            "schemeClr" => Attr(element, "val") is { } v ? v == "phClr" ? PptxColor.Black : Resolve(v, ctx) : null,
            "prstClr" => Preset(Attr(element, "val")),
            "scrgbClr" => new PptxColor(Pct(element, "r"), Pct(element, "g"), Pct(element, "b")),
            "hslClr" => FromHsl(Num(element, "hue") / 60000 / 360, Num(element, "sat") / 100000, Num(element, "lum") / 100000),
            _ => null
        };
        if (baseColor is not { } color) return null;
        foreach (var m in element.Elements())
        {
            double v = Num(m, "val") / 100000;
            switch (m.Name.LocalName)
            {
                case "alpha": color = color with { A = (byte)Math.Clamp(Math.Round(v * 255), 0, 255) }; break;
                case "tint": color = Mix(color, PptxColor.White, 1 - v); break;
                case "shade": color = Mix(color, PptxColor.Black, 1 - v); break;
                case "lumMod" or "lumOff":
                {
                    var (h, s, l) = ToHsl(color);
                    l = m.Name.LocalName == "lumMod" ? l * v : l + v;
                    color = FromHsl(h, s, Math.Clamp(l, 0, 1)) with { A = color.A };
                    break;
                }
            }
        }
        return color;
    }

    private static byte Pct(XElement e, string name) => (byte)Math.Clamp(Math.Round(Math.Pow(Math.Clamp(Num(e, name) / 100000, 0, 1), 1 / 2.2) * 255), 0, 255);

    private static PptxColor Mix(PptxColor a, PptxColor b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new((byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t), a.A);
    }

    private static (double H, double S, double L) ToHsl(PptxColor c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), l = (max + min) / 2;
        if (max == min) return (0, 0, l);
        double d = max - min, s = l > .5 ? d / (2 - max - min) : d / (max + min);
        double h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h / 6, s, l);
    }

    private static PptxColor FromHsl(double h, double s, double l)
    {
        if (s <= 0) { byte v = (byte)Math.Round(Math.Clamp(l, 0, 1) * 255); return new(v, v, v); }
        double q = l < .5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
        double Hue(double t) { t = t < 0 ? t + 1 : t > 1 ? t - 1 : t; return t < 1.0 / 6 ? p + (q - p) * 6 * t : t < .5 ? q : t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6 : p; }
        byte B(double x) => (byte)Math.Round(Math.Clamp(x, 0, 1) * 255);
        return new(B(Hue(h + 1.0 / 3)), B(Hue(h)), B(Hue(h - 1.0 / 3)));
    }

    private static PptxColor? Hex(string? value) =>
        value is { Length: 6 } && int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)
            ? new PptxColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb) : null;

    private static PptxColor? Preset(string? name) => name switch
    {
        "black" => PptxColor.Black, "white" => PptxColor.White, "red" => new(255, 0, 0), "green" => new(0, 128, 0), "blue" => new(0, 0, 255),
        "yellow" => new(255, 255, 0), "gray" or "grey" => new(128, 128, 128), "darkGray" => new(169, 169, 169), "lightGray" => new(211, 211, 211),
        "orange" => new(255, 165, 0), "purple" => new(128, 0, 128), "cyan" => new(0, 255, 255), "magenta" => new(255, 0, 255), "navy" => new(0, 0, 128),
        _ => null
    };

    // ---------------------------------------------------------------- xml helpers

    private static XElement? Child(XElement? e, string name) => e?.Elements().FirstOrDefault(c => c.Name.LocalName == name);
    private static string? Attr(XElement? e, string name) => e?.Attributes().FirstOrDefault(a => a.Name.LocalName == name && a.Name.NamespaceName.Length == 0)?.Value;
    private static string? RelId(XElement e) => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "id" && a.Name.NamespaceName.Contains("relationships", StringComparison.Ordinal))?.Value;
    private static double Parse(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v) && Math.Abs(v) <= 1e12 ? v : 0;
    private static double Num(XElement? e, string name, double fallback = 0) => Attr(e, name) is { } v ? Parse(v) : fallback;
    private static long Emu(XAttribute? attribute, long fallback) => long.TryParse(attribute?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : fallback;
}
