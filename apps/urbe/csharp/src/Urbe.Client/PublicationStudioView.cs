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
    private readonly TextBlock _status = new() { FontSize = UrbeTokens.Type.Caption.Size, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _saveStatus = new() { FontSize = UrbeTokens.Type.Caption.Size, TextWrapping = TextWrapping.Wrap };
    private readonly Button _retry = new() { Content = "Tentar salvar novamente", MinHeight = 44, IsVisible = false };
    private readonly Button _print;
    private PublicationSourceCatalog? _catalog;
    private PublicationProject? _project;
    private readonly Stack<PublicationProject> _undo = new(), _redo = new();
    private readonly Button _undoButton, _redoButton;
    private PublicationVideoPanel? _video;

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
            Item("Verificar publicação", () => ReviewRequested?.Invoke());
            if (_project?.Sections.Count > 1) Item("Ordenar as notas", () => SortRequested?.Invoke());
            Item("Pedir ao Assistente", () => AskAssistantRequested?.Invoke());
            Item("Exportar PDF", () => OfficeRequested?.Invoke("pdf"));
            if (IsVideo)
            {
                Item("Tema do vídeo", () => VideoThemeRequested?.Invoke());
                Item("Exportar vídeo MP4", () => VideoExportRequested?.Invoke());
                Item("Exportar legendas (SRT)", () => SubtitlesRequested?.Invoke("srt"));
                Item("Exportar legendas para a web (VTT)", () => SubtitlesRequested?.Invoke("vtt"));
                Item("Adicionar áudio (voz, música, efeito)", () => AddAudioRequested?.Invoke());
                if (_project?.Video?.Audio.Count > 0) Item("Ajustar áudios do vídeo", () => AdjustAudioRequested?.Invoke());
                if (CanNarrate) Item("Narrar o roteiro", () => NarrateRequested?.Invoke());
                if (CanNarrate) Item("Voz da narração", () => VoiceRequested?.Invoke());
            }
            Item("Exportar livro EPUB", () => EpubRequested?.Invoke());
            Item("Exportar documento Word (DOCX)", () => OfficeRequested?.Invoke("docx"));
            Item("Exportar documento OpenDocument (ODT)", () => OfficeRequested?.Invoke("odt"));
            Item("Exportar apresentação PowerPoint (PPTX)", () => OfficeRequested?.Invoke("pptx"));
            Item("Exportar pacote para distribuir (ZIP)", () => PackageRequested?.Invoke());
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
            Text = "Notas da publicação", FontSize = UrbeTokens.Type.BodyStrong.Size, FontWeight = UrbeTokens.Type.BodyStrong.Weight,
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
        // Preview text can contain hundreds of pages. Build only sections near the viewport.
        Ui.Deferred.Watch(_preview);
    }

    public string ProjectPath { get; private set; } = "";
    public PublicationProject? Project => _project;
    public bool HasUnsavedChanges { get; private set; }
    public string? SaveError { get; private set; }
    public event Action? RetrySaveRequested;
    public event Action? AddSourceRequested;
    public event Action? ExportRequested;
    public event Action? EpubRequested;
    /// <summary>CST-158: the Assistente from here, with this publication already named (nothing is sent by itself).</summary>
    public event Action? AskAssistantRequested;
    /// <summary>CST-163: the publication with a manifest of sizes and checksums, to hand to someone else.</summary>
    public event Action? PackageRequested;
    /// <summary>CST-166/168: an office file of the publication ("docx", "odt" or "pptx").</summary>
    public event Action<string>? OfficeRequested;
    public event Action? PrintRequested;
    public event Action? RenameRequested;
    /// <summary>CST-179/180: "Verificar publicação" — what to fix before exporting.</summary>
    public event Action? ReviewRequested;
    /// <summary>CST-160: order the notes by title, date or folder.</summary>
    public event Action? SortRequested;
    public event Action? ConfigureRequested;
    public event Action? Closed;
    public event Action<string>? EditSourceRequested;
    public event Action<PublicationSourceReference>? EditReferenceRequested;
    /// <summary>CST-156: a double tap on the preview — the source and the text shown where it was tapped.</summary>
    public event Action<PublicationSourceReference, string>? EditShownTextRequested;
    public event Action<PublicationProject>? ProjectChanged;
    /// <summary>CST-155: "Adaptar" on a piece — a real note adapted from its source takes its place.</summary>
    public event Action<PublicationPlacement>? AdaptRequested;
    /// <summary>CST-155: the adaptation (its id) was checked against its source as it is now (the source's id).</summary>
    public event Action<string, string>? AdaptationReviewed;
    public event Action? VideoExportRequested;
    public event Action? VideoCancelRequested;
    public event Action<string>? SubtitlesRequested;
    public event Action? AddAudioRequested;
    /// <summary>CST-172: volume, fade in and out of a track, or take it out.</summary>
    public event Action? AdjustAudioRequested;
    /// <summary>CST-172: each scene's script spoken by the device's voice and placed at the scene's start.</summary>
    public event Action? NarrateRequested;
    public event Action<string>? PodcastAudioRequested;
    public event Action<string>? PlayPodcastRequested;
    public Func<string, string?>? FindPodcastAudio { get; set; }
    /// <summary>#1004: choosing the device voice that narrates.</summary>
    public event Action? VoiceRequested;
    /// <summary>#956: choose the video's finished look (VideoThemes).</summary>
    public event Action? VideoThemeRequested;
    public bool CanNarrate { get; set; }
    public event Action<string>? AddVideoElementRequested;

    /// <summary>CST-170: a publication laid out as a video (one note per scene).</summary>
    public bool IsVideo => _project?.Design.Layout == "video";
    /// <summary>The vault files the video's elements show (images), read by the host before drawing.</summary>
    public IReadOnlyDictionary<string, byte[]> VideoFiles { get; set; } = new Dictionary<string, byte[]>();
    public PublicationVideoPanel VideoPanel => _video ??= MakeVideoPanel();

    private PublicationVideoPanel MakeVideoPanel()
    {
        var panel = new PublicationVideoPanel();
        panel.ExportRequested += () => VideoExportRequested?.Invoke();
        panel.CancelRequested += () => VideoCancelRequested?.Invoke();
        panel.AddAudioRequested += () => AddAudioRequested?.Invoke();
        panel.NarrateRequested += () => NarrateRequested?.Invoke();
        panel.VoiceRequested += () => VoiceRequested?.Invoke();
        panel.AddElementRequested += id => AddVideoElementRequested?.Invoke(id);
        panel.SceneChanged += ApplyVideoScene;
        panel.CodeEditRequested += (scene, element) => CodeEditRequested?.Invoke(scene, element);
        return panel;
    }

    /// <summary>A scene of the video changed (the panel's edits, or the host's code editor), saved like any change.</summary>
    public void ApplyVideoScene(VideoScene scene)
    {
        if (_project is not { } p) return;
        var synced = VideoComposition.Synced(p, ReadScript);
        UpdateProject(synced with { Video = synced.Video!.WithScene(scene) });
    }

    /// <summary>#1004: editing a code element's program (the host has the dialogs).</summary>
    public event Action<VideoScene, VideoElement>? CodeEditRequested;

    private string? ReadScript(PublicationSourceReference reference)
    {
        try { return _catalog?.Read(reference)?.Markdown; }
        catch (NotSupportedException) { return null; }
    }

    /// <summary>The text of the block the pointer is on: the nearest text control up from where it landed.</summary>
    private static string? ShownText(object? source)
    {
        for (var v = source as Avalonia.Visual; v is not null; v = Avalonia.VisualTree.VisualExtensions.GetVisualParent(v))
            switch (v)
            {
                case SelectableTextBlock s when !string.IsNullOrWhiteSpace(s.Text ?? s.Inlines?.Text): return s.Text ?? s.Inlines?.Text;
                case TextBlock t when !string.IsNullOrWhiteSpace(t.Text ?? t.Inlines?.Text): return t.Text ?? t.Inlines?.Text;
            }
        return null;
    }

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
        _video?.Release();
        IsVisible = false;
        if (_catalog is not null) _catalog.SourceChanged -= SourceChanged;
        _catalog = null;
        Closed?.Invoke();
    }

    public void UpdateProject(PublicationProject project)
    {
        if (_project is null) return;
        // a video keeps one scene per note: new notes get a scene, removed ones take theirs along
        if (project.Design.Layout == "video") project = VideoComposition.Synced(project, ReadScript);
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

    private static Button ActionButton(string icon, string label, bool on = false)
    {
        var b = new Button
        {
            Content = UrbeTheme.Icon(icon, 19, on ? UrbeTheme.Accent : UrbeTheme.Text2),
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

    /// <summary>
    /// CST-155: an adaptation shows what it adapts, and says so when the source's text changed since it was made or last
    /// checked, with the source a tap away; nothing is updated by itself.
    /// </summary>
    private Control? Provenance(PublicationSourceView adaptation, PublicationSourceCatalog catalog)
    {
        if (EditorialNotes.Read(adaptation.Markdown) is not { } link) return null;
        PublicationSourceView? origin;
        try { origin = catalog.Read(new PublicationSourceReference(link.OriginId)); }
        catch (NotSupportedException) { origin = null; }
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(14, 8, 14, 0) };
        var name = origin is null ? "uma nota que não existe mais" : System.IO.Path.GetFileNameWithoutExtension(origin.Path);
        panel.Children.Add(new TextBlock { Text = link.Kind + " de " + name, FontSize = UrbeTokens.Type.Caption.Size, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) });
        if (origin is null) return panel;
        var actions = new WrapPanel();
        var open = new Ui.UrbeButton("Abrir a origem", Ui.ButtonKind.Ghost) { MinHeight = 40, Padding = new Thickness(10, 0) };
        open.Click += (_, _) => EditSourceRequested?.Invoke(link.OriginId);
        actions.Children.Add(open);
        if (EditorialNotes.IsBehind(link, origin.Markdown))
        {
            panel.Children.Add(new TextBlock { Text = "A origem mudou desde esta adaptação. Confira e marque como revisada.", TextWrapping = TextWrapping.Wrap, FontSize = UrbeTokens.Type.Footnote.Size, Foreground = UrbeTheme.Brush(UrbeTheme.Text) });
            var reviewed = new Ui.UrbeButton("Marcar como revisada", Ui.ButtonKind.Ghost) { MinHeight = 40, Padding = new Thickness(10, 0) };
            reviewed.Click += (_, _) => AdaptationReviewed?.Invoke(adaptation.DocumentId, link.OriginId);
            actions.Children.Add(reviewed);
        }
        panel.Children.Add(actions);
        return panel;
    }

    public void Refresh()
    {
        if (_project is not { } project || _catalog is not { } catalog) return;
        _title.Text = project.Title;
        _undoButton.IsEnabled = CanUndo;
        _redoButton.IsEnabled = CanRedo;
        _sources.Children.Clear();
        _preview.Children.Clear();
        // with a cover, the cover says the title (as in the exports)
        if (project.Cover is null)
            _preview.Children.Add(new TextBlock
            {
                Text = project.Title, FontSize = 26, FontWeight = FontWeight.Bold,
                TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text)
            });
        var missing = 0;
        var video = IsVideo;
        if (video)
        {
            try
            {
                var timeline = VideoTimeline.Build(project, catalog, PublicationVideoPanel.PreviewResolution);
                VideoPanel.Show(timeline, VideoFiles, id =>
                {
                    var piece = project.Sections.FirstOrDefault(s => s.Id == id);
                    PublicationSourceView? view = null;
                    try { view = piece is null ? null : catalog.Read(piece.Source); } catch (NotSupportedException) { }
                    return view is null ? "Fonte indisponível" : System.IO.Path.GetFileNameWithoutExtension(view.Path);
                });
                _preview.Children.Add(VideoPanel);
            }
            catch (Exception e) when (e is InvalidDataException or NotSupportedException or InvalidOperationException)
            {
                _preview.Children.Add(new TextBlock { Text = "Não foi possível montar o vídeo: " + e.Message, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
            }
        }
        for (int i = 0; i < project.Sections.Count; i++)
        {
            var piece = project.Sections[i];
            PublicationSourceView? source;
            try { source = catalog.Read(piece.Source); }
            catch (NotSupportedException) { source = null; }
            if (source is null) missing++;
            var documentId = piece.Source.DocumentId;
            var label = (piece.IsCover ? "Capa · " : "") + (source is { } found ? System.IO.Path.GetFileNameWithoutExtension(found.Path) +
                (piece.Source.Fragment is { Length: > 0 } fragment ? " #" + fragment.TrimStart('#') : "") : "Fonte indisponível");
            var item = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 3) };
            var remove = ActionButton(UrbeTheme.Icons.Trash, "Remover fonte da composição");
            remove.Click += (_, _) => { if (_project is { } p) UpdateProject(p.RemovePlacement(piece.Id)); };
            DockPanel.SetDock(remove, Dock.Right);
            item.Children.Add(remove);
            if (CanNarrate && source?.Path is { } podcastPath &&
                podcastPath.EndsWith(Urbe.Core.Study.Podcast.Suffix, StringComparison.OrdinalIgnoreCase))
            {
                var actions = new WrapPanel();
                AddAction("Editar roteiro", () => EditSourceRequested?.Invoke(documentId));
                AddAction("Gerar áudio do podcast", () => PodcastAudioRequested?.Invoke(podcastPath));
                if (FindPodcastAudio?.Invoke(podcastPath) is { } audio)
                    AddAction("Ouvir podcast", () => PlayPodcastRequested?.Invoke(audio));
                _preview.Children.Add(actions);

                void AddAction(string label, Action run)
                {
                    var button = new Ui.UrbeButton(label, Ui.ButtonKind.Ghost);
                    button.Click += (_, _) => run();
                    actions.Children.Add(button);
                }
            }
            // one way where it can move: the first goes down, the others up (a button that does nothing is not shown);
            // the cover stays at the start, so it has none and the piece after it counts as the first
            int head = project.Cover is null ? 0 : 1;
            if (project.Sections.Count - head > 1 && !piece.IsCover)
            {
                bool first = i == head;
                var move = ActionButton(first ? UrbeTheme.Icons.ArrowDown : UrbeTheme.Icons.ArrowUp, first ? "Mover fonte para baixo" : "Mover fonte para cima");
                move.Click += (_, _) =>
                {
                    if (_project is not { } p) return;
                    var at = p.Sections.FindIndex(s => s.Id == piece.Id);
                    UpdateProject(p.MovePlacement(piece.Id, first ? Math.Min(p.Sections.Count - 1, at + 1) : Math.Max(0, at - 1)));
                };
                DockPanel.SetDock(move, Dock.Right);
                item.Children.Add(move);
            }
            // CST-159: a note as the cover of a text publication (article, book); a presentation's first slide already is one
            if (project.Design.Layout is "article" or "book" && (piece.IsCover || project.Sections.Count > 1))
            {
                var cover = ActionButton(UrbeTheme.Icons.Book, piece.IsCover ? "Deixar de ser a capa" : "Usar como capa", on: piece.IsCover);
                cover.Click += (_, _) => { if (_project is { } p) UpdateProject(p.WithCover(piece.IsCover ? null : piece.Id)); };
                DockPanel.SetDock(cover, Dock.Right);
                item.Children.Add(cover);
            }
            var adapt = ActionButton(UrbeTheme.Icons.Edit, "Adaptar: resumo, roteiro, fala ou tradução");
            adapt.IsEnabled = source is not null;
            adapt.Click += (_, _) => AdaptRequested?.Invoke(piece);
            DockPanel.SetDock(adapt, Dock.Right);
            item.Children.Add(adapt);
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
            if (video) continue;   // the video panel is the preview; each note is a scene there
            if (source is not null)
            {
                var previewSource = source;
                var previewLabel = label;
                var previewReference = piece.Source;
                var isCover = piece.IsCover;
                var text = MarkdownEngine.SplitFrontmatter(previewSource.Markdown).Body;
                Control BuildPreview()
                {
                    var origin = Provenance(previewSource, catalog);
                    var shown = new Border
                    {
                        Padding = isCover ? new Thickness(14, 48) : new Thickness(14, 10),
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
                        Child = new StackPanel
                        {
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = previewLabel, FontSize = UrbeTokens.Type.Caption.Size,
                                    Foreground = UrbeTheme.Brush(UrbeTheme.Text3),
                                    IsVisible = !string.Equals(EmbedFragments.Headings(text).FirstOrDefault()?.Trim(),
                                        previewLabel, StringComparison.CurrentCultureIgnoreCase)
                                },
                                new MarkdownReader
                                {
                                    Selectable = true, ProgressContextMarkdown = previewSource.Markdown,
                                    ProgressNoteResolver = id => catalog.Read(new PublicationSourceReference(id))?.Markdown
                                }.BuildMarkdown(text)
                            }
                        }
                    };
                    shown.DoubleTapped += (_, e) =>
                    {
                        if (ShownText(e.Source) is { Length: > 0 } tapped)
                        { EditShownTextRequested?.Invoke(previewReference, tapped); e.Handled = true; }
                    };
                    if (origin is null) return shown;
                    var together = new StackPanel { Spacing = 8 };
                    together.Children.Add(origin);
                    together.Children.Add(shown);
                    return together;
                }
                // Short publications keep their immediate preview. Books and long notes
                // defer Markdown layout until the reader scrolls to that section.
                if (project.Sections.Count <= 3 && text.Length <= 2500)
                    _preview.Children.Add(BuildPreview());
                else
                    _preview.Children.Add(new Ui.Deferred(BuildPreview,
                        Math.Clamp(180 + text.Length / 55.0 * 18, 240, 2400)));
            }
            else
                _preview.Children.Add(new TextBlock { Text = "Fonte ausente — o arquivo original não será substituído.", Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
        }
        _status.Text = project.Sections.Count == 0 ? "Adicione notas do vault para começar a compor."
            : (project.Sections.Count == 1 ? "1 nota" : project.Sections.Count + " notas") +
              (missing == 0 ? "" : missing == 1 ? " · 1 não encontrada" : " · " + missing + " não encontradas");
    }
}
