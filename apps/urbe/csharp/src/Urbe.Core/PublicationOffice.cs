namespace Urbe.Core;

/// <summary>A run of text with its inline formatting (the semantic inline layer every office format shares).</summary>
public sealed record OfficeRun(string Text, bool Bold = false, bool Italic = false, bool Strike = false, bool Code = false,
    bool Math = false, string? Href = null, bool Break = false);

/// <summary>A block of a publication as office formats see it: semantic, never a picture of the HTML.</summary>
public abstract record OfficeBlock;
public sealed record OfficeHeading(int Level, IReadOnlyList<OfficeRun> Runs) : OfficeBlock;
/// <summary>A paragraph; <see cref="Style"/> is body, quote or math (a display formula).</summary>
public sealed record OfficeParagraph(IReadOnlyList<OfficeRun> Runs, string Style = "body") : OfficeBlock;
/// <summary>A list item; items of the same list share <see cref="ListId"/> (an ordered list restarts per list).</summary>
public sealed record OfficeListItem(int ListId, int Level, bool Ordered, bool? Checked, IReadOnlyList<OfficeRun> Runs) : OfficeBlock;
public sealed record OfficeCode(string Text, string? Language) : OfficeBlock;
public sealed record OfficeTable(IReadOnlyList<IReadOnlyList<IReadOnlyList<OfficeRun>>> Rows, bool HeaderRow) : OfficeBlock;
/// <summary>A picture. <see cref="Svg"/> is the vector original of a drawing made by the Urbe (a graph), kept beside
/// <see cref="Bytes"/> (PNG then) for formats that hold both.</summary>
public sealed record OfficeImage(byte[] Bytes, string Mime, string Extension, int Width, int Height, string Alt, string? Svg = null) : OfficeBlock;

/// <summary>A drawing turned into pixels by the platform (the core draws no pixels).</summary>
public sealed record OfficeRaster(byte[] Png, int Width, int Height);
public sealed record OfficeRule : OfficeBlock;
/// <summary>A <c>:::tipo título</c> container of the note (CST-026), with the blocks inside it.</summary>
public sealed record OfficeBox(string Type, string Title, IReadOnlyList<OfficeBlock> Blocks) : OfficeBlock;
/// <summary>One placement of the publication: a live note at one revision.</summary>
public sealed record OfficeChapter(string Title, string DocumentId, IReadOnlyList<OfficeBlock> Blocks);

/// <summary>
/// The publication read once for the office adapters (CST-166/168): chapters of semantic blocks, and the honest
/// list of what a format could not keep (an office file is not the HTML; nothing is flattened without saying so).
/// </summary>
public sealed record OfficeDocument(string Title, string Layout, IReadOnlyList<OfficeChapter> Chapters, IReadOnlyList<string> Limitations);

/// <summary>An exported office file and what it could not represent natively.</summary>
public sealed record PublicationExport(byte[] Bytes, IReadOnlyList<string> Limitations);

