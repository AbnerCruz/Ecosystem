using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

/// <summary>
/// Projeção visual offline e somente leitura do catálogo canônico do Product.
/// Não cria servidor, sessão de agente, ledger, estado paralelo nem recursos externos.
/// </summary>
public static class CliHtmlSnapshot
{
    public static string Export(string catalogDirectory, string outputFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFile);
        var sourceDirectory = Path.GetFullPath(catalogDirectory);
        // Ausência não é um catálogo vazio: não inicializar armazenamento em modo consulta.
        if (!File.Exists(Path.Combine(sourceDirectory, "catalog.json")))
            throw new FileNotFoundException("Catálogo inexistente; exportação não cria histórico.");

        var output = Path.GetFullPath(outputFile);
        if (!string.Equals(Path.GetExtension(output), ".html", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A saída deve ser um arquivo .html.", nameof(outputFile));
        var parent = Path.GetDirectoryName(output)
            ?? throw new ArgumentException("Arquivo de saída sem diretório.", nameof(outputFile));
        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException("O diretório de saída precisa existir.");
        EnsureNoSymlinkAncestors(parent);
        if (IsWithin(output, sourceDirectory))
            throw new ArgumentException("Não salve o HTML dentro do catálogo de conversas.", nameof(outputFile));

        var catalog = new LocalProjectStore(sourceDirectory).Read();
        foreach (var project in catalog.Projects)
        {
            // Recusar workspace que atravesse link: um alias pode apontar
            // para a saída sem parecer contido no path lexical.
            EnsureNoSymlinkAncestors(project.WorkspaceDirectory);
            if (IsWithin(output, project.WorkspaceDirectory))
                throw new ArgumentException("Não salve conversas exportadas dentro de um workspace acessível ao agente.", nameof(outputFile));
        }

        // Nunca sobrescrever exportações ou seguir arquivo de destino simbólico.
        if (File.Exists(output) || new FileInfo(output).LinkTarget is not null)
            throw new IOException("O destino já existe; escolha outro nome para o snapshot.");
        var bytes = new UTF8Encoding(false).GetBytes(Render(catalog));
        var temporary = Path.Combine(parent, ".ecosystem-ai-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, output); // Sem overwrite. Rename no mesmo diretório/volume.
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return output;
    }

    public static string Render(ProjectCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var sb = new StringBuilder();
        string E(string? value) => HtmlEncoder.Default.Encode(value ?? "");
        string Date(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

        var sessions = catalog.Projects.SelectMany(p => p.Sessions).ToArray();
        var receipts = sessions.SelectMany(s => s.Runs).ToArray();
        var totals = receipts.GroupBy(r => r.Currency, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (Currency: g.Key, Minor: g.Sum(r => (decimal)r.CostMinor))).ToArray();

        sb.Append("""
<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<meta name="color-scheme" content="dark">
<meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'">
<title>Ecosystem AI — Histórico local</title>
<style>
:root{color-scheme:dark;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;background:#0e121b;color:#eff2f8}
*{box-sizing:border-box}body{margin:0;line-height:1.48}main{max-width:1080px;margin:auto;padding:16px clamp(14px,4vw,32px) 60px}
h1{font-size:clamp(1.65rem,5vw,2.5rem);margin:.1em 0}h2{font-size:1.3rem;margin:0 0 12px}h3{font-size:1rem;margin:0}
p{margin:.45em 0 1em}header{padding:32px 0 16px}header p,small,.muted{color:#a4b0c6}
.summary,.project,.session-body,.message,.receipt{border:1px solid #30394a;border-radius:14px;background:#181f2c}
.summary{padding:16px;margin:16px 0 24px}.metric-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(125px,1fr));gap:12px}
.metric{padding:8px 0}.metric strong{font-size:1.45rem;display:block;font-variant-numeric:tabular-nums}.metric span{color:#a4b0c6}
nav{display:flex;flex-wrap:wrap;gap:8px;margin:18px 0 26px}a{color:#b5d1ff}nav a{border:1px solid #3c4c69;padding:8px 12px;border-radius:10px;text-decoration:none}
.project{padding:18px;margin:0 0 20px}.project>header{padding:0}.project>header p{overflow-wrap:anywhere}
details{margin:10px 0;border:1px solid #343e51;border-radius:12px;overflow:hidden}summary{padding:14px;cursor:pointer;display:flex;justify-content:space-between;gap:12px;align-items:center}
summary:focus-visible,a:focus-visible{outline:3px solid #92bfff;outline-offset:2px}summary::marker{color:#92bfff}
.session-body{border:0;border-top:1px solid #343e51;border-radius:0;background:#131925;padding:12px}
.message{padding:13px 15px;margin:10px 0}.message.assistant{border-left:4px solid #86b6ff}.message.user{border-left:4px solid #abb5c8}
.message p{white-space:pre-wrap;overflow-wrap:anywhere;margin:10px 0 0}.message header{padding:0;display:flex;justify-content:space-between;gap:10px;flex-wrap:wrap}
.scroll{overflow-x:auto}table{border-collapse:collapse;width:100%;min-width:560px;font-size:.88rem}th,td{text-align:left;border-bottom:1px solid #354054;padding:11px 8px;vertical-align:top;overflow-wrap:anywhere}th{color:#c6d1e4}
.tag{border-radius:7px;background:#2d3a50;padding:3px 7px;font-size:.77rem;white-space:nowrap}
.ok{background:#174c3d}.bad{background:#53313b}
footer{margin-top:35px;color:#a4b0c6;font-size:.88rem}
@media(min-width:700px){main{padding-top:18px}.project{padding:24px}}
@media(prefers-reduced-motion:no-preference){a,summary{scroll-margin-top:14px}}
</style>
</head>
<body><main>
<header><p class="muted">ECOSYSTEM AI · ARQUIVO LOCAL</p>
<h1>Projetos e conversas</h1>
<p>Visualização offline e somente leitura de um snapshot. Não executa agentes, não consulta provedores e não sincroniza.</p></header>
<section class="summary" aria-label="Resumo">
<div class="metric-grid">
""");
        void Metric(string count, string label) =>
            sb.Append("<div class=\"metric\"><strong>").Append(E(count)).Append("</strong><span>")
                .Append(E(label)).Append("</span></div>");
        Metric(catalog.Projects.Count.ToString(CultureInfo.InvariantCulture), "projetos");
        Metric(sessions.Length.ToString(CultureInfo.InvariantCulture), "sessões");
        Metric(sessions.Sum(s => s.Turns.Count).ToString(CultureInfo.InvariantCulture), "mensagens");
        Metric(receipts.Length.ToString(CultureInfo.InvariantCulture), "execuções");
        sb.Append("</div><p class=\"muted\">Revisão do catálogo: ")
            .Append(catalog.Revision.ToString(CultureInfo.InvariantCulture)).Append("</p>");
        if (totals.Length != 0)
        {
            sb.Append("<p><strong>Custos registrados:</strong> ");
            sb.Append(string.Join(" · ", totals.Select(x => E(x.Minor.ToString(CultureInfo.InvariantCulture))
                + " unidades mínimas " + E(x.Currency))));
            sb.Append(" <small>(podem incluir estimativas; não são faturas)</small></p>");
        }
        sb.Append("</section>");

        if (catalog.Projects.Count == 0) sb.Append("<p>Nenhum projeto salvo neste catálogo.</p>");
        else
        {
            sb.Append("<nav aria-label=\"Ir para projeto\">");
            foreach (var project in catalog.Projects)
                sb.Append("<a href=\"#p-").Append(E(project.Id)).Append("\">")
                    .Append(E(project.Name)).Append("</a>");
            sb.Append("</nav>");
        }

        foreach (var project in catalog.Projects)
        {
            sb.Append("<section class=\"project\" id=\"p-").Append(E(project.Id)).Append("\"><header><h2>")
                .Append(E(project.Name)).Append("</h2><p>")
                .Append(project.Sessions.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" sessões · criado ").Append(E(Date(project.CreatedAt)))
                .Append("</p></header>");
            // Não exportar caminhos de workspaces: o HTML pode ser movido a outro aparelho.
            if (project.Sessions.Count == 0) sb.Append("<p class=\"muted\">Nenhuma sessão registrada.</p>");
            foreach (var session in project.Sessions)
            {
                sb.Append("<details id=\"s-").Append(E(session.Id)).Append("\"><summary><span>")
                    .Append(E(session.Title)).Append("</span><small>")
                    .Append(session.Turns.Count.ToString(CultureInfo.InvariantCulture)).Append(" mensagens · ")
                    .Append(session.Runs.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" execuções</small></summary><div class=\"session-body\">");
                if (session.Turns.Count == 0) sb.Append("<p class=\"muted\">Nenhuma mensagem salva nesta sessão.</p>");
                foreach (var turn in session.Turns)
                {
                    var role = turn.Role == "assistant" ? "assistant" : "user";
                    sb.Append("<article class=\"message ").Append(role).Append("\"><header><strong>")
                        .Append(role == "assistant" ? "Assistente" : "Usuário")
                        .Append("</strong><small>").Append(E(Date(turn.At))).Append("</small></header><p>")
                        .Append(E(turn.Text)).Append("</p></article>");
                }
                if (session.Runs.Count > 0)
                {
                    sb.Append("<h3>Execuções registradas</h3><div class=\"scroll\"><table><thead><tr>")
                        .Append("<th>ID</th><th>Estado</th><th>Verificação</th><th>Custo registrado</th><th>Data</th>")
                        .Append("</tr></thead><tbody>");
                    foreach (var run in session.Runs)
                    {
                        var statusClass = run.Status == "succeeded" ? "ok" : run.Status == "failed" ? "bad" : "";
                        sb.Append("<tr><td>").Append(E(run.RunId)).Append("</td><td><span class=\"tag ")
                            .Append(statusClass).Append("\">").Append(E(run.Status))
                            .Append("</span></td><td>").Append(run.Verified ? "Verificado" : "Não verificado");
                        if (!string.IsNullOrWhiteSpace(run.Verification))
                            sb.Append("<small> · ").Append(E(run.Verification)).Append("</small>");
                        sb.Append("</td><td>").Append(run.CostEstimated ? "estimado " : "")
                            .Append(run.CostMinor.ToString(CultureInfo.InvariantCulture))
                            .Append(" unidades mínimas ").Append(E(run.Currency)).Append("</td><td>")
                            .Append(E(Date(run.At))).Append("</td></tr>");
                    }
                    sb.Append("</tbody></table></div>");
                }
                sb.Append("</div></details>");
            }
            sb.Append("</section>");
        }
        sb.Append("""
<footer>
<p>Conteúdo derivado do catálogo salvo localmente. Mensagens e evidências podem conter dados privados: mantenha este HTML em local protegido e não o publique.</p>
<p>Snapshot estático — alterar este arquivo não altera o catálogo e nenhuma verificação de execução é refeita.</p>
</footer></main></body></html>
""");
        return sb.ToString();
    }

    private static bool IsWithin(string candidate, string root)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), candidate);
        return relative == "." || (!Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal));
    }

    private static void EnsureNoSymlinkAncestors(string directory)
    {
        for (DirectoryInfo? current = new(directory); current is not null; current = current.Parent)
            if (current.LinkTarget is not null)
                throw new IOException("Saída através de diretório simbólico não permitida.");
    }
}
