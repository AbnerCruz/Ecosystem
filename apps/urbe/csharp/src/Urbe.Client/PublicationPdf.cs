using SkiaSharp;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-164: a real PDF file of a publication — not the print dialog. Pages are composed here from the same semantic
/// reading as the office files (<see cref="PublicationOffice"/>): A4 pages with margins, a cover and a table of
/// contents with page numbers for books, chapters on new pages, numbered footers, headings kept with what follows,
/// no single orphan or widow line, lists, quotes, boxes, code, tables that continue (header repeated) on the next page,
/// pictures, graphs and formulas as vectors, and clickable links. SkiaSharp writes the PDF (vector text and shapes).
/// </summary>
public static class PublicationPdf
{
    private const float PageW = 595.28f, PageH = 841.89f, MarginX = 62, MarginTop = 64, MarginBottom = 74;
    private const float ContentW = PageW - 2 * MarginX;
    private static readonly SKColor Ink = new(0x25, 0x27, 0x2c), Accent = new(0x6b, 0x4f, 0x2a), Muted = new(0x66, 0x66, 0x66), Link = new(0x05, 0x63, 0xc1);

    public static async Task<PublicationExport> ExportAsync(PublicationProject project, PublicationSourceCatalog catalog,
        Func<string, Task<byte[]?>>? readAsset = null, Func<string, string?>? data = null, CancellationToken cancellationToken = default) =>
        Write(await PublicationOffice.ReadAsync(project, catalog, readAsset, cancellationToken, rasterize: null, data: data));

