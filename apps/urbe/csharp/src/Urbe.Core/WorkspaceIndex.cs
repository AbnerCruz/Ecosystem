namespace Urbe.Core;

/// <summary>What an item of the Área de trabalho is, by its extension (its card's icon and what a tap opens).</summary>
public enum WorkspaceItemKind
{
    Note,
    Page,
    Image,
    Pdf,
    Media,
    Other
}

/// <summary>A note or file on a card: where it is, its title, when it last changed and the start of its text.</summary>
public sealed record WorkspaceItem(string Path, string Title, string Folder, WorkspaceItemKind Kind, string? Modified, string Snippet)
{
    public bool IsNote => Kind == WorkspaceItemKind.Note;
}

/// <summary>A folder as a box: its name, its colour, how many items it holds (with its subfolders) and when one last changed.</summary>
public sealed record WorkspaceBox(string Folder, string Name, int Color, int Count, string? Modified);

/// <summary>
/// UC-84 (DEC-0053, ADD-0030): the Área de trabalho over the vault. Each top folder is a project, a box; inside it, its
/// subfolders as smaller boxes and its notes and files as cards. The folders and files stay the only authority
/// (NN-001): this is a view built from them, never saved. Colours come from a stable palette by the folder's name,
/// so a project keeps its colour on every device while nothing about it is stored.
/// </summary>
public sealed class WorkspaceIndex
{
    /// <summary>How many colours the palette has (the client paints them).</summary>
    public const int Palette = 8;

    private readonly Dictionary<string, List<WorkspaceItem>> _direct = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<string>> _children = new(StringComparer.Ordinal);
    private readonly List<WorkspaceItem> _all = [];

