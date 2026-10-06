using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

internal sealed record PageTocEntry(
    string Id,
    string Anchor,
    string Title,
    bool Menu);

internal sealed record PageBookEntry(
    string Kind,
    int Number,
    string Title,
    string Anchor);

internal sealed record PageChapterDocument(
    UrbeDocument Document,
    int Number,
    string Anchor);

internal sealed class PageSectionRenderContext
{
    public required PageSection Section { get; init; }
    public required int Index { get; init; }
    public required string Anchor { get; set; }
    public int PartNumber { get; set; }
    public int ChapterNumber { get; set; }
    public IReadOnlyList<PageChapterDocument> ChapterDocuments { get; set; } =
        Array.Empty<PageChapterDocument>();
}

internal sealed partial class PageRenderPlan
{
    private PageRenderPlan(
        IReadOnlyList<PageSectionRenderContext> sections,
        IReadOnlyList<PageTocEntry> tocEntries,
        IReadOnlyList<PageBookEntry> bookEntries,
        string chapterStyle,
        bool bookFormat)
    {
        Sections = sections;
        TocEntries = tocEntries;
        BookEntries = bookEntries;
        ChapterStyle = chapterStyle;
        BookFormat = bookFormat;
    }

    public IReadOnlyList<PageSectionRenderContext> Sections { get; }
    public IReadOnlyList<PageTocEntry> TocEntries { get; }
    public IReadOnlyList<PageBookEntry> BookEntries { get; }
    public string ChapterStyle { get; }
    public bool BookFormat { get; }

    public static PageRenderPlan Create(
        PageDocument page,
        DocumentStore? documents)
    {
        ArgumentNullException.ThrowIfNull(page);

        var sectionContexts = page.Sections
            .Select((section, index) =>
                new PageSectionRenderContext
                {
                    Section = section,
                    Index = index,
                    Anchor = string.Empty
                })
            .ToArray();

        var usedAnchors = new HashSet<string>(StringComparer.Ordinal);
        var toc = new List<PageTocEntry>();

        foreach (var context in sectionContexts)
        {
            if (PageDocument.BoolValue(context.Section.Style["hidden"]))
                continue;

            var props = context.Section.Props;
            var title = PageDocument.StringValue(props["title"]);
            if (string.IsNullOrWhiteSpace(title) &&
                context.Section.Type == "note")
            {
                title = FindDocument(
                    documents,
                    PageDocument.StringValue(props["path"]))?.Title;
            }

            var customAnchor =
                PageDocument.StringValue(context.Section.Style["anchor"]);
            var anchor = !string.IsNullOrWhiteSpace(customAnchor)
                ? Slug(customAnchor)
                : !string.IsNullOrWhiteSpace(title)
                    ? Slug(title)
                    : string.Empty;

            if (anchor.Length > 0)
                anchor = UniqueAnchor(anchor, usedAnchors);

            context.Anchor = anchor;

            if (!string.IsNullOrWhiteSpace(title) && anchor.Length > 0)
            {
                toc.Add(
                    new PageTocEntry(
                        context.Section.Id ?? "s" + (context.Index + 1)
                            .ToString(CultureInfo.InvariantCulture),
                        anchor,
                        !string.IsNullOrWhiteSpace(customAnchor)
                            ? customAnchor!
                            : title!,
                        PageDocument.BoolValue(context.Section.Style["menu"])));
            }
        }

        var raw = page.Raw;
        var layout = raw["layout"] as JsonObject;
        var chapterStyle =
            PageDocument.StringValue(layout?["chapterStyle"]) ?? "word";
        var bookFormat =
            string.Equals(
                PageDocument.StringValue(layout?["format"]),
                "book",
                StringComparison.Ordinal);

        var book = new List<PageBookEntry>();
        var chapterNumber = 0;
        var partNumber = 0;

        foreach (var context in sectionContexts)
        {
            if (PageDocument.BoolValue(context.Section.Style["hidden"]))
                continue;

            var props = context.Section.Props;
            switch (context.Section.Type)
            {
                case "part":
                {
                    context.PartNumber = ++partNumber;
                    if (context.Anchor.Length == 0)
                    {
                        context.Anchor = UniqueAnchor(
                            "parte-" + partNumber.ToString(CultureInfo.InvariantCulture),
                            usedAnchors);
                    }

                    book.Add(
                        new PageBookEntry(
                            "part",
                            partNumber,
                            PageDocument.StringValue(props["title"]) ?? string.Empty,
                            context.Anchor));
                    break;
                }
                case "chapter":
                {
                    var numbered = PageDocument.BoolValue(
                        props["numbered"],
                        true);
                    context.ChapterNumber = numbered ? ++chapterNumber : 0;

                    var title =
                        PageDocument.StringValue(props["title"]) ??
                        (string.Equals(
                            PageDocument.StringValue(props["source"]),
                            "note",
                            StringComparison.Ordinal)
                            ? FindDocument(
                                documents,
                                PageDocument.StringValue(props["path"]))?.Title
                            : null) ??
                        string.Empty;

                    if (context.Anchor.Length == 0)
                    {
                        var fallback = context.ChapterNumber > 0
                            ? "capitulo-" +
                              context.ChapterNumber.ToString(
                                  CultureInfo.InvariantCulture)
                            : "capitulo-" +
                              (context.Section.Id ??
                               (context.Index + 1).ToString(
                                   CultureInfo.InvariantCulture));
                        context.Anchor = UniqueAnchor(fallback, usedAnchors);
                    }

                    book.Add(
                        new PageBookEntry(
                            "chapter",
                            context.ChapterNumber,
                            title,
                            context.Anchor));
                    break;
                }
                case "chapters":
                {
                    var folder =
                        PageDocument.StringValue(props["folder"]) ?? string.Empty;
                    var sort =
                        PageDocument.StringValue(props["sort"]) ?? "path";
                    var selected = SelectFolderDocuments(
                        documents,
                        folder,
                        sort);

                    var children = new List<PageChapterDocument>();
                    foreach (var document in selected)
                    {
                        chapterNumber++;
                        var anchor = UniqueAnchor(
                            "nota-" + Slug(document.Title),
                            usedAnchors);
                        children.Add(
                            new PageChapterDocument(
                                document,
                                chapterNumber,
                                anchor));
                        book.Add(
                            new PageBookEntry(
                                "chapter",
                                chapterNumber,
                                document.Title,
                                anchor));
                    }

                    context.ChapterDocuments = children.AsReadOnly();
                    break;
                }
            }
        }

        return new PageRenderPlan(
            Array.AsReadOnly(sectionContexts),
            toc.AsReadOnly(),
            book.AsReadOnly(),
            chapterStyle,
            bookFormat);
    }

