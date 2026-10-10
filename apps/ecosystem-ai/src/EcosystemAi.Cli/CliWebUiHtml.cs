using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

/// <summary>
/// HTML responsivo produzido pelo Product C# a partir de um snapshot do
/// LocalProjectStore. Nenhum JavaScript, asset remoto ou estado persistente
/// alternativo ao catálogo existente.
/// </summary>
public static class CliWebUiHtml
{
    public static string Render(ProjectCatalog catalog, string csrf)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(csrf);

        var output = new StringBuilder();
        var sessions = catalog.Projects.SelectMany(p => p.Sessions).ToArray();
        var runs = sessions.SelectMany(s => s.Runs).ToArray();
        var costs = runs.GroupBy(r => r.Currency, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Sum(r => (decimal)r.CostMinor).ToString(CultureInfo.InvariantCulture)
                + " " + Escape(g.Key) + " (unidades mínimas)")
            .ToArray();

        output.Append(Header("Painel local"));
        output.Append("<header class='top'><div class='brand'><span class='mark'>E</span>")
            .Append("<div><small>ECOSYSTEM / PRODUCT</small><h1>Ecosystem AI</h1></div></div>")
            .Append("<span class='status'>● Local · sem conexão externa</span></header>")
            .Append("<main><section class='intro'><p class='eyebrow'>WORKSPACE / PROJETOS</p>")
            .Append("<h2>Seus projetos. Suas sessões.<br>Seus resultados.</h2>")
            .Append("<p>Gerenciamento real do catálogo local. Nenhum agente é executado nesta tela.</p></section>")
            .Append("<section class='stats' aria-label='Indicadores'>");
        Metric(output, "Projetos", catalog.Projects.Count.ToString(CultureInfo.InvariantCulture));
        Metric(output, "Sessões", sessions.Length.ToString(CultureInfo.InvariantCulture));
        Metric(output, "Execuções", runs.Length.ToString(CultureInfo.InvariantCulture));
        Metric(output, "Custo registrado", costs.Length == 0 ? "Nenhum" : string.Join(" · ", costs));
        output.Append("</section><div class='layout'><section class='panel creation'>")
            .Append("<div class='panel-heading'><p class='eyebrow'>CRIAR / VINCULAR</p><h2>Novo projeto</h2></div>")
            .Append("<p>Selecione uma pasta já existente no dispositivo que executa o servidor local.</p>")
            .Append("<form method='post' action='/projects'>")
            .Append("<input type='hidden' name='csrf' value='").Append(Escape(csrf)).Append("'>")
            .Append("<label for='name'>Nome do projeto</label>")
            .Append("<input id='name' name='name' maxlength='120' required autocomplete='off' placeholder='Meu aplicativo'>")
            .Append("<label for='workspace'>Pasta existente (caminho absoluto)</label>")
            .Append("<input id='workspace' name='workspace' required autocomplete='off' placeholder='/home/usuario/projeto'>")
            .Append("<button type='submit'>+ Vincular projeto</button>")
            .Append("</form><p class='hint'>O catálogo fica separado do workspace; nenhum arquivo do projeto é alterado.</p></section>")
            .Append("<section class='panel projects'><div class='panel-heading'><p class='eyebrow'>BIBLIOTECA</p>")
            .Append("<h2>Projetos e conversas</h2></div>");

