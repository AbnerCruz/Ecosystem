using Urbe.Core;
using Urbe.Core.Assistant;
using Urbe.Core.Study;

namespace Urbe.Client;

/// <summary>Publicações in the real vault. No second content store or legacy PageEngine.</summary>
public sealed partial class MainView
{
    private PublicationStudioView? _publicationsStudio;
    private PublicationSourceCatalog? _publicationsCatalog;
    private DocumentStore? _publicationsCatalogStore;
    private readonly SemaphoreSlim _publicationWrites = new(1, 1);
    private string? _publicationReturnPath;
    // Uncommitted layout drafts only. Authored note content remains in DocumentStore.
    // Key by vault/store and document identity so a queued save cannot target another vault.
    private sealed class PublicationDraft(DocumentStore store, string id, string baseline, PublicationProject project)
    {
        public DocumentStore Store { get; } = store;
        public string DocumentId { get; } = id;
        public string Baseline { get; set; } = baseline;
        public PublicationProject Project { get; set; } = project;
        public bool Dirty { get; set; }
        public string? Error { get; set; }
    }
    private readonly Dictionary<(DocumentStore, string), PublicationDraft> _publicationDrafts = [];
    private PublicationDraft? _publicationDraft;

    // All publication formats share the same library, composer and .publication.json contract.
    private static readonly (string Layout, string Name, string Detail)[] PublicationKinds =
    [
        ("article", "Artigo", "Texto contínuo"),
        ("book", "Livro", "Capítulos e sumário"),
        ("slides", "Apresentação", "Uma nota por slide"),
        ("course", "Curso", "Módulos, aulas e exercícios"),
        ("video", "Vídeo", "Cenas, motion graphics e narração"),
        ("podcast", "Podcast", "Roteiro editável e exportação de áudio")
    ];

    private async Task<int?> ChoosePublicationKindAsync() =>
        await Dialogs.ChooseAsync("Formato da publicação",
            PublicationKinds.Select(k => new DialogItem(k.Name, Detail: k.Detail)).ToList());

    // Entry point used by the Publicações library and the "Nova publicação" workspace action.
    public async Task CreateNewPublicationAsync()
    {
        if (!VaultWritable || _store is null)
        {
            await Dialogs.AlertAsync("Publicações", NoVaultMessage);
            return;
        }
        var kind = await ChoosePublicationKindAsync();
        if (kind is { } selected)
            await CreatePublicationAsync(layout: PublicationKinds[selected].Layout);
    }

    public PublicationStudioView PublicationsStudio => _publicationsStudio ??= MakePublicationsStudio();

    private PublicationStudioView MakePublicationsStudio()
    {
        var studio = new PublicationStudioView();
        studio.AddSourceRequested += () => _ = AddPublicationSourceAsync();
        studio.RenameRequested += () => _ = RenamePublicationAsync();
        studio.ReviewRequested += () => _ = ReviewPublicationAsync();
        studio.SortRequested += () => _ = SortPublicationAsync();
        studio.ConfigureRequested += () => _ = ConfigurePublicationAsync();
        studio.EditSourceRequested += EditPublicationSource;
        studio.EditReferenceRequested += reference => _ = EditPublicationReferenceAsync(reference);
        studio.EditShownTextRequested += (reference, text) => _ = EditPublicationAtTextAsync(reference, text);
        studio.AdaptRequested += piece => _ = AdaptPublicationPieceAsync(piece);
        studio.AdaptationReviewed += (adaptation, origin) => _ = MarkAdaptationReviewedAsync(adaptation, origin);
        studio.ExportRequested += () => _ = ExportPublicationHtmlAsync();
        studio.EpubRequested += () => _ = ExportPublicationEpubAsync();
        studio.PackageRequested += () => _ = ExportPublicationPackageAsync();
        studio.AskAssistantRequested += AskAssistantAboutPublication;
        studio.OfficeRequested += format => _ = ExportPublicationOfficeAsync(format);
        studio.PrintRequested += () => _ = PrintPublicationAsync();
        studio.RetrySaveRequested += () => _ = SavePublicationAsync();
        WirePublicationVideo(studio);
        studio.PodcastAudioRequested += path => _ = SpeakPodcastAsync(path);
        studio.FindPodcastAudio = FindPodcastAudio;
        studio.PlayPodcastRequested += path => _ = ShowFileAsync(path);
        studio.Closed += () =>
        {
            ReleaseSavedPublicationDraft(_publicationDraft);
            UpdateFloatingNav();
        };
        studio.ProjectChanged += project =>
        {
            if (_publicationDraft is not { } draft) return;
            draft.Project = project;
            draft.Dirty = true;
            studio.SetSaveState(true);
            _ = SavePublicationDraftAsync(draft);
        };
        _stage.Children.Insert(Math.Max(0, _stage.Children.IndexOf(FilePreview)), studio);
        return studio;
    }

    /// <summary>
    /// CST-155 "Adaptar": a real note beside the source (named after it and the kind), starting from its text and linked
    /// to it, takes the piece's place in the publication. The source is not touched.
    /// </summary>
    public async Task AdaptPublicationPieceAsync(PublicationPlacement piece)
    {
        if (_store?.Get(piece.Source.DocumentId) is not { } origin) return;
        if (await Dialogs.ChooseAsync("Adaptar " + System.IO.Path.GetFileNameWithoutExtension(origin.Path),
                EditorialNotes.Kinds.Select(k => new DialogItem(k)).ToList(),
                "Uma nota nova, ao lado da origem, começa com o texto dela. A origem não muda; a publicação passa a mostrar a adaptação.") is not int kind) return;
        var folder = System.IO.Path.GetDirectoryName(origin.Path)?.Replace('\\', '/') ?? "";
        var name = System.IO.Path.GetFileNameWithoutExtension(origin.Path) + " — " + EditorialNotes.Kinds[kind];
        var content = EditorialNotes.Create(origin.Id, origin.Path, origin.Content, EditorialNotes.Kinds[kind]);
        var free = _store.Get((folder.Length > 0 ? folder + "/" : "") + name + ".md") is null;
        var path = await CreateNoteInNotesAsync(free ? name : null, folder, _ => Task.FromResult<string?>(content), open: false);
        if (path is null || _store.Get(path) is not { } adaptation || PublicationsStudio.Project is not { } project ||
            !project.Sections.Any(s => s.Id == piece.Id)) return;
        PublicationsStudio.UpdateProject(project.ReplacePlacementSource(piece.Id, new PublicationSourceReference(adaptation.Id)));
        _notice.Show("Adaptação criada em " + path + ".", TimeSpan.FromSeconds(8), "Abrir", () => EditPublicationSource(adaptation.Id));
    }

    /// <summary>CST-155: the adaptation was checked against the source as it is now (only its property changes).</summary>
    public async Task MarkAdaptationReviewedAsync(string adaptationId, string originId)
    {
        if (_store?.Get(adaptationId) is not { } adaptation || _store.Get(originId) is not { } origin) return;
        await SaveNoteAsync(new CityNote(adaptation.Path, EditorialNotes.Reviewed(adaptation.Content, origin.Content)));
        PublicationsStudio.Refresh();
    }

