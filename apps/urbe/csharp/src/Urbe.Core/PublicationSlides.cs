using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>One slide cut from a note: its Markdown and its speaker notes (a <c>:::notas</c> container).</summary>
public sealed record SlideSegment(string Markdown, string Notes);

/// <summary>
/// CST-167: an interactive HTML presentation from the publication's live notes, in one self-contained file.
/// A note, a # or ## heading and a --- line (with a blank line before it) start a slide, as in the PPTX
/// (<see cref="PublicationPptx"/>). Each slide is a 16:9 box whose text scales with it (container units), so the
/// file reads well with scripts off; with them it presents one slide at a time (arrows, space, swipe, Home/End,
/// F full screen, O overview, N speaker notes, #n links), warns when a slide's content does not fit, and prints
/// one slide per page (PDF through the system's print). Text stays the note's: the deck is never a copy to edit.
/// </summary>
public static partial class PublicationSlides
{
    /// <summary>The slides of one note's body (frontmatter and comments already removed).</summary>
    public static IReadOnlyList<SlideSegment> Split(string body)
    {
        var lines = body.Replace("\r", "").Split('\n');
        var segments = new List<string>();
        var current = new StringBuilder();
        string? fence = null;
        int depth = 0;
        bool hasContent = false;
        void Cut()
        {
            if (hasContent) segments.Add(current.ToString());
            current.Clear(); hasContent = false;
        }
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (fence is not null)
            {
                if (trimmed.StartsWith(fence, StringComparison.Ordinal) && trimmed.TrimStart(fence[0]).Length == 0) fence = null;
            }
            else if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal)) fence = trimmed[..3];
            else if (trimmed == ":::") depth = Math.Max(0, depth - 1);
            else if (ContainerOpen().IsMatch(trimmed)) depth++;
            else if (depth == 0 && SlideHeading().IsMatch(line)) Cut();
            else if (depth == 0 && Rule().IsMatch(line) && (i == 0 || lines[i - 1].Trim().Length == 0)) { Cut(); continue; }
            current.Append(line).Append('\n');
            if (trimmed.Length > 0) hasContent = true;
        }
        Cut();
        return segments.Select(Notes).ToList();
    }

    /// <summary>Takes the <c>:::notas</c> containers out of a slide's Markdown into its notes.</summary>
    private static SlideSegment Notes(string markdown)
    {
        var boxes = NoteContainers.Parse(markdown).Where(b => NoteContainers.IsSpeakerNotes(b.Type)).OrderByDescending(b => b.Start).ToList();
        if (boxes.Count == 0) return new SlideSegment(markdown, "");
        var notes = new List<string>();
        foreach (var box in boxes)
        {
            notes.Insert(0, markdown[box.BodyStart..box.BodyEnd]);
            markdown = markdown[..box.Start] + markdown[Math.Min(markdown.Length, box.End + 1)..];
        }
        return new SlideSegment(markdown, string.Join("\n\n", notes));
    }

    public static PublicationHtmlResult Export(PublicationProject project, PublicationSourceCatalog catalog, Func<string, bool, string> render)
    {
        // formulas are drawn at 18 px; on a slide they follow its text size (em), at any scale of the slide
        string renderMath(string tex, bool display) => SvgSize().Replace(render(tex, display), m =>
            $"{m.Groups[1].Value}width='{Em(m.Groups[2].Value)}em' height='{Em(m.Groups[3].Value)}em'", 1);
        var accent = project.Design.Theme switch { "ocean" => "#075985", "ink" => "#312e81", _ => "#6b4f2a" };
        var title = MarkdownEngine.EscapeHtml(project.Title);
        var missing = new List<string>();
        var slides = new List<(string Html, string Notes, string Source)>();
        slides.Add(("<h1 class=\"deck-title\">" + title + "</h1>", "", ""));
        foreach (var placement in project.Sections)
        {
            var source = catalog.Read(placement.Source);
            if (source is null)
            {
                if (!missing.Contains(placement.Source.DocumentId, StringComparer.Ordinal)) missing.Add(placement.Source.DocumentId);
                slides.Add(("<p class=\"missing\" role=\"status\">Fonte indisponível para esta publicação.</p>", "", ""));
                continue;
            }
            var body = NoteMarks.StripComments(MarkdownEngine.SplitFrontmatter(source.Markdown).Body);
            string Render(string md) => NoteProgress.RenderHtml(NoteContainers.RenderHtml(md, m => MarkdownEngine.Render(m, renderMath)), source.Markdown,
                id => catalog.Read(new PublicationSourceReference(id))?.Markdown);
            foreach (var segment in Split(body))
                slides.Add((Render(segment.Markdown), segment.Notes.Trim().Length > 0 ? Render(segment.Notes) : "", Path.GetFileNameWithoutExtension(source.Path)));
        }

        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"pt-BR\"><head><meta charset=\"utf-8\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">")
          .Append("<title>").Append(title).Append("</title><style>")
          .Append(":root{color-scheme:light;--accent:").Append(accent).Append("}*{box-sizing:border-box}")
          .Append("body{margin:0;background:#2b2b2b;color:#25272c;font-family:system-ui,sans-serif}")
          .Append(".deck{display:flex;flex-direction:column;align-items:center;gap:24px;padding:24px 12px}")
          // the slide: 16:9, its text in container units so it scales with the slide on any screen
          .Append(".slide{position:relative;width:min(100%,1280px);aspect-ratio:16/9;background:#fff;border-radius:6px;overflow:hidden;container-type:inline-size;box-shadow:0 4px 18px #0006}")
          .Append(".slide-body{position:absolute;inset:4cqw 5cqw 5cqw;overflow:hidden;font-size:2.1cqw;line-height:1.4}")
          .Append(".slide h1,.slide h2{color:var(--accent);font-size:3.6cqw;margin:0 0 2cqw;line-height:1.15}.slide h3{font-size:2.6cqw;margin:1.2cqw 0}")
          .Append(".slide p,.slide li{margin:.6cqw 0}.slide ul,.slide ol{padding-left:3cqw}.slide pre{font-size:1.6cqw;background:#f3f1ec;padding:1cqw;border-radius:.6cqw;overflow:hidden;white-space:pre-wrap}")
          .Append(".slide img,.slide video{max-width:100%;max-height:30cqw;height:auto}.slide svg{max-width:100%;vertical-align:middle}.slide table{border-collapse:collapse}.slide td,.slide th{border:1px solid #999;padding:.4cqw .8cqw}")
          .Append(".slide blockquote{border-left:.4cqw solid #999;margin:1cqw 0;padding-left:1.5cqw;color:#555}")
          .Append(".ubox{border-left:.5cqw solid #9c7b45;background:#f6f3ee;padding:.8cqw 1.5cqw;margin:1cqw 0;border-radius:.6cqw}.ubox-title{margin:0 0 .4cqw}")
          .Append(".umath-block{display:block;text-align:center}.missing{border:1px dashed #a35;padding:1cqw}")
          .Append(".deck-title{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;text-align:center;font-size:5cqw!important;padding:0 6cqw}")
          .Append(".slide-no{position:absolute;right:2cqw;bottom:1.2cqw;font-size:1.2cqw;color:#888}")
          .Append(".overflow-badge{display:none;position:absolute;left:2cqw;bottom:1.2cqw;font-size:1.2cqw;background:#b45309;color:#fff;padding:.2cqw .8cqw;border-radius:.4cqw}.slide.overflow .overflow-badge{display:block}")
          .Append(".notes{width:min(100%,1280px);background:#fffbe6;border-radius:6px;padding:8px 16px;font-size:15px;margin-top:-16px}.notes::before{content:'Notas do apresentador';display:block;font-weight:600;font-size:12px;color:#7a6a3a}")
          .Append("progress{width:100%;accent-color:var(--accent)}a{color:var(--accent)}")
          // presenting (script on): one slide fills the screen; notes only when asked (N)
          .Append(".presenting{background:#000}.presenting .deck{padding:0;height:100vh;justify-content:center}.presenting .slide{display:none;width:min(100vw,177.78vh);border-radius:0;box-shadow:none}")
          .Append(".presenting .slide.current{display:block;animation:fade .25s ease}.presenting .notes{display:none}.presenting.show-notes .notes.current{display:block;position:fixed;left:0;right:0;bottom:0;margin:0;width:auto;max-height:35vh;overflow:auto;border-radius:0}")
          .Append(".presenting.overview .deck{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));height:auto;padding:16px;gap:16px}.presenting.overview .slide{display:block;width:100%;cursor:pointer}")
          .Append("@keyframes fade{from{opacity:.4}to{opacity:1}}@media (prefers-reduced-motion:reduce){.presenting .slide.current{animation:none}}")
          .Append(".controls{position:fixed;right:12px;bottom:12px;display:flex;gap:6px;z-index:5}.controls button{font:inherit;font-size:14px;border:0;border-radius:6px;padding:8px 12px;background:#ffffffd9;cursor:pointer}")
          .Append("@media print{@page{size:320mm 180mm;margin:0}body{background:#fff}.deck{display:block;padding:0}.slide{width:320mm;height:180mm;aspect-ratio:auto;border-radius:0;box-shadow:none;break-after:page}.notes,.controls,.overflow-badge{display:none!important}}")
          .Append("</style></head><body><main class=\"deck\" aria-label=\"Apresentação\">");
        for (int i = 0; i < slides.Count; i++)
        {
            var (html, notes, from) = slides[i];
            sb.Append("<section class=\"slide\" id=\"s").Append(i + 1).Append("\" tabindex=\"0\" aria-roledescription=\"slide\" aria-label=\"Slide ").Append(i + 1).Append(" de ").Append(slides.Count)
              .Append(from.Length > 0 ? " — " + MarkdownEngine.EscapeHtml(from) : "").Append("\"><div class=\"slide-body\">").Append(html).Append("</div>")
              .Append("<span class=\"overflow-badge\">O conteúdo não cabe neste slide</span><span class=\"slide-no\">").Append(i + 1).Append("</span></section>");
            if (notes.Length > 0) sb.Append("<aside class=\"notes\" data-for=\"s").Append(i + 1).Append("\">").Append(notes).Append("</aside>");
        }
        sb.Append("</main><div class=\"controls\" hidden><button id=\"prev\" aria-label=\"Slide anterior\">←</button><button id=\"next\" aria-label=\"Próximo slide\">→</button>")
          .Append("<button id=\"present\" aria-label=\"Apresentar\">Apresentar</button></div><script>").Append(Script).Append("</script></body></html>");
        return new PublicationHtmlResult(sb.ToString(), missing.AsReadOnly());
    }

    /// <summary>The presenting behaviour; the deck without it is a readable list of slides.</summary>
    private const string Script = """
(function(){var d=document,b=d.body,s=[].slice.call(d.querySelectorAll('.slide')),n=0;
function notes(){[].forEach.call(d.querySelectorAll('.notes'),function(x){x.classList.toggle('current',x.dataset.for===s[n].id)})}
function go(i){n=Math.max(0,Math.min(s.length-1,i));s.forEach(function(x,k){x.classList.toggle('current',k===n)});notes();if(location.hash!=='#'+s[n].id)history.replaceState(null,'','#'+s[n].id);if(b.classList.contains('presenting'))s[n].focus({preventScroll:true})}
function present(on){b.classList.toggle('presenting',on);b.classList.remove('overview');go(n)}
function fits(){s.forEach(function(x){var c=x.querySelector('.slide-body');x.classList.toggle('overflow',c.scrollHeight>c.clientHeight+2||c.scrollWidth>c.clientWidth+2)})}
d.querySelector('.controls').hidden=false;
d.getElementById('prev').onclick=function(){go(n-1)};d.getElementById('next').onclick=function(){go(n+1)};
d.getElementById('present').onclick=function(){present(!b.classList.contains('presenting'))};
s.forEach(function(x,k){x.addEventListener('click',function(){if(b.classList.contains('overview')){b.classList.remove('overview');go(k)}})});
d.addEventListener('keydown',function(e){if(e.altKey||e.ctrlKey||e.metaKey)return;var k=e.key;
if(k==='ArrowRight'||k==='PageDown'||k===' '){if(!b.classList.contains('presenting'))present(true);else go(n+1);e.preventDefault()}
else if(k==='ArrowLeft'||k==='PageUp'){go(n-1);e.preventDefault()}else if(k==='Home')go(0);else if(k==='End')go(s.length-1);
else if(k==='Escape'){if(b.classList.contains('overview'))b.classList.remove('overview');else present(false)}
else if(k==='f'||k==='F'){if(!d.fullscreenElement&&d.documentElement.requestFullscreen){present(true);d.documentElement.requestFullscreen()}else if(d.exitFullscreen)d.exitFullscreen()}
else if(k==='o'||k==='O'){if(!b.classList.contains('presenting'))present(true);b.classList.toggle('overview')}
else if(k==='n'||k==='N'){b.classList.toggle('show-notes')}});
var x0=null;d.addEventListener('touchstart',function(e){x0=e.touches[0].clientX},{passive:true});
d.addEventListener('touchend',function(e){if(x0===null||!b.classList.contains('presenting'))return;var dx=e.changedTouches[0].clientX-x0;if(Math.abs(dx)>40)go(n+(dx<0?1:-1));x0=null},{passive:true});
var h=s.findIndex(function(x){return '#'+x.id===location.hash});if(h>=0){n=h;present(true)}else go(0);
fits();addEventListener('resize',fits);addEventListener('load',fits)})();
""";

    private static string Em(string px) =>
        (double.Parse(px, System.Globalization.CultureInfo.InvariantCulture) / 18).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(<svg\b[^>]*?)width='([0-9.]+)' height='([0-9.]+)'")]
    private static partial Regex SvgSize();
    [GeneratedRegex(@"^ {0,3}#{1,2}\s+\S")]
    private static partial Regex SlideHeading();
    [GeneratedRegex(@"^ {0,3}(-{3,}|\*{3,}|_{3,})\s*$")]
    private static partial Regex Rule();
    [GeneratedRegex(@"^:::[A-Za-z0-9_-]+")]
    private static partial Regex ContainerOpen();
}
