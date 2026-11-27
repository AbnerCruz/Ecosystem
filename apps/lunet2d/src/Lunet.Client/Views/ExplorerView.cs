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
        root.Children.Add(new ScrollViewer { Content = _list });
        Content = root;
        Fill();
    }

    /// <summary>Redraws the tree from the game's folder (after a Studio or the AI added files).</summary>
    public void Fill()
    {
        _list.Children.Clear();
        if (_shell.Workspace.Project is not { } project) { Shown = []; return; }
        var shown = new List<string>();
        string? hiddenUnder = null;
        foreach (var entry in project.ListTree())
        {
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
        var more = LunetUi.IconButton(LunetIcons.More, "Ações de " + entry.Name, () =>
        {
            _actionsFor = _actionsFor == path ? null : path;
            _confirmDelete = false; _renaming = null;
            Fill();
        }, LunetTokens.Color.TextTertiary);
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
        var rename = LunetUi.Button("Renomear", ButtonKind.Ghost, null, () => { _renaming = entry.Path; Fill(); });
        rename.Name = "ExplorerRename";
        panel.Children.Add(rename);
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
