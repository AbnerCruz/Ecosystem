using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Urbe.Client;

/// <summary>
/// Native 1.8.4 vault Explorer: each folder is a neighbourhood and each text
/// document is a selectable house. Never reads or writes directly; the canonical
/// vault snapshot is provided by MainView/Urbe.Core.
/// </summary>
public sealed class NotesExplorer : Grid
{
    private readonly StackPanel _entries = new() { Spacing = 6, Margin = new Thickness(12, 8) };
    private readonly TextBox _search = new()
    {
        PlaceholderText = "Buscar notas e pastas",
        FontSize = 15,
        MinHeight = 42,
        Margin = new Thickness(12, 6),
        Background = UrbeTheme.Brush(UrbeTheme.Surface2),
        Foreground = UrbeTheme.Brush(UrbeTheme.Text)
    };
    private readonly TextBlock _location = new()
    {
        FontSize = 15,
        FontWeight = FontWeight.SemiBold,
        Foreground = UrbeTheme.Brush(UrbeTheme.Text)
    };
    private readonly TextBlock _summary = new()
    {
        Foreground = UrbeTheme.Brush(UrbeTheme.Text3),
        FontSize = 12,
        Margin = new Thickness(17, 0, 0, 8)
    };
    private readonly List<CityNote> _notes = [];
    private CityNote[] _visibleNotes = [];
    private string[] _visibleFolders = [];
    private string _folder = string.Empty;

