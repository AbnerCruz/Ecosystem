using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// UC-85 (DEC-0053, ADD-0030): how the boxes of the Área de trabalho look and are ordered, in .urbe/area.json. It only
/// keeps presentation — colour, border, icon, the order of the projects and how each box shows its items — keyed by
/// the folder's path. The folders and files stay the only authority of the content (NN-001): a box whose folder is gone
/// is just not shown, and nothing here creates, moves or deletes anything.
/// </summary>
public sealed class AreaLayout
{
    public const string Path = ".urbe/area.json";
    public const int Version = 1;
    public const string Source = "Apresentação das caixas da Área de trabalho (UC-85). As pastas e os arquivos do vault continuam a única autoridade do conteúdo.";

    /// <summary>The borders a box may have.</summary>
    public static readonly IReadOnlyList<string> Borders = ["continua", "tracejada", "dupla", "brilho"];

    /// <summary>The icons a box may have.</summary>
    public static readonly IReadOnlyList<string> Icons = ["pasta", "livro", "estrela", "flor", "casa", "lista", "imagem", "pagina"];

    /// <summary>One box: null fields keep the default (the palette's colour by name, a plain border, the folder icon, recentes, grid).</summary>
    public sealed record Box(int? Color = null, string? Border = null, string? Icon = null, WorkspaceOrder? Order = null, bool? AsList = null)
    {
        public bool IsEmpty => Color is null && Border is null && Icon is null && Order is null && AsList is null;
    }

    private readonly Dictionary<string, Box> _boxes = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    /// <summary>The box of a folder as stored (null: all defaults).</summary>
    public Box? Of(string folder) => _boxes.GetValueOrDefault(folder);

    /// <summary>The colour of a folder: its project's stored colour, else the palette's by name.</summary>
    public int ColorOf(string folder)
    {
        var project = WorkspaceIndex.ProjectOf(folder + "/");
        return Of(project)?.Color is { } c && c >= 0 && c < WorkspaceIndex.Palette ? c : WorkspaceIndex.ColorOf(folder);
    }

    /// <summary>Stores a folder's box (an empty one is forgotten).</summary>
    public void Set(string folder, Box box)
    {
        if (box.Border is { } b && !Borders.Contains(b)) throw new ArgumentException("Borda desconhecida: " + b, nameof(box));
        if (box.Icon is { } i && !Icons.Contains(i)) throw new ArgumentException("Ícone desconhecido: " + i, nameof(box));
        if (box.Color is { } c && (c < 0 || c >= WorkspaceIndex.Palette)) throw new ArgumentOutOfRangeException(nameof(box), "Cor fora da paleta.");
        if (box.IsEmpty) _boxes.Remove(folder); else _boxes[folder] = box;
    }

    /// <summary>The projects in the stored order; the ones never ordered come after, as they came.</summary>
    public IReadOnlyList<T> Arrange<T>(IReadOnlyList<T> projects, Func<T, string> folderOf)
    {
        var rank = _order.Select((f, i) => (f, i)).ToDictionary(x => x.f, x => x.i, StringComparer.Ordinal);
        return [.. projects.Select((p, i) => (p, i)).OrderBy(x => rank.TryGetValue(folderOf(x.p), out var r) ? r : int.MaxValue).ThenBy(x => x.i).Select(x => x.p)];
    }

    /// <summary>Moves a project one place up (-1) or down (+1) among <paramref name="current"/> (the order shown now); false at an end.</summary>
    public bool Move(string project, int delta, IReadOnlyList<string> current)
    {
        var list = current.ToList();
        int at = list.IndexOf(project), to = at + Math.Sign(delta);
        if (at < 0 || to < 0 || to >= list.Count) return false;
        (list[at], list[to]) = (list[to], list[at]);
        _order.Clear();
        _order.AddRange(list);
        return true;
    }

    /// <summary>Puts a project in the place of <paramref name="target"/> among <paramref name="current"/> (dragging a box onto another); false when nothing moves.</summary>
    public bool MoveTo(string project, string target, IReadOnlyList<string> current)
    {
        var list = current.ToList();
        int from = list.IndexOf(project), to = list.IndexOf(target);
        if (from < 0 || to < 0 || from == to) return false;
        list.RemoveAt(from);
        list.Insert(to, project);
        _order.Clear();
        _order.AddRange(list);
        return true;
    }

