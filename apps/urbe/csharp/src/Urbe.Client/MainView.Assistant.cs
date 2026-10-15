using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// The Assistente (UC-21) in the native app: the rail/dock destination opens the panel; the agents' tools read the
/// vault's documents and write through the same paths as the person (the vault file first, verified, then the store,
/// the city and the map), so nothing changes in memory only. Conversations and keys stay on this device.
/// </summary>
public sealed partial class MainView : IAssistantApp
{
    private AssistantPanel? _assistant;

    /// <summary>The Assistente panel, made the first time it opens (over the city and the editor, under the dialogs).</summary>
    public AssistantPanel Assistant => _assistant ??= MakeAssistant();

    private AssistantPanel MakeAssistant()
    {
        IAssistantStorage storage = UrbeApp.AssistantFolder is { } folder ? new FolderAssistantStorage(folder) : new MemoryAssistantStorage();
        var panel = new AssistantPanel(this, storage);
        _stage.Children.Add(panel);   // over the city, the editor, the previews and the plugins' panels; the dialogs stay above
        PlaceAssistant(panel, Bounds.Width);
        return panel;
    }

    /// <summary>Wide screens: a 440 px panel at the right, beside the city or the editor; narrow ones: the whole screen.</summary>
    private static void PlaceAssistant(AssistantPanel? panel, double width)
    {
        if (panel is null) return;
        bool side = width >= 900;
        panel.Width = side ? 440 : double.NaN;
        panel.HorizontalAlignment = side ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Stretch;
    }

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

    /// <summary>The city's plugins for write_plugin: listed as Personalização lists them; turned on for their exact text.</summary>
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
        public IReadOnlyList<string> Packs => c.Packs().Select(p => p.Path).ToList();
    }

    void IAssistantApp.Notify(string text) => ShowLifeNotice(text);

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
        if (!VaultWritable || _store is null || World.City is null)
            throw new InvalidOperationException("O vault está só para leitura. Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para o Assistente alterar notas.");
    }

    /// <summary>The folders of <paramref name="folder"/> as neighbourhoods, each made (region and vault folder) when missing.</summary>
    private async Task EnsureFolderAsync(string folder)
    {
        var city = World.City!;
        string? parent = null, prefix = "";
        foreach (var part in folder.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            prefix = prefix.Length == 0 ? part : prefix + "/" + part;
            var region = city.RegionByPath(prefix);
            if (region is null)
            {
                region = city.CreateFolder(part, parent, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                         ?? throw new InvalidOperationException("Não encontrei área livre na cidade para a pasta " + prefix + ".");
                try { await _vault!.CreateDirectoryAsync(city.RegionPaths()[region.Id]); }
                catch { city.Regions.Remove(region); throw; }
            }
            parent = region.Id;
        }
    }

    private static string FolderOf(string path) => path.LastIndexOf('/') is var i and >= 0 ? path[..i] : "";

    private void AfterAssistantWrite(string path)
    {
        // a file of Personalização changed (tema.json, temas, texturas, tags): recarregar(), as customize.js does on document:updated
        if (path.StartsWith(LegacyCustomize.Folder + "/", StringComparison.Ordinal)) Customization.Load(_contents);
        ScheduleRoadRebuild();
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
    }

    async Task<UrbeDocument> IAssistantApp.CreateNoteAsync(string path, string content)
    {
        RequireWritable();
        if (_store!.Get(path) is not null || _contents.ContainsKey(path)) throw new InvalidOperationException("Já existe " + path + ".");
        if (path.StartsWith(LegacyCustomize.Folder + "/", StringComparison.Ordinal))
        {
            // Personalização (tema, plugins...): written as customize.js writes it, a house beside the others of the folder
            await WriteVaultFileAsync(path, content, exists: false);
            AfterAssistantWrite(path);
            return _store.Get(path)!;
        }
        await EnsureFolderAsync(FolderOf(path));
        await _vault!.CreateTextFileAsync(path, content);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var today = LegacyCity.NowDate(now);
        var doc = _store.Upsert(new DocumentInput { Path = path, Content = content, Created = today, Modified = today }, "ai.agent");
        World.City!.AddDocumentHouse(doc, now);
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
        var city = World.City!;
        var house = city.Buildings.Find(b => b.IsNote && b.Path == doc.Path) ?? throw new InvalidOperationException("A nota " + doc.Path + " não está na cidade.");
        bool editing = Editor.IsVisible && Editor.Current?.Path == doc.Path;
        if (editing && Editor.HasChanges && !await Editor.SaveAsync()) throw new InvalidOperationException("A nota tem alterações que não foram salvas no vault.");
        await EnsureFolderAsync(FolderOf(newPath));
        var from = doc.Path;
        await _vault!.MoveTextFileAsync(from, newPath);
        if (_contents.Remove(from, out var text)) _contents[newPath] = text;
        var moved = LegacyCity.MoveDocument(_store!, _store!.Get(doc.Id) ?? doc, newPath);
        city.ApplyFolderMove(house, moved, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await CarryLooksAsync(new Dictionary<string, string>(StringComparer.Ordinal) { [from] = newPath });
        if (editing) Editor.Renamed(newPath);
        AfterAssistantWrite(newPath);
        return _store.Get(moved.Id) ?? moved;
    }

    async Task IAssistantApp.TrashNoteAsync(UrbeDocument doc)
    {
        RequireWritable();
        if (!await DeleteNoteAsync(doc.Path, confirmed: true)) throw new InvalidOperationException("Não foi possível mover " + doc.Path + " para a lixeira.");
        if (doc.Path.StartsWith(LegacyCustomize.Folder + "/", StringComparison.Ordinal)) Customization.Load(_contents);
        ScheduleRoadRebuild();
    }

    async Task IAssistantApp.CreateFolderAsync(string path)
    {
        RequireWritable();
        await EnsureFolderAsync(path);
        RefreshNotes();
        World.InvalidateVisual();
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
