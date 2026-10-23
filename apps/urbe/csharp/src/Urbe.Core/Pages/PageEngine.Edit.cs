using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Urbe.Core.Pages;

/// <summary>
/// UC-62 slice 4 (DEC-0055): in the studio preview every plain one-line text of a section (titles, subtitles of one
/// line, labels, the items' titles) is written in place, as the free sections' texts already were. Before the render
/// each such field gets an invisible opening and closing mark with its index; after it, a pair found in the page's text
/// becomes an editable span with the field's dotted path (a pair inside a tag, the title, a script or a style is only
/// cut out). The span tells the studio its new text when it loses the focus. Texts with Markdown, comments or Urbe
/// marks are left to the Editar panel: their rendered words are not their source.
/// </summary>
internal sealed class PageEditMarks(PageEngine engine)
{
    private const char Open = '', Close = '', First = '', Last = '';
    private const string Special = "*_`[]<>#|{}%&~\\$\n\r\t";
    private readonly List<string> _paths = [];

    /// <summary>True for a text that reads the same rendered and written: one line, no Markdown, no marks.</summary>
    public static bool Plain(string text) => text.Length is > 0 and <= 300 && text.IndexOfAny(Special.ToCharArray()) < 0 && text.All(c => c < '' || c > '');

    /// <summary>A copy of the page with each plain text field of its sections between marks.</summary>
    public JsonNode? Page(JsonNode? input)
    {
        if (input?.DeepClone() is not JsonObject page || page["sections"] is not JsonArray sections) return input;
        for (int i = 0; i < sections.Count; i++)
        {
            if (sections[i] is not JsonObject sec || sec["props"] is not JsonObject props || (string?)sec["type"] == "free") continue;
            if (engine.BlockInfo((string?)sec["type"]) is not { } info) continue;
            Mark(props, info.Fields, "sections." + i.ToString(CultureInfo.InvariantCulture) + ".props");
        }
        return page;
    }

    private void Mark(JsonObject props, JsonArray fields, string at)
    {
        foreach (var f in fields.OfType<JsonObject>())
        {
            var key = (string?)f["key"];
            if (key is null || key == "icon" || key.Contains('.')) continue;
            var type = (string?)f["type"];
            if (type == "text" && props[key] is JsonValue v && v.TryGetValue<string>(out var text) && Plain(text) && First + _paths.Count <= Last)
            {
                props[key] = Open.ToString() + (char)(First + _paths.Count) + text + Close;
                _paths.Add(at + "." + key);
            }
            else if (type == "list" && props[key] is JsonArray items && f["fields"] is JsonArray sub)
                for (int j = 0; j < items.Count; j++)
                    if (items[j] is JsonObject item) Mark(item, sub, at + "." + key + "." + j.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>The rendered page with each marked text as an editable span (or bare, where a span cannot go).</summary>
    public string Finish(string html)
    {
        if (html.IndexOf(Open) < 0) return html;
        var sb = new StringBuilder(html.Length + _paths.Count * 64);
        bool inTag = false;
        string? raw = null;
        int made = 0;
        for (int i = 0; i < html.Length; i++)
        {
            var c = html[i];
            if (c == Open)
            {
                int index = i + 1 < html.Length && html[i + 1] is >= First and <= Last ? html[i + 1] - First : -1;
                if (index >= 0) i++;
                int close = html.IndexOf(Close, i + 1);
                // a span only around plain text, outside any tag: the words between the marks hold no markup
                if (!inTag && raw is null && index >= 0 && index < _paths.Count && close > i && html.IndexOfAny(['<', '>', Open], i + 1, close - i - 1) < 0)
                {
                    sb.Append("<span class=\"urbe-ed\" contenteditable=\"true\" spellcheck=\"true\" data-path=\"").Append(PageMarkdown.Esc(_paths[index])).Append("\">")
                      .Append(html, i + 1, close - i - 1).Append("</span>");
                    i = close;
                    made++;
                }
                continue;
            }
            if (c == Close) continue;
            sb.Append(c);
            if (raw is not null)
            {
                if (c == '<' && string.Compare(html, i, "</" + raw, 0, raw.Length + 2, StringComparison.OrdinalIgnoreCase) == 0) { raw = null; inTag = true; }
            }
            else if (inTag) { if (c == '>') inTag = false; }
            else if (c == '<')
            {
                inTag = true;
                foreach (var name in (string[])["title", "script", "style", "textarea"])
                    if (string.Compare(html, i + 1, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0 && i + 1 + name.Length < html.Length &&
                        html[i + 1 + name.Length] is '>' or ' ' or '\t' or '\n' or '/' && html.IndexOf('>', i) is var end and > 0)
                    {
                        sb.Append(html, i + 1, end - i);
                        i = end;
                        inTag = false;
                        raw = name;
                        break;
                    }
            }
        }
        var output = sb.ToString();
        if (made == 0) return output;
        var at = output.LastIndexOf("</body>", StringComparison.Ordinal);
        var extra = "<style>" + Css + "</style><script>" + Script + "</script>\n";
        return at < 0 ? output + extra : output.Insert(at, extra);
    }

    private const string Css = ".urbe-ed{outline:none;border-radius:3px;cursor:text;transition:box-shadow .12s}" +
        ".urbe-ed:hover{box-shadow:0 0 0 1px color-mix(in srgb,currentColor 30%,transparent)}.urbe-ed:focus{box-shadow:0 0 0 2px var(--accent,#c58a2b)}";

    // the text written in place goes to the studio when the span loses the focus; Enter ends it (one line)
    private const string Script = "(function(){var d=document;d.querySelectorAll('.urbe-ed').forEach(function(n){n._was=n.textContent});" +
        "function ed(n){return n&&n.classList&&n.classList.contains('urbe-ed')}" +
        "addEventListener('focusout',function(e){var n=e.target;if(!ed(n))return;var t=n.textContent.replace(/\\s+/g,' ').trim();if(!t.length){n.textContent=n._was;return}" +
        "if(t!==n._was){n._was=t;parent.postMessage({urbePage:'field',path:n.dataset.path,text:t},'*')}},true);" +
        "addEventListener('keydown',function(e){var n=e.target;if(ed(n)&&e.key==='Enter'){e.preventDefault();n.blur()}if(ed(n)&&e.key==='Escape'){n.textContent=n._was;n.blur()}},true);" +
        "addEventListener('click',function(e){var n=e.target.closest&&e.target.closest('.urbe-ed');if(n&&n.closest('a'))e.preventDefault()},true)})();";
}
