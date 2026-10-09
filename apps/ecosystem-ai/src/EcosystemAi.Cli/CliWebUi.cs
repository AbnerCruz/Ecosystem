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
/// segredos, execução de tarefas ou permissão remota no navegador.
/// Os POSTs apenas delegam ao LocalProjectStore já existente.
/// </summary>
public static class CliWebUi
{
    public const int DefaultPort = 8765;
    public const int MaxFormBytes = 32 * 1024;

    public static WebApplication CreateApp(string catalogDirectory, int port = DefaultPort,
        string? journalDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDirectory);
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Porta permitida: 1024–65535.");

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
                        cancellationToken: ctx.RequestAborted);
                await RespondHtml(ctx, CliWebUiHtml.Render(snapshot, csrfToken, audited));
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

        return app;
    }

    public static async Task ServeAsync(string catalogDirectory, int port = DefaultPort,
        CancellationToken cancellationToken = default, string? journalDirectory = null)
    {
        await using var app = CreateApp(catalogDirectory, port, journalDirectory);
        Console.WriteLine($"Ecosystem AI — UI local: http://127.0.0.1:{port}");
        Console.WriteLine("Somente neste dispositivo, sem IA, rede externa ou gasto de modelo. Ctrl+C encerra.");
        if (journalDirectory is not null)
            Console.WriteLine("Auditoria do Runtime ativa: agentes, tarefas, verificações e artefatos, sem payloads.");
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
