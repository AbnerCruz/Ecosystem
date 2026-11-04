using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// "Fixar em painel" (owner, DEC-0045, UC-39): a stretch of text kept at the top of the editor, with its own scroll,
/// while the note below goes on scrolling and being written. Writing on the phone with something else as reference
/// meant going up and down the same note. It stays when another note opens, so a passage of one note can be the
/// reference for writing another. Nothing is written to the vault: it is only the screen.
/// </summary>
public sealed class PinnedPanel : Border
{
    private readonly TextBlock _from = new()
    {
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        Foreground = UrbeTheme.Brush(UrbeTheme.Text3),
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly ScrollViewer _scroll = new()
    {
        HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
    };
    private readonly ContentControl _content = new();
    public FilePreviewPanel Preview { get; } = new(embedded: true);
    /// <summary>The web and YouTube beside the note (Estudo): research → sources → writing, on one screen.</summary>
    public StudyBrowserPanel Web { get; } = new() { Embedded = true };
    /// <summary>The source shown is the web.</summary>
    public bool ShowsWeb => ReferenceEquals(_content.Content, Web);
    public Func<IEnumerable<string>>? Files { get; set; }
    public Func<string, string?>? NoteText { get; set; }
    public IUrbeVaultStorage? Storage { get => Preview.Storage; set => Preview.Storage = value; }
    private readonly TextBox _filter = new() { PlaceholderText = "Buscar arquivo pelo nome ou caminho" };
    private readonly ListBox _choices = new() { MinHeight = 64 };
    private readonly DockPanel _chooser = new();

    /// <summary>Opening the panel never chooses the current editor note.</summary>
    public void Open()
    {
        IsVisible = true;
        if (SourcePath is null) ShowChooser();
    }

    private void ShowChooser()
    {
        _choices.SelectedIndex = -1;
        _from.Text = "Fonte: um site, um vídeo ou um arquivo";
        _content.Content = _chooser;
        FilterFiles();
    }

    private void FilterFiles() => _choices.ItemsSource = (Files?.Invoke() ?? [])
        .Distinct(StringComparer.Ordinal).Where(p => p.Contains(_filter.Text ?? "", StringComparison.OrdinalIgnoreCase))
        .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>The web in the pane: an address or a search (null: the field waits for one).</summary>
    public async Task OpenWebAsync(string? address = null, string? placeholder = null)
    {
        if (!await Preview.TryCloseAsync()) return;
        Text = null;
        _scroll.Content = null;
        SourcePath = null;
        IsVisible = true;
        _from.Text = "Navegador";
        _content.Content = Web;
        Web.Open(address);
        if (address is null) Web.FocusAddress(placeholder);
    }

    public async Task<bool> OpenFileAsync(string path)
    {
        if (!await Preview.TryCloseAsync()) return false;
        if (NoteText?.Invoke(path) is { } markdown) { Pin(markdown, path); return true; }
        Text = null;
        _scroll.Content = null;
        SourcePath = path;
        IsVisible = true;
        _from.Text = "Fixado de " + System.IO.Path.GetFileName(path);
        _content.Content = Preview;
        await Preview.OpenAsync(path);
        return true;
    }

    public PinnedPanel()
    {
        var filter = Ui.SoftField.WithIcon(_filter, UrbeTheme.Icons.SearchUi);
        filter.Margin = new Thickness(0, 0, 0, 6);
        DockPanel.SetDock(filter, Dock.Top);
        _chooser.Children.Add(filter);
        _chooser.Children.Add(_choices);
        IsVisible = false;
        ClipToBounds = true;
        Background = UrbeTheme.Brush(UrbeTheme.Surface);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        BorderThickness = new Thickness(0, 0, 0, 1);
        Padding = new Thickness(16, 8, 8, 10);
        var close = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.CloseUi, 16, UrbeTheme.Text2, 1.8),
            Width = 32, Height = 32, Padding = new Thickness(0), CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(close, "Desafixar");
        close.Click += async (_, _) => await TryUnpinAsync();
        var pin = UrbeTheme.Icon(UrbeTheme.Icons.Pin, 15, UrbeTheme.Accent, 1.8);
        pin.VerticalAlignment = VerticalAlignment.Center;
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(pin, Dock.Left);
        head.Children.Add(close);
        head.Children.Add(pin);
        var choose = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.File, 16, UrbeTheme.Text2, 1.8),
            Width = 32, Height = 32, Padding = new Thickness(0), CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(choose, "Escolher arquivo");
        Avalonia.Automation.AutomationProperties.SetName(choose, "Escolher arquivo");
        Avalonia.Automation.AutomationProperties.SetName(close, "Desafixar");
        DockPanel.SetDock(choose, Dock.Right);
        head.Children.Add(choose);
        choose.Click += async (_, _) =>
        {
            if (!await Preview.TryCloseAsync()) return;
            if (ShowsWeb) { _content.Content = null; Web.Close(); }
            Text = SourcePath = null;
            _scroll.Content = null;
            ShowChooser();
        };
        _filter.TextChanged += (_, _) => FilterFiles();
        // Estudo: the web and a video come first, then the vault's files
        Button Source(string icon, string label, Func<Task> run)
        {
            var b = new Ui.UrbeButton(label, Ui.ButtonKind.Secondary, icon) { MinHeight = 44, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
            b.Click += async (_, _) => await run();
            return b;
        }
        var sources = new UniformGrid { Columns = 2, Margin = new Thickness(0, 0, 0, 6),
            Children =
            {
                Source(UrbeTheme.Icons.SearchUi, "Pesquisar na web", () => OpenWebAsync(null, "Pesquisa ou endereço")),
                Source(UrbeTheme.Icons.Open, "Vídeo do YouTube", () => OpenWebAsync(null, "Cole o link do vídeo"))
            } };
        sources.Children[0].Margin = new Thickness(0, 0, 3, 0);
        sources.Children[1].Margin = new Thickness(3, 0, 0, 0);
        DockPanel.SetDock(sources, Dock.Top);
        _chooser.Children.Insert(0, sources);
        Web.Closed += () => { if (IsVisible) ShowChooser(); };
        _choices.SelectionChanged += async (_, _) =>
        {
            if (_choices.SelectedItem is string path) await OpenFileAsync(path);
        };
        _from.Margin = new Thickness(6, 0);
        head.Children.Add(_from);
        _scroll.Padding = new Thickness(0, 0, 8, 0);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        grid.Children.Add(head);
        Grid.SetRow(_content, 1);
        grid.Children.Add(_content);
        Child = grid;
    }

