using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// UC-83/UC-84 (DEC-0053, ADD-0030): the Área de trabalho is the base of the app. Where the window is wide, the
/// Explorer is a sidebar beside it; on a phone, the Explorer opens over it as a drawer (Pastas). Editor and panels
/// still come over both.
/// </summary>
public sealed partial class MainView
{
    /// <summary>From this width of the stage the Explorer stays beside the Área de trabalho.</summary>
    public const double SidebarBreakpoint = 900;

    /// <summary>The sidebar's width beside the Área de trabalho.</summary>
    public const double SidebarWidth = 320;

    /// <summary>The Área de trabalho: projects as glass boxes, each a catalogue of its notes and files.</summary>
    public WorkspaceView Workspace { get; } = new();

    /// <summary>True while the Explorer is a sidebar beside the Área de trabalho (a wide window).</summary>
    public bool SidebarDocked => _stage.Bounds.Width >= SidebarBreakpoint;

    private void WireWorkspace()
    {
        // under the Explorer and the editor; after the Explorer in the tree, so the Explorer's controls come first
        Workspace.ZIndex = -1;
        _stage.Children.Insert(_stage.Children.IndexOf(Explorer) + 1, Workspace);
        Workspace.NoteRequested += path => OpenFromNotes(new CityNote(path, _contents.GetValueOrDefault(path, "")));
        Workspace.FileRequested += path => _ = ShowFileAsync(path);
        Workspace.NewProjectRequested += () => _ = CreateProjectAsync();
        Workspace.NewNoteRequested += folder => _ = CreateNoteInNotesAsync(folder: folder);
        Workspace.FolderMenuRequested += folder => _ = ShowFolderMenuAsync(folder);
        Workspace.ItemHeld += path => _ = ShowItemSheetAsync(path);
        Workspace.PresentationChanged += KeepPresentation;
        Workspace.SidebarRequested += ShowNotes;
        Workspace.SettingsRequested += () => _ = ShowSettingsAsync();
        _stage.SizeChanged += (_, e) => { if (e.WidthChanged) PlaceSidebar(); };
        Explorer.PropertyChanged += (_, e) => { if (e.Property == NotesExplorer.ZIndexProperty) PlaceSidebar(); };
        PlaceSidebar();
    }

