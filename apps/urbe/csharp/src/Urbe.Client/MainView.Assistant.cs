using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// The Assistente (UC-21) in the native app: the rail/dock destination opens the panel; the agents' tools read the
/// vault's documents and write through the same paths as the person (the vault file first, verified, then the store
/// and the notes' record), so nothing changes in memory only. Conversations and keys stay on this device.
/// </summary>
public sealed partial class MainView : IAssistantApp
{
    private AssistantPanel? _assistant;

    /// <summary>The Assistente panel, made the first time it opens (over the Explorer and the editor, under the dialogs).</summary>
    public AssistantPanel Assistant => _assistant ??= MakeAssistant();

    private AssistantPanel MakeAssistant()
    {
        IAssistantStorage storage = UrbeApp.AssistantFolder is { } folder ? new FolderAssistantStorage(folder) : new MemoryAssistantStorage();
        var panel = new AssistantPanel(this, storage);
        _stage.Children.Add(panel);   // over the Explorer, the editor, the previews and the plugins' panels; the dialogs stay above
        PlaceAssistant(panel, Bounds.Width);
        panel.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) UpdateNav(); };
        return panel;
    }

    /// <summary>Wide screens: a 440 px panel at the right, beside the Explorer or the editor; narrow ones: the whole screen.</summary>
    private static void PlaceAssistant(AssistantPanel? panel, double width)
    {
        if (panel is null) return;
        bool side = width >= 900;
        panel.Width = side ? 440 : double.NaN;
        panel.HorizontalAlignment = side ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Stretch;
    }

    /// <summary>
    /// UC-80: a <c>&amp;Nome&amp;:</c> line asked its agent. The note is saved first (the agent reads and edits what is in the
    /// vault), then the Assistente opens on a conversation of its own for this request.
    /// </summary>
    private async Task AskAgentInNoteAsync(NoteMarks.AgentCall call)
    {
        if (Editor.Current is not { } note || Dialogs.IsOpen) return;
        if (call.Request.Length == 0) { ShowLifeNotice("Escreva o pedido depois de &" + call.Agent + "&:."); return; }
        if (Editor.HasChanges && !await Editor.SaveAsync())
        {
            await Dialogs.AlertAsync("Pedir ao agente", "A nota ainda não foi salva no vault. O pedido espera a nota estar salva, para o agente ler o que você escreveu.");
            return;
        }
        var title = _store?.Get(note.Path)?.Title ?? LastName(note.Path);
        if (Assistant.AskInNote(note.Path, title, call) is { } problem) await Dialogs.AlertAsync("Pedir ao agente", problem);
    }

    /// <summary>True while the Assistente panel is open.</summary>
    private bool AssistantOpen => _assistant is { IsVisible: true };

    /// <summary>The rail/dock "Assistente": opens (or closes) the panel.</summary>
    public void ToggleAssistant()
    {
        if (Dialogs.IsOpen) return;
        if (_assistant is { IsVisible: true }) { _assistant.Close(); return; }
        Assistant.Open();
    }

    // ---------------------------------------------------------------- IAssistantApp

    DocumentStore? IAssistantApp.Documents => _store;

    string IAssistantApp.VaultName => _vault?.VaultName is { Length: > 0 } name ? name : "Urbe";

    (string? Path, string? Selection) IAssistantApp.EditorNote()
    {
        if (!Editor.IsVisible || Editor.Current is not { } note) return (null, null);
        return (note.Path, Editor.SelectedText);
    }

    UrbeDialogs IAssistantApp.Dialogs => Dialogs;

    IAssistantAppearance? IAssistantApp.Appearance => new AppearanceAdapter(Customization);

    IAssistantPlugins? IAssistantApp.Plugins => new PluginsAdapter(Customization);

    /// <summary>The vault's plugins for write_plugin: listed as Personalização lists them; turned on for their exact text.</summary>
    private sealed class PluginsAdapter(Customization c) : IAssistantPlugins
    {
        public IReadOnlyList<(string Path, string Name, string Status)> List() =>
            c.Plugins().Select(p => (p.Path, p.Name, Urbe.Core.Plugins.VaultPluginSpec.IsPluginPath(p.Path) && p.Status == "aprovado" ? "ligado" : p.Status)).ToList();
        public void Enable(string path) => c.EnablePlugin(path);
    }

    /// <summary>customize api for the appearance tools: get, normalize, replace (gravar: the vault file first), themes and packs.</summary>
    private sealed class AppearanceAdapter(Customization c) : IAssistantAppearance
    {
        public System.Text.Json.Nodes.JsonObject Current => (System.Text.Json.Nodes.JsonObject)c.Config.DeepClone();
        public LegacyCustomize.Result Normalize(System.Text.Json.Nodes.JsonNode config) => c.Normalize(config);
        public async Task ReplaceAsync(System.Text.Json.Nodes.JsonObject config)
        {
            var n = await c.SaveAsync(config);
            if (n.Errors.Count > 0) throw new InvalidOperationException(n.Errors[0].Path + ": " + n.Errors[0].Message);
        }
        public IReadOnlyList<string> SavedThemes => c.Themes().Select(t => t.Id).ToList();
    }

    void IAssistantApp.Notify(string text) => ShowLifeNotice(text);

    void IAssistantApp.Notify(string text, string action, Action onAction) => _notice.Show(text, TimeSpan.FromSeconds(8), action, onAction);

    bool IAssistantApp.SuggestInNote(string path, LineChange change, string who, Action accept, Action refuse)
    {
        // only on the note as it is in the vault: unsaved typing would make the change land elsewhere
        if (!Editor.IsVisible || Editor.Current?.Path != path || Editor.HasChanges) return false;
        Editor.ShowSuggestion(new VisualSurface.Suggestion(change, who,
            () => { Editor.ShowSuggestion(null); accept(); },
            () => { Editor.ShowSuggestion(null); refuse(); }));
        return true;
    }

    void IAssistantApp.EndSuggestion() { if (Editor.Suggestion is not null) Editor.ShowSuggestion(null); }

    async Task IAssistantApp.CreateBinaryFileAsync(string path, byte[] bytes)
    {
        RequireWritable();
        if (((IAssistantApp)this).FileExists(path)) throw new InvalidOperationException("Já existe " + path + ".");
        await EnsureFolderAsync(DirOf(path));
        await _vault!.CreateBinaryFileAsync(path, bytes);
        _attachments.Add(path);
        RefreshNotes();
    }

    bool IAssistantApp.FileExists(string path) =>
        _contents.Keys.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) ||
        _attachments.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));

    bool IAssistantApp.OpenReference(string reference)
    {
        if (_store is null || reference.Length == 0) return false;
        var d = _store.Get(reference) ?? _store.Get(reference + ".md")
            ?? _store.List().FirstOrDefault(x => string.Equals(x.Title, reference, StringComparison.OrdinalIgnoreCase));
        if (d is null) return false;
        OpenNoteByPath(d.Path);
        return true;
    }

    private void RequireWritable()
    {
        if (!VaultWritable || _library is null)
            throw new InvalidOperationException("O vault está só para leitura. Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para o Assistente alterar notas.");
    }

    /// <summary>The folders of <paramref name="folder"/>, each made in the vault when missing, and recorded.</summary>
    private async Task EnsureFolderAsync(string folder)
    {
        if (folder.Length == 0 || NoteFolders().Contains(folder)) return;
        await _vault!.CreateDirectoryAsync(folder);
        _library!.AddFolder(folder);
        NotesChanged();
    }

    private void AfterAssistantWrite(string path)
    {
        // a file of Personalização changed (tema.json, temas, tags): recarregar(), as customize.js does on document:updated
        if (path.StartsWith(LegacyCustomize.Folder + "/", StringComparison.Ordinal)) Customization.Load(_contents);
        NotesChanged();
        RefreshNotes();
    }

    async Task<UrbeDocument> IAssistantApp.CreateNoteAsync(string path, string content)
    {
        RequireWritable();
        if (_store!.Get(path) is not null || _contents.ContainsKey(path)) throw new InvalidOperationException("Já existe " + path + ".");
        if (path.StartsWith(LegacyCustomize.Folder + "/", StringComparison.Ordinal))
        {
            // Personalização (tema, plugins...): written as customize.js writes it
            await WriteVaultFileAsync(path, content, exists: false);
            AfterAssistantWrite(path);
            return _store.Get(path)!;
        }
        await EnsureFolderAsync(DirOf(path));
        await _vault!.CreateTextFileAsync(path, content);
        var doc = _library!.AddNote(path, content, Now, "ai.agent");
        _contents[path] = content;
        AfterAssistantWrite(path);
        return doc;
    }

    async Task<UrbeDocument> IAssistantApp.SaveNoteAsync(UrbeDocument doc, string content)
    {
        RequireWritable();
        bool editing = Editor.IsVisible && Editor.Current?.Path == doc.Path;
        if (editing && Editor.HasChanges) throw new InvalidOperationException("A nota " + doc.Path + " está aberta no editor com alterações não salvas. Salve ou descarte antes.");
        await SaveNoteAsync(new CityNote(doc.Path, content));
        if (editing) Editor.Open(new CityNote(doc.Path, content));
        AfterAssistantWrite(doc.Path);
        return _store!.Get(doc.Id) ?? doc;
    }

    async Task<UrbeDocument> IAssistantApp.RenameNoteAsync(UrbeDocument doc, string newPath)
    {
        RequireWritable();
        if (_store!.Get(doc.Path) is null) throw new InvalidOperationException("A nota " + doc.Path + " não está no vault.");
        bool editing = Editor.IsVisible && Editor.Current?.Path == doc.Path;
        if (editing && Editor.HasChanges && !await Editor.SaveAsync()) throw new InvalidOperationException("A nota tem alterações que não foram salvas no vault.");
        await EnsureFolderAsync(DirOf(newPath));
        var from = doc.Path;
        await _vault!.MoveTextFileAsync(from, newPath);
        if (_contents.Remove(from, out var text)) _contents[newPath] = text;
        var moved = NoteLibrary.MoveDocument(_store, _store.Get(doc.Id) ?? doc, newPath);
        moved = _library!.Edited(moved.Path, moved.Content, Now) ?? moved;
        if (editing) Editor.Renamed(newPath);
        AfterAssistantWrite(newPath);
        return _store.Get(moved.Id) ?? moved;
    }

    async Task IAssistantApp.TrashNoteAsync(UrbeDocument doc)
    {
        RequireWritable();
        if (!await DeleteNoteAsync(doc.Path, confirmed: true)) throw new InvalidOperationException("Não foi possível mover " + doc.Path + " para a lixeira.");
        if (doc.Path.StartsWith(LegacyCustomize.Folder + "/", StringComparison.Ordinal)) Customization.Load(_contents);
    }

    async Task IAssistantApp.CreateFolderAsync(string path)
    {
        RequireWritable();
        await EnsureFolderAsync(path);
        RefreshNotes();
    }

    async Task<bool> IAssistantApp.RestoreTrashedAsync(string id)
    {
        await RestoreFromTrashAsync(id);
        if (_store?.Get(id) is not { } back) return false;
        if (back.Path.StartsWith(LegacyCustomize.Folder + "/", StringComparison.Ordinal)) Customization.Load(_contents);
        return true;
    }

    async Task IAssistantApp.WriteBackAsync(UrbeDocument current, string path, string content)
    {
        IAssistantApp app = this;
        var doc = current;
        if (!string.Equals(current.Path, path, StringComparison.Ordinal)) doc = await app.RenameNoteAsync(current, path);
        if (doc.Content != content) await app.SaveNoteAsync(doc, content);
    }
}