    /// <summary>Beside the text (a wide window): the accent line is on its right instead of below.</summary>
    public bool Beside
    {
        set => BorderThickness = value ? new Thickness(0, 0, 1, 0) : new Thickness(0, 0, 0, 1);
    }

    public void RetargetSource(string from, string to)
    {
        if (SourcePath != from) return;
        SourcePath = to; _from.Text = "Fixado de " + System.IO.Path.GetFileName(to);
    }
    /// <summary>The pinned Markdown, or null when nothing is pinned.</summary>
    public string? Text { get; private set; }

    /// <summary>The note the pinned text came from.</summary>
    public string? SourcePath { get; private set; }

    public void Pin(string markdown, string sourcePath)
    {
        if (Preview.IsVisible) { _ = PinAfterCloseAsync(markdown, sourcePath); return; }
        Text = markdown;
        SourcePath = sourcePath;
        _from.Text = "Fixado de " + System.IO.Path.GetFileName(sourcePath);
        _scroll.Content = new MarkdownReader().BuildMarkdown(markdown);
        _content.Content = _scroll;
        _scroll.Offset = default;
        IsVisible = true;
    }

    private async Task PinAfterCloseAsync(string markdown, string sourcePath)
    {
        if (await Preview.TryCloseAsync()) Pin(markdown, sourcePath);
    }

    public void Unpin()
    {
        _ = TryUnpinAsync();
    }

    public async Task<bool> TryUnpinAsync()
    {
        if (!await Preview.TryCloseAsync()) return false;
        if (ShowsWeb) { _content.Content = null; Web.Close(); }
        Text = SourcePath = null;
        _scroll.Content = null;
        _content.Content = null;
        IsVisible = false;
        return true;
    }
}
