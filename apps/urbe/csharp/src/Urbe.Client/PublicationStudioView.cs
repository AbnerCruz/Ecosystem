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
    private readonly TextBlock _saveStatus = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly Button _retry = new() { Content = "Tentar salvar novamente", MinHeight = 44, IsVisible = false };
    private readonly Button _print;
    private PublicationSourceCatalog? _catalog;
    private PublicationProject? _project;
    private readonly Stack<PublicationProject> _undo = new(), _redo = new();
    private readonly Button _undoButton, _redoButton;

    public PublicationStudioView()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        IsVisible = false;
        // one bar: ← title · undo redo · composition · export · ⋯ (rename, EPUB, print)
        var bar = new DockPanel { LastChildFill = true, Margin = new Thickness(4, 6, 4, 6), Height = 52 };
        var close = ActionButton(UrbeTheme.Icons.Back, "Fechar estúdio");
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Left);
        bar.Children.Add(close);
        var more = ActionButton(UrbeTheme.Icons.MoreUi, "Mais ações da publicação");
        DockPanel.SetDock(more, Dock.Right);
        bar.Children.Add(more);
        var save = ActionButton(UrbeTheme.Icons.Download, "Exportar HTML");
        save.Click += (_, _) => ExportRequested?.Invoke();
        DockPanel.SetDock(save, Dock.Right);
        bar.Children.Add(save);
        var configure = ActionButton(UrbeTheme.Icons.Sliders, "Configurar composição");
        configure.Click += (_, _) => ConfigureRequested?.Invoke();
        DockPanel.SetDock(configure, Dock.Right);
        bar.Children.Add(configure);
        _redoButton = ActionButton(UrbeTheme.Icons.Redo, "Refazer composição");
        _undoButton = ActionButton(UrbeTheme.Icons.Undo, "Desfazer composição");
        _undoButton.Click += (_, _) => UndoComposition();
        _redoButton.Click += (_, _) => RedoComposition();
        DockPanel.SetDock(_redoButton, Dock.Right);
        DockPanel.SetDock(_undoButton, Dock.Right);
        bar.Children.Add(_redoButton);
        bar.Children.Add(_undoButton);
        _print = new Button();   // a menu entry; kept for its visibility (a platform with a printer)
        more.Click += (_, _) =>
        {
            var items = new List<MenuItem>();
            void Item(string header, Action? run) { var i = new MenuItem { Header = header }; i.Click += (_, _) => run?.Invoke(); items.Add(i); }
            Item("Renomear publicação", () => RenameRequested?.Invoke());
            Item("Exportar livro EPUB", () => EpubRequested?.Invoke());
            Item("Exportar documento Word (DOCX)", () => OfficeRequested?.Invoke("docx"));
            Item("Exportar documento OpenDocument (ODT)", () => OfficeRequested?.Invoke("odt"));
            if (_print.IsVisible) Item("Imprimir ou salvar como PDF", () => PrintRequested?.Invoke());
            new ContextMenu { ItemsSource = items }.Open(more);
        };
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.Margin = new Thickness(4, 0);
        _title.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        bar.Children.Add(_title);

        var add = new Ui.UrbeButton("Adicionar nota", Ui.ButtonKind.Secondary, UrbeTheme.Icons.Plus) { MinHeight = 40, Padding = new Thickness(12, 0) };
        AutomationProperties.SetName(add, "Adicionar nota à publicação");
        add.Click += (_, _) => AddSourceRequested?.Invoke();
        var sourcesHead = new DockPanel { LastChildFill = true, Margin = new Thickness(16, 12, 16, 4) };
        DockPanel.SetDock(add, Dock.Right);
        sourcesHead.Children.Add(add);
        sourcesHead.Children.Add(new TextBlock
        {
            Text = "Notas da publicação", FontSize = 16, FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text)
        });
        _saveStatus.Margin = new Thickness(16, 0, 16, 4);
        _saveStatus.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _retry.Margin = new Thickness(16, 0, 16, 8);
        _retry.Click += (_, _) => RetrySaveRequested?.Invoke();
        var sourceGroup = new StackPanel { Children = { sourcesHead, _saveStatus, _retry, _status, _sources } };
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
    public bool HasUnsavedChanges { get; private set; }
    public string? SaveError { get; private set; }
    public event Action? RetrySaveRequested;
    public event Action? AddSourceRequested;
    public event Action? ExportRequested;
    public event Action? EpubRequested;
    /// <summary>CST-166: an office document of the publication ("docx" or "odt").</summary>
    public event Action<string>? OfficeRequested;
    public event Action? PrintRequested;
    public event Action? RenameRequested;
    public event Action? ConfigureRequested;
    public event Action? Closed;
    public event Action<string>? EditSourceRequested;
    public event Action<PublicationSourceReference>? EditReferenceRequested;
    public event Action<PublicationProject>? ProjectChanged;

    public void SetSaveState(bool dirty, string? error = null)
    {
        HasUnsavedChanges = dirty;
        SaveError = error;
        _saveStatus.Text = error is not null
            ? "Não foi possível salvar. O rascunho está preservado nesta sessão. " + error
            : dirty ? "Salvando composição…" : "Composição salva no vault";
        _retry.IsVisible = error is not null;
    }

    public void Open(string path, PublicationProject project, PublicationSourceCatalog catalog)
    {
        if (_catalog is not null) _catalog.SourceChanged -= SourceChanged;
        ProjectPath = path;
        _project = project;
        _undo.Clear();
        _redo.Clear();
        _catalog = catalog;
        _catalog.SourceChanged += SourceChanged;
        _print.IsVisible = ContentViewers.Printer is not null;
        IsVisible = true;
        Refresh();
    }

    public void Close()
    {
        IsVisible = false;
        if (_catalog is not null) _catalog.SourceChanged -= SourceChanged;
        _catalog = null;
        Closed?.Invoke();
    }

    public void UpdateProject(PublicationProject project)
    {
        if (_project is null) return;
        PublicationProjectCodec.Validate(project);
        if (PublicationProjectCodec.Serialize(project) == PublicationProjectCodec.Serialize(_project)) return;
        _undo.Push(_project);
        _redo.Clear();
        _project = project;
        Refresh();
        ProjectChanged?.Invoke(project);
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public void UndoComposition() => RestoreComposition(_undo, _redo);
    public void RedoComposition() => RestoreComposition(_redo, _undo);

    private void RestoreComposition(Stack<PublicationProject> from, Stack<PublicationProject> to)
    {
        if (_project is null || !from.TryPop(out var previous)) return;
        to.Push(_project);
        _project = previous;
        Refresh();
        ProjectChanged?.Invoke(previous);
    }

    public void AddSource(string documentId)
    {
        if (_project is { } project) UpdateProject(project.AddSource(documentId));
    }

    public void AddSource(PublicationSourceReference source)
    {
        if (_project is { } project) UpdateProject(project.AddSource(source));
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
        // disabled (nothing to undo): the icon fades, never Fluent's grey block
        b.Resources["ButtonBackgroundDisabled"] = Brushes.Transparent;
        b.Resources["ButtonBorderBrushDisabled"] = Brushes.Transparent;
        b.PropertyChanged += (_, e) => { if (e.Property == IsEnabledProperty) b.Opacity = b.IsEnabled ? 1 : .35; };
        ToolTip.SetTip(b, label);
        AutomationProperties.SetName(b, label);
        return b;
    }

    public void Refresh()
    {
        if (_project is not { } project || _catalog is not { } catalog) return;
        _title.Text = project.Title;
        _undoButton.IsEnabled = CanUndo;
        _redoButton.IsEnabled = CanRedo;
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
            var label = source is { } found ? System.IO.Path.GetFileNameWithoutExtension(found.Path) +
                (piece.Source.Fragment is { Length: > 0 } fragment ? " #" + fragment.TrimStart('#') : "") : "Fonte indisponível";
            var item = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 3) };
            var remove = ActionButton(UrbeTheme.Icons.Trash, "Remover fonte da composição");
            remove.Click += (_, _) => { if (_project is { } p) UpdateProject(p.RemovePlacement(piece.Id)); };
            DockPanel.SetDock(remove, Dock.Right);
            item.Children.Add(remove);
            var up = ActionButton(UrbeTheme.Icons.ArrowUp, "Mover fonte para cima");
            up.Click += (_, _) => { if (_project is { } p) UpdateProject(p.MovePlacement(piece.Id, Math.Max(0, p.Sections.FindIndex(s => s.Id == piece.Id) - 1))); };
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
            edit.Click += (_, _) =>
            {
                if (EditReferenceRequested is not null) EditReferenceRequested(piece.Source);
                else EditSourceRequested?.Invoke(documentId);
            };
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
                            new MarkdownReader { Selectable = true, ProgressContextMarkdown = source.Markdown,
                                ProgressNoteResolver = id => catalog.Read(new PublicationSourceReference(id))?.Markdown }.BuildMarkdown(text)
                        }
                    }
                });
            }
            else
                _preview.Children.Add(new TextBlock { Text = "Fonte ausente — o arquivo original não será substituído.", Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
        }
        _status.Text = project.Sections.Count == 0 ? "Adicione notas do vault para começar a compor."
            : (project.Sections.Count == 1 ? "1 nota" : project.Sections.Count + " notas") +
              (missing == 0 ? "" : missing == 1 ? " · 1 não encontrada" : " · " + missing + " não encontradas");
    }
}
