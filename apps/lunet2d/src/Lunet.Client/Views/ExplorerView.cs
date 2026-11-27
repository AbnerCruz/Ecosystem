using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// The Explorer (owner, 27/11: "veja explorador de arquivos"): the game's folder as a tree, like VS Code's side bar.
/// It replaces the workspace's tab bar (Código/Jogo/Recursos/Notas): folders are folders, and a tap on a file opens
/// it where it belongs — code and text in the editor (as a tab), notes in the notes editor, art, maps, sounds, music
/// and data in their Studio. New file, new folder, rename and delete live here, on the item they act on.
/// </summary>
public sealed class ExplorerView : UserControl
{
    private readonly LunetShell _shell;
    private readonly Action _opened;
    private readonly StackPanel _list = new() { Spacing = 0 };
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private string? _actionsFor;
    private bool _confirmDelete;
    private bool _moving;
    private string? _renaming;
    private readonly TextBlock _notice = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);

    /// <param name="opened">Called after a file opened (a phone closes the drawer).</param>
    public ExplorerView(LunetShell shell, Action opened)
    {
        _shell = shell;
        _opened = opened;
        _notice.TextWrapping = TextWrapping.Wrap;
        _notice.Margin = new Thickness(LunetTokens.Space.M, 0);
        Build();
    }

    /// <summary>The paths shown (tests).</summary>
    public IReadOnlyList<string> Shown { get; private set; } = [];

    private void Build()
    {
        var header = new DockPanel { Margin = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S, LunetTokens.Space.Xs, LunetTokens.Space.Xs) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var newFile = LunetUi.IconButton(LunetIcons.Plus, "Novo arquivo C#", () => Create(folder: false));
        newFile.Name = "ExplorerNewFile";
        var newFolder = LunetUi.IconButton(LunetIcons.Folder, "Nova pasta", () => Create(folder: true));
        newFolder.Name = "ExplorerNewFolder";
        var search = LunetUi.IconButton(LunetIcons.Search, "Buscar no jogo", () => ShowSearch(!Searching));
        search.Name = "ExplorerSearch";
        actions.Children.Add(search);
        actions.Children.Add(newFile);
        actions.Children.Add(newFolder);
        DockPanel.SetDock(actions, Dock.Right);
        header.Children.Add(actions);
        var title = LunetUi.Text((_shell.Workspace.Project?.Name ?? "").ToUpperInvariant(), LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary, FontWeight.SemiBold);
        title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);
        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        DockPanel.SetDock(_notice, Dock.Bottom);
        root.Children.Add(_notice);
        _filter.TextChanged += (_, _) => Fill();
        AutomationProperties.SetName(_filter, "Filtrar arquivos");
        var tree = new DockPanel();
        DockPanel.SetDock(_filter, Dock.Top);
        tree.Children.Add(_filter);
        tree.Children.Add(new ScrollViewer { Content = _list });
        _tree.Content = tree;
        _body.Child = _tree;
        root.Children.Add(_body);
        Content = root;
        Fill();
    }

    private readonly ContentControl _tree = new();
    private readonly TextBox _filter = new()
    {
        PlaceholderText = "Filtrar por nome", Name = "ExplorerFilter", MinHeight = LunetTokens.MinTouch,
        Margin = new Thickness(LunetTokens.Space.S, 0, LunetTokens.Space.S, LunetTokens.Space.Xs),
    };

    /// <summary>The name filter over the tree ("" shows everything).</summary>
    public string Filter { get => _filter.Text ?? ""; set => _filter.Text = value; }
    private readonly Border _body = new();
    private SearchView? _search;

    /// <summary>The search in the whole game (the magnifier in the header; Ctrl+Shift+F).</summary>
    public SearchView Search => _search ??= new SearchView(_shell, _opened);

    /// <summary>True while the search shows instead of the tree.</summary>
    public bool Searching => _body.Child == _search && _search is not null;

    /// <summary>Shows the search (with a word to look for) or back the tree.</summary>
    public void ShowSearch(bool on, string? text = null)
    {
        _body.Child = on ? Search : _tree;
        if (on) Search.Focus(text);
    }

    /// <summary>Redraws the tree from the game's folder (after a Studio or the AI added files).</summary>
    public void Fill()
    {
        _list.Children.Clear();
        if (_shell.Workspace.Project is not { } project) { Shown = []; return; }
        var shown = new List<string>();
        string? hiddenUnder = null;
        var tree = project.ListTree();
        var filter = Filter.Trim();
        HashSet<string>? keep = null;
        if (filter.Length > 0)
        {
            // Matching names and the folders above them, all open (the filter overrides what was collapsed).
            keep = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in tree.Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                keep.Add(e.Path);
                var parts = e.Path.Split('/');
                for (var i = 1; i < parts.Length; i++) keep.Add(string.Join('/', parts[..i]));
            }
        }
        foreach (var entry in tree)
        {
            if (keep is not null)
            {
                if (!keep.Contains(entry.Path)) continue;
                shown.Add(entry.Path);
                _list.Children.Add(Row(entry));
                if (_actionsFor == entry.Path) _list.Children.Add(Actions(entry));
                continue;
            }
            if (hiddenUnder is not null && entry.Path.StartsWith(hiddenUnder + "/", StringComparison.Ordinal)) continue;
            hiddenUnder = entry.IsDirectory && _collapsed.Contains(entry.Path) ? entry.Path : null;
            shown.Add(entry.Path);
            _list.Children.Add(Row(entry));
            if (_actionsFor == entry.Path) _list.Children.Add(Actions(entry));
        }
        Shown = shown;
    }

    private Control Row(ExplorerEntry entry)
    {
        var open = !entry.IsDirectory && (entry.Path == _shell.WorkspaceView.Code.OpenPath || entry.Path == _shell.WorkspaceView.Notes.OpenPath);
        var icon = entry.IsDirectory ? (_collapsed.Contains(entry.Path) ? LunetIcons.ChevronRight : LunetIcons.ChevronDown) : IconFor(entry.Path);
        var label = LunetUi.Text(entry.Name, LunetTokens.Type.Label, open ? LunetTokens.Color.TextPrimary : LunetTokens.Color.TextSecondary,
            entry.IsDirectory || open ? FontWeight.SemiBold : FontWeight.Normal);
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.VerticalAlignment = VerticalAlignment.Center;
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs, Margin = new Thickness(LunetTokens.Space.M + entry.Depth * 14, 0, 0, 0) };
        line.Children.Add(LunetIcons.Create(icon, entry.IsDirectory ? LunetTokens.Color.TextSecondary : LunetTokens.Color.Info, 16));
        line.Children.Add(label);
        var row = new Button
        {
            Content = line, MinHeight = 40, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(0), CornerRadius = new CornerRadius(0), BorderThickness = new Thickness(0),
            Background = LunetTokens.Brush(open ? LunetTokens.Color.PrimarySoft : "#00000000"), Name = "ExplorerItem",
        };
        AutomationProperties.SetName(row, (entry.IsDirectory ? "Pasta " : "Abrir ") + entry.Path);
        var path = entry.Path;
        row.Click += (_, _) =>
        {
            if (entry.IsDirectory) { if (!_collapsed.Remove(path)) _collapsed.Add(path); Fill(); }
            else Open(path);
        };
        void ToggleActions()
        {
            _actionsFor = _actionsFor == path ? null : path;
            _confirmDelete = false; _renaming = null; _moving = false;
            Fill();
        }
        var more = LunetUi.IconButton(LunetIcons.More, "Ações de " + entry.Name, ToggleActions, LunetTokens.Color.TextTertiary);
        // A long press (touch) or a right click opens the same actions, like VS Code's context menu.
        row.ContextRequested += (_, e) => { e.Handled = true; ToggleActions(); };
        var dock = new DockPanel();
        DockPanel.SetDock(more, Dock.Right);
        dock.Children.Add(more);
        dock.Children.Add(row);
        return dock;
    }

    private Control Actions(ExplorerEntry entry)
    {
        var panel = new WrapPanel { Margin = new Thickness(LunetTokens.Space.M + entry.Depth * 14 + 24, 0, LunetTokens.Space.S, LunetTokens.Space.Xs) };
        if (_renaming == entry.Path)
        {
            var box = new TextBox { Text = entry.Name, MinWidth = 180, MinHeight = LunetTokens.MinTouch, Name = "ExplorerRenameBox" };
            AutomationProperties.SetName(box, "Novo nome");
            box.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; Rename(entry, box.Text ?? ""); } };
            panel.Children.Add(box);
            panel.Children.Add(LunetUi.Button("Renomear", ButtonKind.Primary, null, () => Rename(entry, box.Text ?? "")));
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { box.Focus(); box.SelectAll(); });
            return panel;
        }
        if (_moving)
        {
            // Mover para…: every folder of the game (and the root), except the item itself and what is inside it.
            var folders = new List<string> { "" };
            folders.AddRange(_shell.Workspace.Project!.ListTree().Where(e => e.IsDirectory).Select(e => e.Path));
            var here = Path.GetDirectoryName(entry.Path)?.Replace('\\', '/') ?? "";
            foreach (var folder in folders.Where(f => f != here && f != entry.Path && !f.StartsWith(entry.Path + "/", StringComparison.Ordinal)))
            {
                var target = folder;
                var b = LunetUi.Button(folder.Length == 0 ? "(raiz do jogo)" : folder, ButtonKind.Ghost, LunetIcons.Folder, () => Move(entry, target));
                b.Name = "ExplorerMoveTo";
                panel.Children.Add(b);
            }
            panel.Children.Add(LunetUi.Button("Cancelar", ButtonKind.Ghost, null, () => { _moving = false; Fill(); }));
            return panel;
        }
        var rename = LunetUi.Button("Renomear", ButtonKind.Ghost, null, () => { _renaming = entry.Path; Fill(); });
        rename.Name = "ExplorerRename";
        panel.Children.Add(rename);
        var move = LunetUi.Button("Mover para…", ButtonKind.Ghost, LunetIcons.Folder, () => { _moving = true; Fill(); });
        move.Name = "ExplorerMove";
        panel.Children.Add(move);
        if (!entry.IsDirectory)
        {
            var duplicate = LunetUi.Button("Duplicar", ButtonKind.Ghost, LunetIcons.Copy, () => Duplicate(entry.Path));
            duplicate.Name = "ExplorerDuplicate";
            panel.Children.Add(duplicate);
        }
        var copyPath = LunetUi.Button("Copiar caminho", ButtonKind.Ghost, LunetIcons.Copy, () =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip) _ = Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clip, entry.Path);
            _actionsFor = null; Fill(); Say("Caminho copiado: " + entry.Path);
        });
        copyPath.Name = "ExplorerCopyPath";
        panel.Children.Add(copyPath);
        if (entry.IsDirectory)
        {
            panel.Children.Add(LunetUi.Button("Novo arquivo aqui", ButtonKind.Ghost, LunetIcons.Plus, () => Create(folder: false, inside: entry.Path)));
            panel.Children.Add(LunetUi.Button("Nova pasta aqui", ButtonKind.Ghost, LunetIcons.Folder, () => Create(folder: true, inside: entry.Path)));
        }
        var delete = LunetUi.Button(_confirmDelete ? "Confirmar: apagar " + entry.Name : "Apagar", _confirmDelete ? ButtonKind.Primary : ButtonKind.Ghost, LunetIcons.Close, () =>
        {
            if (!_confirmDelete) { _confirmDelete = true; Fill(); return; }
            Delete(entry);
        });
        delete.Name = "ExplorerDelete";
        panel.Children.Add(delete);
        return panel;
    }

    /// <summary>Opens the folders above <paramref name="path"/> and scrolls it into sight.</summary>
    public void Reveal(string path)
    {
        var parts = path.Split('/');
        for (var i = 1; i < parts.Length; i++) _collapsed.Remove(string.Join('/', parts[..i]));
        Fill();
        var index = Shown.ToList().IndexOf(path);
        Revealed = index >= 0 ? path : null;
        var row = _list.Children.OfType<Control>().Where(c => c is DockPanel).Skip(Math.Max(index, 0)).FirstOrDefault();
        if (index >= 0 && row is not null) Avalonia.Threading.Dispatcher.UIThread.Post(() => row.BringIntoView(), Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>The last file revealed (tests).</summary>
    public string? Revealed { get; private set; }

    /// <summary>Opens a file where it belongs (see the class summary).</summary>
    public void Open(string path)
    {
        _shell.WorkspaceView.OpenFile(path);
        _opened();
    }

    /// <summary>Creates a C# file (or a folder) at the root or inside a folder, with a free name, and opens the file.</summary>
    public string Create(bool folder, string? inside = null)
    {
        var project = _shell.Workspace.Project ?? throw new InvalidOperationException("Nenhum jogo aberto.");
        var files = project.ListTree().Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var prefix = inside is null ? "" : inside + "/";
        string path;
        var n = 1;
        do path = prefix + (folder ? $"Pasta{(n == 1 ? "" : n)}" : $"Script{(n == 1 ? "" : n)}.cs"); while (files.Contains(path) && n++ < 10_000);
        try
        {
            if (folder) project.CreateDirectory(path);
            else
            {
                var name = Path.GetFileNameWithoutExtension(path);
                project.CreateFile(path, $"using Lunet;\n\npublic sealed class {name}\n{{\n}}\n");
            }
        }
        catch (Exception e) when (e is IOException or ProjectException or UnauthorizedAccessException) { Say(e.Message); return path; }
        if (inside is not null) _collapsed.Remove(inside);
        _actionsFor = path; _renaming = path; _confirmDelete = false; // name it right away
        Fill();
        if (!folder) _shell.WorkspaceView.OpenFile(path);
        return path;
    }

    /// <summary>Renames a file or folder in place (same folder); open tabs follow.</summary>
    public string? Rename(ExplorerEntry entry, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0 || newName.IndexOfAny(['/', '\\']) >= 0 || newName is "." or "..") { Say("Use só o nome, sem barras."); return null; }
        if (!entry.IsDirectory && Path.GetExtension(newName).Length == 0) newName += Path.GetExtension(entry.Name);
        var folder = Path.GetDirectoryName(entry.Path)?.Replace('\\', '/');
        var to = string.IsNullOrEmpty(folder) ? newName : folder + "/" + newName;
        if (to == entry.Path) { _renaming = null; Fill(); return to; }
        try
        {
            _shell.WorkspaceView.SaveAll();
            _shell.Workspace.Project!.Rename(entry.Path, to);
        }
        catch (Exception e) when (e is IOException or ProjectException or UnauthorizedAccessException) { Say(e.Message); return null; }
        _renaming = null; _actionsFor = null;
        _shell.WorkspaceView.FilesMoved(entry.Path, to);
        Fill();
        return to;
    }

    private void Delete(ExplorerEntry entry)
    {
        try
        {
            _shell.WorkspaceView.SaveAll();
            _shell.Workspace.Project!.Delete(entry.Path);
        }
        catch (Exception e) when (e is IOException or ProjectException or UnauthorizedAccessException) { Say(e.Message); return; }
        _actionsFor = null; _confirmDelete = false;
        _shell.WorkspaceView.FilesMoved(entry.Path, null);
        Fill();
    }

    /// <summary>"Script.cs" → "Script cópia.cs" (then "Script cópia 2.cs"…), next to it, and opens the copy.</summary>
    public string? Duplicate(string path)
    {
        var project = _shell.Workspace.Project ?? throw new InvalidOperationException("Nenhum jogo aberto.");
        var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
        var name = Path.GetFileName(path);
        var dot = name.IndexOf('.', 1);
        var (stem, ext) = dot < 0 ? (name, "") : (name[..dot], name[dot..]);
        var taken = project.ListTree().Select(e => e.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string to;
        var n = 1;
        do to = (string.IsNullOrEmpty(folder) ? "" : folder + "/") + stem + (n == 1 ? " cópia" : $" cópia {n}") + ext; while (taken.Contains(to) && n++ < 10_000);
        try
        {
            _shell.WorkspaceView.SaveAll();
            project.WriteBytes(to, project.ReadBytes(path));
        }
        catch (Exception e) when (e is IOException or ProjectException or UnauthorizedAccessException) { Say(e.Message); return null; }
        _actionsFor = to; _renaming = to; // name the copy right away
        Fill();
        return to;
    }

    /// <summary>Moves a file or folder into another folder ("" is the game's root); open tabs follow.</summary>
    public string? Move(ExplorerEntry entry, string folder)
    {
        var to = (folder.Length == 0 ? "" : folder + "/") + entry.Name;
        _moving = false;
        if (to == entry.Path) { Fill(); return to; }
        try
        {
            if (_shell.Workspace.Project!.ListTree().Any(e => string.Equals(e.Path, to, StringComparison.OrdinalIgnoreCase)))
            { Say($"Já existe {to}."); Fill(); return null; }
            _shell.WorkspaceView.SaveAll();
            _shell.Workspace.Project!.Rename(entry.Path, to);
        }
        catch (Exception e) when (e is IOException or ProjectException or UnauthorizedAccessException) { Say(e.Message); Fill(); return null; }
        _actionsFor = null;
        if (folder.Length > 0) _collapsed.Remove(folder);
        _shell.WorkspaceView.FilesMoved(entry.Path, to);
        Fill();
        return to;
    }

    private void Say(string text) => _notice.Text = text;

    private static string IconFor(string path)
    {
        var p = path.ToLowerInvariant();
        if (p.EndsWith(".cs")) return LunetIcons.Code;
        if (p.EndsWith(".md")) return LunetIcons.Notes;
        if (p.EndsWith(".png") || p.EndsWith(".jpg") || p.EndsWith(".sprite.json")) return LunetIcons.Art;
        if (p.EndsWith(".wav") || p.EndsWith(".ogg") || p.EndsWith(".sfx.json") || p.EndsWith(".song.json")) return LunetIcons.Music;
        return LunetIcons.File;
    }
}
