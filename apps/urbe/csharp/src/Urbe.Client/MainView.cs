using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The 1.8.4 screen: the city fills everything; navigation rail (wide) or dock (narrow),
/// the top pill with the biome, the round buttons, the "+" and the panels sit on top of it.
/// </summary>
public sealed class MainView : Grid
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

    private string _today = DateTime.Now.ToString("yyyy-MM-dd");

    public MainView(IUrbeVaultStorage? storage = null)
    {
        _vault = storage ?? UrbeApp.VaultStorageFactory?.Invoke();
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        RowDefinitions = new RowDefinitions("*,Auto");

        World = new WorldView();
        Explorer = new NotesExplorer { IsVisible = false };
        Editor = new EditorOverlay { IsVisible = false };
        Explorer.Closed += () => Explorer.IsVisible = false;
        Explorer.NoteRequested += note =>
        {
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
                Explorer.SetNotes(_contents.Select(n => new CityNote(n.Key, n.Value)));
                Explorer.IsVisible = true;
            }
        };
        if (_vault is not null) Editor.SaveRequested += SaveNoteAsync;

        _stage.Children.Add(World);
        _stage.Children.Add(TopBar());
        _stage.Children.Add(Fab());
        _stage.Children.Add(HousePanel());
        _stage.Children.Add(LoadingLabel());
        _stage.Children.Add(Explorer);
        _stage.Children.Add(Editor);
        SetColumn(_stage, 1);
        Children.Add(_stage);

        BuildRail();
        SetColumn(_rail, 0);
        Children.Add(_rail);
        BuildDock();
        SetRow(_dock, 1);
        SetColumnSpan(_dock, 2);
        Children.Add(_dock);

        World.CameraChanged += (_, _) => _biome.Text = World.BiomeNameAtCentre();
        World.SelectionChanged += (_, house) => ShowHouse(house);
    }

    public WorldView World { get; }
    public NotesExplorer Explorer { get; }
    public EditorOverlay Editor { get; }

    /// <summary>Open the native hierarchical vault Explorer (1.8.4 Notas), not a web panel.</summary>
    public void ShowNotes()
    {
        if (Editor.IsVisible) return;
        Explorer.SetNotes(_contents.Select(n => new CityNote(n.Key, n.Value)));
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
        _snapshot = snapshot;
        Editor.SaveOnClose = !snapshot.IsReadOnly;
        var notes = snapshot.Documents.Where(d => d.Text is not null)
            .Select(d => new CityNote(d.Path, d.Text!)).ToArray();
        await LoadAsync(notes, snapshot.Mundo ?? "urbe", snapshot);
        _vaultName.Text = snapshot.IsReadOnly ? "Vault: somente leitura" : "Pasta conectada";
    }

    private async Task SaveNoteAsync(CityNote note)
    {
        if (_vault is null || !_vault.IsConnected || _snapshot is null)
            throw new InvalidOperationException("Selecione uma pasta real para gravar.");
        if (_snapshot.IsReadOnly)
            throw new InvalidOperationException("Vault em modo somente leitura.");
        await _vault.SaveExistingNoteAsync(note.Path, note.Content);
        _contents[note.Path] = note.Content;
    }

    /// <summary>
    /// Generates the 1.8.4 world and opens the files as on the first open of a vault
    /// (LegacyCity.OpenFirstTime), off the UI thread; then frames all houses (city.fit).
    /// </summary>
    public async Task LoadAsync(IReadOnlyList<CityNote> files, string seed = "urbe", VaultSnapshot? persisted = null)
    {
        _contents.Clear();
        foreach (var f in files) _contents[f.Path] = f.Content;
        _today = DateTime.Now.ToString("yyyy-MM-dd");
        try
        {
            var (world, city) = await Task.Run(() =>
            {
                var w = new LegacyWorld(seed);
                int next = 0;
                // app.js id(): ids seed lot order and growth; deterministic here so the same files give the same city.
                var city = new LegacyCity(new LegacyCityTerrain((x, y) => w.At(x, y).Biome), prefix => prefix + (++next).ToString("x"));
                var store = city.OpenFirstTime(files.Select(f => (f.Path, f.Content)).ToList());
                if (persisted is { IsMapReadOnly: false })
                    city.RestoreSavedNotePositions(persisted.Documents, store);
                return (w, city);
            });
            World.Load(world, city);
            Explorer.SetNotes(files);
            World.FitNotes();
            if (persisted is not null && LegacySavedMapCamera.TryRead(persisted, out var camera))
            {
                World.Camera.X = camera.X;
                World.Camera.Y = camera.Y;
                World.Camera.SetZoom(camera.Zoom);
                World.InvalidateVisual();
            }
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
            Children = { RoundButton(UrbeTheme.Icons.Search, "Buscar"), RoundButton(UrbeTheme.Icons.Map, "Mapa"), RoundButton(UrbeTheme.Icons.Sliders, "Personalizar"), VaultButton() }
        };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12, 10), VerticalAlignment = VerticalAlignment.Top };
        SetColumn(buttons, 2);
        bar.Children.Add(pill);
        bar.Children.Add(buttons);
        return bar;
    }

    private Button VaultButton()
    {
        var button = RoundButton(UrbeTheme.Icons.Notes, "Cidades · selecionar pasta do vault");
        button.IsEnabled = _vault is not null;
        button.Click += async (_, _) => await OpenVaultAsync();
        return button;
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

    private static Control Fab()
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
        ToolTip.SetTip(b, "Nova nota (em portabilidade)");
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