    /// <summary>A folder renamed or moved by the app: its box and its subfolders' follow it. False when nothing was kept for it.</summary>
    public bool Renamed(string from, string to)
    {
        bool changed = false;
        foreach (var key in _boxes.Keys.Where(k => k == from || k.StartsWith(from + "/", StringComparison.Ordinal)).ToList())
        {
            var box = _boxes[key];
            _boxes.Remove(key);
            _boxes[to + key[from.Length..]] = box;
            changed = true;
        }
        for (int i = 0; i < _order.Count; i++)
            if (_order[i] == from) { _order[i] = to; changed = true; }
        return changed;
    }

    /// <summary>A folder gone (to the trash): its box and its subfolders' are forgotten.</summary>
    public bool Removed(string folder)
    {
        int before = _boxes.Count + _order.Count;
        foreach (var key in _boxes.Keys.Where(k => k == folder || k.StartsWith(folder + "/", StringComparison.Ordinal)).ToList()) _boxes.Remove(key);
        _order.Remove(folder);
        return _boxes.Count + _order.Count != before;
    }

    /// <summary>The file's text: the version, the source it declares, the order and the boxes, keys sorted.</summary>
    public string Serialize()
    {
        var boxes = new JsonObject();
        foreach (var (folder, box) in _boxes.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var o = new JsonObject();
            if (box.Color is { } c) o["cor"] = c;
            if (box.Border is { } b) o["borda"] = b;
            if (box.Icon is { } i) o["icone"] = i;
            if (box.Order is { } order) o["ordem"] = OrderName(order);
            if (box.AsList is { } l) o["lista"] = l;
            boxes[folder] = o;
        }
        var root = new JsonObject { ["versao"] = Version, ["fonte"] = Source, ["ordem"] = new JsonArray(_order.Select(f => (JsonNode)f).ToArray()), ["caixas"] = boxes };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    /// <summary>
    /// Reads the file. A file of a newer version is read for what this version knows and marked <paramref name="future"/>
    /// (never written over); an unreadable one gives the defaults and is marked too, so it is not replaced.
    /// </summary>
    public static AreaLayout Parse(string? json, out bool future)
    {
        future = false;
        var layout = new AreaLayout();
        if (string.IsNullOrWhiteSpace(json)) return layout;
        JsonObject root;
        try { root = JsonNode.Parse(json) as JsonObject ?? throw new JsonException(); }
        catch (JsonException) { future = true; return layout; }
        if (root["versao"] is not JsonValue v || !v.TryGetValue<int>(out var version) || version < 1) { future = true; return layout; }
        future = version > Version;
        if (root["ordem"] is JsonArray order)
            foreach (var f in order)
                if (f is JsonValue fv && fv.TryGetValue<string>(out var folder) && folder.Length > 0 && !layout._order.Contains(folder)) layout._order.Add(folder);
        if (root["caixas"] is JsonObject boxes)
            foreach (var (folder, node) in boxes)
            {
                if (node is not JsonObject o) continue;
                int? color = o["cor"] is JsonValue cv && cv.TryGetValue<int>(out var c) && c >= 0 && c < WorkspaceIndex.Palette ? c : null;
                string? border = o["borda"] is JsonValue bv && bv.TryGetValue<string>(out var b) && Borders.Contains(b) ? b : null;
                string? icon = o["icone"] is JsonValue iv && iv.TryGetValue<string>(out var i) && Icons.Contains(i) ? i : null;
                WorkspaceOrder? sort = o["ordem"] is JsonValue ov && ov.TryGetValue<string>(out var s) ? OrderOf(s) : null;
                bool? list = o["lista"] is JsonValue lv && lv.TryGetValue<bool>(out var l) ? l : null;
                var box = new Box(color, border, icon, sort, list);
                if (!box.IsEmpty) layout._boxes[folder] = box;
            }
        return layout;
    }

    /// <summary>True for a file this version may write over (the version it knows).</summary>
    public static bool IsLayout(string json)
    {
        var parsed = Parse(json, out var future);
        return !future && parsed is not null;
    }

    private static string OrderName(WorkspaceOrder order) => order switch { WorkspaceOrder.Name => "nome", WorkspaceOrder.Created => "criacao", _ => "recentes" };

    private static WorkspaceOrder? OrderOf(string name) => name switch { "nome" => WorkspaceOrder.Name, "criacao" => WorkspaceOrder.Created, "recentes" => WorkspaceOrder.Recent, _ => null };
}
