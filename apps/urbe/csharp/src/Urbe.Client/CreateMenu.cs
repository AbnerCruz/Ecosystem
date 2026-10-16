using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The rest of the "Criar" (+) menu (app.js v23MenuCriar): Nova pasta, Desenhar pasta no mapa,
/// Adicionar construção and Importar. The rules live in Urbe.Core (LegacyCityCreate, ImportPipeline);
/// this file only asks, writes to the vault and draws the tool dock. Nothing is kept in memory only:
/// without a writable vault folder these actions refuse, like "Nova nota".
/// </summary>
public sealed partial class MainView
{
    private const string NoVaultMessage = "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para criar pastas, construções e importar. O Tutorial desta prévia é só leitura.";

    private readonly Border _toolDock = new();
    private readonly TextBlock _toolText = new();
    private readonly Button _toolOk = new();
    // UC-53: the decoration tool's own buttons (variation, turn/mirror, brush), shown only while placing new decorations
    private readonly Button _toolVariant = new();
    private readonly Button _toolTurn = new();
    private readonly Button _toolBrush = new();
    private readonly StackPanel _toolExtras = new() { Orientation = Orientation.Horizontal, Spacing = 8, IsVisible = false };

    /// <summary>Replaces the system picker (tests, other platforms): true = a whole folder, false = files.</summary>
    public Func<bool, Task<ImportSelection?>>? ImportPicker { get; set; }

    /// <summary>True while a map tool (draw a region / place a building) owns the one-finger drag.</summary>
    public bool ToolActive => World.Tool is not null;
    public string ToolDockText => _toolText.Text ?? "";

    private bool VaultWritable => _vault is { IsConnected: true } && _snapshot is { IsReadOnly: false };

    // ------------------------------------------------------------------ the dock of a tool (#v21PlacementDock)

