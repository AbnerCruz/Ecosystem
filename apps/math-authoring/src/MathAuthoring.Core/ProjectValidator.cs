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
        CheckText(project.ProjectId, "project.id.required", "project", problems);
        if (project.Metadata is null)
            problems.Add(new("project.metadata.required", "Metadata é obrigatória."));
        else
        {
            CheckText(project.Metadata.Title, "project.title.required", "metadata.title", problems);
            if (project.Metadata.Description?.Length > 4096)
                problems.Add(new("project.description.limit", "Descrição excede 4096 caracteres."));
        }

        if (project.Variables is null || project.Scenes is null || project.ExportProfiles is null)
        {
            problems.Add(new("project.collections.required", "Coleções de projeto não podem ser nulas."));
            return problems.AsReadOnly();
        }
        CheckLimit(project.Variables.Count, ProjectJson.MaxVariables, "variable.limit", problems);
        CheckLimit(project.Scenes.Count, ProjectJson.MaxScenes, "scene.limit", problems);
        CheckLimit(project.ExportProfiles.Count, ProjectJson.MaxProfiles, "export-profile.limit", problems);

        AddDuplicateProblems(project.Variables.Select(x => x.Id), "variable.id.duplicate", problems);
        AddDuplicateProblems(project.Scenes.Select(x => x.Id), "scene.id.duplicate", problems);
        AddDuplicateProblems(project.ExportProfiles.Select(x => x.Id), "export-profile.id.duplicate", problems);

        var variableIds = project.Variables.Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);
        int expressionCount = 0;
        foreach (var variable in project.Variables)
        {
            CheckText(variable.Id, "variable.id.required", variable.Id, problems);
            CheckText(variable.Name, "variable.name.required", variable.Id, problems);
            if (!double.IsFinite(variable.InitialValue))
                problems.Add(new("variable.value.non-finite", "Valor inicial deve ser finito.", variable.Id));
            if (variable.Definition is not null)
                CheckExpression(variable.Definition, variableIds, variable.Id, 0, ref expressionCount, problems);
        }

        foreach (var scene in project.Scenes)
        {
            CheckText(scene.Id, "scene.id.required", scene.Id, problems);
            CheckText(scene.Name, "scene.name.required", scene.Id, problems);
            if (scene.ObjectIds is null)
            {
                problems.Add(new("scene.objects.required", "ObjectIds não pode ser nulo.", scene.Id));
                continue;
            }
            CheckLimit(scene.ObjectIds.Count, ProjectJson.MaxObjectIdsPerScene, "scene.objects.limit", problems, scene.Id);
            foreach (var id in scene.ObjectIds)
                CheckText(id, "scene.object.id.required", scene.Id, problems);
            AddDuplicateProblems(scene.ObjectIds, "scene.object.id.duplicate", problems);
            // Scene graph registration and object reference validation enter in MA-005.
        }

        foreach (var profile in project.ExportProfiles)
        {
            CheckText(profile.Id, "export-profile.id.required", profile.Id, problems);
            CheckText(profile.Name, "export-profile.name.required", profile.Id, problems);
            if (profile.Width <= 0 || profile.Height <= 0 || profile.FramesPerSecond <= 0)
                problems.Add(new("export-profile.invalid-dimensions",
                    "Perfil de export precisa de dimensões e fps positivos.", profile.Id));
        }

        return problems.AsReadOnly();
    }

    private static void CheckExpression(MathExpression expression, HashSet<string> variables,
        string subject, int depth, ref int count, ICollection<ValidationProblem> problems)
    {
        if (++count > ProjectJson.MaxExpressions || depth >= ProjectJson.MaxExpressionDepth)
        {
            problems.Add(new("expression.limit", "Profundidade ou quantidade de expressões excedida.", subject));
            return;
        }
        switch (expression)
        {
            case ConstantExpression constant:
                if (!double.IsFinite(constant.Value))
                    problems.Add(new("expression.constant.non-finite", "Constante deve ser finita.", subject));
                break;
            case VariableReferenceExpression reference:
                if (!variables.Contains(reference.VariableId))
                    problems.Add(new("expression.variable.missing", "Referência a variável inexistente.", subject));
                break;
            case BinaryExpression binary:
                if (!Enum.IsDefined(binary.Operator))
                    problems.Add(new("expression.operator.unknown", "Operador desconhecido.", subject));
                if (binary.Left is null || binary.Right is null)
                {
                    problems.Add(new("expression.child.required", "Operador binário precisa de dois operandos.", subject));
                    break;
                }
                CheckExpression(binary.Left, variables, subject, depth + 1, ref count, problems);
                CheckExpression(binary.Right, variables, subject, depth + 1, ref count, problems);
                break;
            default:
                problems.Add(new("expression.kind.unknown", "Tipo de expressão desconhecido.", subject));
                break;
        }
    }

    private static void CheckLimit(int count, int limit, string code,
        ICollection<ValidationProblem> problems, string? subject = null)
    {
        if (count > limit)
            problems.Add(new(code, $"Limite de {limit} itens excedido.", subject));
    }

    private static void CheckText(string? value, string code, string? subject,
        ICollection<ValidationProblem> problems)
    {
        if (string.IsNullOrWhiteSpace(value))
            problems.Add(new(code, "Texto obrigatório ausente.", subject));
        else if (value.Length > 4096)
            problems.Add(new("text.limit", "Texto excede 4096 caracteres.", subject));
    }

    private static void AddDuplicateProblems(
        IEnumerable<string> ids, string code, ICollection<ValidationProblem> problems)
    {
        foreach (var duplicate in ids.Where(x => !string.IsNullOrWhiteSpace(x))
                     .GroupBy(x => x, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
            problems.Add(new(code, $"ID duplicado: {duplicate}.", duplicate));
    }
}
