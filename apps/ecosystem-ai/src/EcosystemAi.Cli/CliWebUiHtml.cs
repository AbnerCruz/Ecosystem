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
    public static string Render(ProjectCatalog catalog, string csrf,
        IReadOnlyDictionary<string, VisualRunDetails>? auditDetails = null,
        WebTaskBoard? taskBoard = null, AgentRoster? roster = null,
        TeamReviewBoard? teamReviews = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(csrf);

        var output = new StringBuilder();
        var sessions = catalog.Projects.SelectMany(p => p.Sessions).ToArray();
        var runs = sessions.SelectMany(s => s.Runs).ToArray();
        var audited = auditDetails?.Values.ToArray() ?? [];
        var agents = audited.GroupBy(x => x.Agent, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal).ToArray();
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
        if (auditDetails is not null)
        {
            output.Append("<section class='audit-board' aria-label='Quadro de tarefas e agentes'>")
                .Append("<div class='board-heading'><div><p class='eyebrow'>RUNTIME / JOURNAL</p>")
                .Append("<h2>Atividade verificada de agentes</h2>")
                .Append("<p>Somente referências e estado reconstruídos do log do Runtime. Sem prompts nem arquivos.</p>")
                .Append("</div><span class='pill ok'>Auditoria ativa</span></div>")
                .Append("<div class='board-metrics'>");
            Metric(output, "Agentes com execuções", agents.Length.ToString(CultureInfo.InvariantCulture));
            Metric(output, "Tarefas auditadas", audited.Length.ToString(CultureInfo.InvariantCulture));
            Metric(output, "Verificações aprovadas", audited.Count(r => r.Verified).ToString(CultureInfo.InvariantCulture));
            Metric(output, "Sem evidência no journal", runs.Count(r => !auditDetails.ContainsKey(r.RunId))
                .ToString(CultureInfo.InvariantCulture));
            output.Append("</div>");
            if (agents.Length == 0)
                output.Append("<p class='hint'>Nenhum run auditado. Os recibos sem journal não provam conclusão.</p>");
            foreach (var agent in agents)
            {
                output.Append("<details class='agent-card'><summary><span><strong>Agente: ")
                    .Append(Escape(agent.Key)).Append("</strong><small>")
                    .Append(agent.Count().ToString(CultureInfo.InvariantCulture))
                    .Append(" execuções · ").Append(agent.Count(x => x.Verified).ToString(CultureInfo.InvariantCulture))
                    .Append(" verificadas</small></span><span class='chevron'>⌄</span></summary>")
                    .Append("<div class='agent-tasks'>");
                foreach (var run in agent)
                    output.Append("<a href='#r-").Append(Escape(run.RunId)).Append("'>Tarefa ")
                        .Append(Escape(run.TaskId)).Append(" · ").Append(Escape(run.State))
                        .Append("</a>");
                output.Append("</div></details>");
            }
            output.Append("</section>");
        }
        if (taskBoard is not null)
        {
            output.Append("<section class='execution-status' aria-label='Limites de execução'>")
                .Append("<div><p class='eyebrow'>AGENTE / EXECUÇÃO SUPERVISIONADA</p><h2>Enviar tarefas reais</h2>")
                .Append("<p>Execução habilitada pelo operador ao iniciar o processo, sem escrita no projeto.</p></div>")
                .Append("<div class='execution-budget'><span>Orçamento restante nesta instância: <strong>")
                .Append(taskBoard.RemainingCents.ToString(CultureInfo.InvariantCulture))
                .Append(" centavos USD</strong></span><span>Tarefas restantes: <strong>")
                .Append(taskBoard.RemainingRuns.ToString(CultureInfo.InvariantCulture))
                .Append("</strong></span><span>Limite por tarefa: <strong>")
                .Append(taskBoard.RunBudgetCents.ToString(CultureInfo.InvariantCulture))
                .Append(" centavos USD</strong></span></div>")
                .Append(taskBoard.ReviewTeams
                    ? "<p>Revisão de equipe ativa: dois runs independentes; parecer não aprova nem integra arquivos.</p>"
                    : "").Append("</section>");
        }
        output.Append("<div class='layout'><section class='panel creation'>")
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
            var projectAgents = roster?.Agents.Where(a => a.ProjectId == project.Id).ToArray()
                ?? Array.Empty<LocalAgentProfile>();
            var projectTeams = roster?.Teams.Where(t => t.ProjectId == project.Id).ToArray()
                ?? Array.Empty<LocalAgentTeam>();
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
            if (roster is not null)
            {
                output.Append("<section class='roster'><h4>Agentes · ")
                    .Append(projectAgents.Length.ToString(CultureInfo.InvariantCulture))
                    .Append("</h4>");
                foreach (var agent in projectAgents)
                    output.Append("<div class='roster-item'><strong>")
                        .Append(Escape(agent.Name)).Append("</strong><small>")
                        .Append(Escape(agent.Instructions)).Append("</small></div>");
                output.Append("<form method='post' action='/agents' class='roster-form'>")
                    .Append("<input type='hidden' name='csrf' value='").Append(Escape(csrf)).Append("'>")
                    .Append("<input type='hidden' name='projectId' value='").Append(Escape(project.Id)).Append("'>")
                    .Append("<label for='agent-name-").Append(Escape(project.Id)).Append("'>Nome do agente</label>")
                    .Append("<input id='agent-name-").Append(Escape(project.Id))
                    .Append("' name='name' maxlength='80' required placeholder='Desenvolvedor'>")
                    .Append("<label for='agent-prompt-").Append(Escape(project.Id)).Append("'>Instruções (não inclua segredos)</label>")
                    .Append("<textarea id='agent-prompt-").Append(Escape(project.Id))
                    .Append("' name='instructions' maxlength='2048' required rows='3' ")
                    .Append("placeholder='Atue na especialidade informada sem ampliar permissões'></textarea>")
                    .Append("<button type='submit'>+ Agente</button></form>")
                    .Append("<h4>Equipes · ").Append(projectTeams.Length.ToString(CultureInfo.InvariantCulture))
                    .Append("</h4>");
                foreach (var team in projectTeams)
                {
                    var producer = projectAgents.First(a => a.Id == team.ProducerId);
                    var reviewer = projectAgents.First(a => a.Id == team.ReviewerId);
                    output.Append("<div class='roster-item'><strong>").Append(Escape(team.Name))
                        .Append("</strong><small>Produção: ").Append(Escape(producer.Name))
                        .Append(" · Revisão designada: ").Append(Escape(reviewer.Name))
                        .Append("</small></div>");
                }
                if (projectAgents.Length >= 2)
                {
                    output.Append("<form method='post' action='/teams' class='roster-form'>")
                        .Append("<input type='hidden' name='csrf' value='").Append(Escape(csrf)).Append("'>")
                        .Append("<input type='hidden' name='projectId' value='").Append(Escape(project.Id)).Append("'>")
                        .Append("<label>Nome da equipe</label><input name='name' maxlength='80' required>")
                        .Append("<label>Produtor</label><select name='producerId'>");
                    foreach (var agent in projectAgents)
                        output.Append("<option value='").Append(Escape(agent.Id)).Append("'>")
                            .Append(Escape(agent.Name)).Append("</option>");
                    output.Append("</select><label>Revisor independente</label><select name='reviewerId'>");
                    foreach (var agent in projectAgents)
                        output.Append("<option value='").Append(Escape(agent.Id)).Append("'>")
                            .Append(Escape(agent.Name)).Append("</option>");
                    output.Append("</select><button type='submit'>+ Equipe</button></form>");
                }
                else
                    output.Append("<p class='hint'>Cadastre dois agentes para formar uma equipe com revisão independente.</p>");
                output.Append("<p class='hint'>Equipe designa funções; revisão automática ainda não está habilitada.</p></section>");
            }
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
                var sessionReviews = teamReviews?.Reviews.Where(r =>
                    r.ProjectId == project.Id && r.SessionId == session.Id)
                    .OrderByDescending(r => r.CreatedAt).ToArray() ?? [];
                if (sessionReviews.Length > 0)
                {
                    output.Append("<section class='review-inbox' aria-label='Pareceres independentes'>")
                        .Append("<h4>Pareceres independentes · ")
                        .Append(sessionReviews.Length.ToString(CultureInfo.InvariantCulture))
                        .Append("</h4><p class='hint'>O aceite local reconhece o parecer; ")
                        .Append("não autoriza escrita, merge nem integração.</p>");
                    foreach (var review in sessionReviews)
                    {
                        var teamName = projectTeams.FirstOrDefault(t => t.Id == review.TeamId)?.Name
                            ?? "Equipe não disponível";
                        output.Append("<article class='review-entry'><strong>")
                            .Append(Escape(teamName)).Append("</strong><small>Produtor: ")
                            .Append(Escape(review.ProducerRunId)).Append(" · Revisor: ")
                            .Append(Escape(review.ReviewerRunId)).Append("</small>");
                        if (review.Decision == "pending_owner")
                        {
                            output.Append("<p>Parecer aguardando decisão do operador.</p>")
                                .Append("<form method='post' action='/review-decisions' class='review-form'>")
                                .Append("<input type='hidden' name='csrf' value='")
                                .Append(Escape(csrf)).Append("'>")
                                .Append("<input type='hidden' name='reviewId' value='")
                                .Append(Escape(review.Id)).Append("'>")
                                .Append("<label>Justificativa da decisão (obrigatória)</label>")
                                .Append("<textarea name='note' maxlength='512' rows='2' required ")
                                .Append("placeholder='Registre seu critério de aceite ou rejeição'></textarea>")
                                .Append("<div class='review-actions'>")
                                .Append("<button type='submit' name='decision' value='owner_accepted'>")
                                .Append("Aceitar parecer</button>")
                                .Append("<button type='submit' name='decision' value='owner_rejected'>")
                                .Append("Rejeitar parecer</button></div></form>");
                        }
                        else
                            output.Append("<p>Decisão do operador: <strong>")
                                .Append(review.Decision == "owner_accepted" ? "Parecer aceito" : "Parecer rejeitado")
                                .Append("</strong></p><p class='hint'>Justificativa: ")
                                .Append(Escape(review.Note ?? "")).Append("</p>");
                        output.Append("</article>");
                    }
                    output.Append("</section>");
                }
                if (taskBoard is not null)
                {
                    output.Append("<form class='task-form' method='post' action='/tasks'>")
                        .Append("<input type='hidden' name='csrf' value='").Append(Escape(csrf)).Append("'>")
                        .Append("<input type='hidden' name='projectId' value='").Append(Escape(project.Id)).Append("'>")
                        .Append("<input type='hidden' name='sessionId' value='").Append(Escape(session.Id)).Append("'>")
                        .Append("<label>Executor</label><select name='assignee'>")
                        .Append("<option value='default'>Assistente padrão</option>");
                    foreach (var agent in projectAgents)
                        output.Append("<option value='agent:").Append(Escape(agent.Id)).Append("'>Agente: ")
                            .Append(Escape(agent.Name)).Append("</option>");
                    foreach (var team in projectTeams)
                        output.Append("<option value='team:").Append(Escape(team.Id)).Append("'>Equipe: ")
                            .Append(Escape(team.Name)).Append(taskBoard.ReviewTeams
                                ? " (produtor + revisor independente)</option>"
                                : " (produtor)</option>");
                    output.Append("</select>")
                        .Append("<label for='goal-").Append(Escape(session.Id)).Append("'>Nova tarefa supervisionada</label>")
                        .Append("<textarea id='goal-").Append(Escape(session.Id))
                        .Append("' name='goal' rows='3' maxlength='16384' required ")
                        .Append(taskBoard.RemainingCents <= 0 || taskBoard.RemainingRuns <= 0
                            ? "disabled " : "")
                        .Append("placeholder='Descreva a tarefa para o agente...'></textarea>")
                        .Append("<button type='submit' ")
                        .Append(taskBoard.RemainingCents <= 0 || taskBoard.RemainingRuns <= 0
                            ? "disabled " : "")
                        .Append(">Executar tarefa</button>")
                        .Append("<p class='hint'>Somente leitura do workspace. ")
                        .Append(taskBoard.UseHistory ? "Histórico reenviado ao modelo conforme autorização do operador. "
                            : "Histórico anterior não é enviado ao modelo. ")
                        .Append(taskBoard.ReviewTeams
                            ? "Equipes: dois runs pagos, produtor + revisor; parecer não é aprovação. "
                            : "Equipes: somente o produtor executa. ")
                        .Append("Cada envio reserva orçamento. Sem retry automático.</p></form>");
                }
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
                        output.Append("<div class='run' id='r-").Append(Escape(run.RunId))
                            .Append("'><span class='pill ").Append(pill)
                            .Append("'>").Append(Escape(run.Status)).Append("</span><div class='run-body'>")
                            .Append("<strong>").Append(Escape(run.RunId))
                            .Append("</strong><small>")
                            .Append(run.CostEstimated ? "Custo estimado · " : "Custo registrado · ")
                            .Append(run.CostMinor.ToString(CultureInfo.InvariantCulture)).Append(" ")
                            .Append(Escape(run.Currency)).Append(" minor · ")
                            .Append(run.Verified ? "Verificado no recibo" : "Não verificado no recibo")
                            .Append(" · ").Append(Escape(Date(run.At)))
                            .Append("</small>");
                        if (auditDetails is not null)
                        {
                            if (auditDetails.TryGetValue(run.RunId, out var details))
                            {
                                output.Append("<details class='run-audit'><summary>Ver auditoria da tarefa</summary>")
                                    .Append("<div><p><strong>Tarefa:</strong> ").Append(Escape(details.TaskId))
                                    .Append(" · <strong>Agente:</strong> ").Append(Escape(details.Agent));
                                if (!string.IsNullOrWhiteSpace(details.Model))
                                    output.Append(" · <strong>Modelo:</strong> ").Append(Escape(details.Model));
                                output.Append("</p><p><strong>Estado do Runtime:</strong> ")
                                    .Append(Escape(details.State))
                                    .Append(" · <strong>Verificação:</strong> ")
                                    .Append(details.Verified ? "aprovada" : "não aprovada")
                                    .Append(" · passos: ")
                                    .Append(details.Steps.ToString(CultureInfo.InvariantCulture))
                                    .Append(" · ferramentas: ")
                                    .Append(details.ToolCalls.ToString(CultureInfo.InvariantCulture))
                                    .Append("</p>");
                                if (!string.Equals(run.Status, details.State, StringComparison.OrdinalIgnoreCase)
                                    || run.Verified != details.Verified)
                                    output.Append("<p class='warning'>Atenção: recibo e replay do journal divergem.</p>");
                                if (details.Artifacts.Count != 0)
                                {
                                    output.Append("<p><strong>Artefatos referenciados:</strong></p><ul>");
                                    foreach (var item in details.Artifacts)
                                    {
                                        output.Append("<li><strong>").Append(Escape(item.Kind)).Append("</strong> — ")
                                            .Append(Escape(item.Name));
                                        if (item.TextPreview is not null)
                                            output.Append("<details class='artifact-preview-wrap'><summary>Ver texto atual do arquivo (não histórico)</summary>")
                                                .Append("<pre class='artifact-preview'>")
                                                .Append(Escape(item.TextPreview))
                                                .Append("</pre></details>");
                                        output.Append("</li>");
                                    }
                                    output.Append("</ul>");
                                }
                                else output.Append("<p class='hint'>Nenhum artefato referenciado.</p>");
                                output.Append("</div></details>");
                            }
                            else output.Append("<p class='warning'>Sem evidência de journal para este run.</p>");
                        }
                        output.Append("</div></div>");
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
.audit-board{border:1px solid #325478;background:linear-gradient(135deg,#12243a,#101a2a);border-radius:16px;padding:20px;margin:0 0 22px}.board-heading{display:flex;justify-content:space-between;align-items:start;gap:12px}.board-heading h2{margin:0}.board-heading p:last-child{color:#b8cce0;margin:8px 0}.board-metrics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:10px;margin:14px 0}.agent-card{border:1px solid #355473;border-radius:12px;margin:8px 0;background:#132336}.agent-card>summary{display:flex;justify-content:space-between;gap:12px;padding:13px}.agent-card>summary span:first-child{display:grid;gap:4px;overflow-wrap:anywhere}.agent-tasks{display:flex;flex-wrap:wrap;gap:9px;padding:12px;border-top:1px solid #355473}.agent-tasks a{display:inline-block;border:1px solid #426187;border-radius:9px;padding:9px 12px;text-decoration:none}.execution-status{border:1px solid #335e79;border-radius:16px;padding:18px;margin-bottom:22px;background:#14283a}.execution-status h2{margin:0}.execution-status p:last-child{color:#b9d5e6}.execution-budget{display:flex;gap:10px;flex-wrap:wrap;margin-top:12px}.execution-budget span{background:#092035;border:1px solid #365d79;border-radius:9px;padding:9px 12px;font-size:.85rem}.task-form{border:1px solid #426483;background:#112538;border-radius:10px;padding:12px;margin-bottom:12px}.task-form label{margin:0 0 8px}.task-form textarea{display:block;min-height:96px;width:100%;background:#0d1421;color:#fff;border:1px solid #53647e;border-radius:9px;padding:12px;resize:vertical;font:inherit}.task-form button{margin-top:10px}.task-form button:disabled,.task-form textarea:disabled{opacity:.5;cursor:not-allowed}.task-form .hint{margin:8px 0 0;font-size:.8rem}.task-form textarea:focus-visible{outline:3px solid #6bb3ff;outline-offset:3px}.run-body{min-width:0;flex:1}.run-audit{border:1px solid #3a526d;border-radius:10px;margin:9px 0 0;overflow:hidden}.run-audit>summary{padding:10px 12px;color:#aad8ff}.run-audit>div{padding:10px 13px;border-top:1px solid #3a526d}.run-audit p{margin:5px 0;overflow-wrap:anywhere}.run-audit li{overflow-wrap:anywhere}.artifact-preview-wrap{margin:7px 0;border:1px solid #34587a;border-radius:9px}.artifact-preview-wrap>summary{padding:10px 12px;color:#b8dbff}.artifact-preview{margin:0;padding:13px;white-space:pre-wrap;overflow-wrap:anywhere;font-size:.82rem;background:#0a1422;border-top:1px solid #34587a;max-height:440px;overflow-y:auto}.warning{color:#ffd394!important}.run:target,.session:target{outline:2px solid #6bb3ff;outline-offset:2px}.roster{margin:15px 0;padding:13px;background:#101e2e;border:1px solid #385575;border-radius:11px}.roster h4{margin:14px 0 9px}.roster-item{padding:9px 0;border-bottom:1px solid #2f475e}.roster-item strong,.roster-item small{display:block;overflow-wrap:anywhere}.roster-form{margin:12px 0;padding:12px;border:1px dashed #48617c;border-radius:10px}.roster-form button{margin-top:12px}.roster textarea,.roster select,.task-form select{display:block;width:100%;min-height:43px;padding:10px;background:#0d1421;color:#f4f7ff;border:1px solid #53647e;border-radius:8px;font:inherit}.roster textarea{resize:vertical}.roster textarea:focus-visible,.roster select:focus-visible,.task-form select:focus-visible{outline:3px solid #6bb3ff;outline-offset:2px}.review-inbox{margin:12px 0 18px;padding:13px;border:1px solid #49647b;background:#122333;border-radius:12px}.review-inbox h4{margin:0 0 10px}.review-entry{border-top:1px solid #3c536a;padding:12px 0}.review-entry strong,.review-entry small{display:block;overflow-wrap:anywhere}.review-form textarea{display:block;width:100%;min-height:65px;background:#0d1421;color:#fff;border:1px solid #53647e;border-radius:8px;font:inherit;padding:10px;resize:vertical}.review-actions{display:flex;gap:10px;flex-wrap:wrap;margin-top:9px}.review-actions button{flex:1;min-width:150px}.review-form textarea:focus-visible{outline:3px solid #6bb3ff;outline-offset:2px}footer{margin:30px 0 0;color:#9daec5;font-size:.79rem;border-top:1px solid #253449;padding-top:20px}.error{max-width:750px;margin:auto;padding:40px 18px}.error p{white-space:pre-wrap;overflow-wrap:anywhere}
@media(max-width:760px){.board-metrics{grid-template-columns:repeat(2,minmax(0,1fr))}.board-heading{flex-direction:column}.top{align-items:flex-start}.status{font-size:.65rem;max-width:145px}.stats{grid-template-columns:repeat(2,minmax(0,1fr))}.layout{grid-template-columns:minmax(0,1fr)}.panel{padding:15px}}
@media(max-width:370px){.inline{flex-direction:column}.status{display:none}.top{padding:16px}}
</style></head><body>
""";
}
