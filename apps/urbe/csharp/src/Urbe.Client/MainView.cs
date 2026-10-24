using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The app's screen (ADD-0028, DEC-0053): the Área de trabalho is the base, with the Explorer beside it (wide) or as
/// a drawer (narrow); the editor and the other screens come over them;
/// navigation rail (wide) or dock (narrow). The notes are the vault's files; their ids, dates and folders are the
/// <see cref="NoteLibrary"/>, recorded in .urbe/notas.json.
/// </summary>
public sealed partial class MainView : Grid
{
    private const double WideBreakpoint = 760;

    private readonly Panel _stage = new();
    private readonly StackPanel _rail = new();
    private readonly Grid _dock = new();
    private readonly Dictionary<string, string> _contents = new(StringComparer.Ordinal);
    private readonly IUrbeVaultStorage? _vault;
    private VaultSnapshot? _snapshot;
    private NoteLibrary? _library;
    private DocumentStore? _store;
    private NotesSaveScheduler? _notesSave;
    private bool _notesReadOnly;
    private LegacyTrash _trash = new();

    /// <summary>Favourites and recents of Notas, kept on the device (model.js localStorage).</summary>
    public ExplorerState ExplorerState { get; } = new(UrbeApp.LocalStatePath);
    private bool _trashReadOnly;