    internal static UrbeDocument? FindDocument(
        DocumentStore? documents,
        string? path)
    {
        if (documents is null || string.IsNullOrWhiteSpace(path))
            return null;

        var clean = path.Trim();
        var direct = documents.Get(clean);
        if (direct is not null)
            return direct;

        if (!clean.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            direct = documents.Get(clean + ".md");
            if (direct is not null)
                return direct;
        }

        var wanted = Path.GetFileNameWithoutExtension(clean);
        return documents.List().FirstOrDefault(document =>
            string.Equals(
                document.Title,
                wanted,
                StringComparison.OrdinalIgnoreCase));
    }

    internal static string Slug(string? source)
    {
        var normalized = (source ?? string.Empty)
            .Normalize(NormalizationForm.FormD);
        var withoutMarks = new string(
            normalized
                .Where(ch =>
                    CharUnicodeInfo.GetUnicodeCategory(ch) !=
                    UnicodeCategory.NonSpacingMark)
                .ToArray())
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant();

        var slug = NonAlphaNumericPattern()
            .Replace(withoutMarks, "-")
            .Trim('-');

        if (slug.Length == 0)
            slug = "secao";
        return slug.Length <= 60 ? slug : slug[..60].TrimEnd('-');
    }

    private static IReadOnlyList<UrbeDocument> SelectFolderDocuments(
        DocumentStore? documents,
        string folder,
        string sort)
    {
        if (documents is null)
            return Array.Empty<UrbeDocument>();

        var normalizedFolder = folder.Trim('/')
            .ToLowerInvariant();

        IEnumerable<UrbeDocument> query = documents.List()
            .Where(document =>
                normalizedFolder.Length == 0 ||
                document.Path.ToLowerInvariant()
                    .StartsWith(
                        normalizedFolder + "/",
                        StringComparison.Ordinal));

        query = sort switch
        {
            "modified" => query.OrderByDescending(
                document => document.Modified ?? string.Empty),
            "title" => query.OrderBy(
                document => document.Title,
                StringComparer.Ordinal),
            _ => query.OrderBy(
                document => document.Path,
                StringComparer.Ordinal)
        };

        return Array.AsReadOnly(query.Take(200).ToArray());
    }

    private static string UniqueAnchor(
        string requested,
        ISet<string> used)
    {
        var candidate = requested;
        while (!used.Add(candidate))
            candidate += "-2";
        return candidate;
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericPattern();
}