    /// <summary>CST-160: the notes of the publication ordered by title, by date or grouped by folder (the cover stays first; undoable).</summary>
    public async Task SortPublicationAsync()
    {
        if (_publicationsStudio?.Project is not { } project || _store is not { } store) return;
        string[] kinds = ["titulo", "data", "pasta"];
        var pick = await Dialogs.ChooseAsync("Ordenar as notas",
            [new DialogItem("Pelo título", Detail: "De A a Z"), new DialogItem("Pela data", Detail: "Da mudança mais antiga para a mais nova"),
             new DialogItem("Pela pasta", Detail: "Agrupadas por pasta, pelo título dentro de cada uma")]);
        if (pick is not { } i || !ReferenceEquals(_publicationsStudio?.Project, project)) return;
        _publicationsStudio.UpdateProject(project.SortedBy(kinds[i], store.Get));
    }

    /// <summary>Open an existing project by path, preserving the current unsaved note first.</summary>
    /// <summary>
    /// CST-160: a publication made from a tag, folder or search shows the notes that match it now and it does not use
    /// yet; the person adds them or leaves them out (remembered), and the file is saved before the studio opens.
    /// </summary>
    private async Task<PublicationProject> ReviewQueryMatchesAsync(string path, PublicationProject project)
    {
        if (project.Query is not { } query || _store is null || !VaultWritable) return project;
        using var index = new KnowledgeIndex(_store);
        var found = query.NewMatches(project, _store.List(), q => index.Search(q));
        // and the other way: notes it uses that stopped matching, told for review (never taken out by themselves)
        var gone = query.NoLongerMatching(project, _store.Get, q => index.Search(q));
        if (found.Count == 0 && gone.Count == 0) return project;
        var what = query.Kind switch { "tag" => "com a tag #" + query.Value.TrimStart('#'), "folder" => "na pasta " + query.Value, _ => "para “" + query.Value + "”" };
        var next = project;
        if (gone.Count > 0)
            next = await Dialogs.ConfirmAsync(gone.Count + (gone.Count == 1 ? " nota da publicação não está mais " : " notas da publicação não estão mais ") + what,
                string.Join("\n", gone.Take(12).Select(d => "• " + d.Title)) + (gone.Count > 12 ? "\n… e mais " + (gone.Count - 12) : "") +
                "\n\nTirar da publicação? As notas continuam no vault. Se mantiver, não pergunto de novo.", "Tirar da publicação", cancel: "Manter")
                ? gone.Aggregate(next, (p, d) => p.RemoveSource(d.Id))
                : next with { Query = query with { Kept = [.. query.Kept, .. gone.Select(d => d.Id)] } };
        if (found.Count == 0) return await SaveQueryReviewAsync(path, project, next);
        var add = await Dialogs.ConfirmAsync(found.Count + (found.Count == 1 ? " nota nova " : " notas novas ") + what,
            string.Join("\n", found.Take(12).Select(d => "• " + d.Title)) + (found.Count > 12 ? "\n… e mais " + (found.Count - 12) : "") +
            "\n\nAcrescentar à publicação? Se não, elas não serão oferecidas de novo.", "Acrescentar", cancel: "Deixar de fora");
        next = add ? found.Aggregate(next, (p, d) => p.AddSource(d.Id))
            : next with { Query = (next.Query ?? query) with { Declined = [.. query.Declined, .. found.Select(d => d.Id)] } };
        return await SaveQueryReviewAsync(path, project, next);
    }

    private async Task<PublicationProject> SaveQueryReviewAsync(string path, PublicationProject project, PublicationProject next)
    {
        if (ReferenceEquals(project, next) || _store is null) return project;
        try { await ((IAssistantApp)this).SaveNoteAsync(_store.Get(path)!, PublicationProjectCodec.Serialize(next)); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            await Dialogs.AlertAsync("Publicações", StorageProblems.Explain(ex));
            return project;
        }
        return next;
    }

    public async Task OpenPublicationAsync(string path)
    {
        UiStallWatch.Doing("abrir uma publicação");
        // Reopening during an outstanding write must not reload its stale on-disk state.
        await _publicationWrites.WaitAsync();
        _publicationWrites.Release();
        if (_store?.Get(path) is not { } file) { ShowLifeNotice("Publicação não encontrada: " + path); return; }
        PublicationProject project;
        try { project = PublicationProjectCodec.Parse(file.Content); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or ArgumentException)
        {
            await Dialogs.AlertAsync("Não foi possível abrir a publicação", e.Message);
            return;
        }
        project = await ReviewQueryMatchesAsync(path, project);
        if (Editor.IsVisible)
        {
            Editor.FinishEditing();
            if (Editor.HasChanges && !await Editor.SaveAsync())
            {
                ShowLifeNotice("Salve a nota antes de abrir Publicações.", Ui.ToastKind.Warning);
                return;
            }
        }
        if (_publicationsCatalogStore != _store)
        {
            _publicationsCatalog?.Dispose();
            _publicationsCatalog = new PublicationSourceCatalog(_store);
            _publicationsCatalogStore = _store;
        }
        _pagesPanel?.Close();
        ReleaseSavedPublicationDraft(_publicationDraft);
        var key = (_store!, file.Id);
        if (!_publicationDrafts.TryGetValue(key, out var draft) || !draft.Dirty)
        {
            draft = new PublicationDraft(_store!, file.Id, file.Content, project);
            _publicationDrafts[key] = draft;
        }
        _publicationDraft = draft;
        HideExplorerTip();
        PublicationsStudio.Open(path, draft.Project, _publicationsCatalog!);
        TellChangesSinceExport(draft.Project);
        PublicationsStudio.SetSaveState(draft.Dirty, draft.Error);
        UpdateFloatingNav();
        await LoadPublicationVideoFilesAsync();
    }

