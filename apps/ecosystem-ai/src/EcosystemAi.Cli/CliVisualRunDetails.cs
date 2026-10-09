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

public sealed record VisualArtifact(string Kind, string Name, string? TextPreview = null);

public static class CliVisualRunDetails
{
    public const int MaxReceiptsPerExport = 250;

    /// <summary>
    /// Retorna somente runs que têm eventos completos no journal. Um receipt sem
    /// journal não é tratado como verificado. Journal inválido falha fechado.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, VisualRunDetails>> ReadAsync(
        ProjectCatalog catalog, string journalDirectory, bool includeTextPreviews = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(journalDirectory);
        var journal = Path.GetFullPath(journalDirectory);
        if (!Directory.Exists(journal))
            throw new DirectoryNotFoundException("Diretório de journal não existe; nada a auditar.");
        foreach (var project in catalog.Projects)
            CliRunJournalCommands.RequireOutsideWorkspace(journal, project.WorkspaceDirectory);

        var references = catalog.Projects.SelectMany(p => p.Sessions
            .SelectMany(s => s.Runs.Select(r => (r.RunId, p.WorkspaceDirectory)))).ToArray();
        var ids = references.Select(r => r.RunId).Distinct(StringComparer.Ordinal).ToArray();
        // Mesmo run referenciado por múltiplos workspaces não tem uma origem
        // única: é seguro auditar, mas nunca escolher de onde ler o arquivo.
        if (includeTextPreviews && references.GroupBy(r => r.RunId, StringComparer.Ordinal)
            .Any(g => g.Select(r => r.WorkspaceDirectory).Distinct(
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() > 1))
            throw new InvalidDataException("Run associado a mais de um workspace; previews recusados.");
        var totalPreviewBytes = 0;
        var previewCount = 0;
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
            var workspace = includeTextPreviews
                ? references.First(r => r.RunId == id).WorkspaceDirectory : null;
            var artifacts = new List<VisualArtifact>();
            foreach (var artifact in state.Artifacts)
            {
                var preview = workspace is null ? null : CliArtifactTextPreview.Read(
                    artifact, workspace, ref totalPreviewBytes, ref previewCount);
                artifacts.Add(new VisualArtifact(artifact.Kind, SafeFileName(artifact.Location), preview));
            }
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
