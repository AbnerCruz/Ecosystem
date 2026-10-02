using System.Text.RegularExpressions;

namespace Hub.Core;

/// <summary>Estado de um item do ROADMAP: <c>[x]</c> concluído, <c>[~]</c> aguardando validação/revisão, <c>[ ]</c> aberto.</summary>
public enum ItemState { Open, Verified, Done }

/// <summary>Estado de um gate de fase, como declarado na linha <c>*Estado do gate:*</c> do ROADMAP.</summary>
public enum GateState { NotStarted, Waiting, Approved }

public sealed record RoadmapItem(string Id, string Title, ItemState State, IReadOnlyList<string> DependsOn);

public sealed record PhaseGate(int Phase, string Title, GateState State);

/// <summary>Lê o ROADMAP (a autoridade de escopo e IDs). Somente leitura: o Hub deriva, nunca digita (MANIFEST §18).</summary>
public static partial class RoadmapReader
{
    [GeneratedRegex(@"^- \[(?<m>[ x~])\] (?<id>P\d+-\d+) — (?<rest>.*)$")]
    private static partial Regex ItemLine();

    [GeneratedRegex(@"^\s+- .*?Depende de:\s*(?<deps>.*?)(?=\.\s|\.$|\s+Evidência:|$)")]
    private static partial Regex DependsLine();

    [GeneratedRegex(@"(?<a>P\d+-\d+)\.\.(?<b>P\d+-\d+)|(?<one>P\d+-\d+)")]
    private static partial Regex DepToken();

    [GeneratedRegex(@"^## Fase (?<n>\d+) — (?<t>.+)$")]
    private static partial Regex PhaseHeader();

    [GeneratedRegex(@"^\*Estado do gate:\*\s+\*\*(?<s>aprovado|aguardando|não iniciado)\*\*")]
    private static partial Regex GateLine();

    [GeneratedRegex(@"\*\*(?<b>[^*]+)\*\*")]
    private static partial Regex Bold();

    public static IReadOnlyList<RoadmapItem> ReadItems(string roadmap)
    {
        var lines = roadmap.Split('\n');
        var items = new List<RoadmapItem>();
        for (var i = 0; i < lines.Length; i++)
        {
            var m = ItemLine().Match(lines[i].TrimEnd('\r'));
            if (!m.Success) continue;

            // "Depende de:" aparece nas linhas de detalhe que seguem o item, antes do próximo item.
            var deps = new List<string>();
            for (var j = i + 1; j < lines.Length && !ItemLine().IsMatch(lines[j]) && !lines[j].StartsWith("## ") && !lines[j].StartsWith("**Gate:**"); j++)
            {
                var d = DependsLine().Match(lines[j].TrimEnd('\r'));
                if (d.Success) deps.AddRange(Expand(d.Groups["deps"].Value));
            }

            items.Add(new RoadmapItem(
                m.Groups["id"].Value,
                Title(m.Groups["rest"].Value),
                m.Groups["m"].Value switch { "x" => ItemState.Done, "~" => ItemState.Verified, _ => ItemState.Open },
                deps.Distinct().ToList()));
        }
        return items;
    }

    public static IReadOnlyList<PhaseGate> ReadGates(string roadmap)
    {
        var gates = new List<PhaseGate>();
        PhaseGate? current = null;
        foreach (var raw in roadmap.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (PhaseHeader().Match(line) is { Success: true } h)
            {
                if (current is not null) gates.Add(current);
                current = new PhaseGate(int.Parse(h.Groups["n"].Value), h.Groups["t"].Value.Trim(), GateState.NotStarted);
            }
            else if (current is not null && GateLine().Match(line) is { Success: true } g)
                current = current with { State = g.Groups["s"].Value switch { "aprovado" => GateState.Approved, "aguardando" => GateState.Waiting, _ => GateState.NotStarted } };
        }
        if (current is not null) gates.Add(current);
        return gates;
    }

    static string Title(string rest)
    {
        if (Bold().Match(rest) is { Success: true } b) return b.Groups["b"].Value.Trim();
        var t = rest.Trim();
        return t.Length <= 120 ? t : t[..120] + "…";
    }

    /// <summary>Expande "P3-4..P3-6, P3-1" em IDs. Só reconhece IDs de tarefa; DEC-xxxx e texto livre são ignorados.</summary>
    static IEnumerable<string> Expand(string text)
    {
        foreach (Match t in DepToken().Matches(text))
        {
            if (!t.Groups["one"].Success)
            {
                var (pa, na) = Split(t.Groups["a"].Value);
                var (pb, nb) = Split(t.Groups["b"].Value);
                if (pa == pb && na <= nb)
                    for (var n = na; n <= nb; n++) yield return $"P{pa}-{n}";
                else { yield return t.Groups["a"].Value; yield return t.Groups["b"].Value; }
            }
            else yield return t.Groups["one"].Value;
        }
    }

    static (int Phase, int N) Split(string id)
    {
        var p = id[1..].Split('-');
        return (int.Parse(p[0]), int.Parse(p[1]));
    }
}
