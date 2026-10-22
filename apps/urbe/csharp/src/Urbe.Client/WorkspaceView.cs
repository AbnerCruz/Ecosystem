using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// UC-84 (DEC-0053, ADD-0030): the Área de trabalho, the base of the app. Each project (a top folder) is a glass box
/// with its colour's border: what is inside shows through, as a grid of its most recent notes and files, with its
/// subfolders as smaller boxes. A box opens into a catalogue of its folder, with a way back and a search; a card
/// opens its note or file. The root's search looks in every project, grouped by box. Calm: no motion beyond a tap's,
/// and only what fits the screen is built (a box shows a few cards; an open folder, a page of them at a time).
/// </summary>
public sealed class WorkspaceView : Grid
{
    /// <summary>How many cards an open folder shows at a time (Mostrar mais brings the next ones).</summary>
    public const int Page = 60;

    private readonly ScrollViewer _scroll;
    private readonly StackPanel _body = new() { Spacing = 16, Margin = new Thickness(16, 8, 16, 24) };
    private readonly StackPanel _crumbs = new() { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBox _search;
    private readonly Button _sidebar;
    private WorkspaceIndex _index = new(Array.Empty<WorkspaceNote>());
    private string _title = "Área de trabalho";
    private int _shown = Page;
    private double _laidOutFor;
    private DispatcherTimer? _searchDelay;

    public WorkspaceView()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*");

        _sidebar = HeaderButton(UrbeTheme.Icons.List, "Pastas");
        _sidebar.Click += (_, _) => SidebarRequested?.Invoke();
        var add = HeaderButton(UrbeTheme.Icons.Plus, "Novo projeto");
        add.Click += (_, _) => { if (Folder.Length == 0) NewProjectRequested?.Invoke(); else NewNoteRequested?.Invoke(Folder); };
        _add = add;
        _settings = HeaderButton(UrbeTheme.Icons.Settings, "Configurações");
        _settings.Click += (_, _) => SettingsRequested?.Invoke();
        var head = new DockPanel { Margin = new Thickness(8, 6, 8, 0), Height = 52, LastChildFill = true };
        DockPanel.SetDock(_sidebar, Dock.Left);
        DockPanel.SetDock(_settings, Dock.Right);
        DockPanel.SetDock(add, Dock.Right);
        head.Children.Add(_sidebar);
        head.Children.Add(_settings);
        head.Children.Add(add);
        head.Children.Add(new ScrollViewer { Content = _crumbs, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(4, 0) });
        Children.Add(head);

        _search = new TextBox
        {
            PlaceholderText = "Buscar em todos os projetos", FontSize = 15, MinHeight = 44, CornerRadius = new CornerRadius(14),
            Padding = new Thickness(40, 10, 12, 10), VerticalContentAlignment = VerticalAlignment.Center,
            Background = UrbeTheme.Brush(UrbeTheme.Surface2), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2)
        };
        AutomationProperties.SetName(_search, "Buscar na Área de trabalho");
        _search.TextChanged += (_, _) => SearchSoon();
        var searchIcon = UrbeTheme.Icon(UrbeTheme.Icons.Search, 18, UrbeTheme.Text3, 1.8);
        searchIcon.Margin = new Thickness(14, 0, 0, 0);
        searchIcon.HorizontalAlignment = HorizontalAlignment.Left;
        searchIcon.VerticalAlignment = VerticalAlignment.Center;
        searchIcon.IsHitTestVisible = false;
        var searchRow = new Grid { Margin = new Thickness(16, 4, 16, 6), Children = { _search, searchIcon } };
        SetRow(searchRow, 1);
        Children.Add(searchRow);

        var tools = new ScrollViewer
        {
            Content = _tools, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(16, 0, 16, 6)
        };
        SetRow(tools, 2);
        Children.Add(tools);

