using System.Text.Json;

namespace Lunet.Core;

public sealed record MindTopic(Guid Id, Guid? Parent, string Text, bool Folded = false, string? Reference = null);
public sealed record MindConnection(Guid From, Guid To);
public sealed record MindPlacement(MindTopic Topic, int Depth, int Row);

public sealed record ProjectMindMap(int Version, MindTopic[] Topics, MindConnection[] Connections)
{
    public const string Extension = ".mindmap.json";
    public Guid Root => Topics[0].Id;

    public static ProjectMindMap Create(string title) =>
        new(1, [new(Guid.NewGuid(), null, Clean(title))], []);

    public static ProjectMindMap Parse(string json)
    {
        var map = JsonSerializer.Deserialize<ProjectMindMap>(json) ?? throw new InvalidDataException("Mapa vazio.");
        map.Validate();
        return map;
    }

    public string Serialize()
    {
        Validate();
        return JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
    }

    public ProjectMindMap Add(Guid parent, string text)
    {
        Require(parent);
        return this with { Topics = [.. Topics, new(Guid.NewGuid(), parent, Clean(text))] };
    }

    public ProjectMindMap Edit(Guid id, string text, string? reference)
    {
        ValidateReference(reference);
        return Replace(Require(id) with { Text = Clean(text), Reference = reference });
    }

    public ProjectMindMap Fold(Guid id)
    {
        var topic = Require(id);
        return Replace(topic with { Folded = !topic.Folded });
    }

    public ProjectMindMap Move(Guid id, Guid parent)
    {
        var topic = Require(id);
        Guid? ancestor = parent;
        while (ancestor is { } current)
        {
            if (current == id || id == Root) throw new InvalidOperationException("O destino criaria um ciclo ou moveria o tópico central.");
            ancestor = Require(current).Parent;
        }
        if (topic.Parent == parent) return this;
        return Replace(topic with { Parent = parent });
    }

    public ProjectMindMap Remove(Guid id)
    {
        Require(id);
        if (id == Root) throw new InvalidOperationException("O tópico central não pode ser removido.");
        var subtree = Walk(id, fold: false).Select(row => row.Topic.Id).ToHashSet();
        return this with
        {
            Topics = Topics.Where(topic => !subtree.Contains(topic.Id)).ToArray(),
            Connections = Connections.Where(link => !subtree.Contains(link.From) && !subtree.Contains(link.To)).ToArray()
        };
    }

    public ProjectMindMap ToggleConnection(Guid from, Guid to)
    {
        Require(from);
        Require(to);
        if (from == to) throw new InvalidOperationException("Escolha dois tópicos diferentes.");
        var link = Connections.FirstOrDefault(c => c.From == from && c.To == to || c.From == to && c.To == from);
        return this with { Connections = link is null ? [.. Connections, new(from, to)] : Connections.Where(c => c != link).ToArray() };
    }

    public IReadOnlyList<MindPlacement> Walk(Guid start, bool fold = true)
    {
        var children = Topics.Skip(1).ToLookup(topic => topic.Parent!.Value);
        var pending = new Stack<(MindTopic Topic, int Depth)>();
        pending.Push((Require(start), 0));
        var items = new List<MindPlacement>();
        while (pending.TryPop(out var item))
        {
            items.Add(new(item.Topic, item.Depth, items.Count));
            if (fold && item.Topic.Folded) continue;
            foreach (var child in children[item.Topic.Id].Reverse()) pending.Push((child, item.Depth + 1));
        }
        return items;
    }

    private ProjectMindMap Replace(MindTopic value) =>
        this with { Topics = Topics.Select(topic => topic.Id == value.Id ? value : topic).ToArray() };

    private MindTopic Require(Guid id) => Topics.FirstOrDefault(topic => topic.Id == id)
        ?? throw new ArgumentException("Tópico não encontrado.");

    private static string Clean(string text) => string.IsNullOrWhiteSpace(text) ? "Novo tópico" : text.Trim();

    private static void ValidateReference(string? path)
    {
        if (path is not null && (!path.StartsWith(ProjectNotes.Folder + "/", StringComparison.Ordinal) ||
            path.Contains('\\') || path.Contains(':') || path.Any(char.IsControl) ||
            path.Split('/').Any(part => part is "" or "." or "..")))
            throw new InvalidDataException("Referência inválida.");
    }

    private void Validate()
    {
        if (Version != 1 || Topics is not { Length: > 0 } || Connections is null ||
            Topics.Any(topic => topic is null || topic.Id == Guid.Empty || string.IsNullOrWhiteSpace(topic.Text)) ||
            Topics[0].Parent is not null)
            throw new InvalidDataException("Formato de mapa mental inválido.");
        var ids = Topics.Select(topic => topic.Id).ToHashSet();
        if (ids.Count != Topics.Length || Topics.Skip(1).Any(topic => topic.Parent is not { } parent || !ids.Contains(parent)) ||
            Connections.Any(link => link is null || link.From == link.To || !ids.Contains(link.From) || !ids.Contains(link.To)))
            throw new InvalidDataException("Tópicos ou ligações inválidos.");
        if (Walk(Root, fold: false).Count != Topics.Length)
            throw new InvalidDataException("O mapa contém um ramo sem ligação com o tópico central.");
        foreach (var topic in Topics) ValidateReference(topic.Reference);
    }
}

public sealed class MindMapHistory(ProjectMindMap initial)
{
    private readonly Stack<ProjectMindMap> _undo = new();
    private readonly Stack<ProjectMindMap> _redo = new();
    public ProjectMindMap Current { get; private set; } = initial;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Apply(ProjectMindMap next, Action<ProjectMindMap> persist)
    {
        if (ReferenceEquals(Current, next)) return;
        persist(next);
        _undo.Push(Current);
        Current = next;
        _redo.Clear();
    }

    public void Undo(Action<ProjectMindMap> persist) => Change(_undo, _redo, persist);
    public void Redo(Action<ProjectMindMap> persist) => Change(_redo, _undo, persist);

    private void Change(Stack<ProjectMindMap> from, Stack<ProjectMindMap> to, Action<ProjectMindMap> persist)
    {
        if (!from.TryPeek(out var previous)) return;
        persist(previous);
        from.Pop();
        to.Push(Current);
        Current = previous;
    }
}
