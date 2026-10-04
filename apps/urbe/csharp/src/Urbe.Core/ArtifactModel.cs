using System.Text.RegularExpressions;

namespace Urbe.Core;

public enum ArtifactType
{
    System,
    Plugin,
    Theme,
    Style,
    Texture,
    Personalization,
    Page,
    PageTemplate,
    PageBlock,
    Note,
    Text,
    Asset
}

public sealed record ArtifactClassification(
    ArtifactType Type,
    string Extension,
    bool Textual,
    bool Editable,
    bool Note,
    bool System);

public static partial class ArtifactModel
{
    private const string PersonalizationRoot = "Personalização/";

    private static readonly HashSet<string> TextExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".md",
            ".markdown",
            ".txt",
            ".html",
            ".htm",
            ".js",
            ".mjs",
            ".css",
            ".json",
            ".yaml",
            ".yml",
            ".csv"
        };

    private static readonly HashSet<string> NoteTextExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".md",
            ".markdown",
            ".txt"
        };

    private static readonly HashSet<ArtifactType> NotLinkable =
    [
        ArtifactType.System,
        ArtifactType.Plugin,
        ArtifactType.Theme,
        ArtifactType.Style,
        ArtifactType.Texture,
        ArtifactType.Personalization
    ];

    public static ArtifactClassification Classify(string? path)
    {
        var normalized = NormalizeArtifactPath(path);
        var extension = Extension(normalized);
        var system = IsSystem(normalized);
        var textualByExtension = TextExtensions.Contains(extension);
        var type = ArtifactType.Asset;

        if (system)
        {
            type = ArtifactType.System;
        }
        else if (normalized.StartsWith(PersonalizationRoot, StringComparison.Ordinal) &&
                 textualByExtension)
        {
            if (PluginRegex().IsMatch(normalized))
                type = ArtifactType.Plugin;
            else if (ThemeRegex().IsMatch(normalized))
                type = ArtifactType.Theme;
            else if (StyleRegex().IsMatch(normalized))
                type = ArtifactType.Style;
            else if (TextureRegex().IsMatch(normalized))
                type = ArtifactType.Texture;
            else
                type = ArtifactType.Personalization;
        }
        else if (PageRegex().IsMatch(normalized))
        {
            type = ArtifactType.Page;
        }
        else if (PageTemplateRegex().IsMatch(normalized))
        {
            type = ArtifactType.PageTemplate;
        }
        else if (PageBlockRegex().IsMatch(normalized))
        {
            type = ArtifactType.PageBlock;
        }
        else if (NoteTextExtensions.Contains(extension))
        {
            type = ArtifactType.Note;
        }
        else if (textualByExtension)
        {
            type = ArtifactType.Text;
        }

        var textual = textualByExtension && !system;
        return new ArtifactClassification(
            type,
            extension,
            textual,
            textual,
            type == ArtifactType.Note,
            system);
    }

    public static bool IsNote(string? path) => Classify(path).Note;

    public static bool IsText(string? path) => Classify(path).Textual;

    public static bool IsSystem(string? path)
    {
        var normalized = NormalizeArtifactPath(path);
        return normalized
            .Split('/')
            .Any(segment => segment.Length > 0 && segment[0] == '.');
    }

    public static bool Linkable(string? path) =>
        !NotLinkable.Contains(Classify(path).Type);

    public static string Extension(string? path)
    {
        var normalized = NormalizeArtifactPath(path);
        var slash = normalized.LastIndexOf('/');
        var dot = normalized.LastIndexOf('.');
        return dot > slash
            ? normalized[dot..].ToLowerInvariant()
            : string.Empty;
    }

    public static string NormalizeArtifactPath(string? path)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/');
        return normalized.TrimStart('/');
    }

    public static string SafeName(string? value)
    {
        var safe = InvalidNameCharsRegex().Replace(value ?? string.Empty, "-");
        safe = WhitespaceRegex().Replace(safe, " ").Trim();
        safe = LeadingDotsRegex().Replace(safe, string.Empty);
        safe = TrailingDotsRegex().Replace(safe, string.Empty);

        if (safe.Length > 90)
            safe = safe[..90];

        return safe.Length == 0 ? "sem-nome" : safe;
    }

    public static string TitleFromPath(string? path)
    {
        var normalized = DocumentModel.NormalizePath(path);
        var slash = normalized.LastIndexOf('/');
        var file = slash >= 0 ? normalized[(slash + 1)..] : normalized;

        if (file.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
            return file[..^".markdown".Length];
        if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return file[..^".md".Length];

        return file;
    }

    public static string WithoutNoteExtension(string? path)
    {
        var value = path ?? string.Empty;
        if (value.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase))
            return value[..^".markdown".Length];
        if (value.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            return value[..^".md".Length];
        return value;
    }

    public static IReadOnlyList<ArtifactType> Types { get; } =
        Array.AsReadOnly(
            new[]
            {
                ArtifactType.System,
                ArtifactType.Plugin,
                ArtifactType.Theme,
                ArtifactType.Style,
                ArtifactType.Texture,
                ArtifactType.Personalization,
                ArtifactType.Page,
                ArtifactType.PageTemplate,
                ArtifactType.PageBlock,
                ArtifactType.Note,
                ArtifactType.Text,
                ArtifactType.Asset
            });

    [GeneratedRegex(
        @"^Personalização/plugins/.+\.js$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PluginRegex();

    [GeneratedRegex(
        @"^Personalização/(?:tema\.json|temas/.+\.json)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ThemeRegex();

    [GeneratedRegex(
        @"^Personalização/estilos/.+\.css$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex StyleRegex();

    [GeneratedRegex(
        @"^Personalização/texturas/.+\.json$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TextureRegex();

    [GeneratedRegex(
        @"\.page\.json$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PageRegex();

    [GeneratedRegex(
        @"\.template\.json$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PageTemplateRegex();

    [GeneratedRegex(
        @"\.block\.json$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PageBlockRegex();

    [GeneratedRegex(
        @"[\\/:*?""<>|\u0000-\u001f]",
        RegexOptions.CultureInvariant)]
    private static partial Regex InvalidNameCharsRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"^\.+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingDotsRegex();

    [GeneratedRegex(@"\.+$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingDotsRegex();
}

public sealed record ArtifactOpenContext(bool Raw = false);

public sealed record ArtifactRouteResult(bool Handled, object? Result = null);

public sealed class ArtifactRouter
{
    private readonly Dictionary<ArtifactType, Func<UrbeDocument, ArtifactOpenContext, object?>> _openers = new();
    private readonly List<Action<UrbeDocument, ArtifactOpenContext>> _before = [];

    public void RegisterOpener(
        ArtifactType type,
        Func<UrbeDocument, ArtifactOpenContext, object?> opener)
    {
        ArgumentNullException.ThrowIfNull(opener);
        _openers[type] = opener;
    }

    public void OnBeforeOpen(Action<UrbeDocument, ArtifactOpenContext> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        _before.Add(callback);
    }

    public ArtifactRouteResult Route(
        UrbeDocument document,
        ArtifactOpenContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var actualContext = context ?? new ArtifactOpenContext();

        foreach (var callback in _before)
            callback(document, actualContext);

        var type = ArtifactModel.Classify(document.Path).Type;
        if (!actualContext.Raw && _openers.TryGetValue(type, out var opener))
            return new ArtifactRouteResult(true, opener(document, actualContext));

        return new ArtifactRouteResult(false);
    }
}
