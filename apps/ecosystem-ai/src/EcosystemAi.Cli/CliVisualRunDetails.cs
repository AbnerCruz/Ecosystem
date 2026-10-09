using System.Security.Cryptography;
using System.Text;
using AgentRuntime;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace EcosystemAi.Cli;

/// <summary>
/// Projeção efêmera de runs registrados no catálogo e auditados pelo próprio
/// IEventLog do Runtime. O journal continua sendo a autoridade do replay.
/// Nenhum payload, argumento de ferramenta ou resposta do modelo é exposto.
/// </summary>
public sealed record VisualRunDetails(
    string RunId, string State, bool Verified, int Steps, int ToolCalls,
    string Agent, string? Model, IReadOnlyList<VisualArtifact> Artifacts);

public sealed record VisualArtifact(string Kind, string Name);

public static class CliVisualRunDetails
{
    public const int MaxReceiptsPerExport = 250;

    /// <summary>
    /// Retorna somente runs que têm eventos completos no journal. Um receipt sem
    /// journal não é tratado como verificado. Journal inválido falha fechado.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, VisualRunDetails>> ReadAsync(
        ProjectCatalog catalog, string journalDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(journalDirectory);
        var journal = Path.GetFullPath(journalDirectory);
        if (!Directory.Exists(journal))
            throw new DirectoryNotFoundException("Diretório de journal não existe; nada a auditar.");
        foreach (var project in catalog.Projects)
            CliRunJournalCommands.RequireOutsideWorkspace(journal, project.WorkspaceDirectory);

        var ids = catalog.Projects.SelectMany(p => p.Sessions)
            .SelectMany(s => s.Runs).Select(r => r.RunId)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length > MaxReceiptsPerExport)
            throw new InvalidOperationException("Snapshot de auditoria excede o limite de 250 runs.");
        var log = new LocalRunEventLog(journal);
        var result = new Dictionary<string, VisualRunDetails>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))).ToLowerInvariant();
            if (!File.Exists(Path.Combine(journal, digest + ".json")))
                continue; // Evidência ausente: não inventar um run auditado.
            var entries = await log.ReadRunAsync(id, cancellationToken);
            var state = RunState.Replay(entries);
            var agent = entries[0].Agent.Id;
            var model = entries.LastOrDefault(e => e.Model is not null)?.Model;
            var artifacts = state.Artifacts
                .Select(a => new VisualArtifact(a.Kind, SafeFileName(a.Location)))
                .ToArray();
            result.Add(id, new VisualRunDetails(id, state.Status.ToString(), state.Verified,
                state.Steps, state.ToolCalls, agent, model, artifacts));
        }
        return result;
    }

    private static string SafeFileName(string location)
    {
        if (string.IsNullOrWhiteSpace(location)) return "(sem nome)";
        var normalized = location.Replace('\\', '/');
        return normalized[(normalized.LastIndexOf('/') + 1)..] is { Length: > 0 } name
            ? name : "(sem nome)";
    }
}
