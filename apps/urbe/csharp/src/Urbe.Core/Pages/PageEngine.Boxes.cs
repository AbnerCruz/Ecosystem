using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core.Pages;

/// <summary>
/// `:::tipo [título]` … `:::` containers (UC-44, <see cref="NoteContainers"/>) in page texts, behind
/// <see cref="PageEngine.UrbeMarks"/>: the opening and closing lines become marker paragraphs before the 1.8.4 Markdown
/// runs, and the rendered marker paragraphs become <c>&lt;div class="urbe-box urbe-box-tipo"&gt;</c> after. Boxes are
/// content: they show in the preview and in the export alike.
/// </summary>
internal sealed partial class PageBoxMarks
{
    // private-use markers, distinct from the comment and math ones: Open + index + End, and Close
    private const char Open = '', End = '', Close = '';

    private readonly List<(string Type, string Title)> _boxes = [];

    /// <summary>The text with each container's opening/closing line as a marker paragraph of its own.</summary>
    public string Text(string text)
    {
        if (text.IndexOf(":::", StringComparison.Ordinal) < 0) return text;
        var edits = new List<(int Start, int End, string With)>();
        void Walk(IReadOnlyList<NoteContainer> list)
        {
            foreach (var c in list)
            {
                var token = Open + _boxes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + End;
                _boxes.Add((c.Type, c.Title));
                edits.Add((c.Start, c.BodyStart, "\n\n" + token + "\n\n"));
                edits.Add((c.BodyEnd, c.End, "\n\n" + Close + "\n\n"));
                Walk(c.Children);
            }
        }
        Walk(NoteContainers.Parse(text));
        if (edits.Count == 0) return text;
        var sb = new StringBuilder(text);
        foreach (var (start, end, with) in edits.OrderByDescending(e => e.Start))
            sb.Remove(start, end - start).Insert(start, with);
        return sb.ToString();
    }

    /// <summary>The rendered page with the marker paragraphs as boxes; a marker anywhere else (an excerpt, an attribute) goes.</summary>
    public string Finish(string html, bool preview)
    {
        if (html.IndexOf(Open) < 0 && html.IndexOf(Close) < 0) return html;
        int depth = 0, used = 0;
        html = MarkerParagraph().Replace(html, m =>
        {
            if (m.Groups[1].Success)
            {
                if (!int.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var i) || i >= _boxes.Count) return "";
                depth++;
                used++;
                return Head(_boxes[i].Type, _boxes[i].Title);
            }
            if (depth == 0) return "";
            depth--;
            return "</div>";
        });
        html = StrayMarker().Replace(html, "");
        if (used == 0) return html;
        var extra = "<style>" + Css + "</style>" + (preview ? "<script>" + Script + "</script>" : "") + "\n";
        var at = html.LastIndexOf("</body>", StringComparison.Ordinal);
        // a box left open by a cut text closes before the page's end
        var closing = string.Concat(Enumerable.Repeat("</div>", depth));
        return at < 0 ? html + closing + extra : html.Insert(at, closing + extra);
    }

    /// <summary>The opening of a box: its type as a class and a header with the type and the title.</summary>
    public static string Head(string type, string title)
    {
        var t = PageMarkdown.Esc(type);
        return "<div class=\"urbe-box urbe-box-" + t + "\" data-type=\"" + t + "\"><div class=\"urbe-box-head\"><span class=\"urbe-box-type\">" + t + "</span>" +
               (title.Length > 0 ? "<span class=\"urbe-box-title\">" + PageMarkdown.Esc(title) + "</span>" : "") + "</div>";
    }

    private const string Css = ".urbe-box{margin:1em 0;padding:.6em .9em;border:1px solid color-mix(in srgb,currentColor 18%,transparent);border-left:4px solid var(--accent,#c58a2b);border-radius:8px;background:color-mix(in srgb,currentColor 4%,transparent)}" +
        ".urbe-box>.urbe-box-head{display:flex;gap:.5em;align-items:baseline;margin-bottom:.35em;font-weight:600}" +
        ".urbe-box-type{font-size:.72em;letter-spacing:.06em;text-transform:uppercase;opacity:.7}.urbe-box>:last-child{margin-bottom:0}";

    // in the studio a text with a box is not edited in place (its ::: lines would be lost); the panel edits it
    private const string Script = "document.querySelectorAll('.urbe-box').forEach(function(b){var e=b.closest('[data-edit]');if(e)e.removeAttribute('data-edit')});";

    [GeneratedRegex("<p>\\s*(?:(\\d+)|)\\s*</p>")]
    private static partial Regex MarkerParagraph();

    [GeneratedRegex("\\d*?|[]")]
    private static partial Regex StrayMarker();
}
