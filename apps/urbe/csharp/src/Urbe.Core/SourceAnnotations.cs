using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

public enum SourceAnnotationKind { Citation, Highlight, Bookmark, Comment }

/// <summary>Whether an annotation's anchor still names the source as it is now.</summary>
public enum SourceAnnotationStatus
{
    /// <summary>The source has the revision the annotation was made on: its anchor is exact.</summary>
    Current,
    /// <summary>The source changed since: the anchor is not moved to a look-alike passage; a person reviews it.</summary>
    Outdated,
    /// <summary>The annotation keeps no revision (or the source could not be read): nothing can be promised.</summary>
    Unknown,
    /// <summary>The source is no longer in the vault.</summary>
    Missing
}

/// <summary>
/// CST-239: one annotation of a source (PDF, EPUB/document part, image, web page), as written in a real note.
/// The note is the only store: this is read back from its Markdown (no parallel annotation database), so writing,
/// undo, history and conflicts stay the note's own.
/// </summary>
public sealed record SourceAnnotation(
    string NotePath, int Line, SourceAnnotationKind Kind, string Quote, string? Comment,
    string Source, string? Fragment, string? Sha256, string? Unit, int? Index, int? TextOffset, int? TextLength, ReaderRegion? Region)
{
    /// <summary>Where in the source, said the way its format allows ("p. 12", "capítulo 3", a web page).</summary>
    public string Place => Unit switch
    {
        "page" => "p. " + Index,
        null => Source.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "página web" : "",
        "chapter" => "capítulo " + Index,
        "slide" => "slide " + Index,
        "section" => "seção " + Index,
        "image" => "imagem",
        var unit => unit + " " + Index
    };

    public string KindLabel => Kind switch
    {
        SourceAnnotationKind.Highlight => "Destaque",
        SourceAnnotationKind.Bookmark => "Marcador",
        SourceAnnotationKind.Comment => "Anotação",
        _ => "Citação"
    };

    public bool IsWeb => Source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || Source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}