        _scroll = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        TouchIntent.Attach(_scroll);
        SetRow(_scroll, 3);
        Children.Add(_scroll);
        SizeChanged += (_, e) =>
        {
            // the number of columns follows the width; nothing else depends on it
            if (Columns(e.NewSize.Width) != Columns(_laidOutFor)) Render();
        };
    }

    private readonly Button _add, _settings;
    private readonly StackPanel _tools = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    private WorkspaceItemKind? _kind;
    private string? _tag;
    private WorkspaceOrder _order;
    private bool _asList;

    /// <summary>Only items of this kind (null: all), in the open box and its subfolders, or everywhere at the root.</summary>
    public WorkspaceItemKind? KindFilter { get => _kind; set { _kind = value; _shown = Page; Render(); } }

    /// <summary>Only items with this tag (null: any).</summary>
    public string? TagFilter { get => _tag; set { _tag = value; _shown = Page; Render(); } }

    /// <summary>How the items are ordered (kept for the open box, UC-85).</summary>
    public WorkspaceOrder Order { get => _order; set { _order = value; Render(); if (Folder.Length > 0) PresentationChanged?.Invoke(Folder); } }

    /// <summary>Items as a list of rows instead of a grid of cards (kept for the open box, UC-85).</summary>
    public bool AsList { get => _asList; set { _asList = value; Render(); if (Folder.Length > 0) PresentationChanged?.Invoke(Folder); } }

    /// <summary>The open box's order or grid/list changed.</summary>
    public event Action<string>? PresentationChanged;

    private AreaLayout _layout = new();

    /// <summary>How the boxes look and are ordered (UC-85, .urbe/area.json).</summary>
    public AreaLayout Layout { get => _layout; set { _layout = value ?? new(); Render(); } }

    /// <summary>The projects as the root shows them (their stored order first).</summary>
    public List<string> ProjectOrder => [.. _layout.Arrange(_index.Projects, p => p.Folder).Select(p => p.Folder)];

    private int ColorOf(string folder) => _layout.ColorOf(folder);

    /// <summary>The icon a box shows: its own (UC-85), else a folder.</summary>
    private string BoxIcon(string folder) => IconFor(_layout.Of(folder)?.Icon);

    /// <summary>The drawing of one of <see cref="AreaLayout.Icons"/>.</summary>
    public static string IconFor(string? id) => id switch
    {
        "livro" => UrbeTheme.Icons.Book, "estrela" => UrbeTheme.Icons.Star, "flor" => UrbeTheme.Icons.Flower, "casa" => UrbeTheme.Icons.House,
        "lista" => UrbeTheme.Icons.List, "imagem" => UrbeTheme.Icons.Image, "pagina" => UrbeTheme.Icons.Page, _ => UrbeTheme.Icons.Folder
    };

    /// <summary>A note's card was tapped.</summary>
    public event Action<string>? NoteRequested;
    /// <summary>A file's card was tapped (a page, an image, a PDF…).</summary>
    public event Action<string>? FileRequested;
    /// <summary>A card was held (or right-clicked): its information and actions.</summary>
    public event Action<string>? ItemHeld;
    /// <summary>UC-84 fatia 4: a card dragged with the mouse onto a box: the item and the box's folder ("" = the root).</summary>
    public event Action<string, string>? ItemDropped;
    /// <summary>UC-84 fatia 4 / UC-85: a box's head dragged onto another box: the project and the one it goes before.</summary>
    public event Action<string, string>? ProjectDropped;
    /// <summary>"+ Novo projeto": a new top folder.</summary>
    public event Action? NewProjectRequested;
    /// <summary>"+" inside a folder: a new note there.</summary>
    public event Action<string>? NewNoteRequested;
    /// <summary>A box's "⋯": the folder's options.</summary>
    public event Action<string>? FolderMenuRequested;
    /// <summary>The header's Pastas: the tree (a drawer on a phone).</summary>
    public event Action? SidebarRequested;
    /// <summary>The header's Configurações (on a phone, where the sidebar with its own is not beside it).</summary>
    public event Action? SettingsRequested;

    /// <summary>The folder open now ("" for the root, the projects).</summary>
    public string Folder { get; private set; } = "";

    /// <summary>The header's Pastas button: only where the tree is not already beside the Área de trabalho.</summary>
    public bool ShowsSidebarButton { get => _sidebar.IsVisible; set => _sidebar.IsVisible = value; }

    /// <summary>The header's Configurações: where the sidebar, which has its own, is not shown beside it.</summary>
    public bool ShowsSettingsButton { get => _settings.IsVisible; set => _settings.IsVisible = value; }

    /// <summary>The name shown for the root (the vault's).</summary>
    public string Title { get => _title; set { _title = string.IsNullOrWhiteSpace(value) ? "Área de trabalho" : value; Render(); } }

    /// <summary>The vault is being read: an empty Área de trabalho says so instead of inviting to a first project.</summary>
    public bool Opening
    {
        get => _opening;
        set { if (_opening == value) return; _opening = value; Render(); }
    }

    private bool _opening;

    /// <summary>The search box (tests type in it).</summary>
    public TextBox Search => _search;

    /// <summary>What the Área de trabalho shows: the vault's notes and files, as they are now.</summary>
    public WorkspaceIndex Index => _index;

    /// <summary>The vault changed: the same folder stays open if it still exists, else the nearest one that does.</summary>
    public void SetIndex(WorkspaceIndex index)
    {
        _index = index;
        var folder = Folder;
        while (folder.Length > 0 && !Exists(folder)) folder = NoteLibrary.FolderOf(folder);
        Folder = folder;
        Render();
    }

    /// <summary>Opens a folder's box ("" for the root).</summary>
    public void Open(string folder)
    {
        Folder = folder;
        _shown = Page;
        _kind = null;
        _tag = null;
        // each box keeps its order and its grid or list (UC-85); the root starts from the defaults
        _order = _layout.Of(folder)?.Order ?? WorkspaceOrder.Recent;
        _asList = _layout.Of(folder)?.AsList ?? false;
        _search.Text = "";
        Render();
        _scroll.Offset = default;
    }

    private bool Exists(string folder) =>
        _index.Children(NoteLibrary.FolderOf(folder)).Contains(folder) || _index.Items(folder).Count > 0;

    // ------------------------------------------------------------------ rendering

    private static int Columns(double width) => width <= 0 ? 1 : Math.Clamp((int)((width - 32 + 16) / (380 + 16)), 1, 4);

    private void SearchSoon()
    {
        _searchDelay?.Stop();
        _searchDelay = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background, (_, _) => { _searchDelay?.Stop(); Render(); });
        _searchDelay.Start();
    }

    /// <summary>Builds the screen for the folder and the search now.</summary>
    public void Render()
    {
        _laidOutFor = Bounds.Width;
        RenderCrumbs();
        _search.PlaceholderText = Folder.Length == 0 ? "Buscar em todos os projetos" : "Buscar em " + _index.Box(Folder).Name;
        AutomationProperties.SetName(_add, Folder.Length == 0 ? "Novo projeto" : "Nova nota aqui");
        ToolTip.SetTip(_add, Folder.Length == 0 ? "Novo projeto" : "Nova nota aqui");
        _body.Children.Clear();
        var query = (_search.Text ?? "").Trim();
        RenderTools(query);
        if (query.Length > 0 || _kind is not null || _tag is not null) RenderMatches(query);
        else if (Folder.Length == 0) RenderRoot();
        else RenderFolder();
    }

    private static readonly (WorkspaceItemKind? Kind, string Label)[] Kinds =
    [
        (null, "Tudo"), (WorkspaceItemKind.Note, "Notas"), (WorkspaceItemKind.Page, "Páginas"), (WorkspaceItemKind.Image, "Imagens"),
        (WorkspaceItemKind.Pdf, "PDF"), (WorkspaceItemKind.Media, "Mídia"), (WorkspaceItemKind.Other, "Outros")
    ];

    /// <summary>The row under the search (UC-84 fatia 3): kinds and tags present here, the order and grid or list.</summary>
    private void RenderTools(string query)
    {
        _tools.Children.Clear();
        var scope = Folder.Length == 0 ? _index.Recent("", int.MaxValue) : _index.Recent(Folder, int.MaxValue);
        var present = scope.Select(i => i.Kind).ToHashSet();
        foreach (var (kind, label) in Kinds)
            if (kind is null || present.Contains(kind.Value))
                _tools.Children.Add(Chip(label, _kind == kind, () => KindFilter = kind));
        foreach (var tag in _index.TagsIn(Folder).Take(6))
            _tools.Children.Add(Chip("#" + tag, string.Equals(_tag, tag, StringComparison.OrdinalIgnoreCase), () => TagFilter = string.Equals(_tag, tag, StringComparison.OrdinalIgnoreCase) ? null : tag));
        var order = Chip(_order switch { WorkspaceOrder.Name => "Ordem: nome", WorkspaceOrder.Created => "Ordem: criação", _ => "Ordem: recentes" }, false,
            () => Order = _order switch { WorkspaceOrder.Recent => WorkspaceOrder.Name, WorkspaceOrder.Name => WorkspaceOrder.Created, _ => WorkspaceOrder.Recent });
        AutomationProperties.SetName(order, "Ordem");
        _tools.Children.Add(order);
        var view = HeaderButton(_asList ? UrbeTheme.Icons.Region : UrbeTheme.Icons.List, _asList ? "Ver em grade" : "Ver em lista");
        view.Width = view.Height = 36;
        view.Click += (_, _) => AsList = !_asList;
        _tools.Children.Add(view);
    }

    private static Button Chip(string text, bool on, Action click)
    {
        var b = Ui.UrbeMotion.Pressable(new Button
        {
            Content = new TextBlock { Text = text, FontSize = 13, FontWeight = on ? FontWeight.SemiBold : FontWeight.Normal, Foreground = UrbeTheme.Brush(on ? UrbeTheme.AccentInk : UrbeTheme.Text2) },
            Padding = new Thickness(12, 6), MinHeight = 34, CornerRadius = new CornerRadius(17), VerticalContentAlignment = VerticalAlignment.Center,
            Background = on ? UrbeTheme.Brush(UrbeTheme.Accent) : UrbeTheme.Brush(UrbeTheme.Surface2), BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Line2), BorderThickness = new Thickness(1)
        });
        AutomationProperties.SetName(b, text);
        b.Click += (_, _) => click();
        return b;
    }

    private void RenderCrumbs()
    {
        _crumbs.Children.Clear();
        _crumbs.Children.Add(Crumb(_title, "", Folder.Length == 0));
        if (Folder.Length == 0) return;
        var parts = Folder.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            _crumbs.Children.Add(new TextBlock { Text = "›", FontSize = 16, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0) });
            _crumbs.Children.Add(Crumb(parts[i], string.Join("/", parts[..(i + 1)]), i == parts.Length - 1));
        }
    }

    private Control Crumb(string text, string folder, bool current)
    {
        var label = new TextBlock
        {
            Text = text, FontSize = current ? 18 : 15, FontWeight = current ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = UrbeTheme.Brush(current ? UrbeTheme.Text : UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 260
        };
        if (current) return new Border { Child = label, Padding = new Thickness(6, 0), VerticalAlignment = VerticalAlignment.Center };
        var b = Plain(label);
        b.Padding = new Thickness(6, 6);
        b.Click += (_, _) => Open(folder);
        return Ui.UrbeMotion.Pressable(b);
    }

    private void RenderRoot()
    {
        var projects = _layout.Arrange(_index.Projects, p => p.Folder);
        var loose = _index.Loose;
        if (projects.Count == 0 && loose.Count == 0)
        {
            _body.Children.Add(_opening
                ? new TextBlock { Text = "Abrindo suas notas…", FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 56) }
                : EmptyRoot());
            return;
        }
        var boxes = projects.Select(p => (Control)PreviewBox(p)).ToList();
        if (loose.Count > 0) boxes.Add(LooseBox(loose));
        boxes.Add(NewProjectTile());
        _body.Children.Add(Columned(boxes));
    }

    /// <summary>The boxes in columns that fill down (a box keeps its height; no row waits on the tallest box).</summary>
    private Control Columned(IReadOnlyList<Control> boxes)
    {
        int cols = Columns(Bounds.Width);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", cols))) };
        for (int c = 0; c < cols; c++)
        {
            var column = new StackPanel { Spacing = 16, Margin = new Thickness(c == 0 ? 0 : 8, 0, c == cols - 1 ? 0 : 8, 0) };
            for (int i = c; i < boxes.Count; i += cols) column.Children.Add(boxes[i]);
            SetColumn(column, c);
            grid.Children.Add(column);
        }
        return grid;
    }

    private Control EmptyRoot()
    {
        var plus = UrbeTheme.Icon(UrbeTheme.Icons.Plus, 28, UrbeTheme.Accent, 2);
        plus.HorizontalAlignment = HorizontalAlignment.Center;
        var start = Ui.UrbeMotion.Pressable(new Button
        {
            Content = new StackPanel
            {
                Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    plus,
                    new TextBlock { Text = "Novo projeto", FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock
                    {
                        Text = "Cada projeto é uma pasta do vault, mostrada como uma caixa: o que está dentro aparece através do vidro.",
                        FontSize = 14, LineHeight = 21, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 320
                    }
                }
            },
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(20, 40), Margin = new Thickness(0, 24, 0, 0), CornerRadius = new CornerRadius(20),
            Background = UrbeTheme.Brush(UrbeTheme.Accent, .05), BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent, .45), BorderThickness = new Thickness(1.5)
        });
        AutomationProperties.SetName(start, "Novo projeto");
        start.Click += (_, _) => NewProjectRequested?.Invoke();
        return start;
    }

    private Control NewProjectTile()
    {
        var b = Ui.UrbeMotion.Pressable(new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center,
                Children = { UrbeTheme.Icon(UrbeTheme.Icons.Plus, 18, UrbeTheme.Text2, 2), new TextBlock { Text = "Novo projeto", FontSize = 15, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), VerticalAlignment = VerticalAlignment.Center } }
            },
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(16, 18),
            CornerRadius = new CornerRadius(18), Background = Brushes.Transparent, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1.5)
        });
        AutomationProperties.SetName(b, "Novo projeto");
        b.Click += (_, _) => NewProjectRequested?.Invoke();
        return b;
    }

    /// <summary>A project on the root: its head, its subfolders, its most recent items through the glass, and Ver tudo.</summary>
    private Control PreviewBox(WorkspaceBox box)
    {
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(BoxHead(box, open: true));
        var subs = _index.Children(box.Folder);
        if (subs.Count > 0)
        {
            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var s in subs.Take(6)) wrap.Children.Add(SubfolderChip(_index.Box(s)));
            if (subs.Count > 6) wrap.Children.Add(new TextBlock { Text = "+" + (subs.Count - 6), FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 6) });
            content.Children.Add(wrap);
        }
        int cardCols = Bounds.Width / Columns(Bounds.Width) >= 520 ? 3 : 2;
        var recent = _index.Recent(box.Folder, cardCols * 3);
        if (recent.Count > 0) content.Children.Add(Cards(recent, cardCols));
        else if (subs.Count == 0) content.Children.Add(new TextBlock { Text = "Vazio por enquanto.", FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
        if (box.Count > recent.Count)
        {
            var all = Plain(new TextBlock { Text = "Ver tudo (" + box.Count + ")", FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(PaletteHex(ColorOf(box.Folder))) });
            all.HorizontalAlignment = HorizontalAlignment.Left;
            all.Padding = new Thickness(4, 6);
            all.Click += (_, _) => Open(box.Folder);
            content.Children.Add(Ui.UrbeMotion.Pressable(all));
        }
        return Glass(box.Folder, content);
    }

    private Control LooseBox(IReadOnlyList<WorkspaceItem> loose)
    {
        var head = new TextBlock { Text = "Soltas", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) };
        var hint = new TextBlock { Text = "Na raiz do vault, fora dos projetos", FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) };
        int cardCols = Bounds.Width / Columns(Bounds.Width) >= 520 ? 3 : 2;
        var content = new StackPanel { Spacing = 10, Children = { new StackPanel { Spacing = 2, Children = { head, hint } }, Cards(loose.Take(cardCols * 2).ToList(), cardCols) } };
        if (loose.Count > cardCols * 2)
        {
            var all = Plain(new TextBlock { Text = "Ver tudo (" + loose.Count + ")", FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) });
            all.HorizontalAlignment = HorizontalAlignment.Left;
            all.Padding = new Thickness(4, 6);
            all.Click += (_, _) => { _search.Text = ""; ShowLoose(); };
            content.Children.Add(Ui.UrbeMotion.Pressable(all));
        }
        return DropOn(new Border
        {
            Child = content, Padding = new Thickness(14), CornerRadius = new CornerRadius(18),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), Background = UrbeTheme.Brush(UrbeTheme.Surface, .6)
        }, "");
    }

    private void ShowLoose()
    {
        _body.Children.Clear();
        _body.Children.Add(new TextBlock { Text = "Soltas", FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) });
        _body.Children.Add(Cards(_index.Loose, CardColumns()));
    }

    private void RenderFolder()
    {
        var box = _index.Box(Folder);
        var content = new StackPanel { Spacing = 14 };
        content.Children.Add(BoxHead(box, open: false));
        var subs = _index.Children(Folder);
        if (subs.Count > 0)
        {
            var grid = new UniformGrid { Columns = CardColumns() };
            foreach (var s in subs) grid.Children.Add(SubfolderTile(_index.Box(s)));
            content.Children.Add(grid);
        }
        var items = WorkspaceIndex.Arrange(_index.Items(Folder), order: _order);
        if (items.Count > 0) content.Children.Add(ItemsView(items.Take(_shown).ToList(), CardColumns()));
        if (items.Count > _shown)
        {
            var more = Plain(new TextBlock { Text = "Mostrar mais (" + (items.Count - _shown) + ")", FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(PaletteHex(ColorOf(box.Folder))) });
            more.HorizontalAlignment = HorizontalAlignment.Center;
            more.Click += (_, _) => { _shown += Page; Render(); };
            content.Children.Add(Ui.UrbeMotion.Pressable(more));
        }
        if (subs.Count == 0 && items.Count == 0)
        {
            var add = Plain(new TextBlock { Text = "Nova nota aqui", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(PaletteHex(ColorOf(box.Folder))) });
            add.HorizontalAlignment = HorizontalAlignment.Center;
            add.Click += (_, _) => NewNoteRequested?.Invoke(Folder);
            content.Children.Add(new StackPanel
            {
                Spacing = 6, Margin = new Thickness(0, 24),
                Children = { new TextBlock { Text = "Pasta vazia", FontSize = 15, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), HorizontalAlignment = HorizontalAlignment.Center }, Ui.UrbeMotion.Pressable(add) }
            });
        }
        _body.Children.Add(Glass(box.Folder, content));
    }

    /// <summary>A search, a kind or a tag: in the open box (and its subfolders) as one catalogue, at the root grouped by project.</summary>
    private void RenderMatches(string query)
    {
        IEnumerable<WorkspaceItem> pool = query.Length > 0 ? _index.Search(query, Folder.Length == 0 ? null : Folder) : _index.Recent(Folder, int.MaxValue);
        var all = WorkspaceIndex.Arrange(pool, _kind, _tag, _order);
        if (all.Count == 0) { _body.Children.Add(Nothing(query)); return; }
        if (Folder.Length > 0)
        {
            _body.Children.Add(ItemsView(all.Take(Math.Max(_shown, 200)).ToList(), CardColumns()));
            return;
        }
        // the root: grouped by project, the loose ones last
        var boxes = new List<Control>();
        foreach (var group in all.GroupBy(i => WorkspaceIndex.ProjectOf(i.Path)).OrderBy(g => g.Key.Length == 0 ? 1 : 0).ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var list = group.ToList();
            int cardCols = Bounds.Width / Columns(Bounds.Width) >= 520 ? 3 : 2;
            var content = new StackPanel { Spacing = 10 };
            if (group.Key.Length == 0) content.Children.Add(new TextBlock { Text = "Soltas", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) });
            else content.Children.Add(BoxHead(_index.Box(group.Key), open: true, count: list.Count));
            content.Children.Add(ItemsView(list.Take(12).ToList(), cardCols));
            if (list.Count > 12) content.Children.Add(new TextBlock { Text = "e mais " + (list.Count - 12), FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
            boxes.Add(group.Key.Length == 0
                ? new Border { Child = content, Padding = new Thickness(14), CornerRadius = new CornerRadius(18), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), Background = UrbeTheme.Brush(UrbeTheme.Surface, .6) }
                : Glass(group.Key, content));
        }
        _body.Children.Add(Columned(boxes));
    }

    private static Control Nothing(string query) => new TextBlock
    {
        Text = query.Length > 0 ? "Nada com “" + query + "”." : "Nada com esse filtro.", FontSize = 15, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 40)
    };

    private int CardColumns() => Math.Clamp((int)((Bounds.Width - 32 - 28) / 180), 2, 6);

    // ------------------------------------------------------------------ pieces

    /// <summary>
    /// The glass: a translucent surface tinted by the project's colour, a light edge on top, the colour's border — plain,
    /// dashed, double or glowing, as the box was set (UC-85).
    /// </summary>
    private Control Glass(string folder, Control content)
    {
        var c = Color.Parse(PaletteHex(ColorOf(folder)));
        var style = _layout.Of(WorkspaceIndex.ProjectOf(folder + "/"))?.Border ?? "continua";
        var glass = new Border
        {
            Child = content, Padding = new Thickness(14), CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(style is "tracejada" ? 0 : 1.5),
            BorderBrush = new SolidColorBrush(c, style == "brilho" ? .85 : .6),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(34, c.R, c.G, c.B), 0),
                    new GradientStop(Color.FromArgb(150, 0x15, 0x16, 0x1a), .55),
                    new GradientStop(Color.FromArgb(190, 0x15, 0x16, 0x1a), 1)
                }
            },
            BoxShadow = style == "brilho"
                ? new BoxShadows(new BoxShadow { Blur = 22, Spread = 1, Color = Color.FromArgb(110, c.R, c.G, c.B) })
                : new BoxShadows(new BoxShadow { OffsetY = 6, Blur = 24, Color = Color.FromArgb(70, 0, 0, 0) })
        };
        if (style == "dupla")
            return DropOn(new Border { Child = glass, Padding = new Thickness(3), CornerRadius = new CornerRadius(21), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(c, .45) }, folder);
        if (style == "tracejada")
            return DropOn(new Panel
            {
                Children =
                {
                    glass,
                    new Avalonia.Controls.Shapes.Rectangle
                    {
                        RadiusX = 18, RadiusY = 18, Stroke = new SolidColorBrush(c, .75), StrokeThickness = 1.5,
                        StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 5, 4 }, IsHitTestVisible = false
                    }
                }
            }, folder);
        return DropOn(glass, folder);
    }

    /// <summary>A box's head: its icon in its colour, its name, how many items and when; tapping it opens the box.</summary>
    private Control BoxHead(WorkspaceBox box, bool open, int? count = null)
    {
        var hex = PaletteHex(ColorOf(box.Folder));
        var icon = new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(11), Background = UrbeTheme.Brush(hex, .16),
            Child = UrbeTheme.Icon(BoxIcon(box.Folder), 19, hex, 1.8)
        };
        var n = count ?? box.Count;
        var meta = (n == 1 ? "1 item" : n + " itens") + (box.Modified is { } m && When(m) is { } w ? " · " + w : "");
        var text = new StackPanel
        {
            Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0),
            Children =
            {
                new TextBlock { Text = box.Name, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = meta, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }
            }
        };
        var menu = HeaderButton(UrbeTheme.Icons.MoreUi, "Opções de " + box.Name);
        menu.Click += (_, _) => FolderMenuRequested?.Invoke(box.Folder);
        var row = new DockPanel();
        DockPanel.SetDock(menu, Dock.Right);
        row.Children.Add(menu);
        var title = new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, text } };
        if (!open) { row.Children.Add(title); return row; }
        var b = Plain(title);
        b.HorizontalAlignment = HorizontalAlignment.Stretch;
        b.HorizontalContentAlignment = HorizontalAlignment.Left;
        AutomationProperties.SetName(b, box.Name);
        // a project's head dragged with the mouse onto another project puts it in that one's place (UC-85)
        if (!box.Folder.Contains('/'))
            WireDrag(b, box.Name, BoxIcon(box.Folder), target =>
            {
                var project = WorkspaceIndex.ProjectOf(target + "/");
                if (target.Length > 0 && project != box.Folder) ProjectDropped?.Invoke(box.Folder, project);
            });
        b.Click += (_, _) =>
        {
            if (_dragged) { _dragged = false; return; }
            if (TouchIntent.IsIntended(b)) Open(box.Folder);
        };
        row.Children.Add(Ui.UrbeMotion.Pressable(b));
        return row;
    }

    private Control SubfolderChip(WorkspaceBox box)
    {
        var hex = PaletteHex(ColorOf(box.Folder));
        var b = Ui.UrbeMotion.Pressable(new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 6,
                Children =
                {
                    UrbeTheme.Icon(UrbeTheme.Icons.Folder, 14, hex, 1.8),
                    new TextBlock { Text = box.Name, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center, MaxWidth = 180, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = box.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center }
                }
            },
            Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(10),
            Background = UrbeTheme.Brush(hex, .08), BorderBrush = UrbeTheme.Brush(hex, .35), BorderThickness = new Thickness(1)
        });
        AutomationProperties.SetName(b, box.Name);
        DropOn(b, box.Folder);
        b.Click += (_, _) => { if (TouchIntent.IsIntended(b)) Open(box.Folder); };
        return b;
    }

    /// <summary>A subfolder in an open box: a small glass box of its own.</summary>
    private Control SubfolderTile(WorkspaceBox box)
    {
        var hex = PaletteHex(ColorOf(box.Folder));
        var b = Ui.UrbeMotion.Pressable(new Button
        {
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    UrbeTheme.Icon(UrbeTheme.Icons.Folder, 22, hex, 1.8),
                    new TextBlock { Text = box.Name, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = box.Count == 1 ? "1 item" : box.Count + " itens", FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }
                }
            },
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Top,
            Height = 104, Padding = new Thickness(12), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(14),
            Background = UrbeTheme.Brush(hex, .07), BorderBrush = UrbeTheme.Brush(hex, .45), BorderThickness = new Thickness(1.2)
        });
        AutomationProperties.SetName(b, box.Name);
        b.Click += (_, _) => { if (TouchIntent.IsIntended(b)) Open(box.Folder); };
        return b;
    }

    /// <summary>The items as cards in a grid, or as rows of a list.</summary>
    private Control ItemsView(IReadOnlyList<WorkspaceItem> items, int columns)
    {
        if (!_asList) return Cards(items, columns);
        var list = new StackPanel { Spacing = 2 };
        foreach (var i in items) list.Children.Add(Row(i));
        return list;
    }

    /// <summary>A note or file as a row: its kind, title, where it is and when it changed. Tap and hold as a card.</summary>
    private Control Row(WorkspaceItem item)
    {
        var line = new DockPanel { LastChildFill = true };
        if (item.Modified is { } m && When(m) is { } when)
        {
            var date = new TextBlock { Text = when, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            DockPanel.SetDock(date, Dock.Right);
            line.Children.Add(date);
        }
        var icon = UrbeTheme.Icon(IconOf(item.Kind), 18, item.IsNote ? UrbeTheme.Text2 : UrbeTheme.Accent, 1.8);
        icon.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(icon, Dock.Left);
        line.Children.Add(icon);
        line.Children.Add(new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = item.Title, FontSize = 15, FontWeight = FontWeight.Medium, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = item.Folder.Length == 0 ? "Soltas" : item.Folder, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis }
            }
        });
        var b = new Button
        {
            Content = line, MinHeight = 56, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(10), Background = Brushes.Transparent, BorderThickness = new Thickness(0)
        };
        return Openable(b, item);
    }

    private Control Cards(IReadOnlyList<WorkspaceItem> items, int columns)
    {
        var grid = new UniformGrid { Columns = columns };
        foreach (var i in items) grid.Children.Add(Card(i));
        return grid;
    }

    /// <summary>A note's or file's card: its kind, title, the start of its text and when it changed. A tap opens it.</summary>
    private Control Card(WorkspaceItem item)
    {
        var top = new DockPanel();
        var when = item.Modified is { } m ? When(m) : null;
        if (when is not null)
        {
            var date = new TextBlock { Text = when, FontSize = 11, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(date, Dock.Right);
            top.Children.Add(date);
        }
        var kind = UrbeTheme.Icon(IconOf(item.Kind), 16, item.IsNote ? UrbeTheme.Text2 : UrbeTheme.Accent, 1.8);
        kind.HorizontalAlignment = HorizontalAlignment.Left;
        top.Children.Add(kind);
        var stack = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                top,
                new TextBlock { Text = item.Title, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis }
            }
        };
        if (item.Snippet.Length > 0)
            stack.Children.Add(new TextBlock { Text = item.Snippet, FontSize = 12, LineHeight = 17, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis });
        else if (!item.IsNote)
            stack.Children.Add(new TextBlock { Text = KindName(item.Kind), FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
        var b = Ui.UrbeMotion.Pressable(new Button
        {
            Content = stack, Height = 118, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top, Padding = new Thickness(12, 10), Margin = new Thickness(0, 0, 8, 8),
            CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Surface2, .78), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1)
        });
        return Openable(b, item);
    }

    /// <summary>A tap opens the item; a hold (touch) or a right click shows its information and actions.</summary>
    private Button Openable(Button b, WorkspaceItem item)
    {
        AutomationProperties.SetName(b, item.Title);
        ToolTip.SetTip(b, item.Path);
        // a hold (touch) or a right click shows the item's information and actions; the release after a hold opens nothing
        bool held = false;
        b.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, _) => held = false, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        b.AddHandler(Avalonia.Input.InputElement.HoldingEvent, (object? _, Avalonia.Input.HoldingRoutedEventArgs e) =>
        {
            if (e.HoldingState != Avalonia.Input.HoldingState.Started || !TouchIntent.IsIntended(b)) return;
            held = true;
            e.Handled = true;
            Ui.PlatformHaptics.Hold();
            ItemHeld?.Invoke(item.Path);
        });
        b.ContextRequested += (_, e) =>
        {
            e.Handled = true;
            if (!held) ItemHeld?.Invoke(item.Path);
        };
        WireDrag(b, item.Title, IconOf(item.Kind), folder => { if (folder != item.Folder) ItemDropped?.Invoke(item.Path, folder); });
        b.Click += (_, _) =>
        {
            if (_dragged) { _dragged = false; return; }
            if (held) { held = false; return; }
            if (!TouchIntent.IsIntended(b)) return;
            if (item.IsNote) NoteRequested?.Invoke(item.Path); else FileRequested?.Invoke(item.Path);
        };
        return b;
    }

    // ------------------------------------------------------------------ dragging (UC-84 fatia 4)

    private const string DropClass = "ws-drop";
    private Button? _pressed;
    private Button? _dragging;
    private bool _dragged;
    private Point _dragFrom;
    private Control? _dropTarget;
    private Border? _ghost;

    /// <summary>A box (or a subfolder chip) that takes a dragged card: its folder.</summary>
    private static T DropOn<T>(T c, string folder) where T : Control
    {
        c.Classes.Add(DropClass);
        c.Tag = folder;
        return c;
    }

    /// <summary>With the mouse, a card (or a box's head) pressed and moved follows the pointer; released on a box, <paramref name="drop"/> gets its folder.</summary>
    private void WireDrag(Button b, string title, string icon, Action<string> drop)
    {
        b.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, e) =>
        {
            if (e.Pointer.Type != Avalonia.Input.PointerType.Mouse || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            _pressed = b;
            _dragFrom = e.GetPosition(this);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        b.AddHandler(Avalonia.Input.InputElement.PointerMovedEvent, (_, e) =>
        {
            if (!ReferenceEquals(_pressed, b) || e.Pointer.Type != Avalonia.Input.PointerType.Mouse) return;
            var at = e.GetPosition(this);
            if (_dragging is null && Math.Abs(at.X - _dragFrom.X) + Math.Abs(at.Y - _dragFrom.Y) < 8) return;
            _dragging = b;
            MoveGhost(title, icon, at);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        b.AddHandler(Avalonia.Input.InputElement.PointerReleasedEvent, (_, e) =>
        {
            _pressed = null;
            if (!ReferenceEquals(_dragging, b)) return;
            var folder = DropTargetAt(e.GetPosition(this))?.Tag as string;
            EndDrag();
            _dragged = true;
            Dispatcher.UIThread.Post(() => _dragged = false);   // a release elsewhere raises no click to clear it
            if (folder is not null) drop(folder);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void MoveGhost(string title, string icon, Point at)
    {
        if (_ghost is null)
        {
            _ghost = new Border
            {
                IsHitTestVisible = false, ZIndex = 20, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(10), Background = UrbeTheme.Brush(UrbeTheme.Surface2), Opacity = .92,
                BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent, .6), BorderThickness = new Thickness(1),
                BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 6, Blur = 18, Color = Color.FromArgb(90, 0, 0, 0) }),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 6,
                    Children = { UrbeTheme.Icon(icon, 14, UrbeTheme.Text2, 1.8), new TextBlock { Text = title, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text), MaxWidth = 220, TextTrimming = TextTrimming.CharacterEllipsis } }
                }
            };
            SetRowSpan(_ghost, 4);
            Children.Add(_ghost);
        }
        _ghost.Margin = new Thickness(at.X + 12, at.Y + 8, 0, 0);
        var target = DropTargetAt(at);
        if (ReferenceEquals(target, _dropTarget)) return;
        if (_dropTarget is not null) _dropTarget.Opacity = 1;
        _dropTarget = target;
        if (_dropTarget is not null) _dropTarget.Opacity = .7;
    }

    private Control? DropTargetAt(Point at) =>
        (Avalonia.Input.InputExtensions.InputHitTest(this, at) as Visual)?.GetSelfAndVisualAncestors().OfType<Control>().FirstOrDefault(c => c.Classes.Contains(DropClass));

    private void EndDrag()
    {
        _dragging = null;
        if (_dropTarget is not null) _dropTarget.Opacity = 1;
        _dropTarget = null;
        if (_ghost is not null) { Children.Remove(_ghost); _ghost = null; }
    }

    private static string IconOf(WorkspaceItemKind kind) => kind switch
    {
        WorkspaceItemKind.Note => UrbeTheme.Icons.File,
        WorkspaceItemKind.Page => UrbeTheme.Icons.Page,
        WorkspaceItemKind.Image => UrbeTheme.Icons.Image,
        WorkspaceItemKind.Pdf => UrbeTheme.Icons.Book,
        WorkspaceItemKind.Media => UrbeTheme.Icons.Mic,
        _ => UrbeTheme.Icons.FileOther
    };

    private static string KindName(WorkspaceItemKind kind) => kind switch
    {
        WorkspaceItemKind.Page => "Página",
        WorkspaceItemKind.Image => "Imagem",
        WorkspaceItemKind.Pdf => "PDF",
        WorkspaceItemKind.Media => "Áudio ou vídeo",
        _ => "Arquivo"
    };

    /// <summary>The colours of the projects (the same as Conexões).</summary>
    public static string PaletteHex(int color) => ConnectionsGraph.PaletteHex[Math.Clamp(color, 0, ConnectionsGraph.PaletteHex.Count - 1)];

    /// <summary>When an item changed, short: hoje, ontem, or the date.</summary>
    public static string? When(string modified, DateTime? now = null)
    {
        if (!DateTimeOffset.TryParse(modified, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var at)) return null;
        var day = at.ToLocalTime().Date;
        var today = (now ?? DateTime.Now).Date;
        if (day == today) return "hoje";
        if (day == today.AddDays(-1)) return "ontem";
        return day.Year == today.Year ? day.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture) : day.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static Button Plain(Control content) => new()
    {
        Content = content, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0), CornerRadius = new CornerRadius(10)
    };

    private static Button HeaderButton(string icon, string tip)
    {
        var b = Ui.UrbeMotion.Pressable(new Button
        {
            Content = UrbeTheme.Icon(icon, 22, UrbeTheme.Text2, 1.8), Width = 44, Height = 44, Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(22), VerticalAlignment = VerticalAlignment.Center
        });
        ToolTip.SetTip(b, tip);
        AutomationProperties.SetName(b, tip);
        return b;
    }
}