public static class PublicationOffice
{
    /// <summary>
    /// Reads the publication's live notes into semantic blocks. Images come from the vault through
    /// <paramref name="readAsset"/>; a source that changes while assets are read fails the export (as EPUB does),
    /// so a file never mixes two revisions.
    /// </summary>
    public static async Task<OfficeDocument> ReadAsync(PublicationProject project, PublicationSourceCatalog catalog,
        Func<string, Task<byte[]?>>? readAsset = null, CancellationToken cancellationToken = default, Func<string, int, int, OfficeRaster?>? rasterize = null)
    {
        PublicationProjectCodec.Validate(project);
        ArgumentNullException.ThrowIfNull(catalog);
        if (project.Sections.Count == 0) throw new InvalidDataException("Adicione notas à publicação antes de exportar.");
        var limits = new SortedSet<string>(StringComparer.Ordinal);
        var chapters = new List<OfficeChapter>();
        var sources = new List<PublicationSourceView>();
        int lists = 0;
        foreach (var piece in project.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = catalog.Read(piece.Source) ?? throw new InvalidDataException("Nota de origem indisponível: " + piece.Source.DocumentId);
            sources.Add(source);
            var content = NoteMarks.StripComments(MarkdownEngine.SplitFrontmatter(source.Markdown).Body);
            var blocks = new List<OfficeBlock>();
            foreach (var node in NoteContainers.ParseRich(content)) await Block(node, blocks, source);
            chapters.Add(new OfficeChapter(Path.GetFileNameWithoutExtension(source.Path), source.DocumentId, blocks));
        }
        foreach (var source in sources)
        {
            var current = catalog.Read(new PublicationSourceReference(source.DocumentId, source.Fragment));
            if (current is null || current.Revision != source.Revision || current.Markdown != source.Markdown)
                throw new InvalidOperationException("Uma fonte mudou durante a exportação. Exporte novamente para usar a revisão atual.");
        }
        return new OfficeDocument(project.Title, project.Design.Layout, chapters, limits.ToArray());

        async Task Block(RichNode node, List<OfficeBlock> into, PublicationSourceView source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Tag != RichNode.TextTag && node.Attr("data-tex") is { Length: > 0 } display)
            { into.Add(new OfficeParagraph([MathRun(display)], "math")); return; }
            switch (node.Tag)
            {
                case RichNode.TextTag:
                    if (!string.IsNullOrWhiteSpace(node.Text)) into.Add(new OfficeParagraph(Runs(node, source)));
                    return;
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    into.Add(new OfficeHeading(node.Tag[1] - '0', Runs(node, source)));
                    return;
                case "ul" or "ol":
                    List(node, into, source, ++lists, 0);
                    return;
                case "blockquote":
                    Quote(node, into, source);
                    return;
                case "pre":
                    if (NoteProgress.IsLanguage(node.Attr("data-lang")))
                    {
                        var result = NoteProgress.Evaluate(Plain(node), source.Markdown, id => catalog.Read(new(id))?.Markdown);
                        into.Add(new OfficeParagraph([new OfficeRun(result.Label + ": " + result.Summary, Bold: true)]));
                        limits.Add("Barras de progresso saem como texto com o valor atual (sem a barra gráfica).");
                        return;
                    }
                    if (Calculus.Calculator.IsLanguage(node.Attr("data-lang")))
                    {
                        // CST-206: a calculation is its lines and their results, computed now from the note
                        var rows = Calculus.Calculator.Run(Plain(node)).Where(l => l.Input.Trim().Length > 0)
                            .Select(l => (IReadOnlyList<IReadOnlyList<OfficeRun>>)[[new OfficeRun(l.Input.Trim(), Code: true)], [new OfficeRun(l.Result ?? l.Error ?? "", Bold: l.Ok)]]).ToList();
                        if (rows.Count > 0) into.Add(new OfficeTable(rows, false));
                        return;
                    }
                    if (FunctionPlot.IsPlotLanguage(node.Attr("data-lang")))
                    {
                        // CST-169: a graph is a drawing in every output, vector where the format holds it
                        const int w = 640, h = 400;
                        var plotText = Plain(node);
                        var svg = PlotSvg(FunctionPlot.Parse(plotText).ToSvg(w, h), w, h);
                        var alt = "Gráfico: " + plotText.Replace('\n', ' ').Trim();
                        if (rasterize?.Invoke(svg, w * 2, h * 2) is { } png)
                            into.Add(new OfficeImage(png.Png, "image/png", ".png", w, h, alt, svg));
                        else
                        {
                            into.Add(new OfficeImage(System.Text.Encoding.UTF8.GetBytes(svg), "image/svg+xml", ".svg", w, h, alt, svg));
                            limits.Add("Gráficos vão como imagem vetorial (SVG); editores muito antigos podem não mostrá-los.");
                        }
                        return;
                    }
                    into.Add(new OfficeCode(Plain(node).TrimEnd('\n'), node.Attr("data-lang") is { Length: > 0 } lang ? lang : null));
                    return;
                case "table":
                    Table(node, into, source);
                    return;
                case "hr":
                    into.Add(new OfficeRule());
                    return;
                case "aside" when node.HasClass("ubox"):
                    var inner = new List<OfficeBlock>();
                    var boxTitle = "";
                    foreach (var child in node.Children)
                    {
                        if (child.Tag == "p" && child.HasClass("ubox-title")) { boxTitle = Plain(child).Trim(); continue; }
                        await Block(child, inner, source);
                    }
                    into.Add(new OfficeBox(node.Attr("data-type"), boxTitle, inner));
                    return;
                case "p" or "div" or "section" or "figure":
                    await Paragraph(node, into, source);
                    return;
                case "img":
                    if (await Image(node, source) is { } image) into.Add(image);
                    return;
                default:
                    if (node.Children.Any(IsBlock)) foreach (var child in node.Children) await Block(child, into, source);
                    else if (Runs(node, source) is { Count: > 0 } runs) into.Add(new OfficeParagraph(runs));
                    return;
            }
        }

        // A paragraph that holds images or a display formula is split around them: they are blocks of their own.
        async Task Paragraph(RichNode node, List<OfficeBlock> into, PublicationSourceView source)
        {
            if (node.Children.Any(IsBlock)) { foreach (var child in node.Children) await Block(child, into, source); return; }
            var pending = new List<OfficeRun>();
            void Flush() { if (pending.Any(r => r.Break || r.Text.Trim().Length > 0)) into.Add(new OfficeParagraph(pending.ToArray())); pending.Clear(); }
            foreach (var child in node.Children)
            {
                if (child.Tag == "img") { Flush(); if (await Image(child, source) is { } image) into.Add(image); continue; }
                if ((child.HasClass("umath-display") || child.HasClass("umath-block")) && child.Attr("data-tex") is { Length: > 0 } tex)
                { Flush(); into.Add(new OfficeParagraph([MathRun(tex)], "math")); continue; }
                Inline(child, pending, default, source);
            }
            Flush();
        }