    /// <summary>
    /// The Explorer beside the Área de trabalho (wide), hidden under it (narrow), or over everything while it is open
    /// as a drawer or over the editor.
    /// </summary>
    private void PlaceSidebar()
    {
        bool docked = SidebarDocked;
        Workspace.ShowsSidebarButton = !docked;
        Workspace.Margin = new Thickness(docked ? SidebarWidth : 0, 0, 0, 0);
        if (Explorer.Overlaid)
        {
            Explorer.IsVisible = true;
            Explorer.HorizontalAlignment = HorizontalAlignment.Stretch;
            Explorer.Width = double.NaN;
            Explorer.BorderThickness = default;
            return;
        }
        Explorer.IsVisible = docked;
        Explorer.HorizontalAlignment = docked ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
        Explorer.Width = docked ? SidebarWidth : double.NaN;
        Explorer.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line);
        Explorer.BorderThickness = docked ? new Thickness(0, 0, 1, 0) : default;
    }

    /// <summary>The Área de trabalho as the vault is now.</summary>
    private void RefreshWorkspace()
    {
        var notes = _contents.Select(n =>
        {
            var doc = _store?.Get(n.Key);
            return new WorkspaceNote(n.Key, doc?.Title, n.Value, doc?.Modified ?? doc?.Created, doc?.Created, doc?.Tags);
        }).ToList();
        var files = new HashSet<string>(_attachments, StringComparer.Ordinal);
        files.ExceptWith(_contents.Keys);
        Workspace.SetIndex(new WorkspaceIndex(notes, files, _library?.Folders));
    }

    /// <summary>A note chosen in the Explorer or the Área de trabalho: it opens in the editor (and joins Recentes).</summary>
    private void OpenFromNotes(CityNote note)
    {
        // openId: model.touchRecent(id)
        if (_store?.Get(note.Path) is { } opened) ExplorerState.TouchRecent(opened.Id);
        CloseNotesOverEditor();
        Editor.Open(note);
    }

    /// <summary>The dock's Notas: the Área de trabalho, from its root (over the editor, the Explorer as before).</summary>
    public void ShowWorkspace()
    {
        if (Editor.IsVisible) { ShowNotes(); return; }
        CloseNotesOverEditor();
        RefreshNotes();
        Workspace.Open("");
    }

    /// <summary>
    /// A card held in the Área de trabalho (UC-84 fatia 2): a note's information (folder, dates, size, links, tags) and
    /// its actions; a file's own actions.
    /// </summary>
    public Task ShowItemSheetAsync(string path)
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        if (!_contents.ContainsKey(path)) return ShowAttachmentActionsAsync(path, withView: true);
        if (_store?.Get(path) is not { } doc) return Task.CompletedTask;
        int cites, citedBy;
        using (var index = new KnowledgeIndex(_store))
        {
            cites = index.Links(doc.Id).Count(d => d.Id != doc.Id);
            citedBy = index.Backlinks(doc.Id).Count(d => d.Id != doc.Id);
        }
        bool favorite = ExplorerState.Favorites.Contains(doc.Id);
        return Dialogs.MenuAsync(doc.Title,
        [
            new("Abrir", UrbeTheme.Icons.Open, null, Run: () => { OpenFromNotes(new CityNote(path, _contents.GetValueOrDefault(path, ""))); return Task.CompletedTask; }),
            new("Renomear", UrbeTheme.Icons.Pencil, "Muda o nome do arquivo no vault.", Run: () => RenameNoteAsync(path)),
            new("Mover para", UrbeTheme.Icons.Move, "Outro projeto ou pasta.", Run: () => SelectionActionAsync("move", [path])),
            new("Duplicar", UrbeTheme.Icons.Copy, "Uma cópia ao lado.", Run: () => SelectionActionAsync("duplicate", [path])),
            favorite
                ? new("Tirar dos favoritos", UrbeTheme.Icons.Star, null, Run: () => { ExplorerState.Unfavorite(doc.Id); RefreshNotes(); return Task.CompletedTask; })
                : new("Favoritar", UrbeTheme.Icons.Star, "Aparece em Favoritos.", Run: () => { ExplorerState.Favorite(doc.Id); RefreshNotes(); return Task.CompletedTask; }),
            new("Mover para a lixeira", UrbeTheme.Icons.Trash, "Dá para restaurar pela Lixeira.", Danger: true, Run: () => SelectionActionAsync("delete", [path]))
        ], WorkspaceIndex.Describe(doc, cites, citedBy));
    }

    // ------------------------------------------------------------------ UC-85: how the boxes look (.urbe/area.json)

    private AreaLayout _area = new();
    private bool _areaReadOnly;

    /// <summary>How the boxes look and are ordered (.urbe/area.json), as loaded and changed since.</summary>
    public AreaLayout Area => _area;

    private void LoadArea(string? json)
    {
        _area = AreaLayout.Parse(json, out _areaReadOnly);
        Workspace.Layout = _area;
    }

    /// <summary>Writes .urbe/area.json with the verified write; a vault without it, a newer file or a protected vault are told, not written.</summary>
    private async Task<bool> SaveAreaAsync()
    {
        if (_vault is null || !VaultWritable || _areaReadOnly)
        {
            await Dialogs.AlertAsync("Aparência das caixas", _areaReadOnly ? "A aparência das caixas foi gravada por uma versão mais nova do Urbe e não é alterada aqui." : NoVaultMessage);
            return false;
        }
        try { await _vault.SaveAreaLayoutAsync(_area.Serialize()); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível gravar a aparência das caixas", ex.Message);
            return false;
        }
        return true;
    }

    /// <summary>A folder renamed, moved (<paramref name="to"/>) or gone (null): its box follows, written in the background.</summary>
    private void AreaFollows(string from, string? to)
    {
        if (_areaReadOnly || !(to is null ? _area.Removed(from) : _area.Renamed(from, to))) return;
        _ = SaveAreaAsync();
    }

    /// <summary>The box items of a project's menu: its appearance and its place among the projects.</summary>
    private IEnumerable<DialogItem> AreaMenuItems(string folder)
    {
        if (folder.Contains('/')) yield break;
        yield return new("Aparência da caixa", UrbeTheme.Icons.Brush, "Cor, borda e ícone.", Run: () => EditBoxAsync(folder));
        var order = Workspace.ProjectOrder;
        int at = order.IndexOf(folder);
        if (at > 0) yield return new("Mover caixa para antes", UrbeTheme.Icons.ArrowUp, null, Run: () => MoveBoxAsync(folder, -1));
        if (at >= 0 && at < order.Count - 1) yield return new("Mover caixa para depois", UrbeTheme.Icons.Move, null, Run: () => MoveBoxAsync(folder, 1));
    }

    /// <summary>The names of the palette's colours, borders and icons, as the person reads them.</summary>
    private static readonly string[] ColorNames = ["Azul", "Verde", "Âmbar", "Lilás", "Rosa", "Ciano", "Areia", "Lima"];
    private static readonly (string Id, string Label)[] BorderNames = [("continua", "Contínua"), ("tracejada", "Tracejada"), ("dupla", "Dupla"), ("brilho", "Brilho")];
    private static readonly (string Id, string Label)[] IconNames = [("pasta", "Pasta"), ("livro", "Livro"), ("estrela", "Estrela"), ("flor", "Flor"), ("casa", "Casa"), ("lista", "Lista"), ("imagem", "Imagem"), ("pagina", "Página")];

    /// <summary>"Aparência da caixa": colour, then border, then icon; nothing changes until the last one is chosen.</summary>
    public async Task EditBoxAsync(string project)
    {
        var box = _area.Of(project) ?? new AreaLayout.Box();
        var colors = ColorNames.Select((n, i) => new DialogItem(n + (_area.ColorOf(project) == i ? " (atual)" : ""), UrbeTheme.Icons.Folder)).ToList();
        if (await Dialogs.ChooseAsync("Cor de " + project, colors) is not { } color) return;
        var borders = BorderNames.Select(b => new DialogItem(b.Label + ((box.Border ?? "continua") == b.Id ? " (atual)" : ""), UrbeTheme.Icons.Region)).ToList();
        if (await Dialogs.ChooseAsync("Borda de " + project, borders) is not { } border) return;
        var icons = IconNames.Select(i => new DialogItem(i.Label + ((box.Icon ?? "pasta") == i.Id ? " (atual)" : ""), WorkspaceView.IconFor(i.Id))).ToList();
        if (await Dialogs.ChooseAsync("Ícone de " + project, icons) is not { } icon) return;
        var before = _area.Serialize();
        _area.Set(project, box with
        {
            Color = color == WorkspaceIndex.ColorOf(project) ? null : color,
            Border = BorderNames[border].Id == "continua" ? null : BorderNames[border].Id,
            Icon = IconNames[icon].Id == "pasta" ? null : IconNames[icon].Id
        });
        if (!await SaveAreaAsync()) { _area = AreaLayout.Parse(before, out _); Workspace.Layout = _area; return; }
        Workspace.Layout = _area;
    }

    /// <summary>Moves a project's box one place before (-1) or after (+1) among the projects.</summary>
    public async Task MoveBoxAsync(string project, int delta)
    {
        var before = _area.Serialize();
        if (!_area.Move(project, delta, Workspace.ProjectOrder)) return;
        if (!await SaveAreaAsync()) { _area = AreaLayout.Parse(before, out _); }
        Workspace.Layout = _area;
    }

    /// <summary>A box's order or grid/list changed: kept for that folder.</summary>
    private void KeepPresentation(string folder)
    {
        var box = _area.Of(folder) ?? new AreaLayout.Box();
        var next = box with { Order = Workspace.Order == WorkspaceOrder.Recent ? null : Workspace.Order, AsList = Workspace.AsList ? true : null };
        if (next == box || _areaReadOnly || _vault is null || !VaultWritable) return;
        _area.Set(folder, next);
        _ = SaveAreaAsync();
    }

    /// <summary>"+ Novo projeto": a folder at the top of the vault, shown as a new box, which opens.</summary>
    public async Task CreateProjectAsync(string? typedName = null)
    {
        if (_library is null || !VaultWritable)
        {
            await Dialogs.AlertAsync("Novo projeto", NoVaultMessage);
            return;
        }
        var typed = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Novo projeto", Label: "Nome do projeto", Placeholder: "Ex.: Matemática",
            Hint: "Uma pasta no topo do vault, mostrada como uma caixa na Área de trabalho.", Confirm: "Criar projeto"));
        if (string.IsNullOrWhiteSpace(typed)) return;
        if (await CreateFolderAsync("", typed) is not { } folder) return;
        RefreshNotes();
        Workspace.Open(folder);
    }
}
