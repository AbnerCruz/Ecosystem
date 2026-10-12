using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
        Explorer = new NotesExplorer { IsVisible = false };
        Editor = new EditorOverlay { IsVisible = false, Dialogs = Dialogs, Notes = () => _contents.Select(n => new CityNote(n.Key, n.Value)) };
        Explorer.Closed += () => Explorer.IsVisible = false;
        Explorer.SelectionAction += (action, paths) => _ = SelectionActionAsync(action, paths);
        Explorer.MoreRequested += () => _ = ShowNotesMenuAsync();
        Explorer.RestoreRequested += id => _ = RestoreFromTrashAsync(id);
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
            SaveEditorToMemory();
            if (_editorOpenedFromNotes)
            {
                _editorOpenedFromNotes = false;
                RefreshNotes();
                Explorer.IsVisible = true;
            }
        };
        if (_vault is not null) Editor.SaveRequested += SaveNoteAsync;
        Editor.MoreRequested += ShowEditorMenuAsync;

        _stage.Children.Add(World);
        _stage.Children.Add(TopBar());
        _stage.Children.Add(Fab());
        _stage.Children.Add(LifeNotice());
        _stage.Children.Add(ToolDock());
        _stage.Children.Add(HousePanel());
        _stage.Children.Add(LoadingLabel());
        _stage.Children.Add(CityMap);
        _stage.Children.Add(Explorer);
        _stage.Children.Add(Editor);
        Customize = new CustomizePanel(Customization, Dialogs);
        Customize.OpenFileRequested += path => Editor.Open(new CityNote(path, _contents.GetValueOrDefault(path, "")));
        Customization.Applied += ApplyCustomization;
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
        foreach (var overlay in new Control[] { Customize, Dialogs })
        {
            SetRowSpan(overlay, 2);
            SetColumnSpan(overlay, 2);
            Children.Add(overlay);
        }
        Search = new SearchFeature(this, () => _contents.Select(n => new CityNote(n.Key, n.Value)).ToList());

        World.CityVisible = () => !Editor.IsVisible && !Explorer.IsVisible;
        World.Notice += (_, text) => ShowLifeNotice(text);
        World.CameraChanged += (_, _) => _biome.Text = World.BiomeNameAtCentre();
        World.SelectionChanged += (_, house) => ShowHouse(house);
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
        Editor.SaveOnClose = !snapshot.IsReadOnly;
        var texts = snapshot.Documents.Where(d => d.Text is not null).ToArray();
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
        _vaultName.Text = snapshot.IsReadOnly ? "Vault: somente leitura" : "Pasta conectada";
        if (store is null || opened is null || !ReferenceEquals(World.City, opened)) return;
        _store = store;
        StartMapSaves(snapshot, opened);
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
        scheduler.StateChanged += (_, _) => _vaultName.Text = scheduler.State switch
        {
            "error" => "Erro ao salvar o mapa",
            "refused" => "Mapa não salvo",
            _ => "Pasta conectada"
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
                var w = new LegacyWorld(seed);
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
        _dock.IsVisible = !wide;
        PlaceHousePanel(wide);
    }

    // ---------------- city chrome ----------------

    private Control TopBar()
    {
        var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = UrbeTheme.Brush(UrbeTheme.Ok), VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = "Urbe", FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center };
        _vaultName.Text = "Tutorial · prévia";
        _vaultName.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _vaultName.FontSize = 11;
        _vaultName.VerticalAlignment = VerticalAlignment.Center;
        var sep = new TextBlock { Text = "·", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center };
        _biome.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _biome.FontSize = 14;
        _biome.VerticalAlignment = VerticalAlignment.Center;
        var pill = new Border
        {
            Background = UrbeTheme.Brush(UrbeTheme.Surface, .92),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(14, 0, 16, 0),
            Height = 42,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { dot, name, sep, _biome, _vaultName } }
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { SearchButton(), MapButton(), CustomizeButton(), VaultButton() }
        };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12, 10), VerticalAlignment = VerticalAlignment.Top };
        SetColumn(buttons, 2);
        bar.Children.Add(pill);
        bar.Children.Add(buttons);
        return bar;
    }

    private Button SearchButton()
    {
        var button = RoundButton(UrbeTheme.Icons.Search, "Buscar");
        ToolTip.SetTip(button, "Buscar notas");
        button.Click += (_, _) => Search.OpenQuickOpen();
        return button;
    }

    private Button MapButton()
    {
        var button = RoundButton(UrbeTheme.Icons.Map, "Mapa da cidade");
        ToolTip.SetTip(button, "Mapa da cidade");
        button.Click += (_, _) => ShowMap();
        return button;
    }

    private Button VaultButton()
    {
        var button = RoundButton(UrbeTheme.Icons.Notes, "Cidades · selecionar pasta do vault");
        button.IsEnabled = _vault is not null;
        button.Click += async (_, _) => await OpenVaultAsync();
        return button;
    }

    private Button CustomizeButton()
    {
        var b = RoundButton(UrbeTheme.Icons.Sliders, "Personalizar");
        ToolTip.SetTip(b, "Personalização");
        b.Click += (_, _) => Customize.Open();
        return b;
    }

    private static Button RoundButton(string icon, string tip)
    {
        var b = new Button
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = UrbeTheme.Brush(UrbeTheme.Surface, .92),
            Content = UrbeTheme.Icon(icon, 20),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        ToolTip.SetTip(b, tip + " (em portabilidade)");
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
        UrbeAppearance.Apply(this);   // Personalização: tema, texto, forma, editor e chão
        if (Customization.Config["editor"] is System.Text.Json.Nodes.JsonObject ed) Editor.ApplyOptions((string?)ed["largura"], (string?)ed["modoInicial"]);
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

    /// <summary>v23MenuCriar: the primary action of the + button.</summary>
    public Task ShowCreateMenuAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        return Dialogs.MenuAsync("Criar",
        [
            new DialogItem("Nova nota", UrbeTheme.Icons.Plus, Run: () => CreateNoteAsync(null)),
            new DialogItem("Nova pasta", UrbeTheme.Icons.Folder, Run: () => CreateFolderAsync()),
            new DialogItem("Desenhar pasta no mapa", UrbeTheme.Icons.Region, Run: BeginDrawRegionAsync),
            new DialogItem("Adicionar construção", UrbeTheme.Icons.House, Run: BeginPlacementAsync),
            new DialogItem("Importar", UrbeTheme.Icons.Upload, Run: () => ImportAsync())
        ]);
    }

    /// <summary>
    /// v21CreateFileInRegion: name dialog, the house in the neighbourhood (or loose), the file
    /// written to the vault, the camera on the house and the editor open. Nothing is created in
    /// memory only: without a writable vault folder the note is not created.
    /// </summary>
    public async Task CreateNoteAsync(LegacyCityRegion? region, string? typedName = null)
    {
        if (World.City is not { } city) return;
        if (_vault is null || !_vault.IsConnected || _snapshot is null || _snapshot.IsReadOnly)
        {
            await Dialogs.AlertAsync("Nova nota", "Abra a pasta do seu vault (botão Cidades) para criar notas. O Tutorial desta prévia é só leitura.");
            return;
        }
        var typed = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Nova nota", Label: "Nome", Placeholder: "Ex.: Ideias",
            Hint: "Sem extensão, vira .md. Também aceita .txt, .html, .css, .js, .json, .yaml e .csv.", Confirm: "Criar nota"));
        if (typed is null) return;
        // app.js has one camera: the loose lot is searched where the person is looking (centroBuscaRaiz)
        city.CameraX = World.Camera.X;
        city.CameraY = World.Camera.Y;
        city.CameraZoom = World.Camera.Zoom;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (city.CreateNote(region, typed, now) is not { } made)
        {
            await Dialogs.AlertAsync("Nova nota", "Não encontrei lote livre.");
            return;
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
            return;
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
        RefreshNotes();
        World.CenterOn(made.House);
        World.Select(made.House);
        Editor.Open(new CityNote(made.House.Path, made.Content));
    }

    /// <summary>
    /// The editor's "Mais ações" (v23 overflow → abrirMenuExplorer(nomeCompletoNota(currentFile), …)) with the
    /// actions the native app already has. The 1.8.4 also lists Assistente, Mostrar nas notas, Versões
    /// anteriores, Estrutura e backlinks and Mover para a lixeira; they join as their screens are ported.
    /// </summary>
    private Task ShowEditorMenuAsync(EditorOverlay editor)
    {
        if (Dialogs.IsOpen || editor.Current is not { } note) return Task.CompletedTask;
        var house = World.City?.Buildings.Find(b => b.IsNote && b.Path == note.Path);
        var title = house is not null ? LegacyCity.FullNoteName(house) : System.IO.Path.GetFileName(note.Path);
        return Dialogs.MenuAsync(title,
        [
            new DialogItem("Renomear", UrbeTheme.Icons.Pencil, Run: () => RenameNoteAsync(note.Path)),
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
            await Dialogs.AlertAsync("Renomear", "Abra a pasta do seu vault (botão Cidades) para renomear notas. O Tutorial desta prévia é só leitura.");
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
        if (_store?.Get(path) is { } doc && !string.Equals(doc.Path, plan.Path, StringComparison.Ordinal))
            LegacyCity.MoveDocument(_store, doc, plan.Path);
        // aplicarNomeCompletoNota: buildTree, agendarSalvar + marcarSinc, pedirDesenho; v21RefreshEditorChrome
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
            await Dialogs.AlertAsync("Excluir", "Abra a pasta do seu vault (botão Cidades) para excluir notas. O Tutorial desta prévia é só leitura.");
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
        // removerNota: marcarIndice, indexar, scheduleRoadRebuild, counts, buildTree, agendarSalvar + marcarSinc
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
        return true;
    }

    /// <summary>Notas shows the notes and, like model.folders, every neighbourhood's folder even without notes.</summary>
    private void RefreshNotes() =>
        Explorer.SetNotes(_contents.Select(n => new CityNote(n.Key, n.Value)), World.City?.RegionPaths().Values);

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
    /// mobile-ui.js moreMenu ("Notas") with the items the native app already has: Lixeira (n item(ns)). Recentes,
    /// Favoritos, Páginas, Composições, Importar, Exportar and the device folder join as their screens are ported.
    /// </summary>
    public Task ShowNotesMenuAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        int n = _trash.Items.Count;
        return Dialogs.MenuAsync("Notas",
        [
            new DialogItem("Recentes", UrbeTheme.Icons.Clock, Run: () => { ShowCollection("recent"); return Task.CompletedTask; }),
            new DialogItem("Favoritos", UrbeTheme.Icons.Star, Run: () => { ShowCollection("favorites"); return Task.CompletedTask; }),
            new DialogItem("Lixeira", UrbeTheme.Icons.Trash, Detail: (n > 0 ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Nenhum") + " item(ns)",
                Run: () => { ShowTrashView(); return Task.CompletedTask; })
        ]);
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
        var folders = NoteFolders();
        var options = new List<DialogItem> { new("Raiz", UrbeTheme.Icons.Notes, "Fora de qualquer pasta") };
        options.AddRange(folders.Select(f => new DialogItem(f[(f.LastIndexOf('/') + 1)..], UrbeTheme.Icons.Folder, f)));
        if (await Dialogs.ChooseAsync("Mover para", options) is not { } chosen) return;
        var folder = chosen == 0 ? "" : folders[chosen - 1];
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
            if (Editor.IsVisible && Editor.Current?.Path == from) Editor.Renamed(to);
        }
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
            ? "Abra a pasta do seu vault (botão Cidades) para usar a lixeira."
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
        // document:created → urbeCasaParaDocumento → marcarSinc; persistence schedule()
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
        Grid.SetColumn(delete, 2);
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
        [(UrbeTheme.Icons.City, "Cidade"), (UrbeTheme.Icons.Notes, "Notas"), (UrbeTheme.Icons.Assistant, "Assistente")];

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
        _dock.Background = UrbeTheme.Brush(UrbeTheme.Bg);
        _dock.Height = 64;
        _dock.ColumnDefinitions = new ColumnDefinitions("*,*,*");
        for (int i = 0; i < Destinations.Length; i++)
        {
            var item = NavItem(Destinations[i].Icon, Destinations[i].Label, i == 0);
            SetColumn(item, i);
            _dock.Children.Add(item);
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
            Child = UrbeTheme.Icon(icon, 18, active ? UrbeTheme.Accent : UrbeTheme.Text3),
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
        var button = new Button
        {
            Content = item,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(0)
        };
        if (label == "Notas")
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
        }
        return button;
    }
}