        void List(RichNode node, List<OfficeBlock> into, PublicationSourceView source, int listId, int level)
        {
            var ordered = node.Tag == "ol";
            foreach (var li in node.Children.Where(c => c.Tag == "li"))
            {
                bool? check = li.HasClass("task") ? li.Children.Any(c => c.Tag == "input" && c.Attrs.ContainsKey("checked")) : null;
                var runs = new List<OfficeRun>();
                var nested = new List<RichNode>();
                foreach (var child in li.Children)
                {
                    if (child.Tag is "ul" or "ol") nested.Add(child);
                    else if (child.Tag == "input" && check is not null) continue; // the box is the item's own mark
                    else if (child.Tag == "p") { if (runs.Count > 0) runs.Add(new OfficeRun("", Break: true)); foreach (var c in child.Children) Inline(c, runs, default, source); }
                    else Inline(child, runs, default, source);
                }
                into.Add(new OfficeListItem(listId, Math.Min(level, 8), ordered, check, Trim(runs)));
                foreach (var sub in nested) List(sub, into, source, ++lists, level + 1);
            }
        }

        void Quote(RichNode node, List<OfficeBlock> into, PublicationSourceView source)
        {
            var runs = new List<OfficeRun>();
            foreach (var child in node.Children)
            {
                if (child.Tag == "p" || IsBlock(child))
                {
                    if (runs.Count > 0) { into.Add(new OfficeParagraph(Trim(runs), "quote")); runs = []; }
                    var inner = new List<OfficeRun>();
                    Inline(child, inner, default, source);
                    if (inner.Count > 0) into.Add(new OfficeParagraph(Trim(inner), "quote"));
                }
                else Inline(child, runs, default, source);
            }
            if (runs.Count > 0) into.Add(new OfficeParagraph(Trim(runs), "quote"));
        }

        void Table(RichNode node, List<OfficeBlock> into, PublicationSourceView source)
        {
            var rows = new List<IReadOnlyList<IReadOnlyList<OfficeRun>>>();
            var header = false;
            foreach (var tr in Descendants(node).Where(n => n.Tag == "tr"))
            {
                var cells = tr.Children.Where(c => c.Tag is "td" or "th").Select(c => (IReadOnlyList<OfficeRun>)Runs(c, source)).ToList();
                if (rows.Count == 0 && tr.Children.Any(c => c.Tag == "th")) header = true;
                if (cells.Count > 0) rows.Add(cells);
            }
            if (rows.Count == 0) return;
            var width = rows.Max(r => r.Count);
            into.Add(new OfficeTable(rows.Select(r => (IReadOnlyList<IReadOnlyList<OfficeRun>>)r.Concat(Enumerable.Repeat((IReadOnlyList<OfficeRun>)[], width - r.Count)).ToList()).ToList(), header));
        }

        async Task<OfficeImage?> Image(RichNode node, PublicationSourceView source)
        {
            var raw = node.Attr("src");
            var alt = node.Attr("alt");
            string path;
            try { path = PublicationEpub.ResolveAsset(source.Path, raw); }
            catch (InvalidDataException) { limits.Add("Imagens fora do vault (links da web) ficam como texto alternativo."); return null; }
            if (readAsset is null || await readAsset(path) is not { Length: > 0 } bytes)
                throw new InvalidDataException("Imagem indisponível no vault: " + path);
            (string Extension, string Mime) type;
            try { type = PublicationEpub.ImageType(bytes); }
            catch (InvalidDataException) { limits.Add("Imagens que não são PNG, JPEG ou GIF ficam como texto alternativo."); return null; }
            int w = ImageHeader.Width(bytes) ?? 800, h = ImageHeader.Height(bytes) ?? 600;
            return new OfficeImage(bytes, type.Mime, type.Extension, Math.Max(1, w), Math.Max(1, h), alt);
        }

        List<OfficeRun> Runs(RichNode node, PublicationSourceView source)
        {
            var runs = new List<OfficeRun>();
            if (node.Tag == RichNode.TextTag) Inline(node, runs, default, source);
            else foreach (var child in node.Children) Inline(child, runs, default, source);
            return Trim(runs);
        }

