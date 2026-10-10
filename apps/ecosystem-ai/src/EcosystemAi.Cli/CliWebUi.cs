using System.Net;
using System.Security.Cryptography;
using EcosystemAi.ProjectStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace EcosystemAi.Cli;

/// <summary>
/// UI HTTP local opt-in, somente loopback. Não expõe AgentRunner, modelo,
/// segredos nem permissão remota no navegador. A execução opt-in de
/// tarefas usa os mesmos comandos CLI/Workspace, sem grants de escrita.
/// </summary>
public static class CliWebUi
{
    public const int DefaultPort = 8765;
    public const int MaxFormBytes = 32 * 1024;

    public static WebApplication CreateApp(string catalogDirectory, int port = DefaultPort,
        string? journalDirectory = null, bool embedTextPreviews = false,
        CliWebTaskRunner? taskRunner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDirectory);
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Porta permitida: 1024–65535.");

        if (embedTextPreviews && journalDirectory is null)
            throw new ArgumentException("Prévia de texto exige --journal.");

        var catalogPath = Path.GetFullPath(catalogDirectory);
        var journalPath = journalDirectory is null ? null : Path.GetFullPath(journalDirectory);
        // A fonte de verdade é exclusivamente o catálogo canônico. Se já
        // houver projetos, recusar catálogos acidentalmente expostos aos agentes.
        var store = new LocalProjectStore(catalogPath);
        ProjectCatalog ReadCatalog()
        {
            var result = store.Read();
            foreach (var project in result.Projects)
            {
                CliRunJournalCommands.RequireOutsideWorkspace(catalogPath, project.WorkspaceDirectory);
                if (journalPath is not null)
                    CliRunJournalCommands.RequireOutsideWorkspace(journalPath, project.WorkspaceDirectory);
            }
            return result;
        }
        _ = ReadCatalog();
        if (journalPath is not null && !Directory.Exists(journalPath))
            throw new DirectoryNotFoundException("Journal auditável não encontrado.");

        // Token apenas em RAM; não vai à URL, a arquivo nem a log.
        var csrfToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();

        app.Use(async (ctx, next) =>
        {
            // Binding loopback não basta contra DNS rebinding. Recusar Host
            // arbitrário antes de mostrar dados ou token CSRF.
            if (!IsTrustedHost(ctx, port))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            ctx.Response.Headers["Content-Security-Policy"] =
                "default-src 'none'; style-src 'unsafe-inline'; script-src 'none'; " +
                "connect-src 'none'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
            ctx.Response.Headers["Cache-Control"] = "no-store";
            await next();
        });

