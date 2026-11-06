using System.Text;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>A question of a quiz: its text, its options (which are right) and the explanation shown after answering.</summary>
public sealed record QuizQuestion(string Prompt, IReadOnlyList<(string Text, bool Correct)> Options, string? Explanation)
{
    /// <summary>More than one right option: the person marks all of them.</summary>
    public bool Multiple => Options.Count(o => o.Correct) > 1;
}

/// <summary>
/// CST-174: a <c>```quiz</c> block of a note — self-check questions written as plain, readable Markdown:
/// <code>
/// ? Qual a derivada de x²?
/// - [ ] x
/// - [x] 2x
/// - [ ] x²
/// > Pela regra da potência: n·xⁿ⁻¹.
/// </code>
/// A line with <c>?</c> starts a question, task items are its options (<c>[x]</c> the right ones), and a <c>&gt;</c>
/// line is the explanation. The note keeps only this text; answers given are never written into it.
/// </summary>
public static partial class QuizBlock
{
    public static bool IsLanguage(string? lang) => lang is not null && lang.Trim().ToLowerInvariant() is "quiz" or "questoes" or "questões" or "exercicios" or "exercícios";

    public static IReadOnlyList<QuizQuestion> Parse(string block, List<string>? problems = null)
    {
        var questions = new List<QuizQuestion>();
        string? prompt = null;
        var options = new List<(string, bool)>();
        var explanation = new StringBuilder();
        void Close()
        {
            if (prompt is null) return;
            if (options.Count < 2) problems?.Add("“" + prompt + "” precisa de pelo menos duas alternativas (- [ ] …).");
            else if (!options.Any(o => o.Item2)) problems?.Add("“" + prompt + "” não marca nenhuma alternativa certa ([x]).");
            else questions.Add(new QuizQuestion(prompt, options.ToArray(), explanation.Length > 0 ? explanation.ToString().Trim() : null));
            prompt = null; options = []; explanation.Clear();
        }
        foreach (var raw in block.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('?')) { Close(); prompt = line[1..].Trim(); continue; }
            if (prompt is null) { problems?.Add("Comece cada questão com “? ”: " + line); continue; }
            if (OptionRe().Match(line) is { Success: true } m) { options.Add((m.Groups[2].Value.Trim(), m.Groups[1].Value is "x" or "X")); continue; }
            if (line.StartsWith('>')) { explanation.Append(line[1..].Trim()).Append(' '); continue; }
            // more text of the question (a long prompt over lines)
            if (options.Count == 0) prompt += " " + line;
            else problems?.Add("Linha fora do formato em “" + prompt + "”: " + line);
        }
        Close();
        return questions;
    }

    /// <summary>
    /// Every <c>```quiz</c> block of rendered HTML as a form: interactive where the page has the quiz script
    /// (<paramref name="interactive"/>), else the questions with the answer key after them (print, EPUB).
    /// </summary>
    public static string RenderHtml(string html, bool interactive) => BlockRe().Replace(html, m =>
    {
        var questions = Parse(System.Net.WebUtility.HtmlDecode(m.Groups[1].Value));
        if (questions.Count == 0) return "<p class=\"missing\">Questionário sem questões válidas.</p>";
        var sb = new StringBuilder("<section class=\"urbe-quiz\" aria-label=\"Questionário\">");
        for (int q = 0; q < questions.Count; q++)
        {
            var question = questions[q];
            sb.Append("<fieldset class=\"quiz-q\" data-multiple=\"").Append(question.Multiple ? "1" : "0").Append("\"><legend>").Append(q + 1).Append(". ")
              .Append(MarkdownEngine.EscapeHtml(question.Prompt)).Append("</legend>");
            for (int o = 0; o < question.Options.Count; o++)
            {
                var (text, correct) = question.Options[o];
                if (interactive)
                    sb.Append("<label><input type=\"").Append(question.Multiple ? "checkbox" : "radio").Append("\" name=\"q").Append(q).Append("\" data-ok=\"").Append(correct ? "1" : "0").Append("\"> ")
                      .Append(MarkdownEngine.EscapeHtml(text)).Append("</label>");
                else sb.Append("<p class=\"quiz-o\">").Append((char)('a' + o)).Append(") ").Append(MarkdownEngine.EscapeHtml(text)).Append("</p>");
            }
            if (interactive)
                sb.Append("<button type=\"button\" class=\"quiz-check\">Conferir</button><p class=\"quiz-feedback\" role=\"status\" hidden></p>")
                  .Append(question.Explanation is { } e ? "<p class=\"quiz-why\" hidden>" + MarkdownEngine.EscapeHtml(e) + "</p>" : "");
            sb.Append("</fieldset>");
        }
        if (!interactive)
        {
            sb.Append("<details class=\"quiz-key\" open><summary>Gabarito</summary><ol>");
            foreach (var question in questions)
                sb.Append("<li>").Append(string.Join(", ", question.Options.Select((o, i) => (o, i)).Where(x => x.o.Correct).Select(x => (char)('a' + x.i) + ")")))
                  .Append(question.Explanation is { } e ? " — " + MarkdownEngine.EscapeHtml(e) : "").Append("</li>");
            sb.Append("</ol></details>");
        }
        return sb.Append("</section>").ToString();
    });

    /// <summary>The script that makes interactive quizzes answer (feedback, explanation); no network, no storage of its own.</summary>
    public const string Script = """
document.querySelectorAll('.urbe-quiz .quiz-check').forEach(function(b){b.addEventListener('click',function(){
var f=b.closest('.quiz-q'),inputs=[].slice.call(f.querySelectorAll('input'));
if(!inputs.some(function(i){return i.checked})){var p=f.querySelector('.quiz-feedback');p.hidden=false;p.textContent='Escolha uma alternativa.';return;}
var ok=inputs.every(function(i){return i.checked===(i.dataset.ok==='1')});
inputs.forEach(function(i){i.parentElement.classList.toggle('right',i.dataset.ok==='1');i.parentElement.classList.toggle('wrong',i.checked&&i.dataset.ok!=='1')});
var p=f.querySelector('.quiz-feedback');p.hidden=false;p.textContent=ok?'Certo!':'Ainda não. A certa está marcada.';f.classList.toggle('done-ok',ok);f.classList.add('answered');
var w=f.querySelector('.quiz-why');if(w)w.hidden=false;
f.dispatchEvent(new CustomEvent('quiz-answered',{bubbles:true,detail:{ok:ok}}));});});
""";

    public const string Style = ".urbe-quiz{margin:1.2rem 0}.quiz-q{border:1px solid #d8d2c6;border-radius:10px;padding:.8rem 1rem;margin:0 0 1rem}.quiz-q legend{font-weight:600;padding:0 .3rem}.quiz-q label{display:block;padding:.35rem .4rem;border-radius:6px;cursor:pointer}.quiz-q label.right{background:#e6f4ea}.quiz-q label.wrong{background:#fde8e8}.quiz-check{margin-top:.5rem;font:inherit;padding:.4rem .9rem;border-radius:8px;border:1px solid #9c7b45;background:#fff;cursor:pointer}.quiz-feedback{font-weight:600}.quiz-why{color:#555}.quiz-o{margin:.2rem 0 .2rem 1rem}.quiz-key{margin-top:.6rem}";

    [GeneratedRegex(@"^[-+*]\s+\[([ xX])\]\s+(.+)$")]
    private static partial Regex OptionRe();
    [GeneratedRegex("<pre data-lang=\"(?:quiz|questoes|questões|exercicios|exercícios)\"><code>(.*?)</code></pre>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BlockRe();
}