    public static PublicationExport Write(OfficeDocument doc)
    {
        var limitations = new SortedSet<string>(doc.Limitations.Where(l => !l.Contains("TeX") && !l.Contains("SVG")), StringComparer.Ordinal);
        var book = doc.Layout == "book";
        var fonts = new Fonts(book);
        // first pass: chapters laid out to know where each starts; the contents pages come before them
        var tocPages = book && doc.Chapters.Count > 1 ? (int)Math.Ceiling(doc.Chapters.Count / 30.0) : 0;
        var firstContentPage = book ? 1 + tocPages : 0;
        var layout = new Layout(fonts, firstContentPage, limitations);
        if (!book) layout.Title(doc.Title);
        var chapterPages = new List<int>();
        for (int c = 0; c < doc.Chapters.Count; c++)
        {
            if (book && c > 0) layout.NewPage();
            chapterPages.Add(layout.PageNumber);
            foreach (var block in PublicationOffice.Flatten(doc.Chapters[c].Blocks)) layout.Block(block);
        }

        using var stream = new MemoryStream();
        using (var pdf = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata { Title = doc.Title, Creator = "Urbe", Producer = "Urbe", RasterDpi = 300 }))
        {
            if (book)
            {
                var cover = pdf.BeginPage(PageW, PageH);
                Cover(cover, fonts, doc.Title);
                pdf.EndPage();
                for (int t = 0; t < tocPages; t++)
                {
                    var canvas = pdf.BeginPage(PageW, PageH);
                    Contents(canvas, fonts, doc, chapterPages, t);
                    Footer(canvas, fonts, 2 + t);
                    pdf.EndPage();
                }
            }
            for (int p = 0; p < layout.Pages.Count; p++)
            {
                var canvas = pdf.BeginPage(PageW, PageH);
                foreach (var draw in layout.Pages[p]) draw(canvas);
                Footer(canvas, fonts, firstContentPage + p + 1);
                pdf.EndPage();
            }
            pdf.Close();
        }
        return new PublicationExport(stream.ToArray(), limitations.ToArray());
    }

    private static void Cover(SKCanvas canvas, Fonts fonts, string title)
    {
        using var font = fonts.Get(30, bold: true);
        var lines = Wrap(title, font, ContentW);
        float y = PageH * .38f;
        foreach (var line in lines)
        {
            using var paint = new SKPaint { Color = Accent, IsAntialias = true };
            canvas.DrawText(line, PageW / 2 - font.MeasureText(line) / 2, y, font, paint);
            y += 40;
        }
    }

    private static void Contents(SKCanvas canvas, Fonts fonts, OfficeDocument doc, List<int> pages, int sheet)
    {
        float y = MarginTop;
        using var head = fonts.Get(20, bold: true);
        using var ink = new SKPaint { Color = Accent, IsAntialias = true };
        if (sheet == 0) { canvas.DrawText("Sumário", MarginX, y + 20, head, ink); y += 50; }
        using var font = fonts.Get(11.5f);
        using var text = new SKPaint { Color = Ink, IsAntialias = true };
        using var dots = new SKPaint { Color = Muted, IsAntialias = true };
        foreach (var (chapter, i) in doc.Chapters.Select((c, i) => (c, i)).Skip(sheet * 30).Take(30))
        {
            var number = (pages[i] + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var title = Fit(Title(chapter), font, ContentW - 60);
            canvas.DrawText(title, MarginX, y, font, text);
            var nw = font.MeasureText(number);
            canvas.DrawText(number, PageW - MarginX - nw, y, font, text);
            var from = MarginX + font.MeasureText(title) + 6;
            for (var x = from; x < PageW - MarginX - nw - 8; x += 5) canvas.DrawText(".", x, y, font, dots);
            y += 22;
        }
    }

    /// <summary>A chapter's title: its first heading, else the note's name.</summary>
    private static string Title(OfficeChapter chapter) =>
        chapter.Blocks.OfType<OfficeHeading>().FirstOrDefault() is { } h ? string.Concat(h.Runs.Select(r => r.Text)).Trim() : chapter.Title;

    private static void Footer(SKCanvas canvas, Fonts fonts, int number)
    {
        using var font = fonts.Get(9);
        using var paint = new SKPaint { Color = Muted, IsAntialias = true };
        var text = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        canvas.DrawText(text, PageW / 2 - font.MeasureText(text) / 2, PageH - 36, font, paint);
    }

    private static List<string> Wrap(string text, SKFont font, float width)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && font.MeasureText(candidate) > width) { lines.Add(line); line = word; }
            else line = candidate;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    private static string Fit(string text, SKFont font, float width)
    {
        if (font.MeasureText(text) <= width) return text;
        while (text.Length > 1 && font.MeasureText(text + "…") > width) text = text[..^1];
        return text + "…";
    }

    /// <summary>The typefaces, with a fallback found per word for glyphs the main one lacks (☑, arrows, Greek…).</summary>
    private sealed class Fonts(bool serif)
    {
        private readonly Dictionary<(bool, bool, bool), SKTypeface> _faces = [];

        public SKTypeface Face(bool bold = false, bool italic = false, bool mono = false)
        {
            if (_faces.TryGetValue((bold, italic, mono), out var f)) return f;
            var family = mono ? "monospace" : serif ? "serif" : "sans-serif";
            f = SKTypeface.FromFamilyName(family, bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal,
                italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright) ?? SKTypeface.Default;
            return _faces[(bold, italic, mono)] = f;
        }

        public SKFont Get(float size, bool bold = false, bool italic = false, bool mono = false) => new(Face(bold, italic, mono), size);

        /// <summary>The face to draw <paramref name="text"/> with: the asked one, or one that has its glyphs.</summary>
        public SKTypeface For(string text, SKTypeface asked)
        {
            if (asked.ContainsGlyphs(text)) return asked;
            foreach (var rune in text.EnumerateRunes())
                if (!asked.ContainsGlyph(rune.Value) && SKFontManager.Default.MatchCharacter(rune.Value) is { } other) return other;
            return asked;
        }
    }

    /// <summary>A piece of a line: a word (or a space) in one style, or an inline drawing (a formula).</summary>
    private sealed record Piece(string Text, SKFont Font, SKColor Color, float Width, string? Href = null, string? Svg = null, float Height = 0, bool Space = false, bool Strike = false, SKColor? Back = null);

    private sealed class Layout(Fonts fonts, int firstPage, ISet<string> limitations)
    {
        public List<List<Action<SKCanvas>>> Pages { get; } = [[]];
        private float _y = MarginTop;
        private bool _pageHasContent;
        public int PageNumber => firstPage + Pages.Count - 1;
        private List<Action<SKCanvas>> Page => Pages[^1];
        private float Bottom => PageH - MarginBottom;

        public void NewPage()
        {
            if (!_pageHasContent && Pages.Count > 0 && Page.Count == 0) return;
            Pages.Add([]);
            _y = MarginTop;
            _pageHasContent = false;
        }

        private void Need(float height)
        {
            if (_y + height > Bottom && _pageHasContent) NewPage();
        }

        public void Title(string title)
        {
            using var font = fonts.Get(24, bold: true);
            foreach (var line in Wrap(title, font, ContentW))
            {
                var y = _y + 26;
                var l = line;
                Page.Add(c => { using var p = new SKPaint { Color = Accent, IsAntialias = true }; c.DrawText(l, MarginX, y, fonts.Get(24, bold: true), p); });
                _y += 32;
            }
            _y += 14;
            _pageHasContent = true;
        }

        public void Block(OfficeBlock block)
        {
            switch (block)
            {
                case OfficeHeading h:
                    var size = h.Level switch { 1 => 20f, 2 => 16f, 3 => 13.5f, _ => 12f };
                    // a heading never ends a page: it keeps two lines of what follows with it
                    var headLines = Lines(h.Runs.Select(r => r with { Bold = true }).ToList(), size, ContentW, Accent);
                    Need(headLines.Sum(l => l.Height) + 2 * 16 + 18);
                    _y += _pageHasContent ? size * .9f : 0;
                    Flow(headLines, MarginX, keepTogether: true);
                    _y += 6;
                    break;
                case OfficeParagraph p:
                    Paragraph(p);
                    break;
                case OfficeListItem li:
                    var indent = 18f * (li.Level + 1);
                    var marker = li.Ordered ? NextNumber(li) + "." : li.Level % 2 == 0 ? "•" : "◦";
                    var lines = Lines(li.Runs.ToList(), 11, ContentW - indent, Ink);
                    if (lines.Count == 0) break;
                    Need(lines[0].Height);
                    var my = _y + lines[0].Ascent;
                    var mx = MarginX + indent - 14;
                    if (li.Checked is { } done)
                        // a task's box is drawn, not a glyph (not every font has ☐/☑)
                        Page.Add(c =>
                        {
                            using var stroke = new SKPaint { Color = Ink, Style = SKPaintStyle.Stroke, StrokeWidth = .9f, IsAntialias = true };
                            var box = SKRect.Create(mx - 1, my - 8.5f, 9, 9);
                            c.DrawRoundRect(box, 1.5f, 1.5f, stroke);
                            if (!done) return;
                            using var tick = new SKPath();
                            tick.MoveTo(box.Left + 2, box.MidY);
                            tick.LineTo(box.Left + 3.8f, box.Bottom - 2);
                            tick.LineTo(box.Right - 1.8f, box.Top + 2);
                            stroke.StrokeWidth = 1.4f;
                            c.DrawPath(tick, stroke);
                        });
                    else Page.Add(c => DrawPiece(c, new Piece(marker, fonts.Get(11), Ink, 0), mx, my));
                    Flow(lines, MarginX + indent);
                    _y += 3;
                    break;
                case OfficeCode code:
                    Code(code);
                    break;
                case OfficeTable table:
                    Table(table);
                    break;
                case OfficeImage image:
                    Image(image);
                    break;
                case OfficeRule:
                    Need(16);
                    var ry = _y + 8;
                    Page.Add(c => { using var p = new SKPaint { Color = new SKColor(0x99, 0x99, 0x99), StrokeWidth = .7f }; c.DrawLine(MarginX, ry, PageW - MarginX, ry, p); });
                    _y += 16;
                    _pageHasContent = true;
                    break;
            }
            if (block is not OfficeListItem) _lastList = null;
        }

        private int? _lastList;
        private int _counter;
        private string NextNumber(OfficeListItem li)
        {
            if (_lastList != li.ListId) { _lastList = li.ListId; _counter = 0; }
            return (++_counter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void Paragraph(OfficeParagraph p)
        {
            if (p.Style == "math" && p.Runs.Count == 1 && p.Runs[0].Math && MathRenderer.RenderSvg(p.Runs[0].Text, true, 18) is { Success: true } m && SvgRaster.SizeOf(m.Svg!) is { } sz)
            {
                // a display formula, drawn (vector), centred
                float w = (float)sz.Width * .78f, h = (float)sz.Height * .78f;
                if (w > ContentW) { h *= ContentW / w; w = ContentW; }
                Need(h + 12);
                var rect = SKRect.Create(MarginX + (ContentW - w) / 2, _y + 6, w, h);
                var svg = m.Svg!;
                Page.Add(c => SvgRaster.DrawInto(c, svg, rect));
                _y += h + 14;
                _pageHasContent = true;
                return;
            }
            var quote = p.Style is "quote" or "box" or "boxtitle";
            var color = p.Style == "quote" ? Muted : p.Style == "boxtitle" ? Accent : Ink;
            var runs = p.Style == "quote" ? p.Runs.Select(r => r with { Italic = true }).ToList() : p.Runs.ToList();
            var x = MarginX + (quote ? 16 : 0);
            var lines = Lines(runs, 11, ContentW - (quote ? 16 : 0), color);
            if (lines.Count == 0) return;
            Flow(lines, x, back: p.Style is "box" or "boxtitle" ? new SKColor(0xf6, 0xf3, 0xee) : null, bar: quote ? (p.Style == "quote" ? new SKColor(0x99, 0x99, 0x99) : new SKColor(0x9c, 0x7b, 0x45)) : null);
            _y += p.Style is "box" or "boxtitle" ? 2 : 7;
        }

        private void Code(OfficeCode code)
        {
            using var probe = fonts.Get(9.2f, mono: true);
            var lineH = 13f;
            var rows = new List<string>();
            foreach (var raw in code.Text.Replace("\r", "").Split('\n'))
            {
                var line = raw.Replace("\t", "    ");
                // long lines wrap by character, never run off the page
                while (probe.MeasureText(line) > ContentW - 16 && line.Length > 1)
                {
                    int cut = line.Length;
                    while (cut > 1 && probe.MeasureText(line[..cut]) > ContentW - 16) cut--;
                    rows.Add(line[..cut]);
                    line = line[cut..];
                }
                rows.Add(line);
            }
            int i = 0;
            while (i < rows.Count)
            {
                Need(lineH * Math.Min(2, rows.Count - i) + 10);
                int fit = Math.Max(1, (int)((Bottom - _y - 10) / lineH));
                var chunk = rows.Skip(i).Take(fit).ToList();
                var top = _y;
                var height = chunk.Count * lineH + 10;
                Page.Add(c =>
                {
                    using var bg = new SKPaint { Color = new SKColor(0xf3, 0xf1, 0xec) };
                    c.DrawRoundRect(SKRect.Create(MarginX, top, ContentW, height), 4, 4, bg);
                    using var font = fonts.Get(9.2f, mono: true);
                    using var ink = new SKPaint { Color = Ink, IsAntialias = true };
                    for (int k = 0; k < chunk.Count; k++) c.DrawText(chunk[k], MarginX + 8, top + 5 + lineH * (k + 1) - 3.5f, font, ink);
                });
                _y += height + 6;
                _pageHasContent = true;
                i += chunk.Count;
                if (i < rows.Count) NewPage();
            }
        }

        private void Table(OfficeTable table)
        {
            int cols = table.Rows.Max(r => r.Count);
            // columns share the width by the length of what they hold (a long text column gets more room)
            var weights = Enumerable.Range(0, cols).Select(c => Math.Clamp(table.Rows.Max(r => c < r.Count ? string.Concat(r[c].Select(x => x.Text)).Length : 0), 3, 40)).ToArray();
            // every column at least as wide as its longest word (bold in the header), the rest shared by weight
            using var measure = fonts.Get(9.8f, bold: true);
            var minimum = Enumerable.Range(0, cols).Select(c => table.Rows.Max(r => c < r.Count
                ? string.Concat(r[c].Select(x => x.Text)).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => measure.MeasureText(w)).DefaultIfEmpty(0).Max() + 12 : 12)).ToArray();
            var spare = Math.Max(0, ContentW - minimum.Sum());
            var widths = minimum.Select((m, c) => minimum.Sum() >= ContentW ? ContentW * m / minimum.Sum() : m + spare * weights[c] / (float)weights.Sum()).ToArray();
            var laid = table.Rows.Select((row, r) => Enumerable.Range(0, cols).Select(c =>
                Lines(c < row.Count ? (r == 0 && table.HeaderRow ? row[c].Select(x => x with { Bold = true }).ToList() : row[c].ToList()) : [], 9.8f, widths[c] - 10, Ink)).ToList()).ToList();
            float RowHeight(int r) => Math.Max(16, laid[r].Max(cell => cell.Sum(l => l.Height)) + 8);
            void DrawRow(int r)
            {
                var top = _y;
                var height = RowHeight(r);
                var header = r == 0 && table.HeaderRow;
                Page.Add(c =>
                {
                    float x = MarginX;
                    using var line = new SKPaint { Color = new SKColor(0x99, 0x99, 0x99), Style = SKPaintStyle.Stroke, StrokeWidth = .6f };
                    using var head = new SKPaint { Color = new SKColor(0xef, 0xe8, 0xdc) };
                    for (int k = 0; k < cols; k++)
                    {
                        var rect = SKRect.Create(x, top, widths[k], height);
                        if (header) c.DrawRect(rect, head);
                        c.DrawRect(rect, line);
                        x += widths[k];
                    }
                });
                float cx = MarginX;
                for (int k = 0; k < cols; k++)
                {
                    float cy = top + 4;
                    foreach (var l in laid[r][k]) { DrawLine(l, cx + 5, cy); cy += l.Height; }
                    cx += widths[k];
                }
                _y += height;
                _pageHasContent = true;
            }
            for (int r = 0; r < laid.Count; r++)
            {
                if (_y + RowHeight(r) > Bottom && _pageHasContent)
                {
                    NewPage();
                    if (table.HeaderRow && r > 0) DrawRow(0); // the header again on the new page
                }
                DrawRow(r);
            }
            _y += 10;
        }

        private void Image(OfficeImage image)
        {
            float w = Math.Min(ContentW, image.Width * .75f), h = image.Height * .75f * (w / (image.Width * .75f));
            var maxH = (PageH - MarginTop - MarginBottom) * .6f;
            if (h > maxH) { w *= maxH / h; h = maxH; }
            Need(h + 10);
            var rect = SKRect.Create(MarginX + (ContentW - w) / 2, _y + 4, w, h);
            if (image.Svg is { } svg)
                Page.Add(c => SvgRaster.DrawInto(c, svg, rect));
            else
            {
                var bytes = image.Bytes;
                Page.Add(c => { using var img = SKImage.FromEncodedData(bytes); if (img is not null) c.DrawImage(img, rect, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)); });
            }
            _y += h + 12;
            _pageHasContent = true;
        }

        // ------------------------------------------------------------------ text

        private sealed record Line(List<Piece> Pieces, float Height, float Ascent);

        private List<Line> Lines(List<OfficeRun> runs, float size, float width, SKColor color)
        {
            // runs → words with their style; inline formulas become drawings sized to the text
            var pieces = new List<Piece>();
            foreach (var run in runs)
            {
                if (run.Break) { pieces.Add(new Piece("\n", fonts.Get(size), color, 0)); continue; }
                if (run.Math && MathRenderer.RenderSvg(run.Text, false, 18) is { Success: true } m && SvgRaster.SizeOf(m.Svg!) is { } sz)
                {
                    var scale = size / 18f * 1.05f;
                    pieces.Add(new Piece(run.Text, fonts.Get(size), color, (float)sz.Width * scale, Svg: m.Svg, Height: (float)sz.Height * scale));
                    continue;
                }
                if (run.Math) limitations.Add("Fórmulas que o Urbe não conseguiu desenhar saem como TeX.");
                var face = fonts.Face(run.Bold, run.Italic || run.Math, run.Code);
                var ink = run.Href is not null ? Link : color;
                var runSize = run.Code ? size * .9f : size;
                foreach (var token in System.Text.RegularExpressions.Regex.Split(run.Text, @"(\s+)"))
                {
                    if (token.Length == 0) continue;
                    var space = char.IsWhiteSpace(token[0]);
                    var font = new SKFont(fonts.For(space ? " " : token, face), runSize);
                    pieces.Add(new Piece(space ? " " : token, font, ink, font.MeasureText(space ? " " : token), run.Href, Space: space, Strike: run.Strike,
                        Back: run.Code ? new SKColor(0xf3, 0xf1, 0xec) : null));
                }
            }
            var lines = new List<Line>();
            var current = new List<Piece>();
            float used = 0;
            void Close()
            {
                while (current.Count > 0 && current[^1].Space) current.RemoveAt(current.Count - 1);
                var asc = Math.Max(size, current.Count == 0 ? size : current.Max(p => p.Svg is null ? p.Font.Size : p.Height * .75f));
                var height = Math.Max(size * 1.42f, current.Count == 0 ? 0 : current.Max(p => p.Svg is null ? 0 : p.Height + 4));
                lines.Add(new Line(current, height, asc));
                current = [];
                used = 0;
            }
            foreach (var p in pieces)
            {
                if (p.Text == "\n") { Close(); continue; }
                if (p.Space && current.Count == 0) continue;
                if (!p.Space && used + p.Width > width && current.Count > 0) Close();
                // a word wider than the line is cut, never drawn off the page
                if (!p.Space && p.Svg is null && p.Width > width)
                {
                    var rest = p.Text;
                    while (rest.Length > 0)
                    {
                        int cut = rest.Length;
                        while (cut > 1 && p.Font.MeasureText(rest[..cut]) > width - used) cut--;
                        var part = rest[..cut];
                        current.Add(p with { Text = part, Width = p.Font.MeasureText(part) });
                        used += p.Font.MeasureText(part);
                        rest = rest[cut..];
                        if (rest.Length > 0) Close();
                    }
                    continue;
                }
                current.Add(p);
                used += p.Width;
            }
            if (current.Count > 0) Close();
            return lines.Where(l => l.Pieces.Count > 0).ToList();
        }

        /// <summary>Lines down the page; a paragraph never leaves a single line alone at the foot or the head of a page.</summary>
        private void Flow(List<Line> lines, float x, bool keepTogether = false, SKColor? back = null, SKColor? bar = null)
        {
            int i = 0;
            while (i < lines.Count)
            {
                var room = Bottom - _y;
                int fit = 0;
                float h = 0;
                while (i + fit < lines.Count && h + lines[i + fit].Height <= room) { h += lines[i + fit].Height; fit++; }
                if (keepTogether && fit < lines.Count - i && _pageHasContent) { NewPage(); continue; }
                int remaining = lines.Count - i;
                if (fit < remaining)
                {
                    // orphan: only one line would stay here → move all; widow: only one would go → keep one more back
                    if (fit == 1 && remaining > 1 && _pageHasContent) fit = 0;
                    else if (remaining - fit == 1 && fit > 1) fit--;
                }
                if (fit == 0)
                {
                    if (!_pageHasContent) fit = 1; // an empty page takes at least one line
                    else { NewPage(); continue; }
                }
                var top = _y;
                var height = lines.Skip(i).Take(fit).Sum(l => l.Height);
                if (back is { } b) Page.Add(c => { using var p = new SKPaint { Color = b }; c.DrawRect(SKRect.Create(x - 12, top, ContentW - (x - MarginX) + 12, height + 2), p); });
                if (bar is { } br) Page.Add(c => { using var p = new SKPaint { Color = br }; c.DrawRect(SKRect.Create(x - 12, top, 2.5f, height + 2), p); });
                for (int k = 0; k < fit; k++) { DrawLine(lines[i + k], x, _y); _y += lines[i + k].Height; }
                _pageHasContent = true;
                i += fit;
                if (i < lines.Count) NewPage();
            }
        }

        private void DrawLine(Line line, float x, float top)
        {
            var baseline = top + line.Ascent;
            float cx = x;
            foreach (var p in line.Pieces)
            {
                var px = cx;
                var piece = p;
                if (piece.Svg is { } svg)
                {
                    var rect = SKRect.Create(px, baseline - piece.Height * .78f, piece.Width, piece.Height);
                    Page.Add(c => SvgRaster.DrawInto(c, svg, rect));
                }
                else Page.Add(c => DrawPiece(c, piece, px, baseline));
                cx += p.Width;
            }
        }
    }

    private static void DrawPiece(SKCanvas canvas, Piece p, float x, float baseline)
    {
        if (p.Back is { } back)
        {
            using var bg = new SKPaint { Color = back };
            canvas.DrawRect(SKRect.Create(x - 1, baseline - p.Font.Size * .85f, p.Width + 2, p.Font.Size * 1.1f), bg);
        }
        using var paint = new SKPaint { Color = p.Color, IsAntialias = true };
        canvas.DrawText(p.Text, x, baseline, p.Font, paint);
        if (p.Strike)
        {
            using var line = new SKPaint { Color = p.Color, StrokeWidth = .7f };
            canvas.DrawLine(x, baseline - p.Font.Size * .3f, x + p.Width, baseline - p.Font.Size * .3f, line);
        }
        if (p.Href is { } href)
        {
            using var under = new SKPaint { Color = p.Color, StrokeWidth = .5f };
            if (!p.Space) canvas.DrawLine(x, baseline + 1.5f, x + p.Width, baseline + 1.5f, under);
            canvas.DrawUrlAnnotation(SKRect.Create(x, baseline - p.Font.Size, p.Width, p.Font.Size * 1.3f), href);
        }
    }
}
