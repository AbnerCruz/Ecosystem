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
    private readonly Action<string>? _publish;
    private readonly Action _close;
    private readonly StackPanel _page = new() { Spacing = LunetTokens.Space.S };
    private readonly TextBlock _status = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private int? _selected;

    private readonly Lunet.Core.StudioAssetWorkspace? _storage;
    private readonly string? _path;

    /// <summary>The game's palette (Assets/palette.json), or with <paramref name="storage"/> and <paramref name="path"/> a
    /// standalone <c>.palette.json</c> of the Studios (written with its <c>.gpl</c>, as the previous screen did).</summary>
    public PaletteView(LunetShell shell, Action close, Lunet.Core.StudioAssetWorkspace? storage = null, string? path = null, Action<string>? publish = null)
    {
        _shell = shell;
        _close = close;
        _storage = storage;
        _path = path;
        _publish = publish;
        Palette = storage is not null && path is not null
            ? (File.Exists(Path.Combine(storage.RootDirectory, path)) ? ProjectPalette.Parse(storage.ReadText(path)) : new ProjectPalette { Name = "Paleta" })
            : Load(shell) ?? new ProjectPalette();
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
        if (!project.Exists(ProjectPalette.DefaultPath)) return null;
        try { return ProjectPalette.Parse(project.ReadText(ProjectPalette.DefaultPath)); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { return null; }
    }

    public bool Close() { _close(); return true; }

    public void UsePreset(string id) { Palette = ProjectPalette.FromPreset(id); _selected = null; Save(); }

    public int AddColors(IEnumerable<uint> colors) { var added = Palette.AddRange(colors); Save(); return added; }

    /// <summary>The colours of the game's sprites (their saved sources), at most 16, nearest kept apart.</summary>
    public int ExtractFromArt()
    {
        if (_shell.Workspace.Project is not { } project) return 0;
        var pixels = new List<uint>();
        foreach (var file in project.ListFiles().Where(f => f.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase)))
        {
            try { foreach (var frame in SpriteDocument.Parse(project.ReadText(file)).Frames) pixels.AddRange(frame.Where(p => (p >> 24) != 0)); }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or ArgumentException) { }
            if (pixels.Count > 400_000) break;
        }
        return AddColors(ProjectPalette.FromPixels(pixels, 16));
    }

    /// <summary>Adds the colours of a .gpl (GIMP, Krita, Aseprite) or a list of hex values. Returns how many were new.</summary>
    public int ImportText(string text, string name) => AddColors(ProjectPalette.Import(text, name).Argb);

    /// <summary>Writes <c>Scripts/GamePalette.cs</c>: one <c>Color</c> per swatch, so code draws with the art's colours.</summary>
    public string GenerateCode()
    {
        const string path = "Scripts/GamePalette.cs";
        _shell.Workspace.Write(path, Palette.ToCSharp(null));
        _status.Text = $"{path} gerado: use GamePalette.{Palette.ExportNames().FirstOrDefault() ?? "All"} no código.";
        _status.Foreground = LunetTokens.Brush(LunetTokens.Color.Success);
        return path;
    }

    private async Task PickImport()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var picked = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Importar paleta (.gpl ou hex)", AllowMultiple = false });
        if (picked.FirstOrDefault() is not { } file) return;
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var added = ImportText(await reader.ReadToEndAsync(), Path.GetFileNameWithoutExtension(file.Name));
            _status.Text = $"{added} cor(es) importadas.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _status.Text = "Não foi possível importar: " + e.Message; }
    }

    private async Task PickExportGpl()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage) return;
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions { Title = "Exportar paleta", SuggestedFileName = "paleta.gpl", DefaultExtension = "gpl" });
        if (file is null) return;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(Palette.ToGpl());
            _status.Text = "Paleta exportada (.gpl: GIMP, Krita, Aseprite).";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _status.Text = "Não foi possível exportar: " + e.Message; }
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
            if (_storage is not null && _path is not null)
                _storage.WriteTextPair(_path, Palette.ToJson(), _path[..^".palette.json".Length] + ".gpl", Palette.ToGpl());
            else _shell.Workspace.Project!.WriteText(ProjectPalette.DefaultPath, Palette.ToJson());
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
            // Contrast (WCAG ratio) against dark and light backgrounds: is this colour readable as text or UI?
            double dark = ProjectPalette.Contrast(color, 0xFF000000), light = ProjectPalette.Contrast(color, 0xFFFFFFFF);
            string Grade(double r) => r >= 7 ? "ótimo" : r >= 4.5 ? "bom para texto" : r >= 3 ? "só para formas" : "fraco";
            _page.Children.Add(LunetUi.Text($"Contraste: {dark:0.0}:1 no escuro ({Grade(dark)}) · {light:0.0}:1 no claro ({Grade(light)})", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary));
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
        var files = new WrapPanel { Orientation = Orientation.Horizontal };
        void FileAction(string label, string icon, Action act, string name)
        {
            var b = LunetUi.Button(label, ButtonKind.Secondary, icon, act);
            b.Margin = new Thickness(0, 0, LunetTokens.Space.Xs, LunetTokens.Space.Xs); b.Name = name;
            files.Children.Add(b);
        }
        FileAction("Importar .gpl ou hex", LunetIcons.Download, () => _ = PickImport(), "ImportGpl");
        FileAction("Exportar .gpl", LunetIcons.Share, () => _ = PickExportGpl(), "ExportGpl");
        if (_storage is null) FileAction("Gerar código C#", LunetIcons.Code, () => GenerateCode(), "PaletteCode");
        if (_publish is not null && _path is not null) FileAction("Publicar", LunetIcons.Share, () => { Save(); _publish(_path); }, "PublishPalette");
        _page.Children.Add(files);
        var extract = LunetUi.Button("Extrair da arte do jogo", ButtonKind.Secondary, LunetIcons.Art, () =>
        {
            if (ExtractFromArt() == 0) { _status.Text = "Nenhuma cor nova nos sprites do jogo."; _status.Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary); }
        });
        extract.Name = "ExtractArt";
        extract.IsVisible = _shell.Workspace.Project is not null && _storage is null;
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

    private static TextBlock Section(string text) => LunetUi.Section(text);
}