    public NotesExplorer()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        RowDefinitions = new RowDefinitions("62,58,*,Auto");
        var back = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.Back, 21),
            Width = 48,
            Height = 48,
            Padding = new Thickness(4),
            Background = Brushes.Transparent
        };
        back.Click += (_, _) => GoBack();
        var title = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 3,
            Children =
            {
                new TextBlock { Text = "NOTAS", FontSize = 11, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) },
                _location
            }
        };
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
            Children = { back, title }
        };
        // mobile-ui.js header: [data-more] "Mais opções"
        var more = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.More, 21),
            Width = 48,
            Height = 48,
            Padding = new Thickness(4),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(more, "Mais opções");
        more.Click += (_, _) => MoreRequested?.Invoke();
        var top = new Grid { Margin = new Thickness(10, 4), Children = { bar, more } };
        SetRow(top, 0);
        Children.Add(top);
        SetRow(_search, 1);
        Children.Add(_search);
        var content = new StackPanel { Spacing = 4, Children = { _summary, _entries } };
        var scroll = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        SetRow(scroll, 2);
        Children.Add(scroll);
        SetRow(_bar, 3);
        Children.Add(SelectionBar());
        _search.TextChanged += (_, _) => Rebuild();
        Rebuild();
    }

    public event Action? Closed;
    /// <summary>The header's "Mais opções" (moreMenu).</summary>
    public event Action? MoreRequested;
    /// <summary>Lixeira: Restaurar (the trashed document's id).</summary>
    public event Action<string>? RestoreRequested;
    /// <summary>Lixeira: ✕ Excluir de vez (the trashed document's id).</summary>
    public event Action<string>? PurgeRequested;
    /// <summary>Lixeira: Esvaziar.</summary>
    public event Action? EmptyTrashRequested;

    /// <summary>One item of the trash as the Lixeira lists it (renderTrash): the document's id and title, its path.</summary>
    public sealed record TrashEntry(string Id, string Title, string OriginalPath);

    private IReadOnlyList<TrashEntry>? _trash;

    /// <summary>The Lixeira view (show('trash')): newest first; null leaves it.</summary>
    public void ShowTrash(IReadOnlyList<TrashEntry>? items)
    {
        _trash = items;
        if (items is not null) _collection = null;
        Rebuild();
    }

    /// <summary>A collection view (Recentes, Favoritos: renderCollection).</summary>
    public sealed record Collection(string Title, IReadOnlyList<CityNote> Notes, string EmptyTitle, string EmptyText);

    private Collection? _collection;

    /// <summary>show('recent') / show('favorites'); null leaves it.</summary>
    public void ShowCollection(Collection? collection)
    {
        _collection = collection;
        _trash = null;
        Rebuild();
    }

    /// <summary>The title of the collection shown, or null.</summary>
    public string? CollectionTitle => _collection?.Title;

    private void RenderCollection(Collection c)
    {
        _location.Text = c.Title;
        _summary.Text = "";
        _visibleFolders = [];
        _visibleNotes = c.Notes.ToArray();
        _entries.Children.Clear();
        if (c.Notes.Count == 0)
        {
            _entries.Children.Add(new StackPanel
            {
                Margin = new Thickness(12, 24),
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = c.EmptyTitle, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                    new TextBlock { Text = c.EmptyText, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap }
                }
            });
            return;
        }
        foreach (var note in c.Notes)
        {
            var file = note;
            int slash = file.Path.LastIndexOf('/');
            _entries.Children.Add(Item(file.Name, slash < 0 ? "" : file.Path[..slash],
                () => TryOpenNote(file.Path), () => HoldNote(file.Path), () => HoldDelay, _selected.Contains(file.Path)));
        }
    }

    /// <summary>The Lixeira is shown.</summary>
    public bool ShowingTrash => _trash is not null;
    public event Action<CityNote>? NoteRequested;
    /// <summary>How long a press must last to be a hold (mobile-ui.js HOLD: 430 ms).</summary>
    public TimeSpan HoldDelay { get; set; } = TimeSpan.FromMilliseconds(430);

    /// <summary>An action of the selection bar (.ume-actions): "open", "favorite", "move", "duplicate" or "delete",
    /// with the selected notes' paths.</summary>
    public event Action<string, IReadOnlyList<string>>? SelectionAction;

    private readonly List<string> _selected = [];
    private readonly Border _bar = new();
    private readonly TextBlock _count = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>The selected notes (model.selection), in the order they were selected.</summary>
    public IReadOnlyList<string> SelectedPaths => _selected.ToList();

    /// <summary>model.clear().</summary>
    public void ClearSelection()
    {
        _selected.Clear();
        Rebuild();
    }

    /// <summary>model.select(id,'toggle').</summary>
    private void Toggle(string path)
    {
        if (!_selected.Remove(path)) _selected.Add(path);
        Rebuild();
    }

    /// <summary>The .ume-actions bar: Limpar, the count, Abrir, Favoritar, Mover para pasta, Duplicar, Mover para a lixeira
    /// (Compor joins with the compositions).</summary>
    private Control SelectionBar()
    {
        Button Act(string icon, string tip, string action, string? color = null)
        {
            var b = new Button { Content = UrbeTheme.Icon(icon, 20, color ?? UrbeTheme.Text), Width = 44, Height = 44, Background = Brushes.Transparent, Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => { if (_selected.Count > 0) SelectionAction?.Invoke(action, _selected.ToList()); };
            return b;
        }
        var clear = new Button { Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 20, UrbeTheme.Text2), Width = 44, Height = 44, Background = Brushes.Transparent, Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(clear, "Limpar seleção");
        clear.Click += (_, _) => ClearSelection();
        _count.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right, Children =
        {
            Act(UrbeTheme.Icons.Open, "Abrir", "open"),
            Act(UrbeTheme.Icons.Star, "Favoritar", "favorite"),
            Act(UrbeTheme.Icons.Move, "Mover para pasta", "move"),
            Act(UrbeTheme.Icons.Copy, "Duplicar", "duplicate"),
            Act(UrbeTheme.Icons.Trash, "Mover para a lixeira", "delete", UrbeTheme.Danger)
        } };
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(clear, Dock.Left);
        DockPanel.SetDock(actions, Dock.Right);
        row.Children.Add(clear);
        row.Children.Add(actions);
        row.Children.Add(_count);
        _bar.Child = row;
        _bar.Background = UrbeTheme.Brush(UrbeTheme.Surface2);
        _bar.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _bar.BorderThickness = new Thickness(0, 1, 0, 0);
        _bar.Padding = new Thickness(8, 6);
        _bar.IsVisible = false;
        return _bar;
    }

    private void UpdateBar()
    {
        _bar.IsVisible = _selected.Count > 0 && _trash is null;
        _count.Text = _selected.Count == 1 ? "1 nota" : _selected.Count + " notas";
    }
    public string CurrentFolder => _folder;
    public string SearchText
    {
        get => _search.Text ?? string.Empty;
        set
        {
            _search.Text = value;
            Rebuild(); // deterministic API even before Avalonia raises TextChanged on dispatcher
        }
    }
    public IReadOnlyList<string> VisibleNotePaths => _visibleNotes.Select(n => n.Path).ToArray();
    public IReadOnlyList<string> VisibleFolderPaths => _visibleFolders.ToArray();

    public void SetNotes(IEnumerable<CityNote> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        _notes.Clear();
        _notes.AddRange(notes.Where(n => !string.IsNullOrWhiteSpace(n.Path))
            .OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase));
        _folder = string.Empty;
        _selected.Clear();
        _search.Text = string.Empty;
        Rebuild();
    }

    public bool TryEnterFolder(string folder)
    {
        if (!_visibleFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
            return false;
        _folder = folder;
        _search.Text = string.Empty;
        Rebuild();
        return true;
    }

    public bool TryOpenNote(string path)
    {
        var note = _visibleNotes.FirstOrDefault(n =>
            string.Equals(n.Path, path, StringComparison.OrdinalIgnoreCase));
        if (note is null) return false;
        // activate(): while a selection exists a tap toggles, otherwise it opens
        if (_selected.Count > 0) { Toggle(note.Path); return true; }
        NoteRequested?.Invoke(note);
        return true;
    }

    /// <summary>The hold of a visible note row (what the gesture raises): model.select(id,'toggle').</summary>
    public bool HoldNote(string path)
    {
        var note = _visibleNotes.FirstOrDefault(n => string.Equals(n.Path, path, StringComparison.OrdinalIgnoreCase));
        if (note is null) return false;
        Toggle(note.Path);
        return true;
    }

    private void GoBack()
    {
        if (_trash is not null)
        {
            ShowTrash(null);
            return;
        }
        if (_collection is not null)
        {
            ShowCollection(null);
            return;
        }
        if (!string.IsNullOrEmpty(_search.Text))
        {
            _search.Text = string.Empty;
            return;
        }
        if (string.IsNullOrEmpty(_folder))
        {
            Closed?.Invoke();
            return;
        }
        var separator = _folder.LastIndexOf('/');
        _folder = separator < 0 ? string.Empty : _folder[..separator];
        Rebuild();
    }

    private void Rebuild()
    {
        UpdateBar();
        if (_trash is not null)
        {
            RenderTrash(_trash);
            return;
        }
        if (_collection is { } c)
        {
            RenderCollection(c);
            return;
        }
        var search = _search.Text?.Trim() ?? string.Empty;
        if (search.Length > 0)
        {
            _visibleFolders = [];
            _visibleNotes = _notes.Where(n =>
                    n.Path.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        else
        {
            var prefix = _folder.Length == 0 ? string.Empty : _folder + "/";
            var folders = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var direct = new List<CityNote>();
            foreach (var note in _notes)
            {
                if (!note.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                var suffix = note.Path[prefix.Length..];
                var slash = suffix.IndexOf('/');
                if (slash < 0) direct.Add(note);
                else if (slash > 0) folders.Add(prefix + suffix[..slash]);
            }
            _visibleFolders = folders.ToArray();
            _visibleNotes = direct.ToArray();
        }

        _location.Text = string.IsNullOrEmpty(_folder) ? "Arquivos" : _folder;
        _summary.Text = search.Length > 0
            ? $"Busca: {_visibleNotes.Length} resultado(s)"
            : $"{_visibleFolders.Length} pasta(s) · {_visibleNotes.Length} nota(s)";
        _entries.Children.Clear();

        foreach (var path in _visibleFolders)
        {
            var folder = path;
            _entries.Children.Add(Item(
                System.IO.Path.GetFileName(folder),
                "PASTA", () => TryEnterFolder(folder)));
        }
        foreach (var note in _visibleNotes)
        {
            var file = note;
            _entries.Children.Add(Item(
                file.Name,
                search.Length > 0 ? file.Path : "NOTA MARKDOWN",
                () => TryOpenNote(file.Path), () => HoldNote(file.Path), () => HoldDelay, _selected.Contains(file.Path)));
        }
        if (_visibleFolders.Length + _visibleNotes.Length == 0)
            _entries.Children.Add(new TextBlock
            {
                Text = "Nenhuma nota nesta pasta.",
                Foreground = UrbeTheme.Brush(UrbeTheme.Text3),
                Margin = new Thickness(12, 24),
                FontSize = 14
            });
    }

    /// <summary>renderTrash: "n item(ns) · restaure ou exclua de vez" with Esvaziar, then each item with Restaurar and ✕.</summary>
    private void RenderTrash(IReadOnlyList<TrashEntry> items)
    {
        _location.Text = "Lixeira";
        _summary.Text = items.Count > 0 ? items.Count + " item(ns) · restaure ou exclua de vez" : "";
        _visibleFolders = [];
        _visibleNotes = [];
        _entries.Children.Clear();
        if (items.Count == 0)
        {
            _entries.Children.Add(new StackPanel
            {
                Margin = new Thickness(12, 24),
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "A lixeira está vazia", FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                    new TextBlock { Text = "Notas excluídas ficam aqui e podem ser restauradas.", FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap }
                }
            });
            return;
        }
        var empty = new Button { Content = "Esvaziar", HorizontalAlignment = HorizontalAlignment.Right, Background = UrbeTheme.Brush(UrbeTheme.Surface3), Foreground = UrbeTheme.Brush(UrbeTheme.Text), CornerRadius = new CornerRadius(10) };
        empty.Click += (_, _) => EmptyTrashRequested?.Invoke();
        _entries.Children.Add(empty);
        foreach (var item in items)
        {
            var entry = item;
            var restore = new Button { Content = "Restaurar", Background = UrbeTheme.Brush(UrbeTheme.Surface3), Foreground = UrbeTheme.Brush(UrbeTheme.Text), CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Center };
            restore.Click += (_, _) => RestoreRequested?.Invoke(entry.Id);
            var purge = new Button { Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 18, UrbeTheme.Text2), Background = Brushes.Transparent, Padding = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(purge, "Excluir de vez");
            purge.Click += (_, _) => PurgeRequested?.Invoke(entry.Id);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { restore, purge } };
            DockPanel.SetDock(actions, Dock.Right);
            var texts = new StackPanel
            {
                Spacing = 3,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = entry.Title, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = entry.OriginalPath, FontSize = 11, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis }
                }
            };
            _entries.Children.Add(new Border
            {
                Background = UrbeTheme.Brush(UrbeTheme.Surface),
                BorderBrush = UrbeTheme.Brush(UrbeTheme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 10),
                MinHeight = 62,
                Child = new DockPanel { Children = { actions, texts } }
            });
        }
    }

    private static Control Item(string name, string category, Action click, Action? hold = null, Func<TimeSpan>? holdDelay = null, bool selected = false)
    {
        var texts = new StackPanel
        {
            Spacing = 3,
            Children =
            {
                new TextBlock
                {
                    Text = name,
                    FontSize = 15,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Text),
                    TextTrimming = TextTrimming.CharacterEllipsis
                },
                new TextBlock
                {
                    Text = category,
                    FontSize = 11,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Text3),
                    TextTrimming = TextTrimming.CharacterEllipsis
                }
            }
        };
        var button = new Button
        {
            Content = texts,
            Background = selected ? UrbeTheme.Brush(UrbeTheme.Accent, .14) : UrbeTheme.Brush(UrbeTheme.Surface),
            BorderBrush = UrbeTheme.Brush(selected ? UrbeTheme.Accent : UrbeTheme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 62
        };
        // v21BindEntry: a press held 520 ms without moving more than 9 px is the hold; the release then opens nothing
        bool held = false;
        Avalonia.Threading.DispatcherTimer? timer = null;
        Point start = default;
        if (hold is not null)
        {
            button.AddHandler(PointerPressedEvent, (_, e) =>
            {
                if (e.GetCurrentPoint(button).Properties.IsRightButtonPressed) return;
                held = false;
                start = e.GetPosition(button);
                timer?.Stop();
                timer = new Avalonia.Threading.DispatcherTimer { Interval = holdDelay?.Invoke() ?? TimeSpan.FromMilliseconds(520) };
                timer.Tick += (_, _) => { timer!.Stop(); timer = null; held = true; hold(); };
                timer.Start();
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(PointerMovedEvent, (_, e) =>
            {
                var p = e.GetPosition(button);
                if (timer is not null && Math.Sqrt(Math.Pow(p.X - start.X, 2) + Math.Pow(p.Y - start.Y, 2)) > 9) { timer.Stop(); timer = null; }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            button.AddHandler(PointerReleasedEvent, (_, _) => { timer?.Stop(); timer = null; },
                Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
        }
        button.Click += (_, _) =>
        {
            if (held) { held = false; return; }
            click();
        };
        return button;
    }
}
