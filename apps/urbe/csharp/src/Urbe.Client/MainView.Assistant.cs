using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
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
    IReadOnlyCollection<string> IAssistantApp.VaultFolders => _library?.Folders ?? [];
    private AssistantPanel? _assistant;

    // A small, always-available launcher; no extra AI service or duplicate conversation.
    private readonly Button _assistantLauncher = new();
    private readonly Border _assistantMini = new() { IsVisible = false };
    private string? _assistantMiniRenderedKey;

    public Button AssistantLauncher => _assistantLauncher;
    /// <summary>CST-043: the phone's navigation (Assistente, Notas, Grafos) in the bottom right corner.</summary>
    public Ui.RadialNav Navigation => _radial!;
    private Ui.RadialNav? _radial;
    public Border AssistantMini => _assistantMini;

    /// <summary>Called once after the editor and plugin surfaces have been added to the stage.</summary>
    private void InstallAssistantLauncher()
    {
        _assistantLauncher.ZIndex = 20; // Keep accessible when the Notes drawer is open.
        _assistantLauncher.Width = UrbeTokens.MinTouch;
        _assistantLauncher.Height = UrbeTokens.MinTouch;
        _assistantLauncher.MinWidth = UrbeTokens.MinTouch;
        _assistantLauncher.MinHeight = UrbeTokens.MinTouch;
        _assistantLauncher.Padding = new Thickness(0);
        _assistantLauncher.CornerRadius = new CornerRadius(24);
        _assistantLauncher.Background = UrbeTheme.Brush(UrbeTheme.Surface2, .94);
        _assistantLauncher.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _assistantLauncher.BorderThickness = new Thickness(1);
        _assistantLauncher.Content = UrbeTheme.Icon(UrbeTheme.Icons.Assistant, 19, UrbeTheme.Accent);
        _assistantLauncher.HorizontalAlignment = HorizontalAlignment.Right;
        _assistantLauncher.VerticalAlignment = VerticalAlignment.Bottom;
        _assistantLauncher.HorizontalContentAlignment = HorizontalAlignment.Center;
        _assistantLauncher.VerticalContentAlignment = VerticalAlignment.Center;
        _assistantLauncher.Margin = new Thickness(0, 0, 16, 16);
        _assistantLauncher.Opacity = .9;
        Avalonia.Automation.AutomationProperties.SetName(_assistantLauncher, "Abrir Assistente");
        ToolTip.SetTip(_assistantLauncher, "Assistente");
        // like the editor's own button: the block being written is in the text the Assistente sees
        _assistantLauncher.Click += (_, _) => OpenAssistantFromLauncher();

        _assistantMini.Background = UrbeTheme.Brush(UrbeTheme.Surface2, .98);
        _assistantMini.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _assistantMini.BorderThickness = new Thickness(1);
        _assistantMini.CornerRadius = new CornerRadius(UrbeTokens.Radius.L);
        _assistantMini.BoxShadow = UrbeTokens.Elevation.Low;
        _assistantMini.Padding = new Thickness(UrbeTokens.Space.M);
        _assistantMini.Margin = new Thickness(12, 0, 12, 76);
        _assistantMini.MaxWidth = 440;
        _assistantMini.ZIndex = 19; // Explorer drawer uses ZIndex=1; contextual UI must be above it.
        _assistantMini.HorizontalAlignment = HorizontalAlignment.Right;
        _assistantMini.VerticalAlignment = VerticalAlignment.Bottom;
        Avalonia.Automation.AutomationProperties.SetName(_assistantMini, "Assistente contextual");
        _stage.Children.Add(_assistantMini);
        _stage.Children.Add(Ui.UrbeMotion.Pressable(_assistantLauncher));

        // the same destinations as the rail; the Assistente first, straight up from the thumb
        _radial = new Ui.RadialNav(
        [
            new("Assistente", UrbeTheme.Icons.Assistant, "Assistente", OpenAssistantFromLauncher),
            new("Notas", UrbeTheme.Icons.Notes, "Notas e pastas do vault", () => _ = GoToNotesAsync()),
            new("Grafos", UrbeTheme.Icons.Layers, "Grafos: criar, conectar e navegar no vault", () => _ = ShowGraphsHomeAsync())
        ]) { ZIndex = 20 };   // over the Notes drawer, like the launcher
        _radial.SetActive(ActiveDestination);
        _stage.Children.Add(_radial);
        UpdateFloatingNav();
    }

    // like the editor's own button: the block being written is in the text the Assistente sees
    private void OpenAssistantFromLauncher()
    {
        if (Editor.IsVisible) Editor.FinishEditing();
        ToggleAssistant();
    }

    /// <summary>A phone shows the radial menu, a wide window its rail and the launcher; neither while the Assistente is open.</summary>
    private void UpdateFloatingNav()
    {
        bool phone = !(Bounds.Width >= WideBreakpoint);
        bool free = _assistant is not { IsVisible: true };
        _assistantLauncher.IsVisible = free && !phone;
        if (_radial is null) return;
        _radial.IsVisible = free && phone;
        if (!_radial.IsVisible) _radial.Close();
    }

    private void RefreshAssistantMini()
    {
        var panel = _assistant;
        bool showing = panel is { CompactActive: true, IsVisible: false };
        _assistantMini.IsVisible = showing;
        UpdateFloatingNav();
        var attention = panel?.PendingApproval is not null || panel?.SpecialistWaiting == true || panel?.Conversation.Runs.LastOrDefault()?.Stopped == "needs_input";
        _assistantLauncher.BorderBrush = UrbeTheme.Brush(attention ? UrbeTheme.Accent : UrbeTheme.Line2);
        Avalonia.Automation.AutomationProperties.SetName(_assistantLauncher, attention ? "Assistente: aguardando sua resposta" : "Abrir Assistente");
        _radial?.SetAttention(attention);
        if (!showing)
        {
            _assistantMiniRenderedKey = null;
            _assistantMini.Child = null;
            return;
        }

        // Leave live editors/buttons mounted while a tool streams more tokens. Don't erase user input.
        var key = panel!.CompactRenderKey;
        if (_assistantMiniRenderedKey == key && _assistantMini.Child is not null) return;
        _assistantMiniRenderedKey = key;

        // Keep the editor alive under this non-modal, bounded surface. Never move keyboard focus.
        _assistantMini.Child = new ScrollViewer
        {
            MaxHeight = 380,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = panel!.CompactContent()
        };

    }

    /// <summary>The Assistente panel, made the first time it opens (over the Explorer and the editor, under the dialogs).</summary>
    public AssistantPanel Assistant => _assistant ??= MakeAssistant();

    private AssistantPanel MakeAssistant()
    {
        IAssistantStorage storage = UrbeApp.AssistantFolder is { } folder ? new FolderAssistantStorage(folder) : new MemoryAssistantStorage();
        var panel = new AssistantPanel(this, storage) { ZIndex = 30 };
        _stage.Children.Add(panel);   // over the Explorer, the editor, the previews and the plugins' panels; the dialogs stay above
        PlaceAssistant(panel, Bounds.Width);
        panel.PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty) { UpdateNav(); RefreshAssistantMini(); }
        };
        panel.CompactChanged += RefreshAssistantMini;
        RefreshAssistantMini();
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

    IAssistantGraphs? IAssistantApp.Graphs => _vault is { IsConnected: true } ? new GraphsAdapter(this) : null;

    private sealed class GraphsAdapter(MainView view) : IAssistantGraphs
    {
        public IReadOnlyList<string> ListPaths() =>
            view._attachments.Where(GraphDocument.IsGraphPath)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();

        public async Task<string?> ReadAsync(string path)
        {
            if (!GraphDocument.IsGraphPath(path) || !ListPaths().Contains(path, StringComparer.OrdinalIgnoreCase)
                || view._vault is not { } vault) return null;
            var file = await vault.ReadFileAsync(path, long.MaxValue);
            return file?.Bytes is { } bytes ? System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF') : null;
        }

        public Task WriteAsync(string path, string json, bool create) => view.WriteGraphForAssistantAsync(path, json, create);
    }

    /// <summary>The vault's plugins for write_plugin: listed as Personalização lists them; turned on for their exact text.</summary>

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

    async Task<byte[]?> IAssistantApp.ReadGeneratedImageAsync(string path)
    {
        if (!_attachments.Contains(path) || _vault is not { IsConnected: true } vault) return null;
        // Bound decoded bytes per chat image; the full-resolution file remains in the vault.
        var file = await vault.ReadFileAsync(path, 8 * 1024 * 1024);
        return file?.Bytes;
    }

    async Task IAssistantApp.OpenGeneratedImageAsync(string path)
    {
        if (_attachments.Contains(path)) await ShowFileAsync(path);
    }

    Task IAssistantApp.ExportGeneratedImageAsync(string path) => DownloadFileAsync(path);

    async Task IAssistantApp.ExportGeneratedImagesAsync(IReadOnlyList<string> paths)
    {
        if (_vault is null) return;
        try
        {
            using var memory = new MemoryStream();
            using (var zip = new System.IO.Compression.ZipArchive(memory, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                long total = 0;
                foreach (var path in paths.Distinct(StringComparer.Ordinal))
                {
                    var read = await _vault.ReadFileAsync(path, 32L * 1024 * 1024);
                    if (read?.Bytes is not { } bytes) throw new IOException("Não foi possível ler " + path + ". Exporte individualmente.");
                    total += bytes.Length;
                    if (total > 128L * 1024 * 1024) throw new IOException("Conjunto grande demais para exportar de uma vez neste aparelho. Exporte as imagens individualmente.");
                    var entry = zip.CreateEntry(path.Replace('\\', '/'), System.IO.Compression.CompressionLevel.NoCompression);
                    await using var stream = entry.Open();
                    await stream.WriteAsync(bytes);
                }
            }
            if (await (SaveFileCopy ?? SaveAnyFileAsync)("Urbe-imagens.zip", memory.ToArray()) == true)
                ShowLifeNotice("Imagens exportadas.", Ui.ToastKind.Success);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { await Dialogs.AlertAsync("Exportar imagens", StorageProblems.Explain(ex)); }
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
        await SaveNoteAsync(new CityNote(doc.Path, content), expectedUndoContent: null, sealsByEngine: true);
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
        await RetargetGraphsAsync([(from, newPath)]);
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

    async Task IAssistantApp.WriteBackVerifiedUndoAsync(UrbeDocument current, string path, string before, string expectedAfter)
    {
        RequireWritable();
        // Undo must be the exact inverse of a recorded operation. Refuse a stale
        // or externally modified note, including a note open with unsaved edits.
        if (!string.Equals(current.Content, expectedAfter, StringComparison.Ordinal) ||
            !_contents.TryGetValue(current.Path, out var persisted) ||
            !string.Equals(persisted, expectedAfter, StringComparison.Ordinal))
            throw new InvalidOperationException("A nota mudou desde a operação da IA; não é possível desfazer com segurança.");
        bool editing = Editor.IsVisible && Editor.Current?.Path == current.Path;
        if (editing && Editor.HasChanges)
            throw new InvalidOperationException("Salve ou descarte as alterações da nota aberta antes de desfazer.");
        IAssistantApp app = this;
        var doc = current;
        if (!string.Equals(current.Path, path, StringComparison.Ordinal))
            doc = await app.RenameNoteAsync(current, path);
        // The narrow exemption allows reverting the exact saved AI operation.
        // Normal edits cannot pass a prior-version proof.
        if (!string.Equals(doc.Content, before, StringComparison.Ordinal))
        {
            await SaveNoteAsync(new CityNote(doc.Path, before), expectedUndoContent: expectedAfter);
            if (editing) Editor.Open(new CityNote(doc.Path, before));
            AfterAssistantWrite(doc.Path);
        }
    }

    async Task IAssistantApp.WriteBackAsync(UrbeDocument current, string path, string content)
    {
        IAssistantApp app = this;
        var doc = current;
        if (!string.Equals(current.Path, path, StringComparison.Ordinal)) doc = await app.RenameNoteAsync(current, path);
        if (doc.Content != content) await app.SaveNoteAsync(doc, content);
    }
}
