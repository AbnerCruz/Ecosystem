using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core.Pages;

/// <summary>
/// The Urbe's own .md marks in pages (DEC-0045, UC-43/49/51), kept out of the 1.8.4 engine: the page's texts and notes
/// are rewritten before <see cref="PageEngine.Render(JsonNode?, IPageNotes?, bool)"/> and its HTML finished after.
/// Exported HTML leaves comments out (<see cref="NoteMarks.StripComments"/>); the studio preview shows each
/// <c>palavra{"…"}</c> as a 💬 after the word that opens its comments.
/// </summary>
internal sealed class PageCommentMarks(bool show)
{
    // a comment run becomes Open + one char of its index: survives Markdown, escaping and slugs, and is cut out whole
    private const char Open = '';
    private const char First = '', Last = '';
    // UC-82: the Assistente's mark {ia:"modelo"} becomes AiOpen + one char of the model's index, finished as a small "IA"
    private const char AiOpen = '\ue04d';
    // raw text: fields that are code (blocks, CSS, the <head>) are never touched
    private static readonly HashSet<string> RawKeys = new(StringComparer.Ordinal) { "code", "css", "head" };

    // each 💬: its comments and where its text lives (a page field by dotted path, or a note), with the group's index there
    private readonly List<(IReadOnlyList<string> Comments, string? Field, string? Note, int N)> _groups = [];
    private readonly PageBoxMarks _boxes = new();
    // the models whose marks the page has, in the order they first appear
    private readonly List<string> _models = [];

