using System.Security.Cryptography;
using System.Text;
using AgentRuntime;
using EcosystemAi.RunJournal;

namespace EcosystemAi.Cli;

/// <summary>
/// Leitura redigida dos metadados de um run durável: não imprime Payload, Result,
/// prompts, argumentos ou conteúdo de arquivos. O replay continua no Core.
/// </summary>
public static class CliRunJournalCommands
{
    public static void RequireOutsideWorkspace(string journalDirectory, string workspaceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var journal = Path.GetFullPath(journalDirectory);
        var workspace = Path.GetFullPath(workspaceDirectory);
        // Paths lexicalmente fora do workspace podem entrar nele por um
        // ancestral simbólico; recusar esse alias antes de criar o journal.
        EnsureNoLinkAncestors(journal);
        EnsureNoLinkAncestors(workspace);
        var relative = Path.GetRelativePath(workspace, journal);
        if (relative == "." || (!Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
            throw new ArgumentException("O journal precisa ficar fora do projeto autorizado ao agente.");
    }

    private static void EnsureNoLinkAncestors(string fullPath)
    {
        for (DirectoryInfo? current = new(fullPath); current is not null; current = current.Parent)
            if (current.LinkTarget is not null)
                throw new ArgumentException("Journal e workspace não podem utilizar ancestral simbólico.");
    }

    public static async Task<IReadOnlyList<string>> ShowAsync(string directory, string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        var root = Path.GetFullPath(directory);
        // --show-run é leitura: não cria pasta vazia ao consultar um run inexistente.
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("Diretório de journal inexistente.");
        var hashed = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(runId))).ToLowerInvariant();
        if (!File.Exists(Path.Combine(root, hashed + ".json")))
            throw new FileNotFoundException("Run não encontrado no journal.");
        var events = await new LocalRunEventLog(root).ReadRunAsync(runId, CancellationToken.None);
        var state = RunState.Replay(events);
        var lines = new List<string>
        {
            $"Run: {runId}",
            $"Estado: {state.Status}; verificado: {state.Verified}; passos: {state.Steps}; ferramentas: {state.ToolCalls}",
            $"Custo registrado: {state.Cost?.ToString() ?? "não informado"}",
            $"Eventos validados: {events.Count}"
        };
        // A auditoria estrutural não expõe dados confidenciais do payload persistido.
        foreach (var entry in events)
            lines.Add($"  #{entry.Sequence}: {entry.Kind}");
        return lines;
    }
}
