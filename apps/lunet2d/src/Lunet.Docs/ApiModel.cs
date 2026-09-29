using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lunet.Docs;

public sealed record ParameterDoc(string Name, string Type, string Description);

public sealed record MemberDoc(
    string Id,
    string Name,
    string Kind,
    string Signature,
    string Summary,
    string Remarks,
    IReadOnlyList<ParameterDoc> Parameters,
    string Returns,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> Related,
    string Since);

public sealed record TypeDoc(
    string Id,
    string Name,
    string Namespace,
    string Kind,
    string Signature,
    string Summary,
    string Remarks,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> Related,
    string Since,
    IReadOnlyList<MemberDoc> Members);

/// <summary>Documentação de uma API: tipos e membros públicos com resumo, parâmetros, exemplos e relacionados.</summary>
public sealed class ApiDocumentation
{
    private Dictionary<string, (TypeDoc Type, MemberDoc? Member)>? _index;

    public ApiDocumentation(string assembly, string version, IReadOnlyList<TypeDoc> types)
    {
        Assembly = assembly;
        Version = version;
        Types = types;
    }

    public string Assembly { get; }
    public string Version { get; }
    public IReadOnlyList<TypeDoc> Types { get; }

    private Dictionary<string, (TypeDoc Type, MemberDoc? Member)> Index => _index ??= BuildIndex();

    private Dictionary<string, (TypeDoc, MemberDoc?)> BuildIndex()
    {
        var index = new Dictionary<string, (TypeDoc, MemberDoc?)>(StringComparer.Ordinal);
        foreach (var type in Types)
        {
            index[type.Id] = (type, null);
            foreach (var member in type.Members) index[member.Id] = (type, member);
        }
        return index;
    }

    /// <summary>Procura pelo identificador de documentação (o mesmo do Roslyn: <c>T:...</c>, <c>M:...</c>, <c>P:...</c>).</summary>
    public bool TryFind(string documentationId, out TypeDoc type, out MemberDoc? member)
    {
        if (Index.TryGetValue(documentationId, out var found)) { (type, member) = found; return true; }
        type = null!;
        member = null;
        return false;
    }

    public TypeDoc? FindType(string name) => Types.FirstOrDefault(t =>
        t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || $"{t.Namespace}.{t.Name}".Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Busca por nome (tipos e membros) e por texto do resumo, com os nomes na frente.</summary>
    public IReadOnlyList<SearchResult> Search(string query, int max = 40)
    {
        query = (query ?? "").Trim();
        if (query.Length == 0) return [];
        var results = new List<(int Score, SearchResult Result)>();
        foreach (var type in Types)
        {
            var typeScore = Score(type.Name, type.Summary, query);
            if (typeScore > 0) results.Add((typeScore + 5, new SearchResult(type.Id, type.Name, type.Kind, type.Namespace, type.Summary)));
            foreach (var member in type.Members)
            {
                var memberScore = Math.Max(Score(member.Name, member.Summary, query), Score($"{type.Name}.{member.Name}", "", query));
                if (memberScore > 0)
                    results.Add((memberScore, new SearchResult(member.Id, $"{type.Name}.{member.Name}", member.Kind, type.Namespace, member.Summary)));
            }
        }
        return results.OrderByDescending(r => r.Score).ThenBy(r => r.Result.Title, StringComparer.OrdinalIgnoreCase)
            .Select(r => r.Result).DistinctBy(r => r.Id).Take(max).ToList();
    }

    private static int Score(string name, string summary, string query)
    {
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase)) return 100;
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 60;
        if (name.Contains(query, StringComparison.OrdinalIgnoreCase)) return 40;
        return summary.Contains(query, StringComparison.OrdinalIgnoreCase) ? 10 : 0;
    }

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public string ToJson() => JsonSerializer.Serialize(new Dto(Assembly, Version, Types), JsonOptions);

    public static ApiDocumentation FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions) ?? throw new InvalidDataException("Documentação vazia.");
        return new ApiDocumentation(dto.Assembly, dto.Version, dto.Types);
    }

    private sealed record Dto(string Assembly, string Version, IReadOnlyList<TypeDoc> Types);
}

public sealed record SearchResult(string Id, string Title, string Kind, string Namespace, string Summary);
