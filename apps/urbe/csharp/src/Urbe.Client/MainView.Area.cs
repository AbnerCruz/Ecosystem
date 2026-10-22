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
            return (n.Key, doc?.Title, (string?)n.Value, doc?.Modified ?? doc?.Created);
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
