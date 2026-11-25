using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;
using PaletteHarmony = Lunet.Studio.PaletteHarmony;
using PaletteSwatch = Lunet.Studio.PaletteSwatch;
using ProjectPalette = Lunet.Studio.ProjectPalette;
using SpriteColor = Lunet.Studio.SpriteColor;
using SpriteDocument = Lunet.Studio.SpriteDocument;

namespace Lunet.Client.Views;

/// <summary>
/// The game's palette in the shell (A5): the colours every Studio offers (Assets/palette.json, the previous Studio's
/// file). Start from a preset or from the game's own art, add by hex, and grow a colour into a ramp or a harmony.
/// Each change is written at once. The Sprite Studio shows this palette when the game has one.
/// </summary>
public sealed class PaletteView : UserControl
{
    private readonly LunetShell _shell;
    private readonly Action _close;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S };
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private int? _selected;

    public PaletteView(LunetShell shell, Action close)
    {
        _shell = shell;
        _close = close;
        Palette = Load(shell) ?? new ProjectPalette();
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        var back = LunetUi.IconButton(LunetIcons.Back, "Voltar", () => Close());
        var bar = new DockPanel { Height = 56, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.M, 0) };
        DockPanel.SetDock(back, Dock.Left);
        bar.Children.Add(back);
        bar.Children.Add(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { LunetUi.Title("Paleta do jogo"), _status } });
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer { Content = new Border { Child = _page, MaxWidth = 760, Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.Xl) } });
        Content = root;
        Refresh();
    }

    public ProjectPalette Palette { get; private set; }

    /// <summary>The game's palette, or null when it has none (or it cannot be read).</summary>
    public static ProjectPalette? Load(LunetShell shell)
    {
        if (shell.Workspace.Project is not { } project) return null;
        var file = Path.Combine(project.Directory, ProjectPalette.DefaultPath);
        if (!File.Exists(file)) return null;
        try { return ProjectPalette.Parse(File.ReadAllText(file)); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { return null; }
    }

    public bool Close() { _close(); return true; }

    public void UsePreset(string id) { Palette = ProjectPalette.FromPreset(id); _selected = null; Save(); }

    public int AddColors(IEnumerable<uint> colors) { var added = Palette.AddRange(colors); Save(); return added; }

    /// <summary>The colours of the game's sprites (their saved sources), at most 16, nearest kept apart.</summary>
    public int ExtractFromArt()
    {
        var pixels = new List<uint>();
        var project = _shell.Workspace.Project!;
        foreach (var file in project.ListFiles().Where(f => f.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase)))
        {
            try { foreach (var frame in SpriteDocument.Parse(project.ReadText(file)).Frames) pixels.AddRange(frame.Where(p => (p >> 24) != 0)); }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException) { }
            if (pixels.Count > 400_000) break;
        }
        return AddColors(ProjectPalette.FromPixels(pixels, 16));
    }

    public void Remove(int index)
    {
        if (index < 0 || index >= Palette.Colors.Count) return;
        Palette.Colors.RemoveAt(index);
        _selected = null;
        Save();
    }

    private void Save()
    {
        try
        {
            _shell.Workspace.Project!.WriteText(ProjectPalette.DefaultPath, Palette.ToJson());
            _status.Text = $"{Palette.Colors.Count} cores · salvo";
            _status.Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _status.Text = "Não foi possível salvar: " + e.Message;
            _status.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger);
        }
        Refresh();
    }

    private void Refresh()
    {
        if (string.IsNullOrEmpty(_status.Text)) _status.Text = $"{Palette.Colors.Count} cores";
        _page.Children.Clear();
        _page.Children.Add(LunetUi.Secondary("As cores do jogo, oferecidas em todos os Studios. Toque numa cor para criar tons dela."));

        var grid = new WrapPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < Palette.Colors.Count; i++)
        {
            var index = i;
            var swatch = Palette.Colors[i];
            var selected = _selected == i;
            var b = new Button
            {
                Width = 52, Height = 52, Padding = new Thickness(0), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
                Background = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Avalonia.Media.Color.FromUInt32(swatch.Argb)),
                BorderBrush = LunetTokens.Brush(selected ? LunetTokens.Color.TextPrimary : LunetTokens.Color.BorderStrong), BorderThickness = new Thickness(selected ? 3 : 1),
                Name = "Swatch-" + i,
            };
            AutomationProperties.SetName(b, $"{swatch.Name} {SpriteColor.ToHex(swatch.Argb)}");
            ToolTip.SetTip(b, swatch.Name);
            b.Click += (_, _) => { _selected = _selected == index ? null : index; Refresh(); };
            grid.Children.Add(b);
        }
        if (Palette.Colors.Count == 0) _page.Children.Add(LunetUi.Text("Sem cores ainda. Comece de um modelo ou extraia da arte do jogo.", LunetTokens.Type.Label, LunetTokens.Color.TextTertiary));
        _page.Children.Add(grid);

        if (_selected is { } sel && sel < Palette.Colors.Count)
        {
            var color = Palette.Colors[sel].Argb;
            _page.Children.Add(Section($"{Palette.Colors[sel].Name} · {SpriteColor.ToHex(color)}"));
            var actions = new WrapPanel { Orientation = Orientation.Horizontal };
            void Action(string label, Action act, string name, ButtonKind kind = ButtonKind.Secondary)
            {
                var b = LunetUi.Button(label, kind, null, act);
                b.MinHeight = 40; b.Margin = new Thickness(0, 0, LunetTokens.Space.Xs, LunetTokens.Space.Xs); b.Name = name;
                actions.Children.Add(b);
            }
            Action("Rampa de 5 tons", () => AddColors(ProjectPalette.Ramp(color, 5)), "Ramp");
            Action("Complementar", () => AddColors(ProjectPalette.Harmony(color, PaletteHarmony.Complementary)), "Complementary");
            Action("Análogas", () => AddColors(ProjectPalette.Harmony(color, PaletteHarmony.Analogous)), "Analogous");
            Action("Tríade", () => AddColors(ProjectPalette.Harmony(color, PaletteHarmony.Triadic)), "Triadic");
            Action("Remover", () => Remove(sel), "RemoveColor", ButtonKind.Ghost);
            _page.Children.Add(actions);
        }

        _page.Children.Add(Section("Adicionar"));
        var hex = new TextBox { PlaceholderText = "#RRGGBB", Width = 140, MinHeight = LunetTokens.MinTouch, FontFamily = LunetTokens.MonoFont, VerticalContentAlignment = VerticalAlignment.Center, Name = "HexInput" };
        AutomationProperties.SetName(hex, "Cor em hexadecimal");
        var add = LunetUi.Button("Adicionar cor", ButtonKind.Secondary, LunetIcons.Plus, () =>
        {
            if (SpriteColor.ParseHex(hex.Text) is { } c) AddColors([c]);
            else { _status.Text = "Use #RRGGBB, por exemplo #FFA300."; _status.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger); }
        });
        add.Name = "AddHex";
        _page.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs, Children = { hex, add } });
        var extract = LunetUi.Button("Extrair da arte do jogo", ButtonKind.Secondary, LunetIcons.Art, () =>
        {
            if (ExtractFromArt() == 0) { _status.Text = "Nenhuma cor nova nos sprites do jogo."; _status.Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary); }
        });
        extract.Name = "ExtractArt";
        extract.HorizontalAlignment = HorizontalAlignment.Left;
        _page.Children.Add(extract);

        _page.Children.Add(Section("Começar de um modelo (troca as cores)"));
        var presets = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var (id, name, colors) in ProjectPalette.Presets)
        {
            var strip = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var c in colors) strip.Children.Add(new Border { Width = 12, Height = 20, Background = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Avalonia.Media.Color.FromUInt32(c)) });
            var content = new StackPanel { Spacing = 4, Children = { strip, LunetUi.Text(name, LunetTokens.Type.Caption) } };
            var presetId = id;
            var b = new Button
            {
                Content = content, Padding = new Thickness(LunetTokens.Space.S), Margin = new Thickness(0, 0, LunetTokens.Space.Xs, LunetTokens.Space.Xs),
                Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
                Name = "Preset-" + id,
            };
            AutomationProperties.SetName(b, "Modelo " + name);
            b.Click += (_, _) => UsePreset(presetId);
            presets.Children.Add(b);
        }
        _page.Children.Add(presets);
    }

    private static TextBlock Section(string text)
    {
        var t = LunetUi.Text(text.ToUpperInvariant(), LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary, FontWeight.SemiBold);
        t.Margin = new Thickness(0, LunetTokens.Space.M, 0, 0);
        return t;
    }
}