    private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public MainView(IUrbeVaultStorage? storage = null)
    {
        _vault = storage ?? UrbeApp.VaultStorageFactory?.Invoke();
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        RowDefinitions = new RowDefinitions("*,Auto");

        Explorer = new NotesExplorer { State = ExplorerState };
        Editor = new EditorOverlay { IsVisible = false, Dialogs = Dialogs, Notes = () => _contents.Select(n => new CityNote(n.Key, n.Value)) };
        Editor.EmbedFor = BuildEmbed;
        Editor.Properties.TagSource = () => _store is null ? [] : _store.List().SelectMany(d => d.Tags).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        Editor.PropertiesFor = path => _library?.PropertiesText(path, Now);
        // v23Atualizar: v23Dock.hidden = editor (the rail of a wide window stays)
        Editor.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) _dock.IsVisible = !Editor.IsVisible && !(Bounds.Width >= WideBreakpoint); };
        // overlays enter with a short fade + slide (UX.md P6); IsVisible flips at once, so nothing waits for the motion
        Ui.UrbeMotion.EnterOnShow(Editor, dy: 12);
        Explorer.Closed += CloseNotesOverEditor;
        // a backlink opens its note; the panel then shows that note's context (document:updated → renderContext)
        Editor.Context.BacklinkChosen += async path =>
        {
            // a followed backlink joins the editor's trail, so Voltar comes back here (DEC-0045, UC-37)
            // the note opens at once (the one left saves behind it, UC-69), and so does its context
            var going = Editor.NavigateAsync(new CityNote(path, _contents.GetValueOrDefault(path, "")));
            ShowEditorContext();
            await going;
        };
        // UC-71: a point or a row of Conexões opens its note, and the graph follows it (it joins the trail, like a backlink)
        Editor.Connections.SkyRequested += () =>
        {
            if (StillReading() || Editor.Current is not { } open || _store?.Get(open.Path) is not { } doc) return;
            Editor.Connections.IsVisible = false;
            _ = ShowConstellationAsync(doc.Id);
        };
        Editor.Connections.NoteChosen += async path =>
        {
            var going = Editor.NavigateAsync(new CityNote(path, _contents.GetValueOrDefault(path, "")));
            ShowEditorConnections();
            await going;
        };
        // [[link]] to a note that does not exist yet: created next to the open note, in its folder (DEC-0045, UC-37)
        Editor.CreateLinked = name => CreateNoteAsync(Editor.Current is { } open ? NoteLibrary.FolderOf(open.Path) : "", name, open: false);
        Explorer.SelectionAction += (action, paths) => { if (!StillReading()) _ = SelectionActionAsync(action, paths); };
        Explorer.MoreRequested += () => { if (!StillReading()) _ = ShowNotesMenuAsync(); };
        Explorer.NewRequested += () => { if (!StillReading()) _ = ShowNotesCreateMenuAsync(); };
        Explorer.CreateFirstNoteRequested += () => _ = CreateNoteInNotesAsync();
        Explorer.RestoreRequested += id => _ = RestoreFromTrashAsync(id);
        Explorer.FileRequested += path => { if (!StillReading()) _ = ShowFileAsync(path); };
        Explorer.FileMenuRequested += path => { if (!StillReading()) _ = ShowAttachmentActionsAsync(path, withView: true); };
        Explorer.FolderMenuRequested += folder => { if (!StillReading()) _ = ShowFolderMenuAsync(folder, inExplorer: true); };
        // UC-83: a row renamed in place goes through the same verified renames as the dialogs
        Explorer.RenameCommitted += (key, name) =>
        {
            if (StillReading()) return;
            _ = key.StartsWith(NotesExplorer.FolderPrefix, StringComparison.Ordinal)
                ? RenameFolderAsync(key[NotesExplorer.FolderPrefix.Length..], name)
                : RenameNoteAsync(key, name);
        };
        Explorer.MoveRequested += async (paths, folder) => { if (!StillReading()) await MoveSelectionToAsync(paths, folder); Explorer.ClearSelection(); };
        Explorer.FileMoveRequested += (path, folder) => { if (!StillReading()) _ = MoveAttachmentAsync(path, folder); };
        Explorer.PurgeRequested += id => _ = PurgeFromTrashAsync(id);
        Explorer.EmptyTrashRequested += () => _ = EmptyTrashAsync();
        Explorer.NoteRequested += OpenFromNotes;
        // UC-69: Voltar slides the editor away the way it came (a picture of it; the screen under it is there at once)
        Editor.Leaving += () => Ui.UrbeMotion.Ghost(Editor, _stage, dy: 12);
        Editor.Closed += (_, _) =>
        {
            Editor.Context.IsVisible = false;
            Editor.Connections.IsVisible = false;
            SaveEditorToMemory();
            RefreshNotes();
        };
        if (_vault is not null) Editor.SaveRequested += SaveNoteAsync;
        Editor.Editor.TextChanged += (_, _) => CaptureEditorHistory();
        Editor.MoreRequested += ShowEditorMenuAsync;
        // UC-69: a tap on the title renames the note; a save that failed after leaving the editor is told here
        Editor.RenameRequested += () => { if (Editor.Current is { } open) _ = RenameNoteAsync(open.Path); };
        Editor.SaveFailed += message => _notice.Show(message, TimeSpan.FromSeconds(8), "Tentar de novo", () => _ = Editor.RetrySavesAsync(), Ui.ToastKind.Warning);
        Editor.AssistantRequested += ToggleAssistant;
        Editor.AgentAsked += call => _ = AskAgentInNoteAsync(call);
        Editor.Shown += path => NoteOpened?.Invoke(path);   // plugins: "nota:aberta"

        Explorer.TitleMark = _vaultDot;
        Explorer.HeaderActions.Children.Add(SettingsButton());
        _topButtons = Explorer.HeaderActions;   // the plugins' buttons go before Configurações
        PaintDot();
        Explorer.Status = "Nenhuma pasta aberta";
        _stage.Children.Add(Explorer);
        _stage.Children.Add(Editor);
        WireWorkspace();
        FilePreview.Storage = _vault;
        FilePreview.ActionsRequested += path => _ = ShowAttachmentActionsAsync(path);
        FilePreview.PageActionsRequested += path => _ = ShowPageMenuAsync(path);
        _stage.Children.Add(FilePreview);
        _stage.Children.Add(_notice);
        _stage.Children.Add(_pluginPanels);   // the plugins' panels, over the Explorer and the editor (ADD-0021)
        InstallAssistantLauncher();          // contextual AI lives over the editor, not in a second destination
        Customize = new CustomizePanel(Customization, Dialogs);
        Customize.OpenFileRequested += path => Editor.Open(new CityNote(path, _contents.GetValueOrDefault(path, "")));
        Customize.NativePlugins = NativePlugins;
        Customize.SetNativePlugin = SetNativePlugin;
        Customization.Applied += ApplyCustomization;
        Customization.Applied += SyncPlugins;   // the safe mode turns every plugin off
        Customization.PluginsChanged += SyncPlugins;
        SetColumn(_stage, 1);
        Children.Add(_stage);

        BuildRail();
        SetColumn(_rail, 0);
        Children.Add(_rail);
        BuildDock();
        SetRow(_dock, 1);
        SetColumnSpan(_dock, 2);
        Children.Add(_dock);

        // .uc and .udlg are position:fixed over the whole app, bottom navigation included
        // #mathPane (z-index 10400), under the dialogs
        Editor.AskFormula = (tex, block, editing) => Formula.ShowAsync(tex, block, editing);
        foreach (var overlay in new Control[] { Customize, Formula, ActionSheet, Dialogs })
        {
            SetRowSpan(overlay, 2);
            SetColumnSpan(overlay, 2);
            Children.Add(overlay);
        }
        Search = new SearchFeature(this, () => _contents.Select(n => new CityNote(n.Key, n.Value)).ToList());
        Search.UserShortcuts = MyShortcuts;
        RestoreTouchDebug();
        WatchErrors();
        WatchSession();
        WatchTips();
        WireSplit();
    }

    /// <summary>quick-open.js / command-palette.js / keymap.js: Buscar notas, Comandos and their shortcuts.</summary>
    public SearchFeature Search { get; }

    /// <summary>document.open for a note of the vault: the editor over the Explorer.</summary>
    public void OpenNoteByPath(string path)
    {
        CloseNotesOverEditor();
        Editor.Open(new CityNote(path, _contents.GetValueOrDefault(path, "")));
    }

    // ---- avisar(): a discreet pill under the top, 4.5 s, fading in .6 s (the shared toast, UC-69)
    private readonly Ui.UrbeToast _notice = new(top: 70, fade: TimeSpan.FromSeconds(.6));

    /// <summary>The text of the notice currently shown, or null.</summary>
    public string? NoticeText => _notice.Text;

    private readonly List<string> _notices = [];

    /// <summary>The notices shown lately (the last 20, oldest first): each notice is shown 4.5 s (tests).</summary>
    public IReadOnlyList<string> Notices => _notices;

    private void ShowLifeNotice(string text, Ui.ToastKind kind = Ui.ToastKind.Info)
    {
        _notices.Add(text);
        if (_notices.Count > 20) _notices.RemoveAt(0);
        _notice.Show(text, kind: kind);
    }

    public NotesExplorer Explorer { get; }
    public EditorOverlay Editor { get; }

    /// <summary>The preview of a file that is not a note (openFilePreview).</summary>
    public FilePreviewPanel FilePreview { get; } = new();

    /// <summary>
    /// The Explorer of the vault (1.8.4 Notas): back to its tree, up to date. Beside the Área de trabalho on a wide
    /// window; over it (a drawer) on a phone, and over the editor while one is open.
    /// </summary>
    public void ShowNotes()
    {
        Explorer.ShowTrash(null);
        Explorer.ShowCollection(null);
        RefreshNotes();
        if (Editor.IsVisible || !SidebarDocked) ShowNotesOverEditor();
    }

    /// <summary>
    /// ui.explorer.open from the editor ("Mostrar nas notas"): Notas over the open editor (theme.css: z-index 52 while
    /// #editorFull is open); Fechar shows the editor again, a note tapped there opens in it.
    /// </summary>
    public void ShowNotesOverEditor()
    {
        Explorer.ShowTrash(null);
        RefreshNotes();
        Explorer.Overlaid = true;
        Explorer.ZIndex = 1;
        PlaceSidebar();
        // a phone's drawer slides in from the left (UX.md P6)
        if (!SidebarDocked) Ui.UrbeMotion.Enter(Explorer, dx: -24, dy: 0);
    }

    /// <summary>Notas goes back under the editor.</summary>
    private void CloseNotesOverEditor()
    {
        if (!Explorer.Overlaid) return;
        // UC-69: the phone's drawer slides back to the left
        if (!SidebarDocked) Ui.UrbeMotion.Ghost(Explorer, _stage, dx: -24, dy: 0);
        Explorer.Overlaid = false;
        Explorer.ZIndex = 0;
        PlaceSidebar();
    }

    /// <summary>True while Notas is shown over the open editor.</summary>
    public bool NotesOverEditor => Explorer.Overlaid;

    /// <summary>editor.contextPanel: Outline of the text being edited and the notes that link to it (knowledge.backlinks).</summary>
    public void ShowEditorContext()
    {
        if (Editor.Current is not { } note) return;
        var backlinks = new List<(string, string)>();
        if (_store?.Get(note.Path) is { } doc)
        {
            using var index = new KnowledgeIndex(_store);
            backlinks.AddRange(index.Backlinks(doc.Id).Select(d => (d.Title, d.Path)));
        }
        Editor.Context.Show(note.Content, backlinks);
    }

    /// <summary>"Conexões" (UC-71): the local graph of the open note, from its links as they are now.</summary>
    public void ShowEditorConnections()
    {
        if (Editor.Current is not { } note || _store is null) return;
        using var index = new KnowledgeIndex(_store);
        if (LinkGraph.Around(_store, index, note.Path) is { } graph) Editor.Connections.Show(graph);
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (_vault is not null)
            {
                Explorer.Opening = Workspace.Opening = true;
                // UC-78: the tree of the last opening at once, from its names, while the folder is read
                ShowLastNames();
                try
                {
                    var snapshot = await _vault.RestoreAsync();
                    // UC-59: the last note opens as soon as the vault is read
                    if (snapshot is not null) { await ShowVaultAsync(snapshot, restoreSession: true); return; }
                }
                catch (Exception ex) { ShowLifeNotice("Vault indisponível: " + ex.Message, Ui.ToastKind.Warning); }
            }
            // no folder yet: no notes (DEC-0045, UC-40/UC-64: the Tutorial is never created in the vault by itself)
            await LoadAsync([]);
        }
        finally
        {
            LeaveLastNames();
            Explorer.Opening = Workspace.Opening = false;
            MarkReady();   // UC-56: a widget's quick note waits for the first vault
            OfferFirstRun();   // UC-58: no folder yet, the first time on this device
        }
    }

    public async Task OpenVaultAsync()
    {
        if (_vault is null) return;
        try
        {
            Explorer.Opening = Workspace.Opening = true;   // only shows while the list is empty
            var snapshot = await _vault.PickAsync();
            if (snapshot is not null) await ShowVaultAsync(snapshot);
        }
        catch (Exception ex)
        {
            ShowLifeNotice("Não foi possível abrir a pasta: " + ex.Message, Ui.ToastKind.Warning);
        }
        finally { Explorer.Opening = Workspace.Opening = false; }
    }

    private async Task ShowVaultAsync(VaultSnapshot snapshot, bool restoreSession = false)
    {
        // an import interrupted (app closed, error) left its journal: the vault is put back as it was first
        if (snapshot.Files.ContainsKey(ImportTransaction.JournalPath) && _vault?.ImportStorage is { } importer)
        {
            try
            {
                if (await new ImportTransaction(importer).RecoverAsync() && await _vault.ReloadAsync() is { } recovered) snapshot = recovered;
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or InvalidOperationException) { /* the vault stays protected (Plan refuses) */ }
        }
        // the previous vault's pending record is written first (abrirCidade: rodarSinc(true) + flush)
        if (_notesSave is { } previous)
        {
            try { await previous.FlushAsync(); } catch { /* shown by the state; the new vault opens anyway */ }
            previous.Stop();
            _notesSave = null;
        }
        _snapshot = snapshot;
        // workspace.js readSide('trash'): the v2 file, else the v1 file; another version is kept and not written
        string? Text(string path) => snapshot.Files.FirstOrDefault(f => string.Equals(f.Key, path, StringComparison.OrdinalIgnoreCase)).Value is { } file
            ? System.Text.Encoding.UTF8.GetString(file.Bytes.Span).TrimStart('﻿') : null;
        _trash = LegacyTrash.Read(Text(LegacyTrash.Path), Text(LegacyTrash.PathV1), out _trashReadOnly);
        LoadHistory(Text);
        LoadArea(Text(AreaLayout.Path));
        _skyText = Text(Constellation.Path);
        Editor.SaveOnClose = !snapshot.IsReadOnly;
        var notes = snapshot.Documents.Where(d => d.Text is not null).Select(d => new CityNote(d.Path, d.Text!)).ToArray();
        // .urbe/notas.json: ids, dates and folders; a vault without it takes them from its 1.8.4 map, read only
        var record = Text(NoteLibrary.Path);
        _notesReadOnly = NoteLibrary.IsFuture(record);
        var ids = OpeningIds(snapshot, notes, record);
        _attachments.Clear();
        if (_vault is not null)
        {
            // every other file of the vault, by name only, for the Explorer (DEC-0045, UC-38); hidden folders (.urbe) stay out
            try { foreach (var path in await _vault.ListFilePathsAsync()) _attachments.Add(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { /* the Explorer shows the notes */ }
        }
        _lastNames = null;   // UC-78: the folder read replaces the names of the last opening
        Open(NoteLibrary.Open(notes.Select(n => (n.Path, n.Content, ids[n.Path])).ToList(), record, snapshot.MapJson, _attachments), notes);
        // the other files only: a note deleted later must not stay behind as a file of its folder
        _attachments.ExceptWith(notes.Select(n => n.Path));
        Explorer.Status = snapshot.IsReadOnly ? "Somente leitura" : null;
        // v23Atualizar: the brand is Disco.cidade (the vault folder's name), 'Urbe' without one
        Explorer.HomeTitle = _vault?.VaultName is { Length: > 0 } folder ? folder : "Urbe";
        Workspace.Title = Explorer.HomeTitle;
        // a note tapped while the folder was read opens now, instead of the last session's
        if (TakeOpenWhenRead() is { } tapped) OpenFromNotes(new CityNote(tapped, _contents[tapped]));
        else if (restoreSession) RestoreSession();
        VaultNotices(snapshot);
        TipsLoaded();
        StartNotesSaves(snapshot, record);
        RememberNames();
        // tutorial.js: workspace:loaded → setTimeout(semear, 300)
    }

    /// <summary>
    /// The id of each note at the opening: the one .urbe/notas.json (or, before it exists, the 1.8.4 map) gives its path;
    /// then .urbe/identity.json for the same path and for a note renamed or moved outside the app while it was closed
    /// (same content, LegacyIdentity.Reconcile; vault-notices.js tells what was recognized); a new id for the rest.
    /// </summary>
    private Dictionary<string, string> OpeningIds(VaultSnapshot snapshot, IReadOnlyList<CityNote> notes, string? record)
    {
        _identity = null;
        _identityOnDisk = null;
        _identityReadOnly = false;
        _identityNotice = null;
        var file = snapshot.Files.FirstOrDefault(f => string.Equals(f.Key, VaultIdentity.Path, StringComparison.OrdinalIgnoreCase)).Value;
        if (file is not null)
        {
            _identityOnDisk = file.Bytes.ToArray();
            _identity = VaultIdentity.Parse(file.Bytes, out var future);
            _identityReadOnly = future;
        }
        var known = NoteLibrary.Ids(notes.Select(n => n.Path), record, snapshot.MapJson);
        var items = notes.Select(n => new LegacyIdentity.Item(n.Path, n.Content, known.GetValueOrDefault(n.Path))).ToList();
        if (_identity is not null && !snapshot.RecoveredFromJournal)
        {
            var result = LegacyIdentity.Reconcile(items, _identity);
            if (result.Ambiguous.Count > 0)
                _identityNotice = "Notas idênticas apareceram fora do app (" + string.Join("; ", result.Ambiguous.Select(a => string.Join(", ", a.To))) + "): entraram como notas novas.";
            else if (result.Renames.Count == 1)
                _identityNotice = "Uma nota renomeada fora do app foi reconhecida: " + result.Renames[0].To + ".";
            else if (result.Renames.Count > 1)
                _identityNotice = result.Renames.Count + " notas renomeadas ou movidas fora do app foram reconhecidas.";
        }
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var i in items) if (i.Id is { } id && used.Add(id)) ids[i.Path] = id;
        // the rest: the id the reader gave it when no other note has it, else a new one
        foreach (var d in snapshot.Documents)
            if (d.Text is not null && !ids.ContainsKey(d.Path)) ids[d.Path] = used.Add(d.Id) ? d.Id : DocumentModel.CreateDocumentId();
        return ids;
    }

    /// <summary>The notes of the vault on the screen: the Explorer, the Personalização and the editor's properties.</summary>
    private void Open(NoteLibrary? library, IReadOnlyList<CityNote> notes)
    {
        _library = library;
        _store = library?.Store;
        _contents.Clear();
        foreach (var n in notes) _contents[n.Path] = n.Content;
        RefreshNotes();
        // workspace:loaded → customize recarregar(); it writes only with a writable vault folder
        Customization.Writer = _vault is { IsConnected: true } && _snapshot is { IsReadOnly: false } ? WriteVaultFileAsync : null;
        Customization.Load(_contents);
        if (Editor.IsVisible) Editor.RefreshProperties();
    }

    /// <summary>
    /// From now on the notes' ids, dates and folders are written to .urbe/notas.json (with .urbe/identity.json derived from
    /// them): only for a real, writable vault, never over a record of a newer version. The first save of a vault opened
    /// before writes its record for the first time; .urbe/mapa.json and .urbe/mundo.json are never written (ADD-0028).
    /// </summary>
    private void StartNotesSaves(VaultSnapshot snapshot, string? record)
    {
        if (_vault is null || snapshot.IsReadOnly || snapshot.RecoveredFromJournal || _notesReadOnly) return;
        var library = _library;
        NotesSaveScheduler? scheduler = null;
        scheduler = new NotesSaveScheduler(() => ReferenceEquals(_library, library) ? library?.Serialize() : null, async json =>
        {
            await _vault.SaveNotesRecordAsync(json);
            await SaveIdentityAsync();
        }, record);
        scheduler.StateChanged += (_, _) =>
        {
            Explorer.Status = scheduler.State switch { "error" => "Erro ao salvar o registro das notas", _ => _snapshot?.IsReadOnly == true ? "Somente leitura" : null };
            // statusSinc: 'gravando' while writing, 'erro' on a failure, else ok
            _saveState = scheduler.State switch { "saving" => "gravando", "error" => "erro", _ => "ok" };
            if (scheduler.State == "saved") _lastSavedAt = DateTimeOffset.UtcNow;
            PaintDot();
        };
        _notesSave = scheduler;
        // abrirCidade ends with marcarSinc(): the first save writes the record of the opened vault when it changed
        scheduler.MarkSync();
    }

    /// <summary>The notes' ids, dates or folders changed: their record is written soon (marcarSinc).</summary>
    private void NotesChanged() => _notesSave?.MarkSync();

    /// <summary>The save of the notes' record (null with no folder open and for read-only vaults).</summary>
    public NotesSaveScheduler? NotesSave => _notesSave;

    /// <summary>The documents of the open vault, or null.</summary>
    public DocumentStore? Documents => _store;

    /// <summary>The notes of the open vault (documents and folders), or null.</summary>
    public NoteLibrary? Library => _library;

    /// <summary>flushPending: write a pending record now (window closing, app going to the background).</summary>
    public Task FlushPendingAsync() => _notesSave?.FlushAsync() ?? Task.CompletedTask;

    private async Task SaveNoteAsync(CityNote note)
    {
        if (_vault is null || !_vault.IsConnected || _snapshot is null)
            throw new InvalidOperationException("Selecione uma pasta real para gravar.");
        if (_snapshot.IsReadOnly)
            throw new InvalidOperationException("Vault em modo somente leitura.");
        await _vault.SaveExistingTextFileAsync(note.Path, note.Content);
        _contents[note.Path] = note.Content;
        RecordSaved(note.Path, note.Content);
        await SaveHistoryAsync();
        NoteSaved?.Invoke(note.Path);   // plugins: "nota:salva"
        if (note.Path == EditorLayout.Path) ApplyEditorLayout();
    }

    /// <summary>
    /// 1.8.4 editor input: modified = nowDate(); session.record → document:updated → persistence schedule() + marcarSinc()
    /// </summary>
    private void RecordSaved(string path, string content)
    {
        if (_store?.Get(path) is not { } doc || doc.Content == content) return;
        _library!.Edited(path, content, Now);
        NotesChanged();
    }

    /// <summary>
    /// Opens these notes with no vault folder (no folder chosen yet, the tests): nothing is written. The Explorer shows
    /// them and the editor opens them; creating notes asks for a folder first.
    /// </summary>
    public Task LoadAsync(IReadOnlyList<CityNote> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var library = NoteLibrary.Open(files.Select(f => (f.Path, f.Content, DocumentModel.CreateDocumentId())).ToList(), null, null);
        Open(library, files);
        return Task.CompletedTask;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        bool wide = e.NewSize.Width >= WideBreakpoint;
        _rail.IsVisible = wide;
        _dock.IsVisible = !wide && !Editor.IsVisible;
        PlaceAssistant(_assistant, e.NewSize.Width);
    }

    // ---------------- the Explorer's header ----------------

    private readonly Border _vaultDot = new() { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
    private string _saveState = "ok";
    private StackPanel? _topButtons;

    /// <summary>#v23VaultDot: ok (green), gravando (warn) or erro (danger), with a 3px ring of the same colour at 15 %.</summary>
    private void PaintDot()
    {
        var color = _saveState switch { "gravando" => UrbeTheme.Warn, "erro" => UrbeTheme.Danger, _ => UrbeTheme.Ok };
        _vaultDot.Background = UrbeTheme.Brush(color);
        _vaultDot.BoxShadow = BoxShadows.Parse("0 0 0 3 #26" + color[1..]);   // the ring: the colour at 15 %
        ToolTip.SetTip(_vaultDot, _saveState switch { "gravando" => "Gravando…", "erro" => "Erro ao gravar", _ => "Tudo salvo" });
    }

    /// <summary>The state of the dot (statusSinc), for the tests.</summary>
    public string SaveDotState => _saveState;

    /// <summary>
    /// settings.js: "Configurações" with what the native app has — Personalização, Tutorial, Exportar tudo (.zip),
    /// Importar arquivos, Pasta no dispositivo (the vault folder), Assistente, Páginas and Diagnóstico de toques.
    /// </summary>
    public Task ShowSettingsAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var items = new List<DialogItem>
        {
            new("Personalização", UrbeTheme.Icons.Brush, "Tema, cores, fontes, editor, estilos e plugins", Run: () => { Customize.Open(); return Task.CompletedTask; }),
            new("Tutorial", UrbeTheme.Icons.Book, "Guia completo de tudo o que o Urbe faz", Run: ShowTutorialMenuAsync),
            new("Atalhos", UrbeTheme.Icons.List, "Os atalhos de teclado, e os seus em atalhos.json", Run: ShowShortcutsAsync),
            new("Barra do editor e menu /", UrbeTheme.Icons.Pencil, "Quais botões e em que ordem, em editor.json", Run: EditEditorLayoutAsync),
            new("Configuração inicial", UrbeTheme.Icons.Select, "Pasta, aparência e Assistente, passo a passo", Run: () => { ShowFirstRun(); return Task.CompletedTask; })
        };
        if (_vault is not null)
            items.Add(new("Exportar tudo (.zip)", UrbeTheme.Icons.Download, "Cópia de segurança de todas as notas", Run: () => ExportAsync()));
        items.Add(new("Importar arquivos", UrbeTheme.Icons.Upload, "Notas, pastas ou .zip do aparelho", Run: () => ImportAsync()));
        if (_vault is not null)
            items.Add(new("Pasta no dispositivo", UrbeTheme.Icons.Storage, "Onde os arquivos ficam salvos", Run: () => OpenVaultAsync()));
        items.Add(new("Assistente", UrbeTheme.Icons.Assistant, "Agentes que pesquisam, escrevem e organizam o vault", Run: () => { ToggleAssistant(); return Task.CompletedTask; }));
        items.Add(new("Páginas", UrbeTheme.Icons.Page, "Sites e páginas HTML do vault", Run: () => { ShowPages(); return Task.CompletedTask; }));
        items.Add(new("Diagnóstico de teclado e toques", UrbeTheme.Icons.Select, TouchDebugOn ? "Ligado — toque para desligar" : "Registrar foco, rolagem, IME e toques; desligado por padrão", Run: ToggleTouchDebugAsync));
        if (UpdateItem() is { } update) items.Add(update);
        if (CrashLog.Read().Length > 0)
            items.Add(new("Relatório de erros", UrbeTheme.Icons.List, "Erros que o Urbe registrou neste aparelho", Run: ShowErrorReportAsync));
        return Dialogs.MenuAsync("Configurações", items);
    }

    private Button SettingsButton()
    {
        var b = RoundButton(UrbeTheme.Icons.Settings, "Configurações");
        b.Click += (_, _) => _ = ShowSettingsAsync();
        return b;
    }

    /// <summary>A header button (44×44, radius 12, a 22px icon): Configurações and the plugins' buttons.</summary>
    private static Button RoundButton(string icon, string tip)
    {
        var b = new Button
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Content = UrbeTheme.Icon(icon, 22, UrbeTheme.Text2, 1.8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        ToolTip.SetTip(b, tip);
        Avalonia.Automation.AutomationProperties.SetName(b, tip);
        return Ui.UrbeMotion.Pressable(b);
    }

    /// <summary>Personalização (customize.js): Personalização/tema.json of the open vault.</summary>
    public Customization Customization { get; } = new();
    public CustomizePanel Customize { get; }

    /// <summary>The Personalização applied: theme, text, shape and editor.</summary>
    private void ApplyCustomization()
    {
        UrbeAppearance.Apply(this);   // Personalização: tema, texto, forma e editor
        if (Customization.Config["editor"] is System.Text.Json.Nodes.JsonObject ed) Editor.ApplyOptions((string?)ed["largura"], (string?)ed["modoInicial"]);
        ApplyEditorLayout();
        if (Editor.IsVisible) Editor.RefreshVisual();   // tag colours (DEC-0045, UC-43)
    }

    /// <summary>UC-61: Personalização/editor.json — the editor's bar and "/" menu.</summary>
    private void ApplyEditorLayout()
    {
        var (bar, menu) = EditorLayout.Parse(_contents.GetValueOrDefault(EditorLayout.Path), EditorOverlay.ToolbarIds,
            [.. LegacyEditorTools.FilterSlashWithAddons("").Select(x => x.Act)]);
        Editor.ApplyLayout(bar, menu);
    }

    /// <summary>The customize.js writer (docs.upsert of a Personalização file): the vault file, and its document.</summary>
    private async Task WriteVaultFileAsync(string path, string content, bool exists)
    {
        if (_vault is null || !_vault.IsConnected || _snapshot is null || _snapshot.IsReadOnly)
            throw new InvalidOperationException("Selecione uma pasta real para gravar.");
        if (exists) await _vault.SaveExistingTextFileAsync(path, content);
        else await _vault.CreateTextFileAsync(path, content);
        _contents[path] = content;
        // docs.upsert({...d, path, content}, {source:'customize'}): the document follows the file
        if (_library is not null)
        {
            if (_store!.Get(path) is null) _library.AddNote(path, content, Now, "customize");
            else _library.Edited(path, content, Now);
            NotesChanged();
        }
        RefreshNotes();
    }

    /// <summary>UrbeDialogs: one dialog at a time over everything.</summary>
    public UrbeDialogs Dialogs { get; } = new();

    /// <summary>The formula pane of the editor (math/editor.js #mathPane).</summary>
    public FormulaPane Formula { get; } = new();

    /// <summary>
    /// v21CreateFileInRegion: the name dialog, a name unique in <paramref name="folder"/> ("" = the
    /// root), the file written to the vault and the editor open. Nothing is created in memory only: without a writable
    /// vault folder the note is not created.
    /// </summary>
    public async Task<CityNote?> CreateNoteAsync(string? folder = null, string? typedName = null, bool open = true)
    {
        if (_library is not { } library) return null;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Nova nota", "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para criar notas.");
            return null;
        }
        var typed = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Nova nota", Label: "Nome", Placeholder: "Ex.: Ideias",
            Hint: "Sem extensão, vira .md. Também aceita .txt, .html, .css, .js, .json, .yaml e .csv.", Confirm: "Criar nota"));
        if (typed is null) return null;
        var (path, content) = library.PlanNewNote(folder ?? "", typed);
        try
        {
            await _vault!.CreateTextFileAsync(path, content);
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível criar a nota", ex.Message);
            return null;
        }
        _contents[path] = content;
        library.AddNote(path, content, Now);
        NotesChanged();
        RefreshNotes();
        var note = new CityNote(path, content);
        if (open)
        {
            CloseNotesOverEditor();
            Editor.Open(note);
        }
        return note;
    }

    /// <summary>The sheet of the editor's "Mais ações" (UX.md P4, P8: it rises from the bottom, a swipe down closes it).</summary>
    public Ui.BottomSheet ActionSheet { get; } = new();

    /// <summary>
    /// The editor's "Mais ações" (v23 overflow → abrirMenuExplorer(nomeCompletoNota(currentFile), …)) with the
    /// actions the native app has, in the 1.8.4's order: Renomear, Mover para outra pasta, Mostrar nas notas, Versões
    /// anteriores, Conexões (UC-71), Estrutura e backlinks, Fixar em painel and Mover para a lixeira.
    /// </summary>
    private Task ShowEditorMenuAsync(EditorOverlay editor)
    {
        if (Dialogs.IsOpen || ActionSheet.IsOpen || editor.Current is not { } note) return Task.CompletedTask;
        return SheetMenuAsync(NoteLibrary.FullNoteName(note.Path),
        [
            new DialogItem(editor.ReadOnly ? "Modo edição" : "Modo leitura", editor.ReadOnly ? UrbeTheme.Icons.Pencil : UrbeTheme.Icons.Book,
                editor.ReadOnly ? "Volta a escrever na nota." : "Só ler: nada é alterado sem querer.", Run: () => { editor.ReadOnly = !editor.ReadOnly; return Task.CompletedTask; }),
            new DialogItem("Renomear", UrbeTheme.Icons.Pencil, Run: () => RenameNoteAsync(note.Path)),
            new DialogItem("Mover para outra pasta", UrbeTheme.Icons.Folder, Run: async () =>
            {
                // UC-54: move from inside the editor (the note stays open at its new path)
                if (await ChooseFolderAsync() is { } folder) await MoveNotesToAsync([note.Path], folder);
            }),
            new DialogItem("Mostrar nas notas", UrbeTheme.Icons.Notes, Run: () => { ShowNotesOverEditor(); return Task.CompletedTask; }),
            new DialogItem("Versões anteriores", UrbeTheme.Icons.Restore, Run: ShowVersionsAsync),
            new DialogItem("Conexões", UrbeTheme.Icons.Connections, "As notas que ela cita e as que a citam.", Run: () => { ShowEditorConnections(); return Task.CompletedTask; }),
            new DialogItem("Estrutura e backlinks", UrbeTheme.Icons.List, Run: () => { ShowEditorContext(); return Task.CompletedTask; }),
            new DialogItem("Criar página daqui", UrbeTheme.Icons.Layers, "Uma página com esta nota.", Run: async () => { if (!editor.HasChanges || await editor.SaveAsync()) await CreatePageFromAsync(notePath: note.Path); }),
            // DEC-0045, UC-39: the selection (or the whole note) stays at the top while writing
            new DialogItem("Fixar em painel", UrbeTheme.Icons.Pin, "A seleção, ou a nota inteira, fica no topo enquanto você escreve.",
                Run: () => { editor.PinSelectionOrNote(); return Task.CompletedTask; }),
            new DialogItem("Mover para a lixeira", UrbeTheme.Icons.Trash, Danger: true, Run: () => DeleteNoteAsync(note.Path))
        ]);
    }

    /// <summary>A menu in the <see cref="ActionSheet"/>: a title and one 56 dp row per action; a choice closes it, then runs.</summary>
    private Task SheetMenuAsync(string title, IReadOnlyList<DialogItem> items)
    {
        var done = new TaskCompletionSource();
        var list = new StackPanel { Spacing = 2 };
        list.Children.Add(new TextBlock
        {
            Text = title, FontFamily = UrbeTheme.UiFont, FontSize = UrbeTokens.Type.Title.Size, FontWeight = FontWeight.Bold, TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextPrimary), Margin = new Thickness(UrbeTokens.Space.Xs, 0, UrbeTokens.Space.Xs, UrbeTokens.Space.Xs)
        });
        bool chosen = false;
        foreach (var x in items)
        {
            var item = x;
            var ink = item.Danger ? UrbeTokens.Color.Danger : UrbeTokens.Color.TextPrimary;
            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = item.Label, FontFamily = UrbeTheme.UiFont, FontSize = UrbeTokens.Type.Body.Size, FontWeight = FontWeight.Medium, Foreground = UrbeTheme.Brush(ink) });
            if (item.Detail is not null)
                text.Children.Add(new TextBlock { Text = item.Detail, FontFamily = UrbeTheme.UiFont, FontSize = UrbeTokens.Type.Footnote.Size, Foreground = UrbeTheme.Brush(UrbeTokens.Color.TextTertiary), TextTrimming = TextTrimming.CharacterEllipsis });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = UrbeTokens.Space.M };
            if (item.Icon is not null)
                row.Children.Add(new Border
                {
                    Width = 40, Height = 40, CornerRadius = new CornerRadius(UrbeTokens.Radius.M),
                    Background = item.Danger ? UrbeTheme.Brush(UrbeTokens.Color.Danger, .12) : UrbeTheme.Brush(UrbeTokens.Color.SurfaceOverlay),
                    Child = new Border { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Child = UrbeTheme.Icon(item.Icon, 20, item.Danger ? UrbeTokens.Color.Danger : UrbeTokens.Color.TextSecondary, 1.8) }
                });
            row.Children.Add(text);
            var b = new Button
            {
                Content = row, MinHeight = 56, Padding = new Thickness(UrbeTokens.Space.Xs, UrbeTokens.Space.Xxs), CornerRadius = new CornerRadius(UrbeTokens.Radius.M),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center
            };
            b.Resources["ButtonBackgroundPointerOver"] = UrbeTheme.Brush(UrbeTokens.Color.SurfaceOverlay, .6);
            b.Resources["ButtonBackgroundPressed"] = UrbeTheme.Brush(UrbeTokens.Color.SurfaceOverlay);
            Avalonia.Automation.AutomationProperties.SetName(b, item.Detail is null ? item.Label : item.Label + ". " + item.Detail);
            Ui.UrbeMotion.Pressable(b, haptic: true);
            b.Click += async (_, _) =>
            {
                if (chosen) return;
                chosen = true;
                _ = ActionSheet.CloseAsync();
                try { if (item.Run is { } run) await run(); }
                finally { done.TrySetResult(); }
            };
            list.Children.Add(b);
        }
        void OnClosed() { ActionSheet.Closed -= OnClosed; if (!chosen) done.TrySetResult(); }
        ActionSheet.Closed += OnClosed;
        ActionSheet.Open(new ScrollViewer { Content = list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        return done.Task;
    }

    /// <summary>
    /// renomearNota (final, app.js ≈2397): UD.prompt "Renomear" with the full file name, v21UniqueName in the note's
    /// folder and aplicarNomeCompletoNota. The file is moved in the vault FIRST, verified, and only then the document
    /// follows, so nothing is renamed in memory only. Unsaved text of the open note is saved before the move.
    /// </summary>
    public async Task RenameNoteAsync(string path, string? typedName = null)
    {
        if (_library is not { } library || _store?.Get(path) is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Renomear", "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para renomear notas.");
            return;
        }
        var typed = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Renomear", Label: "Nome do arquivo",
            Value: NoteLibrary.FullNoteName(path), Hint: "Mantenha a extensão (.md, .txt, .html…) para preservar o tipo.", Confirm: "Renomear"));
        if (typed is null) return;
        var plan = library.PlanRename(path, typed);
        var editing = Editor.IsVisible && Editor.Current?.Path == path;
        if (editing && Editor.HasChanges && !await Editor.SaveAsync())
        {
            await Dialogs.AlertAsync("Não foi possível renomear", "A nota tem alterações que não foram salvas no vault.");
            return;
        }
        if (string.Equals(plan.Path, path, StringComparison.Ordinal)) return;
        try
        {
            await _vault!.MoveTextFileAsync(path, plan.Path);
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível renomear", ex.Message);
            return;
        }
        if (_contents.Remove(path, out var content)) _contents[plan.Path] = content;
        // aplicarNomeCompletoNota: the same document at the new path, modified today
        if (_store.Get(path) is { } doc)
        {
            var moved = NoteLibrary.MoveDocument(_store, doc, plan.Path);
            library.Edited(moved.Path, moved.Content, Now);
        }
        NotesChanged();
        RefreshNotes();
        if (editing)
        {
            Editor.Renamed(plan.Path);
            Editor.Notify("Arquivo renomeado.");
        }
    }

    /// <summary>
    /// removerNota (app.js ≈1852): UD.confirm "Mover para a lixeira?", explorer.delete → trash.trash (the document kept
    /// in .urbe/trash.v2.json, removed from the store) and the editor closes. The trash is written and verified first,
    /// then the file is removed, and only then the document follows. Nothing is deleted in memory only.
    /// </summary>
    public async Task<bool> DeleteNoteAsync(string path, bool confirmed = false)
    {
        if (_store?.Get(path) is null) return false;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Excluir", "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para excluir notas.");
            return false;
        }
        if (_trashReadOnly)
        {
            await Dialogs.AlertAsync("Excluir", "A lixeira deste vault é de outra versão do Urbe e foi preservada; nada é excluído.");
            return false;
        }
        if (!confirmed && !await Dialogs.ConfirmAsync("Mover para a lixeira?",
                "“" + NoteLibrary.FullNoteName(path) + "” poderá ser restaurada pela Lixeira, em Notas.", "Mover para a lixeira", danger: true))
            return false;
        var editing = Editor.IsVisible && Editor.Current?.Path == path;
        if (editing && Editor.HasChanges && !await Editor.SaveAsync())
        {
            await Dialogs.AlertAsync("Não foi possível excluir", "A nota tem alterações que não foram salvas no vault.");
            return false;
        }
        if (_store.Get(path) is not { } doc) return false;
        // the trash as it will be: nothing changes in memory until the vault holds it
        long now = Now;
        try
        {
            await _vault!.TrashTextFileAsync(path, _trash.ExportWith(doc, now));
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível excluir", ex.Message);
            return false;
        }
        _trash.Trash(_store, doc.Id, now, "explorer.delete");
        _contents.Remove(path);
        if (editing) Editor.IsVisible = false;
        NotesChanged();
        if (_batch > 0)
        {
            // many at once (UC-65): the history is written and the tree redrawn once, at the end of the batch
            CaptureRemoved(doc);
            if (++_batchDone % 10 == 0 && _batchTotal > 10) ShowLifeNotice($"Movendo para a lixeira… {_batchDone} de {_batchTotal}");
            return true;
        }
        await CaptureRemovedAsync(doc);   // document:removed → history.captureRemoved
        RefreshNotes();
        return true;
    }

    private int _batch, _batchDone, _batchTotal;

    /// <summary>
    /// UC-65: many notes to the trash in one go (a folder): the trash, holding all of them, is written and verified
    /// first, then their files are removed and the vault verified, once. Nothing changes in memory until the vault
    /// holds it; if the vault refuses half-way, it is read again so the screen shows what is on the disk.
    /// </summary>
    private async Task<bool> TrashNotesAsync(IReadOnlyList<UrbeDocument> docs)
    {
        if (_store is null || _vault is null) return false;
        if (!TrashWritable(out var problem)) { await Dialogs.AlertAsync("Excluir", problem!); return false; }
        if (docs.Count == 1) return await DeleteNoteAsync(docs[0].Path, confirmed: true);
        // the note being written is saved first, so the trash keeps what is on the screen
        var editing = Editor.IsVisible && Editor.Current?.Path is { } open && docs.Any(d => d.Path == open);
        if (editing && Editor.HasChanges && !await Editor.SaveAsync())
        {
            await Dialogs.AlertAsync("Não foi possível excluir", "A nota aberta tem alterações que não foram salvas no vault.");
            return false;
        }
        var current = docs.Select(d => _store.Get(d.Id)).OfType<UrbeDocument>().ToList();
        long now = Now;
        ShowLifeNotice($"Movendo {current.Count} notas para a lixeira…");
        try
        {
            await _vault.TrashTextFilesAsync([.. current.Select(d => d.Path)], _trash.ExportWith(current, now));
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível excluir", ex.Message);
            if (await _vault.ReloadAsync() is { } snapshot) await ShowVaultAsync(snapshot);
            return false;
        }
        foreach (var doc in current)
        {
            _trash.Trash(_store, doc.Id, now, "explorer.delete");
            _contents.Remove(doc.Path);
            CaptureRemoved(doc);
        }
        if (editing) Editor.IsVisible = false;
        NotesChanged();
        if (_batch == 0)
        {
            await SaveHistoryAsync();
            RefreshNotes();
        }
        return true;
    }

    /// <summary>
    /// UC-65: many notes to the trash at once (a folder, a selection). Each note is still trashed and verified on its
    /// own, first in the trash and then off the disk; only the history's write and the tree's redraw wait for the end.
    /// </summary>
    private async Task<T> InBatchAsync<T>(int total, Func<Task<T>> work)
    {
        if (_batch++ == 0) { _batchDone = 0; _batchTotal = total; }
        try { return await work(); }
        finally
        {
            if (--_batch == 0)
            {
                await SaveHistoryAsync();
                RefreshNotes();
            }
        }
    }

    /// <summary>The vault's files that are not notes (images, PDFs…), by path, for the Explorer.</summary>
    private readonly HashSet<string> _attachments = new(StringComparer.Ordinal);

    /// <summary>Notas shows the notes, every folder (empty ones too, like model.folders) and the other files.</summary>
    private void RefreshNotes()
    {
        if (RefreshLastNames()) return;
        Explorer.SetNotes(_contents.Select(n => new CityNote(n.Key, n.Value) { Title = _store?.Get(n.Key)?.Title }), _library?.Folders);
        var files = new HashSet<string>(_attachments, StringComparer.Ordinal);
        files.ExceptWith(_contents.Keys);
        Explorer.SetFiles(files);
        RefreshWorkspace();
        RememberNames();
    }

    /// <summary>A file of the Explorer that is not a note (DEC-0045, UC-38): its preview (abrirEntradaTree → openFilePreview).</summary>
    private Task ShowFileAsync(string path) => Dialogs.IsOpen ? Task.CompletedTask : FilePreview.OpenAsync(path);

    /// <summary>
    /// The "⋯" of a folder in the Explorer (the 1.8.4 showed no options when a folder was selected, DEC-0045, UC-38):
    /// Nova nota aqui, Nova subpasta, Renomear, Mover para (UC-54) and Mover para a lixeira.
    /// </summary>
    private Task ShowFolderMenuAsync(string folder, bool inExplorer = false)
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        return Dialogs.MenuAsync(LastName(folder),
        [
            new("Nova nota aqui", UrbeTheme.Icons.File, NotesWhere(folder), Run: () => CreateNoteInNotesAsync(folder: folder)),
            new("Nova subpasta", UrbeTheme.Icons.Folder, "Dentro desta pasta.", Run: () => CreateFolderAsync(folder)),
            new("Renomear", UrbeTheme.Icons.Pencil, "Muda o nome da pasta no vault.",
                Run: () => inExplorer && Explorer.BeginRename(NotesExplorer.FolderPrefix + folder) ? Task.CompletedTask : RenameFolderAsync(folder)),
            new("Mover para", UrbeTheme.Icons.Move, "A pasta e tudo o que há nela.", Run: () => MoveFolderAsync(folder)),
            new("Criar página daqui", UrbeTheme.Icons.Layers, "Uma página com as notas desta pasta, que acompanha as mudanças.", Run: () => CreatePageFromAsync(folder: folder)),
            new("Mover para a lixeira", UrbeTheme.Icons.Trash, "A pasta e as notas dela.", Run: () => SelectionActionAsync("delete", [NotesExplorer.FolderPrefix + folder])),
            .. AreaMenuItems(folder)
        ]);
    }

    /// <summary>mobile-ui.js newMenu: "Criar" with Nova nota (in the selected folder), A partir de um modelo, Nova pasta
    /// and Nota de voz.</summary>
    public Task ShowNotesCreateMenuAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var folder = Explorer.CurrentFolder;
        var items = new List<DialogItem>
        {
            new("Nova nota", UrbeTheme.Icons.File, NotesWhere(folder), Run: () => CreateNoteInNotesAsync()),
            new("Nova pasta", UrbeTheme.Icons.Folder, NotesWhere(folder), Run: () => CreateFolderAsync(folder))
        };
        // UC-44: notes in Modelos/ are templates; with any, a note can start from one
        if (NoteTemplateEngine.Templates(_contents.Keys).Count > 0)
            items.Insert(1, new("A partir de um modelo", UrbeTheme.Icons.File, "Uma nota de “" + NoteTemplateEngine.Folder + "”. " + NotesWhere(folder),
                Run: () => CreateNoteFromTemplateAsync()));
        if (QuickVoiceItem() is { } voice) items.Add(voice);
        return Dialogs.MenuAsync("Criar", items);
    }

    /// <summary>
    /// UC-44 (DEC-0045): "A partir de um modelo" — pick a note of Modelos/, name the new note, and it is created (the
    /// same verified path as Nova nota) with the template's content, {{titulo}} and {{data}} filled in and each other
    /// {{campo}} asked for after the name.
    /// </summary>
    public async Task CreateNoteFromTemplateAsync(string? templatePath = null, string? typedName = null, string? folder = null)
    {
        var templates = NoteTemplateEngine.Templates(_contents.Keys);
        if (templates.Count == 0) return;
        if (templatePath is null)
        {
            var pick = await Dialogs.ChooseAsync("A partir de um modelo",
                templates.Select(p => new DialogItem(NoteTemplateEngine.NameOf(p), UrbeTheme.Icons.File)).ToList(),
                "Notas da pasta “" + NoteTemplateEngine.Folder + "”.");
            if (pick is not { } i) return;
            templatePath = templates[i];
        }
        if (!_contents.TryGetValue(templatePath, out var template)) return;
        await CreateNoteInNotesAsync(typedName, folder, async name =>
        {
            // the template's other {{campo}}: one question each, in order; Cancelar creates nothing
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var fields = NoteTemplateEngine.InputFields(template);
            for (int i = 0; i < fields.Count; i++)
            {
                var value = await Dialogs.PromptAsync(new PromptOptions(NoteTemplateEngine.NameOf(templatePath), Label: fields[i],
                    Hint: fields.Count > 1 ? "Campo " + (i + 1) + " de " + fields.Count + " do modelo." : "Campo do modelo.",
                    Confirm: i == fields.Count - 1 ? "Criar nota" : "Continuar", Required: false));
                if (value is null) return null;
                values[fields[i]] = value;
            }
            return NoteTemplateEngine.Fill(template, ArtifactModel.IsText(name) ? System.IO.Path.GetFileNameWithoutExtension(name) : name, DateTime.Now, values);
        });
    }

    /// <summary>mobile-ui.js where(folder).</summary>
    private static string NotesWhere(string folder) => folder.Length > 0 ? "Em “" + folder + "”." : "Na raiz das notas.";

    /// <summary>mobile-ui.js cleanName.</summary>
    private static string CleanNoteName(string? s)
    {
        var t = System.Text.RegularExpressions.Regex.Replace((s ?? "").Trim(), "[\\\\/:*?\"<>|\u0000-\u001f]", "-");
        t = System.Text.RegularExpressions.Regex.Replace(t, "^\\.+|\\.+$", "");
        return t.Length > 90 ? t[..90] : t;
    }

    /// <summary>
    /// mobile-ui.js createNote: "Nova nota" in the selected folder (or the root), "Ex.: Ideias", a name without an
    /// extension becomes .md, a name already taken there is refused; the empty file is written to the vault first, then
    /// its document is created (explorer.create) and it opens.
    /// </summary>
    public async Task CreateNoteInNotesAsync(string? typedName = null, string? folder = null, Func<string, Task<string?>>? contentFor = null)
    {
        if (_library is not { } library || _store is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Nova nota", "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para criar notas.");
            return;
        }
        folder ??= Explorer.CurrentFolder;
        string PathOf(string name)
        {
            if (!ArtifactModel.IsText(name)) name += ".md";
            return (folder.Length > 0 ? folder + "/" : "") + name;
        }
        var raw = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Nova nota", Label: "Nome", Placeholder: "Ex.: Ideias",
            Hint: NotesWhere(folder) + " Sem extensão, será salva como .md.", Confirm: "Criar nota",
            Validate: v => CleanNoteName(v) is not { Length: > 0 } n ? "Use um nome válido." : _store.Get(PathOf(n)) is not null ? "Já existe uma nota com esse nome aqui." : null));
        var name = CleanNoteName(raw);
        if (name.Length == 0) return;
        var path = PathOf(name);
        if (_store.Get(path) is not null || _contents.ContainsKey(path)) return;
        // UC-44: a note made from a template starts with its filled-in content (null: the person gave up)
        var content = contentFor is null ? "" : await contentFor(name);
        if (content is null) return;
        try { await _vault!.CreateTextFileAsync(path, content); }
        catch (Exception ex) { await Dialogs.AlertAsync("Não foi possível criar a nota", ex.Message); return; }
        library.AddNote(path, content, Now);
        _contents[path] = content;
        NotesChanged();
        RefreshNotes();
        // document.open: touchRecent is not called by createNote; the editor opens
        CloseNotesOverEditor();
        Editor.Open(new CityNote(path, content));
    }

    /// <summary>
    /// mobile-ui.js deleteSelected: folders inside another selected folder and notes inside a selected folder are
    /// dropped; one confirmation ("Excluir a pasta “X”?" / "Excluir 1 pasta e 2 notas?" with how many notes go to the
    /// Lixeira, or "Mover para a lixeira?" for notes only); then explorer.deleteFolder for each folder and
    /// explorer.delete for the notes.
    /// </summary>
    private async Task DeleteSelectionAsync(IReadOnlyList<string> selection)
    {
        var folders = selection.Where(x => x.StartsWith(NotesExplorer.FolderPrefix, StringComparison.Ordinal))
            .Select(x => x[NotesExplorer.FolderPrefix.Length..]).Where(f => f.Length > 0).ToList();
        folders = folders.Where(f => !folders.Any(g => g != f && f.StartsWith(g + "/", StringComparison.Ordinal))).ToList();
        bool Within(string path) => folders.Any(f => path.StartsWith(f + "/", StringComparison.Ordinal));
        var notes = selection.Where(x => !x.StartsWith(NotesExplorer.FolderPrefix, StringComparison.Ordinal) && !Within(x)).ToList();
        if (notes.Count == 0 && folders.Count == 0) return;
        static string Plural(int n, string one, string many) => n.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);
        string title, message;
        if (folders.Count > 0)
        {
            if (!TrashWritable(out var problem)) { await Dialogs.AlertAsync("Excluir", problem!); return; }
            int total = notes.Count + folders.Sum(f => NoteLibrary.FolderDocuments(_store!, f).Count);
            title = folders.Count == 1 && notes.Count == 0
                ? "Excluir a pasta “" + LastName(folders[0]) + "”?"
                : "Excluir " + string.Join(" e ", new[] { Plural(folders.Count, "pasta", "pastas"), notes.Count > 0 ? Plural(notes.Count, "nota", "notas") : "" }.Where(x => x.Length > 0)) + "?";
            message = total > 0
                ? Plural(total, "nota vai", "notas vão") + " para a Lixeira e " + (total == 1 ? "pode" : "podem") + " ser restaurada" + (total == 1 ? "" : "s") + ". Outros arquivos da pasta ficam no vault."
                : "A pasta está vazia.";
        }
        else
        {
            title = "Mover para a lixeira?";
            message = notes.Count == 1 ? "A nota poderá ser restaurada pela Lixeira." : notes.Count + " notas poderão ser restauradas pela Lixeira.";
        }
        if (!await Dialogs.ConfirmAsync(title, message, folders.Count > 0 ? "Excluir" : "Mover para a lixeira", danger: true)) return;
        await InBatchAsync(notes.Count + folders.Sum(f => NoteLibrary.FolderDocuments(_store!, f).Count), async () =>
        {
            foreach (var f in folders)
                if (!await DeleteFolderAsync(f)) break;
            var picked = notes.Select(p => _store?.Get(p)).OfType<UrbeDocument>().ToList();
            if (picked.Count > 0) await TrashNotesAsync(picked);
            return true;
        });
        Explorer.ClearSelection();
    }

    /// <summary>
    /// mobile-ui.js moreMenu ("Notas") with the items the native app has, in its order: Buscar notas e comandos,
    /// Recentes, Favoritos, Importar arquivos (into the open folder), Exportar tudo (.zip), Lixeira (n item(ns)) and
    /// Pasta do Urbe no dispositivo.
    /// </summary>
    public Task ShowNotesMenuAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        int n = _trash.Items.Count;
        return Dialogs.MenuAsync("Explorer",
        [
            new DialogItem("Buscar notas e comandos", UrbeTheme.Icons.SearchUi, "No texto das notas, e tudo o que o Urbe faz", Run: () => { Search.OpenQuickOpen(); return Task.CompletedTask; }),
            new DialogItem("Recentes", UrbeTheme.Icons.Clock, Run: () => { ShowCollection("recent"); return Task.CompletedTask; }),
            new DialogItem("Favoritos", UrbeTheme.Icons.Star, Run: () => { ShowCollection("favorites"); return Task.CompletedTask; }),
            new DialogItem("Constelação", UrbeTheme.Icons.Star, "As notas como um céu à noite", Run: () => ShowConstellationAsync()),
            new DialogItem("Importar arquivos", UrbeTheme.Icons.Upload, "Notas, pastas ou .zip do aparelho", Run: () => ImportAsync(Explorer.CurrentFolder)),
            new DialogItem("Exportar tudo (.zip)", UrbeTheme.Icons.Download, "Cópia de segurança de todas as notas", Run: () => ExportAsync()),
            new DialogItem("Lixeira", UrbeTheme.Icons.Trash, Detail: (n > 0 ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Nenhum") + " item(ns)",
                Run: () => { ShowTrashView(); return Task.CompletedTask; }),
            .. (_vault is null ? Array.Empty<DialogItem>() :
                [new DialogItem("Pasta do Urbe no dispositivo", UrbeTheme.Icons.Storage, "Escolher onde os arquivos ficam salvos", Run: () => OpenVaultAsync())])
        ]);
    }

    /// <summary>Where the .zip goes (a save dialog); the tests replace it. Null = cancelled.</summary>
    public Func<byte[], Task<bool?>>? SaveExport { get; set; }

    /// <summary>
    /// app.js exportarVault: the pending note and map are written first, then every physical file of the vault except
    /// the journals goes into "Urbe-vault.zip" with the urbe-export.json manifest (VaultArchive.Export, REQ-044).
    /// "Vault completo exportado." or "Falhou ao exportar: …". Only reads the vault.
    /// </summary>
    public async Task ExportAsync()
    {
        if (_vault is not { IsConnected: true } vault || _snapshot is null)
        {
            ShowLifeNotice("Abra o Urbe antes de exportar.");
            return;
        }
        try
        {
            if (Editor.HasChanges && Editor.SaveOnClose && !await Editor.SaveAsync()) throw new IOException("a nota aberta não foi salva");
            await FlushPendingAsync();
            var files = await vault.ReadAllFilesAsync();
            var zip = VaultArchive.Export(files, new VaultArchiveExportOptions
            {
                AppVersion = "urbe-native-preview",
                VaultName = vault.VaultName,
                VaultFormatVersion = _snapshot.VaultFormatVersion,
                // collectState: this device's preferences that travel with the export (tips, editor session, plugin approvals; never AI keys)
                State = VaultExportStatePolicy.Collect(Customization.Device.All(), "Urbe")
            });
            var saved = await (SaveExport ?? SaveZipAsync)(zip.Bytes.ToArray());
            if (saved == true) ShowLifeNotice("Vault completo exportado.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowLifeNotice("Falhou ao exportar: " + ex.Message);
        }
    }

    private async Task<bool?> SaveZipAsync(byte[] bytes)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
            throw new NotSupportedException("este aparelho não permite salvar arquivos");
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Exportar tudo (.zip)",
            SuggestedFileName = "Urbe-vault.zip",
            DefaultExtension = "zip",
            FileTypeChoices = [new FilePickerFileType("ZIP") { Patterns = ["*.zip"], MimeTypes = ["application/zip"] }]
        });
        if (file is null) return null;
        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(bytes);
        return true;
    }

    /// <summary>show('recent') / show('favorites'): model.listRecent / listFavorites (notes that still exist).</summary>
    public void ShowCollection(string view)
    {
        var ids = view == "recent" ? ExplorerState.Recent : ExplorerState.Favorites;
        var notes = ids.Select(id => _store?.Get(id)).Where(d => d is not null && _contents.ContainsKey(d.Path))
            .Select(d => new CityNote(d!.Path, _contents[d.Path])).ToList();
        Explorer.ShowCollection(view == "recent"
            ? new NotesExplorer.Collection("Recentes", notes, "Nada recente", "As notas que você abrir aparecem aqui.")
            : new NotesExplorer.Collection("Favoritos", notes, "Nenhum favorito", "Selecione notas (toque e segure) e escolha Favoritar."));
    }

    /// <summary>
    /// The selection bar of Notas (mobile-ui.js): Abrir (the first note, touchRecent), Favoritar, Mover para pasta
    /// (explorer.move), Duplicar (explorer.duplicate), Agrupar em nova pasta and Mover para a lixeira (explorer.delete,
    /// one confirmation for all). Files are written (and verified) before the documents change; the selection is cleared
    /// after the action (model.clear()).
    /// </summary>
    public async Task SelectionActionAsync(string action, IReadOnlyList<string> selection)
    {
        if (selection.Count == 0) return;
        if (action == "delete")
        {
            await DeleteSelectionAsync(selection);
            return;
        }
        // the actions that only apply to notes (renderBar disables them when only folders are selected)
        var paths = selection.Where(x => !x.StartsWith(NotesExplorer.FolderPrefix, StringComparison.Ordinal)).ToList();
        if (paths.Count == 0) return;
        switch (action)
        {
            case "open":
                Explorer.ClearSelection();
                if (_contents.TryGetValue(paths[0], out var text))
                {
                    if (_store?.Get(paths[0]) is { } d) ExplorerState.TouchRecent(d.Id);
                    CloseNotesOverEditor();
                    Editor.Open(new CityNote(paths[0], text));
                }
                return;
            case "favorite":
                foreach (var p in paths) if (_store?.Get(p) is { } d) ExplorerState.Favorite(d.Id);
                Explorer.ClearSelection();
                return;
            case "move":
                await MoveToFolderAsync(paths);
                return;
            case "duplicate":
                await DuplicateNotesAsync(paths);
                return;
            case "group":
                await GroupIntoFolderAsync(paths);
                return;
        }
    }

    /// <summary>folders(): every folder of the Notas tree (recorded ones and the folders of files), depth first, each
    /// level sorted by name (localeCompare).</summary>
    private List<string> NoteFolders()
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        void Add(string folder) { for (var f = folder; f.Length > 0; f = DirOf(f)) all.Add(f); }
        foreach (var f in _library?.Folders ?? []) Add(f);
        foreach (var path in _contents.Keys) Add(DirOf(path));
        var output = new List<string>();
        void Walk(string parent)
        {
            var kids = all.Where(f => DirOf(f) == parent).OrderBy(LastName, StringComparer.CurrentCulture);
            foreach (var k in kids) { output.Add(k); Walk(k); }
        }
        Walk("");
        return output;
    }

    /// <summary>moveSelected: D.choose "Mover para" (Raiz + every folder), then explorer.move for each note.</summary>
    private async Task MoveToFolderAsync(IReadOnlyList<string> paths)
    {
        if (_store is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Mover", NoVaultMessage);
            return;
        }
        if (await ChooseFolderAsync() is not { } folder) return;
        await MoveNotesToAsync(paths, folder);
    }

    /// <summary>uiDestinoRegiao("Mover para"): Raiz or one of the folders; "" is the root, null when cancelled. A folder
    /// being moved leaves itself and the folders below it out (UC-54).</summary>
    private async Task<string?> ChooseFolderAsync(string? except = null)
    {
        var folders = NoteFolders();
        if (except is { Length: > 0 }) folders.RemoveAll(f => f == except || f.StartsWith(except + "/", StringComparison.Ordinal));
        var options = new List<DialogItem> { new("Raiz", UrbeTheme.Icons.Notes, "Fora de qualquer pasta") };
        options.AddRange(folders.Select(f => new DialogItem(LastName(f), UrbeTheme.Icons.Folder, f)));
        if (await Dialogs.ChooseAsync("Mover para", options) is not { } chosen) return null;
        return chosen == 0 ? "" : folders[chosen - 1];
    }

    /// <summary>explorer.move of each note into <paramref name="folder"/> ("" = the root): the file first, then the
    /// document (modified today). Also what dragging notes onto a folder in the Explorer does (DEC-0045, UC-38).</summary>
    public async Task MoveNotesToAsync(IReadOnlyList<string> paths, string folder)
    {
        if (_library is not { } library || _store is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Mover", NoVaultMessage);
            return;
        }
        long now = Now;
        foreach (var from in paths)
        {
            if (_store.Get(from) is not { } doc) continue;
            var to = NoteLibrary.FolderMovePath(_store, doc, folder);
            if (to == from) continue;
            if (Editor.IsVisible && Editor.Current?.Path == from && Editor.HasChanges && !await Editor.SaveAsync()) continue;
            try { await _vault!.MoveTextFileAsync(from, to); }
            catch (Exception ex) { await Dialogs.AlertAsync("Não foi possível mover", ex.Message); break; }
            if (_contents.Remove(from, out var content)) _contents[to] = content;
            var moved = NoteLibrary.MoveDocument(_store, doc, to);
            library.Edited(moved.Path, moved.Content, now);
            library.AddFolder(folder);
            if (Editor.IsVisible && Editor.Current?.Path == from) Editor.Renamed(to);
        }
        NotesChanged();
        RefreshNotes();
    }

    /// <summary>duplicateSelected: explorer.duplicate for each note — "X cópia.ext" in the same folder with the same
    /// text. The new file is written first.</summary>
    private async Task DuplicateNotesAsync(IReadOnlyList<string> paths)
    {
        if (_store is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Duplicar", NoVaultMessage);
            return;
        }
        long now = Now;
        foreach (var from in paths)
        {
            if (_store.Get(from) is not { } doc) continue;
            int dot = doc.Path.LastIndexOf('.');
            var target = NoteLibrary.UniqueDocumentPath(_store, (dot >= 0 ? doc.Path[..dot] : doc.Path) + " cópia" + (dot >= 0 ? doc.Path[dot..] : ""));
            var content = _contents.GetValueOrDefault(from, doc.Content);
            try { await _vault!.CreateTextFileAsync(target, content); }
            catch (Exception ex) { await Dialogs.AlertAsync("Não foi possível duplicar", ex.Message); break; }
            var copy = NoteLibrary.DuplicateDocument(_store, doc with { Content = content }, now);
            _contents[copy.Path] = content;
        }
        NotesChanged();
        RefreshNotes();
    }

    /// <summary>show('trash'): the Lixeira in Notas.</summary>
    public void ShowTrashView() => Explorer.ShowTrash(_trash.Items.Select(i =>
        new NotesExplorer.TrashEntry((string?)i["document"]?["id"] ?? "", (string?)i["document"]?["title"] ?? "", (string?)i["originalPath"] ?? "")).ToList());

    private bool TrashWritable(out string? problem)
    {
        problem = !VaultWritable || _store is null
            ? "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para usar a lixeira."
            : _trashReadOnly ? "A lixeira deste vault é de outra versão do Urbe e foi preservada." : null;
        return problem is null;
    }

    /// <summary>
    /// Restaurar (trash.restore): the note goes back to its path with its id and dates. The file is created in the vault
    /// first, then the trash is rewritten without it; a document already at that path is refused.
    /// </summary>
    public async Task RestoreFromTrashAsync(string id)
    {
        if (!TrashWritable(out var problem)) { await Dialogs.AlertAsync("Lixeira", problem!); return; }
        var item = _trash.Items.FirstOrDefault(i => (string?)i["document"]?["id"] == id);
        if (item is null || _library is not { } library) return;
        var path = (string?)item["originalPath"] ?? (string?)item["document"]?["path"] ?? "";
        var content = (string?)item["document"]?["content"] ?? "";
        try
        {
            if (_store!.Get(path) is not null || _contents.ContainsKey(path)) throw new InvalidOperationException("Já existe um documento em " + path);
            await _vault!.CreateTextFileAsync(path, content);
            await _vault.SaveTrashAsync(_trash.ExportAfter(t => t.Purge(id)));
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível restaurar", ex.Message);
            return;
        }
        var doc = library.Restore(_trash.Restore(_store!, id)!, Now);
        _contents[doc.Path] = doc.Content;
        NotesChanged();
        RefreshNotes();
        ShowTrashView();
    }

    /// <summary>✕ Excluir de vez (trash.purge) after "Excluir de vez?".</summary>
    public async Task PurgeFromTrashAsync(string id, bool confirmed = false)
    {
        if (!TrashWritable(out var problem)) { await Dialogs.AlertAsync("Lixeira", problem!); return; }
        var item = _trash.Items.FirstOrDefault(i => (string?)i["document"]?["id"] == id);
        if (item is null) return;
        if (!confirmed && !await Dialogs.ConfirmAsync("Excluir de vez?",
                "“" + ((string?)item["document"]?["title"] ?? "") + "” será apagada para sempre. Não dá para desfazer.", "Excluir de vez", danger: true))
            return;
        try { await _vault!.SaveTrashAsync(_trash.ExportAfter(t => t.Purge(id))); }
        catch (Exception ex) { await Dialogs.AlertAsync("Não foi possível excluir", ex.Message); return; }
        _trash.Purge(id);
        ShowTrashView();
    }

    /// <summary>Esvaziar (trash.clear) after "Esvaziar a lixeira?".</summary>
    public async Task EmptyTrashAsync(bool confirmed = false)
    {
        if (!TrashWritable(out var problem)) { await Dialogs.AlertAsync("Lixeira", problem!); return; }
        int n = _trash.Items.Count;
        if (n == 0) return;
        if (!confirmed && !await Dialogs.ConfirmAsync("Esvaziar a lixeira?",
                "Todos os " + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + " itens serão apagados para sempre. Não dá para desfazer.", "Esvaziar", danger: true))
            return;
        try { await _vault!.SaveTrashAsync(_trash.ExportAfter(t => t.Clear())); }
        catch (Exception ex) { await Dialogs.AlertAsync("Não foi possível esvaziar", ex.Message); return; }
        _trash.Clear();
        ShowTrashView();
    }

    private void SaveEditorToMemory()
    {
        if (Editor.Current is { } edited) _contents[edited.Path] = edited.Content;
    }

    private Avalonia.Controls.Platform.IInputPane? _inputPane;
    private Avalonia.Controls.Platform.IInsetsManager? _insets;
    private TopLevel? _keyboardTop;
    // the keyboard counts as open (a short close while moving from one block to the next does not give the space back)
    private bool _keyboardOpen;
    private readonly DispatcherTimer _keyboardClosed = new() { Interval = TimeSpan.FromMilliseconds(220) };

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // the phone's keyboard: the app ends where the keyboard starts, so the editor's formatting bar (bottom row on
        // a phone) and the text being typed stay above the keys (UC-48/UC-55)
        _keyboardTop = TopLevel.GetTopLevel(this);
        _inputPane = _keyboardTop?.InputPane;
        _insets = _keyboardTop?.InsetsManager;
        if (_inputPane is not null) _inputPane.StateChanged += OnInputPane;
        if (_insets is not null) _insets.SafeAreaChanged += OnSafeArea;
        if (_keyboardTop is not null) _keyboardTop.PropertyChanged += OnTopLevelResized;
        _keyboardClosed.Tick += OnKeyboardClosed;
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_inputPane is not null) _inputPane.StateChanged -= OnInputPane;
        if (_insets is not null) _insets.SafeAreaChanged -= OnSafeArea;
        if (_keyboardTop is not null) _keyboardTop.PropertyChanged -= OnTopLevelResized;
        _keyboardClosed.Tick -= OnKeyboardClosed;
        _keyboardClosed.Stop();
        _inputPane = null;
        _insets = null;
        _keyboardTop = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnInputPane(object? sender, Avalonia.Controls.Platform.InputPaneStateEventArgs e)
    {
        if (_touchDebug?.IsVisible == true)
        {
            var rect = _inputPane?.OccludedRect;
            _touchDebug.RecordExternal("IME " + e.NewState + " · janela=" + Math.Round(_keyboardTop?.ClientSize.Height ?? 0) +
                " · topo=" + Math.Round(rect?.Top ?? 0) + " · coberto=" + Math.Round(rect?.Height ?? 0));
        }
        if (e.NewState == Avalonia.Controls.Platform.InputPaneState.Open)
        {
            _keyboardClosed.Stop();
            _keyboardOpen = true;
            FitKeyboard();
        }
        // moving from one block to the next closes and reopens the keyboard: wait a moment before giving the
        // space back, so the screen does not jump down and up again (16/10/2026)
        else if (!_keyboardClosed.IsEnabled) _keyboardClosed.Start();
    }

    private void OnKeyboardClosed(object? sender, EventArgs e)
    {
        _keyboardClosed.Stop();
        _keyboardOpen = false;
        FitKeyboard();
    }

    // the keyboard grew or shrank (suggestions, emoji), or the system bars changed
    private void OnSafeArea(object? sender, Avalonia.Controls.Platform.SafeAreaChangedArgs e) => FitKeyboard();

    private void OnTopLevelResized(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TopLevel.ClientSizeProperty) FitKeyboard();
    }

    /// <summary>
    /// The space the keyboard covers at the bottom of the window, from the window's height and the keyboard's top taken
    /// at the same moment. A window the system shrinks for the keyboard (Android up to 14: adjustResize, the app not
    /// edge-to-edge) is never covered: adding the keyboard's height there pushed the app up and left the window's black
    /// background between it and the keys (16/10 and 22/10/2026). Only an edge-to-edge window (Android 15+) is covered.
    /// </summary>
    public static double KeyboardCover(double windowHeight, double keyboardTop, bool windowShrinks = false) =>
        windowShrinks || double.IsNaN(keyboardTop) ? 0 : Math.Max(0, windowHeight - keyboardTop);

    private void FitKeyboard()
    {
        var height = _keyboardTop?.ClientSize.Height ?? 0;
        // the keyboard's place now, not the one announced when it started to open (the window may have changed since)
        var top = _keyboardOpen && _inputPane is { } pane && pane.OccludedRect.Height > 0 ? pane.OccludedRect.Top : double.NaN;
        var covered = height <= 0 ? 0 : KeyboardCover(height, top, windowShrinks: _insets is { DisplaysEdgeToEdge: false });
        if (Math.Abs(Margin.Bottom - covered) > .5)
        {
            if (_touchDebug?.IsVisible == true) _touchDebug.RecordExternal("viewport: janela=" + Math.Round(height) +
                " · topoTeclado=" + (double.IsNaN(top) ? "—" : Math.Round(top).ToString()) + " · margemInferior=" + Math.Round(covered));
            Margin = new Thickness(0, 0, 0, covered);
        }
    }

    // ---------------- navigation (rail / dock) ----------------

    // Assistente is opened from its quiet floating launcher; the dock no longer has an AI tab.
    private static readonly (string Icon, string Label)[] Destinations =
        [(UrbeTheme.Icons.Notes, "Notas")];

    private void BuildRail()
    {
        _rail.Width = 72;
        _rail.Background = UrbeTheme.Brush(UrbeTheme.Bg);
        _rail.Spacing = 10;
        _rail.Margin = new Thickness(0, 12, 0, 0);
        for (int i = 0; i < Destinations.Length; i++)
            _rail.Children.Add(NavItem(Destinations[i].Icon, Destinations[i].Label, i == 0));
    }

    private void BuildDock()
    {
        // #v23Dock: rgba(bg, .94) with a 1px line on top
        _dock.Background = UrbeTheme.Brush(UrbeTheme.Bg, .94);
        _dock.Height = 64;
        var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, Background = UrbeTheme.Brush(UrbeTheme.Line) };
        SetColumnSpan(line, Destinations.Length);
        _dock.Children.Add(line);
        _dock.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Destinations.Select(_ => "*")));
        for (int i = 0; i < Destinations.Length; i++)
        {
            var item = NavItem(Destinations[i].Icon, Destinations[i].Label, i == 0);
            SetColumn(item, i);
            _dock.Children.Add(item);
        }
    }

    private readonly List<(string Label, string Icon, Border Ico, TextBlock Text)> _nav = [];

    /// <summary>The destination shown as active (.ativo) in the rail and the dock: Assistente while it is open, else Notas.</summary>
    public string ActiveDestination => AssistantOpen ? "Assistente" : "Notas";

    private void UpdateNav()
    {
        foreach (var (label, icon, ico, text) in _nav)
        {
            bool active = label == ActiveDestination;
            ico.Background = active ? UrbeTheme.Brush(UrbeTheme.Accent, .14) : Brushes.Transparent;
            ico.Child = UrbeTheme.Icon(icon, 22, active ? UrbeTheme.Accent : UrbeTheme.Text3, 1.8);
            text.Foreground = UrbeTheme.Brush(active ? UrbeTheme.Text : UrbeTheme.Text3);
        }
    }

    private Control NavItem(string icon, string label, bool active)
    {
        var ico = new Border
        {
            Width = 56,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = active ? UrbeTheme.Brush(UrbeTheme.Accent, .14) : Brushes.Transparent,
            Child = UrbeTheme.Icon(icon, 22, active ? UrbeTheme.Accent : UrbeTheme.Text3, 1.8), // #v23Dock .ui-icon: 22px, stroke 1.8
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var text = new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = UrbeTheme.Brush(active ? UrbeTheme.Text : UrbeTheme.Text3),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var item = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { ico, text } };
        _nav.Add((label, icon, ico, text));
        var button = new Button
        {
            Content = item,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            // the whole part of the dock (or the rail's width) is the button: centred, and the touch area is all of it
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        if (label == "Notas")
        {
            ToolTip.SetTip(button, "Notas e pastas do vault");
            button.Click += (_, _) => { if (AssistantOpen) ToggleAssistant(); ShowWorkspace(); };
        }
        else
        {
            ToolTip.SetTip(button, "Assistente: agentes que pesquisam, escrevem e organizam o vault");
            button.Click += (_, _) => ToggleAssistant();
        }
        if (Avalonia.Automation.AutomationProperties.GetName(button) is not { Length: > 0 }) Avalonia.Automation.AutomationProperties.SetName(button, label);
        return Ui.UrbeMotion.Pressable(button);
    }
}
