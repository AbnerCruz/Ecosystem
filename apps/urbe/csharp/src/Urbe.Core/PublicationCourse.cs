using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>
/// CST-174: a course as a product, from the publication's live notes — course → module (a note) → lesson (each ##
/// section; the text before the first one opens the module) → activities (<c>```quiz</c> blocks). A module's
/// <c>objetivos</c> property is shown as its learning objectives. One self-contained HTML file: a contents panel,
/// one lesson at a time with previous/next, quizzes that answer at once, and the learner's progress (lessons
/// finished, questions right) kept in the browser apart from the content — never written into the notes. With
/// scripts off every lesson reads in order. SCORM/xAPI packages are not made: they need LMS conformance tests first.
/// </summary>
public static partial class PublicationCourse
{
    public sealed record Lesson(string Id, string Title, string Html);
    public sealed record Module(string Title, IReadOnlyList<string> Objectives, IReadOnlyList<Lesson> Lessons);

    public static IReadOnlyList<Module> Modules(PublicationProject project, PublicationSourceCatalog catalog, Func<string, string?>? data, List<string> missing)
    {
        var modules = new List<Module>();
        int m = 0;
        foreach (var placement in project.Sections)
        {
            m++;
            var source = catalog.Read(placement.Source);
            if (source is null)
            {
                if (!missing.Contains(placement.Source.DocumentId)) missing.Add(placement.Source.DocumentId);
                modules.Add(new Module("Módulo indisponível", [], [new Lesson("m" + m + "-1", "Fonte indisponível", "<p class=\"missing\" role=\"status\">Fonte indisponível para este curso.</p>")]));
                continue;
            }
            var parts = MarkdownEngine.SplitFrontmatter(source.Markdown);
            var objectives = parts.Fields.Where(f => f.Key is "objetivos" or "objetivo" or "objectives").SelectMany(f => SplitList(f.Value)).ToList();
            var body = NoteMarks.StripComments(parts.Body);
            string title = Path.GetFileNameWithoutExtension(source.Path);
            if (H1Re().Match(body) is { Success: true } h1) { title = h1.Groups[1].Value.Trim(); body = body.Remove(h1.Index, h1.Length); }
            string Render(string md) => NoteProgress.RenderHtml(QuizBlock.RenderHtml(Data.DataBlock.RenderHtml(Calculus.Calculator.RenderHtml(FunctionPlot.RenderHtml(
                NoteContainers.RenderHtml(md, x => MarkdownEngine.Render(x, PublicationHtml.RenderMath)))), data ?? (_ => null)), interactive: true),
                source.Markdown, id => catalog.Read(new PublicationSourceReference(id))?.Markdown);
            var lessons = new List<Lesson>();
            foreach (var (lessonTitle, md) in Lessons(body))
            {
                if (md.Trim().Length == 0 && lessonTitle is null) continue;
                lessons.Add(new Lesson("m" + m + "-" + (lessons.Count + 1), lessonTitle ?? (lessons.Count == 0 ? "Introdução" : "Aula " + (lessons.Count + 1)), Render(md)));
            }
            if (lessons.Count == 0) lessons.Add(new Lesson("m" + m + "-1", title, "<p>(módulo sem conteúdo)</p>"));
            modules.Add(new Module(title, objectives, lessons));
        }
        return modules;
    }

    /// <summary>A module's lessons: the text before the first ## heading, then one per ## heading (outside code).</summary>
    public static IEnumerable<(string? Title, string Markdown)> Lessons(string body)
    {
        var current = new StringBuilder();
        string? title = null;
        bool fence = false;
        foreach (var line in body.Replace("\r", "").Split('\n'))
        {
            var t = line.TrimStart();
            if (t.StartsWith("```", StringComparison.Ordinal) || t.StartsWith("~~~", StringComparison.Ordinal)) fence = !fence;
            if (!fence && H2Re().Match(line) is { Success: true } h)
            {
                if (title is not null || current.ToString().Trim().Length > 0) yield return (title, current.ToString());
                title = h.Groups[1].Value.Trim();
                current.Clear();
                continue;
            }
            current.Append(line).Append('\n');
        }
        if (title is not null || current.ToString().Trim().Length > 0) yield return (title, current.ToString());
    }

