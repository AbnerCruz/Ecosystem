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
        RowDefinitions = new RowDefinitions("62,58,*");
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
        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 8,
            Margin = new Thickness(10, 4),
            Children = { back, title }
        };
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
        _search.TextChanged += (_, _) => Rebuild();
        Rebuild();
    }

    public event Action? Closed;
    public event Action<CityNote>? NoteRequested;
    public string CurrentFolder => _folder;
    public string SearchText
    {
        get => _search.Text ?? string.Empty;
        set => _search.Text = value;
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
        NoteRequested?.Invoke(note);
        return true;
    }

    private void GoBack()
    {
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
                () => TryOpenNote(file.Path)));
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

    private static Control Item(string name, string category, Action click)
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
            Background = UrbeTheme.Brush(UrbeTheme.Surface),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 62
        };
        button.Click += (_, _) => click();
        return button;
    }
}