    public string Text(string? markdown, string? field = null, string? note = null)
    {
        var text = markdown ?? "";
        if (show)
        {
            var groups = NoteMarks.CommentGroups(text);
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                var g = groups[i];
                string token = "";
                if (First + _groups.Count <= Last)
                {
                    token = Open.ToString() + (char)(First + _groups.Count);
                    _groups.Add((g.Comments, field, note, i));
                }
                text = text[..g.Start] + token + text[(g.Start + g.Length)..];
            }
        }
        return _boxes.Text(NoteMarks.StripComments(text, AiToken));
    }

    private string AiToken(string model)
    {
        int index = _models.IndexOf(model);
        if (index < 0)
        {
            if (First + _models.Count > Last) return "";
            index = _models.Count;
            _models.Add(model);
        }
        return AiOpen.ToString() + (char)(First + index);
    }

    /// <summary>A copy of the page with every text field rewritten.</summary>
    public JsonNode? Page(JsonNode? input) => Map(input?.DeepClone(), null, null, true);

    // path: the dotted path of the node in the page (PageStudio.Get/Set); a key with a dot cannot be written back by it
    private JsonNode? Map(JsonNode? node, string? key, string? path, bool writable)
    {
        static string At(string? path, string k) => path is null ? k : path + "." + k;
        switch (node)
        {
            case JsonObject o:
                foreach (var name in o.Select(p => p.Key).ToList())
                    if (Map(o[name], name, At(path, name), writable && name.Length > 0 && !name.Contains('.')) is var m && !ReferenceEquals(m, o[name])) o[name] = m;
                return o;
            case JsonArray a:
                for (int i = 0; i < a.Count; i++)
                    if (Map(a[i], key, At(path, i.ToString(System.Globalization.CultureInfo.InvariantCulture)), writable) is var m && !ReferenceEquals(m, a[i])) a[i] = m;
                return a;
            case JsonValue v when v.GetValueKind() == JsonValueKind.String && (key is null || !RawKeys.Contains(key)):
                var s = (string)v!;
                var t = Text(s, writable ? path : null);
                return t == s ? v : JsonValue.Create(t);
            default:
                return node;
        }
    }

    public IPageNotes? Notes(IPageNotes? notes) => notes is null ? null : new Rewritten(notes, this);

    private sealed class Rewritten(IPageNotes inner, PageCommentMarks marks) : IPageNotes
    {
        private readonly Dictionary<string, PageNote?> _byPath = new(StringComparer.Ordinal);
        private IReadOnlyList<PageNote>? _all;

        private PageNote Of(PageNote n)
        {
            if (_byPath.TryGetValue(n.Path, out var done) && done is not null) return done;
            var r = n with { Content = marks.Text(n.Content, note: n.Path) };
            _byPath[n.Path] = r;
            return r;
        }

        public PageNote? Get(string path) => inner.Get(path) is { } n ? Of(n) : null;
        public IReadOnlyList<PageNote> List() => _all ??= inner.List().Select(Of).ToList();
    }

    /// <summary>The rendered page with each comment run as its 💬 (text) or gone (inside tags, the title, scripts and styles).</summary>
    public string Finish(string html) => _boxes.Finish(FinishComments(html), show);

    private string FinishComments(string html)
    {
        if (html.IndexOf(Open) < 0 && html.IndexOf(AiOpen) < 0) return html;
        var sb = new StringBuilder(html.Length + 256);
        bool inTag = false;
        string? raw = null; // the raw-text element we are inside (title, script, style, textarea)
        int used = 0, marked = 0;
        for (int i = 0; i < html.Length; i++)
        {
            var c = html[i];
            if (c == AiOpen)
            {
                // UC-82: a small "IA" with the model in its title; inside a tag or the title, nothing
                int model = i + 1 < html.Length && html[i + 1] is >= First and <= Last ? html[i + 1] - First : -1;
                if (model >= 0) i++;
                if (!inTag && raw is null && model >= 0 && model < _models.Count) { sb.Append(AiMarker(_models[model], show)); marked++; }
                continue;
            }
            if (c == Open)
            {
                // a run cut in half (an excerpt) leaves only Open: it never reaches the page either
                int index = i + 1 < html.Length && html[i + 1] is >= First and <= Last ? html[i + 1] - First : -1;
                if (index >= 0) i++;
                if (!inTag && raw is null && index >= 0 && index < _groups.Count) { var g = _groups[index]; sb.Append(Marker(g.Comments, g.Field, g.Note, g.N)); used++; }
                continue;
            }
            sb.Append(c);
            if (raw is not null)
            {
                if (c == '<' && string.Compare(html, i, "</" + raw, 0, raw.Length + 2, StringComparison.OrdinalIgnoreCase) == 0) { raw = null; inTag = true; }
            }
            else if (inTag) { if (c == '>') inTag = false; }
            else if (c == '<')
            {
                inTag = true;
                if (RawElement(html, i) is { } name && html.IndexOf('>', i) is var end and > 0)
                {
                    sb.Append(html, i + 1, end - i);
                    i = end;
                    inTag = false;
                    raw = name;
                }
            }
        }
        var output = sb.ToString();
        if (used == 0 && marked == 0) return output;
        var at = output.LastIndexOf("</body>", StringComparison.Ordinal);
        var extra = (marked > 0 ? AiNote(_models) + "<style>" + AiCss + "</style>" : "") +
                    (used > 0 || (show && marked > 0) ? "<style>" + Css + "</style><script>" + Script + "</script>" : "") + "\n";
        return at < 0 ? output + extra : output.Insert(at, extra);
    }

    private static string? RawElement(string html, int at)
    {
        foreach (var name in (string[])["title", "script", "style", "textarea"])
            if (string.Compare(html, at + 1, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
                at + 1 + name.Length < html.Length && html[at + 1 + name.Length] is '>' or ' ' or '\t' or '\n' or '/')
                return name;
        return null;
    }

    /// <summary>
    /// The 💬 after the word: the count when more than one, the comments in its title and data-comments; with its place
    /// (data-field: the page's dotted path, or data-note) and the group's index there (data-n), its popover can comment.
    /// </summary>
    public static string Marker(IReadOnlyList<string> comments, string? field = null, string? note = null, int n = 0)
    {
        var json = JsonSerializer.Serialize(comments);
        var source = string.Concat(comments.Select(NoteMarks.Quote));
        var place = field is not null ? " data-field=\"" + PageMarkdown.Esc(field) + "\"" : note is not null ? " data-note=\"" + PageMarkdown.Esc(note) + "\"" : "";
        if (place.Length > 0) place += " data-n=\"" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"";
        return "<sup class=\"urbe-cm\" role=\"button\" tabindex=\"0\" title=\"" + PageMarkdown.Esc(string.Join("\n", comments)) +
               "\" data-comments=\"" + PageMarkdown.Esc(json) + "\" data-src=\"" + PageMarkdown.Esc(source) + "\"" + place + ">💬" +
               (comments.Count > 1 ? comments.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : "") + "</sup>";
    }

    /// <summary>UC-82: the Assistente's mark in a page — a small "IA" in superscript, the model in its title.</summary>
    /// In the studio preview it keeps its source, so a text written in place gets <c>{ia:"modelo"}</c> back.
    public static string AiMarker(string model, bool preview = false) =>
        "<sup class=\"urbe-ia\" title=\"Escrito com IA (" + PageMarkdown.Esc(model) + ")\"" +
        (preview ? " data-src=\"" + PageMarkdown.Esc("{ia:\"" + model + "\"}") + "\"" : "") + ">IA</sup>";

    /// <summary>UC-82: the page's foot says that parts of it were written with AI, and with which models.</summary>
    public static string AiNote(IReadOnlyList<string> models) =>
        "<p class=\"urbe-ia-note\">Partes deste documento foram escritas com IA (" + PageMarkdown.Esc(string.Join(", ", models)) + ").</p>";

    private const string AiCss = ".urbe-ia{font-size:.55em;line-height:0;margin-left:.15em;vertical-align:super;font-weight:600;letter-spacing:.02em;opacity:.55;cursor:help;user-select:none;-webkit-user-select:none}" +
        ".urbe-ia-note{max-width:72ch;margin:32px auto 24px;padding:0 16px;font:13px/1.4 system-ui,sans-serif;opacity:.6;text-align:center}";

    private const string Css = ".urbe-cm{cursor:pointer;font-size:.62em;line-height:0;margin-left:.08em;vertical-align:super;font-weight:600;color:var(--accent,#c58a2b);user-select:none;-webkit-user-select:none}" +
        ".urbe-cm-pop{position:absolute;z-index:2147483000;max-width:min(320px,calc(100vw - 24px));max-height:240px;overflow:auto;padding:10px 12px;border-radius:10px;" +
        "background:var(--surface,#fff);color:var(--text,#1d1d1f);border:1px solid color-mix(in srgb,currentColor 18%,transparent);box-shadow:0 8px 28px rgba(0,0,0,.25);font:14px/1.45 system-ui,sans-serif;text-align:left}" +
        ".urbe-cm-pop b{display:block;font-size:12px;opacity:.7;margin-bottom:6px}.urbe-cm-pop p{margin:0 0 6px;white-space:pre-wrap;overflow-wrap:anywhere}.urbe-cm-pop p:last-child{margin:0}" +
        ".urbe-cm-add{display:block;margin:8px 0 0 auto;padding:6px 14px;border-radius:8px;border:0;background:var(--accent,#c58a2b);color:#fff;font:600 13px system-ui,sans-serif;cursor:pointer}";

    // tapping a 💬 opens its comments (window capture runs before the studio's selection script); while a text is being
    // written in place its 💬 goes back to {"…"}, so the comments are kept in what the studio receives
    private const string Script = "(function(){var d=document,pop=null;function close(){if(pop){pop.remove();pop=null}}" +
        "function open(m){close();var list=[];try{list=JSON.parse(m.dataset.comments||'[]')}catch(e){}pop=d.createElement('div');pop.className='urbe-cm-pop';pop.setAttribute('role','dialog');" +
        "var h=d.createElement('b');h.textContent=list.length>1?list.length+' comentários':'Comentário';pop.appendChild(h);list.forEach(function(t){var p=d.createElement('p');p.textContent=t;pop.appendChild(p)});" +
        "if(m.dataset.field||m.dataset.note){var a=d.createElement('button');a.type='button';a.className='urbe-cm-add';a.textContent='Comentar';pop.appendChild(a)}" +
        "d.body.appendChild(pop);var r=m.getBoundingClientRect(),w=pop.offsetWidth,x=Math.max(12,Math.min(r.left+scrollX-w/2,scrollX+innerWidth-w-12));pop.style.left=x+'px';pop.style.top=(r.bottom+scrollY+6)+'px';pop._for=m}" +
        "addEventListener('click',function(e){var m=e.target.closest&&e.target.closest('.urbe-cm');if(m&&!m.closest('[contenteditable=true]')){e.preventDefault();e.stopPropagation();if(pop&&pop._for===m)close();else open(m);return}" +
        "if(pop){var add=e.target.closest&&e.target.closest('.urbe-cm-add');if(add&&pop._for){var f=pop._for.dataset,q={urbePage:'comment',n:+(f.n||0)};if(f.field)q.field=f.field;else q.note=f.note;close();e.preventDefault();parent.postMessage(q,'*')}" +
        "else if(!(e.target.closest&&e.target.closest('.urbe-cm-pop'))){close();e.preventDefault()}e.stopPropagation()}},true);" +
        "addEventListener('keydown',function(e){var m=e.target.closest&&e.target.closest('.urbe-cm');if(m&&(e.key==='Enter'||e.key===' ')){e.preventDefault();open(m)}if(e.key==='Escape')close()},true);" +
        "addEventListener('scroll',function(){if(pop&&pop._for&&!pop._for.isConnected)close()},{passive:true});" +
        "function raw(n){n.querySelectorAll('.urbe-cm,.urbe-ia').forEach(function(m){m.replaceWith(d.createTextNode(m.dataset.src||''))})}" +
        "addEventListener('focusin',function(e){var n=e.target;if(n&&n.isContentEditable){close();raw(n)}},true);" +
        "addEventListener('focusout',function(e){var n=e.target;if(n&&n.isContentEditable)raw(n)},true)})();";
}