public static partial class SourceAnnotations
{
    /// <summary>
    /// The annotations a note holds, in the shape the readers write them:
    /// <c>**Destaque**</c> (or Marcador/Anotação, none for a citation), the quote as <c>&gt;</c> lines, an optional
    /// <c>Comentário:</c> line and a <c>Fonte:</c> line with the link to the source and its anchor.
    /// </summary>
    public static IReadOnlyList<SourceAnnotation> Parse(string notePath, string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var found = new List<SourceAnnotation>();
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].StartsWith("Fonte: ", StringComparison.Ordinal)) continue;
            if (Target(lines[i]) is not { } target) continue;
            // walk back over the comment, the quote and the label (blank lines between them)
            int j = i - 1;
            string? comment = null;
            while (j >= 0 && lines[j].Trim().Length == 0) j--;
            if (j >= 0 && lines[j].StartsWith("Comentário: ", StringComparison.Ordinal)) { comment = lines[j]["Comentário: ".Length..].Trim(); j--; while (j >= 0 && lines[j].Trim().Length == 0) j--; }
            var quote = new List<string>();
            while (j >= 0 && (lines[j].StartsWith("> ", StringComparison.Ordinal) || lines[j] == ">")) { quote.Insert(0, lines[j].Length > 2 ? lines[j][2..] : ""); j--; }
            if (quote.Count == 0) continue;
            int start = j + 1;
            while (j >= 0 && lines[j].Trim().Length == 0) j--;
            var kind = SourceAnnotationKind.Citation;
            if (j >= 0)
                switch (lines[j].Trim())
                {
                    case "**Destaque**": kind = SourceAnnotationKind.Highlight; start = j; break;
                    case "**Marcador**": kind = SourceAnnotationKind.Bookmark; start = j; break;
                    case "**Anotação**": kind = SourceAnnotationKind.Comment; start = j; break;
                }
            var (source, fragment) = target;
            string? sha = null, unit = null; int? index = null, offset = null, length = null; ReaderRegion? region = null;
            if (PdfSourceReference.Parse(fragment) is { } pdf) { sha = pdf.Sha256; unit = "page"; index = pdf.Page; offset = pdf.TextOffset; length = pdf.TextLength; region = pdf.Region; }
            else if (PartSourceReference.Parse(fragment) is { } part) { sha = part.Sha256; unit = part.Unit; index = part.Index; offset = part.TextOffset; length = part.TextLength; region = part.Region; }
            found.Add(new SourceAnnotation(notePath, start + 1, kind, string.Join("\n", quote), comment, source, fragment, sha, unit, index, offset, length, region));
        }
        return found;
    }

    /// <summary>Every annotation of one source across the notes, in note order then line order.</summary>
    public static IReadOnlyList<SourceAnnotation> ForSource(IEnumerable<(string Path, string Content)> notes, string source) =>
        notes.OrderBy(n => n.Path, StringComparer.Ordinal)
            .SelectMany(n => n.Content.Contains(source, StringComparison.Ordinal) ? Parse(n.Path, n.Content) : [])
            .Where(a => string.Equals(a.Source, source, StringComparison.Ordinal)).ToArray();

    /// <summary>Exact anchor only: a changed source is flagged for review, never re-anchored by similarity.</summary>
    public static SourceAnnotationStatus Status(SourceAnnotation annotation, string? currentSha256, bool exists = true) =>
        !exists ? SourceAnnotationStatus.Missing
        : annotation.Sha256 is null || currentSha256 is null ? SourceAnnotationStatus.Unknown
        : string.Equals(annotation.Sha256, currentSha256, StringComparison.OrdinalIgnoreCase) ? SourceAnnotationStatus.Current
        : SourceAnnotationStatus.Outdated;

    public static string StatusLabel(SourceAnnotationStatus status) => status switch
    {
        SourceAnnotationStatus.Current => "âncora exata",
        SourceAnnotationStatus.Outdated => "fonte mudou: revise",
        SourceAnnotationStatus.Missing => "fonte ausente",
        _ => "sem versão registrada"
    };

    /// <summary>
    /// The annotations as a W3C Web Annotation collection (JSON-LD): a TextQuoteSelector (exact, plus prefix/suffix
    /// taken from <paramref name="textOf"/> when the source text of that part is known and the quote is at its
    /// recorded offset), a TextPositionSelector and a FragmentSelector for the page/part. The note's text is the body.
    /// Nothing in the source or the note changes.
    /// </summary>
    public static string ToWebAnnotations(IReadOnlyList<SourceAnnotation> annotations, Func<SourceAnnotation, string?>? textOf = null, string? label = null)
    {
        var items = new JsonArray();
        foreach (var a in annotations)
        {
            var selectors = new JsonArray();
            if (a.Kind != SourceAnnotationKind.Bookmark && a.Quote.Length > 0)
            {
                var quote = new JsonObject { ["type"] = "TextQuoteSelector", ["exact"] = a.Quote };
                if (a.TextOffset is { } offset && textOf?.Invoke(a) is { } text && offset >= 0 && offset + a.Quote.Length <= text.Length &&
                    string.CompareOrdinal(text, offset, a.Quote, 0, a.Quote.Length) == 0)
                {
                    quote["prefix"] = text[Math.Max(0, offset - 32)..offset];
                    quote["suffix"] = text[(offset + a.Quote.Length)..Math.Min(text.Length, offset + a.Quote.Length + 32)];
                }
                selectors.Add(quote);
                if (a.TextOffset is { } start)
                    selectors.Add(new JsonObject { ["type"] = "TextPositionSelector", ["start"] = start, ["end"] = start + (a.TextLength ?? a.Quote.Length) });
            }
            if (a.Unit == "page" && a.Index is { } page)
                selectors.Add(new JsonObject { ["type"] = "FragmentSelector", ["conformsTo"] = "http://tools.ietf.org/rfc/rfc3778", ["value"] = "page=" + page.ToString(CultureInfo.InvariantCulture) });
            else if (a.Fragment is { Length: > 0 } fragment && !a.IsWeb)
                selectors.Add(new JsonObject { ["type"] = "FragmentSelector", ["value"] = fragment });
            var target = new JsonObject { ["source"] = a.Source };
            if (a.Sha256 is { } sha) target["urbe:sha256"] = sha;
            if (selectors.Count == 1) target["selector"] = selectors[0]!.DeepClone();
            else if (selectors.Count > 1) target["selector"] = selectors;
            var item = new JsonObject
            {
                ["type"] = "Annotation",
                ["id"] = "urn:urbe:annotation:" + Id(a),
                ["motivation"] = a.Kind switch
                {
                    SourceAnnotationKind.Highlight => "highlighting",
                    SourceAnnotationKind.Bookmark => "bookmarking",
                    SourceAnnotationKind.Comment => "commenting",
                    _ => "quoting"
                },
                ["target"] = target,
                ["urbe:note"] = a.NotePath + "#L" + a.Line.ToString(CultureInfo.InvariantCulture)
            };
            if (a.Comment is { Length: > 0 } comment) item["body"] = new JsonObject { ["type"] = "TextualBody", ["value"] = comment, ["format"] = "text/plain" };
            items.Add(item);
        }
        var collection = new JsonObject
        {
            ["@context"] = new JsonArray("http://www.w3.org/ns/anno.jsonld", new JsonObject { ["urbe"] = "urn:urbe:" }),
            ["type"] = "AnnotationCollection",
            ["label"] = label ?? "Anotações",
            ["total"] = items.Count,
            ["first"] = new JsonObject { ["type"] = "AnnotationPage", ["items"] = items }
        };
        return collection.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>A stable id: the same annotation in the same note gives the same id across exports.</summary>
    private static string Id(SourceAnnotation a) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(
        a.NotePath + "\n" + a.Source + "\n" + a.Fragment + "\n" + a.Kind + "\n" + a.Quote))).ToLowerInvariant()[..32];

    /// <summary>The source and fragment a <c>Fonte:</c> line links to: a wikilink to a vault file or a web link.</summary>
    private static (string Source, string? Fragment)? Target(string line)
    {
        if (WikiRe().Match(line) is { Success: true } wiki)
        {
            var (path, fragment) = EmbedFragments.Split(wiki.Groups[1].Value);
            return (path, fragment);
        }
        if (WebRe().Match(line) is { Success: true } web && Uri.TryCreate(web.Groups[1].Value, UriKind.Absolute, out var url))
        {
            var builder = new UriBuilder(url) { Fragment = "" };
            return (builder.Uri.AbsoluteUri, url.Fragment.Length > 1 ? url.Fragment[1..] : null);
        }
        return null;
    }

    [GeneratedRegex(@"\[\[([^\]|]+)(?:\|[^\]]*)?\]\]")]
    private static partial Regex WikiRe();
    [GeneratedRegex(@"\[[^\]]*\]\(<?(https?://[^>)\s]+)>?\)")]
    private static partial Regex WebRe();
}
