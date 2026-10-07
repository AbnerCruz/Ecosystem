namespace Urbe.UI;

public sealed record ShellDestination(
    string Id,
    string Label,
    string Href,
    bool Exact,
    string OwnerUseCase);

/// <summary>
/// Canonical top-level destinations for the shared C# shell.
/// Later UCs replace placeholder surfaces without changing host routing.
/// </summary>
public static class ShellNavigation
{
    public static IReadOnlyList<ShellDestination> Primary { get; } =
        Array.AsReadOnly<ShellDestination>([
            new("home", "Início", "", true, "UC-17"),
            new("explorer", "Explorer", "explorer", false, "UC-18"),
            new("editor", "Editor", "editor", false, "UC-18"),
            new("world", "Cidade", "cidade", false, "UC-19"),
            new("more", "Mais", "mais", false, "UC-17")
        ]);
}
