using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

public static class CliHistoryCommands
{
    public static IReadOnlyList<string> List(string directory)
    {
        var catalog = ReadExisting(directory);
        var lines = new List<string> { $"Projetos: {catalog.Projects.Count}; revisão: {catalog.Revision}" };
        foreach (var project in catalog.Projects)
        {
            lines.Add($"[project] {project.Id} — {project.Name} ({project.Sessions.Count} sessões)");
            foreach (var session in project.Sessions)
                lines.Add($"  [session] {session.Id} — {session.Title} ({session.Turns.Count} mensagens, {session.Runs.Count} runs)");
        }
        return lines;
    }

    public static IReadOnlyList<string> Show(string directory, string projectId, string sessionId)
    {
        var catalog = ReadExisting(directory);
        var project = catalog.Projects.FirstOrDefault(x => x.Id == projectId)
            ?? throw new KeyNotFoundException("Projeto não encontrado.");
        var session = project.Sessions.FirstOrDefault(x => x.Id == sessionId)
            ?? throw new KeyNotFoundException("Sessão não encontrada.");
        var lines = new List<string>
        {
            $"Projeto: {project.Name} ({project.Id})",
            $"Sessão: {session.Title} ({session.Id})"
        };
        foreach (var turn in session.Turns)
            lines.Add($"[{turn.At:O}] {turn.Role}: {turn.Text}");
        foreach (var receipt in session.Runs)
            lines.Add($"[run] {receipt.RunId} status={receipt.Status} verificado={receipt.Verified} " +
                $"custo={(receipt.CostEstimated ? "estimado " : "")}{receipt.CostMinor} {receipt.Currency} minor " +
                $"evidência={receipt.Verification ?? "indisponível"}");
        return lines;
    }

    private static ProjectCatalog ReadExisting(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!File.Exists(Path.Combine(Path.GetFullPath(directory), "catalog.json")))
            throw new FileNotFoundException("Catálogo inexistente; leitura não cria histórico.");
        return new LocalProjectStore(directory).Read();
    }
}