    private static IEnumerable<string> SplitList(string value) =>
        value.Trim().TrimStart('[').TrimEnd(']').Split([',', ';', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(v => v.Trim('"', '\'', '-', ' ')).Where(v => v.Length > 0);

    public static PublicationHtmlResult Export(PublicationProject project, PublicationSourceCatalog catalog, Func<string, string?>? data = null)
    {
        var missing = new List<string>();
        var modules = Modules(project, catalog, data, missing);
        var accent = project.Design.Theme switch { "ocean" => "#075985", "ink" => "#312e81", _ => "#6b4f2a" };
        var title = MarkdownEngine.EscapeHtml(project.Title);
        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n<html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
          .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\">")
          .Append("<title>").Append(title).Append("</title><style>")
          .Append(":root{color-scheme:light;--accent:").Append(accent).Append("}*{box-sizing:border-box}body{margin:0;color:#25272c;background:#fff;font-family:system-ui,sans-serif;line-height:1.6}")
          .Append(".course{display:grid;grid-template-columns:minmax(220px,300px) 1fr;min-height:100vh}@media(max-width:760px){.course{grid-template-columns:1fr}nav.toc{position:static!important;max-height:none!important}}")
          .Append("nav.toc{background:#f6f3ee;padding:1rem;position:sticky;top:0;max-height:100vh;overflow:auto}nav.toc h1{font-size:1.15rem;color:var(--accent);margin:.2rem 0 .8rem}")
          .Append("nav.toc h2{font-size:.95rem;margin:1rem 0 .3rem}nav.toc a{display:block;padding:.3rem .5rem;border-radius:6px;color:#25272c;text-decoration:none;font-size:.92rem}nav.toc a.current{background:#fff;font-weight:600}nav.toc a.done::after{content:' ✓';color:#30a46c}")
          .Append(".bar{height:8px;background:#e5dfd3;border-radius:4px;overflow:hidden;margin:.4rem 0 .2rem}.bar>i{display:block;height:100%;width:0;background:var(--accent)}")
          .Append("main{padding:clamp(16px,4vw,48px);max-width:820px}main h2.lesson-title{color:var(--accent)}.objectives{border-left:4px solid var(--accent);background:#f6f3ee;padding:.6rem 1rem;border-radius:6px}")
          .Append("img,svg{max-width:100%;height:auto}pre{overflow:auto;background:#f3f1ec;padding:.6rem;border-radius:6px}table{border-collapse:collapse}td,th{border:1px solid #d8d2c6;padding:.3rem .6rem}")
          .Append(".ubox{border-left:4px solid #9c7b45;background:#f6f3ee;padding:.6rem 1rem;margin:1rem 0;border-radius:6px}.umath-block{display:block;text-align:center}.missing{border:1px dashed #a35;padding:1rem}")
          .Append(".lesson-nav{display:flex;justify-content:space-between;gap:.6rem;margin-top:2rem}.lesson-nav button{font:inherit;padding:.5rem 1rem;border-radius:8px;border:1px solid var(--accent);background:#fff;cursor:pointer}.lesson-nav .finish{background:var(--accent);color:#fff}")
          .Append(".scripted section.lesson{display:none}.scripted section.lesson.current{display:block}")
          .Append(QuizBlock.Style)
          .Append("@media print{nav.toc,.lesson-nav,.quiz-check{display:none}.course{display:block}section.lesson{display:block!important;break-before:page}}")
          .Append("</style></head><body><div class=\"course\"><nav class=\"toc\" aria-label=\"Conteúdo do curso\"><h1>").Append(title).Append("</h1>")
          .Append("<div class=\"bar\" role=\"progressbar\" aria-label=\"Progresso no curso\" aria-valuemin=\"0\" aria-valuemax=\"100\" aria-valuenow=\"0\"><i></i></div><p class=\"progress-text\" style=\"font-size:.85rem;margin:0\"></p>");
        foreach (var module in modules)
        {
            sb.Append("<h2>").Append(MarkdownEngine.EscapeHtml(module.Title)).Append("</h2>");
            foreach (var lesson in module.Lessons) sb.Append("<a href=\"#").Append(lesson.Id).Append("\" data-lesson=\"").Append(lesson.Id).Append("\">").Append(MarkdownEngine.EscapeHtml(lesson.Title)).Append("</a>");
        }
        sb.Append("</nav><main>");
        foreach (var module in modules)
            for (int i = 0; i < module.Lessons.Count; i++)
            {
                var lesson = module.Lessons[i];
                sb.Append("<section class=\"lesson\" id=\"").Append(lesson.Id).Append("\" aria-label=\"").Append(MarkdownEngine.EscapeHtml(module.Title + " — " + lesson.Title)).Append("\">");
                sb.Append("<p style=\"color:#777;margin:0\">").Append(MarkdownEngine.EscapeHtml(module.Title)).Append("</p><h2 class=\"lesson-title\">").Append(MarkdownEngine.EscapeHtml(lesson.Title)).Append("</h2>");
                if (i == 0 && module.Objectives.Count > 0)
                    sb.Append("<div class=\"objectives\"><strong>Objetivos</strong><ul>").Append(string.Concat(module.Objectives.Select(o => "<li>" + MarkdownEngine.EscapeHtml(o) + "</li>"))).Append("</ul></div>");
                sb.Append(lesson.Html).Append("<div class=\"lesson-nav\"><button class=\"prev\">← Anterior</button><button class=\"finish\">Concluir aula</button><button class=\"next\">Próxima →</button></div></section>");
            }
        sb.Append("</main></div><script>").Append(QuizBlock.Script).Append(CourseScript.Replace("__KEY__", MarkdownEngine.EscapeHtml(project.PublicationId))).Append("</script></body></html>");
        return new PublicationHtmlResult(sb.ToString(), missing.AsReadOnly());
    }

    /// <summary>One lesson at a time, finished lessons and right answers kept in this browser (never in the notes).</summary>
    private const string CourseScript = """
(function(){var d=document,b=d.body,key='urbe-course-__KEY__',ls=[].slice.call(d.querySelectorAll('section.lesson')),links=[].slice.call(d.querySelectorAll('nav.toc a')),n=0;
var st={done:{},right:{}};try{st=JSON.parse(localStorage.getItem(key))||st}catch(e){}
function save(){try{localStorage.setItem(key,JSON.stringify(st))}catch(e){}}
function paint(){var total=ls.length,done=ls.filter(function(l){return st.done[l.id]}).length,q=d.querySelectorAll('.quiz-q').length,r=Object.keys(st.right).length,p=total?Math.round(done*100/total):0;
d.querySelector('.bar>i').style.width=p+'%';d.querySelector('.bar').setAttribute('aria-valuenow',p);
d.querySelector('.progress-text').textContent=done+' de '+total+' aulas concluídas'+(q?' · '+r+' de '+q+' questões certas':'');
links.forEach(function(a){a.classList.toggle('done',!!st.done[a.dataset.lesson]);a.classList.toggle('current',a.dataset.lesson===ls[n].id)})}
function go(i){n=Math.max(0,Math.min(ls.length-1,i));ls.forEach(function(l,k){l.classList.toggle('current',k===n)});if(location.hash!=='#'+ls[n].id)history.replaceState(null,'','#'+ls[n].id);paint();scrollTo(0,0)}
b.classList.add('scripted');
ls.forEach(function(l,k){l.querySelector('.prev').onclick=function(){go(k-1)};l.querySelector('.next').onclick=function(){go(k+1)};
l.querySelector('.finish').onclick=function(){st.done[l.id]=1;save();go(k+1)};l.querySelector('.prev').disabled=k===0;l.querySelector('.next').disabled=k===ls.length-1;
[].slice.call(l.querySelectorAll('.quiz-q')).forEach(function(f,j){f.addEventListener('quiz-answered',function(e){var id=l.id+'-'+j;if(e.detail.ok)st.right[id]=1;else delete st.right[id];save();paint()})})});
links.forEach(function(a){a.addEventListener('click',function(e){e.preventDefault();go(ls.findIndex(function(l){return l.id===a.dataset.lesson}))})});
var h=ls.findIndex(function(l){return '#'+l.id===location.hash});go(h>=0?h:0)})();
""";

    [GeneratedRegex(@"^\s*#[ \t]+([^\n]+?)[ \t#]*(?:\n|$)")]
    private static partial Regex H1Re();
    [GeneratedRegex(@"^ {0,3}##\s+(.+?)\s*#*\s*$")]
    private static partial Regex H2Re();
}
