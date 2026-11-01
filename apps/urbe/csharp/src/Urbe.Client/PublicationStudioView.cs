using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// First native, touch-first surface for Publicações. Authorship stays in real notes:
/// the publication only orders references and design metadata. Never hosts a second text editor.
/// </summary>
public sealed class PublicationStudioView : Border
{
    private readonly StackPanel _sources = new() { Spacing = 8 };
    private readonly StackPanel _preview = new() { Spacing = 20, Margin = new Thickness(18, 14, 18, 32) };
    private readonly TextBlock _title = new() { FontSize = 20, FontWeight = FontWeight.Bold };
    private readonly TextBlock _status = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private PublicationSourceCatalog? _catalog;
    private PublicationProject? _project;

    public PublicationStudioView()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        IsVisible = false;
        var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(8, 6, 12, 6), Height = 52 };
        var close = ActionButton(UrbeTheme.Icons.Back, "Fechar estúdio");
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Left);
        bar.Children.Add(close);
        var save = ActionButton(UrbeTheme.Icons.Download, "Exportar HTML");
        save.Click += (_, _) => ExportRequested?.Invoke();
        DockPanel.SetDock(save, Dock.Right);
        bar.Children.Add(save);
        var rename = ActionButton(UrbeTheme.Icons.Pencil, "Renomear publicação");
        rename.Click += (_, _) => RenameRequested?.Invoke();
        DockPanel.SetDock(rename, Dock.Right);
        bar.Children.Add(rename);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        bar.Children.Add(_title);

        var add = new Button
        {
            Content = "Adicionar nota",
            MinHeight = 44, Padding = new Thickness(16, 0), CornerRadius = new CornerRadius(12),
            Background = UrbeTheme.Brush(UrbeTheme.Accent),
            Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk)
        };
        AutomationProperties.SetName(add, "Adicionar nota à publicação");
        add.Click += (_, _) => AddSourceRequested?.Invoke();
        var sourcesHead = new DockPanel { LastChildFill = true, Margin = new Thickness(16, 12, 16, 4) };
        DockPanel.SetDock(add, Dock.Right);
        sourcesHead.Children.Add(add);
        sourcesHead.Children.Add(new TextBlock
        {
            Text = "Fontes vivas", FontSize = 16, FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text)
        });
        var sourceGroup = new StackPanel { Children = { sourcesHead, _status, _sources } };
        _status.Margin = new Thickness(16, 0, 16, 8);
        _sources.Margin = new Thickness(16, 0, 16, 12);
        // One scroll owner in every viewport: no reparenting a live text surface
        // during Android keyboard/layout changes (a known editor reliability risk).
        var body = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    sourceGroup,
                    new Border { Height = 1, Background = UrbeTheme.Brush(UrbeTheme.Line2), Margin = new Thickness(16, 0) },
                    _preview
                }
            },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(body);
        Child = root;
    }

    public string ProjectPath { get; private set; } = "";
    public PublicationProject? Project => _project;
    public event Action? AddSourceRequested;
    public event Action? ExportRequested;
    public event Action? RenameRequested;
    public event Action<string>? EditSourceRequested;
    public event Action<PublicationProject>? ProjectChanged;

    public void Open(string path, PublicationProject project, PublicationSourceCatalog catalog)
    {
        if (_catalog is not null) _catalog.SourceChanged -= SourceChanged;
        ProjectPath = path;
        _project = project;
        _catalog = catalog;
        _catalog.SourceChanged += SourceChanged;
        IsVisible = true;
        Refresh();
    }

    public void Close()
    {
        IsVisible = false;
        if (_catalog is not null) _catalog.SourceChanged -= SourceChanged;
        _catalog = null;
    }

    public void UpdateProject(PublicationProject project)
    {
        if (_project is null) return;
        _project = project;
        Refresh();
        ProjectChanged?.Invoke(project);
    }

    public void AddSource(string documentId)
    {
        if (_project is { } project) UpdateProject(project.AddSource(documentId));
    }

    private void SourceChanged(object? sender, PublicationSourceChange change)
    {
        if (_project is null || change.DocumentId is { } id && !_project.Sources.Any(s => s.DocumentId == id)) return;
        Dispatcher.UIThread.Post(() => { if (IsVisible) Refresh(); }, DispatcherPriority.Background);
    }

    private static Button ActionButton(string icon, string label)
    {
        var b = new Button
        {
            Content = UrbeTheme.Icon(icon, 19, UrbeTheme.Text2),
            MinWidth = 44, MinHeight = 44, Padding = new Thickness(10),
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(12)
        };
        ToolTip.SetTip(b, label);
        AutomationProperties.SetName(b, label);
        return b;
    }

    public void Refresh()
    {
        if (_project is not { } project || _catalog is not { } catalog) return;
        _title.Text = project.Title;
        _sources.Children.Clear();
        _preview.Children.Clear();
        _preview.Children.Add(new TextBlock
        {
            Text = project.Title, FontSize = 26, FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text)
        });
        var missing = 0;
        for (int i = 0; i < project.Sections.Count; i++)
        {
            var piece = project.Sections[i];
            PublicationSourceView? source;
            try { source = catalog.Read(piece.Source); }
            catch (NotSupportedException) { source = null; }
            if (source is null) missing++;
            var documentId = piece.Source.DocumentId;
            var label = source is { } found ? System.IO.Path.GetFileNameWithoutExtension(found.Path) : "Fonte indisponível";
            var item = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 3) };
            var remove = ActionButton(UrbeTheme.Icons.Trash, "Remover fonte da composição");
            remove.Click += (_, _) => { if (_project is { } p) UpdateProject(p.RemoveSource(documentId)); };
            DockPanel.SetDock(remove, Dock.Right);
            item.Children.Add(remove);
            var up = ActionButton(UrbeTheme.Icons.ArrowUp, "Mover fonte para cima");
            up.Click += (_, _) => { if (_project is { } p) UpdateProject(p.MoveSource(documentId, Math.Max(0, p.Sections.FindIndex(s => s.Source.DocumentId == documentId) - 1))); };
            DockPanel.SetDock(up, Dock.Right);
            item.Children.Add(up);
            var edit = new Button
            {
                Content = label, MinHeight = 44, Padding = new Thickness(10, 4),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = UrbeTheme.Brush(UrbeTheme.Text)
            };
            AutomationProperties.SetName(edit, "Editar nota de origem: " + label);
            edit.IsEnabled = source is not null;
            edit.Click += (_, _) => EditSourceRequested?.Invoke(documentId);
            item.Children.Add(edit);
            _sources.Children.Add(item);
            if (source is not null)
            {
                var text = MarkdownEngine.SplitFrontmatter(source.Markdown).Body;
                _preview.Children.Add(new Border
                {
                    Padding = new Thickness(14, 10), BorderThickness = new Thickness(0, 1, 0, 0),
                    BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = label, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) },
                            new MarkdownReader { Selectable = true }.BuildMarkdown(text)
                        }
                    }
                });
            }
            else
                _preview.Children.Add(new TextBlock { Text = "Fonte ausente — o arquivo original não será substituído.", Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
        }
        _status.Text = project.Sections.Count == 0 ? "Adicione notas do vault para começar a compor."
            : project.Sections.Count + " nota(s) • " + (missing == 0 ? "fontes atualizadas" : missing + " fonte(s) indisponível(is)");
    }
}
