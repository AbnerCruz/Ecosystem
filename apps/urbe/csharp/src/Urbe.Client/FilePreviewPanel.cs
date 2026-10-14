using System.IO.Compression;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// app.js openFilePreview / renderFilePreview natively: the file of a file building (or of the Explorer) with its
/// title, "Imagem · 120 KB · pasta/arquivo.png", a chooser when the building holds several files, and the body by
/// kind, always inside the Urbe (owner, ADD-0023): images drawn here, HTML as a page (or its source), PDF pages, audio
/// and video in the platform's content viewers (<see cref="ContentViewers"/>); text and code as text; a .zip lists
/// its entries. "Abrir" in another app stays as an extra. Read only: nothing is written to the vault.
/// </summary>
public sealed class FilePreviewPanel : Grid
{
    /// <summary>The largest file read for a preview (the vault's own limit).</summary>
    public const long MaxBytes = 32L * 1024 * 1024;
    /// <summary>Text shown as text up to this size (the 1.8.4: blob.size &lt; 2 MB).</summary>
    public const long MaxTextBytes = 2L * 1024 * 1024;

    private readonly Border _card = new();
    private readonly TextBlock _title = new() { FontSize = 18, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _meta = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    private readonly ComboBox _choose = new() { HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 10, 0, 0), IsVisible = false };
    private readonly ContentControl _body = new() { Margin = new Thickness(0, 14, 0, 0) };
    private readonly Button _open = Action("Abrir", primary: true);
    private readonly Button _show = Action("Mostrar na cidade", primary: false);
    private readonly StackPanel _htmlMode = new() { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 10, 0, 0), IsVisible = false };
    private bool _htmlSource;
    private IReadOnlyList<(string Name, string? Path)> _files = [];
    private int _version;
    private bool _choosing;

    public FilePreviewPanel()
    {
        IsVisible = false;
        Focusable = true;
        Background = UrbeTheme.Brush("#000000", .55);
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _meta.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        var close = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 20, UrbeTheme.Text2), Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
            Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(close, "Fechar");
        close.Click += (_, _) => Close();
        // menuEntradaExplorer for an attachment: Renomear, Mover, Excluir
        var more = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.More, 20, UrbeTheme.Text2), Width = 40, Height = 40, CornerRadius = new CornerRadius(20),
            Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(more, "Mais ações");
        more.Click += (_, _) =>
        {
            if (_generated is { } g) PageActionsRequested?.Invoke(g.Source);
            else if (Current is { } path) ActionsRequested?.Invoke(path);
        };
        _more = more;
        var head = new DockPanel();
        DockPanel.SetDock(close, Dock.Right);
        DockPanel.SetDock(more, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(more);
        head.Children.Add(_title);
        _choose.SelectionChanged += (_, _) => { if (!_choosing && _choose.SelectedIndex >= 0 && IsVisible) _ = ShowAsync(_choose.SelectedIndex); };
        _open.Click += async (_, _) =>
        {
            if (Current is { } path && Storage is { } s && !await s.OpenExternallyAsync(path))
                _meta.Text += " · não foi possível abrir em outro app";
        };
        _show.Click += (_, _) =>
        {
            var b = Building;
            Close();
            if (b is not null) ShowInCity?.Invoke(b);
        };
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,*"), Margin = new Thickness(0, 16, 0, 0) };
        Grid.SetColumn(_open, 2);
        actions.Children.Add(_show);
        actions.Children.Add(_open);
        var layout = new DockPanel();
        // htmlRenderBtn / htmlSourceBtn: the page or its source
        foreach (var (label, source) in new[] { ("Página", false), ("Fonte", true) })
        {
            var b = Chip(label);
            b.Click += (_, _) => { if (_htmlSource == source) return; _htmlSource = source; _ = ShowAsync(Index); };
            _htmlMode.Children.Add(b);
        }
        var top = new StackPanel { Children = { head, _meta, _choose, _htmlMode } };
        DockPanel.SetDock(top, Dock.Top);
        DockPanel.SetDock(actions, Dock.Bottom);
        layout.Children.Add(top);
        layout.Children.Add(actions);
        layout.Children.Add(_body);
        _card.Child = layout;
        _card.Background = UrbeTheme.Brush(UrbeTheme.Surface);
        _card.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _card.BorderThickness = new Thickness(1);
        _card.Padding = new Thickness(20, 14, 20, 20);
        Children.Add(_card);
        PointerPressed += (_, e) => { if (e.Source == this) Close(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        SizeChanged += (_, _) => Place();
    }

    private readonly Button _more;

    /// <summary>"⋯": the host shows the attachment's actions (Renomear, Mover, Excluir).</summary>
    public event Action<string>? ActionsRequested;

    /// <summary>The vault the files are read from.</summary>
    public IUrbeVaultStorage? Storage { get; set; }

    /// <summary>"Mostrar na cidade": the building of the file.</summary>
    public event Action<LegacyCityBuilding>? ShowInCity;

    /// <summary>The vault path of the file shown, or null.</summary>
    public string? Current => _files.Count > 0 && Index < _files.Count ? _files[Index].Path : null;

    public int Index { get; private set; }

    public LegacyCityBuilding? Building { get; private set; }

    /// <summary>What the body shows now: "image", "text", "zip", "viewer" (a content viewer) or "none" (tests).</summary>
    public string Shown { get; private set; } = "none";

    /// <summary>The text of the body when it is text or a listing (tests).</summary>
    public string? ShownText { get; private set; }

    /// <summary>openFilePreview(b, index): the files of a file building.</summary>
    public Task OpenAsync(LegacyCityBuilding building, int index = 0) => OpenAsync(building.PreviewFiles, index, building);

    /// <summary>One file of the vault by its path (the Explorer), with its building when the city has one.</summary>
    public Task OpenAsync(string path, LegacyCityBuilding? building = null) =>
        OpenAsync([(path[(path.LastIndexOf('/') + 1)..], path)], 0, building);

    /// <summary>
    /// A page of the vault (Páginas): the HTML the page engine wrote for <paramref name="source"/> (a .page.json), shown
    /// as a page or as its source like any HTML file; "⋯" offers the page's actions. Nothing is written to the vault.
    /// </summary>
    public async Task OpenGeneratedAsync(string title, string source, byte[] html)
    {
        _generated = (source, html);
        _files = [(title, null)];
        Building = null;
        _htmlSource = false;
        _show.IsVisible = false;
        _choose.IsVisible = false;
        IsVisible = true;
        Place();
        await ShowAsync(0);
        Focus();
    }

    private (string Source, byte[] Html)? _generated;

    /// <summary>The .page.json whose HTML is shown, or null (tests).</summary>
    public string? GeneratedFrom => _generated?.Source;

    /// <summary>"⋯" on a generated page: the host shows the page's actions.</summary>
    public event Action<string>? PageActionsRequested;

    private async Task OpenAsync(IReadOnlyList<(string Name, string? Path)> files, int index, LegacyCityBuilding? building)
    {
        _generated = null;
        _files = files;
        Building = building;
        _htmlSource = false;
        _show.IsVisible = building is not null;
        _choosing = true;
        _choose.ItemsSource = files.Select(f => f.Name).ToList();
        _choosing = false;
        _choose.IsVisible = files.Count > 1;
        IsVisible = true;
        Place();
        await ShowAsync(Math.Clamp(index, 0, Math.Max(0, files.Count - 1)));
        Focus();
    }

    public void Close()
    {
        _version++;
        IsVisible = false;
        _body.Content = null;
        Shown = "none";
        ShownText = null;
    }

    private void Place()
    {
        bool wide = Bounds.Width >= 720;
        _card.Width = wide ? Math.Min(760, Bounds.Width - 48) : double.NaN;
        _card.MaxHeight = wide ? Math.Max(320, Bounds.Height - 64) : Math.Max(320, Bounds.Height * .9);
        _card.HorizontalAlignment = wide ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        _card.VerticalAlignment = wide ? VerticalAlignment.Center : VerticalAlignment.Bottom;
        _card.CornerRadius = wide ? new CornerRadius(16) : new CornerRadius(20, 20, 0, 0);
    }

    private async Task ShowAsync(int index)
    {
        int version = ++_version;
        Index = index;
        _choosing = true;
        _choose.SelectedIndex = index;
        _choosing = false;
        var (name, path) = _files[index];
        var kind = _generated is not null ? VaultFileKinds.Html : VaultFileKinds.Classify(name);
        _title.Text = name;
        _meta.Text = VaultFileKinds.Label(kind) + (path is not null ? " · " + path : "");
        _open.IsVisible = false;
        _more.IsVisible = path is not null || _generated is not null;
        _htmlMode.IsVisible = kind == VaultFileKinds.Html;
        for (int i = 0; i < _htmlMode.Children.Count; i++)
            if (_htmlMode.Children[i] is Button chip) Mark(chip, (i == 1) == _htmlSource);
        Shown = "none";
        ShownText = null;
        _body.Content = Empty("Carregando do dispositivo…");
        // audio and video are played from the vault by the platform: only their size is read here
        bool media = kind is VaultFileKinds.Audio or VaultFileKinds.Video;
        var read = _generated is { } g ? new VaultFileRead(g.Html.Length, g.Html)
            : path is not null && Storage is { } s ? await s.ReadFileAsync(path, media ? 0 : MaxBytes) : null;
        if (version != _version) return;
        if (read is null)
        {
            _body.Content = Empty("Arquivo não encontrado", "O item existe no índice da cidade, mas não foi encontrado na pasta do vault.");
            return;
        }
        _meta.Text = (_generated is { } gen ? "Página · " + VaultFileKinds.FormatBytes(read.Size) + " · " + gen.Source
            : VaultFileKinds.Label(kind) + " · " + VaultFileKinds.FormatBytes(read.Size) + (path is not null ? " · " + path : ""));
        _open.IsVisible = _generated is null;
        var bytes = read.Bytes;
        if (kind == VaultFileKinds.Image && bytes is not null && Image(bytes) is { } image)
        {
            _body.Content = image;
            Shown = "image";
            return;
        }
        if (kind == VaultFileKinds.Zip && bytes is not null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                var names = zip.Entries.Where(e => !e.FullName.EndsWith('/')).Select(e => e.FullName).ToList();
                var text = (names.Count > 0 ? string.Join("\n", names.Take(400)) : "Arquivo compactado vazio") + (names.Count > 400 ? $"\n… +{names.Count - 400} itens" : "");
                ShowText(text, "zip");
            }
            catch (InvalidDataException) { _body.Content = Empty("Arquivo compactado", name); }
            return;
        }
        // HTML as a page, PDF, audio and video: the content viewer of the platform, inside the panel (ADD-0023)
        if (kind is VaultFileKinds.Pdf or VaultFileKinds.Audio or VaultFileKinds.Video || (kind == VaultFileKinds.Html && !_htmlSource))
        {
            Control? viewer = null;
            try { viewer = await ContentViewers.CreateAsync(new ContentRequest(Storage, path ?? _generated?.Source ?? name, name, kind, bytes)); }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException) { viewer = null; }
            if (version != _version) { (viewer as IDisposable)?.Dispose(); return; }
            if (viewer is not null)
            {
                viewer.Height = ViewerHeight();
                _body.Content = viewer;
                Shown = "viewer";
                return;
            }
        }
        bool textual = kind is VaultFileKinds.Html or VaultFileKinds.Other || name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
        if (textual && bytes is not null && read.Size < MaxTextBytes && Text(bytes) is { } source)
        {
            ShowText(source, "text");
            return;
        }
        _body.Content = Empty(name, kind is VaultFileKinds.Pdf or VaultFileKinds.Audio or VaultFileKinds.Video
            ? "Este aparelho ainda não tem o visualizador deste formato dentro do Urbe."
            : "Sem visualização nativa para este formato.");
    }

    /// <summary>The height of a content viewer: a native view needs one, and it fills what the card leaves.</summary>
    private double ViewerHeight() => Bounds.Width >= 720 ? Math.Max(260, Bounds.Height - 280) : Math.Max(260, Bounds.Height * .9 - 250);

    private static Button Chip(string text) => new()
    {
        Content = text, Height = 34, Padding = new Thickness(14, 0), CornerRadius = new CornerRadius(17), FontSize = 13, FontWeight = FontWeight.SemiBold,
        VerticalContentAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(1)
    };

    private static void Mark(Button chip, bool on)
    {
        chip.Background = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Surface2);
        chip.Foreground = UrbeTheme.Brush(on ? UrbeTheme.AccentInk : UrbeTheme.Text2);
        chip.BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Line2);
    }

    private void ShowText(string text, string shown)
    {
        _body.Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = new SelectableTextBlock
            {
                Text = text, FontFamily = new FontFamily("Consolas, Menlo, monospace"), FontSize = 13, LineHeight = 19,
                Foreground = UrbeTheme.Brush(UrbeTheme.Text2), Padding = new Thickness(12)
            },
            Background = UrbeTheme.Brush(UrbeTheme.Bg)
        };
        Shown = shown;
        ShownText = text;
    }

    private static Control? Image(byte[] bytes)
    {
        try
        {
            var bitmap = new Bitmap(new MemoryStream(bytes));
            return new Avalonia.Controls.Image { Source = bitmap, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxHeight = 560 };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException or IOException) { return null; }
    }

    /// <summary>The file as text: UTF-8 (with or without BOM); null when it has NUL bytes or is not valid UTF-8.</summary>
    public static string? Text(byte[] bytes)
    {
        if (Array.IndexOf(bytes, (byte)0) >= 0) return null;
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿'); }
        catch (DecoderFallbackException) { return null; }
    }

    private static Control Empty(string title, string? message = null)
    {
        var box = new StackPanel { Spacing = 6, Margin = new Thickness(0, 24), HorizontalAlignment = HorizontalAlignment.Center };
        box.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold, FontSize = 15, Foreground = UrbeTheme.Brush(UrbeTheme.Text), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap });
        if (message is not null)
            box.Children.Add(new TextBlock { Text = message, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        return box;
    }

    private static Button Action(string text, bool primary) => new()
    {
        Content = text, Height = 48, CornerRadius = new CornerRadius(12), FontSize = 15, FontWeight = FontWeight.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        Background = UrbeTheme.Brush(primary ? UrbeTheme.Accent : UrbeTheme.Surface2),
        Foreground = UrbeTheme.Brush(primary ? UrbeTheme.AccentInk : UrbeTheme.Text),
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(primary ? 0 : 1)
    };
}
