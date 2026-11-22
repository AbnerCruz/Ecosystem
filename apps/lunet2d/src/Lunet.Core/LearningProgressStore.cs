using System.Text.Json;

namespace Lunet.Core;

/// <summary>Progresso privado do aprendiz, separado dos jogos e do catálogo instalado.
/// Nunca importa arquivos do projeto para determinar aprendizado.</summary>
public sealed class LearningProgressStore
{
    private sealed record Document(int Version, string? ProjectName, string? ProjectGameId, string[] CompletedStepIds);
    private readonly string _path;
    private Document _state = new(1, null, null, []);

    public LearningProgressStore(string path)
    {
        _path = Path.GetFullPath(path);
        if (!File.Exists(_path)) return;
        try
        {
            if (new FileInfo(_path).Length > 128_000) throw new InvalidDataException("Progresso excede o tamanho esperado.");
            var state = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path)) ??
                throw new InvalidDataException("Progresso vazio.");
            if (state.Version != 1 || state.CompletedStepIds is null ||
                state.CompletedStepIds.Length > LearningCatalog.FirstProjectSteps.Count ||
                state.CompletedStepIds.Any(s => !LearningCatalog.FirstProjectSteps.Any(x => x.Id == s)) ||
                state.CompletedStepIds.Distinct(StringComparer.Ordinal).Count() != state.CompletedStepIds.Length ||
                (state.ProjectName is not null && (string.IsNullOrWhiteSpace(state.ProjectName) || state.ProjectName.Length > 120 ||
                  state.ProjectName.IndexOfAny(['/', '\\', '\0']) >= 0)) ||
                (state.ProjectGameId is not null && (string.IsNullOrWhiteSpace(state.ProjectGameId) || state.ProjectGameId.Length > 256)) ||
                ((state.ProjectName is null) != (state.ProjectGameId is null)))
                throw new InvalidDataException("Progresso inválido; arquivo original preservado.");
            _state = state;
        }
        catch (JsonException e) { throw new InvalidDataException("Progresso inválido; arquivo original preservado.", e); }
    }

    public string? ProjectName => _state.ProjectName;
    public string? ProjectGameId => _state.ProjectGameId;
    public int CompletedCount => _state.CompletedStepIds.Length;
    public bool Completed(string id) => _state.CompletedStepIds.Contains(id, StringComparer.Ordinal);

    public void LinkProject(string name, string gameId)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 ||
            name.IndexOfAny(['/', '\\', '\0']) >= 0 || string.IsNullOrWhiteSpace(gameId) || gameId.Length > 256)
            throw new ArgumentException("Identidade do projeto inválida.");
        Save(_state with { ProjectName = name, ProjectGameId = gameId });
    }

    public void SetCompleted(string stepId, bool completed)
    {
        if (!LearningCatalog.FirstProjectSteps.Any(s => s.Id == stepId))
            throw new ArgumentException("Etapa desconhecida.", nameof(stepId));
        var values = _state.CompletedStepIds.ToHashSet(StringComparer.Ordinal);
        if (completed) values.Add(stepId); else values.Remove(stepId);
        Save(_state with { CompletedStepIds = values.OrderBy(x => x, StringComparer.Ordinal).ToArray() });
    }

    private void Save(Document next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(next));
            File.Move(temp, _path, overwrite: true);
            _state = next;
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
        }
    }
}
