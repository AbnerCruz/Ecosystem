using System.Text.RegularExpressions;

namespace Urbe.Core.Study;

/// <summary>
/// #956: a session's study map (the NotebookLM mind map, adapted): the session in the middle, each source around it,
/// a note's headings as its topics, and "cita" where one source links another. Derived from the sources and remade
/// from them; drawn and navigated like any graph, never written into the agents' memory.
/// </summary>
public static partial class StudyMap
{
    public static GraphDocument Build(StudySession session, IReadOnlyList<(string Id, string Path, string? Markdown)> sources)
    {
        var graph = GraphDocument.Create("Mapa — " + session.Title);
        var center = graph.AddText(session.Title + (session.Objectives.Count > 0 ? "\n" + session.Objectives[0] : ""), 0, 0);
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sources.Count; i++)
        {
            var (id, path, markdown) = sources[i];
            double angle = 2 * Math.PI * i / Math.Max(1, sources.Count);
            double x = Math.Round(Math.Cos(angle) * 420), y = Math.Round(Math.Sin(angle) * 320);
            var node = graph.AddReference(markdown is null ? GraphNodeKind.File : GraphNodeKind.Note, path, x, y, referenceId: id.StartsWith("file:", StringComparison.Ordinal) ? null : id);
            graph.Connect(center.Id, node.Id, null, "fonte");
            nodes[System.IO.Path.GetFileNameWithoutExtension(path)] = node.Id;
            if (markdown is null) continue;
            int t = 0;
            foreach (Match h in Heading().Matches(markdown))
            {
                if (t >= 6) break;
                var topic = graph.AddText(h.Groups[1].Value.Trim(), x + (x >= 0 ? 260 : -260), y - 120 + t * 60);
                graph.Connect(node.Id, topic.Id, null, "tópico");
                t++;
            }
        }
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i].Markdown is not { } md) continue;
            var from = nodes[System.IO.Path.GetFileNameWithoutExtension(sources[i].Path)];
            foreach (Match link in Wiki().Matches(md))
                if (nodes.TryGetValue(link.Groups[1].Value.Split('|')[0].Split('#')[0].Trim(), out var to) && to != from)
                    graph.Connect(from, to, "cita", "cita");
        }
        return graph;
    }

    [GeneratedRegex(@"^#{1,2}\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex Heading();
    [GeneratedRegex(@"\[\[([^\]]+)\]\]")]
    private static partial Regex Wiki();
}
