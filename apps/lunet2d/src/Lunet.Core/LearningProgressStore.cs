using System.Text.Json;

namespace Lunet.Core;

/// <summary>
/// Registro privado de progresso declarado, fora dos jogos. V2 mantém vínculos e
/// etapas por desafio; lê V1 sem alterar o arquivo até a próxima gravação válida.
/// </summary>
public sealed class LearningProgressStore
{
    private sealed record LegacyDocument(int Version, string? ProjectName, string? ProjectGameId,
        string[] CompletedStepIds);
    private sealed record GameProgress(string Name, string GameId, string[] CompletedStepIds);
    private sealed record ProjectProgress(string Id, string? Name, string? GameId, string[] CompletedStepIds,
        GameProgress[]? PreviousGames = null);
    private sealed record Document(int Version, ProjectProgress[] Projects);

    private readonly string _path;
    private Document _state = new(2, []);

    public LearningProgressStore(string path)
    {
        _path = Path.GetFullPath(path);
        if (!File.Exists(_path)) return;
        try
        {
            var content = File.ReadAllText(_path);
            using var json = JsonDocument.Parse(content);
            if (json.RootElement.ValueKind != JsonValueKind.Object ||
                !json.RootElement.TryGetProperty("Version", out var version) ||
                (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var versionNumber)))
                throw new InvalidDataException("Versão de progresso ausente ou inválida.");

            if (versionNumber == 1)
            {
                var legacy = JsonSerializer.Deserialize<LegacyDocument>(content)
                    ?? throw new InvalidDataException("Progresso vazio.");
                if (legacy.CompletedStepIds is null ||
                    legacy.CompletedStepIds.Length > LearningCatalog.FirstProjectSteps.Count ||
                    legacy.CompletedStepIds.Any(id => !LearningCatalog.FirstProjectSteps.Any(s => s.Id == id)) ||
                    legacy.CompletedStepIds.Distinct(StringComparer.Ordinal).Count() != legacy.CompletedStepIds.Length ||
                    !ValidLink(legacy.ProjectName, legacy.ProjectGameId))
                    throw new InvalidDataException("Progresso V1 inválido; arquivo original preservado.");

                _state = legacy.CompletedStepIds.Length == 0 && legacy.ProjectName is null
                    ? new Document(2, [])
                    : new Document(2, [new ProjectProgress(LearningCatalog.FirstProjectId,
                        legacy.ProjectName, legacy.ProjectGameId, legacy.CompletedStepIds)]);
            }
            else if (versionNumber == 2)
            {
                var current = JsonSerializer.Deserialize<Document>(content)
                    ?? throw new InvalidDataException("Progresso vazio.");
                Validate(current);
                _state = current;
            }
            else throw new InvalidDataException("Versão de progresso não reconhecida; dados preservados.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("Progresso inválido; arquivo original preservado.", e);
        }
    }

    private static bool ValidLink(string? name, string? gameId) =>
        ((name is null) == (gameId is null)) &&
        (name is null || (!string.IsNullOrWhiteSpace(name) && name.Length <= 120 &&
            name.IndexOfAny(['/', '\\', '\0']) < 0)) &&
        (gameId is null || (!string.IsNullOrWhiteSpace(gameId) && gameId.Length <= 256));

    private static void Validate(Document doc)
    {
        if (doc.Version != 2 || doc.Projects is null ||
            doc.Projects.Length > LearningCatalog.Projects.Count ||
            doc.Projects.Any(p => p is null) ||
            doc.Projects.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != doc.Projects.Length)
            throw new InvalidDataException("Progresso V2 inválido; arquivo original preservado.");

        foreach (var project in doc.Projects)
        {
            var reference = LearningCatalog.FindProject(project.Id);
            if (reference is null || !ValidLink(project.Name, project.GameId) ||
                !ValidSteps(reference, project.CompletedStepIds) ||
                project.PreviousGames is { } previous &&
                (previous.Any(game => game is null || game.Name is null || game.GameId is null || game.GameId == project.GameId ||
                    !ValidLink(game.Name, game.GameId) || !ValidSteps(reference, game.CompletedStepIds)) ||
                 previous.Select(game => game.GameId).Distinct(StringComparer.Ordinal).Count() != previous.Length))
                throw new InvalidDataException("Progresso V2 inválido; arquivo original preservado.");
        }
    }

    private static bool ValidSteps(LearningProject project, string[]? ids) =>
        ids is not null && ids.Length <= project.Steps.Count && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length &&
        ids.All(id => project.Steps.Any(step => step.Id == id));

    public static bool HasRecovery(string path)
    {
        try { return File.Exists(path + ".bak") && new LearningProgressStore(path + ".bak")._state.Version == 2; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { return false; }
    }

    public static void RestoreBackup(string path)
    {
        if (!File.Exists(path + ".bak")) throw new FileNotFoundException("Cópia do progresso não encontrada.");
        var backup = new LearningProgressStore(path + ".bak");
        if (File.Exists(path))
            File.Copy(path, path + ".preserved-" + Guid.NewGuid().ToString("N"));
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(backup._state));
    }

