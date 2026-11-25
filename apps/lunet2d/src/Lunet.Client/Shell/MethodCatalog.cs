using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lunet.Client.Shell;

/// <summary>One callable or readable member of the catalog (all overloads of a name are one entry).</summary>
public sealed record CatalogMember(
    string Namespace, string Type, string Name, string Kind, bool IsStatic, IReadOnlyList<string> Signatures, string Summary)
{
    /// <summary>Stable identity for the usage history (namespace, type, name).</summary>
    public string Key => $"{Namespace}|{Type}|{Name}";
    public bool IsCallable => Kind is "method" or "constructor";
    public bool HasParameters => Signatures.Any(s => !s.Contains("()", StringComparison.Ordinal));
}

/// <summary>A class (or struct, enum…) of the catalog and its members, alphabetical.</summary>
public sealed record CatalogType(string Namespace, string Name, IReadOnlyList<CatalogMember> Members);

/// <summary>A namespace of the catalog and its types, alphabetical.</summary>
public sealed record CatalogNamespace(string Name, IReadOnlyList<CatalogType> Types);

/// <summary>
/// The "Métodos" tab: the Lunet API generated from the Framework's XML docs (<c>docs/api/lunet-framework.json</c>,
/// kept current by DocsTests — never a hand list) plus the members the game itself declares (Roslyn syntax), with
/// "Recentes" and "Mais usados" on top (study 25/11 §4). No UI dependency.
/// </summary>
public sealed class MethodCatalog
{
    /// <summary>Namespace that holds the members declared by the open game.</summary>
    public const string ProjectNamespace = "Este jogo";
    public const int TopCount = 8;

    private static readonly HashSet<string> Kinds = ["method", "property", "field", "constructor", "enum value"];
    private static readonly Lazy<IReadOnlyList<CatalogMember>> FrameworkMembers = new(LoadFramework);

    private readonly Dictionary<string, (int Count, DateTime Last)> _usage = new(StringComparer.Ordinal);
    private readonly string? _usagePath;
    private IReadOnlyList<CatalogMember> _project = [];

    public MethodCatalog(string? usagePath = null)
    {
        _usagePath = usagePath;
        LoadUsage();
    }

    /// <summary>All members: framework first, then the game's.</summary>
    public IEnumerable<CatalogMember> All => FrameworkMembers.Value.Concat(_project);

    /// <summary>Reads the members the game declares (its .cs files); called when the tab opens, so it is never stale.</summary>
    public void SetProjectSources(IEnumerable<(string Path, string Text)> sources)
    {
        var members = new Dictionary<string, CatalogMember>(StringComparer.Ordinal);
        foreach (var (_, text) in sources)
        {
            var root = CSharpSyntaxTree.ParseText(text).GetRoot();
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            foreach (var member in type.Members)
            {
                var (name, kind, signature, isStatic) = member switch
                {
                    MethodDeclarationSyntax m => (m.Identifier.Text, "method", $"{m.ReturnType} {m.Identifier}{m.ParameterList}", m.Modifiers.Any(SyntaxKind.StaticKeyword)),
                    PropertyDeclarationSyntax p => (p.Identifier.Text, "property", $"{p.Type} {p.Identifier}", p.Modifiers.Any(SyntaxKind.StaticKeyword)),
                    _ => (null, "", "", false),
                };
                if (name is null || member.Modifiers.Any(SyntaxKind.OverrideKeyword)) continue;
                var key = type.Identifier.Text + "|" + name;
                members[key] = members.TryGetValue(key, out var existing)
                    ? existing with { Signatures = [.. existing.Signatures, signature] }
                    : new CatalogMember(ProjectNamespace, type.Identifier.Text, name, kind, isStatic, [signature], "");
            }
        }
        _project = members.Values.ToArray();
    }