    public WorkspaceIndex(IEnumerable<(string Path, string? Title, string? Content, string? Modified)> notes, IEnumerable<string>? files = null, IEnumerable<string>? folders = null)
    {
        ArgumentNullException.ThrowIfNull(notes);
        _children[""] = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var f in folders ?? []) AddFolder(f.Trim('/'));
        foreach (var (path, title, content, modified) in notes)
            Add(new WorkspaceItem(path, string.IsNullOrWhiteSpace(title) ? NameOf(path) : title!, NoteLibrary.FolderOf(path), KindOf(path), modified, Snippet(content)));
        foreach (var path in files ?? [])
            Add(new WorkspaceItem(path, NameOf(path), NoteLibrary.FolderOf(path), KindOf(path), null, ""));
        foreach (var list in _direct.Values) list.Sort(Recent);
    }

    /// <summary>The projects: the top folders, by name.</summary>
    public IReadOnlyList<WorkspaceBox> Projects => [.. Children("").Select(Box)];

    /// <summary>The notes and files at the root of the vault, in no project ("Soltas").</summary>
    public IReadOnlyList<WorkspaceItem> Loose => Items("");

    /// <summary>Every note and file.</summary>
    public int Count => _all.Count;

    /// <summary>The direct subfolders of a folder, by name.</summary>
    public IReadOnlyList<string> Children(string folder) =>
        _children.TryGetValue(folder, out var set) ? [.. set] : [];

    /// <summary>The notes and files directly in a folder, most recent first.</summary>
    public IReadOnlyList<WorkspaceItem> Items(string folder) =>
        _direct.TryGetValue(folder, out var list) ? list : [];

    /// <summary>The box of a folder: name, colour (of its project), items with its subfolders and the last change.</summary>
    public WorkspaceBox Box(string folder)
    {
        var inside = Inside(folder).ToList();
        return new WorkspaceBox(folder, NameOf(folder), ColorOf(folder), inside.Count, inside.Select(i => i.Modified).Where(m => m is not null).DefaultIfEmpty(null).Max(StringComparer.Ordinal));
    }

    /// <summary>The most recent items anywhere in a folder (with its subfolders): what the glass shows from outside.</summary>
    public IReadOnlyList<WorkspaceItem> Recent(string folder, int max) => [.. Inside(folder).OrderBy(i => i, Comparer<WorkspaceItem>.Create(Recent)).Take(Math.Max(0, max))];

    /// <summary>
    /// The items whose title or name holds every word of the query (case and accents ignored), in a folder and its
    /// subfolders, or everywhere when the folder is null; most recent first.
    /// </summary>
    public IReadOnlyList<WorkspaceItem> Search(string query, string? folder = null)
    {
        var words = Fold(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [];
        var pool = folder is null ? _all : Inside(folder);
        return [.. pool.Where(i => { var hay = Fold(i.Title + " " + NameOf(i.Path)); return words.All(w => hay.Contains(w, StringComparison.Ordinal)); })
            .OrderBy(i => i, Comparer<WorkspaceItem>.Create(Recent))];
    }

    /// <summary>The project a path belongs to (its top folder), or "" at the root.</summary>
    public static string ProjectOf(string path)
    {
        int slash = path.IndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    /// <summary>
    /// The colour of a folder: its project's, from a stable hash of the project's name (FNV-1a, the same on every
    /// device), the same colour Conexões gives its notes.
    /// </summary>
    public static int ColorOf(string folder)
    {
        var project = folder.Contains('/') ? folder[..folder.IndexOf('/')] : folder;
        return (int)(LinkGraph.Hash(project) % Palette);
    }

    public static WorkspaceItemKind KindOf(string path)
    {
        var p = path.ToLowerInvariant();
        if (p.EndsWith(".page.json", StringComparison.Ordinal)) return WorkspaceItemKind.Page;
        return System.IO.Path.GetExtension(p) switch
        {
            ".md" or ".markdown" or ".txt" => WorkspaceItemKind.Note,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" or ".bmp" or ".avif" => WorkspaceItemKind.Image,
            ".pdf" => WorkspaceItemKind.Pdf,
            ".mp3" or ".m4a" or ".ogg" or ".wav" or ".opus" or ".aac" or ".flac" or ".mp4" or ".webm" or ".mov" or ".mkv" => WorkspaceItemKind.Media,
            _ => WorkspaceItemKind.Other
        };
    }

    /// <summary>The start of a note's text for its card: the first line that is not a heading, front matter, a rule or a fence, without the marks.</summary>
    public static string Snippet(string? content, int max = 140)
    {
        if (string.IsNullOrEmpty(content)) return "";
        var lines = content.Replace("\r\n", "\n").Split('\n');
        int i = 0;
        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            i = 1;
            while (i < lines.Length && lines[i].Trim() != "---") i++;
            i++;
        }
        for (; i < lines.Length; i++)
        {
            var l = lines[i].Trim();
            if (l.Length == 0 || l.StartsWith('#') || l.StartsWith("```", StringComparison.Ordinal) || l is "---" or "***" || l.StartsWith("%%", StringComparison.Ordinal)) continue;
            l = l.TrimStart('>', '-', '*', '+', ' ');
            if (l.StartsWith("[ ] ", StringComparison.Ordinal) || l.StartsWith("[x] ", StringComparison.OrdinalIgnoreCase)) l = l[4..];
            l = System.Text.RegularExpressions.Regex.Replace(l, @"\[\[([^\]|]+)\|?([^\]]*)\]\]", m => m.Groups[2].Value.Length > 0 ? m.Groups[2].Value : m.Groups[1].Value);
            l = System.Text.RegularExpressions.Regex.Replace(l, @"\[([^\]]*)\]\([^)]*\)", "$1");
            l = System.Text.RegularExpressions.Regex.Replace(l, @"[*_`~]|\{ia:""[^""]*""\}", "").Trim();
            if (l.Length == 0) continue;
            return l.Length > max ? l[..(max - 1)].TrimEnd() + "…" : l;
        }
        return "";
    }

    /// <summary>
    /// What a held card tells about a note (UC-84 fatia 2): where it is, when it was made and changed, how long it is,
    /// how it is linked and its tags, one line each.
    /// </summary>
    public static string Describe(UrbeDocument doc, int cites, int citedBy, TimeZoneInfo? zone = null)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var lines = new List<string>();
        var folder = NoteLibrary.FolderOf(doc.Path);
        lines.Add("Pasta: " + (folder.Length == 0 ? "raiz do vault" : folder));
        string? Date(string? iso) => DateTimeOffset.TryParse(iso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at)
            ? TimeZoneInfo.ConvertTime(at, zone ?? TimeZoneInfo.Local).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) : null;
        var dates = new[] { Date(doc.Created) is { } c ? "Criada em " + c : null, Date(doc.Modified) is { } m ? "alterada em " + m : null }.Where(x => x is not null).ToList();
        if (dates.Count > 0) lines.Add(string.Join(" · ", dates));
        int words = JsText.Words(doc.Content).Length;
        lines.Add((words == 1 ? "1 palavra" : words + " palavras") + " · cita " + cites + " · citada por " + citedBy);
        if (doc.Tags.Count > 0) lines.Add("Tags: " + string.Join(" ", doc.Tags.Select(t => "#" + t.TrimStart('#'))));
        return string.Join("\n", lines);
    }

    private static string NameOf(string path)
    {
        var name = path.Contains('/') ? path[(path.LastIndexOf('/') + 1)..] : path;
        if (name.EndsWith(".page.json", StringComparison.OrdinalIgnoreCase)) return name[..^".page.json".Length];
        return name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? name[..^3] : name;
    }

    private static string Fold(string text)
    {
        var d = (text ?? "").ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(d.Length);
        foreach (var c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    private static int Recent(WorkspaceItem a, WorkspaceItem b)
    {
        int byDate = string.CompareOrdinal(b.Modified, a.Modified);
        return byDate != 0 ? byDate : string.Compare(a.Title, b.Title, StringComparison.CurrentCultureIgnoreCase);
    }

    private IEnumerable<WorkspaceItem> Inside(string folder) =>
        folder.Length == 0 ? _all : _all.Where(i => i.Folder == folder || i.Folder.StartsWith(folder + "/", StringComparison.Ordinal));

    private void Add(WorkspaceItem item)
    {
        AddFolder(item.Folder);
        if (!_direct.TryGetValue(item.Folder, out var list)) _direct[item.Folder] = list = [];
        list.Add(item);
        _all.Add(item);
    }

    private void AddFolder(string folder)
    {
        while (folder.Length > 0)
        {
            var parent = NoteLibrary.FolderOf(folder);
            if (!_children.TryGetValue(parent, out var set)) _children[parent] = set = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            if (!set.Add(folder)) return;
            folder = parent;
        }
    }
}