    private static LearningProject RequireProject(string projectId) =>
        LearningCatalog.FindProject(projectId) ??
        throw new ArgumentException("Projeto didático desconhecido.", nameof(projectId));

    private ProjectProgress? FindProgress(string projectId)
    {
        RequireProject(projectId);
        return _state.Projects.FirstOrDefault(p => p.Id == projectId);
    }

    // API antiga preservada para não perder a experiência do primeiro desafio.
    public string? ProjectName => ProjectNameFor(LearningCatalog.FirstProjectId);
    public string? ProjectGameId => ProjectGameIdFor(LearningCatalog.FirstProjectId);
    public int CompletedCount => CompletedCountFor(LearningCatalog.FirstProjectId);
    public bool Completed(string stepId) => Completed(LearningCatalog.FirstProjectId, stepId);
    public void LinkProject(string name, string gameId) => LinkProject(LearningCatalog.FirstProjectId, name, gameId);
    public void SetCompleted(string stepId, bool completed) =>
        SetCompleted(LearningCatalog.FirstProjectId, stepId, completed);

    public string? ProjectNameFor(string projectId) => FindProgress(projectId)?.Name;
    public string? ProjectGameIdFor(string projectId) => FindProgress(projectId)?.GameId;
    public int CompletedCountFor(string projectId) => FindProgress(projectId)?.CompletedStepIds.Length ?? 0;
    public int OverallCompletedCount => _state.Projects.Sum(p => p.CompletedStepIds.Length);

    /// <summary>Desafios associados ao GameId do projeto aberto; múltiplos vínculos
    /// podem ser selecionados explicitamente no painel da IDE.</summary>
    public IReadOnlyList<string> LinkedProjectIdsForGameId(string gameId) =>
        _state.Projects.Where(p => p.GameId is not null &&
                p.GameId.Equals(gameId, StringComparison.Ordinal))
            .Select(p => p.Id).OrderBy(p => p, StringComparer.Ordinal).ToArray();

    public bool Completed(string projectId, string stepId)
    {
        var project = RequireProject(projectId);
        if (!project.Steps.Any(s => s.Id == stepId))
            throw new ArgumentException("Etapa não pertence a este projeto.", nameof(stepId));
        return FindProgress(projectId)?.CompletedStepIds.Contains(stepId, StringComparer.Ordinal) == true;
    }

    public void LinkProject(string projectId, string name, string gameId)
    {
        RequireProject(projectId);
        if (!ValidLink(name, gameId) || name is null || gameId is null)
            throw new ArgumentException("Identidade do projeto inválida.");
        UpdateProject(projectId, current =>
        {
            if (current.GameId is null || current.GameId == gameId)
                return current with { Name = name, GameId = gameId };

            var previous = (current.PreviousGames ?? []).ToList();
            var restored = previous.Find(game => game.GameId == gameId);
            if (restored is not null) previous.Remove(restored);
            previous.Add(new(current.Name!, current.GameId, current.CompletedStepIds));
            return current with
            {
                Name = name,
                GameId = gameId,
                CompletedStepIds = restored?.CompletedStepIds ?? [],
                PreviousGames = previous.ToArray()
            };
        });
    }

    public void SetCompleted(string projectId, string stepId, bool completed)
    {
        var project = RequireProject(projectId);
        if (!project.Steps.Any(s => s.Id == stepId))
            throw new ArgumentException("Etapa não pertence a este projeto.", nameof(stepId));

        UpdateProject(projectId, p =>
        {
            var values = p.CompletedStepIds.ToHashSet(StringComparer.Ordinal);
            if (completed) values.Add(stepId); else values.Remove(stepId);
            return p with { CompletedStepIds = values.OrderBy(v => v, StringComparer.Ordinal).ToArray() };
        });
    }

    private void UpdateProject(string projectId, Func<ProjectProgress, ProjectProgress> change)
    {
        var projects = _state.Projects.ToList();
        int index = projects.FindIndex(p => p.Id == projectId);
        var before = index >= 0 ? projects[index] : new ProjectProgress(projectId, null, null, []);
        var after = change(before);
        if (index >= 0) projects[index] = after; else projects.Add(after);
        var next = new Document(2, projects.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray());
        Validate(next);
        Save(next);
    }

    private void Save(Document next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (File.Exists(_path))
        {
            // Retain a verified previous write. An interruption while replacing the current file
            // leaves either the original state or the new state, plus the old valid snapshot.
            var previous = new LearningProgressStore(_path);
            AtomicFile.WriteAllText(_path + ".bak", JsonSerializer.Serialize(previous._state));
        }
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(next));
        _state = next;
    }
}