    /// <summary>
    /// First-class creation from the current note, a folder, chosen notes (a selection, search results), a tag or one
    /// section of a note (<paramref name="fragment"/>: a heading). Every source is a reference to a real note, never a
    /// copy (CST-135).
    /// </summary>
    public async Task CreatePublicationAsync(string? sourcePath = null, string? sourceFolder = null, IReadOnlyList<string>? sourcePaths = null,
        string? tag = null, string? fragment = null, PublicationQuery? query = null, PublicationProject? style = null, bool podcast = false, string? layout = null)
    {
        if (!VaultWritable || _store is null)
        {
            await Dialogs.AlertAsync("Publicações", NoVaultMessage);
            return;
        }
        // The podcast is a publication format, not a separate publication subsystem.
        podcast |= layout == "podcast";
        var source = sourcePath is null ? null : _store.Get(sourcePath);
        if (source is not null && !ArtifactModel.IsNote(source.Path)) source = null;
        var originalStore = _store;
        var originalVault = _vault;
        var folder = sourceFolder?.Trim('/');
        var suggested = fragment ?? source?.Title ?? (folder is { Length: > 0 } ? System.IO.Path.GetFileName(folder) : null) ?? (tag is { Length: > 0 } ? "#" + tag.TrimStart('#') : null);
        var title = await Dialogs.PromptAsync(new PromptOptions(podcast ? "Novo podcast" : "Nova publicação", Label: "Título", Placeholder: suggested ?? (podcast ? "Ex.: Conversas sobre ciência" : "Ex.: Meu livro"), Confirm: "Criar"));
        if (string.IsNullOrWhiteSpace(title)) return;
        if (!ReferenceEquals(_store, originalStore) || !ReferenceEquals(_vault, originalVault))
        {
            ShowLifeNotice("O vault mudou. Abra a criação novamente no vault desejado.", Ui.ToastKind.Warning);
            return;
        }
        var project = style?.StyleFor(title.Trim()) ?? PublicationProject.Create(title.Trim());
        if (podcast) layout = "podcast";
        if (layout is not null)
            project = project with { Design = project.Design with { Layout = layout } };
        if (fragment is { Length: > 0 } && source is not null) project = project.AddSource(new PublicationSourceReference(source.Id, fragment));
        else if (source is not null) project = project.AddSource(source.Id);
        foreach (var p in sourcePaths ?? [])
            if (_store.Get(p) is { } chosen && ArtifactModel.IsNote(chosen.Path)) project = project.AddSource(chosen.Id);
        if (tag is { Length: > 0 })
        {
            var wanted = tag.TrimStart('#');
            foreach (var note in _store.List().Where(d => ArtifactModel.IsNote(d.Path) && d.Tags.Any(t => string.Equals(t.TrimStart('#'), wanted, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase))
                project = project.AddSource(note.Id);
        }
        if (folder is not null)
        {
            var prefix = folder.Length == 0 ? "" : folder + "/";
            foreach (var note in _store.List().Where(d => ArtifactModel.IsNote(d.Path) && d.Path.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase))
                project = project.AddSource(note.Id);
        }
        query ??= tag is { Length: > 0 } ? new PublicationQuery("tag", tag.TrimStart('#')) : folder is { Length: > 0 } ? new PublicationQuery("folder", folder) : null;
        if (query is not null) project = project with { Query = query };
        if (project.Design.Layout == "video")
            project = VideoComposition.Synced(project, reference => _store.Get(reference.DocumentId)?.Content);
        if (project.Sections.Count == 0 && (sourcePaths is { Count: > 0 } || tag is { Length: > 0 }))
        {
            await Dialogs.AlertAsync("Publicações", tag is { Length: > 0 } ? "Nenhuma nota tem a tag #" + tag.TrimStart('#') + "." : "Nenhuma das escolhidas é uma nota.");
            return;
        }
        const string publicationSuffix = ".publication.json";
        var basePath = "Publicações/" + ArtifactModel.SafeName(title.Trim());
        var stem = basePath;
        var path = stem + publicationSuffix;
        for (var n = 2; Taken(path) || podcast && Taken(stem + Podcast.Suffix); n++)
        {
            stem = basePath + "-" + n;
            path = stem + publicationSuffix;
        }

        bool Taken(string candidate) => originalStore.Get(candidate) is not null || _attachments.Contains(candidate);

        try
        {
            var app = (IAssistantApp)this;
            if (podcast)
            {
                var script = await app.CreateNoteAsync(stem + Podcast.Suffix,
                    "# " + title.Trim() + "\n\n<!-- Escreva cada fala em um parágrafo no formato **Nome:** texto. -->\n");
                project = project.AddSource(script.Id);
                if (!ReferenceEquals(_store, originalStore) || !ReferenceEquals(_vault, originalVault)) return;
            }
            await app.CreateNoteAsync(path, PublicationProjectCodec.Serialize(project));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            await Dialogs.AlertAsync("Não foi possível criar a publicação", StorageProblems.Explain(ex));
            return;
        }
        await OpenPublicationAsync(path);
    }

    /// <summary>
    /// CST-157/159: an old page turned into a publication, on command. Its text becomes real notes in
    /// Publicações/&lt;título&gt;/, its notes are referenced, and a report says what had no equivalent. The page file is not touched.
    /// </summary>
    public async Task ConvertPageToPublicationAsync(string pagePath)
    {
        if (!VaultWritable || _store is null || _store.Get(pagePath) is not { } file) return;
        var page = PageDocument.Parse(file.Content);
        if (page.State is PageDocumentState.Corrupt) { await Dialogs.AlertAsync("Converter em publicação", "A página não pôde ser lida; o arquivo fica como está."); return; }
        var store = _store;
        var result = PageToPublication.Convert(page, path => store.Get(path) is { } d && ArtifactModel.IsNote(d.Path) ? d.Id : null);
        if (result.Pieces.Count == 0) { await Dialogs.AlertAsync("Converter em publicação", "Nada desta página tem equivalente numa publicação.\n\n" + string.Join("\n", result.Report)); return; }
        var title = page.Title.Trim();
        var folder = "Publicações/" + ArtifactModel.SafeName(title);
        var project = PublicationProject.Create(title);
        int part = 0, texts = result.Pieces.Count(p => p.Markdown is not null);
        foreach (var piece in result.Pieces)
        {
            if (piece.NoteId is { } id) { project = project.AddSource(id); continue; }
            part++;
            var name = texts == 1 ? title : title + " — parte " + part;
            var created = await CreateNoteInNotesAsync(store.Get(folder + "/" + ArtifactModel.SafeName(name) + ".md") is null ? ArtifactModel.SafeName(name) : null,
                folder, _ => Task.FromResult<string?>(piece.Markdown), open: false);
            if (created is null || store.Get(created) is not { } note) return;   // nothing half made is hidden: the notes made so far stay
            project = project.AddSource(note.Id);
        }
        var target = "Publicações/" + ArtifactModel.SafeName(title) + ".publication.json";
        for (var n = 2; store.Get(target) is not null; n++) target = "Publicações/" + ArtifactModel.SafeName(title) + "-" + n + ".publication.json";
        try { await ((IAssistantApp)this).CreateNoteAsync(target, PublicationProjectCodec.Serialize(project)); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        { await Dialogs.AlertAsync("Não foi possível criar a publicação", StorageProblems.Explain(ex)); return; }
        await OpenPublicationAsync(target);
        await Dialogs.AlertAsync("Página convertida", (texts > 0 ? (texts == 1 ? "1 nota" : texts + " notas") + " com os textos da página em " + folder + ".\n" : "") +
            "A página antiga continua em " + pagePath + ", sem mudança.\n\n" + string.Join("\n", result.Report));
    }

    /// <summary>CST-135: a publication of the notes with a tag (chosen from the vault's tags).</summary>
    public async Task CreatePublicationFromTagAsync()
    {
        if (_store is null) return;
        var tags = _store.List().Where(d => ArtifactModel.IsNote(d.Path)).SelectMany(d => d.Tags).Select(t => t.TrimStart('#'))
            .GroupBy(t => t, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Tag: g.First(), Count: g.Count())).ToList();
        if (tags.Count == 0) { await Dialogs.AlertAsync("Publicações", "O vault não tem tags."); return; }
        var choice = await Dialogs.ChooseAsync("Publicação de uma tag", tags.Select(t => new DialogItem("#" + t.Tag, Detail: t.Count + (t.Count == 1 ? " nota" : " notas"))).ToList());
        if (choice is not { } k) return;
        await CreatePublicationAsync(tag: tags[k].Tag);
    }

    /// <summary>CST-135: a publication of one section of a note (a heading and what is under it), still the note's text.</summary>
    /// <summary>CST-161: a new publication in the style of an existing one (its look and audience, none of its notes).</summary>
    public async Task CreatePublicationInStyleAsync()
    {
        if (_store is null) { await Dialogs.AlertAsync("Publicações", NoVaultMessage); return; }
        var existing = _store.List().Where(d => d.Path.EndsWith(".publication.json", StringComparison.OrdinalIgnoreCase)).OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList();
        if (existing.Count == 0) { await Dialogs.AlertAsync("Publicações", "Ainda não há publicações para servir de modelo."); return; }
        if (await Dialogs.ChooseAsync("No estilo de qual publicação?", existing.Select(d => new DialogItem(System.IO.Path.GetFileName(d.Path)[..^".publication.json".Length], Detail: d.Path)).ToList(),
                "A nova leva o layout, o tema, a cor, a letra e o público; nenhuma nota.") is not { } i) return;
        PublicationProject model;
        try { model = PublicationProjectCodec.Parse(existing[i].Content); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or ArgumentException or System.Text.Json.JsonException)
        { await Dialogs.AlertAsync("Publicações", "Não foi possível ler essa publicação: " + e.Message); return; }
        await CreatePublicationAsync(style: model);
    }

    /// <summary>CST-135: a publication from a search: the notes that match, after the person sees and confirms them.</summary>
    public async Task CreatePublicationFromSearchAsync()
    {
        if (_store is null) { await Dialogs.AlertAsync("Publicações", NoVaultMessage); return; }
        var query = await Dialogs.PromptAsync(new PromptOptions("Publicação de uma busca", Label: "Buscar", Placeholder: "Ex.: fotossíntese", Confirm: "Buscar"));
        if (string.IsNullOrWhiteSpace(query)) return;
        using var index = new KnowledgeIndex(_store);
        var found = index.Search(query).Where(d => ArtifactModel.IsNote(d.Path)).ToList();
        if (found.Count == 0) { await Dialogs.AlertAsync("Publicações", "Nenhuma nota encontrada para “" + query.Trim() + "”."); return; }
        var shown = string.Join("\n", found.Take(12).Select(d => "• " + d.Title)) + (found.Count > 12 ? "\n… e mais " + (found.Count - 12) : "");
        if (!await Dialogs.ConfirmAsync(found.Count + (found.Count == 1 ? " nota" : " notas") + " para “" + query.Trim() + "”", shown, "Criar publicação")) return;
        await CreatePublicationAsync(sourcePaths: found.Select(d => d.Path).ToList(), query: new PublicationQuery("search", query.Trim()));
    }

    public async Task CreatePublicationFromSectionAsync(string notePath)
    {
        if (Editor.Current?.Path == notePath && Editor.HasChanges && !await Editor.SaveAsync()) return;
        if (_store?.Get(notePath) is not { } note) return;
        var headings = Urbe.Core.MarkdownEngine.SplitFrontmatter(note.Content).Body.Replace("\r", "").Split('\n')
            .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^#{1,6}\s+\S")).Select(l => l.TrimStart('#').Trim()).Distinct().ToList();
        // CST-135: what is selected in the editor is the passage, when it is one paragraph of the note
        var selected = ((IAssistantApp)this).EditorNote() is { Path: var open, Selection: { Length: > 0 } text } && open == notePath
            && note.Content.Replace("\r\n", "\n").IndexOf(text.Trim(), StringComparison.Ordinal) is >= 0 and var at ? at : -1;
        var items = headings.Select(h => new DialogItem(h)).ToList();
        if (selected >= 0) items.Insert(0, new DialogItem("O parágrafo selecionado", UrbeTheme.Icons.Page, "Marcado na nota com um ^id, como os links de bloco."));
        if (items.Count == 0) { await Dialogs.AlertAsync("Publicações", "Esta nota não tem títulos. Selecione um parágrafo no editor e escolha de novo."); return; }
        var choice = await Dialogs.ChooseAsync("Publicação de um trecho", items, "O trecho continua sendo o texto da nota: um título e o que vem embaixo dele, ou o parágrafo selecionado.");
        if (choice is not { } k) return;
        if (selected < 0 || k > 0) { await CreatePublicationAsync(sourcePath: notePath, fragment: headings[k - (selected >= 0 ? 1 : 0)]); return; }
        if (Urbe.Core.EmbedFragments.AnchorBlock(note.Content, selected, () => "pub-" + Guid.NewGuid().ToString("N")[..6]) is not { } anchored)
        {
            await Dialogs.AlertAsync("Publicações", "A seleção não está num parágrafo (é um título, código ou linha vazia).");
            return;
        }
        if (anchored.Markdown != note.Content.Replace("\r\n", "\n"))
        {
            await ((IAssistantApp)this).SaveNoteAsync(_store.Get(notePath)!, anchored.Markdown);
            if (Editor.Current?.Path == notePath) Editor.Open(new CityNote(notePath, anchored.Markdown));
        }
        await CreatePublicationAsync(sourcePath: notePath, fragment: "^" + anchored.Id);
    }

    /// <summary>The user chooses a project by title, not by raw JSON path.</summary>
    public async Task ShowPublicationsAsync()
    {
        if (_store is null)
        {
            await Dialogs.AlertAsync("Publicações", NoVaultMessage);
            return;
        }
        var projects = _store.List().Where(d => d.Path.EndsWith(".publication.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase).ToList();
        var options = projects.Select(d =>
        {
            try { return new DialogItem(PublicationProjectCodec.Parse(d.Content).Title, Detail: d.Path); }
            catch (Exception e) when (e is InvalidDataException or NotSupportedException)
            { return new DialogItem(d.Title + " (verificar arquivo)", Detail: d.Path); }
        }).ToList();
        options.Add(new DialogItem("Nova publicação", UrbeTheme.Icons.Plus,
            "Artigo, livro, apresentação, curso, vídeo ou podcast"));
        var chosen = await Dialogs.ChooseAsync("Publicações", options);
        if (chosen is not { } index) return;
        if (index == projects.Count) await CreateNewPublicationAsync();
        else await OpenPublicationAsync(projects[index].Path);
    }

    private async Task AddPublicationSourceAsync()
    {
        var draft = _publicationDraft;
        var project = _publicationsStudio?.Project;
        if (project is null || _store is null) return;
        var notes = _store.List().Where(d => ArtifactModel.IsNote(d.Path))
            .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList();
        if (notes.Count == 0)
        {
            await Dialogs.AlertAsync("Adicionar nota", "Não há notas disponíveis para adicionar.");
            return;
        }
        var chosen = await Dialogs.ChooseAsync("Selecionar nota de origem", notes.Select(n => new DialogItem(n.Title, Detail: n.Path)).ToList());
        if (chosen is not { } i) return;
        var note = notes[i];
        var fragments = new List<string?> { null };
        fragments.AddRange(EmbedFragments.Headings(note.Content).Distinct(StringComparer.Ordinal));
        fragments.AddRange(EmbedFragments.BlockIds(note.Content).Distinct(StringComparer.Ordinal).Select(id => "^" + id));
        int? part = 0;
        if (fragments.Count > 1)
            part = await Dialogs.ChooseAsync("Conteúdo de origem", fragments.Select(f => new DialogItem(f is null ? "Nota inteira" : "#" + f)).ToList());
        if (part is { } index && ReferenceEquals(_publicationDraft, draft) &&
            ReferenceEquals(_store, draft?.Store) && _publicationsStudio?.IsVisible == true)
        {
            var reference = new PublicationSourceReference(note.Id, fragments[index]);
            if (_publicationsCatalog?.Read(reference) is null)
            { ShowLifeNotice("A origem mudou. Selecione o trecho novamente.", Ui.ToastKind.Warning); return; }
            _publicationsStudio.AddSource(reference);
        }
    }

    /// <summary>
    /// CST-156: editing a piece's source opens the note where the person wants to change it — one of its blocks, mapped
    /// to its lines (<see cref="VisualDocumentEditor.Locate"/>) — instead of always at the top of a long note.
    /// </summary>
    public async Task EditPublicationReferenceAsync(PublicationSourceReference reference)
    {
        if (_store?.Get(reference.DocumentId) is not { } note || !ArtifactModel.IsNote(note.Path)) return;
        int? line = null;
        if (reference.Fragment is not { Length: > 0 } && VisualDocumentEditor.Locate(note.Content) is { Count: > 2 } blocks)
        {
            static string Preview(VisualBlock b) { var t = b.Text.Replace('\n', ' ').Trim(); return t.Length > 70 ? t[..70] + "…" : t; }
            var pick = await Dialogs.ChooseAsync("Editar onde?", [new DialogItem("Início da nota"), .. blocks.Select(b => new DialogItem(Preview(b.Block), Detail: "linha " + b.FirstLine))]);
            if (pick is null) return;
            if (pick > 0) line = blocks[pick.Value - 1].FirstLine;
        }
        EditPublicationSource(reference.DocumentId);
        if (Editor.Current?.Path != note.Path) return;
        if (reference.Fragment is { Length: > 0 } fragment) Editor.RevealFragment(fragment);
        else if (line is { } at) Editor.RevealLine(at);
    }

    /// <summary>CST-156: the note opens at the block that shows the tapped text; when it cannot tell which, it asks (“Editar onde?”).</summary>
    public async Task EditPublicationAtTextAsync(PublicationSourceReference reference, string shown)
    {
        if (_store?.Get(reference.DocumentId) is not { } note || !ArtifactModel.IsNote(note.Path)) return;
        if (VisualDocumentEditor.LineShowing(note.Content, shown) is not { } line) { await EditPublicationReferenceAsync(reference); return; }
        EditPublicationSource(reference.DocumentId);
        if (Editor.Current?.Path == note.Path) Editor.RevealLine(line);
    }

    private void EditPublicationSource(string documentId)
    {
        if (_store?.Get(documentId) is not { } note || !ArtifactModel.IsNote(note.Path)) return;
        _publicationReturnPath = _publicationsStudio?.ProjectPath;
        _publicationsStudio?.Close();
        UpdateFloatingNav();
        OpenNoteByPath(note.Path); // the same editor, history, undo and vault writer as all notes
    }

    public Task ReturnToPublicationAsync() =>
        _publicationReturnPath is { } path ? OpenPublicationAsync(path) : Task.CompletedTask;

    private async Task RenamePublicationAsync()
    {
        var draft = _publicationDraft;
        if (_publicationsStudio?.Project is not { } project) return;
        var name = await Dialogs.PromptAsync(new PromptOptions("Renomear publicação", Label: "Título", Placeholder: project.Title, Confirm: "Salvar"));
        if (!string.IsNullOrWhiteSpace(name) && ReferenceEquals(_publicationDraft, draft) &&
            ReferenceEquals(_store, draft?.Store) && _publicationsStudio.IsVisible)
            _publicationsStudio.UpdateProject(_publicationsStudio.Project! with { Title = name.Trim() });
    }

    private async Task ConfigurePublicationAsync()
    {
        var draft = _publicationDraft;
        if (_publicationsStudio?.Project is not { } project) return;
        var option = await Dialogs.ChooseAsync("Composição", [new DialogItem("Layout", Detail: "Artigo, livro, apresentação, curso, vídeo ou podcast"),
            new DialogItem("Tema", Detail: "Papel, oceano ou tinta"), new DialogItem("Objetivo", Detail: project.Objective), new DialogItem("Público", Detail: project.Audience),
            .. project.Design.Layout == "video" && project.Video is { } v
                ? new[] { new DialogItem("Formato do vídeo", Detail: v.Aspect + " · " + v.FrameSize.Width + "×" + v.FrameSize.Height + " · " + v.Fps + " qps" + (v.Subtitles ? " · legendas" : "")) }
                : []]);
        PublicationProject? next = null;
        if (option == 0)
        {
            var kind = await ChoosePublicationKindAsync();
            if (kind is { } selected)
                next = project with { Design = project.Design with { Layout = PublicationKinds[selected].Layout } };
        }
        else if (option == 1)
        {
            // CST-161: a theme, the person's own accent, and the type; every output reads them (PublicationTheme)
            var tokens = PublicationTheme.Of(project.Design);
            var theme = await Dialogs.ChooseAsync("Tema", [
                .. PublicationTheme.Themes.Select(t => new DialogItem(t.Name, Detail: t.Accent)),
                new DialogItem("Cor própria", Detail: project.Design.Accent ?? "a do tema: " + tokens.Accent),
                new DialogItem("Letra sem serifa"), new DialogItem("Letra serifada", Detail: "como nos livros"), new DialogItem("Letra monoespaçada")]);
            int themes = PublicationTheme.Themes.Count;
            if (theme is { } t && t < themes) next = project with { Design = project.Design with { Theme = PublicationTheme.Themes[t].Id, Accent = null } };
            else if (theme == themes && await Dialogs.PromptAsync(new PromptOptions("Cor própria", Label: "Cor em hexadecimal", Value: tokens.Accent, Placeholder: "#0b6e4f", Confirm: "Usar",
                         Validate: v => PublicationTheme.ValidAccent(v.Trim()) ? null : "Escreva # e seis dígitos, como #0b6e4f.")) is { } own)
                next = project with { Design = project.Design with { Accent = own.Trim().ToLowerInvariant() } };
            else if (theme is { } f && f > themes) next = project with { Design = project.Design with { Font = new[] { "sans", "serif", "mono" }[f - themes - 1] } };
        }
        else if (option is 2 or 3)
        {
            var text = await Dialogs.PromptAsync(new PromptOptions(option == 2 ? "Objetivo da publicação" : "Público da publicação", Value: option == 2 ? project.Objective : project.Audience, Required: false, Confirm: "Salvar"));
            if (text is not null) next = option == 2 ? project with { Objective = text } : project with { Audience = text };
        }
        else if (option == 4 && project.Video is { } video)
        {
            var choice = await Dialogs.ChooseAsync("Formato do vídeo", [
                new DialogItem("Horizontal 16:9"), new DialogItem("Vertical 9:16"), new DialogItem("Quadrado 1:1"),
                new DialogItem("Resolução", Detail: video.Resolution + "p"), new DialogItem("Quadros por segundo", Detail: video.Fps.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new DialogItem(video.Subtitles ? "Tirar as legendas do vídeo" : "Pôr as legendas no vídeo")]);
            if (choice is >= 0 and <= 2) next = project with { Video = video with { Aspect = VideoComposition.Aspects[choice.Value] } };
            else if (choice == 3 && await Dialogs.ChooseAsync("Resolução", [new DialogItem("480p"), new DialogItem("720p"), new DialogItem("1080p")]) is { } r)
                next = project with { Video = video with { Resolution = new[] { 480, 720, 1080 }[r] } };
            else if (choice == 4 && await Dialogs.ChooseAsync("Quadros por segundo", [new DialogItem("24"), new DialogItem("30"), new DialogItem("60")]) is { } f)
                next = project with { Video = video with { Fps = new[] { 24, 30, 60 }[f] } };
            else if (choice == 5) next = project with { Video = video with { Subtitles = !video.Subtitles } };
        }
        if (next is not null && ReferenceEquals(draft, _publicationDraft) && ReferenceEquals(_store, draft?.Store) &&
            _publicationsStudio.IsVisible && ReferenceEquals(project, _publicationsStudio.Project))
            _publicationsStudio.UpdateProject(next);
    }

    /// <summary>Explicit retry/flush: false means the composition is still only a session draft.</summary>
    public Task<bool> SavePublicationAsync() => _publicationDraft is { } draft
        ? SavePublicationDraftAsync(draft) : Task.FromResult(false);

    private void ReleaseSavedPublicationDraft(PublicationDraft? draft)
    {
        if (draft is null || draft.Dirty) return;
        var key = (draft.Store, draft.DocumentId);
        if (_publicationDrafts.TryGetValue(key, out var retained) && ReferenceEquals(retained, draft))
            _publicationDrafts.Remove(key);
        if (ReferenceEquals(_publicationDraft, draft)) _publicationDraft = null;
    }

    private async Task<bool> SavePublicationDraftAsync(PublicationDraft draft)
    {
        await _publicationWrites.WaitAsync();
        try
        {
            if (!draft.Dirty) return true;
            if (!ReferenceEquals(_store, draft.Store))
                throw new InvalidOperationException("Volte ao vault de origem para salvar este rascunho.");
            if (draft.Store.Get(draft.DocumentId) is not { } file)
                throw new InvalidOperationException("Publicação não está mais no vault.");
            if (file.Content != draft.Baseline)
                throw new InvalidOperationException("A publicação foi alterada por outra operação. A versão do arquivo foi preservada; compare-a com seu rascunho antes de substituir.");
            var project = draft.Project;
            var json = PublicationProjectCodec.Serialize(project);
            if (file.Content != json) await ((IAssistantApp)this).SaveNoteAsync(file, json);
            draft.Baseline = json;
            draft.Dirty = !ReferenceEquals(project, draft.Project);
            draft.Error = null;
            return !draft.Dirty;
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException or InvalidDataException)
        {
            draft.Error = e.Message;
            ShowLifeNotice("Não consegui salvar a Publicação: " + e.Message, Ui.ToastKind.Warning);
            return false;
        }
        finally
        {
            if (ReferenceEquals(_publicationDraft, draft))
                _publicationsStudio?.SetSaveState(draft.Dirty, draft.Error);
            // Successful closed projects need no recovery cache retaining an entire old vault.
            if (_publicationsStudio?.IsVisible != true) ReleaseSavedPublicationDraft(draft);
            _publicationWrites.Release();
        }
    }

    private async Task<(PublicationProject Project, PublicationHtmlResult Render)?> PreparePublicationOutputAsync()
    {
        if (_publicationsStudio?.Project is null || _publicationsCatalog is not { } catalog ||
            _publicationDraft is not { } draft) return null;
        if (!await SavePublicationAsync()) return null;
        if (!ReferenceEquals(_publicationDraft, draft) || !ReferenceEquals(_store, draft.Store) || draft.Dirty) return null;
        var project = draft.Project;
        PublicationHtmlResult result;
        var data = await PublicationDataAsync(project, catalog);
        try { result = PublicationHtml.Export(project, catalog, data); }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            await Dialogs.AlertAsync("Exportação não disponível", ex.Message);
            return null;
        }
        if (!result.IsComplete)
        {
            await Dialogs.AlertAsync("Fontes indisponíveis", "Resolva as notas ausentes antes de exportar: " + result.MissingDocumentIds.Count + " referência(s).");
            return null;
        }
        return (project, result with { Html = await EmbedVaultImagesAsync(result.Html) });
    }

    /// <summary>A file a note names (image, audio, video): the exact vault path, else the one file with that name (as ![[foto.png]] finds it).</summary>
    private async Task<byte[]?> ReadVaultImageAsync(string reference) =>
        VaultFileNamed(reference) is { } path && _vault is { } vault ? (await vault.ReadFileAsync(path, FilePreviewPanel.MaxBytes))?.Bytes : null;

    private string? VaultFileNamed(string reference) => _attachments.Contains(reference) ? reference
        : _attachments.FirstOrDefault(a => string.Equals(System.IO.Path.GetFileName(a), System.IO.Path.GetFileName(reference), StringComparison.OrdinalIgnoreCase));

    /// <summary>CST-162: the publication's images read from the vault and carried inside the HTML (works offline, anywhere).</summary>
    private async Task<string> EmbedVaultImagesAsync(string html)
    {
        var read = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var reference in PublicationAssets.References(html))
        {
            read[reference] = await ReadVaultImageAsync(Uri.UnescapeDataString(reference));
        }
        return PublicationAssets.Embed(html, r => read.GetValueOrDefault(r));
    }

    /// <summary>Uses the platform's print service; opening its dialog is not proof that a PDF was saved.</summary>
    public async Task PrintPublicationAsync()
    {
        if (ContentViewers.Printer is not { } print)
        {
            ShowLifeNotice("Impressão indisponível neste aparelho.", Ui.ToastKind.Warning);
            return;
        }
        if (await PreparePublicationOutputAsync() is not { } output) return;
        try
        {
            await print(output.Project.Title, output.Render.Html, new PagePrintSettings(output.Project.Design.Layout == "slides", "a4"));
            ShowLifeNotice(OperatingSystem.IsAndroid()
                ? "Na impressão do sistema, escolha “Salvar como PDF” para gerar o arquivo."
                : "No navegador aberto, use Imprimir (Ctrl+P) e escolha “Salvar como PDF”.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { ShowLifeNotice("Não consegui preparar a impressão: " + ex.Message, Ui.ToastKind.Warning); }
    }

    public async Task ExportPublicationEpubAsync()
    {
        if (_publicationsCatalog is not { } catalog || _publicationDraft is not { } draft || !await SavePublicationAsync()) return;
        var project = draft.Project;
        try
        {
            var data = await PublicationDataAsync(project, catalog);
            var result = await PublicationEpub.ExportWithReportAsync(project, catalog,
                ReadVaultImageAsync, data: data);
            if (!ReferenceEquals(draft, _publicationDraft) || !ReferenceEquals(draft.Store, _store) || draft.Dirty) return;
            var name = ArtifactModel.SafeName(project.Title) + ".epub";
            LastEpubWarnings = result.Warnings;
            var receipt = EnsureOpens("epub", result.Bytes);
            var saved = SavePageFile is { } save ? await save(name, result.Bytes) : await SavePublicationFileAsync(name, result.Bytes, "epub", "application/epub+zip");
            if (saved != true) return;
            RecordPublicationExport("epub");
            // CST-183: what keeps the book from being fully accessible is said, with how to fix it in the note
            if (result.Warnings.Count > 0)
                await Dialogs.AlertAsync("Livro EPUB salvo", "Acessibilidade: " + result.Warnings.Count + (result.Warnings.Count == 1 ? " imagem está" : " imagens estão") +
                    " sem descrição para quem usa leitor de tela.\n\n" + string.Join("\n", result.Warnings.Take(8)) + (result.Warnings.Count > 8 ? "\n…\n\nTodas estão em ⋯ → Verificar publicação." : ""));
            else ShowLifeNotice("Livro EPUB salvo · " + receipt.Summary + ".");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        { await Dialogs.AlertAsync("Não foi possível exportar EPUB", ex.Message); }
    }

    /// <summary>
    /// CST-166: the publication as an editable office document (Word or OpenDocument), from the live notes. What the
    /// format could not keep is told after saving, never silently flattened.
    /// </summary>
    public async Task ExportPublicationOfficeAsync(string format)
    {
        if (_publicationsCatalog is not { } catalog || _publicationDraft is not { } draft || !await SavePublicationAsync()) return;
        var project = draft.Project;
        var (extension, mime, kind) = format switch
        {
            "odt" => ("odt", "application/vnd.oasis.opendocument.text", "Documento ODT"),
            "pptx" => ("pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", "Apresentação PowerPoint"),
            "pdf" => ("pdf", "application/pdf", "PDF"),
            _ => ("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Documento Word")
        };
        try
        {
            Func<string, Task<byte[]?>> asset = ReadVaultImageAsync;
            var data = await PublicationDataAsync(project, catalog);
            var result = extension switch
            {
                "odt" => await PublicationOdt.ExportAsync(project, catalog, asset, rasterize: SvgRaster.ToPng, data: data),
                "pptx" => await PublicationPptx.ExportAsync(project, catalog, asset, rasterize: SvgRaster.ToPng, data: data),
                "pdf" => await PublicationPdf.ExportAsync(project, catalog, asset, data),
                _ => await PublicationDocx.ExportAsync(project, catalog, asset, rasterize: SvgRaster.ToPng, data: data)
            };
            if (!ReferenceEquals(draft, _publicationDraft) || !ReferenceEquals(draft.Store, _store) || draft.Dirty) return;
            var name = ArtifactModel.SafeName(project.Title) + "." + extension;
            var receipt = EnsureOpens(extension, result.Bytes).Summary;
            LastOfficeLimitations = result.Limitations;
            // CST-191: what this format does not keep, found in these notes, is said before saving — the person can go back
            var accepted = draft.DocumentId + "|" + extension;
            var lost = string.Join("\n", result.Limitations);
            if (result.Limitations.Count > 0 && _acceptedLosses.GetValueOrDefault(accepted) != lost && !await Dialogs.ConfirmAsync(kind + ": o que muda neste formato",
                    "O arquivo é editável. O que o formato não guarda como no Numa:\n\n• " + string.Join("\n• ", result.Limitations),
                    "Salvar assim", cancel: "Voltar"))
                return;
            _acceptedLosses[accepted] = lost;   // the same losses for the same publication and format are not asked again this session
            var saved = SavePageFile is { } save ? await save(name, result.Bytes) : await SavePublicationFileAsync(name, result.Bytes, extension, mime);
            if (saved != true) return;
            RecordPublicationExport(extension);
            // CST-213: the notice says what the check confirmed in the file, and what it could not
            ShowLifeNotice(kind + (extension == "pptx" ? " salva · " : " salvo · ") + receipt + ".");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        { await Dialogs.AlertAsync("Não foi possível exportar: " + kind, ex.Message); }
    }

    /// <summary>CST-158: the Assistente opens with the publication named in what the person types (they finish and send).</summary>
    public void AskAssistantAboutPublication()
    {
        if (_publicationsStudio?.Project is not { } project || _publicationDraft is not { } draft || _store?.Get(draft.DocumentId) is not { } file) return;
        if (!AssistantOpen) ToggleAssistant();
        Assistant.Input.Text = "Na publicação “" + project.Title + "” (" + file.Path + "): ";
        Assistant.Input.CaretIndex = Assistant.Input.Text.Length;
    }

    /// <summary>CST-175: what looks private in a publication about to leave the device, shown before; true to go on.</summary>
    private async Task<bool> ConfirmPrivacyAsync(string html)
    {
        var found = PublicationPrivacy.Scan(html);
        if (found.Count == 0) return true;
        var lines = found.Take(10).Select(f => "• " + f.Kind + ": " + f.Sample);
        return await Dialogs.ConfirmAsync("Antes de compartilhar", "A publicação mostra dados que parecem pessoais:\n\n" + string.Join("\n", lines) +
            (found.Count > 10 ? "\n… e mais " + (found.Count - 10) : "") + "\n\nSe não devem sair, tire-os das notas e exporte de novo.", "Exportar mesmo assim", cancel: "Voltar");
    }

    /// <summary>CST-163: the publication and a manifest (type, size, SHA-256 of each file, source versions) in one ZIP.</summary>
    public async Task ExportPublicationPackageAsync()
    {
        if (await PreparePublicationOutputAsync() is not { } output) return;
        var (project, result) = output;
        if (!await ConfirmPrivacyAsync(result.Html)) return;
        var versions = PublicationExportRecord.Of(project, "zip", id => _store?.Get(id)?.Content, DateTimeOffset.Now).Sources
            .Where(kv => _store?.Get(kv.Key) is not null)
            .ToDictionary(kv => kv.Key, kv => new PublicationPackage.Source(_store!.Get(kv.Key)!.Path, kv.Value, _store.Get(kv.Key)!.Content), StringComparer.Ordinal);
        // the audio and video it embeds go in the package, read from the vault by the name the note uses; a large
        // package says its size first (sizes come without reading the files)
        var references = PublicationPackage.MediaReferences(result.Html);
        long estimate = System.Text.Encoding.UTF8.GetByteCount(result.Html);
        foreach (var reference in references)
            if (VaultFileNamed(reference) is { } path && _vault is { } vault) estimate += (await vault.ReadFileAsync(path, 0))?.Size ?? 0;
        if (estimate > 50L << 20 && !await Dialogs.ConfirmAsync("Pacote grande", "O pacote terá cerca de " + PublicationPackage.Size(estimate) +
                " (" + references.Count + (references.Count == 1 ? " áudio ou vídeo" : " áudios e vídeos") + "). Continuar?", "Exportar")) return;
        var media = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var reference in references) media[reference] = await ReadVaultImageAsync(reference);
        var bytes = PublicationPackage.Build(project, result.Html, versions, DateTimeOffset.Now, media);
        var name = ArtifactModel.SafeName(project.Title) + ".zip";
        try
        {
            EnsureOpens("zip", bytes);
            var saved = SavePageFile is { } save ? await save(name, bytes) : await SavePublicationFileAsync(name, bytes, "zip", "application/zip");
            if (saved != true) return;
            RecordPublicationExport("zip");
            ShowLifeNotice("Pacote salvo (" + PublicationPackage.Size(bytes.Length) + "): a publicação e o manifesto que confere cada arquivo.");
        }
        catch (InvalidDataException ex) { await Dialogs.AlertAsync("Não foi possível exportar o pacote", ex.Message); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        { await Dialogs.AlertAsync("Não foi possível exportar o pacote", StorageProblems.Explain(ex)); }
    }

    /// <summary>CST-153: the version of each source this export used goes into the publication (by hash, never the text).</summary>
    private void RecordPublicationExport(string format)
    {
        if (_publicationsStudio?.Project is not { } project) return;
        _publicationsStudio.UpdateProject(project with { LastExport = PublicationExportRecord.Of(project, format, id => _store?.Get(id)?.Content, DateTimeOffset.Now) });
    }

    /// <summary>CST-153: on opening, what changed in the sources since the last export (the products made then are behind).</summary>
    private void TellChangesSinceExport(PublicationProject project)
    {
        if (project.LastExport is not { } last || _store is null) return;
        var (changed, added) = last.Since(project, id => _store.Get(id)?.Content);
        if (changed.Count == 0 && added.Count == 0) return;
        string Kind(PublicationExportRecord.ChangeKind k) => k switch
        {
            PublicationExportRecord.ChangeKind.Removed => "saiu do vault",
            PublicationExportRecord.ChangeKind.Structure => "títulos ou ligações",
            PublicationExportRecord.ChangeKind.Text => "texto",
            _ => "só formatação"
        };
        string Name(string id) => _store.Get(id)?.Title ?? "uma nota";
        var parts = new List<string>();
        if (changed.Count > 0) parts.Add((changed.Count == 1 ? "mudou " : changed.Count + " fontes mudaram: ") +
            string.Join(", ", changed.Take(3).Select(c => Name(c.Id) + " (" + Kind(c.Kind) + ")")) + (changed.Count > 3 ? "…" : ""));
        if (added.Count > 0) parts.Add(added.Count == 1 ? "entrou 1 fonte nova" : "entraram " + added.Count + " fontes novas");
        ShowLifeNotice("Desde a última exportação (" + last.Format.ToUpperInvariant() + ", " + last.At.ToLocalTime().ToString("dd/MM HH:mm", System.Globalization.CultureInfo.InvariantCulture) + "), " +
            string.Join("; ", parts) + ". Exporte de novo para atualizar o arquivo.");
    }

    /// <summary>The issues of the last review (read by tests).</summary>
    public IReadOnlyList<ReviewIssue> LastPublicationReview { get; private set; } = [];

    /// <summary>
    /// CST-179/180: "Verificar publicação" — the live notes read the way every output reads them; each issue names the
    /// note and line, says how to fix it and opens there. Nothing is changed by the review.
    /// </summary>
    public async Task ReviewPublicationAsync()
    {
        if (_publicationsCatalog is not { } catalog || _publicationDraft is not { } draft || Dialogs.IsOpen) return;
        var project = draft.Project;
        var data = await PublicationDataAsync(project, catalog);
        var files = new HashSet<string>(_attachments.Concat(_contents.Keys), StringComparer.Ordinal);
        var notes = _contents.Keys.Select(p => new CityNote(p, "")).ToList();
        bool NoteExists(string name) => files.Contains(name) || files.Contains(name + ".md") ||
            notes.Any(n => string.Equals(System.IO.Path.GetFileNameWithoutExtension(n.Path), System.IO.Path.GetFileName(name), StringComparison.OrdinalIgnoreCase));
        string? ReadNote(string name) => notes.FirstOrDefault(n => string.Equals(n.Path, name, StringComparison.OrdinalIgnoreCase) || string.Equals(n.Path, name + ".md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(System.IO.Path.GetFileNameWithoutExtension(n.Path), System.IO.Path.GetFileName(name), StringComparison.OrdinalIgnoreCase))?.Content;
        var issues = await Task.Run(() => PublicationReview.Run(project, catalog, files.Contains, NoteExists, data, ReadNote));
        LastPublicationReview = issues;
        if (!ReferenceEquals(draft, _publicationDraft)) return;
        if (issues.Count == 0) { ShowLifeNotice("Nada a corrigir: a publicação está pronta para exportar."); return; }
        int errors = issues.Count(i => i.Severity == ReviewSeverity.Error);
        var shown = issues.ToArray();
        var choice = await Dialogs.ChooseAsync("Verificar publicação",
            shown.Select(i => new DialogItem((i.Severity == ReviewSeverity.Error ? "Erro · " : "Aviso · ") + i.Message, i.NotePath is null ? UrbeTheme.Icons.File : UrbeTheme.Icons.Notes,
                i.Place + " — " + i.Fix, Danger: i.Severity == ReviewSeverity.Error)).ToArray(),
            (errors > 0 ? errors + (errors == 1 ? " erro" : " erros") + " e " : "") + (issues.Count - errors) + (issues.Count - errors == 1 ? " aviso" : " avisos") +
            ". Toque para abrir a nota no lugar.");
        if (choice is not { } index || index >= shown.Length || shown[index].NotePath is not { } path) return;
        OpenNoteByPath(path);
        if (shown[index].Line is { } line) Editor.RevealLine(line);
    }

    /// <summary>The accessibility warnings of the last EPUB export (read by tests).</summary>
    public IReadOnlyList<string> LastEpubWarnings { get; private set; } = [];

    /// <summary>What the last office export could not keep (shown to the person; read by tests).</summary>
    public IReadOnlyList<string> LastOfficeLimitations { get; private set; } = [];

    /// <summary>CST-191: a file that does not open as its format is never saved as if it were done.</summary>
    private readonly Dictionary<string, string> _acceptedLosses = new(StringComparer.Ordinal);

    /// <summary>The file's check (CST-191/213): a problem stops the save; otherwise its receipt, to tell the person.</summary>
    private static OutputReceipt EnsureOpens(string extension, byte[] bytes)
    {
        var receipt = PublicationOutputCheck.Receipt(extension, bytes);
        if (receipt.Problem is { } problem)
            throw new InvalidDataException("O arquivo gerado não passou na conferência e não foi salvo. " + problem);
        return receipt;
    }

    private async Task<bool?> SavePublicationFileAsync(string name, byte[] bytes, string extension, string mime)
    {
        if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
            throw new NotSupportedException("Este aparelho não permite salvar arquivos.");
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Exportar publicação", SuggestedFileName = name, DefaultExtension = extension,
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType(extension.ToUpperInvariant()) { Patterns = ["*." + extension], MimeTypes = [mime] }]
        });
        if (file is null) return null;
        await ExportFiles.WriteAndCheckAsync(file, bytes);   // CST-180: written whole and read back
        return true;
    }

    public async Task ExportPublicationHtmlAsync()
    {
        if (await PreparePublicationOutputAsync() is not { } output) return;
        var (project, result) = output;
        if (!await ConfirmPrivacyAsync(result.Html)) return;
        var filename = ArtifactModel.SafeName(project.Title) + ".html";
        try
        {
            var resultSaved = await (SavePageFile ?? SaveHtmlFileAsync)(filename, System.Text.Encoding.UTF8.GetBytes(result.Html));
            if (resultSaved == true) { RecordPublicationExport("html"); ShowLifeNotice("Publicação HTML salva (" + PublicationPackage.Size(System.Text.Encoding.UTF8.GetByteCount(result.Html)) + ")."); }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            await Dialogs.AlertAsync("Não consegui exportar", StorageProblems.Explain(e));
        }
    }
}
