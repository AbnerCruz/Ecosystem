using System.Text;

namespace Lunet.Core;

/// <summary>Um problema (erro/aviso de compilação) para exportar.</summary>
public sealed record LogProblem(string Severity, string Id, string Message, string? File, int Line, int Column);

/// <summary>Monta o texto exportável de Console e Problems (para compartilhar em relatos de bug ou salvar).</summary>
public static class LogExport
{
    public static string Build(string projectName, string appVersion, string device, DateTimeOffset when,
        IEnumerable<string> consoleLines, IEnumerable<LogProblem> problems)
    {
        var console = consoleLines.ToList();
        var problemList = problems.ToList();
        var builder = new StringBuilder();
        builder.Append("Lunet ").Append(appVersion).Append(" — ").Append(projectName).Append('\n');
        builder.Append("Dispositivo: ").Append(device).Append('\n');
        builder.Append("Gerado em: ").Append(when.ToString("yyyy-MM-dd HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture)).Append("\n\n");

        builder.Append("== Problemas (").Append(problemList.Count).Append(") ==\n");
        if (problemList.Count == 0) builder.Append("(nenhum)\n");
        foreach (var problem in problemList)
        {
            builder.Append(problem.Severity).Append(' ').Append(problem.Id);
            if (problem.File is not null) builder.Append(' ').Append(problem.File).Append('(').Append(problem.Line).Append(',').Append(problem.Column).Append(')');
            builder.Append(": ").Append(problem.Message).Append('\n');
        }

        builder.Append("\n== Console (").Append(console.Count).Append(") ==\n");
        if (console.Count == 0) builder.Append("(vazio)\n");
        foreach (var line in console) builder.Append(line).Append('\n');
        return builder.ToString();
    }
}
