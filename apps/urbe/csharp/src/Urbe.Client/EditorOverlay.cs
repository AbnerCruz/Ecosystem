using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Urbe.Client;

/// <summary>
/// The 1.8.4 full editor (#editorFull) over the city: top bar with back, file name and
/// Visual/Fonte, Markdown toolbar, centred text column and status footer.
/// UC-33 spike: Fonte mode on a native TextBox (soft keyboard/IME proof for G-N0);
/// the native Visual editor and saving to the vault belong to UC-18.
/// </summary>
public sealed class EditorOverlay : Grid
{
    private readonly TextBlock _title = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _saveState = new();
    private CityNote? _note;
    private string _savedContent = string.Empty;
    private bool _saving;
    public bool SaveOnClose { get; set; }
    public bool HasChanges => _note is not null && !string.Equals(Editor.Text, _savedContent, StringComparison.Ordinal);
    public event Func<CityNote, Task>? SaveRequested;

    public EditorOverlay()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        RowDefinitions = new RowDefinitions("56,52,*,48");
        Editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = UrbeTheme.MonoFont,
            FontSize = 15,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent),
            SelectionBrush = UrbeTheme.Brush(UrbeTheme.Accent, .35),
            MaxWidth = 720,
            Padding = new Thickness(20, 24),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        // 1.8.4: the text column has no box of its own; keep Fluent from adding one on hover/focus.
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused" })
            Editor.Resources[key] = Brushes.Transparent;
        foreach (var key in new[] { "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            Editor.Resources[key] = Brushes.Transparent;
        Editor.TextChanged += (_, _) => UpdateStatus(changed: true);

        AddRow(TopBar(), 0);
        AddRow(Toolbar(), 1);
        var body = new ScrollViewer
        {
            Content = Editor,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        AddRow(body, 2);
        AddRow(Footer(), 3);
    }

    public TextBox Editor { get; }
    public event EventHandler? Closed;

    public void Open(CityNote note)
    {
        _note = note;
        _savedContent = note.Content;
        _title.Text = note.Name + ".md";
        Editor.Text = note.Content;
        UpdateStatus(changed: false);
        IsVisible = true;
        // Focus after the overlay is laid out; on Android this brings up the soft keyboard.
        Dispatcher.UIThread.Post(() => Editor.Focus(), DispatcherPriority.Loaded);
    }

    /// <summary>Text currently in the editor, as a note (kept in memory only in the spike).</summary>
    public CityNote? Current => _note is null ? null : _note with { Content = Editor.Text ?? "" };

    private void AddRow(Control control, int row)
    {
        SetRow(control, row);
        Children.Add(control);
    }

    private Control TopBar()
    {
        var back = IconButton(UrbeTheme.Icons.Back, "Voltar para a cidade");
        back.Click += async (_, _) =>
        {
            if (SaveOnClose && HasChanges && !await SaveAsync()) return;
            IsVisible = false;
            Closed?.Invoke(this, EventArgs.Empty);
        };
        _title.FontFamily = UrbeTheme.UiFont;
        _title.FontWeight = FontWeight.SemiBold;
        _title.FontSize = 16;
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;

        var visual = Segment("Visual", active: false);
        visual.IsEnabled = false;
        ToolTip.SetTip(visual, "Editor Visual nativo: UC-18");
        var source = Segment("Fonte", active: true);
        var seg = new Border
        {
            Background = UrbeTheme.Brush(UrbeTheme.Surface2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(3),
            Height = 36,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { visual, source } }
        };
        var bar = new DockPanel { Margin = new Thickness(8, 0, 12, 0), LastChildFill = true };
        DockPanel.SetDock(back, Dock.Left);
        var save = new Button
        {
            Content = "Salvar", MinWidth = 64, Height = 36, Margin = new Thickness(4, 0),
            Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk),
            Background = UrbeTheme.Brush(UrbeTheme.Accent),
            CornerRadius = new CornerRadius(9)
        };
        save.Click += async (_, _) => await SaveAsync();
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4,
            Children = { save, seg } };
        DockPanel.SetDock(right, Dock.Right);
        bar.Children.Add(back);
        bar.Children.Add(right);
        bar.Children.Add(_title);
        _title.Margin = new Thickness(6, 0);
        return new Border
        {
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = bar
        };
    }

    private Control Toolbar()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(10, 0) };
        void Add(string label, Action action, bool mono = false)
        {
            var b = new Button
            {
                Content = label,
                MinWidth = 36,
                Height = 36,
                Background = Brushes.Transparent,
                Foreground = UrbeTheme.Brush(UrbeTheme.Text2),
                FontFamily = mono ? UrbeTheme.MonoFont : UrbeTheme.UiFont,
                FontWeight = FontWeight.SemiBold,
                FontSize = 14,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0)
            };
            b.Click += (_, _) => { action(); Editor.Focus(); };
            row.Children.Add(b);
        }
        void Sep() => row.Children.Add(new Border { Width = 1, Height = 20, Margin = new Thickness(6, 0), Background = UrbeTheme.Brush(UrbeTheme.Line2) });
        Add("H1", () => LinePrefix("# "));
        Add("H2", () => LinePrefix("## "));
        Add("H3", () => LinePrefix("### "));
        Sep();
        Add("B", () => Wrap("**"));
        Add("I", () => Wrap("*"));
        Add("<>", () => Wrap("`"), mono: true);
        Sep();
        Add("•", () => LinePrefix("- "));
        Add("☐", () => LinePrefix("- [ ] "));
        Add("❝", () => LinePrefix("> "));
        Sep();
        Add("[[ ]]", () => Wrap("[[", "]]"));
        Add("∑", () => Wrap("$", "$"));
        return new Border
        {
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new ScrollViewer
            {
                Content = row,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalContentAlignment = VerticalAlignment.Center
            }
        };
    }

    private Control Footer()
    {
        _status.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _saveState.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _status.FontSize = _saveState.FontSize = 12;
        _status.VerticalAlignment = _saveState.VerticalAlignment = VerticalAlignment.Center;
        var bar = new DockPanel { Margin = new Thickness(16, 0) };
        DockPanel.SetDock(_saveState, Dock.Right);
        bar.Children.Add(_saveState);
        bar.Children.Add(_status);
        return new Border { BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(0, 1, 0, 0), Child = bar };
    }

    private void UpdateStatus(bool changed)
    {
        var text = Editor.Text ?? "";
        int lines = text.Length == 0 ? 0 : text.Count(c => c == '\n') + 1;
        _status.Text = $"MD · {lines} linhas";
        _saveState.Text = _saving ? "Salvando…" :
            SaveOnClose ? (HasChanges ? "Alterado · não salvo" : "Sem alterações") :
            (changed ? "Alterado (prévia: não grava no vault)" : "Prévia: não grava no vault");
    }

    /// <summary>Save only via the native vault adapter, never silently to an in-memory preview.</summary>
    public async Task<bool> SaveAsync()
    {
        if (_saving || Current is not { } note) return false;
        if (SaveRequested is null)
        {
            _saveState.Text = "Sem armazenamento conectado";
            return false;
        }
        _saving = true;
        _saveState.Text = "Salvando…";
        try
        {
            await SaveRequested(note);
            _savedContent = note.Content;
            _saveState.Text = "Salvo e verificado no vault";
            return true;
        }
        catch (Exception ex)
        {
            _saveState.Text = "Falha ao salvar: " + ex.Message;
            return false;
        }
        finally { _saving = false; }
    }

    // ---- Markdown editing helpers (Fonte mode) ----

    public void LinePrefix(string prefix)
    {
        var text = Editor.Text ?? "";
        int caret = Math.Clamp(Editor.CaretIndex, 0, text.Length);
        int start = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        Editor.Text = text.Insert(start, prefix);
        Editor.CaretIndex = caret + prefix.Length;
    }

    public void Wrap(string left, string? right = null)
    {
        right ??= left;
        var text = Editor.Text ?? "";
        int a = Math.Clamp(Math.Min(Editor.SelectionStart, Editor.SelectionEnd), 0, text.Length);
        int b = Math.Clamp(Math.Max(Editor.SelectionStart, Editor.SelectionEnd), 0, text.Length);
        Editor.Text = text[..a] + left + text[a..b] + right + text[b..];
        Editor.SelectionStart = a + left.Length;
        Editor.SelectionEnd = b + left.Length;
    }

    private static Button IconButton(string icon, string tip)
    {
        var b = new Button
        {
            Content = UrbeTheme.Icon(icon, 20),
            Width = 40,
            Height = 40,
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tip);
        return b;
    }

    private static Button Segment(string text, bool active) => new()
    {
        Content = text,
        Height = 30,
        Padding = new Thickness(12, 0),
        CornerRadius = new CornerRadius(7),
        FontWeight = FontWeight.SemiBold,
        FontSize = 13,
        VerticalContentAlignment = VerticalAlignment.Center,
        Background = active ? UrbeTheme.Brush(UrbeTheme.Surface3) : Brushes.Transparent,
        Foreground = UrbeTheme.Brush(active ? UrbeTheme.Text : UrbeTheme.Text3)
    };
}
