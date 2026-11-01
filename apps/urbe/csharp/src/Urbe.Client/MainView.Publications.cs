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

    public PublicationStudioView PublicationsStudio => _publicationsStudio ??= MakePublicationsStudio();

    private PublicationStudioView MakePublicationsStudio()
    {
        var studio = new PublicationStudioView();
        studio.AddSourceRequested += () => _ = AddPublicationSourceAsync();
        studio.RenameRequested += () => _ = RenamePublicationAsync();
        studio.EditSourceRequested += EditPublicationSource;
        studio.ExportRequested += () => _ = ExportPublicationHtmlAsync();
        studio.ProjectChanged += project => _ = SavePublicationProjectAsync(studio.ProjectPath, project);
        _stage.Children.Insert(Math.Max(0, _stage.Children.IndexOf(FilePreview)), studio);
        return studio;
    }

    /// <summary>Open an existing project by path, preserving the current unsaved note first.</summary>
    public async Task OpenPublicationAsync(string path)
    {
        if (_store?.Get(path) is not { } file) { ShowLifeNotice("Publicação não encontrada: " + path); return; }
        PublicationProject project;
        try { project = PublicationProjectCodec.Parse(file.Content); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException or ArgumentException)
        {
            await Dialogs.AlertAsync("Não foi possível abrir a publicação", e.Message);
            return;
        }
        if (Editor.IsVisible && Editor.HasChanges)
        {
            Editor.FinishEditing();
            if (!await Editor.SaveAsync())
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
        PublicationsStudio.Open(path, project, _publicationsCatalog!);
        UpdateFloatingNav();
    }

    /// <summary>First-class creation from the current note, or an empty project chosen in Settings.</summary>
    public async Task CreatePublicationAsync(string? sourcePath = null)
    {
        if (!VaultWritable || _store is null)
        {
            await Dialogs.AlertAsync("Publicações", NoVaultMessage);
            return;
        }
        var source = sourcePath is null ? null : _store.Get(sourcePath);
        if (source is not null && !ArtifactModel.IsNote(source.Path)) source = null;
        var title = await Dialogs.PromptAsync(new PromptOptions("Nova publicação", Label: "Título", Placeholder: source?.Title ?? "Ex.: Meu livro", Confirm: "Criar"));
        if (string.IsNullOrWhiteSpace(title)) return;
        var project = PublicationProject.Create(title.Trim(), source?.Id);
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
        if (chosen is { } i) _publicationsStudio?.AddSource(notes[i].Id);
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
        if (_publicationsStudio?.Project is not { } project) return;
        var name = await Dialogs.PromptAsync(new PromptOptions("Renomear publicação", Label: "Título", Placeholder: project.Title, Confirm: "Salvar"));
        if (!string.IsNullOrWhiteSpace(name)) _publicationsStudio.UpdateProject(project with { Title = name.Trim() });
    }

    private async Task SavePublicationProjectAsync(string path, PublicationProject project)
    {
        // Serializes rapid touch operations. A later revision can never be written before an earlier one.
        await _publicationWrites.WaitAsync();
        try
        {
            if (_store?.Get(path) is not { } file) throw new InvalidOperationException("Publicação não está mais no vault.");
            var json = PublicationProjectCodec.Serialize(project);
            if (file.Content == json) return;
            await ((IAssistantApp)this).SaveNoteAsync(file, json);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException or InvalidDataException)
        {
            ShowLifeNotice("Não consegui salvar a Publicação: " + e.Message, Ui.ToastKind.Warning);
        }
        finally { _publicationWrites.Release(); }
    }

    private async Task ExportPublicationHtmlAsync()
    {
        if (_publicationsStudio?.Project is not { } project || _publicationsCatalog is null) return;
        // Save all pending composition actions before claiming a valid export.
        await _publicationWrites.WaitAsync();
        _publicationWrites.Release();
        PublicationHtmlResult result;
        try { result = PublicationHtml.Export(project, _publicationsCatalog); }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            await Dialogs.AlertAsync("Exportação não disponível", ex.Message);
            return;
        }
        if (!result.IsComplete)
        {
            await Dialogs.AlertAsync("Fontes indisponíveis", "Resolva as notas ausentes antes de exportar: " + result.MissingDocumentIds.Count + " referência(s).");
            return;
        }
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
