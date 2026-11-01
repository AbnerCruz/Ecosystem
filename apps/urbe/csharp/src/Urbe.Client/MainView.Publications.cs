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
        studio.EditSourceRequested += EditPublicationSource;
        studio.ExportRequested += () => _ = ExportPublicationHtmlAsync();
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

    /// <summary>First-class creation from the current note, or an empty project chosen in Settings.</summary>
    public async Task CreatePublicationAsync(string? sourcePath = null, string? sourceFolder = null)
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
        var suggested = source?.Title ?? (folder is { Length: > 0 } ? System.IO.Path.GetFileName(folder) : null);
        var title = await Dialogs.PromptAsync(new PromptOptions("Nova publicação", Label: "Título", Placeholder: suggested ?? "Ex.: Meu livro", Confirm: "Criar"));
        if (string.IsNullOrWhiteSpace(title)) return;
        if (!ReferenceEquals(_store, originalStore) || !ReferenceEquals(_vault, originalVault))
        {
            ShowLifeNotice("O vault mudou. Abra a criação novamente no vault desejado.", Ui.ToastKind.Warning);
            return;
        }
        var project = PublicationProject.Create(title.Trim(), source?.Id);
        if (folder is not null)
        {
            var prefix = folder.Length == 0 ? "" : folder + "/";
            foreach (var note in _store.List().Where(d => ArtifactModel.IsNote(d.Path) && d.Path.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase))
                project = project.AddSource(note.Id);
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
        var existing = project.Sources.Select(s => s.DocumentId).ToHashSet(StringComparer.Ordinal);
        var notes = _store.List().Where(d => ArtifactModel.IsNote(d.Path) && !existing.Contains(d.Id))
            .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToList();
        if (notes.Count == 0)
        {
            await Dialogs.AlertAsync("Adicionar nota", "Não há notas disponíveis para adicionar.");
            return;
        }
        var chosen = await Dialogs.ChooseAsync("Selecionar nota de origem", notes.Select(n => new DialogItem(n.Title, Detail: n.Path)).ToList());
        if (chosen is { } i && ReferenceEquals(_publicationDraft, draft) &&
            ReferenceEquals(_store, draft?.Store) && _publicationsStudio?.IsVisible == true)
            _publicationsStudio.AddSource(notes[i].Id);
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
        try { result = PublicationHtml.Export(project, catalog); }
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
            await print(output.Project.Title, output.Render.Html, new PagePrintSettings(false, "a4"));
            ShowLifeNotice("Na impressão do sistema, escolha “Salvar como PDF” para gerar o arquivo.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { ShowLifeNotice("Não consegui preparar a impressão: " + ex.Message, Ui.ToastKind.Warning); }
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
