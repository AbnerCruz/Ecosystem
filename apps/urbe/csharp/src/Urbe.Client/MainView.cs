using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Client.City;
using Urbe.Client.World;

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
    private readonly TextBlock _loading = new();
    private List<CityNote> _notes = [];

    public MainView()
    {
        Background = UrbeTheme.Brush(UrbeTheme.Bg);
        ColumnDefinitions = new ColumnDefinitions("Auto,*");
        RowDefinitions = new RowDefinitions("*,Auto");

        World = new WorldView();
        Editor = new EditorOverlay { IsVisible = false };
        Editor.Closed += (_, _) => SaveEditorToMemory();

        _stage.Children.Add(World);
        _stage.Children.Add(TopBar());
        _stage.Children.Add(Fab());
        _stage.Children.Add(HousePanel());
        _stage.Children.Add(LoadingLabel());
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
    public EditorOverlay Editor { get; }

    /// <summary>Generates the world off the UI thread and shows the notes as a city.</summary>
    public async Task LoadAsync(IReadOnlyList<CityNote> notes, string seed = "urbe")
    {
        _notes = [.. notes];
        try
        {
            var (world, city) = await Task.Run(() =>
            {
                var w = new LegacyWorld(seed);
                return (w, CityModel.Build(_notes, w));
            });
            World.Load(world, city);
            var start = city.Houses.FirstOrDefault(h => h.Note.Name == "Comece aqui") ?? city.Houses.FirstOrDefault();
            if (start is not null) World.CenterOn(start);
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
    }

    // ---------------- city chrome ----------------

    private Control TopBar()
    {
        var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = UrbeTheme.Brush(UrbeTheme.Ok), VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = "Urbe", FontWeight = FontWeight.SemiBold, FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center };
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
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { dot, name, sep, _biome } }
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { RoundButton(UrbeTheme.Icons.Search, "Buscar"), RoundButton(UrbeTheme.Icons.Map, "Mapa"), RoundButton(UrbeTheme.Icons.Sliders, "Personalizar") }
        };
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12, 10), VerticalAlignment = VerticalAlignment.Top };
        SetColumn(buttons, 2);
        bar.Children.Add(pill);
        bar.Children.Add(buttons);
        return bar;
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

    private Control HousePanel()
    {
        _houseTitle.FontWeight = FontWeight.SemiBold;
        _houseTitle.FontSize = 15;
        _houseTitle.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
        _houseTitle.TextTrimming = TextTrimming.CharacterEllipsis;
        _houseMeta.FontSize = 12;
        _houseMeta.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _houseMeta.TextWrapping = TextWrapping.Wrap;
        var open = new Button
        {
            Content = "Abrir nota",
            Background = UrbeTheme.Brush(UrbeTheme.Accent),
            Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk),
            FontWeight = FontWeight.SemiBold,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        open.Click += (_, _) =>
        {
            if (World.Selected is { } house) Editor.Open(house.Note);
        };
        var close = new Button { Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 16), Background = Brushes.Transparent, Padding = new Thickness(6) };
        close.Click += (_, _) => World.Select(null);
        var head = new DockPanel();
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(_houseTitle);
        _housePanel.Child = new StackPanel { Spacing = 10, Children = { head, _houseMeta, open } };
        _housePanel.Background = UrbeTheme.Brush(UrbeTheme.Surface, .97);
        _housePanel.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _housePanel.BorderThickness = new Thickness(1);
        _housePanel.CornerRadius = new CornerRadius(16);
        _housePanel.Padding = new Thickness(16, 12);
        _housePanel.Width = 320;
        _housePanel.Margin = new Thickness(12, 0, 12, 92);
        _housePanel.HorizontalAlignment = HorizontalAlignment.Center;
        _housePanel.VerticalAlignment = VerticalAlignment.Bottom;
        _housePanel.IsVisible = false;
        return _housePanel;
    }

    private void ShowHouse(CityHouse? house)
    {
        _housePanel.IsVisible = house is not null;
        if (house is null) return;
        var text = house.Note.Content;
        int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        _houseTitle.Text = house.Note.Name;
        _houseMeta.Text = (house.Note.Folder.Length > 0 ? house.Note.Folder + " · " : "") + $"{words} palavras";
    }

    private void SaveEditorToMemory()
    {
        if (Editor.Current is not { } edited || World.Selected is not { } house) return;
        house.Note = edited;
        int i = _notes.FindIndex(n => n.Path == edited.Path);
        if (i >= 0) _notes[i] = edited;
        ShowHouse(house);
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

    private static Control NavItem(string icon, string label, bool active)
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
        if (!active) ToolTip.SetTip(item, label + " (em portabilidade)");
        return item;
    }
}