        void Inline(RichNode node, List<OfficeRun> runs, Format f, PublicationSourceView source)
        {
            switch (node.Tag)
            {
                case RichNode.TextTag:
                    if (node.Text.Length > 0) runs.Add(new OfficeRun(node.Text, f.Bold, f.Italic, f.Strike, f.Code, Href: f.Href));
                    return;
                case "br": runs.Add(new OfficeRun("", Break: true)); return;
                case "input": runs.Add(new OfficeRun(node.Attrs.ContainsKey("checked") ? "☑ " : "☐ ")); return;
                case "img":
                    runs.Add(new OfficeRun("[" + (node.Attr("alt") is { Length: > 0 } a ? a : "imagem") + "]", Italic: true));
                    limits.Add("Imagens dentro de listas, tabelas ou citações ficam como texto alternativo.");
                    return;
            }
            if (node.Attr("data-tex") is { Length: > 0 } tex) { runs.Add(MathRun(tex)); return; }
            if (node.HasClass("wikilink"))
            {
                runs.Add(new OfficeRun(Plain(node), f.Bold, f.Italic, f.Strike, f.Code));
                limits.Add("Ligações [[entre notas]] viram texto: o arquivo não leva o vault junto.");
                return;
            }
            var next = node.Tag switch
            {
                "strong" or "b" => f with { Bold = true },
                "em" or "i" => f with { Italic = true },
                "del" or "s" => f with { Strike = true },
                "code" => f with { Code = true },
                "a" when Uri.TryCreate(node.Attr("href"), UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" or "mailto" => f with { Href = uri.AbsoluteUri },
                _ => f
            };
            foreach (var child in node.Children) Inline(child, runs, next, source);
        }

        OfficeRun MathRun(string tex)
        {
            limits.Add("Fórmulas saem como TeX editável (o editor de equações do Word/LibreOffice não as recebe convertidas).");
            return new OfficeRun(tex, Math: true);
        }
    }

    /// <summary>
    /// Containers as plain blocks for formats without boxes of their own: a title line (the title, else the type) and
    /// the body's paragraphs in the box style. Speaker notes are left out when <paramref name="dropSpeakerNotes"/>.
    /// </summary>
    public static IEnumerable<OfficeBlock> Flatten(IEnumerable<OfficeBlock> blocks, bool dropSpeakerNotes = false)
    {
        foreach (var block in blocks)
        {
            if (block is not OfficeBox box) { yield return block; continue; }
            if (dropSpeakerNotes && NoteContainers.IsSpeakerNotes(box.Type)) continue;
            var label = box.Title.Length > 0 ? box.Title : box.Type.Length > 0 ? char.ToUpperInvariant(box.Type[0]) + box.Type[1..] : "";
            if (label.Length > 0) yield return new OfficeParagraph([new OfficeRun(label, Bold: true)], "boxtitle");
            foreach (var inner in Flatten(box.Blocks, dropSpeakerNotes))
                yield return inner is OfficeParagraph { Style: "body" } p ? p with { Style = "box" } : inner;
        }
    }

    /// <summary>A standalone SVG file: fixed size and black ink where the page would give its colour.</summary>
    internal static string PlotSvg(string svg, int width, int height) =>
        svg.Replace("width='100%'", "width='" + width + "' height='" + height + "'").Replace("currentColor", "#25272c");

    private readonly record struct Format(bool Bold, bool Italic, bool Strike, bool Code, string? Href);

    private static bool IsBlock(RichNode n) => n.Tag is "aside" or "p" or "div" or "ul" or "ol" or "pre" or "table" or "blockquote" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "hr" or "section" or "figure";

    private static IEnumerable<RichNode> Descendants(RichNode n) => n.Children.SelectMany(c => new[] { c }.Concat(Descendants(c)));

    internal static string Plain(RichNode node) => node.Tag == RichNode.TextTag ? node.Text : string.Concat(node.Children.Select(Plain));

    private static List<OfficeRun> Trim(List<OfficeRun> runs)
    {
        while (runs.Count > 0 && !runs[0].Break && runs[0].Text.Trim().Length == 0 && !runs[0].Math) runs.RemoveAt(0);
        while (runs.Count > 0 && (runs[^1].Break || runs[^1].Text.Trim().Length == 0) && !runs[^1].Math) runs.RemoveAt(runs.Count - 1);
        if (runs.Count > 0 && !runs[0].Math) runs[0] = runs[0] with { Text = runs[0].Text.TrimStart() };
        if (runs.Count > 0 && !runs[^1].Math) runs[^1] = runs[^1] with { Text = runs[^1].Text.TrimEnd() };
        return runs;
    }

    /// <summary>XML 1.0 forbids most control characters; a note may hold them (pasted text). They are dropped, not escaped.</summary>
    internal static string XmlSafe(string text) =>
        string.Create(text.Length, text, (span, s) => { for (int i = 0; i < s.Length; i++) span[i] = s[i] < 0x20 && s[i] is not ('\t' or '\n' or '\r') ? ' ' : s[i]; });
}
