namespace Urbe.Core;

public sealed record PersistedPage(string Path, PageDocument Page);

public sealed record CompositionMigrationConflict(
    string CompositionId,
    string TargetPath,
    string Reason);

public sealed record CompositionMigrationPlan(
    bool Blocked,
    string? BlockReason,
    IReadOnlyList<CompositionPageConversion> Creates,
    IReadOnlyList<string> AlreadyMigratedCompositionIds,
    IReadOnlyList<CompositionMigrationConflict> Conflicts)
{
    public bool HasChanges => !Blocked && Creates.Count > 0;
}

/// <summary>
/// Pure planning step for ADR-0009. It never writes a vault. The caller can
/// inspect every target before the critical backup/write stage is introduced.
/// </summary>
public static class CompositionMigrationPlanner
{
    public static CompositionMigrationPlan Plan(
        CompositionFile compositions,
        DocumentStore documents,
        IEnumerable<PersistedPage>? existingPages = null)
    {
        ArgumentNullException.ThrowIfNull(compositions);
        ArgumentNullException.ThrowIfNull(documents);

        if (compositions.State == CompositionFileState.Future)
        {
            return Blocked(
                "Arquivo de composições de versão futura não pode ser migrado.");
        }

        if (compositions.State == CompositionFileState.Corrupt)
        {
            return Blocked(
                "Arquivo de composições inválido não pode ser migrado.");
        }

        var byPath = new Dictionary<string, PageDocument>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var page in existingPages ?? Array.Empty<PersistedPage>())
        {
            var path = DocumentModel.NormalizePath(page.Path);
            if (path.Length == 0 || byPath.ContainsKey(path))
                continue;
            byPath[path] = page.Page;
        }

        var creates = new List<CompositionPageConversion>();
        var migrated = new List<string>();
        var conflicts = new List<CompositionMigrationConflict>();

        foreach (var composition in compositions.Items)
        {
            var conversion = CompositionPageConverter.Convert(
                composition,
                documents);
            var target = DocumentModel.NormalizePath(conversion.PagePath);

            if (!byPath.TryGetValue(target, out var existing))
            {
                creates.Add(conversion);
                byPath[target] = conversion.Page;
                continue;
            }

            var existingLegacyId = LegacyCompositionId(existing);
            if (string.Equals(
                    existingLegacyId,
                    composition.Id,
                    StringComparison.Ordinal))
            {
                migrated.Add(composition.Id);
                continue;
            }

            conflicts.Add(
                new CompositionMigrationConflict(
                    composition.Id,
                    target,
                    "Já existe uma página diferente no caminho de destino."));
        }

        return new CompositionMigrationPlan(
            false,
            null,
            creates.AsReadOnly(),
            migrated.AsReadOnly(),
            conflicts.AsReadOnly());
    }

    private static CompositionMigrationPlan Blocked(string reason) =>
        new(
            true,
            reason,
            Array.Empty<CompositionPageConversion>(),
            Array.Empty<string>(),
            Array.Empty<CompositionMigrationConflict>());

    private static string? LegacyCompositionId(PageDocument page)
    {
        var raw = page.Raw;
        return PageDocument.StringValue(
            raw["meta"]?["legacyComposition"]?["id"]);
    }
}
