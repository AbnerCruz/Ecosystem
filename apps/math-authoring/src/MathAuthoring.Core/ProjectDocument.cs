using System.Collections.ObjectModel;

namespace MathAuthoring.Core;

public sealed record ProjectMetadata(string Title, string? Description = null);

public sealed record VariableDefinition(
    string Id,
    string Name,
    double InitialValue,
    MathExpression? Definition = null);

public sealed record SceneDocument(
    string Id,
    string Name,
    IReadOnlyList<string> ObjectIds);

public sealed record ExportProfile(
    string Id,
    string Name,
    int Width,
    int Height,
    int FramesPerSecond);

public sealed record ProjectDocument(
    int SchemaVersion,
    string ProjectId,
    ProjectMetadata Metadata,
    IReadOnlyList<VariableDefinition> Variables,
    IReadOnlyList<SceneDocument> Scenes,
    IReadOnlyList<ExportProfile> ExportProfiles)
{
    public static ProjectDocument Create(string projectId, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new(
            ProductIdentity.CurrentSchemaVersion,
            projectId,
            new ProjectMetadata(title),
            Empty<VariableDefinition>(),
            ReadOnly(new[] { new SceneDocument("scene-1", "Cena 1", Empty<string>()) }),
            ReadOnly(new[] { new ExportProfile("preview-720p30", "720p / 30 fps", 1280, 720, 30) }));
    }

    private static ReadOnlyCollection<T> Empty<T>() => Array.AsReadOnly(Array.Empty<T>());
    private static ReadOnlyCollection<T> ReadOnly<T>(T[] values) => Array.AsReadOnly(values);
}
