using System.Globalization;
using System.Text;
using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

/// <summary>
/// Downloads sem rede, geração de arquivo ou acesso ao workspace.
/// Exportam apenas dados já presentes no catálogo canônico do Product.
/// </summary>
public sealed record CliProductDownload(string FileName, string ContentType, byte[] Bytes);

public static class CliProductDownloads
{
    public const int MaxCsvBytes = 2 * 1024 * 1024;

    public static CliProductDownload Response(ProjectCatalog catalog, string projectId,
        string sessionId, int turnIndex, string format)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!Guid.TryParseExact(projectId, "N", out _)
            || !Guid.TryParseExact(sessionId, "N", out _)
            || format is not ("md" or "txt") || turnIndex < 0)
            throw new ArgumentException("Identificadores, índice ou formato inválidos.");

        var project = catalog.Projects.SingleOrDefault(p => p.Id == projectId)
            ?? throw new KeyNotFoundException("Projeto não encontrado.");
        var session = project.Sessions.SingleOrDefault(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Sessão não pertence ao projeto.");
        if (turnIndex >= session.Turns.Count
            || session.Turns[turnIndex].Role != "assistant")
            throw new KeyNotFoundException("Não existe resposta do agente neste índice.");

        // O catálogo já guarda a resposta redigida; exportação não
        // apresenta artefatos de filesystem nem inventa geração de arquivo.
        var bytes = new UTF8Encoding(false).GetBytes(session.Turns[turnIndex].Text);
        var name = "ecosystem-ai-" + sessionId[..8] + "-resposta-"
            + (turnIndex + 1).ToString("D4", CultureInfo.InvariantCulture) + "." + format;
        return new CliProductDownload(name,
            format == "md" ? "text/markdown; charset=utf-8" : "text/plain; charset=utf-8",
            bytes);
    }

    public static CliProductDownload Costs(ProjectCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var csv = new StringBuilder("ProjectId,SessionId,RunId,RecordedAtUtc,Status,Verified,CostMinor,Currency,CostEstimated\r\n");
        foreach (var project in catalog.Projects)
        foreach (var session in project.Sessions)
        foreach (var run in session.Runs)
        {
            csv.Append(Cell(project.Id)).Append(',').Append(Cell(session.Id)).Append(',')
                .Append(Cell(run.RunId)).Append(',')
                .Append(Cell(run.At.UtcDateTime.ToString("O", CultureInfo.InvariantCulture))).Append(',')
                .Append(Cell(run.Status)).Append(',')
                .Append(run.Verified ? "true" : "false").Append(',')
                .Append(run.CostMinor.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Cell(run.Currency)).Append(',')
                .Append(run.CostEstimated ? "true" : "false")
                .Append("\r\n");
            if (csv.Length > MaxCsvBytes)
                throw new InvalidDataException("Relatório de custos excede o limite seguro.");
        }

        var bytes = new UTF8Encoding(false).GetBytes(csv.ToString());
        if (bytes.Length > MaxCsvBytes)
            throw new InvalidDataException("Relatório de custos excede o limite seguro.");
        return new CliProductDownload("ecosystem-ai-custos.csv",
            "text/csv; charset=utf-8", bytes);
    }

    private static string Cell(string value)
    {
        // RunId vem do Store e pode conter caracteres controlados por outro
        // escritor local. Evitar formula injection ao abrir em planilhas.
        if (value.Length > 0 && (value[0] is '=' or '+' or '-' or '@'
            || char.IsWhiteSpace(value[0])))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
