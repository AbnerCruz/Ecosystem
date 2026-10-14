using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The 1.8.4 screen: the city fills everything; navigation rail (wide) or dock (narrow),
/// the top pill with the biome, the round buttons, the "+" and the panels sit on top of it.
/// </summary>
public sealed partial class MainView : Grid
{
    private const double WideBreakpoint = 760;

    private readonly Panel _stage = new();
    private readonly StackPanel _rail = new();
    private readonly Grid _dock = new();
    private readonly TextBlock _biome = new();
    private readonly Border _housePanel = new();
    private readonly TextBlock _houseTitle = new();
    private readonly TextBlock _houseMeta = new();
    private readonly Image _houseImage = new() { Width = 72, Height = 84, Stretch = Stretch.Uniform };
    private readonly TextBlock _loading = new();
    private readonly Dictionary<string, string> _contents = new(StringComparer.Ordinal);
    private readonly IUrbeVaultStorage? _vault;
    private readonly TextBlock _vaultName = new();
    private VaultSnapshot? _snapshot;
    private bool _editorOpenedFromNotes;
    private DocumentStore? _store;
    private LegacyCityMap? _mapWriter;
    private MapSaveScheduler? _mapSave;
    private LegacyTrash _trash = new();

    /// <summary>Favourites and recents of Notas, kept on the device (model.js localStorage).</summary>
    public ExplorerState ExplorerState { get; } = new(UrbeApp.LocalStatePath);
    private bool _trashReadOnly;

    private string _today = DateTime.Now.ToString("yyyy-MM-dd");