        if (catalog.Projects.Count == 0)
        {
            output.Append("<div class='empty'><strong>Sem projetos registrados</strong>")
                .Append("<p>Vincule uma pasta existente no formulário para começar.</p></div>");
        }
        foreach (var project in catalog.Projects)
        {
            output.Append("<article class='project' id='p-").Append(Escape(project.Id))
                .Append("'><div class='project-header'><div><h3>").Append(Escape(project.Name))
                .Append("</h3><small>")
                .Append(project.Sessions.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" sessões · criado ").Append(Escape(Date(project.CreatedAt)))
                .Append("</small></div><span class='pill'>Projeto</span></div>")
                .Append("<details class='project-content' open><summary>Ver sessões e criar conversa</summary>")
                .Append("<div class='project-body'><form method='post' action='/sessions' class='session-form'>")
                .Append("<input type='hidden' name='csrf' value='").Append(Escape(csrf)).Append("'>")
                .Append("<input type='hidden' name='projectId' value='").Append(Escape(project.Id)).Append("'>")
                .Append("<label for='session-").Append(Escape(project.Id)).Append("'>Título da nova sessão</label>")
                .Append("<div class='inline'><input id='session-").Append(Escape(project.Id))
                .Append("' name='title' maxlength='120' required placeholder='Nova conversa'>")
                .Append("<button type='submit'>+ Sessão</button></div></form>");
            if (project.Sessions.Count == 0)
                output.Append("<p class='hint'>Nenhuma sessão. Crie a primeira acima.</p>");
            foreach (var session in project.Sessions)
            {
                output.Append("<details class='session' id='s-").Append(Escape(session.Id))
                    .Append("'><summary><span><strong>").Append(Escape(session.Title))
                    .Append("</strong><small>").Append(session.Turns.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" mensagens · ").Append(session.Runs.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" runs</small></span><span class='chevron'>⌄</span></summary>")
                    .Append("<div class='conversation'>");
                if (session.Turns.Count == 0)
                    output.Append("<p class='hint'>Esta sessão ainda não tem mensagens.</p>");
                foreach (var turn in session.Turns)
                {
                    output.Append("<article class='turn ").Append(turn.Role == "assistant" ? "assistant" : "user")
                        .Append("'><div><strong>").Append(turn.Role == "assistant" ? "Assistente" : "Usuário")
                        .Append("</strong><time>").Append(Escape(Date(turn.At))).Append("</time></div><p>")
                        .Append(Escape(turn.Text)).Append("</p></article>");
                }
                if (session.Runs.Count != 0)
                {
                    output.Append("<h4>Execuções registradas</h4>");
                    foreach (var run in session.Runs)
                    {
                        var pill = run.Status == "succeeded" ? "ok" : run.Status == "failed" ? "bad" : "waiting";
                        output.Append("<div class='run'><span class='pill ").Append(pill)
                            .Append("'>").Append(Escape(run.Status)).Append("</span><div>")
                            .Append("<strong>").Append(Escape(run.RunId))
                            .Append("</strong><small>")
                            .Append(run.CostEstimated ? "Custo estimado · " : "Custo registrado · ")
                            .Append(run.CostMinor.ToString(CultureInfo.InvariantCulture)).Append(" ")
                            .Append(Escape(run.Currency)).Append(" minor · ")
                            .Append(run.Verified ? "Verificado" : "Não verificado")
                            .Append(" · ").Append(Escape(Date(run.At)))
                            .Append("</small></div></div>");
                    }
                }
                output.Append("</div></details>");
            }
            output.Append("</div></details></article>");
        }