    /// <summary>The catalog: namespaces, types and members, each level alphabetical (ordinal-ignore-case).</summary>
    public IReadOnlyList<CatalogNamespace> Tree() => All
        .GroupBy(m => m.Namespace)
        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
        .Select(ns => new CatalogNamespace(ns.Key, ns
            .GroupBy(m => m.Type)
            .OrderBy(t => t.Key, StringComparer.OrdinalIgnoreCase)
            .Select(t => new CatalogType(ns.Key, t.Key, t.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToArray()))
        .ToArray();

    /// <summary>Most recent first (ties alphabetical).</summary>
    public IReadOnlyList<CatalogMember> Recent() => Used()
        .OrderByDescending(x => x.Usage.Last).ThenBy(x => x.Member.Name, StringComparer.OrdinalIgnoreCase)
        .Take(TopCount).Select(x => x.Member).ToArray();

    /// <summary>Most used first (ties alphabetical).</summary>
    public IReadOnlyList<CatalogMember> MostUsed() => Used()
        .OrderByDescending(x => x.Usage.Count).ThenBy(x => x.Member.Name, StringComparer.OrdinalIgnoreCase)
        .Take(TopCount).Select(x => x.Member).ToArray();

    private IEnumerable<(CatalogMember Member, (int Count, DateTime Last) Usage)> Used() =>
        All.Where(m => _usage.ContainsKey(m.Key)).Select(m => (m, _usage[m.Key]));

    /// <summary>Records that a member was inserted (and saves the history).</summary>
    public void RecordUse(CatalogMember member, DateTime? when = null)
    {
        var now = when ?? DateTime.UtcNow;
        _usage[member.Key] = _usage.TryGetValue(member.Key, out var u) ? (u.Count + 1, now) : (1, now);
        SaveUsage();
    }

    /// <summary>
    /// The edit that inserts a member at the caret: after a dot only the member; a constructor is <c>new T()</c>;
    /// a static member is qualified by its type (unless declared in the game); methods get <c>()</c> with the caret
    /// inside when they take parameters. No placeholders to delete (study 25/11 §4).
    /// </summary>
    public static CodeEdit Insertion(CatalogMember member, string text, int selectionStart, int selectionEnd)
    {
        int start = Math.Clamp(Math.Min(selectionStart, selectionEnd), 0, text.Length);
        int end = Math.Clamp(Math.Max(selectionStart, selectionEnd), 0, text.Length);
        bool afterDot = start > 0 && text[start - 1] == '.';
        string head = member.Kind == "constructor" ? "new " + member.Type
            : !afterDot && (member.IsStatic || member.Kind == "enum value") && member.Namespace != ProjectNamespace ? member.Type + "." + member.Name
            : member.Name;
        if (!member.IsCallable) return new(start, end, head, start + head.Length);
        var insert = head + "()";
        return new(start, end, insert, start + insert.Length - (member.HasParameters ? 1 : 0));
    }

    private static IReadOnlyList<CatalogMember> LoadFramework()
    {
        using var stream = typeof(MethodCatalog).Assembly.GetManifestResourceStream("lunet/api.json");
        if (stream is null) return [];
        using var doc = JsonDocument.Parse(stream);
        var result = new List<CatalogMember>();
        foreach (var type in doc.RootElement.GetProperty("types").EnumerateArray())
        {
            var ns = type.GetProperty("namespace").GetString() ?? "";
            var typeName = type.GetProperty("name").GetString() ?? "";
            foreach (var group in type.GetProperty("members").EnumerateArray()
                .Where(m => Kinds.Contains(m.GetProperty("kind").GetString() ?? ""))
                .GroupBy(m => m.GetProperty("name").GetString() ?? ""))
            {
                var first = group.First();
                var signatures = group.Select(m => m.GetProperty("signature").GetString() ?? "").ToArray();
                result.Add(new CatalogMember(ns, typeName, group.Key, first.GetProperty("kind").GetString()!,
                    signatures.Any(s => s.Contains(" static ", StringComparison.Ordinal)) || first.GetProperty("kind").GetString() == "enum value",
                    signatures, first.GetProperty("summary").GetString() ?? ""));
            }
        }
        return result;
    }

    private void LoadUsage()
    {
        if (_usagePath is null || !File.Exists(_usagePath)) return;
        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, UsageEntry>>(File.ReadAllText(_usagePath));
            foreach (var (k, v) in stored ?? []) _usage[k] = (v.Count, v.Last);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { /* history is a convenience: start empty */ }
    }

    private void SaveUsage()
    {
        if (_usagePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_usagePath)!);
            var tmp = _usagePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_usage.ToDictionary(p => p.Key, p => new UsageEntry(p.Value.Count, p.Value.Last))));
            File.Move(tmp, _usagePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* not saved: the next use tries again */ }
    }

    private sealed record UsageEntry(int Count, DateTime Last);
}