public sealed partial class PageEngine
{
    /// <summary>
    /// The Urbe's own marks of the notes (DEC-0045): on, <c>%%…%%</c> and <c>{"…"}</c> comments are left out of the
    /// page and <c>{ia:"modelo"}</c> shows as "· IA: modelo"; in the studio preview (<c>preview</c>) each word comment
    /// shows as a 💬 that opens it (UC-49); <c>:::tipo título</c> … <c>:::</c> containers are boxes (UC-44). Off (the default, and the oracles) renders the texts as the 1.8.4 does.
    /// </summary>
    public bool UrbeMarks
    {
        get => _urbeMarks;
        set { _urbeMarks = value; if (value) AddVaultQueries(); }
    }

    private bool _urbeMarks;
}

/// <summary>
/// Writing a comment from the studio preview (UC-49): a 💬 points to the n-th <c>palavra{"…"}</c> group of its text
/// (a page field or a note); the new comment goes right after that group's last one, in the text itself.
/// </summary>
public static class PageComments
{
    /// <summary>The n-th comment group of the text, or null when the text changed and it is gone.</summary>
    public static NoteMarks.CommentGroup? At(string? text, int n)
    {
        var groups = NoteMarks.CommentGroups(text);
        return n >= 0 && n < groups.Count ? groups[n] : null;
    }

    /// <summary>The text with <paramref name="comment"/> added to its n-th group, or null (no such group, or nothing to add).</summary>
    public static string? Add(string? text, int n, string? comment)
    {
        var c = (comment ?? "").Trim();
        return c.Length > 0 && At(text, n) is { } g ? NoteMarks.AddComment(text!, g, c) : null;
    }
}