        app.MapGet("/", async ctx =>
        {
            try
            {
                var snapshot = ReadCatalog();
                var audited = journalPath is null ? null
                    : await CliVisualRunDetails.ReadAsync(snapshot, journalPath,
                        includeTextPreviews: embedTextPreviews,
                        cancellationToken: ctx.RequestAborted);
                var roster = new LocalAgentRosterStore(catalogPath).Read();
                var reviews = new LocalTeamReviewStore(catalogPath).Read();
                await RespondHtml(ctx, CliWebUiHtml.Render(snapshot, csrfToken, audited,
                    taskRunner?.Board, roster, reviews));
            }
            catch (Exception e) when (IsExpectedError(e))
            {
                await RespondHtml(ctx, CliWebUiHtml.RenderError("Catálogo indisponível.", e.Message),
                    StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/projects", async ctx =>
        {
            if (!await VerifyPost(ctx, port, csrfToken)) return;
            try
            {
                _ = ReadCatalog();
                var form = await ctx.Request.ReadFormAsync();
                if (!ValidForm(form, "csrf", "name", "workspace"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                var project = CliCatalogManagement.CreateProject(catalogPath,
                    form["workspace"].ToString(), form["name"].ToString());
                ctx.Response.StatusCode = StatusCodes.Status303SeeOther;
                ctx.Response.Headers.Location = "/#p-" + Uri.EscapeDataString(project.Id);
            }
            catch (Exception e) when (IsExpectedError(e))
            {
                await RespondHtml(ctx, CliWebUiHtml.RenderError("Projeto não criado.", e.Message),
                    StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/sessions", async ctx =>
        {
            if (!await VerifyPost(ctx, port, csrfToken)) return;
            try
            {
                _ = ReadCatalog();
                var form = await ctx.Request.ReadFormAsync();
                if (!ValidForm(form, "csrf", "projectId", "title"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                var session = CliCatalogManagement.CreateSession(catalogPath,
                    form["projectId"].ToString(), form["title"].ToString());
                ctx.Response.StatusCode = StatusCodes.Status303SeeOther;
                ctx.Response.Headers.Location = "/#s-" + Uri.EscapeDataString(session.Id);
            }
            catch (Exception e) when (IsExpectedError(e))
            {
                await RespondHtml(ctx, CliWebUiHtml.RenderError("Sessão não criada.", e.Message),
                    StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/agents", async ctx =>
        {
            if (!await VerifyPost(ctx, port, csrfToken)) return;
            try
            {
                _ = ReadCatalog();
                var form = await ctx.Request.ReadFormAsync();
                if (!ValidForm(form, "csrf", "projectId", "name", "instructions"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                new LocalAgentRosterStore(catalogPath).CreateAgent(
                    form["projectId"].ToString(), form["name"].ToString(),
                    form["instructions"].ToString());
                ctx.Response.StatusCode = StatusCodes.Status303SeeOther;
                ctx.Response.Headers.Location = "/#p-" +
                    Uri.EscapeDataString(form["projectId"].ToString());
            }
            catch (Exception e) when (IsExpectedError(e))
            {
                await RespondHtml(ctx, CliWebUiHtml.RenderError(
                    "Agente não criado.", "Verifique os campos e os limites do projeto."),
                    StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/teams", async ctx =>
        {
            if (!await VerifyPost(ctx, port, csrfToken)) return;
            try
            {
                _ = ReadCatalog();
                var form = await ctx.Request.ReadFormAsync();
                if (!ValidForm(form, "csrf", "projectId", "name", "producerId", "reviewerId"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                new LocalAgentRosterStore(catalogPath).CreateTeam(
                    form["projectId"].ToString(), form["name"].ToString(),
                    form["producerId"].ToString(), form["reviewerId"].ToString());
                ctx.Response.StatusCode = StatusCodes.Status303SeeOther;
                ctx.Response.Headers.Location = "/#p-" +
                    Uri.EscapeDataString(form["projectId"].ToString());
            }
            catch (Exception e) when (IsExpectedError(e))
            {
                await RespondHtml(ctx, CliWebUiHtml.RenderError(
                    "Equipe não criada.", "Selecione dois agentes diferentes do mesmo projeto."),
                    StatusCodes.Status400BadRequest);
            }
        });

        app.MapPost("/review-decisions", async ctx =>
        {
            if (!await VerifyPost(ctx, port, csrfToken)) return;
            try
            {
                _ = ReadCatalog();
                var form = await ctx.Request.ReadFormAsync();
                if (!ValidForm(form, "csrf", "reviewId", "decision", "note"))
                {
                    ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                var selected = new LocalTeamReviewStore(catalogPath).Decide(
                    form["reviewId"].ToString(), form["decision"].ToString(),
                    form["note"].ToString());
                ctx.Response.StatusCode = StatusCodes.Status303SeeOther;
                ctx.Response.Headers.Location = "/#s-" + Uri.EscapeDataString(selected.SessionId);
            }
            catch (Exception e) when (IsExpectedError(e))
            {
                await RespondHtml(ctx, CliWebUiHtml.RenderError(
                    "Parecer não decidido.",
                    "Confira o parecer pendente, seus recibos e a justificativa. Nenhum arquivo foi integrado."),
                    StatusCodes.Status400BadRequest);
            }
        });

        if (taskRunner is not null)
        {
            app.MapPost("/tasks", async ctx =>
            {
                if (!await VerifyPost(ctx, port, csrfToken)) return;
                try
                {
                    _ = ReadCatalog(); // Mesmo guard do GET, antes da execução.
                    var form = await ctx.Request.ReadFormAsync();
                    if (!ValidForm(form, "csrf", "projectId", "sessionId", "goal")
                        && !ValidForm(form, "csrf", "projectId", "sessionId", "goal", "assignee"))
                    {
                        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }
                    var result = await taskRunner.SubmitAsync(
                        form["projectId"].ToString(),
                        form["sessionId"].ToString(),
                        form["goal"].ToString(),
                        form.TryGetValue("assignee", out var assignee)
                            ? assignee.ToString() : "default");
                    switch (result.State)
                    {
                        case WebTaskState.Succeeded:
                        case WebTaskState.Reviewed:
                            ctx.Response.StatusCode = StatusCodes.Status303SeeOther;
                            ctx.Response.Headers.Location = "/#s-" +
                                Uri.EscapeDataString(form["sessionId"].ToString());
                            return;
                        case WebTaskState.Busy:
                            await RespondHtml(ctx, CliWebUiHtml.RenderError(
                                "Já existe uma tarefa ativa.",
                                "O painel não cria fila automática. Volte após a execução."),
                                StatusCodes.Status409Conflict);
                            return;
                        case WebTaskState.QuotaExceeded:
                            await RespondHtml(ctx, CliWebUiHtml.RenderError(
                                "Limite desta sessão do servidor atingido.",
                                "Reinicie com uma autorização explícita de orçamento ou número de tarefas."),
                                StatusCodes.Status429TooManyRequests);
                            return;
                        case WebTaskState.ReviewIncomplete:
                            await RespondHtml(ctx, CliWebUiHtml.RenderError(
                                "Revisão independente não concluída.",
                                "O produtor pode ter sido executado. Confira runs e recibos no catálogo; " +
                                "não há aprovação ou integração automática."),
                                StatusCodes.Status422UnprocessableEntity);
                            return;
                        case WebTaskState.Invalid:
                            await RespondHtml(ctx, CliWebUiHtml.RenderError(
                                "Tarefa recusada.",
                                "Confira projeto, sessão e configurações locais. Nenhuma permissão extra foi concedida."),
                                StatusCodes.Status400BadRequest);
                            return;
                        default:
                            await RespondHtml(ctx, CliWebUiHtml.RenderError(
                                "Tarefa não concluída.",
                                "Confira o histórico da sessão e a auditoria do Runtime."),
                                StatusCodes.Status422UnprocessableEntity);
                            return;
                    }
                }
                catch (Exception e) when (IsExpectedError(e))
                {
                    // Não transmitir detalhes do provider, caminho de projeto,
                    // segredo ou estado interno no body HTTP.
                    await RespondHtml(ctx, CliWebUiHtml.RenderError(
                        "Execução indisponível.",
                        "Confira a configuração local e o terminal do Product."),
                        StatusCodes.Status400BadRequest);
                }
            });
        }

        return app;
    }

    public static async Task ServeAsync(string catalogDirectory, int port = DefaultPort,
        CancellationToken cancellationToken = default, string? journalDirectory = null,
        bool embedTextPreviews = false, CliWebTaskRunner? taskRunner = null)
    {
        await using var app = CreateApp(catalogDirectory, port, journalDirectory,
            embedTextPreviews, taskRunner);
        Console.WriteLine($"Ecosystem AI — UI local: http://127.0.0.1:{port}");
        Console.WriteLine("Somente neste dispositivo, sem IA, rede externa ou gasto de modelo. Ctrl+C encerra.");
        if (journalDirectory is not null)
            Console.WriteLine("Auditoria do Runtime ativa: agentes, tarefas, verificações e artefatos, sem payloads.");
        if (embedTextPreviews)
            Console.WriteLine("PRÉVIAS ATIVADAS: conteúdo de arquivos textuais atuais será exibido no browser local.");
        if (taskRunner is not null)
            Console.WriteLine("EXECUÇÃO OPT-IN: tarefas manuais via browser; somente files.read, teto local limitado.");
        if (taskRunner?.Board.ReviewTeams == true)
            Console.WriteLine("REVISÃO DE EQUIPE: produtor seguido do revisor, dois runs pagos; nenhuma aprovação ou integração automática.");
        await ((IHost)app).RunAsync(cancellationToken);
    }

    private static bool IsTrustedHost(HttpContext context, int port) =>
        string.Equals(context.Request.Host.Host, "127.0.0.1", StringComparison.Ordinal)
        && context.Request.Host.Port == port
        && context.Connection.LocalIpAddress is { } local
        && IPAddress.IsLoopback(local);

    private static async Task<bool> VerifyPost(HttpContext ctx, int port, string token)
    {
        var origin = ctx.Request.Headers.Origin.ToString();
        if (origin.Length != 0 && !string.Equals(origin, $"http://127.0.0.1:{port}",
                StringComparison.Ordinal))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return false;
        }
        if (ctx.Request.ContentLength is null or < 1 or > MaxFormBytes
            || ctx.Request.ContentType is null
            || !ctx.Request.ContentType.StartsWith("application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return false;
        }
        var form = await ctx.Request.ReadFormAsync();
        if (!form.TryGetValue("csrf", out var submitted) || submitted.Count != 1
            || !CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(submitted.ToString()),
                System.Text.Encoding.UTF8.GetBytes(token)))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return false;
        }
        return true;
    }

    private static bool ValidForm(IFormCollection form, params string[] keys) =>
        form.Count == keys.Length
        && keys.All(key => form.TryGetValue(key, out var value) && value.Count == 1);

    private static async Task RespondHtml(HttpContext ctx, string html, int status = 200)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync(html);
    }

    private static bool IsExpectedError(Exception e) =>
        e is ArgumentException or KeyNotFoundException or IOException
            or InvalidDataException or InvalidOperationException or UnauthorizedAccessException;
}
