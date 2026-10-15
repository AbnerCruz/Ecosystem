using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client;

/// <summary>
/// The Notas screen of the 1.8.4 (src/explorer/mobile-ui.js, styles/explorer-mobile.css + theme.css #urbeMobileExplorer):
/// header "Notas" with Nova, Mais opções and Fechar, the "Buscar notas" field, the expandable tree of folders (each a
/// neighbourhood) and notes (each a house), Recentes, Favoritos, the Lixeira and the floating selection bar. It never
/// reads or writes the vault: the notes come from MainView/Urbe.Core and every action is raised as an event.
/// </summary>
public sealed class NotesExplorer : Border
{
    /// <summary>model.js folderId: how a folder is kept in the selection.</summary>
    public const string FolderPrefix = "folder:";

    private static readonly StringComparer Locale = StringComparer.InvariantCulture; // localeCompare

    private readonly TextBlock _title = new() { Text = "Explorer", FontSize = 24, FontWeight = FontWeight.Bold, LetterSpacing = -0.48, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _back, _new, _more, _close;
    private readonly Border _searchBox;
    private readonly TextBox _search = new()
    {
        PlaceholderText = "Buscar arquivos",
        FontSize = 16,
        FontWeight = FontWeight.Medium,
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0),
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center
    };
    private readonly StackPanel _list = new() { Margin = new Thickness(8, 0, 8, 88) };
    private readonly Border _bar = new();
    private readonly TextBlock _count = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) };
    private readonly List<Button> _noteActions = [];
    private readonly List<CityNote> _notes = [];
    private readonly List<string> _files = [];
    private readonly Dictionary<Control, string> _folderRows = [];
    private Control? _dropTarget;
    private readonly HashSet<string> _extraFolders = new(StringComparer.Ordinal);
    private readonly List<string> _selected = [];
    private readonly HashSet<string> _localExpanded = new(StringComparer.Ordinal) { "" };
    private readonly List<string> _visibleNotes = [];
    private readonly List<string> _visibleFolders = [];
    private string _view = "tree";
    private IReadOnlyList<TrashEntry>? _trash;
    private Collection? _collection;
    private bool _wide;

    public NotesExplorer()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        AddHandler(PointerMovedEvent, OnDragMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnDragReleased, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _back = HeaderButton(UrbeTheme.Icons.Back, "Voltar para todas as notas");
        _back.Margin = new Thickness(-12, 0, 0, 0);
        _back.Click += (_, _) => Show("tree");
        _new = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    UrbeTheme.Icon(UrbeTheme.Icons.Plus, 18, UrbeTheme.Accent, 1.8),
                    new TextBlock { Text = "Nova", FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Accent), VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Height = 44,
            MinWidth = 44,
            Padding = new Thickness(10, 0, 14, 0),
            CornerRadius = new CornerRadius(12),
            Background = UrbeTheme.AccentSoft,
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(_new, "Nova nota");
        _new.Click += (_, _) => NewRequested?.Invoke();
        _more = HeaderButton(UrbeTheme.Icons.MoreUi, "Mais opções");
        _more.Click += (_, _) => MoreRequested?.Invoke();
        _close = HeaderButton(UrbeTheme.Icons.CloseUi, "Fechar");
        _close.Click += (_, _) => Closed?.Invoke();

        // #urbeMobileExplorer>header: min-height 60px, padding 0 8px 0 20px, gap 4px; strong flex 1; buttons gap 2px
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { _new, _more, _close } };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), MinHeight = 60, Margin = new Thickness(20, 0, 8, 0) };
        Grid.SetColumn(_back, 0);
        Grid.SetColumn(_title, 1);
        Grid.SetColumn(buttons, 2);
        header.Children.Add(_back);
        header.Children.Add(_title);
        header.Children.Add(buttons);

        // .ume-search: 12px above (measured in the 1.8.4: the field at y 72 under a 60px header), 16px sides, 8px below; label 44px, padding 0 14px, gap 10px, radius 12, surface-2
        _search.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        var searchIcon = UrbeTheme.Icon(UrbeTheme.Icons.SearchUi, 18, UrbeTheme.Text3, 1.8);
        searchIcon.VerticalAlignment = VerticalAlignment.Center;
        searchIcon.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(searchIcon, Dock.Left);
        _searchBox = new Border
        {
            Height = 44,
            Margin = new Thickness(16, 12, 16, 8),
            Padding = new Thickness(14, 0),
            CornerRadius = new CornerRadius(12),
            Background = UrbeTheme.Brush(UrbeTheme.Surface2),
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(1),
            Child = new DockPanel { Children = { searchIcon, _search } }
        };
        _search.GotFocus += (_, _) => _searchBox.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _search.LostFocus += (_, _) => _searchBox.BorderBrush = Brushes.Transparent;
        _search.TextChanged += (_, _) => Show((_search.Text ?? "").Trim().Length > 0 ? "search" : "tree");

        var scroll = new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*") };
        Grid.SetRow(header, 0);
        Grid.SetRow(_searchBox, 1);
        Grid.SetRow(scroll, 2);
        layout.Children.Add(header);
        layout.Children.Add(_searchBox);
        layout.Children.Add(scroll);
        var bar = SelectionBar();
        Grid.SetRow(bar, 2);
        layout.Children.Add(bar);
        Child = layout;

        KeyDown += (_, e) =>
        {
            // Escape: clears the search, otherwise closes
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            if ((_search.Text ?? "").Length > 0) SearchText = "";
            else Closed?.Invoke();
        };
        Render();
    }

    /// <summary>Fechar (on wide screens, where Notas is a panel beside the city).</summary>
    public event Action? Closed;
    /// <summary>The header's Nova (newMenu: "Criar").</summary>
    public event Action? NewRequested;
    /// <summary>The header's "Mais opções" (moreMenu).</summary>
    public event Action? MoreRequested;
    /// <summary>The empty tree's "Criar primeira nota" (createNote).</summary>
    public event Action? CreateFirstNoteRequested;
    /// <summary>Lixeira: Restaurar (the trashed document's id).</summary>
    public event Action<string>? RestoreRequested;
    /// <summary>Lixeira: ✕ Excluir de vez (the trashed document's id).</summary>
    public event Action<string>? PurgeRequested;
    /// <summary>Lixeira: Esvaziar.</summary>
    public event Action? EmptyTrashRequested;
    public event Action<CityNote>? NoteRequested;

    /// <summary>A tap on a file that is not a note (an image, a PDF…): the host shows it (DEC-0045, UC-38).</summary>
    public event Action<string>? FileRequested;

    /// <summary>The "⋯" of a folder row: the host shows what can be done with that folder (DEC-0045, UC-38).</summary>
    public event Action<string>? FolderMenuRequested;

    /// <summary>Notes held and dragged onto a folder row: move them there (DEC-0045, UC-38).</summary>
    public event Action<IReadOnlyList<string>, string>? MoveRequested;
    /// <summary>An action of the selection bar (.ume-actions): "open", "favorite", "move", "duplicate", "group" or "delete",
    /// with the selection: notes by path, folders as "folder:" + their path (model.js folderId).</summary>
    public event Action<string, IReadOnlyList<string>>? SelectionAction;

    /// <summary>Where the folders open in the tree are kept (model.expanded); without one, for the session only.</summary>
    public ExplorerState? State { get; set; }

    /// <summary>How long a press must last to be a hold (mobile-ui.js HOLD: 430 ms).</summary>
    public TimeSpan HoldDelay { get; set; } = TimeSpan.FromMilliseconds(430);

    /// <summary>One item of the trash as the Lixeira lists it (renderTrash): the document's id and title, its path.</summary>
    public sealed record TrashEntry(string Id, string Title, string OriginalPath);

    /// <summary>A collection view (Recentes, Favoritos: renderCollection).</summary>
    public sealed record Collection(string Title, IReadOnlyList<CityNote> Notes, string EmptyTitle, string EmptyText);

    /// <summary>The Lixeira view (show('trash')); null goes back to the tree.</summary>
    public void ShowTrash(IReadOnlyList<TrashEntry>? items)
    {
        _trash = items;
        if (items is not null) { _collection = null; _view = "trash"; }
        else if (_view == "trash") _view = "tree";
        Render();
    }

    /// <summary>show('recent') / show('favorites'); null goes back to the tree.</summary>
    public void ShowCollection(Collection? collection)
    {
        _collection = collection;
        if (collection is not null) { _trash = null; _view = "collection"; }
        else if (_view == "collection") _view = "tree";
        Render();
    }

    /// <summary>The title of the collection shown, or null.</summary>
    public string? CollectionTitle => _view == "collection" ? _collection?.Title : null;

    /// <summary>The Lixeira is shown.</summary>
    public bool ShowingTrash => _view == "trash";

    /// <summary>The selection (model.selection), in the order it was made: notes by path, folders as "folder:" + path.</summary>
    public IReadOnlyList<string> SelectedPaths => _selected.ToList();

    /// <summary>currentFolder(): the first selected folder, or the root ("") — where Nova creates.</summary>
    public string CurrentFolder => _selected.FirstOrDefault(x => x.StartsWith(FolderPrefix, StringComparison.Ordinal)) is { } f ? f[FolderPrefix.Length..] : "";

    public string SearchText
    {
        get => _search.Text ?? string.Empty;
        set
        {
            _search.Text = value;
            Show(value.Trim().Length > 0 ? "search" : "tree"); // deterministic even before Avalonia raises TextChanged
        }
    }

    /// <summary>The notes whose rows are shown, in order.</summary>
    public IReadOnlyList<string> VisibleNotePaths { get { Fresh(); return _visibleNotes.ToArray(); } }

    /// <summary>The folders whose rows are shown, in order.</summary>
    public IReadOnlyList<string> VisibleFolderPaths { get { Fresh(); return _visibleFolders.ToArray(); } }

    /// <summary>On a window at least 900 px wide Notas is a 360 px panel beside the city (theme.css @media) and has Fechar.</summary>
    public bool Wide
    {
        get => _wide;
        set
        {
            _wide = value;
            Width = value ? 360 : double.NaN;
            HorizontalAlignment = value ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line);
            BorderThickness = new Thickness(0, 0, value ? 1 : 0, 0);
            BoxShadow = value ? BoxShadows.Parse("0 18 60 0 #8C000000") : default;
            _close.IsVisible = value;
        }
    }

    /// <summary>
    /// The notes of the vault (documents) and the folders the tree shows even without notes (model.folders: the
    /// neighbourhoods' paths, as loadFolders reads them from mapa.json regioes). The selection is cleared; the open
    /// folders stay open, as the 1.8.4 keeps them.
    /// </summary>
    public void SetNotes(IEnumerable<CityNote> notes, IEnumerable<string>? folders = null)
    {
        ArgumentNullException.ThrowIfNull(notes);
        _notes.Clear();
        _notes.AddRange(notes.Where(n => !string.IsNullOrWhiteSpace(n.Path)));
        _extraFolders.Clear();
        foreach (var f in folders ?? []) if (!string.IsNullOrEmpty(f)) _extraFolders.Add(f);
        _selected.Clear();
        Render();
    }

    /// <summary>
    /// The files of the vault that are not notes (images, PDFs, other attachments). The 1.8.4 listed only the notes;
    /// the owner asked for every file with its extension (DEC-0045, UC-38).
    /// </summary>
    public void SetFiles(IEnumerable<string> paths)
    {
        _files.Clear();
        _files.AddRange(paths.Where(p => !string.IsNullOrWhiteSpace(p) && _notes.TrueForAll(n => n.Path != p)).Distinct(StringComparer.Ordinal));
        Render();
    }

    /// <summary>The non-note file rows shown now.</summary>
    public IReadOnlyList<string> VisibleFilePaths { get { Fresh(); return _visibleFiles.ToArray(); } }

    private readonly List<string> _visibleFiles = [];

    /// <summary>The icon of a file by its extension: notes and texts, pages, images, code and data, other files.</summary>
    public static string IconFor(string path)
    {
        var p = path.ToLowerInvariant();
        if (p.EndsWith(".page.json", StringComparison.Ordinal) || p.EndsWith(".template.json", StringComparison.Ordinal)) return UrbeTheme.Icons.Page;
        var ext = System.IO.Path.GetExtension(p);
        return ext switch
        {
            ".md" or ".markdown" or ".txt" => UrbeTheme.Icons.File,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".svg" or ".bmp" or ".avif" => UrbeTheme.Icons.Image,
            ".pdf" => UrbeTheme.Icons.Book,
            ".json" or ".js" or ".css" or ".html" or ".htm" or ".yaml" or ".yml" or ".csv" or ".xml" or ".cs" or ".py" => UrbeTheme.Icons.FileCode,
            _ => UrbeTheme.Icons.FileOther
        };
    }

    /// <summary>A tap on a visible non-note file row.</summary>
    public bool TryOpenFile(string path)
    {
        Fresh();
        if (!_visibleFiles.Contains(path)) return false;
        FileRequested?.Invoke(path);
        return true;
    }

    /// <summary>Holding a non-note file (menuEntradaExplorer: Visualizar, Renomear, Mover, Excluir).</summary>
    public event Action<string>? FileMenuRequested;

    /// <summary>A hold on a visible non-note file row.</summary>
    public bool HoldFile(string path)
    {
        Fresh();
        if (!_visibleFiles.Contains(path)) return false;
        FileMenuRequested?.Invoke(path);
        return true;
    }

    /// <summary>The "⋯" of a visible folder row.</summary>
    public bool OpenFolderMenu(string folder)
    {
        Fresh();
        if (!_visibleFolders.Contains(folder)) return false;
        FolderMenuRequested?.Invoke(folder);
        return true;
    }

    /// <summary>
    /// Drops the selected notes on <paramref name="folder"/> (what a drag onto a folder row does): only notes move,
    /// and only those not already there. False when nothing would move.
    /// </summary>
    public bool DropSelectionOn(string folder)
    {
        var notes = _selected.Where(x => !x.StartsWith(FolderPrefix, StringComparison.Ordinal) && Dir(x) != folder).ToList();
        if (notes.Count == 0) return false;
        MoveRequested?.Invoke(notes, folder);
        return true;
    }

    /// <summary>model.clear().</summary>
    public void ClearSelection()
    {
        _selected.Clear();
        Render();
    }

    /// <summary>isExpanded(path).</summary>
    public bool IsExpanded(string folder) => State?.IsExpanded(folder) ?? _localExpanded.Contains(folder);

    private void Expand(string folder, bool value)
    {
        if (State is { } s) s.Expand(folder, value);
        else if (value) _localExpanded.Add(folder);
        else _localExpanded.Remove(folder);
    }

    /// <summary>A tap on a visible folder row (activate): toggles it in the selection while one exists, otherwise opens or
    /// closes it in the tree.</summary>
    public bool TapFolder(string folder)
    {
        Fresh();
        if (!_visibleFolders.Contains(folder)) return false;
        if (_selected.Count > 0) { Toggle(FolderPrefix + folder); return true; }
        Expand(folder, !IsExpanded(folder));
        Render();
        return true;
    }

    /// <summary>Opens a visible folder in the tree (or, while selecting, toggles it, as a tap does); false when it is not
    /// shown.</summary>
    public bool TryEnterFolder(string folder)
    {
        Fresh();
        if (!_visibleFolders.Contains(folder)) return false;
        if (_selected.Count > 0) { Toggle(FolderPrefix + folder); return true; }
        if (!IsExpanded(folder)) { Expand(folder, true); Render(); }
        return true;
    }

    /// <summary>The hold of a visible folder row: model.select('folder:' + path, 'toggle').</summary>
    public bool HoldFolder(string folder)
    {
        Fresh();
        if (!_visibleFolders.Contains(folder)) return false;
        Toggle(FolderPrefix + folder);
        return true;
    }

    /// <summary>A tap on a visible note row (activate): toggles it while a selection exists, otherwise opens it.</summary>
    public bool TryOpenNote(string path)
    {
        Fresh();
        if (!_visibleNotes.Contains(path)) return false;
        if (_selected.Count > 0) { Toggle(path); return true; }
        if (_notes.Find(n => n.Path == path) is { } note) NoteRequested?.Invoke(note);
        return true;
    }

    /// <summary>The hold of a visible note row (what the gesture raises): model.select(id,'toggle').</summary>
    public bool HoldNote(string path)
    {
        Fresh();
        if (!_visibleNotes.Contains(path)) return false;
        Toggle(path);
        return true;
    }

    /// <summary>model.select(id,'toggle').</summary>
    private void Toggle(string id)
    {
        if (!_selected.Remove(id)) _selected.Add(id);
        Render();
    }

    private void Show(string view)
    {
        _view = view;
        if (view != "trash") _trash = null;
        if (view != "collection") _collection = null;
        Render();
    }

    // ------------------------------------------------------------------ render

    private bool _stale;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible && _stale) Render();
    }

    /// <summary>The list as it is now, even while hidden (what callers read of it is never out of date).</summary>
    private void Fresh()
    {
        if (_stale) Render(force: true);
    }

    private void Render(bool force = false)
    {
        // hidden: the list is rebuilt when it shows again, not on every save of a note behind the city (UC-48)
        if (!IsVisible && !force) { _stale = true; return; }
        _stale = false;
        // setHeader: the title of the view; back outside tree/search; the search field only in tree/search
        _title.Text = _view switch { "trash" => "Lixeira", "search" => "Busca", "collection" => _collection?.Title ?? "Explorer", _ => "Explorer" };
        _back.IsVisible = _view is "trash" or "collection";
        _searchBox.IsVisible = _view is "tree" or "search";
        _list.Children.Clear();
        _visibleNotes.Clear();
        _visibleFolders.Clear();
        _visibleFiles.Clear();
        _folderRows.Clear();
        switch (_view)
        {
            case "trash": RenderTrash(_trash ?? []); break;
            case "collection" when _collection is { } c: RenderCollection(c); break;
            case "search": RenderSearch(); break;
            default: RenderTree(); break;
        }
        RenderBar();
    }

    /// <summary>model.tree(): folders (model.folders and the folders of the documents) and documents; in each folder the
    /// folders first, then by name (localeCompare); flatten() shows the children of the open folders.</summary>
    private void RenderTree()
    {
        var folders = new HashSet<string>(StringComparer.Ordinal);
        void Ensure(string path) { for (var f = path; f.Length > 0 && folders.Add(f); f = Dir(f)) { } }
        foreach (var f in _extraFolders) Ensure(f);
        foreach (var n in _notes) Ensure(Dir(n.Path));
        foreach (var f in _files) Ensure(Dir(f));
        // what is in each folder, gathered once (it was searched again for every folder: slow with many notes — UC-48)
        var inside = new Dictionary<string, List<(int Kind, string Path, string Name)>>(StringComparer.Ordinal);
        void Put(string parent, (int, string, string) item)
        {
            if (!inside.TryGetValue(parent, out var list)) inside[parent] = list = [];
            list.Add(item);
        }
        foreach (var f in folders) Put(Dir(f), (0, f, Base(f)));
        var notesByPath = new Dictionary<string, CityNote>(StringComparer.Ordinal);
        foreach (var n in _notes) { notesByPath.TryAdd(n.Path, n); Put(Dir(n.Path), (1, n.Path, Base(n.Path))); }
        foreach (var f in _files) Put(Dir(f), (2, f, Base(f)));
        void Walk(string parent, int depth)
        {
            if (!inside.TryGetValue(parent, out var here)) return;
            // folders first, then every file by its name with the extension (notes and attachments together)
            var kids = here.OrderBy(k => k.Kind == 0 ? 0 : 1).ThenBy(k => k.Name, Locale).ToList();
            foreach (var k in kids)
            {
                if (k.Kind == 0)
                {
                    bool open = IsExpanded(k.Path);
                    _visibleFolders.Add(k.Path);
                    var folder = k.Path;
                    var row = FolderRow(folder, k.Name, depth, open);
                    _folderRows[row] = folder;
                    _list.Children.Add(row);
                    if (open) Walk(k.Path, depth + 1);
                }
                else if (k.Kind == 1) AddNoteRow(notesByPath[k.Path], depth, withPath: false);
                else AddFileRow(k.Path, depth, withPath: false);
            }
        }
        Walk("", 0);
        if (_list.Children.Count == 0)
            _list.Children.Add(EmptyWithAction("Nenhuma nota ainda", "Cada nota vira uma casa na sua cidade.", "Criar primeira nota"));
    }

    /// <summary>renderSearch: documents whose path or title holds the query (lower case), at most 200, with their folder.</summary>
    private void RenderSearch()
    {
        var query = (_search.Text ?? "").Trim();
        var q = query.ToLowerInvariant();
        foreach (var n in _notes.Where(n => n.Path.ToLowerInvariant().Contains(q, StringComparison.Ordinal)
                     || (n.Title ?? "").ToLowerInvariant().Contains(q, StringComparison.Ordinal)).Take(200))
            AddNoteRow(n, 0, withPath: true);
        foreach (var f in _files.Where(f => f.ToLowerInvariant().Contains(q, StringComparison.Ordinal)).Take(200))
            AddFileRow(f, 0, withPath: true);
        if (_list.Children.Count == 0) _list.Children.Add(Empty("Nada encontrado", "Nenhum arquivo com “" + query + "”."));
    }

    private void RenderCollection(Collection c)
    {
        foreach (var n in c.Notes) AddNoteRow(n, 0, withPath: true);
        if (_list.Children.Count == 0) _list.Children.Add(Empty(c.EmptyTitle, c.EmptyText));
    }

    private void AddNoteRow(CityNote n, int depth, bool withPath, string? name = null)
    {
        _visibleNotes.Add(n.Path);
        var path = n.Path;
        // the file name with its extension, as an explorer shows it (the 1.8.4 showed the title)
        _list.Children.Add(PressableRow(RowFrame(IconFor(path), name ?? Base(path), withPath && path.Contains('/') ? Dir(path) : null, false, false, null),
            depth, _selected.Contains(path), () => TryOpenNote(path), () => { if (HoldNote(path)) ArmDrag(); }, () => HoldDelay));
    }

    private void AddFileRow(string path, int depth, bool withPath)
    {
        _visibleFiles.Add(path);
        _list.Children.Add(Row(IconFor(path), Base(path), withPath && path.Contains('/') ? Dir(path) : null, depth, false, false, false,
            () => TryOpenFile(path), () => HoldFile(path)));
    }

    /// <summary>A folder row: the caret and a "⋯" with the folder's options (the 1.8.4 showed none, DEC-0045).</summary>
    private Control FolderRow(string folder, string name, int depth, bool open)
    {
        var more = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.MoreUi, 18, UrbeTheme.Text3, 1.8),
            Width = 36, Height = 36, Padding = new Thickness(0), CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(more, "Opções da pasta");
        more.Click += (_, e) => { e.Handled = true; OpenFolderMenu(folder); };
        var caret = UrbeTheme.Icon(UrbeTheme.Icons.Chevron, 16, UrbeTheme.Text3, 1.8);
        caret.VerticalAlignment = VerticalAlignment.Center;
        caret.RenderTransform = open ? new RotateTransform(90) : null;
        var trailing = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { more, caret } };
        return PressableRow(RowFrame(UrbeTheme.Icons.Folder, name, null, true, open, trailing), depth, _selected.Contains(FolderPrefix + folder),
            () => TapFolder(folder), () => HoldFolder(folder), () => HoldDelay);
    }

    // A held note can be dragged onto a folder row (the 1.8.4 did nothing, DEC-0045, UC-38). The hold re-renders the list,
    // so the drag is followed here, on the whole explorer, not on the row that was pressed.
    private bool _dragArmed, _dragging;
    private Point? _dragFrom;

    private void ArmDrag()
    {
        _dragArmed = true;
        _dragging = false;
        _dragFrom = null;
    }

    private void OnDragMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (!_dragArmed) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { EndDrag(); return; }
        var at = e.GetPosition(_list);
        _dragFrom ??= at;
        if (!_dragging && Math.Sqrt(Math.Pow(at.X - _dragFrom.Value.X, 2) + Math.Pow(at.Y - _dragFrom.Value.Y, 2)) <= 12) return;
        _dragging = true;
        Highlight(FolderRowAt(at));
    }

    private void OnDragReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (_dragging && FolderRowAt(e.GetPosition(_list)) is { } row && _folderRows.TryGetValue(row, out var folder))
        {
            EndDrag();
            DropSelectionOn(folder);
            return;
        }
        EndDrag();
    }

    private void EndDrag()
    {
        _dragArmed = _dragging = false;
        _dragFrom = null;
        Highlight(null);
    }

    private Control? FolderRowAt(Point at)
    {
        foreach (var (row, _) in _folderRows)
            if (row.Bounds.Contains(at)) return row;
        return null;
    }

    private void Highlight(Control? row)
    {
        if (_dropTarget is Button old && old != row)
            old.BorderBrush = _folderRows.TryGetValue(old, out var f) && _selected.Contains(FolderPrefix + f) ? UrbeTheme.Brush(UrbeTheme.Accent, .35) : Brushes.Transparent;
        _dropTarget = row;
        if (row is Button target) target.BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent);
    }

    /// <summary>Whether a held note is being dragged now (tests).</summary>
    public bool Dragging => _dragging;

    /// <summary>renderTrash: "n item(ns) · restaure ou exclua de vez" with Esvaziar, then each item with Restaurar and ✕.</summary>
    private void RenderTrash(IReadOnlyList<TrashEntry> items)
    {
        if (items.Count == 0)
        {
            _list.Children.Add(Empty("A lixeira está vazia", "Notas excluídas ficam aqui e podem ser restauradas."));
            return;
        }
        // .ume-trash-head: padding 6px 12px 10px, 13px text-3; Esvaziar .ui-btn 36px, danger
        var empty = SmallButton(UrbeTheme.Icons.Trash, "Esvaziar", UrbeTheme.Danger);
        empty.Click += (_, _) => EmptyTrashRequested?.Invoke();
        var head = new DockPanel { Margin = new Thickness(12, 6, 12, 10) };
        DockPanel.SetDock(empty, Dock.Right);
        head.Children.Add(empty);
        head.Children.Add(new TextBlock { Text = items.Count + " item(ns) · restaure ou exclua de vez", FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center });
        _list.Children.Add(head);
        foreach (var item in items)
        {
            var entry = item;
            var restore = SmallButton(UrbeTheme.Icons.Restore, "Restaurar", UrbeTheme.Text);
            restore.Click += (_, _) => RestoreRequested?.Invoke(entry.Id);
            var purge = new Button
            {
                Content = UrbeTheme.Icon(UrbeTheme.Icons.CloseUi, 17, UrbeTheme.Text3, 1.8),
                Width = 36,
                Height = 36,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(purge, "Excluir de vez");
            purge.Click += (_, _) => PurgeRequested?.Invoke(entry.Id);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { restore, purge } };
            _list.Children.Add(new Border
            {
                MinHeight = 48,
                Padding = RowPadding(0),
                CornerRadius = new CornerRadius(12),
                Child = RowFrame(UrbeTheme.Icons.File, entry.Title, entry.OriginalPath, false, false, actions)
            });
        }
    }

    private static Control Empty(string title, string text) => new StackPanel
    {
        // .ume-empty: padding 56px 24px, centred, 15px text-3; strong 17px 600 text, 6px below
        Margin = new Thickness(24, 56),
        HorizontalAlignment = HorizontalAlignment.Center,
        Children =
        {
            new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 6), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
            new TextBlock { Text = text, FontSize = 15, LineHeight = 22.5, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }
        }
    };

    private Control EmptyWithAction(string title, string text, string action)
    {
        var panel = (StackPanel)Empty(title, text);
        // .ui-btn.ui-btn-primary, 18px above
        var create = new Button
        {
            Content = new TextBlock { Text = action, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk) },
            MinHeight = 48,
            Padding = new Thickness(18, 0),
            Margin = new Thickness(0, 18, 0, 0),
            CornerRadius = new CornerRadius(12),
            Background = UrbeTheme.Brush(UrbeTheme.Accent),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        create.Click += (_, _) => CreateFirstNoteRequested?.Invoke();
        panel.Children.Add(create);
        return panel;
    }

    /// <summary>A .ui-btn of the Lixeira (36px, padding 0 12px, 13px 600, a 17px icon, surface-3 with a line-2 border).</summary>
    private static Button SmallButton(string icon, string label, string color) => new()
    {
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children =
            {
                UrbeTheme.Icon(icon, 17, color, 1.8),
                new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(color), VerticalAlignment = VerticalAlignment.Center }
            }
        },
        MinHeight = 36,
        Padding = new Thickness(12, 0),
        CornerRadius = new CornerRadius(12),
        Background = UrbeTheme.Brush(UrbeTheme.Surface3),
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
        BorderThickness = new Thickness(1),
        VerticalAlignment = VerticalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    private static Button HeaderButton(string icon, string tip)
    {
        // header button: 44×44, radius 12, text-2, a 22px .ui-icon
        var b = new Button
        {
            Content = UrbeTheme.Icon(icon, 22, UrbeTheme.Text2, 1.8),
            Width = 44,
            Height = 44,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tip);
        return b;
    }

    /// <summary>The look of a .ume-row: 48px tall, columns 28px | name | caret or actions with a 10px gap, padding
    /// 6px 12px 6px (12 + depth·18)px, radius 12; the icon 20px text-3 (a folder's in the accent), the name 15px 500, the
    /// folder 12px text-3 under it; selected: accent-soft with an accent 35 % line.</summary>
    private static Grid RowFrame(string icon, string name, string? small, bool folder, bool open, Control? trailing)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("28,*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var ico = UrbeTheme.Icon(icon, 20, folder ? UrbeTheme.Accent : UrbeTheme.Text3, 1.8);
        ico.VerticalAlignment = VerticalAlignment.Center;
        ico.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetRowSpan(ico, 2);
        grid.Children.Add(ico);
        var label = new TextBlock
        {
            Text = name,
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            LineHeight = 19.5,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(10, 0),
            VerticalAlignment = small is null ? VerticalAlignment.Center : VerticalAlignment.Bottom
        };
        Grid.SetColumn(label, 1);
        if (small is null) Grid.SetRowSpan(label, 2);
        grid.Children.Add(label);
        if (small is not null)
        {
            var sub = new TextBlock
            {
                Text = small,
                FontSize = 12,
                LineHeight = 15.6,
                Foreground = UrbeTheme.Brush(UrbeTheme.Text3),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(10, 0)
            };
            Grid.SetColumn(sub, 1);
            Grid.SetRow(sub, 1);
            grid.Children.Add(sub);
        }
        if (folder && trailing is null)
        {
            // .ume-caret: 16px chevron, text-3, turned 90° when the folder is open
            var caret = UrbeTheme.Icon(UrbeTheme.Icons.Chevron, 16, UrbeTheme.Text3, 1.8);
            caret.VerticalAlignment = VerticalAlignment.Center;
            caret.RenderTransform = open ? new RotateTransform(90) : null;
            trailing = caret;
        }
        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 2);
            Grid.SetRowSpan(trailing, 2);
            grid.Children.Add(trailing);
        }
        return grid;
    }

    /// <summary>The padding of a .ume-row at that depth: 6px 12px 6px (12 + depth·18)px.</summary>
    private static Thickness RowPadding(int depth) => new(12 + depth * 18, 6, 12, 6);

    private Control Row(string icon, string name, string? small, int depth, bool folder, bool open, bool selected, Action click, Action hold) =>
        PressableRow(RowFrame(icon, name, small, folder, open, null), depth, selected, click, hold, () => HoldDelay);

    private static Control PressableRow(Grid frame, int depth, bool selected, Action click, Action hold, Func<TimeSpan> holdDelay)
    {
        var button = new Button
        {
            Content = frame,
            Background = selected ? UrbeTheme.AccentSoft : Brushes.Transparent,
            BorderBrush = selected ? UrbeTheme.Brush(UrbeTheme.Accent, .35) : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = RowPadding(depth),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = 48
        };
        // the hold: 430 ms without moving more than 12 px selects, and the click that follows opens nothing
        bool held = false;
        Avalonia.Threading.DispatcherTimer? timer = null;
        Point start = default;
        button.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(button).Properties.IsRightButtonPressed) return;
            held = false;
            start = e.GetPosition(button);
            timer?.Stop();
            timer = new Avalonia.Threading.DispatcherTimer { Interval = holdDelay() };
            timer.Tick += (_, _) => { timer!.Stop(); timer = null; held = true; hold(); };
            timer.Start();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        button.AddHandler(PointerMovedEvent, (_, e) =>
        {
            var p = e.GetPosition(button);
            if (timer is not null && Math.Sqrt(Math.Pow(p.X - start.X, 2) + Math.Pow(p.Y - start.Y, 2)) > 12) { timer.Stop(); timer = null; }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        button.AddHandler(PointerReleasedEvent, (_, _) => { timer?.Stop(); timer = null; },
            Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        button.Click += (_, _) =>
        {
            if (held) { held = false; return; }
            click();
        };
        return button;
    }

    // ------------------------------------------------------------------ selection bar

    /// <summary>The .ume-actions bar, floating 12px from the sides and the bottom (60px, padding 6px, gap 2px, radius 16):
    /// Limpar and the count on the left, then Abrir, Favoritar, Mover para pasta, Duplicar and Mover para a lixeira on the
    /// right (38×44 transparent buttons, 20px icons in text-2, the trash in danger; disabled at 30 %). Compor joins with
    /// the compositions.</summary>
    private Control SelectionBar()
    {
        Button Act(string icon, string tip, string? action, string color)
        {
            var b = new Button
            {
                Content = UrbeTheme.Icon(icon, 20, color, 1.8),
                Width = 38,
                MinHeight = 44,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            b.Resources["ButtonBackgroundDisabled"] = Brushes.Transparent;
            b.PropertyChanged += (_, e) => { if (e.Property == IsEnabledProperty) b.Opacity = b.IsEnabled ? 1 : .3; };
            ToolTip.SetTip(b, tip);
            if (action is null) return b;
            b.Click += (_, _) => { if (_selected.Count > 0) SelectionAction?.Invoke(action, _selected.ToList()); };
            // renderBar: the actions that only apply to notes are disabled when only folders are selected
            if (action != "delete") _noteActions.Add(b);
            return b;
        }
        var clear = Act(UrbeTheme.Icons.CloseUi, "Limpar seleção", null, UrbeTheme.Text2);
        clear.Click += (_, _) => ClearSelection();
        _count.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _count.Margin = new Thickness(6, 0);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children =
            {
                Act(UrbeTheme.Icons.Open, "Abrir", "open", UrbeTheme.Text2),
                Act(UrbeTheme.Icons.Star, "Favoritar", "favorite", UrbeTheme.Text2),
                Act(UrbeTheme.Icons.Move, "Mover para pasta", "move", UrbeTheme.Text2),
                Act(UrbeTheme.Icons.Copy, "Duplicar", "duplicate", UrbeTheme.Text2),
                Act(UrbeTheme.Icons.Folder, "Agrupar em nova pasta", "group", UrbeTheme.Text2),
                Act(UrbeTheme.Icons.Trash, "Mover para a lixeira", "delete", UrbeTheme.Danger)
            }
        };
        var row = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(clear, Dock.Left);
        DockPanel.SetDock(_count, Dock.Left);
        DockPanel.SetDock(actions, Dock.Right);
        row.Children.Add(clear);
        row.Children.Add(_count);
        row.Children.Add(actions);
        clear.Margin = new Thickness(0, 0, 2, 0);
        _bar.Child = row;
        _bar.Background = UrbeTheme.Brush(UrbeTheme.Surface2, .97);
        _bar.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _bar.BorderThickness = new Thickness(1);
        _bar.CornerRadius = new CornerRadius(16);
        _bar.BoxShadow = BoxShadows.Parse("0 18 60 0 #8C000000");
        _bar.Padding = new Thickness(6);
        _bar.MinHeight = 60;
        _bar.Margin = new Thickness(12, 0, 12, 12);
        _bar.VerticalAlignment = VerticalAlignment.Bottom;
        _bar.IsVisible = false;
        return _bar;
    }

    /// <summary>renderBar: shown while something is selected (never in the Lixeira); "1 pasta · 2 notas".</summary>
    private void RenderBar()
    {
        _bar.IsVisible = _selected.Count > 0 && _view != "trash";
        int f = _selected.Count(x => x.StartsWith(FolderPrefix, StringComparison.Ordinal)), d = _selected.Count - f;
        static string Plural(int n, string one, string many) => n + " " + (n == 1 ? one : many);
        _count.Text = string.Join(" · ", new[] { f > 0 ? Plural(f, "pasta", "pastas") : "", d > 0 ? Plural(d, "nota", "notas") : "" }.Where(x => x.Length > 0));
        foreach (var b in _noteActions) b.IsEnabled = d > 0;
    }

    private static string Dir(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
    private static string Base(string path) => path[(path.LastIndexOf('/') + 1)..];
}