    private Control ToolDock()
    {
        _toolText.FontFamily = UrbeTheme.UiFont;
        _toolText.FontSize = 13;
        _toolText.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _toolText.VerticalAlignment = VerticalAlignment.Center;
        _toolText.TextTrimming = TextTrimming.CharacterEllipsis;
        var cancel = new Button { Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 18, UrbeTheme.Text2), Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Surface3) };
        ToolTip.SetTip(cancel, "Cancelar");
        cancel.Click += (_, _) => EndTool();
        _toolOk.Content = "Construir";
        _toolOk.Height = 40;
        _toolOk.Padding = new Thickness(14, 0);
        _toolOk.CornerRadius = new CornerRadius(12);
        _toolOk.Background = UrbeTheme.Brush(UrbeTheme.Accent);
        _toolOk.Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk);
        _toolOk.IsVisible = false;
        _toolOk.Click += async (_, _) => await ConfirmPlacementAsync();
        foreach (var b in new[] { _toolVariant, _toolTurn, _toolBrush })
        {
            b.Height = 40;
            b.Padding = new Thickness(10, 0);
            b.CornerRadius = new CornerRadius(12);
            b.Background = UrbeTheme.Brush(UrbeTheme.Surface3);
            b.Foreground = UrbeTheme.Brush(UrbeTheme.Text);
            b.IsVisible = false;
        }
        _toolVariant.Content = "Variação";
        _toolBrush.Content = "Pincel";
        ToolTip.SetTip(_toolVariant, "Próxima variação desta decoração");
        ToolTip.SetTip(_toolBrush, "Arraste para colocar uma em cada lote do caminho");
        _toolVariant.Click += (_, _) => CycleToolVariant();
        _toolTurn.Click += (_, _) => TurnToolDecoration();
        _toolBrush.Click += (_, _) => ToggleBrush();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _toolText, _toolOk, cancel } };
        // a second row, so the dock still fits a phone's width
        _toolExtras.Children.AddRange([_toolVariant, _toolTurn, _toolBrush]);
        _toolDock.Child = new StackPanel { Spacing = 8, Children = { row, _toolExtras } };
        _toolDock.Background = UrbeTheme.Brush(UrbeTheme.Surface2);
        _toolDock.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _toolDock.BorderThickness = new Thickness(1);
        _toolDock.CornerRadius = new CornerRadius(16);
        _toolDock.Padding = new Thickness(14, 8, 8, 8);
        _toolDock.HorizontalAlignment = HorizontalAlignment.Center;
        _toolDock.VerticalAlignment = VerticalAlignment.Bottom;
        _toolDock.Margin = new Thickness(12, 0, 12, 14);
        _toolDock.MaxWidth = 520;
        _toolDock.IsVisible = false;
        return _toolDock;
    }

    private void BeginTool(MapTool tool, string text, bool okVisible)
    {
        // v23Navegar('mundo'): back to the city, panels away
        Explorer.IsVisible = false;
        CityMap.IsVisible = false;
        World.Select(null);
        World.Tool = tool;
        _copySource = null;
        _toolOk.Content = "Construir";
        _toolText.Text = text;
        _toolOk.IsVisible = okVisible;
        _toolOk.IsEnabled = false;
        _toolVariant.IsVisible = _toolTurn.IsVisible = _toolBrush.IsVisible = _toolExtras.IsVisible = false;
        _toolDock.IsVisible = true;
    }

    /// <summary>setTool('select'): the normal city again.</summary>
    public void EndTool()
    {
        _movingDecoration = null;
        World.Tool = null;
        _toolDock.IsVisible = false;
        _toolOk.IsVisible = false;
        _toolVariant.IsVisible = _toolTurn.IsVisible = _toolBrush.IsVisible = _toolExtras.IsVisible = false;
        _toolOk.Content = "Construir";
        _copySource = null;
        _placementExt = ".md";
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\.(md|markdown|txt|html|htm|js|mjs|css|json|yaml|yml|csv)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex TextExtension();

    // ------------------------------------------------------------------ Nova pasta

    /// <summary>criarPastaNoDestino (v21): name dialog, the neighbourhood on the map, the folder in the vault.</summary>
    public async Task CreateFolderAsync(string? parentId = null)
    {
        if (World.City is not { } city) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Nova pasta", NoVaultMessage);
            return;
        }
        var typed = await Dialogs.PromptAsync(new PromptOptions("Nova pasta", Label: "Nome da pasta", Placeholder: "Ex.: Projetos",
            Hint: "Cada pasta vira uma região da cidade.", Confirm: "Criar pasta"));
        if (typed is null || typed.Trim().Length == 0) return;
        if (city.CreateFolder(typed, parentId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) is not { } region)
        {
            ShowLifeNotice("Não encontrei área livre.");
            return;
        }
        if (!await WriteFolderAsync(city, region, undo: () => city.Regions.Remove(region))) return;
        World.InvalidateVisual();
        ShowLifeNotice("Pasta criada.");
    }

    /// <summary>Makes the region's folder in the vault; on failure the region is taken back.</summary>
    private async Task<bool> WriteFolderAsync(LegacyCity city, LegacyCityRegion region, Action undo)
    {
        try
        {
            await _vault!.CreateDirectoryAsync(city.RegionPaths()[region.Id]);
            RefreshNotes(); // model.addFolder: the folder is in Notas even before it has a note
            return true;
        }
        catch (Exception ex)
        {
            undo();
            World.InvalidateVisual();
            await Dialogs.AlertAsync("Não foi possível criar a pasta", ex.Message);
            return false;
        }
    }

    // ------------------------------------------------------------------ Desenhar pasta no mapa

    /// <summary>tool 'region': "Arraste no mapa para delimitar a nova pasta."</summary>
    public async Task BeginDrawRegionAsync()
    {
        if (World.City is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Desenhar pasta no mapa", NoVaultMessage);
            return;
        }
        BeginTool(new RegionDrawTool((x, y, w, h) => _ = FinishDrawnRegionAsync(x, y, w, h)),
            "Arraste no mapa para delimitar a nova pasta.", okVisible: false);
    }

    private async Task FinishDrawnRegionAsync(int x, int y, int w, int h)
    {
        if (World.City is not { } city || Dialogs.IsOpen) return;
        if (w < 3 || h < 3)
        {
            ShowLifeNotice("A região precisa ser maior.");
            return;
        }
        // "Nova região": the 1.8.4 dialog opens blank; the name is made unique on confirm
        var typed = await Dialogs.PromptAsync(new PromptOptions("Nova região", Label: "Nome", Confirm: "Criar", Required: false));
        if (typed is null) return;
        if (city.CreateDrawnRegion(typed, "", city.NextRegionColor(), x, y, w, h) is not { } drawn)
        {
            ShowLifeNotice("Não há terreno disponível nesta seleção.");
            return;
        }
        if (!await WriteFolderAsync(city, drawn.Region, drawn.Undo)) return;
        EndTool();
        // absorverConstrucoesDaRaiz: the root notes under the new region join its folder, their files moved
        var adopted = city.RootBuildingsUnder(drawn.Region).Where(b => b.IsNote).Select(b => b.Path).ToList();
        if (adopted.Count > 0) await MoveNotesToAsync(adopted, city.RegionPaths()[drawn.Region.Id]);
        World.InvalidateVisual();
        ShowLifeNotice("Região criada com prioridade territorial" + (adopted.Count > 0 ? $" · {adopted.Count} item(ns) da raiz entraram nela." : "."));
    }

    // ------------------------------------------------------------------ Adicionar construção

    /// <summary>The types "+" → Construção offers (DEC-0045, UC-41: "as construções dependem da extensão", .md by default):
    /// the text files the 1.8.4 builds, each with the trade it gets in the city.</summary>
    public static IReadOnlyList<(string Ext, string Label)> BuildingTypes { get; } =
    [
        (".md", "Nota .md"), (".txt", "Texto .txt"), (".html", "Página .html"), (".js", "Script .js"),
        (".css", "Estilo .css"), (".json", "Dados .json"), (".csv", "Tabela .csv")
    ];

    /// <summary>The extension of the building being placed (the type chosen in "+" → Construção).</summary>
    private string _placementExt = ".md";

    /// <summary>"+" → Construção: the type (its building shown; Nota .md first), then the placement.</summary>
    public async Task BeginBuildingAsync()
    {
        if (World.City is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Adicionar construção", NoVaultMessage);
            return;
        }
        var options = BuildingTypes.Select(t => (t.Label, (Avalonia.Media.IImage?)World.KindArt(WorldView.TradeOf(t.Ext)))).ToList();
        if (await Dialogs.PickArtAsync("Construção", options, 0, "O tipo do arquivo define a construção.") is not { } i) return;
        await BeginPlacementAsync(BuildingTypes[i].Ext);
    }

    /// <summary>v21BeginNewPlacement: tap the map to position the new building, then "Construir".</summary>
    public async Task BeginPlacementAsync() => await BeginPlacementAsync(".md");

    /// <summary>The placement of a new building whose file gets <paramref name="ext"/> when its name has no extension.</summary>
    public async Task BeginPlacementAsync(string ext)
    {
        if (World.City is not { } city) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Adicionar construção", NoVaultMessage);
            return;
        }
        var tool = new PlacementTool(city.CanHouse, c =>
        {
            _toolOk.IsEnabled = c is { } p && city.CanHouse(p.X, p.Y);
            _toolText.Text = "Posicione a construção" + (c is { } q ? $" · {q.X},{q.Y}" : " · toque no mapa");
        }) { GhostKind = WorldView.TradeOf(ext) };
        BeginTool(tool, "Posicione a construção · toque no mapa", okVisible: true);
        _placementExt = ext;
    }

    /// <summary>The house being copied while the placement tool is up (v21Placement = {kind:'copy', source}).</summary>
    private LegacyCityBuilding? _copySource;

    /// <summary>
    /// The house panel's Copiar (v21BeginCopyPlacement): the panel closes and a tap on the map positions the copy, then
    /// ✓ asks "Configurar cópia". Unlike the 1.8.4 (whose Copiar writes no copy), the copy is a real note: a new file
    /// with the source's text (owner's decision, ROADMAP UC-19).
    /// </summary>
    public async Task BeginCopyAsync(LegacyCityBuilding source)
    {
        if (World.City is not { } city || !source.IsNote) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Copiar", NoVaultMessage);
            return;
        }
        var tool = new PlacementTool(city.CanHouse, c =>
        {
            _toolOk.IsEnabled = c is { } p && city.CanHouse(p.X, p.Y);
            _toolText.Text = "Posicione a cópia" + (c is { } q ? $" · {q.X},{q.Y}" : " · toque no mapa");
        });
        BeginTool(tool, "Posicione a cópia · toque no mapa", okVisible: true);
        _copySource = source;
        _toolOk.Content = "Copiar";
        ShowLifeNotice("Toque no mapa para posicionar a cópia.");
    }

    /// <summary>v21ConfirmPlacement (copy): "Configurar cópia", then the new file, its document and the house.</summary>
    private async Task ConfirmCopyAsync(LegacyCityBuilding source, (int X, int Y) p)
    {
        if (World.City is not { } city) return;
        var typed = await Dialogs.PromptAsync(new PromptOptions("Configurar cópia", Label: "Nome", Placeholder: "Ex.: Ideias",
            Hint: "Sem extensão, vira .md. Também aceita .txt, .html, .css, .js, .json, .yaml e .csv.", Confirm: "Copiar"));
        if (typed is null) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (city.CopyNoteAt(source, p.X, p.Y, typed, now) is not { } copy)
        {
            ShowLifeNotice("Posição inválida.");
            return;
        }
        var content = _contents.GetValueOrDefault(source.Path, "");
        if (Editor.IsVisible && Editor.Current is { } open && open.Path == source.Path) content = open.Content;
        try
        {
            await _vault!.CreateTextFileAsync(copy.Path, content);
        }
        catch (Exception ex)
        {
            city.RemoveBuilding(copy);
            World.InvalidateVisual();
            await Dialogs.AlertAsync("Não foi possível copiar", ex.Message);
            return;
        }
        RegisterNewNote(copy, content, now);
        EndTool();
        World.Select(copy);
        ShowLifeNotice("Cópia posicionada.");
    }

    /// <summary>
    /// A note house whose file was just written gets its document, as urbeReconcileWorld does on the next save
    /// ({path, content, created, modified} = the house's nowDate(), 'world.create'), and the map is saved with it
    /// (agendarSalvar + marcarSinc). Without this the next map save would be refused (a house without document).
    /// </summary>
    private void RegisterNewNote(LegacyCityBuilding house, string content, long nowMs)
    {
        _contents[house.Path] = content;
        if (_store is not null)
        {
            var today = LegacyCity.NowDate(nowMs);
            _store.Upsert(new DocumentInput { Path = house.Path, Content = content, Created = today, Modified = today }, "world.create");
            _mapSave?.Schedule();
            _mapSave?.MarkSync();
        }
        ScheduleRoadRebuild();   // v21ConfirmPlacement (copy and new): scheduleRoadRebuild
        RefreshNotes();
    }

    /// <summary>v21ConfirmPlacement (new): "Configurar arquivo", the house at the tile, its file in the vault.</summary>
    // ------------------------------------------------------------------ Decoração (DEC-0045, UC-41)

    /// <summary>Whether a decoration of <paramref name="kind"/> fits with its corner at (x, y): land, no building, street or
    /// other decoration under any of its tiles.</summary>
    public bool CanDecorate(WorldDecorations.Kind kind, int x, int y) =>
        World.City is { } city && WorldDecorations.Footprint(kind, x, y).All(t =>
            !city.IsWater(t.X, t.Y) && city.BuildingAt(t.X, t.Y) is null && !city.HasRoad(t.X, t.Y) &&
            (World.Prefs.DecorationAt(t.X, t.Y) is not { } there || there.Id == _movingDecoration?.Id));

    /// <summary>The decoration being moved (its own tiles do not block its new place), or null when placing new ones.</summary>
    private WorldDecoration? _movingDecoration;

    /// <summary>"+" → Decoração: which one, then a tap on the map places its ghost and ✓ puts it there (.urbe/mundo.json).</summary>
    public async Task BeginDecorationAsync()
    {
        if (World.City is null) return;
        if (!VaultWritable || Customization.WorldWriter is null)
        {
            await Dialogs.AlertAsync("Decoração", NoVaultMessage);
            return;
        }
        var kinds = WorldDecorations.Kinds;
        if (await Dialogs.ChooseAsync("Decoração", kinds.Select(k => new DialogItem(k.Label, UrbeTheme.Icons.Flower, k.Hint)).ToList()) is not { } i) return;
        BeginDecoration(kinds[i]);
    }

    /// <summary>The look last chosen for each kind (UC-53): new decorations of that kind are placed with it.</summary>
    private readonly Dictionary<string, (int Variant, bool Flipped, bool Turned)> _decorationLooks = new(StringComparer.Ordinal);

    /// <summary>The look new decorations of <paramref name="kind"/> get (the last chosen one; the default at first).</summary>
    public (int Variant, bool Flipped, bool Turned) DecorationLook(WorldDecorations.Kind kind) =>
        _decorationLooks.TryGetValue(kind.Id, out var look) ? look : (0, false, false);

    /// <summary>The placement of a decoration of <paramref name="kind"/> (tests and the menu).</summary>
    public void BeginDecoration(WorldDecorations.Kind kind)
    {
        var moving = _movingDecoration;
        var look = moving is { } m ? (m.Variant, m.Flipped, m.Turned) : DecorationLook(kind);
        var tool = new DecorationTool(kind, (x, y) => CanDecorate(kind, x, y), c =>
        {
            _toolOk.IsEnabled = c is { } p && CanDecorate(kind, p.X, p.Y);
            _toolText.Text = DecorationToolLabel(kind) + (c is { } q ? $" · {q.X},{q.Y}" : " · toque no mapa");
        })
        { Variant = look.Item1, Flipped = look.Item2, Turned = look.Item3, Painted = stroke => _ = PaintDecorationsAsync(stroke) };
        BeginTool(tool, DecorationToolLabel(kind) + " · toque no mapa", okVisible: true);
        _toolOk.Content = moving is null ? "Colocar" : "Mover";
        if (moving is null)
        {
            _toolVariant.IsVisible = kind.Variations.Count > 1;
            _toolTurn.IsVisible = kind.Turns || kind.Flips;
            _toolTurn.Content = kind.Turns ? "Girar" : "Espelhar";
            _toolBrush.IsVisible = true;
            _toolBrush.Content = "Pincel";
            _toolExtras.IsVisible = true;
        }
        _toolText.Text = DecorationToolLabel(kind) + " · toque no mapa";
    }

    /// <summary>The kind's name with the variation about to be placed (when it has more than one).</summary>
    private string DecorationToolLabel(WorldDecorations.Kind kind)
    {
        int v = World.Tool is DecorationTool t && t.Kind == kind ? t.Variant : DecorationLook(kind).Variant;
        return kind.Variations.Count > 1 ? $"{kind.Label} ({kind.Variations[WorldDecorations.VariantOf(kind, v)].Label})" : kind.Label;
    }

    /// <summary>Variação in the dock: the next variation for the decorations about to be placed.</summary>
    public void CycleToolVariant()
    {
        if (World.Tool is not DecorationTool tool || tool.Kind.Variations.Count < 2) return;
        tool.Variant = (WorldDecorations.VariantOf(tool.Kind, tool.Variant) + 1) % tool.Kind.Variations.Count;
        RememberLook(tool);
        _toolText.Text = DecorationToolLabel(tool.Kind) + (tool.Brush ? " · arraste para pintar" : " · toque no mapa");
        World.InvalidateVisual();
    }

    /// <summary>Girar (fence) or Espelhar (cart, sign…) in the dock, for the decorations about to be placed.</summary>
    public void TurnToolDecoration()
    {
        if (World.Tool is not DecorationTool tool) return;
        if (tool.Kind.Turns) tool.Turned = !tool.Turned;
        else if (tool.Kind.Flips) tool.Flipped = !tool.Flipped;
        else return;
        RememberLook(tool);
        World.InvalidateVisual();
    }

    /// <summary>Pincel in the dock: a drag places one per tile along the stroke; again, back to one tap each.</summary>
    public void ToggleBrush()
    {
        if (World.Tool is not DecorationTool tool || _movingDecoration is not null) return;
        tool.Brush = !tool.Brush;
        tool.Clear();
        _toolBrush.Content = tool.Brush ? "Toque" : "Pincel";
        _toolOk.IsVisible = !tool.Brush;
        _toolOk.IsEnabled = false;
        _toolText.Text = DecorationToolLabel(tool.Kind) + (tool.Brush ? " · arraste para pintar" : " · toque no mapa");
        World.InvalidateVisual();
    }

    private void RememberLook(DecorationTool tool) => _decorationLooks[tool.Kind.Id] = (tool.Variant, tool.Flipped, tool.Turned);

    /// <summary>The brush's stroke: one decoration per free tile, with the single placement's rule, saved at once.</summary>
    public async Task PaintDecorationsAsync(IReadOnlyList<(int X, int Y)> stroke)
    {
        if (World.Tool is not DecorationTool tool || stroke.Count == 0) return;
        var kind = tool.Kind;
        // a fence follows the stroke: turned when it runs more north-south than east-west
        bool turned = kind.Turns && stroke.Count > 1
            ? Math.Abs(stroke[^1].Y - stroke[0].Y) > Math.Abs(stroke[^1].X - stroke[0].X) : tool.Turned;
        var prefs = Customization.World;
        var next = prefs.Paint(kind, stroke, (x, y) => CanDecorate(kind, x, y), out var placed, tool.Variant, tool.Flipped, turned);
        if (placed.Count == 0) { ShowLifeNotice("Nenhum lugar livre no caminho."); return; }
        try { await Customization.SetWorldAsync(next); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException)
        {
            await Dialogs.AlertAsync("Não foi possível colocar as decorações", ex.Message);
            return;
        }
        World.InvalidateVisual();
        _toolText.Text = $"{kind.Label}: {placed.Count} colocad{(placed.Count == 1 ? "a" : "as")} · arraste para mais";
    }

    private async Task ConfirmDecorationAsync(DecorationTool tool)
    {
        if (tool.Candidate is not { } p || !CanDecorate(tool.Kind, p.X, p.Y)) { ShowLifeNotice("Posição inválida."); return; }
        var prefs = Customization.World;
        var moving = _movingDecoration;
        var placed = moving is null
            ? new WorldDecoration(prefs.NextDecorationId(), tool.Kind.Id, p.X, p.Y, WorldDecorations.VariantOf(tool.Kind, tool.Variant), tool.Flipped && tool.Kind.Flips, tool.Turned && tool.Kind.Turns)
            : moving with { X = p.X, Y = p.Y };
        var list = moving is null ? [.. prefs.Decorations, placed] : prefs.Decorations.Select(d => d.Id == moving.Id ? placed : d).ToList();
        try { await Customization.SetWorldAsync(prefs with { Decorations = list }); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException)
        {
            await Dialogs.AlertAsync("Não foi possível colocar a decoração", ex.Message);
            return;
        }
        World.InvalidateVisual();
        if (moving is not null) { _movingDecoration = null; EndTool(); return; }
        // UC-53: the tool stays, to place more of the same kind with one tap each; ✕ finishes
        tool.Clear();
        _toolOk.IsEnabled = false;
        _toolText.Text = tool.Kind.Label + " colocado · toque para outro";
    }

    /// <summary>A tap on a decoration: what it is, and Remover.</summary>
    private Task ShowDecorationAsync(WorldDecoration decoration)
    {
        if (Dialogs.IsOpen || World.Tool is not null || WorldDecorations.Find(decoration.Kind) is not { } kind) return Task.CompletedTask;
        var items = new List<DialogItem>();
        if (VaultWritable && Customization.WorldWriter is not null)
        {
            items.Add(new("Mover", UrbeTheme.Icons.Move, "Toque no novo lugar e confirme.", Run: () =>
            {
                _movingDecoration = decoration;
                BeginDecoration(kind);
                return Task.CompletedTask;
            }));
            if (kind.Variations.Count > 1)
                items.Add(new("Variação", UrbeTheme.Icons.Brush, kind.Variations[WorldDecorations.VariantOf(kind, decoration.Variant)].Label,
                    Run: () => ChooseDecorationVariantAsync(decoration)));
            if (kind.Turns)
                items.Add(new("Girar", UrbeTheme.Icons.Restore, decoration.Turned ? "De norte a sul" : "De leste a oeste",
                    Run: () => ChangeDecorationAsync(decoration with { Turned = !decoration.Turned })));
            else if (kind.Flips)
                items.Add(new("Espelhar", UrbeTheme.Icons.Restore, "Virada para o outro lado.",
                    Run: () => ChangeDecorationAsync(decoration with { Flipped = !decoration.Flipped })));
            items.Add(new("Colocar mais desta", UrbeTheme.Icons.Plus, "A mesma decoração, um toque para cada (ou o Pincel).", Run: () =>
            {
                _movingDecoration = null;
                _decorationLooks[kind.Id] = (decoration.Variant, decoration.Flipped, decoration.Turned);
                BeginDecoration(kind);
                return Task.CompletedTask;
            }));
            items.Add(new("Remover decoração", UrbeTheme.Icons.Trash, Danger: true, Run: () => RemoveDecorationAsync(decoration)));
        }
        return Dialogs.MenuAsync(kind.Label, items, kind.Hint);
    }

    /// <summary>Variação on a decoration: the list of its kind's variations; the one chosen is applied and kept for the next ones.</summary>
    public async Task ChooseDecorationVariantAsync(WorldDecoration decoration)
    {
        if (WorldDecorations.Find(decoration.Kind) is not { } kind) return;
        int current = WorldDecorations.VariantOf(kind, decoration.Variant);
        var options = kind.Variations.Select((v, i) => new DialogItem(v.Label + (i == current ? "  ✓" : ""), UrbeTheme.Icons.Flower)).ToList();
        if (await Dialogs.ChooseAsync("Variação · " + kind.Label, options) is not { } chosen || chosen == current) return;
        await ChangeDecorationAsync(decoration with { Variant = chosen });
    }

    /// <summary>A decoration with a new look (same id and place), saved; its look becomes the one for new ones of its kind.</summary>
    public async Task ChangeDecorationAsync(WorldDecoration changed)
    {
        var prefs = Customization.World;
        if (prefs.Decorations.All(d => d.Id != changed.Id)) return;
        _decorationLooks[changed.Kind] = (changed.Variant, changed.Flipped, changed.Turned);
        try { await Customization.SetWorldAsync(prefs with { Decorations = prefs.Decorations.Select(d => d.Id == changed.Id ? changed : d).ToList() }); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException)
        {
            await Dialogs.AlertAsync("Não foi possível mudar a decoração", ex.Message);
            return;
        }
        World.InvalidateVisual();
    }

    public async Task RemoveDecorationAsync(WorldDecoration decoration)
    {
        var prefs = Customization.World;
        await Customization.SetWorldAsync(prefs with { Decorations = prefs.Decorations.Where(d => d.Id != decoration.Id).ToList() });
        World.InvalidateVisual();
    }

    public async Task ConfirmPlacementAsync()
    {
        if (World.Tool is DecorationTool decorating) { await ConfirmDecorationAsync(decorating); return; }
        if (World.Tool is RegionShapeTool shaping) { await ApplyRegionShapeAsync(shaping); return; }
        if (World.City is not { } city || World.Tool is not PlacementTool { Candidate: { } p } || Dialogs.IsOpen) return;
        if (_copySource is { } source)
        {
            if (!city.CanHouse(p.X, p.Y)) { ShowLifeNotice("Posição inválida."); return; }
            await ConfirmCopyAsync(source, p);
            return;
        }
        if (!city.CanHouse(p.X, p.Y))
        {
            ShowLifeNotice("Posição inválida.");
            return;
        }
        var ext = _placementExt;
        var typed = await Dialogs.PromptAsync(new PromptOptions("Configurar arquivo", Label: "Nome", Placeholder: "Ex.: Ideias",
            Hint: $"Sem extensão, vira {ext}. Também aceita .md, .txt, .html, .css, .js, .json, .yaml e .csv.", Confirm: "Construir"));
        if (typed is null) return;
        if (ext != ".md" && !TextExtension().IsMatch(typed.Trim())) typed = typed.Trim() + ext;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (city.CreateNoteAt(p.X, p.Y, typed, now) is not { } made)
        {
            ShowLifeNotice("Posição inválida.");
            return;
        }
        try
        {
            await _vault!.CreateTextFileAsync(made.House.Path, made.Content);
        }
        catch (Exception ex)
        {
            city.RemoveBuilding(made.House);
            World.InvalidateVisual();
            await Dialogs.AlertAsync("Não foi possível criar a nota", ex.Message);
            return;
        }
        RegisterNewNote(made.House, made.Content, now);
        EndTool();
        World.Select(made.House);
        ShowLifeNotice("Arquivo construído.");
    }

    // ------------------------------------------------------------------ Importar

    private bool _importBusy;

    /// <summary>v21ImportPicker + importarEntradasDestino for documents: choose files or a folder, check,
    /// confirm, then every new text document is written (and verified) into the vault.</summary>
    public async Task ImportAsync(string? regionId = null)
    {
        if (World.City is not { } city) return;
        if (_importBusy)
        {
            ShowLifeNotice("Uma importação está em andamento. Aguarde o resultado.");
            return;
        }
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Importar", NoVaultMessage);
            return;
        }
        _importBusy = true;
        try
        {
            var mode = await Dialogs.ChooseAsync("O que deseja importar?",
            [
                new DialogItem("Arquivos", UrbeTheme.Icons.Upload, "Selecione um ou vários arquivos. O Urbe identifica automaticamente o conteúdo."),
                new DialogItem("Pasta inteira", UrbeTheme.Icons.Folder, "Selecione um vault ou uma pasta com seus documentos.")
            ]);
            if (mode is null) return;
            ImportSelection? selection;
            try { selection = await PickImportAsync(mode == 1); }
            catch (Exception ex)
            {
                await Dialogs.AlertAsync("Não foi possível abrir a seleção", ex.Message);
                return;
            }
            if (selection is null) return;
            await ImportSelectionAsync(city, selection, regionId);
        }
        finally { _importBusy = false; }
    }

    /// <summary>The checked selection into the vault (everything after the system picker).</summary>
    public async Task ImportSelectionAsync(LegacyCity city, ImportSelection selection, string? regionId)
    {
        if (selection.Sources.Count == 0)
        {
            await Dialogs.AlertAsync("Nenhum arquivo encontrado", "A seleção está vazia. Escolha uma pasta com documentos ou um backup.");
            return;
        }
        ImportInspection inspection;
        try
        {
            ShowLifeNotice("Conferindo os arquivos…");
            inspection = await ImportPipeline.InspectAsync(selection);
            if (inspection.SourceConflicts is { Count: > 0 } conflicts)
            {
                var chosen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var conflict in conflicts)
                {
                    var pick = await Dialogs.ChooseAsync("Arquivos com o mesmo nome",
                        conflict.Candidates.Select((c, i) => new DialogItem("Versão " + (i + 1) + " — " + c.Path, Detail: c.Bytes.Length + " bytes")).ToList(),
                        conflict.Path + " tem versões diferentes na seleção. Escolha qual importar ou cancele para selecionar novamente.");
                    if (pick is null)
                    {
                        ShowLifeNotice("Importação cancelada.");
                        return;
                    }
                    chosen[conflict.Path] = pick.Value;
                }
                inspection = ImportPipeline.ResolveSources(inspection, chosen);
            }
        }
        catch (InvalidDataException ex)
        {
            await Dialogs.AlertAsync("Não foi possível importar", ex.Message);
            return;
        }
        if (inspection.Kind != ImportKind.Documents)
        {
            await RestoreSelectionAsync(inspection);
            return;
        }

        var plan = ImportDocuments.Plan(inspection, _contents, regionId is not null ? city.RegionPaths().GetValueOrDefault(regionId) : null);
        var description = $"{inspection.DocumentCount} documento(s) e {inspection.AssetCount} arquivo(s). Os documentos serão incorporados ao vault atual, mantendo suas pastas e referências."
            + (plan.Skipped.Count > 0 ? $" {plan.Skipped.Count} arquivo(s) (anexos que não são texto e metadados) não podem ser gravados no app nativo ainda e ficam de fora." : "")
            + (plan.Kept.Count > 0 ? $" {plan.Kept.Count} documento(s) já existem com conteúdo diferente e serão mantidos como estão." : "");
        if (await Dialogs.ChooseAsync("Documentos para incorporar", [new DialogItem("Continuar")], description) is null)
        {
            ShowLifeNotice("Importação cancelada.");
            return;
        }
        int written = 0;
        try
        {
            foreach (var (path, content) in plan.New)
            {
                if (written % 10 == 0) ShowLifeNotice($"Importando {written} de {plan.New.Count}…");
                await WriteVaultFileAsync(path, content, exists: false);
                written++;
            }
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível importar", $"{written} de {plan.New.Count} documento(s) foram gravados antes do erro: {ex.Message}");
            return;
        }
        World.InvalidateVisual();
        ShowLifeNotice($"Importação concluída. {written} arquivo(s) gravado(s); {plan.Unchanged} preservado(s), sem nova gravação.");
    }

    /// <summary>
    /// app.js importarEntradasDestino for a vault, an export or a backup: confirm, write the pending note, map and
    /// history, stop the map saves, plan against the vault as it is on disk, confirm the replacements ("Restaurar com
    /// backup"), run the ImportTransaction (every replaced or removed file is copied to .urbe/backup/import-… first, with
    /// the recovery journal) and reopen the city. On a failure the journal is recovered and the city reopened.
    /// </summary>
    private async Task RestoreSelectionAsync(ImportInspection inspection)
    {
        if (_vault?.ImportStorage is not { } storage || _snapshot is null)
        {
            await Dialogs.AlertAsync("Não foi possível importar", NoVaultMessage);
            return;
        }
        if (_snapshot.IsReadOnly || _snapshot.IsMapReadOnly || _snapshot.FutureFiles.Count > 0)
        {
            await Dialogs.AlertAsync("Não foi possível importar", "O vault atual está protegido. Nenhum dado será alterado.");
            return;
        }
        if (!await Dialogs.ConfirmAsync("Vault para restauração", $"{inspection.DocumentCount} documento(s) e {inspection.AssetCount} arquivo(s). O vault de origem será restaurado com suas notas e metadados. Um backup do vault atual será preservado.", "Continuar"))
        {
            ShowLifeNotice("Importação cancelada.");
            return;
        }
        bool suspended = false;
        try
        {
            // rodarSinc(true) + persistence.flush + suspend: what is pending goes to the vault, then nothing else writes
            if (Editor.HasChanges && !await Editor.SaveAsync()) throw new IOException("A nota aberta não pôde ser salva. Nenhum dado foi alterado.");
            await SaveHistoryAsync();
            if (_mapSave is { } saves)
            {
                await saves.FlushAsync();
                saves.Stop();
                _mapSave = null;
            }
            suspended = true;
            foreach (var tab in Editor.Tabs.ToList()) await Editor.CloseTabAsync(tab);
            var files = new List<VaultFile>();
            foreach (var path in await storage.ListAsync(CancellationToken.None))
                if (await storage.ReadAsync(path, CancellationToken.None) is { } bytes) files.Add(new VaultFile(path, bytes.ToArray()));
            var current = VaultReader.Read(files);
            var revision = ImportTransaction.Revision(files);
            var plan = ImportPipeline.Plan(inspection, current, revision);
            if (plan.Conflicts.Count > 0)
            {
                if (!await Dialogs.ConfirmAsync("Restaurar este vault?", $"A restauração substituirá {plan.Conflicts.Count} arquivo(s) do vault atual. Eles serão guardados em backup antes de qualquer alteração.", "Restaurar com backup"))
                {
                    ShowLifeNotice("Importação cancelada.");
                    return;
                }
                plan = ImportPipeline.Plan(inspection, current, revision, plan.Conflicts.ToDictionary(c => c.Path, _ => ImportConflictChoice.Replace, StringComparer.OrdinalIgnoreCase));
            }
            ShowLifeNotice("Importando…");
            var result = await ImportPipeline.ExecuteAsync(plan, new ImportTransaction(storage));
            suspended = false;
            await ReopenVaultAsync();
            await ApplyBackupPreferencesAsync(inspection);
            ShowLifeNotice($"Vault restaurado. {result.Written} arquivo(s) gravado(s); {result.Unchanged} preservado(s), sem nova gravação.");
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível importar", ex.Message.Length > 0 ? ex.Message : "A seleção não pôde ser lida. O vault atual foi preservado.");
        }
        finally
        {
            if (suspended)
            {
                try
                {
                    await new ImportTransaction(storage).RecoverAsync();
                    await ReopenVaultAsync();
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    ShowLifeNotice("O vault aguarda recuperação. Preserve o backup e reabra o aplicativo.");
                }
            }
        }
    }

    /// <summary>
    /// workspace.backups + workspace.restoreBackup (backup.js list/restore): the backups kept in .urbe/backup (before a
    /// migration, a reorganization or an import), newest first; the one chosen is restored through the same import
    /// transaction, so what it replaces is itself kept in a new backup first.
    /// </summary>
    public async Task ShowBackupsAsync()
    {
        if (_vault?.ImportStorage is not { } storage || !VaultWritable)
        {
            await Dialogs.AlertAsync("Backups do vault", NoVaultMessage);
            return;
        }
        var token = CancellationToken.None;
        var paths = await storage.ListAsync(token);
        var backups = new List<(string Dir, string At, string Detail)>();
        foreach (var path in paths)
        {
            var match = System.Text.RegularExpressions.Regex.Match(path, @"^\.urbe/backup/([^/]+)/manifest\.json$");
            if (!match.Success || await storage.ReadAsync(path, token) is not { } bytes) continue;
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(bytes);
                var root = json.RootElement;
                string at = root.TryGetProperty("at", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.String ? a.GetString()! : "";
                string from = root.TryGetProperty("from", out var fr) ? fr.ToString() : "";
                int count = root.TryGetProperty("files", out var f) && f.ValueKind == System.Text.Json.JsonValueKind.Array ? f.GetArrayLength()
                    : root.TryGetProperty("journal", out var j) && j.TryGetProperty("entries", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.Array ? e.GetArrayLength() : 0;
                var dir = ".urbe/backup/" + match.Groups[1].Value;
                if (at.Length == 0) at = match.Groups[1].Value;
                var label = root.TryGetProperty("kind", out _) ? "Antes de importar" : from == "layout" ? "Antes de reorganizar a cidade" : from.Length > 0 ? "Antes de migrar o formato" : "Backup";
                backups.Add((dir, at, label + " · " + count + " arquivo(s)"));
            }
            catch (System.Text.Json.JsonException) { /* an unreadable manifest is not offered */ }
        }
        if (backups.Count == 0)
        {
            await Dialogs.AlertAsync("Backups do vault", "Nenhum backup neste vault. O Urbe guarda um backup em .urbe/backup antes de migrar, reorganizar ou importar.");
            return;
        }
        backups.Sort((x, y) => string.CompareOrdinal(y.At, x.At));
        static string When(string at) => DateTimeOffset.TryParse(at, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
            ? t.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) : at;
        var pick = await Dialogs.ChooseAsync("Backups do vault", backups.Select(b => new DialogItem(When(b.At).Length > 0 ? When(b.At) : b.Dir, Detail: b.Detail)).ToList(),
            "Escolha um backup para restaurar. O que ele substituir será guardado antes em outro backup.");
        if (pick is null) return;
        var chosen = backups[pick.Value].Dir + "/";
        var sources = new List<ImportSource>();
        foreach (var path in paths.Where(p => p.StartsWith(chosen, StringComparison.Ordinal)))
        {
            var bytes = await storage.ReadAsync(path, token);
            if (bytes is null) continue;
            var copy = bytes.Value.ToArray();
            sources.Add(new ImportSource(path[chosen.Length..], _ => ValueTask.FromResult<Stream>(new MemoryStream(copy, writable: false))));
        }
        ImportInspection inspection;
        try { inspection = await ImportPipeline.InspectAsync(new ImportSelection(sources, IsDirectory: true)); }
        catch (InvalidDataException ex)
        {
            await Dialogs.AlertAsync("Não foi possível restaurar", ex.Message);
            return;
        }
        await RestoreSelectionAsync(inspection);
    }

    /// <summary>
    /// "Preferências do backup" (importarEntradasDestino + UrbeExportManifest.applyState): an export carries this device's
    /// allowed preferences; applied only when the person says so, because plugin approvals come with them.
    /// </summary>
    private async Task ApplyBackupPreferencesAsync(ImportInspection inspection)
    {
        if (inspection.Manifest?.State is not { } state || state.LocalStorage.Count == 0 && (state.Plugins?.Count ?? 0) == 0) return;
        if (!await Dialogs.ConfirmAsync("Preferências do backup",
                "Aplicar as preferências deste backup? Permissões de plugins incluídas também serão autorizadas neste aparelho; mantenha as atuais se não confiar na origem.",
                "Aplicar preferências", cancel: "Manter atuais"))
            return;
        foreach (var (key, value) in VaultExportStatePolicy.Apply(Customization.Device.All(), state, "Urbe").Writes)
            Customization.Device.Set(key, value);
        SyncPlugins();
    }

    /// <summary>abrirCidade(Disco.cidade): the same folder read again from disk.</summary>
    private async Task ReopenVaultAsync()
    {
        if (await _vault!.ReloadAsync() is { } snapshot) await ShowVaultAsync(snapshot);
        World.InvalidateVisual();
    }

    private async Task<ImportSelection?> PickImportAsync(bool folder)
    {
        if (ImportPicker is not null) return await ImportPicker(folder);
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return null;
        var sources = new List<ImportSource>();
        if (folder)
        {
            var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Importar pasta", AllowMultiple = false });
            if (picked.Count == 0) return null;
            async Task Walk(IStorageFolder dir, string prefix, int depth)
            {
                if (depth > 32) throw new IOException("A pasta ultrapassa 32 níveis.");
                await foreach (var item in dir.GetItemsAsync())
                {
                    if (item is IStorageFolder sub) await Walk(sub, prefix + item.Name + "/", depth + 1);
                    else if (item is IStorageFile file) sources.Add(new ImportSource(prefix + file.Name, async _ => await file.OpenReadAsync()));
                }
            }
            await Walk(picked[0], "", 0);
        }
        else
        {
            var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Importar arquivos", AllowMultiple = true });
            foreach (var file in picked) sources.Add(new ImportSource(file.Name, async _ => await file.OpenReadAsync()));
        }
        return sources.Count == 0 ? null : new ImportSelection(sources, folder);
    }
}