    public MainView(IUrbeVaultStorage? storage = null)
    {
        _vault = storage ?? UrbeApp.VaultStorageFactory?.Invoke();
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        RowDefinitions = new RowDefinitions("*,Auto");

        World = new WorldView();
        CityMap = new CityMapOverlay { IsVisible = false };
        CityMap.Navigated += (tileX, tileY) =>
        {
            World.Camera.X = tileX * LegacyWorld.Tile;
            World.Camera.Y = tileY * LegacyWorld.Tile;
            World.InvalidateVisual();
            _biome.Text = World.BiomeNameAtCentre();
            CityMap.IsVisible = false;
        };
        Explorer = new NotesExplorer { IsVisible = false, State = ExplorerState };
        Editor = new EditorOverlay { IsVisible = false, Dialogs = Dialogs, Notes = () => _contents.Select(n => new CityNote(n.Key, n.Value)) };
        Editor.PropertiesFor = path => World.City?.Buildings.Find(b => b.IsNote && b.Path == path) is { } house
            ? World.City.PropertiesText(house, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) : null;
        // v23Atualizar: v23Dock.hidden = editor (the rail of a wide window stays)
        Editor.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) _dock.IsVisible = !Editor.IsVisible && !(Bounds.Width >= WideBreakpoint); };
        Explorer.Closed += () => Explorer.IsVisible = false;
        Explorer.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty && !Explorer.IsVisible) Explorer.ZIndex = 0; };
        // a backlink opens its note; the panel then shows that note's context (document:updated → renderContext)
        Editor.Context.BacklinkChosen += async path =>
        {
            // a followed backlink joins the editor's trail, so Voltar comes back here (DEC-0045, UC-37)
            await Editor.NavigateAsync(new CityNote(path, _contents.GetValueOrDefault(path, "")));
            ShowEditorContext();
        };
        // [[link]] to a note that does not exist yet: created next to the open note, in its neighbourhood (DEC-0045, UC-37)
        Editor.CreateLinked = name =>
        {
            var city = World.City;
            var house = Editor.Current is { } open ? city?.Buildings.Find(b => b.IsNote && b.Path == open.Path) : null;
            var region = house?.RegionId is { } id ? city?.Regions.Find(r => r.Id == id) : null;
            return CreateNoteAsync(region, name, open: false);
        };
        Explorer.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) UpdateNav(); };
        Explorer.SelectionAction += (action, paths) => _ = SelectionActionAsync(action, paths);
        Explorer.MoreRequested += () => _ = ShowNotesMenuAsync();
        Explorer.NewRequested += () => _ = ShowNotesCreateMenuAsync();
        Explorer.CreateFirstNoteRequested += () => _ = CreateNoteInNotesAsync();
        Explorer.RestoreRequested += id => _ = RestoreFromTrashAsync(id);
        Explorer.FileRequested += path => _ = ShowFileAsync(path);
        Explorer.FileMenuRequested += path => _ = ShowAttachmentActionsAsync(path, withView: true);
        Explorer.FolderMenuRequested += folder => _ = ShowFolderMenuAsync(folder);
        Explorer.MoveRequested += async (paths, folder) => { await MoveNotesToAsync(paths, folder); Explorer.ClearSelection(); };
        Explorer.PurgeRequested += id => _ = PurgeFromTrashAsync(id);
        Explorer.EmptyTrashRequested += () => _ = EmptyTrashAsync();
        Explorer.NoteRequested += note =>
        {
            // openId: model.touchRecent(id)
            if (_store?.Get(note.Path) is { } opened) ExplorerState.TouchRecent(opened.Id);
            _editorOpenedFromNotes = true;
            Explorer.IsVisible = false;
            Editor.Open(note);
        };
        Editor.Closed += (_, _) =>
        {
            Editor.Context.IsVisible = false;
            SaveEditorToMemory();
            if (_editorOpenedFromNotes)
            {
                _editorOpenedFromNotes = false;
                RefreshNotes();
                Explorer.IsVisible = true;
            }
        };
        if (_vault is not null) Editor.SaveRequested += SaveNoteAsync;
        Editor.Editor.TextChanged += (_, _) => { EditorLinksChanged(); CaptureEditorHistory(); };
        Editor.MoreRequested += ShowEditorMenuAsync;
        Editor.Shown += path => NoteOpened?.Invoke(path);   // plugins: "nota:aberta"

        _stage.Children.Add(World);
        _stage.Children.Add(_top = TopBar());
        _stage.Children.Add(_fab = Fab());
        _stage.Children.Add(LifeNotice());
        _stage.Children.Add(ToolDock());
        _stage.Children.Add(HousePanel());
        _stage.Children.Add(LoadingLabel());
        _stage.Children.Add(CityMap);
        _stage.Children.Add(Explorer);
        _stage.Children.Add(Editor);
        FilePreview.Storage = _vault;
        FilePreview.ActionsRequested += path => _ = ShowAttachmentActionsAsync(path);
        FilePreview.ShowInCity += building =>
        {
            if (!Explorer.Wide) Explorer.IsVisible = false;
            World.CenterOn(building);
        };
        _stage.Children.Add(FilePreview);
        _stage.Children.Add(_pluginPanels);   // the plugins' panels, over the city and the editor (ADD-0021)
        Customize = new CustomizePanel(Customization, Dialogs);
        Customize.OpenFileRequested += path => Editor.Open(new CityNote(path, _contents.GetValueOrDefault(path, "")));
        Customize.NativePlugins = NativePlugins;
        Customize.SetNativePlugin = SetNativePlugin;
        Customization.Applied += ApplyCustomization;
        Customization.Applied += SyncPlugins;   // the safe mode turns every plugin off
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
        foreach (var overlay in new Control[] { Customize, Formula, Dialogs })
        {
            SetRowSpan(overlay, 2);
            SetColumnSpan(overlay, 2);
            Children.Add(overlay);
        }
        Search = new SearchFeature(this, () => _contents.Select(n => new CityNote(n.Key, n.Value)).ToList());

        // on a wide window Notas is a panel beside the city (theme.css @media (min-width:900px))
        World.CityVisible = () => !Editor.IsVisible && (!Explorer.IsVisible || Explorer.Wide);
        World.Notice += (_, text) => ShowLifeNotice(text);
        World.CameraChanged += (_, _) => _biome.Text = World.BiomeNameAtCentre();
        // app.js v21CanvasUp: a note opens its summary, a file building its preview (openFilePreview)
        World.SelectionChanged += (_, house) =>
        {
            if (house is { IsNote: false } file)
            {
                ShowHouse(null);
                _ = FilePreview.OpenAsync(file);
                return;
            }
            ShowHouse(house);
        };
        World.DecorationTapped += decoration => _ = ShowDecorationAsync(decoration);
        // v21MoveHold: holding a house carries it (only with a vault that can be written)
        World.MoveValid = (b, x, y) => World.City?.ValidMove(b, x, y).Ok ?? false;
        World.BuildingMoveRequested += (b, x, y) => _ = MoveHouseAsync(b, x, y);
        // v20CanvasHold: holding a neighbourhood (not a house) opens its edit dialog
        World.RegionHeld += region => { if (!Dialogs.IsOpen) _ = EditRegionAsync(region); };
        World.RegionTapped += region => { if (!Dialogs.IsOpen && World.Tool is null) BeginRegionShape(region); };
    }

    public WorldView World { get; }

    /// <summary>quick-open.js / command-palette.js / keymap.js: Buscar notas, Comandos and their shortcuts.</summary>
    public SearchFeature Search { get; }

    /// <summary>document.open for a note of the vault: the editor over the city.</summary>
    public void OpenNoteByPath(string path)
    {
        CityMap.IsVisible = false;
        Explorer.IsVisible = false;
        Editor.Open(new CityNote(path, _contents.GetValueOrDefault(path, "")));
    }

    // ---- life.js avisar(): a discreet pill under the top bar, 4.5 s, fading in .6 s
    private readonly Border _notice = new();
    private readonly TextBlock _noticeText = new();
    private DispatcherTimer? _noticeTimer;

    /// <summary>The text of the event notice currently shown, or null.</summary>
    public string? LifeNoticeText => _noticeShown ? _noticeText.Text : null;
    private bool _noticeShown;

    private Control LifeNotice()
    {
        _noticeText.Foreground = UrbeTheme.Brush("#eef2fa");
        _noticeText.FontFamily = UrbeTheme.UiFont;
        _noticeText.FontWeight = FontWeight.SemiBold;
        _noticeText.FontSize = 13;
        _noticeText.TextTrimming = TextTrimming.CharacterEllipsis;
        _notice.Child = _noticeText;
        _notice.Background = new SolidColorBrush(Color.FromArgb(189, 14, 18, 26));
        _notice.CornerRadius = new CornerRadius(999);
        _notice.Padding = new Thickness(14, 7);
        _notice.HorizontalAlignment = HorizontalAlignment.Center;
        _notice.VerticalAlignment = VerticalAlignment.Top;
        _notice.Margin = new Thickness(0, 62, 0, 0);
        _notice.IsHitTestVisible = false;
        _notice.Opacity = 0;
        _notice.BoxShadow = BoxShadows.Parse("0 4 18 0 #40000000");
        _notice.Transitions =
        [
            new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromSeconds(.6) },
            new Avalonia.Animation.ThicknessTransition { Property = MarginProperty, Duration = TimeSpan.FromSeconds(.6) }
        ];
        return _notice;
    }

    private void ShowLifeNotice(string text)
    {
        _noticeText.Text = text;
        _notice.MaxWidth = Math.Max(120, Bounds.Width * .8);
        _notice.Opacity = 1;
        _noticeShown = true;
        _notice.Margin = new Thickness(0, 70, 0, 0);
        _noticeTimer?.Stop();
        _noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer?.Stop();
            _notice.Opacity = 0;
            _noticeShown = false;
            _notice.Margin = new Thickness(0, 62, 0, 0);
        };
        _noticeTimer.Start();
    }
    public CityMapOverlay CityMap { get; }

    /// <summary>The 1.8.4 full city map, not a second world or grid of notes.</summary>
    public void ShowMap()
    {
        if (Editor.IsVisible || World.World is null || World.City is null) return;
        Explorer.IsVisible = false;
        _housePanel.IsVisible = false;
        CityMap.Open(World.World, World.City, World.Camera);
    }

    public NotesExplorer Explorer { get; }
    public EditorOverlay Editor { get; }

    /// <summary>The preview of a file that is not a note (openFilePreview).</summary>
    public FilePreviewPanel FilePreview { get; } = new();

    /// <summary>Open the native hierarchical vault Explorer (1.8.4 Notas), not a web panel.</summary>
    public void ShowNotes()
    {
        if (Editor.IsVisible) return;
        CityMap.IsVisible = false;
        Explorer.ShowTrash(null);
        RefreshNotes();
        _housePanel.IsVisible = false;
        Explorer.IsVisible = true;
    }

    /// <summary>
    /// ui.explorer.open from the editor ("Mostrar nas notas"): Notas over the open editor (theme.css: z-index 52 while
    /// #editorFull is open); closing it shows the editor again, a note tapped there opens in it.
    /// </summary>
    public void ShowNotesOverEditor()
    {
        CityMap.IsVisible = false;
        Explorer.ShowTrash(null);
        RefreshNotes();
        Explorer.ZIndex = 1;
        Explorer.IsVisible = true;
    }

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

    public async Task InitializeAsync()
    {
        if (_vault is not null)
        {
            try
            {
                var snapshot = await _vault.RestoreAsync();
                if (snapshot is not null) { await ShowVaultAsync(snapshot); return; }
            }
            catch (Exception ex) { _loading.Text = "Vault indisponível: " + ex.Message; }
        }
        await LoadAsync(TutorialNotes.Load());
    }

    public async Task OpenVaultAsync()
    {
        if (_vault is null) return;
        try
        {
            var snapshot = await _vault.PickAsync();
            if (snapshot is not null) await ShowVaultAsync(snapshot);
        }
        catch (Exception ex)
        {
            _loading.Text = "Não foi possível abrir a pasta: " + ex.Message;
            _loading.IsVisible = true;
        }
    }

    private async Task ShowVaultAsync(VaultSnapshot snapshot)
    {
        // the previous vault's pending map is written first (abrirCidade: rodarSinc(true) + flush)
        if (_mapSave is { } previous)
        {
            try { await previous.FlushAsync(); } catch { /* shown by the state; the new vault opens anyway */ }
            previous.Stop();
            _mapSave = null;
        }
        _snapshot = snapshot;
        _store = null;
        // workspace.js readSide('trash'): the v2 file, else the v1 file; another version is kept and not written
        string? Text(string path) => snapshot.Files.FirstOrDefault(f => string.Equals(f.Key, path, StringComparison.OrdinalIgnoreCase)).Value is { } file
            ? System.Text.Encoding.UTF8.GetString(file.Bytes.Span).TrimStart('\uFEFF') : null;
        _trash = LegacyTrash.Read(Text(LegacyTrash.Path), Text(LegacyTrash.PathV1), out _trashReadOnly);
        LoadHistory(Text);
        Editor.SaveOnClose = !snapshot.IsReadOnly;
        var texts = snapshot.Documents.Where(d => d.Text is not null).ToArray();
        // every other file of the vault, by name only, for the Explorer (DEC-0045, UC-38); hidden folders (.urbe) stay out
        _attachments.Clear();
        if (_vault is not null)
        {
            try { foreach (var path in await _vault.ListFilePathsAsync()) _attachments.Add(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { /* the Explorer shows the notes */ }
        }
        var notes = texts.Select(d => new CityNote(d.Path, d.Text!)).ToArray();
        var ids = texts.ToDictionary(d => d.Path, d => d.Id, StringComparer.Ordinal);
        // 1.8.4 abrirCidade: the saved map (.urbe/mapa.json) is respected.
        // The terrain is always UrbeTerrain.createWorld('urbe'); mapa.mundo only names its version.
        DocumentStore? store = null;
        LegacyCity? opened = null;
        await LoadAsync(notes, city =>
        {
            store = city.Open(notes.Select(n => (n.Path, n.Content)).ToList(), snapshot.MapJson, ids,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            opened = city;
            return true;
        });
        _vaultName.Text = snapshot.IsReadOnly ? "Vault: somente leitura" : "";
        // v23Atualizar: the brand is Disco.cidade (the vault folder's name), 'Urbe' without one
        _brandName.Text = _vault?.VaultName is { Length: > 0 } folder ? folder : "Urbe";
        if (_brandPill is not null) ToolTip.SetTip(_brandPill, "Salvo na pasta escolhida no dispositivo");
        if (store is null || opened is null || !ReferenceEquals(World.City, opened)) return;
        _store = store;
        StartMapSaves(snapshot, opened);
        // tutorial.js: workspace:loaded → setTimeout(semear, 300)
    }

    /// <summary>
    /// From now on the city's changes are written to .urbe/mapa.json like the 1.8.4 (workspace.js is
    /// the single writer): only for a real, writable vault whose map the 1.8.4 would rewrite. Not for a
    /// read-only vault or a map of a future/unknown format (REQ-035/REQ-040), and not while the map
    /// belongs to another world: the 1.8.4 first reorganizes such a city (urbeReorganizarCidade, not
    /// ported), so saving the old geometry as this world's would lose that reorganization.
    /// </summary>
    private void StartMapSaves(VaultSnapshot snapshot, LegacyCity city)
    {
        if (_vault is null || snapshot.IsReadOnly || snapshot.IsMapReadOnly || snapshot.RecoveredFromJournal ||
            city.WorldReorganizationPending) return;
        var writer = new LegacyCityMap();
        MapSaveScheduler? scheduler = null;
        scheduler = new MapSaveScheduler(() =>
        {
            if (World.City is not { } live || !ReferenceEquals(live, city) || _store is null) return null;
            var text = writer.Produce(live, _store, World.Camera.X, World.Camera.Y, World.Camera.Zoom,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), LegacyCityMap.ReferenceVersion, out var refusal);
            if (text is null) scheduler!.Refused(refusal ?? "");
            return text;
        }, json => _vault.SaveMapAsync(json), snapshot.MapJson);
        scheduler.StateChanged += (_, _) =>
        {
            _vaultName.Text = scheduler.State switch { "error" => "Erro ao salvar o mapa", "refused" => "Mapa não salvo", _ => "" };
            // statusSinc: 'gravando' while writing, 'erro' on a failure, else ok
            _saveState = scheduler.State switch { "saving" => "gravando", "error" => "erro", _ => "ok" };
            PaintDot();
        };
        _mapWriter = writer;
        _mapSave = scheduler;
        // abrirCidade ends with marcarSinc(): the first save writes the map of the opened city
        scheduler.MarkSync();
    }

    /// <summary>The map save of the open vault (null for the Tutorial preview and read-only vaults).</summary>
    public MapSaveScheduler? MapSave => _mapSave;

    /// <summary>The documents of the open vault (DocumentStore of the city), or null.</summary>
    public DocumentStore? Documents => _store;

    /// <summary>flushPending: write a pending map now (window closing, app going to the background).</summary>
    public Task FlushMapAsync() => _mapSave?.FlushAsync() ?? Task.CompletedTask;

    private DispatcherTimer? _roadTimer;
    private string? _roadNote, _roadSignature;

    /// <summary>
    /// scheduleRoadRebuild: 120 ms after the last call the streets are redrawn from the documents (with the text being
    /// typed in the editor, which the 1.8.4 already holds in its document store), unless no note links another; the
    /// villagers' routes go with them (v25: v25Rotas.clear(), v25Povo=[]).
    /// </summary>
    public void ScheduleRoadRebuild()
    {
        if (_roadTimer is null)
        {
            _roadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _roadTimer.Tick += (_, _) => { _roadTimer!.Stop(); RebuildRoadsNow(); };
        }
        _roadTimer.Stop();
        _roadTimer.Start();
    }

    /// <summary>The pending scheduleRoadRebuild at once (tests).</summary>
    public bool RebuildRoadsNow()
    {
        _roadTimer?.Stop();
        if (World.City is not { } city || _store is null) return false;
        var editing = Editor.IsVisible && Editor.Current is { } open && _store.Get(open.Path) is { } doc ? (doc.Id, open.Content) : default;
        if (!city.RebuildRoadsAfterChange(_store, editing.Id, editing.Content)) return false;
        World.RoadsChanged();
        return true;
    }

    /// <summary>markChanged / syncVisualToMarkdown: a change of the note's [[link]] signature redraws the streets.</summary>
    private void EditorLinksChanged()
    {
        if (Editor.Current is not { } note) return;
        var signature = LegacyCity.LinkSignature(note.Content);
        if (_roadNote != note.Path)
        {
            _roadNote = note.Path;   // the note just opened: its text is the baseline
            _roadSignature = signature;
            return;
        }
        if (signature == _roadSignature) return;
        _roadSignature = signature;
        ScheduleRoadRebuild();
    }

    private async Task SaveNoteAsync(CityNote note)
    {
        if (_vault is null || !_vault.IsConnected || _snapshot is null)
            throw new InvalidOperationException("Selecione uma pasta real para gravar.");
        if (_snapshot.IsReadOnly)
            throw new InvalidOperationException("Vault em modo somente leitura.");
        await _vault.SaveExistingTextFileAsync(note.Path, note.Content);
        _contents[note.Path] = note.Content;
        // 1.8.4 editor input: b.modified=nowDate(); session.record(..., {modified:b.modified}) →
        // document:updated → persistence schedule() + marcarSinc()
        if (_store?.Get(note.Path) is { } doc && doc.Content != note.Content && World.City is { } city)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (city.Buildings.Find(b => b.IsNote && (b.DocumentId == doc.Id || (b.DocumentId is null && b.Path == doc.Path))) is { } house) city.NoteEdited(house, now);
            _store.Upsert(new DocumentInput { Id = doc.Id, Path = doc.Path, Title = doc.Title, Content = note.Content,
                Created = doc.Created, Modified = LegacyCity.NowDate(now) }, "editor.input");
            _mapSave?.Schedule();
            _mapSave?.MarkSync();
        }
        await SaveHistoryAsync();
        NoteSaved?.Invoke(note.Path);   // plugins: "nota:salva"
    }

    /// <summary>
    /// Generates the 1.8.4 world and opens the files as the Tutorial's first open
    /// (LegacyCity.OpenFirstTime), off the UI thread; then frames all houses (city.fit, as
    /// tutorial.js semear does on an empty city).
    /// </summary>
    public Task LoadAsync(IReadOnlyList<CityNote> files, string seed = "urbe") =>
        LoadAsync(files, city =>
        {
            city.OpenFirstTime(files.Select(f => (f.Path, f.Content)).ToList());
            return false;
        }, seed);

    /// <summary>
    /// Generates the world and lets <paramref name="open"/> build the city off the UI thread. When it
    /// returns true the camera is the city's (abrirCidade: mapa.camera or the 1.8.4 start, no
    /// framing: urbeEnquadrarNotas runs on workspace:loaded, before the houses exist); otherwise
    /// all houses are framed.
    /// </summary>
    private async Task LoadAsync(IReadOnlyList<CityNote> files, Func<LegacyCity, bool> open, string seed = "urbe")
    {
        _contents.Clear();
        foreach (var f in files) _contents[f.Path] = f.Content;
        _today = DateTime.Now.ToString("yyyy-MM-dd");
        try
        {
            var (world, city, ownCamera) = await Task.Run(() =>
            {
                var w = LegacyWorld.Shared(seed);
                int next = 0;
                // app.js id(): ids seed lot order and growth; deterministic here so the same files give the same city.
                var city = new LegacyCity(new LegacyCityTerrain((x, y) => w.At(x, y).Biome), prefix => prefix + (++next).ToString("x"));
                return (w, city, open(city));
            });
            World.Load(world, city);
            CityMap.IsVisible = false;
            Explorer.SetNotes(files, city.RegionPaths().Values);
            if (ownCamera) World.ShowCityCamera();
            else World.FitNotes();
            // workspace:loaded → customize recarregar(); it writes only with a writable vault folder
            Customization.Writer = _vault is { IsConnected: true } && _snapshot is { IsReadOnly: false } ? WriteVaultFileAsync : null;
            Customization.Load(_contents);
            // .urbe/mundo.json (DEC-0045, UC-41): muralha e contornos; written only with a writable vault
            Customization.WorldWriter = Customization.Writer is null || _vault is null ? null : json => _vault.SaveWorldAsync(json);
            Customization.LoadWorld(_snapshot?.Files.FirstOrDefault(f => string.Equals(f.Key, WorldPrefs.FilePath, StringComparison.OrdinalIgnoreCase)).Value is { } mundo
                ? System.Text.Encoding.UTF8.GetString(mundo.Bytes.Span) : null);
            World.Prefs = Customization.World;
            _biome.Text = World.BiomeNameAtCentre();
            _loading.IsVisible = false;
        }
        catch (Exception ex)
        {
            _loading.Text = "Não foi possível gerar o mundo: " + ex.Message;
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        bool wide = e.NewSize.Width >= WideBreakpoint;
        _rail.IsVisible = wide;
        _dock.IsVisible = !wide && !Editor.IsVisible;
        // theme.css @media (min-width:900px): Notas becomes a 360 px panel beside the city
        Explorer.Wide = e.NewSize.Width >= 900;
        PlaceHousePanel(wide);
    }

    // ---------------- city chrome ----------------

    private readonly Border _vaultDot = new() { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center };
    private string _saveState = "ok";
    private TextBlock _brandName = new();
    private Border? _brandPill;

    /// <summary>
    /// #v23Top: the brand pill (#v23VaultDot: green, warn while writing, danger on an error — statusSinc; the vault name;
    /// "· biome") and the three round buttons Buscar notas, Mapa da cidade and Configurações. Both 44px tall, surface at
    /// 82 %, a faint line and shadow-1. The native-only notices (Tutorial preview, read-only vault, a refused map) follow
    /// the biome in small text; the normal state shows none, as the original.
    /// </summary>
    private Control TopBar()
    {
        var name = _brandName = new TextBlock { Text = "Urbe", FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center };
        _vaultName.Text = "Tutorial · prévia";
        _vaultName.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _vaultName.FontSize = 11;
        _vaultName.VerticalAlignment = VerticalAlignment.Center;
        _vaultName.Margin = new Thickness(8, 0, 0, 0);
        // #urbeBiome::before{content:'·';margin:0 7px 0 1px;color:text-3}; 500 13px text-2
        var sep = new TextBlock { Text = "·", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 7, 0) };
        _biome.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _biome.FontSize = 13;
        _biome.FontWeight = FontWeight.Medium;
        _biome.VerticalAlignment = VerticalAlignment.Center;
        _biome.TextTrimming = TextTrimming.CharacterEllipsis;
        PaintDot();
        _vaultDot.Margin = new Thickness(0, 0, 10, 0);
        var pill = new Border
        {
            Background = UrbeTheme.Brush(UrbeTheme.Surface, .82),
            BorderBrush = UrbeTheme.Brush("#ffffff", .06),
            BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 1 2 0 #59000000, 0 6 20 0 #47000000"),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(12, 0, 14, 0),
            Height = 44,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { _vaultDot, name, sep, _biome, _vaultName } }
        };
        _brandPill = pill;
        ToolTip.SetTip(pill, "Estado da gravação");
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { SearchButton(), MapButton(), SettingsButton() }
        };
        _topButtons = buttons;   // the plugins' buttons go before Configurações
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12, 10, 12, 0), VerticalAlignment = VerticalAlignment.Top };
        SetColumn(buttons, 2);
        bar.Children.Add(pill);
        bar.Children.Add(buttons);
        return bar;
    }

    /// <summary>#v23VaultDot: ok (green), gravando (warn) or erro (danger), with a 3px ring of the same colour at 15 %.</summary>
    private void PaintDot()
    {
        var color = _saveState switch { "gravando" => UrbeTheme.Warn, "erro" => UrbeTheme.Danger, _ => UrbeTheme.Ok };
        _vaultDot.Background = UrbeTheme.Brush(color);
        _vaultDot.BoxShadow = BoxShadows.Parse("0 0 0 3 #26" + color[1..]);   // the ring: the colour at 15 %
    }

    /// <summary>The state of the dot (statusSinc), for the tests.</summary>
    public string SaveDotState => _saveState;

    /// <summary>
    /// settings.js: "Configurações" with what the native app has — Personalização, Tutorial, Exportar tudo (.zip),
    /// Importar arquivos and Pasta no dispositivo (the vault folder). Organizar os bairros, Assistente, Páginas,
    /// Diagnóstico de toques and Procurar atualização join as they are ported.
    /// </summary>
    public Task ShowSettingsAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var items = new List<DialogItem>
        {
            new("Personalização", UrbeTheme.Icons.Brush, "Tema, cores, fontes, cidade, texturas, estilos e plugins", Run: () => { Customize.Open(); return Task.CompletedTask; }),
            new("Tutorial", UrbeTheme.Icons.Book, "Guia completo de tudo o que o Urbe faz", Run: ShowTutorialMenuAsync)
        };
        if (_vault is not null)
            items.Add(new("Exportar tudo (.zip)", UrbeTheme.Icons.Download, "Cópia de segurança de todas as notas", Run: () => ExportAsync()));
        items.Add(new("Importar arquivos", UrbeTheme.Icons.Upload, "Notas, pastas ou .zip do aparelho", Run: () => ImportAsync()));
        if (_vault is not null)
            items.Add(new("Pasta no dispositivo", UrbeTheme.Icons.Storage, "Onde os arquivos ficam salvos", Run: () => OpenVaultAsync()));
        return Dialogs.MenuAsync("Configurações", items);
    }

    private Button SearchButton()
    {
        var button = RoundButton(UrbeTheme.Icons.SearchUi, "Buscar");
        ToolTip.SetTip(button, "Buscar notas");
        button.Click += (_, _) => Search.OpenQuickOpen();
        return button;
    }

    private Button MapButton()
    {
        var button = RoundButton(UrbeTheme.Icons.MapUi, "Mapa da cidade");
        ToolTip.SetTip(button, "Mapa da cidade");
        button.Click += (_, _) => ShowMap();
        return button;
    }

    private Button SettingsButton()
    {
        var b = RoundButton(UrbeTheme.Icons.Settings, "Configurações");
        ToolTip.SetTip(b, "Configurações");
        b.Click += (_, _) => _ = ShowSettingsAsync();
        return b;
    }

    private static Button RoundButton(string icon, string tip)
    {
        // .v23TopBtn: 44px circle, surface at 82 %, a faint line, shadow-1, a 20px icon
        var b = new Button
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = UrbeTheme.Brush(UrbeTheme.Surface, .82),
            BorderBrush = UrbeTheme.Brush("#ffffff", .06),
            BorderThickness = new Thickness(1),
            Content = UrbeTheme.Icon(icon, 20, UrbeTheme.Text, 1.8),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        ToolTip.SetTip(b, tip);
        return b;
    }

    /// <summary>Personalização (customize.js): Personalização/tema.json of the open vault.</summary>
    public Customization Customization { get; } = new();
    public CustomizePanel Customize { get; }

    /// <summary>aplicarCidade(c) → world.custom options (moradores, fauna, clima, eventos, nomes, bairros, ambiente).</summary>
    private void ApplyCustomization()
    {
        var o = World.Life.Options;
        o.People = Customization.City("moradores");
        o.Fauna = Customization.City("fauna");
        o.Weather = Customization.City("clima");
        o.Events = Customization.City("eventos");
        World.ShowHouseNames = Customization.City("nomes");
        World.ShowDistrictNames = Customization.City("bairros");
        World.LightingMode = Customization.Ambient;
        World.Prefs = Customization.World;
        UrbeAppearance.Apply(this);   // Personalização: tema, texto, forma, editor e chão
        if (Customization.Config["editor"] is System.Text.Json.Nodes.JsonObject ed) Editor.ApplyOptions((string?)ed["largura"], (string?)ed["modoInicial"]);
        if (Editor.IsVisible) Editor.RefreshVisual();   // tag colours (DEC-0045, UC-43)
        World.InvalidateVisual();
    }

    /// <summary>The customize.js writer (docs.upsert of a Personalização file): the vault file, and,
    /// as every 1.8.4 document, a house in its folder's neighbourhood when it is new.</summary>
    private async Task WriteVaultFileAsync(string path, string content, bool exists)
    {
        if (_vault is null || !_vault.IsConnected || _snapshot is null || _snapshot.IsReadOnly)
            throw new InvalidOperationException("Selecione uma pasta real para gravar.");
        if (exists) await _vault.SaveExistingTextFileAsync(path, content);
        else
        {
            await _vault.CreateTextFileAsync(path, content);
            if (World.City is { } city && city.Buildings.All(b => b.Path != path))
            {
                var title = new DocumentStore().Upsert(new DocumentInput { Path = path, Content = content }).Title;
                city.AddDocument(path, LegacyCity.NoteName(title, path));
                World.InvalidateVisual();
            }
        }
        _contents[path] = content;
        RefreshNotes();
    }

    /// <summary>UrbeDialogs: one dialog at a time over everything.</summary>
    public UrbeDialogs Dialogs { get; } = new();

    /// <summary>The formula pane of the editor (math/editor.js #mathPane).</summary>
    public FormulaPane Formula { get; } = new();

    /// <summary>v23MenuCriar: the primary action of the + button.</summary>
    public Task ShowCreateMenuAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        return Dialogs.MenuAsync("Criar",
        [
            // DEC-0045 (UC-41): the 1.8.4 had two items for each thing (Nova nota / Adicionar construção, Nova pasta /
            // Desenhar pasta); the city builds, draws, decorates and imports. Nova nota and Nova pasta stay in the Explorer.
            new DialogItem("Construção", UrbeTheme.Icons.House, "Um arquivo onde você tocar no mapa (.md, .html, .js…)", Run: BeginBuildingAsync),
            new DialogItem("Região", UrbeTheme.Icons.Region, "Uma pasta desenhada no mapa", Run: BeginDrawRegionAsync),
            new DialogItem("Decoração", UrbeTheme.Icons.Flower, "Poço, roça, lenha, banco, fonte, canteiro", Run: BeginDecorationAsync),
            new DialogItem("Importar", UrbeTheme.Icons.Upload, "Notas, pastas ou .zip do aparelho", Run: () => ImportAsync())
        ]);
    }

    /// <summary>
    /// v21CreateFileInRegion: name dialog, the house in the neighbourhood (or loose), the file
    /// written to the vault, the camera on the house and the editor open. Nothing is created in
    /// memory only: without a writable vault folder the note is not created.
    /// </summary>
    public async Task<CityNote?> CreateNoteAsync(LegacyCityRegion? region, string? typedName = null, bool open = true)
    {
        if (World.City is not { } city) return null;
        if (_vault is null || !_vault.IsConnected || _snapshot is null || _snapshot.IsReadOnly)
        {
            await Dialogs.AlertAsync("Nova nota", "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para criar notas. O Tutorial desta prévia é só leitura.");
            return null;
        }
        var typed = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Nova nota", Label: "Nome", Placeholder: "Ex.: Ideias",
            Hint: "Sem extensão, vira .md. Também aceita .txt, .html, .css, .js, .json, .yaml e .csv.", Confirm: "Criar nota"));
        if (typed is null) return null;
        // app.js has one camera: the loose lot is searched where the person is looking (centroBuscaRaiz)
        city.CameraX = World.Camera.X;
        city.CameraY = World.Camera.Y;
        city.CameraZoom = World.Camera.Zoom;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (city.CreateNote(region, typed, now) is not { } made)
        {
            await Dialogs.AlertAsync("Nova nota", "Não encontrei lote livre.");
            return null;
        }
        try
        {
            await _vault.CreateTextFileAsync(made.House.Path, made.Content);
        }
        catch (Exception ex)
        {
            city.RemoveBuilding(made.House);
            World.InvalidateVisual();
            await Dialogs.AlertAsync("Não foi possível criar a nota", ex.Message);
            return null;
        }
        _contents[made.House.Path] = made.Content;
        // urbeReconcileWorld: the new house gets its document ({path, content, created, modified} =
        // the house's nowDate()); agendarSalvar() + marcarSinc() write the map with it
        if (_store is not null)
        {
            var today = LegacyCity.NowDate(now);
            _store.Upsert(new DocumentInput { Path = made.House.Path, Content = made.Content, Created = today, Modified = today }, "world.create");
            _mapSave?.MarkSync();
        }
        ScheduleRoadRebuild();   // v21CreateFileInRegion: scheduleRoadRebuild
        RefreshNotes();
        World.CenterOn(made.House);
        World.Select(made.House);
        var note = new CityNote(made.House.Path, made.Content);
        if (open) Editor.Open(note);
        return note;
    }

    /// <summary>
    /// The editor's "Mais ações" (v23 overflow → abrirMenuExplorer(nomeCompletoNota(currentFile), …)) with the
    /// actions the native app already has, in the 1.8.4's order: Renomear, Mostrar nas notas, Versões anteriores,
    /// Estrutura e backlinks and Mover para a lixeira. Assistente joins with the assistant.
    /// </summary>
    private Task ShowEditorMenuAsync(EditorOverlay editor)
    {
        if (Dialogs.IsOpen || editor.Current is not { } note) return Task.CompletedTask;
        var house = World.City?.Buildings.Find(b => b.IsNote && b.Path == note.Path);
        var title = house is not null ? LegacyCity.FullNoteName(house) : System.IO.Path.GetFileName(note.Path);
        return Dialogs.MenuAsync(title,
        [
            new DialogItem("Renomear", UrbeTheme.Icons.Pencil, Run: () => RenameNoteAsync(note.Path)),
            new DialogItem("Mostrar nas notas", UrbeTheme.Icons.Notes, Run: () => { ShowNotesOverEditor(); return Task.CompletedTask; }),
            new DialogItem("Versões anteriores", UrbeTheme.Icons.Restore, Run: ShowVersionsAsync),
            new DialogItem("Estrutura e backlinks", UrbeTheme.Icons.List, Run: () => { ShowEditorContext(); return Task.CompletedTask; }),
            // DEC-0045, UC-39: the selection (or the whole note) stays at the top while writing
            new DialogItem("Fixar em painel", UrbeTheme.Icons.Pin, "A seleção, ou a nota inteira, fica no topo enquanto você escreve.",
                Run: () => { editor.PinSelectionOrNote(); return Task.CompletedTask; }),
            new DialogItem("Mover para a lixeira", UrbeTheme.Icons.Trash, Danger: true, Run: () => DeleteNoteAsync(note.Path))
        ]);
    }

    /// <summary>
    /// renomearNota (final, app.js ≈2397): UD.prompt "Renomear" with the full file name, v21UniqueName in the
    /// house's neighbourhood and aplicarNomeCompletoNota. The 1.8.4 moves the file at its next save
    /// (urbeReconcileWorld 'world.move' + flush: new path written, old one removed); here the file is moved
    /// in the vault FIRST, verified, and only then the house, the document and the map follow, so nothing is
    /// renamed in memory only. Unsaved text of the open note is saved before the move.
    /// </summary>
    public async Task RenameNoteAsync(string path, string? typedName = null)
    {
        if (World.City is not { } city || city.Buildings.Find(b => b.IsNote && b.Path == path) is not { } house) return;
        if (_vault is null || !_vault.IsConnected || _snapshot is null || _snapshot.IsReadOnly)
        {
            await Dialogs.AlertAsync("Renomear", "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para renomear notas. O Tutorial desta prévia é só leitura.");
            return;
        }
        var typed = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Renomear", Label: "Nome do arquivo",
            Value: LegacyCity.FullNoteName(house), Hint: "Mantenha a extensão (.md, .txt, .html…) para preservar o tipo.", Confirm: "Renomear"));
        if (typed is null) return;
        if (city.PlanRename(house, typed) is not { } plan) return;
        var editing = Editor.IsVisible && Editor.Current?.Path == path;
        if (editing && Editor.HasChanges && !await Editor.SaveAsync())
        {
            await Dialogs.AlertAsync("Não foi possível renomear", "A nota tem alterações que não foram salvas no vault.");
            return;
        }
        if (!string.Equals(plan.Path, path, StringComparison.Ordinal))
        {
            try
            {
                await _vault.MoveTextFileAsync(path, plan.Path);
            }
            catch (Exception ex)
            {
                await Dialogs.AlertAsync("Não foi possível renomear", ex.Message);
                return;
            }
            if (_contents.Remove(path, out var content)) _contents[plan.Path] = content;
        }
        city.ApplyRename(house, plan, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (!string.Equals(plan.Path, path, StringComparison.Ordinal))
            await CarryLooksAsync(new Dictionary<string, string>(StringComparer.Ordinal) { [path] = plan.Path });
        if (_store?.Get(path) is { } doc && !string.Equals(doc.Path, plan.Path, StringComparison.Ordinal))
            LegacyCity.MoveDocument(_store, doc, plan.Path);
        // aplicarNomeCompletoNota: buildTree, scheduleRoadRebuild, agendarSalvar + marcarSinc, pedirDesenho; v21RefreshEditorChrome
        ScheduleRoadRebuild();
        _mapSave?.MarkSync();
        RefreshNotes();
        if (ReferenceEquals(World.Selected, house)) ShowHouse(house);
        World.InvalidateVisual();
        if (editing)
        {
            Editor.Renamed(plan.Path);
            Editor.Notify("Arquivo renomeado.");
        }
    }

    /// <summary>
    /// removerNota (app.js ≈1852): UD.confirm "Mover para a lixeira?", explorer.delete → trash.trash (the document kept
    /// in .urbe/trash.v2.json, removed from the store), the house leaves the city and the editor closes; the 1.8.4
    /// removes the file at its next save. Here the trash is written and verified first, then the file is removed,
    /// and only then the city, the document and the map follow. Nothing is deleted in memory only.
    /// </summary>
    public async Task<bool> DeleteNoteAsync(string path, bool confirmed = false)
    {
        if (World.City is not { } city || city.Buildings.Find(b => b.IsNote && b.Path == path) is not { } house) return false;
        if (_vault is null || !_vault.IsConnected || _snapshot is null || _snapshot.IsReadOnly || _store is null)
        {
            await Dialogs.AlertAsync("Excluir", "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para excluir notas. O Tutorial desta prévia é só leitura.");
            return false;
        }
        if (_trashReadOnly)
        {
            await Dialogs.AlertAsync("Excluir", "A lixeira deste vault é de outra versão do Urbe e foi preservada; nada é excluído.");
            return false;
        }
        if (!confirmed && !await Dialogs.ConfirmAsync("Mover para a lixeira?",
                "“" + LegacyCity.FullNoteName(house) + "” poderá ser restaurada pela Lixeira, em Notas.", "Mover para a lixeira", danger: true))
            return false;
        var editing = Editor.IsVisible && Editor.Current?.Path == path;
        if (editing && Editor.HasChanges && !await Editor.SaveAsync())
        {
            await Dialogs.AlertAsync("Não foi possível excluir", "A nota tem alterações que não foram salvas no vault.");
            return false;
        }
        if (_store.Get(path) is not { } doc) return false;
        // the trash as it will be: nothing changes in memory until the vault holds it
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        try
        {
            await _vault.TrashTextFileAsync(path, _trash.ExportWith(doc, now));
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível excluir", ex.Message);
            return false;
        }
        _trash.Trash(_store, doc.Id, now, "explorer.delete");
        city.DeleteNote(house);
        _contents.Remove(path);
        if (editing)
        {
            Editor.IsVisible = false;
            _editorOpenedFromNotes = false;
        }
        await CaptureRemovedAsync(doc);   // document:removed → history.captureRemoved
        // removerNota (house panel, editor): marcarIndice, indexar, scheduleRoadRebuild, counts, buildTree, agendarSalvar +
        // marcarSinc. From Notas it is explorer.delete → document:removed, which does not redraw the streets.
        if (!confirmed) ScheduleRoadRebuild();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
        return true;
    }

    /// <summary>Notas shows the notes and, like model.folders, every neighbourhood's folder even without notes.</summary>
    private readonly HashSet<string> _attachments = new(StringComparer.Ordinal);

    private void RefreshNotes()
    {
        Explorer.SetNotes(_contents.Select(n => new CityNote(n.Key, n.Value) { Title = _store?.Get(n.Key)?.Title }), World.City?.RegionPaths().Values);
        var files = new HashSet<string>(_attachments, StringComparer.Ordinal);
        if (World.City is { } city) foreach (var b in city.Buildings) if (!b.IsNote && !string.IsNullOrEmpty(b.Path)) files.Add(b.Path);
        files.ExceptWith(_contents.Keys);
        Explorer.SetFiles(files);
    }

    /// <summary>
    /// A file of the Explorer that is not a note (DEC-0045, UC-38): its preview (abrirEntradaTree → openFilePreview),
    /// with "Mostrar na cidade" when the city has its building.
    /// </summary>
    private Task ShowFileAsync(string path)
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var building = World.City?.Buildings.Find(b => !b.IsNote && (b.Path == path || b.PreviewFiles.Any(f => f.Path == path)));
        return FilePreview.OpenAsync(path, building);
    }

    /// <summary>
    /// The "⋯" of a folder in the Explorer (the 1.8.4 showed no options when a folder was selected, DEC-0045, UC-38):
    /// Nova nota aqui, Nova subpasta, Mostrar na cidade and Mover para a lixeira, each the existing operation.
    /// </summary>
    private Task ShowFolderMenuAsync(string folder)
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var region = World.City?.RegionByPath(folder);
        var items = new List<DialogItem>
        {
            new("Nova nota aqui", UrbeTheme.Icons.File, NotesWhere(folder), Run: () => CreateNoteInNotesAsync(folder: folder)),
            new("Nova subpasta", UrbeTheme.Icons.Folder, "Vira uma região dentro desta.", Run: () => CreateFolderAsync(region?.Id))
        };
        if (region is not null)
            items.Add(new("Editar nome, cor e descrição", UrbeTheme.Icons.Pencil, "Renomear a pasta muda o nome dela no vault.", Run: () => EditRegionAsync(region)));
        if (region is not null)
            items.Add(new("Mostrar na cidade", UrbeTheme.Icons.City, "O bairro desta pasta.", Run: () =>
            {
                if (!Explorer.Wide) Explorer.IsVisible = false;
                World.CenterOnRegion(region);
                return Task.CompletedTask;
            }));
        items.Add(new("Mover para a lixeira", UrbeTheme.Icons.Trash, "A pasta e as notas dela.", Run: () => SelectionActionAsync("delete", [NotesExplorer.FolderPrefix + folder])));
        return Dialogs.MenuAsync(folder[(folder.LastIndexOf('/') + 1)..], items);
    }

    /// <summary>mobile-ui.js newMenu: "Criar" with Nova nota (in the selected folder) and Nova pasta. Nova página joins
    /// with the pages.</summary>
    public Task ShowNotesCreateMenuAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        var folder = Explorer.CurrentFolder;
        return Dialogs.MenuAsync("Criar",
        [
            new DialogItem("Nova nota", UrbeTheme.Icons.File, NotesWhere(folder), Run: () => CreateNoteInNotesAsync()),
            new DialogItem("Nova pasta", UrbeTheme.Icons.Folder, "Vira uma região na cidade.",
                Run: () => CreateFolderAsync(World.City?.RegionByPath(folder)?.Id))
        ]);
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
    /// extension becomes .md, a name already taken there is refused; the empty document is created (explorer.create),
    /// gets its house like any new document (document:created → urbeCasaParaDocumento), opens, and Notas closes. The
    /// empty file is written to the vault first.
    /// </summary>
    public async Task CreateNoteInNotesAsync(string? typedName = null, string? folder = null)
    {
        if (World.City is not { } city || _store is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Nova nota", "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para criar notas. O Tutorial desta prévia é só leitura.");
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
        try { await _vault!.CreateTextFileAsync(path, ""); }
        catch (Exception ex) { await Dialogs.AlertAsync("Não foi possível criar a nota", ex.Message); return; }
        var doc = _store.Upsert(new DocumentInput { Path = path, Content = "" }, "explorer.create");
        city.AddDocumentHouse(doc, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _contents[path] = "";
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
        // document.open: touchRecent is not called by createNote; the editor opens and Notas closes
        _editorOpenedFromNotes = false;
        Explorer.IsVisible = false;
        Editor.Open(new CityNote(path, ""));
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
            int total = notes.Count + folders.Sum(f => LegacyCity.FolderDocuments(_store!, f).Count);
            title = folders.Count == 1 && notes.Count == 0
                ? "Excluir a pasta “" + folders[0][(folders[0].LastIndexOf('/') + 1)..] + "”?"
                : "Excluir " + string.Join(" e ", new[] { Plural(folders.Count, "pasta", "pastas"), notes.Count > 0 ? Plural(notes.Count, "nota", "notas") : "" }.Where(x => x.Length > 0)) + "?";
            message = (total > 0
                ? Plural(total, "nota vai", "notas vão") + " para a Lixeira e " + (total == 1 ? "pode" : "podem") + " ser restaurada" + (total == 1 ? "" : "s") + ". "
                : "A pasta está vazia. ") + "A pasta também sai da cidade.";
        }
        else
        {
            title = "Mover para a lixeira?";
            message = notes.Count == 1 ? "A nota poderá ser restaurada pela Lixeira." : notes.Count + " notas poderão ser restauradas pela Lixeira.";
        }
        if (!await Dialogs.ConfirmAsync(title, message, folders.Count > 0 ? "Excluir" : "Mover para a lixeira", danger: true)) return;
        foreach (var f in folders)
            if (!await DeleteFolderAsync(f)) break;
        foreach (var p in notes) await DeleteNoteAsync(p, confirmed: true);
        Explorer.ClearSelection();
    }

    /// <summary>
    /// explorer.deleteFolder (operations.js removeFolder): every note under the folder goes to the trash as a note
    /// deleted alone (the trash written and verified before each file leaves), then 'explorer:folderRemoved' →
    /// apagarRegiao removes the neighbourhood and its sub-neighbourhoods from the city and the map is saved; the
    /// emptied folders are removed, deepest first, like the sync's FS.apagarPasta. Deliberate deviation for the
    /// user's data: the 1.8.4 sync also deletes the attached files (images, PDFs…) of that folder for good; here any
    /// file that is not a note stays in the vault, and a folder that still holds something stays too.
    /// If a note cannot go to the trash, nothing more is done and the neighbourhood stays.
    /// </summary>
    private async Task<bool> DeleteFolderAsync(string folder)
    {
        if (World.City is not { } city || _store is null || _vault is null) return false;
        var docs = LegacyCity.FolderDocuments(_store, folder);
        var dirs = new HashSet<string>(StringComparer.Ordinal) { folder };
        foreach (var d in docs)
        {
            for (var dir = d.Path[..d.Path.LastIndexOf('/')]; dir.Length > folder.Length; dir = dir[..dir.LastIndexOf('/')]) dirs.Add(dir);
            if (city.Buildings.Find(b => b.IsNote && b.Path == d.Path) is null) return false;
            if (!await DeleteNoteAsync(d.Path, confirmed: true)) return false;
        }
        if (city.RegionByPath(folder) is { } region)
            foreach (var p in city.DeleteRegion(region)) dirs.Add(p);
        ScheduleRoadRebuild();        // apagarRegiao
        ExplorerState.Forget(folder); // explorer.forgetFolder
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        foreach (var dir in dirs.Where(d => d == folder || d.StartsWith(folder + "/", StringComparison.Ordinal))
                     .OrderByDescending(d => d.Count(c => c == '/')).ThenBy(d => d, StringComparer.Ordinal))
        {
            try { await _vault.RemoveEmptyDirectoryAsync(dir); }
            catch (Exception) { /* FS.apagarPasta: a folder that cannot be removed stays (removeEntry errors are ignored) */ }
        }
        RefreshNotes();
        World.InvalidateVisual();
        return true;
    }

    /// <summary>
    /// mobile-ui.js moreMenu ("Notas") with the items the native app already has, in its order: Recentes, Favoritos,
    /// Importar arquivos (into the open folder), Exportar tudo (.zip), Lixeira (n item(ns)) and Pasta do Urbe no
    /// dispositivo. Páginas and Composições join as their screens are ported.
    /// </summary>
    public Task ShowNotesMenuAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        int n = _trash.Items.Count;
        return Dialogs.MenuAsync("Explorer",
        [
            new DialogItem("Recentes", UrbeTheme.Icons.Clock, Run: () => { ShowCollection("recent"); return Task.CompletedTask; }),
            new DialogItem("Favoritos", UrbeTheme.Icons.Star, Run: () => { ShowCollection("favorites"); return Task.CompletedTask; }),
            new DialogItem("Importar arquivos", UrbeTheme.Icons.Upload, "Notas, pastas ou .zip do aparelho",
                Run: () => ImportAsync(World.City?.RegionByPath(Explorer.CurrentFolder)?.Id)),
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
            await FlushMapAsync();
            var files = await vault.ReadAllFilesAsync();
            var zip = VaultArchive.Export(files, new VaultArchiveExportOptions
            {
                AppVersion = "urbe-native-preview",
                VaultName = vault.VaultName,
                VaultFormatVersion = _snapshot.VaultFormatVersion
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
    /// (explorer.move: the file moves, the house changes neighbourhood and keeps its lot), Duplicar (explorer.duplicate)
    /// and Mover para a lixeira (explorer.delete, one confirmation for all). Files are written (and verified) before the
    /// city, the documents and the map change; the selection is cleared after the action (model.clear()).
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
                    _editorOpenedFromNotes = true;
                    Explorer.IsVisible = false;
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

    /// <summary>folders(): every folder of the Notas tree (neighbourhoods and the folders of documents), depth first,
    /// each level sorted by name (localeCompare).</summary>
    private List<string> NoteFolders()
    {
        var all = new HashSet<string>(StringComparer.Ordinal);
        void Add(string folder) { for (var f = folder; f.Length > 0; f = f.Contains('/') ? f[..f.LastIndexOf('/')] : "") all.Add(f); }
        if (World.City is { } city) foreach (var path in city.RegionPaths().Values) Add(path);
        foreach (var path in _contents.Keys) if (path.Contains('/')) Add(path[..path.LastIndexOf('/')]);
        var output = new List<string>();
        void Walk(string parent)
        {
            var kids = all.Where(f => (f.Contains('/') ? f[..f.LastIndexOf('/')] : "") == parent)
                .OrderBy(f => f[(f.LastIndexOf('/') + 1)..], StringComparer.CurrentCulture);
            foreach (var k in kids) { output.Add(k); Walk(k); }
        }
        Walk("");
        return output;
    }

    /// <summary>moveSelected: D.choose "Mover para" (Raiz + every folder), then explorer.move for each note.</summary>
    private async Task MoveToFolderAsync(IReadOnlyList<string> paths)
    {
        if (World.City is not { } city || _store is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Mover", NoVaultMessage);
            return;
        }
        if (await ChooseFolderAsync() is not { } folder) return;
        await MoveNotesToAsync(paths, folder);
    }

    /// <summary>uiDestinoRegiao("Mover para"): Raiz or one of the folders; "" is the root, null when cancelled.</summary>
    private async Task<string?> ChooseFolderAsync()
    {
        var folders = NoteFolders();
        var options = new List<DialogItem> { new("Raiz", UrbeTheme.Icons.Notes, "Fora de qualquer pasta") };
        options.AddRange(folders.Select(f => new DialogItem(f[(f.LastIndexOf('/') + 1)..], UrbeTheme.Icons.Folder, f)));
        if (await Dialogs.ChooseAsync("Mover para", options) is not { } chosen) return null;
        return chosen == 0 ? "" : folders[chosen - 1];
    }

    /// <summary>explorer.move of each note into <paramref name="folder"/> ("" = the root): the file first, then the house
    /// changes neighbourhood. Also what dragging notes onto a folder in the Explorer does (DEC-0045, UC-38).</summary>
    public async Task MoveNotesToAsync(IReadOnlyList<string> paths, string folder)
    {
        if (World.City is not { } city || _store is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Mover", NoVaultMessage);
            return;
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var moves = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var from in paths)
        {
            if (_store.Get(from) is not { } doc || city.Buildings.Find(b => b.IsNote && b.Path == from) is not { } house) continue;
            var to = LegacyCity.FolderMovePath(_store, doc, folder);
            if (to == from) continue;
            if (Editor.IsVisible && Editor.Current?.Path == from && Editor.HasChanges && !await Editor.SaveAsync()) continue;
            try { await _vault!.MoveTextFileAsync(from, to); }
            catch (Exception ex) { await Dialogs.AlertAsync("Não foi possível mover", ex.Message); break; }
            if (_contents.Remove(from, out var content)) _contents[to] = content;
            var moved = LegacyCity.MoveDocument(_store, doc, to);
            city.ApplyFolderMove(house, moved, now);
            moves[from] = house.Path;
            if (Editor.IsVisible && Editor.Current?.Path == from) Editor.Renamed(to);
        }
        await CarryLooksAsync(moves);
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
    }

    /// <summary>duplicateSelected: explorer.duplicate for each note — "X cópia.ext" in the same folder with the same
    /// text, and its house 4 tiles away. The new file is written first.</summary>
    private async Task DuplicateNotesAsync(IReadOnlyList<string> paths)
    {
        if (World.City is not { } city || _store is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Duplicar", NoVaultMessage);
            return;
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var from in paths)
        {
            if (_store.Get(from) is not { } doc || city.Buildings.Find(b => b.IsNote && b.Path == from) is not { } house) continue;
            int dot = doc.Path.LastIndexOf('.');
            var target = LegacyCity.UniqueDocumentPath(_store, (dot >= 0 ? doc.Path[..dot] : doc.Path) + " cópia" + (dot >= 0 ? doc.Path[dot..] : ""));
            var content = _contents.GetValueOrDefault(from, doc.Content);
            try { await _vault!.CreateTextFileAsync(target, content); }
            catch (Exception ex) { await Dialogs.AlertAsync("Não foi possível duplicar", ex.Message); break; }
            var copy = LegacyCity.DuplicateDocument(_store, doc with { Content = content });
            city.DuplicateHouse(house, copy, now);
            _contents[copy.Path] = content;
            if (LegacyCity.LinkSignature(content).Length > 0) ScheduleRoadRebuild();   // urbeCasaParaDocumento
        }
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
    }

    /// <summary>show('trash'): the Lixeira in Notas.</summary>
    public void ShowTrashView() => Explorer.ShowTrash(_trash.Items.Select(i =>
        new NotesExplorer.TrashEntry((string?)i["document"]?["id"] ?? "", (string?)i["document"]?["title"] ?? "", (string?)i["originalPath"] ?? "")).ToList());

    private bool TrashWritable(out string? problem)
    {
        problem = _vault is null || !_vault.IsConnected || _snapshot is null || _snapshot.IsReadOnly || _store is null
            ? "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para usar a lixeira."
            : _trashReadOnly ? "A lixeira deste vault é de outra versão do Urbe e foi preservada." : null;
        return problem is null;
    }

    /// <summary>
    /// Restaurar (trash.restore): the note goes back to its path with a house (urbeCasaParaDocumento). The file is
    /// created in the vault first, then the trash is rewritten without it; a document already at that path is refused.
    /// </summary>
    public async Task RestoreFromTrashAsync(string id)
    {
        if (!TrashWritable(out var problem)) { await Dialogs.AlertAsync("Lixeira", problem!); return; }
        var item = _trash.Items.FirstOrDefault(i => (string?)i["document"]?["id"] == id);
        if (item is null || World.City is not { } city) return;
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
        var doc = _trash.Restore(_store!, id)!;
        city.AddDocumentHouse(doc, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _contents[doc.Path] = doc.Content;
        // document:created → urbeCasaParaDocumento (scheduleRoadRebuild when it has links) → marcarSinc; persistence schedule()
        if (LegacyCity.LinkSignature(doc.Content).Length > 0) ScheduleRoadRebuild();
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        ShowTrashView();
        World.InvalidateVisual();
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

    private Control Fab()
    {
        var b = new Button
        {
            Width = 56,
            Height = 56,
            CornerRadius = new CornerRadius(16),
            Background = UrbeTheme.Brush(UrbeTheme.Accent),
            Content = UrbeTheme.Icon(UrbeTheme.Icons.Plus, 26, UrbeTheme.AccentInk),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 20)
        };
        ToolTip.SetTip(b, "Criar");
        b.Click += async (_, _) => await ShowCreateMenuAsync();
        return b;
    }

    private Control LoadingLabel()
    {
        _loading.Text = "Gerando o mundo…";
        _loading.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _loading.FontSize = 15;
        _loading.HorizontalAlignment = HorizontalAlignment.Center;
        _loading.VerticalAlignment = VerticalAlignment.Center;
        return _loading;
    }

    /// <summary>
    /// app.js openHouseSummary in the 1.8.4 look: "ARQUIVO" card with the house art, name,
    /// type, folder and dates, "Abrir e editar", "Copiar" and "Excluir". Floating card on wide
    /// screens, bottom sheet on phones.
    /// </summary>
    private Control HousePanel()
    {
        var label = new TextBlock { Text = "ARQUIVO", FontSize = 12, FontWeight = FontWeight.SemiBold, LetterSpacing = 1.2,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center };
        var close = new Button { Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 18, UrbeTheme.Text2), Background = Brushes.Transparent, Padding = new Thickness(6) };
        close.Click += (_, _) => World.Select(null);
        var head = new DockPanel();
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(label);

        RenderOptions.SetBitmapInterpolationMode(_houseImage, BitmapInterpolationMode.None);
        var art = new Border
        {
            Width = 92, Height = 92, CornerRadius = new CornerRadius(12),
            Background = UrbeTheme.Brush(UrbeTheme.Bg), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
            Child = _houseImage
        };
        _houseTitle.FontWeight = FontWeight.Bold;
        _houseTitle.FontSize = 18;
        _houseTitle.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _houseTitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _houseMeta.FontSize = 13;
        _houseMeta.LineHeight = 19.5;
        _houseMeta.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _houseMeta.TextWrapping = TextWrapping.Wrap;
        var info = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), Children = { _houseTitle, _houseMeta } };
        var summary = new DockPanel();
        DockPanel.SetDock(art, Dock.Left);
        summary.Children.Add(art);
        summary.Children.Add(info);

        var open = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = {
                UrbeTheme.Icon(UrbeTheme.Icons.Pencil, 18, UrbeTheme.AccentInk),
                new TextBlock { Text = "Abrir e editar", FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk) } } },
            Background = UrbeTheme.Brush(UrbeTheme.Accent),
            CornerRadius = new CornerRadius(12),
            Height = 52,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        open.Click += (_, _) =>
        {
            if (World.Selected is { } house) Editor.Open(new CityNote(house.Path, _contents.GetValueOrDefault(house.Path, "")));
        };
        Button Secondary(string text, string color)
        {
            var b = new Button
            {
                Content = text, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(color),
                Background = UrbeTheme.Brush(UrbeTheme.Surface2), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12), Height = 48,
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(b, "Chega com a pasta real do vault (UC-19/UC-25)");
            return b;
        }
        var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,*") };
        var copy = Secondary("Copiar", UrbeTheme.Text);
        var delete = Secondary("Excluir", UrbeTheme.Danger);
        ToolTip.SetTip(delete, "Mover para a lixeira");
        // deleteBtn: closeHouseSummary(); removerNota(b)
        delete.Click += async (_, _) =>
        {
            if (World.Selected is not { IsNote: true } house) return;
            World.Select(null);
            await DeleteNoteAsync(house.Path);
        };
        ToolTip.SetTip(copy, "Copiar");
        // copyBtn → v21BeginCopyPlacement (a real copy: owner's decision, ROADMAP UC-19)
        copy.Click += async (_, _) =>
        {
            if (World.Selected is not { IsNote: true } house) return;
            await BeginCopyAsync(house);
        };
        // Visual (DEC-0045, UC-41): the building's look, chosen from pictures
        var look = Secondary("Visual", UrbeTheme.Text);
        ToolTip.SetTip(look, "Mudar o visual da construção");
        look.Click += async (_, _) =>
        {
            if (World.Selected is not { IsNote: true } house) return;
            await EditLookAsync(house);
        };
        // moveBtn → escolherDestinoEMover
        var move = Secondary("Mover", UrbeTheme.Text);
        ToolTip.SetTip(move, "Mover para outra pasta");
        move.Click += async (_, _) =>
        {
            if (World.Selected is not { IsNote: true } house) return;
            await MoveHouseToFolderAsync(house);
        };
        actions.ColumnDefinitions = new ColumnDefinitions("*,8,*");
        actions.RowDefinitions = new RowDefinitions("Auto,8,Auto");
        Grid.SetColumn(move, 2);
        Grid.SetRow(copy, 2);
        Grid.SetRow(delete, 2);
        Grid.SetColumn(delete, 2);
        actions.Children.Add(look);
        actions.Children.Add(move);
        actions.Children.Add(copy);
        actions.Children.Add(delete);

        _housePanel.Child = new StackPanel { Spacing = 14, Children = { head, summary, open, actions } };
        _housePanel.Background = UrbeTheme.Brush(UrbeTheme.Surface);
        _housePanel.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _housePanel.BorderThickness = new Thickness(1);
        _housePanel.Padding = new Thickness(20, 14, 20, 20);
        _housePanel.IsVisible = false;
        return _housePanel;
    }

    private void PlaceHousePanel(bool wide)
    {
        if (wide)
        {
            _housePanel.Width = 380;
            _housePanel.CornerRadius = new CornerRadius(16);
            _housePanel.Margin = new Thickness(0, 0, 24, 24);
            _housePanel.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            _housePanel.Width = double.NaN;
            _housePanel.CornerRadius = new CornerRadius(20, 20, 0, 0);
            _housePanel.Margin = new Thickness(0);
            _housePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        _housePanel.VerticalAlignment = VerticalAlignment.Bottom;
    }

    private void ShowHouse(LegacyCityBuilding? house)
    {
        _housePanel.IsVisible = house is not null;
        if (house is null || World.City is null) return;
        var paths = World.City.RegionPaths();
        var region = house.RegionId is { } rid ? World.City.Regions.Find(r => r.Id == rid) : null;
        var folder = region is not null ? paths[region.Id] : "raiz";
        _houseTitle.Text = house.Name;
        _houseMeta.Text = $"Tipo: Nota Markdown\nPasta: {folder}\nCriado: {_today}\nEditado: {_today}";
        _houseImage.Source = World.ArtOf(house);
    }

    private void SaveEditorToMemory()
    {
        if (Editor.Current is { } edited) _contents[edited.Path] = edited.Content;
    }

    // ---------------- navigation (1.8.4 rail / dock) ----------------

    private static readonly (string Icon, string Label)[] Destinations =
        [(UrbeTheme.Icons.City, "Cidade"), (UrbeTheme.Icons.Notes, "Explorer"), (UrbeTheme.Icons.Assistant, "Assistente")];

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
        SetColumnSpan(line, 3);
        _dock.Children.Add(line);
        _dock.ColumnDefinitions = new ColumnDefinitions("*,*,*");
        for (int i = 0; i < Destinations.Length; i++)
        {
            var item = NavItem(Destinations[i].Icon, Destinations[i].Label, i == 0);
            SetColumn(item, i);
            _dock.Children.Add(item);
        }
    }

    private readonly List<(string Label, string Icon, Border Ico, TextBlock Text)> _nav = [];
    private Control? _top, _fab;

    /// <summary>The destination shown as active (.ativo) in the rail and the dock: Notas while it is open, else Cidade.</summary>
    public string ActiveDestination => Explorer.IsVisible ? "Explorer" : "Cidade";

    private void UpdateNav()
    {
        // v23Atualizar: with Notas open the top bar and the + are hidden (they show again beside a wide panel otherwise)
        if (_top is not null) _top.IsVisible = !Explorer.IsVisible;
        if (_fab is not null) _fab.IsVisible = !Explorer.IsVisible;
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
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        if (label == "Explorer")
        {
            ToolTip.SetTip(button, "Explorador de notas e pastas");
            button.Click += (_, _) => ShowNotes();
        }
        else if (label == "Cidade")
        {
            button.Click += (_, _) => { if (!Editor.IsVisible) Explorer.IsVisible = false; };
        }
        else
        {
            ToolTip.SetTip(button, "Assistente: em portabilidade");
            button.IsEnabled = false;
            // the 1.8.4 draws it like the others; no grey box for the disabled state
            button.Resources["ButtonBackgroundDisabled"] = Brushes.Transparent;
        }
        return button;
    }
}
