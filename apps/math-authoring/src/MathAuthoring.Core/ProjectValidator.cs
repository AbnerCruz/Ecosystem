namespace MathAuthoring.Core;

public sealed record ValidationProblem(string Code, string Message, string? SubjectId = null);

public static class ProjectValidator
{
    public static IReadOnlyList<ValidationProblem> Validate(ProjectDocument project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var problems = new List<ValidationProblem>();

        if (project.SchemaVersion != ProductIdentity.CurrentSchemaVersion)
            problems.Add(new("schema.unsupported", $"Schema {project.SchemaVersion} não é suportado."));

        if (string.IsNullOrWhiteSpace(project.ProjectId))
            problems.Add(new("project.id.required", "ProjectId é obrigatório."));

        AddDuplicateProblems(project.Variables.Select(x => x.Id), "variable.id.duplicate", problems);
        AddDuplicateProblems(project.Scenes.Select(x => x.Id), "scene.id.duplicate", problems);
        AddDuplicateProblems(project.ExportProfiles.Select(x => x.Id), "export-profile.id.duplicate", problems);

        foreach (var variable in project.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Id))
                problems.Add(new("variable.id.required", "Variável precisa de ID."));
            if (string.IsNullOrWhiteSpace(variable.Name))
                problems.Add(new("variable.name.required", "Variável precisa de nome.", variable.Id));
            if (!double.IsFinite(variable.InitialValue))
                problems.Add(new("variable.value.non-finite", "Valor inicial deve ser finito.", variable.Id));
        }

        foreach (var scene in project.Scenes)
        {
            if (string.IsNullOrWhiteSpace(scene.Id))
                problems.Add(new("scene.id.required", "Cena precisa de ID."));
            if (string.IsNullOrWhiteSpace(scene.Name))
                problems.Add(new("scene.name.required", "Cena precisa de nome.", scene.Id));
        }

        foreach (var profile in project.ExportProfiles)
        {
            if (profile.Width <= 0 || profile.Height <= 0 || profile.FramesPerSecond <= 0)
                problems.Add(new("export-profile.invalid-dimensions", "Perfil de export precisa de dimensões e fps positivos.", profile.Id));
        }

        return problems.AsReadOnly();
    }

    private static void AddDuplicateProblems(
        IEnumerable<string> ids,
        string code,
        ICollection<ValidationProblem> problems)
    {
        foreach (var duplicate in ids.Where(x => !string.IsNullOrWhiteSpace(x))
                     .GroupBy(x => x, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
        {
            problems.Add(new(code, $"ID duplicado: {duplicate}.", duplicate));
        }
    }
}
