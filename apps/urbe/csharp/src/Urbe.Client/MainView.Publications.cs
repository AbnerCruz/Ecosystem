using Urbe.Core;
using Urbe.Core.Assistant;

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

    public PublicationStudioView PublicationsStudio => _publicationsStudio ??= MakePublicationsStudio();

    private PublicationStudioView MakePublicationsStudio()
    {
        var studio = new PublicationStudioView();
        studio.AddSourceRequested += () => _ = AddPublicationSourceAsync();
        studio.RenameRequested += () => _ = RenamePublicationAsync();
        studio.ConfigureRequested += () => _ = ConfigurePublicationAsync();
        studio.EditSourceRequested += EditPublicationSource;
        studio.EditReferenceRequested += reference =>
        {
            EditPublicationSource(reference.DocumentId);
            if (_store?.Get(reference.DocumentId) is { } source && Editor.Current?.Path == source.Path &&
                reference.Fragment is { Length: > 0 } fragment) Editor.RevealFragment(fragment);
        };
        studio.ExportRequested += () => _ = ExportPublicationHtmlAsync();
        studio.EpubRequested += () => _ = ExportPublicationEpubAsync();
        studio.OfficeRequested += format => _ = ExportPublicationOfficeAsync(format);
        studio.PrintRequested += () => _ = PrintPublicationAsync();
        studio.RetrySaveRequested += () => _ = SavePublicationAsync();
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

    /// <summary>Open an existing project by path, preserving the current unsaved note first.</summary>
    public async Task OpenPublicationAsync(string path)
    {
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
        if (_studio?.IsVisible == true) await _studio.CloseAsync();
        ReleaseSavedPublicationDraft(_publicationDraft);
        var key = (_store!, file.Id);
        if (!_publicationDrafts.TryGetValue(key, out var draft) || !draft.Dirty)
        {
            draft = new PublicationDraft(_store!, file.Id, file.Content, project);
            _publicationDrafts[key] = draft;
        }
        _publicationDraft = draft;
        PublicationsStudio.Open(path, draft.Project, _publicationsCatalog!);
        PublicationsStudio.SetSaveState(draft.Dirty, draft.Error);
        UpdateFloatingNav();
    }

    /// <summary>
    /// First-class creation from the current note, a folder, chosen notes (a selection, search results), a tag or one
    /// section of a note (<paramref name="fragment"/>: a heading). Every source is a reference to a real note, never a
    /// copy (CST-135).
    /// </summary>
    public async Task CreatePublicationAsync(string? sourcePath = null, string? sourceFolder = null, IReadOnlyList<string>? sourcePaths = null,
        string? tag = null, string? fragment = null)
    {
        if (!VaultWritable || _store is null)
        {
            await Dialogs.AlertAsync("Publicações", NoVaultMessage);
            return;
        }
        var source = sourcePath is null ? null : _store.Get(sourcePath);
        if (source is not null && !ArtifactModel.IsNote(source.Path)) source = null;
        var originalStore = _store;
        var originalVault = _vault;
        var folder = sourceFolder?.Trim('/');
        var suggested = fragment ?? source?.Title ?? (folder is { Length: > 0 } ? System.IO.Path.GetFileName(folder) : null) ?? (tag is { Length: > 0 } ? "#" + tag.TrimStart('#') : null);
        var title = await Dialogs.PromptAsync(new PromptOptions("Nova publicação", Label: "Título", Placeholder: suggested ?? "Ex.: Meu livro", Confirm: "Criar"));
        if (string.IsNullOrWhiteSpace(title)) return;
        if (!ReferenceEquals(_store, originalStore) || !ReferenceEquals(_vault, originalVault))
        {
            ShowLifeNotice("O vault mudou. Abra a criação novamente no vault desejado.", Ui.ToastKind.Warning);
            return;
        }
        var project = fragment is { Length: > 0 } && source is not null
            ? PublicationProject.Create(title.Trim()).AddSource(new PublicationSourceReference(source.Id, fragment))
            : PublicationProject.Create(title.Trim(), source?.Id);
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
        if (project.Sections.Count == 0 && (sourcePaths is { Count: > 0 } || tag is { Length: > 0 }))
        {
            await Dialogs.AlertAsync("Publicações", tag is { Length: > 0 } ? "Nenhuma nota tem a tag #" + tag.TrimStart('#') + "." : "Nenhuma das escolhidas é uma nota.");
            return;
        }
        var path = "Publicações/" + ArtifactModel.SafeName(title.Trim()) + ".publication.json";
        if (_store.Get(path) is not null || _attachments.Contains(path))
        {
            var name = path[..^".publication.json".Length];
            for (var n = 2; ; n++)
            {
                var candidate = name + "-" + n + ".publication.json";
                if (_store.Get(candidate) is null && !_attachments.Contains(candidate)) { path = candidate; break; }
            }
        }
        try { await ((IAssistantApp)this).CreateNoteAsync(path, PublicationProjectCodec.Serialize(project)); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            await Dialogs.AlertAsync("Não foi possível criar a publicação", StorageProblems.Explain(ex));
            return;
        }
        await OpenPublicationAsync(path);
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
    public async Task CreatePublicationFromSectionAsync(string notePath)
    {
        if (_store?.Get(notePath) is not { } note) return;
        var headings = Urbe.Core.MarkdownEngine.SplitFrontmatter(note.Content).Body.Replace("\r", "").Split('\n')
            .Where(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^#{1,6}\s+\S")).Select(l => l.TrimStart('#').Trim()).Distinct().ToList();
        if (headings.Count == 0) { await Dialogs.AlertAsync("Publicações", "Esta nota não tem títulos para escolher um trecho."); return; }
        var choice = await Dialogs.ChooseAsync("Publicação de um trecho", headings.Select(h => new DialogItem(h)).ToList(), "O trecho é o título escolhido e o que vem embaixo dele; continua sendo o texto da nota.");
        if (choice is not { } k) return;
        await CreatePublicationAsync(sourcePath: notePath, fragment: headings[k]);
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
        if (projects.Count == 0) { await CreatePublicationAsync(); return; }
        var options = projects.Select(d =>
        {
            try { return new DialogItem(PublicationProjectCodec.Parse(d.Content).Title, Detail: d.Path); }
            catch (Exception e) when (e is InvalidDataException or NotSupportedException)
            { return new DialogItem(d.Title + " (verificar arquivo)", Detail: d.Path); }
        }).ToList();
        options.Add(new DialogItem("Nova publicação", UrbeTheme.Icons.Plus, "Criar a partir das notas do vault"));
        var chosen = await Dialogs.ChooseAsync("Publicações", options);
        if (chosen is not { } index) return;
        if (index == projects.Count) await CreatePublicationAsync();
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
        var option = await Dialogs.ChooseAsync("Composição", [new DialogItem("Layout", Detail: "Artigo, livro, apresentação ou curso"),
            new DialogItem("Tema", Detail: "Papel, oceano ou tinta"), new DialogItem("Objetivo", Detail: project.Objective), new DialogItem("Público", Detail: project.Audience)]);
        PublicationProject? next = null;
        if (option == 0)
        {
            var layout = await Dialogs.ChooseAsync("Layout", [new DialogItem("Artigo"), new DialogItem("Livro com sumário"), new DialogItem("Apresentação (uma nota por slide)"),
                new DialogItem("Curso", Detail: "Cada nota um módulo, cada ## uma aula, questionários e progresso")]);
            if (layout is >= 0 and <= 3) next = project with { Design = project.Design with { Layout = new[] { "article", "book", "slides", "course" }[layout.Value] } };
        }
        else if (option == 1)
        {
            var theme = await Dialogs.ChooseAsync("Tema", [new DialogItem("Papel"), new DialogItem("Oceano"), new DialogItem("Tinta")]);
            if (theme is >= 0 and <= 2) next = project with { Design = project.Design with { Theme = new[] { "paper", "ocean", "ink" }[theme.Value] } };
        }
        else if (option is 2 or 3)
        {
            var text = await Dialogs.PromptAsync(new PromptOptions(option == 2 ? "Objetivo da publicação" : "Público da publicação", Value: option == 2 ? project.Objective : project.Audience, Required: false, Confirm: "Salvar"));
            if (text is not null) next = option == 2 ? project with { Objective = text } : project with { Audience = text };
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
        return (project, result);
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
        var vault = _vault;
        var project = draft.Project;
        try
        {
            var data = await PublicationDataAsync(project, catalog);
            var bytes = await PublicationEpub.ExportAsync(project, catalog,
                async path => vault is null ? null : (await vault.ReadFileAsync(path, FilePreviewPanel.MaxBytes))?.Bytes, data: data);
            if (!ReferenceEquals(draft, _publicationDraft) || !ReferenceEquals(draft.Store, _store) || draft.Dirty) return;
            var name = ArtifactModel.SafeName(project.Title) + ".epub";
            var saved = SavePageFile is { } save ? await save(name, bytes) : await SavePublicationFileAsync(name, bytes, "epub", "application/epub+zip");
            if (saved == true) ShowLifeNotice("Livro EPUB salvo.");
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
        var vault = _vault;
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
            Func<string, Task<byte[]?>> asset = async path => vault is null ? null : (await vault.ReadFileAsync(path, FilePreviewPanel.MaxBytes))?.Bytes;
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
            var saved = SavePageFile is { } save ? await save(name, result.Bytes) : await SavePublicationFileAsync(name, result.Bytes, extension, mime);
            if (saved != true) return;
            LastOfficeLimitations = result.Limitations;
            if (result.Limitations.Count == 0) ShowLifeNotice(kind + (extension == "pptx" ? " salva." : " salvo."));
            else await Dialogs.AlertAsync(kind + (extension == "pptx" ? " salva" : " salvo"),
                "O arquivo é editável. O que o formato não guarda como no Urbe:\n\n• " + string.Join("\n• ", result.Limitations));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        { await Dialogs.AlertAsync("Não foi possível exportar: " + kind, ex.Message); }
    }

    /// <summary>What the last office export could not keep (shown to the person; read by tests).</summary>
    public IReadOnlyList<string> LastOfficeLimitations { get; private set; } = [];

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
        await using var stream = await file.OpenWriteAsync();
        if (stream.CanSeek) stream.SetLength(0);
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
        return true;
    }

    private async Task ExportPublicationHtmlAsync()
    {
        if (await PreparePublicationOutputAsync() is not { } output) return;
        var (project, result) = output;
        var filename = ArtifactModel.SafeName(project.Title) + ".html";
        try
        {
            var resultSaved = await (SavePageFile ?? SaveHtmlFileAsync)(filename, System.Text.Encoding.UTF8.GetBytes(result.Html));
            if (resultSaved == true) ShowLifeNotice("Publicação HTML salva.");
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException)
        {
            await Dialogs.AlertAsync("Não consegui exportar", StorageProblems.Explain(e));
        }
    }
}