/// <summary>What a documents import will do to the current vault (importarEntradasDestino for text files).</summary>
public sealed record ImportDocumentsPlan(IReadOnlyList<(string Path, string Content)> New, IReadOnlyList<string> Kept,
    IReadOnlyList<string> Skipped, int Unchanged);

public static class ImportDocuments
{
    /// <summary>The selection's text documents (under the target folder when one is chosen) against the
    /// vault's: new ones are written, identical ones are left alone, different ones are KEPT as they are
    /// (the native vault has no backup-and-replace yet); non-text files and hidden metadata are skipped.</summary>
    public static ImportDocumentsPlan Plan(ImportInspection inspection, IReadOnlyDictionary<string, string> current, string? targetFolder)
    {
        var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in current) existing[pair.Key] = pair.Value;
        var added = new List<(string, string)>();
        var kept = new List<string>();
        var skipped = new List<string>();
        int unchanged = 0;
        foreach (var file in inspection.Files.Values.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var path = string.IsNullOrEmpty(targetFolder) ? file.Path : targetFolder + "/" + file.Path;
            if (!IUrbeVaultStorage.WritableExtensions.Contains(System.IO.Path.GetExtension(path)) || path.Split('/').Any(s => s.StartsWith('.')))
            {
                skipped.Add(path);
                continue;
            }
            var text = new UTF8Encoding(false, true).GetString(file.Bytes.Span);
            if (existing.TryGetValue(path, out var old))
            {
                if (old == text) unchanged++; else kept.Add(path);
            }
            else added.Add((path, text));
        }
        return new ImportDocumentsPlan(added, kept, skipped, unchanged);
    }

}
