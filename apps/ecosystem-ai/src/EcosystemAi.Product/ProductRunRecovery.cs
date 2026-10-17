using AgentRuntime;
using EcosystemAi.ProjectStore;
using EcosystemAi.RunJournal;

namespace EcosystemAi.Product;

public sealed record ProjectRunRecoveryResult(IReadOnlyList<JournalRunSummary> Runs,
    int UnavailableFiles = 0, bool Truncated = false, bool JournalUnavailable = false);

/// <summary>Descobre execuções sem recibo, por projeto. Não infere conversa nem autoriza retomada.</summary>
public static class ProductRunRecovery
{
    public static ProjectRunRecoveryResult Read(ProjectCatalog catalog, string projectId, string journalDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        cancellationToken.ThrowIfCancellationRequested();
        var project = catalog.Projects.SingleOrDefault(p => p.Id == projectId)
            ?? throw new KeyNotFoundException("Projeto inexistente.");
        try
        {
            ProductExecutionHost.RequireOutside(journalDirectory, project.WorkspaceDirectory);
            if (!Directory.Exists(journalDirectory)) return new([]);
            var inventory = new LocalRunEventLog(journalDirectory).InspectRuns(cancellationToken: cancellationToken);
            var known = catalog.Projects.SelectMany(p => p.Sessions).SelectMany(s => s.Runs)
                .Select(r => r.RunId).ToHashSet(StringComparer.Ordinal);
            var context = ContextPath.Create(new(ContextLevel.Ecosystem, "ecosystem"),
                new(ContextLevel.Product, "ecosystem-ai"), new(ContextLevel.Project, project.Id)).ToString();
            return new(inventory.Runs.Where(r => r.ContextRef == context && !known.Contains(r.RunId))
                .OrderByDescending(r => r.At).ToArray(), inventory.UnavailableFiles, inventory.Truncated);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            return new([], JournalUnavailable: true);
        }
    }
}