        output.Append("</section></div><footer><p>Disponível somente em 127.0.0.1 no dispositivo que executa o processo.")
            .Append(" Esta tela não contém JavaScript, não executa agentes e não envia informações para servidores externos.</p>")
            .Append("<p>Conversas podem conter dados privados armazenados em texto claro no catálogo local.</p>")
            .Append("</footer></main></body></html>");
        return output.ToString();
    }

    public static string RenderError(string title, string message) =>
        Header(title) + "<main class='error'><h1>" + Escape(title) + "</h1><p>"
        + Escape(message) + "</p><a href='/'>Voltar ao painel</a></main></body></html>";

    private static void Metric(StringBuilder sb, string label, string value) =>
        sb.Append("<div class='metric'><small>").Append(Escape(label)).Append("</small><strong>")
            .Append(Escape(value)).Append("</strong></div>");

    private static string Date(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Escape(string? text) => HtmlEncoder.Default.Encode(text ?? "");

    private static string Header(string title) => """
<!doctype html><html lang="pt-BR"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<meta name="color-scheme" content="dark">
<title>
""" + Escape(title) + """
 — Ecosystem AI</title>
<style>
:root{font-family:system-ui,-apple-system,"Segoe UI",Roboto,sans-serif;background:#0b101a;color:#e7eef9;color-scheme:dark;line-height:1.55}
*{box-sizing:border-box}body{margin:0}a{color:#9ddcff}small,.hint{color:#aabbd1}
.top{display:flex;justify-content:space-between;align-items:center;gap:12px;padding:20px clamp(16px,4vw,36px);border-bottom:1px solid #253449}
.brand{display:flex;align-items:center;gap:14px}.mark{display:grid;place-items:center;width:45px;height:45px;background:#145fd0;border-radius:12px;font-weight:900;font-size:1.4rem}
.brand small{font-size:.65rem;letter-spacing:.16em}.brand h1{font-size:1.12rem;margin:0}.status{font-size:.73rem;color:#94e3c6;border:1px solid #235849;background:#0d2824;padding:7px 10px;border-radius:22px}
main{max-width:1180px;margin:auto;padding:24px clamp(16px,4vw,36px) 65px}.eyebrow{letter-spacing:.18em;font-size:.7rem;font-weight:700;color:#71b5ff;margin:0 0 8px}
.intro{padding:22px 0 14px}.intro h2{font-size:clamp(1.65rem,4vw,2.8rem);line-height:1.18;letter-spacing:-.03em;margin:0 0 12px}.intro p:last-child{color:#afbed3}
.stats{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin:16px 0 22px}
.metric,.panel{border:1px solid #29384d;background:#151f2d;border-radius:16px}.metric{padding:14px;min-width:0}.metric small{display:block}.metric strong{display:block;font-size:clamp(1rem,2vw,1.35rem);overflow-wrap:anywhere;margin-top:5px;font-variant-numeric:tabular-nums}
.layout{display:grid;grid-template-columns:minmax(250px,.86fr) minmax(0,1.6fr);gap:16px;align-items:start}.panel{padding:20px}.panel-heading{margin-bottom:16px}.panel-heading h2{margin:0;font-size:1.25rem}
label{display:block;font-weight:600;font-size:.9rem;margin:15px 0 6px}input{width:100%;background:#0d1421;border:1px solid #53647e;color:#fff;padding:12px 13px;min-height:46px;border-radius:10px;font:inherit;min-width:0}
input:focus-visible,button:focus-visible,summary:focus-visible,a:focus-visible{outline:3px solid #6bb3ff;outline-offset:3px}
button{font:inherit;font-weight:700;min-height:44px;background:#3389ff;border:0;color:#071326;border-radius:10px;padding:10px 16px;cursor:pointer}
.creation button{width:100%;margin-top:17px}.creation p{color:#aabbd1;font-size:.88rem}.creation .hint{margin-top:14px}
.project{border:1px solid #32445e;background:#101827;border-radius:12px;margin:12px 0;overflow:hidden;scroll-margin-top:15px}.project-header{display:flex;justify-content:space-between;gap:12px;align-items:center;padding:16px}
h3{font-size:1.05rem;margin:0 0 3px;overflow-wrap:anywhere}.pill{font-size:.72rem;border-radius:8px;border:1px solid #48638a;background:#223953;padding:5px 9px;white-space:nowrap}.pill.ok{color:#a3efc8;border-color:#23684d;background:#10392e}.pill.bad{color:#ffc0c6;border-color:#8d434a;background:#44262e}.pill.waiting{color:#ffde91}
summary{cursor:pointer;list-style:none}summary::-webkit-details-marker{display:none}.project-content>summary{padding:10px 16px;border-top:1px solid #27384b;color:#aad8ff;font-size:.87rem}.project-body{padding:14px;border-top:1px solid #27384b}
.session-form{border:1px dashed #3b5573;border-radius:10px;padding:12px;margin-bottom:13px}.session-form label{margin:0 0 7px}.inline{display:flex;gap:8px}.inline button{flex-shrink:0}
.session{margin:9px 0;border:1px solid #344459;border-radius:10px;overflow:hidden;scroll-margin-top:20px}.session>summary{display:flex;justify-content:space-between;align-items:center;gap:12px;padding:13px}
.session>summary span:first-child{display:grid;gap:3px;min-width:0}.session>summary strong{overflow-wrap:anywhere}.conversation{padding:12px;background:#0c1522;border-top:1px solid #27384b}.conversation h4{margin:18px 0 8px}
.turn{border:1px solid #2f4159;background:#172335;padding:12px;border-radius:10px;margin-bottom:9px;overflow-wrap:anywhere}.turn.assistant{border-left:3px solid #5bbaff}.turn.user{border-left:3px solid #8a9fb8}
.turn>div{display:flex;justify-content:space-between;gap:6px;flex-wrap:wrap;font-size:.8rem}.turn time{color:#afbed3}.turn p{white-space:pre-wrap;margin:8px 0 0}
.run{display:flex;gap:10px;align-items:start;padding:12px 3px;border-top:1px solid #344459}.run strong{overflow-wrap:anywhere;font-size:.77rem}.run small{display:block;overflow-wrap:anywhere}.empty{padding:20px;border:1px dashed #3a4c63;border-radius:12px}
footer{margin:30px 0 0;color:#9daec5;font-size:.79rem;border-top:1px solid #253449;padding-top:20px}.error{max-width:750px;margin:auto;padding:40px 18px}.error p{white-space:pre-wrap;overflow-wrap:anywhere}
@media(max-width:760px){.top{align-items:flex-start}.status{font-size:.65rem;max-width:145px}.stats{grid-template-columns:repeat(2,minmax(0,1fr))}.layout{grid-template-columns:minmax(0,1fr)}.panel{padding:15px}}
@media(max-width:370px){.inline{flex-direction:column}.status{display:none}.top{padding:16px}}
</style></head><body>
""";
}
