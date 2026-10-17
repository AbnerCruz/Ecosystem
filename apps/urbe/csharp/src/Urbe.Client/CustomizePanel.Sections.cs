using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>An auto-fill grid (grid-template-columns: repeat(auto-fill, minmax(min, 1fr))).</summary>
public sealed class AutoFillPanel(double min, double gap) : Panel
{
    private int Columns(double width) => Math.Max(1, (int)Math.Floor((width + gap) / (min + gap)));

    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsInfinity(available.Width) ? min * 3 : available.Width;
        int cols = Columns(width);
        double cell = (width - gap * (cols - 1)) / cols;
        double rowH = 0, total = 0; int col = 0;
        foreach (var c in Children)
        {
            c.Measure(new Size(cell, double.PositiveInfinity));
            rowH = Math.Max(rowH, c.DesiredSize.Height);
            if (++col == cols) { total += rowH + gap; rowH = 0; col = 0; }
        }
        if (col > 0) total += rowH + gap;
        return new Size(width, Math.Max(0, total - gap));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int cols = Columns(finalSize.Width);
        double cell = (finalSize.Width - gap * (cols - 1)) / cols;
        double y = 0, rowH = 0; int col = 0;
        var row = new List<Control>();
        void Flush() { foreach (var r in row) r.Arrange(new Rect(r.Bounds.X, y, cell, rowH)); }
        foreach (var c in Children)
        {
            c.Arrange(new Rect(col * (cell + gap), y, cell, c.DesiredSize.Height));
            row.Add(c);
            rowH = Math.Max(rowH, c.DesiredSize.Height);
            if (++col == cols) { Flush(); y += rowH + gap; rowH = 0; col = 0; row.Clear(); }
        }
        if (col > 0) Flush();
        return finalSize;
    }
}

/// <summary>The sections of src/customize/panel.js beyond Cidade and Arquivo: Aparência, Texto, Editor, Texturas, Estilos CSS, Plugins and "Cores do chão".</summary>
public sealed partial class CustomizePanel
{
    private static readonly string[] NamesColor = ["destaque", "fundo", "superficie", "superficie2", "superficie3", "linha", "linha2", "texto", "texto2", "texto3", "perigo", "sucesso", "aviso", "codigo"];
    private static readonly Dictionary<string, string> ColorNames = new()
    {
        ["destaque"] = "Destaque", ["fundo"] = "Fundo", ["superficie"] = "Painéis", ["superficie2"] = "Painéis 2", ["superficie3"] = "Botões", ["linha"] = "Linhas", ["linha2"] = "Bordas",
        ["texto"] = "Texto", ["texto2"] = "Texto secundário", ["texto3"] = "Texto apagado", ["perigo"] = "Perigo", ["sucesso"] = "Sucesso", ["aviso"] = "Aviso", ["codigo"] = "Código"
    };
    private static readonly string[] MainColors = ["destaque", "fundo", "superficie", "texto", "perigo", "sucesso"];
    private static readonly string[] Accents = ["#8fb3ff", "#b39cff", "#f08aa0", "#ff9f6b", "#f1c46e", "#8fd18a", "#5fd0e0", "#3b6fe0", "#a0582a", "#ffd400"];
    private static readonly string[] GroundIds = ["grass", "meadow", "forest", "dense", "swamp", "taiga", "tundra", "snow", "hills", "mountain", "peak", "desert", "savanna", "steppe", "beach", "river", "lake", "sea", "deep"];
    private static readonly Dictionary<string, string> PortugueseBiome = new()
    {
        ["grass"] = "grama", ["meadow"] = "prado", ["forest"] = "floresta", ["dense"] = "mata", ["swamp"] = "pantano", ["taiga"] = "taiga", ["tundra"] = "tundra", ["snow"] = "neve", ["hills"] = "colinas",
        ["mountain"] = "montanha", ["peak"] = "pico", ["desert"] = "deserto", ["savanna"] = "savana", ["steppe"] = "estepe", ["beach"] = "praia", ["sea"] = "mar", ["deep"] = "profundo", ["river"] = "rio", ["lake"] = "lago"
    };
    private static readonly Dictionary<string, string> PortugueseBuilding = new()
    {
        ["house"] = "casa", ["hall"] = "salao", ["workshop"] = "oficina", ["dyer"] = "tinturaria", ["tower"] = "torre", ["market"] = "mercado", ["store"] = "armazem"
    };
    private bool _moreColors;
    private Border? _toast;
    private DispatcherTimer? _toastTimer;

    private JsonObject Sec(string name) => (Customization.Config[name] as JsonObject) ?? [];
    private static string S(JsonObject o, string k, string d = "") => o[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s : d;
    private static double N(JsonObject o, string k, double d) => o[k] is JsonValue v && v.TryGetValue<double>(out var x) ? x : d;
    private static bool Bo(JsonObject o, string k, bool d = false) => o[k] is JsonValue v && v.TryGetValue<bool>(out var x) ? x : d;
    private Task Set(string path, JsonNode value) => Customization.SetAsync(Patch(path, value));

    // ---------------------------------------------------------------- pieces

    private static Button Link(string text, string? icon, Action click)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (icon is not null) content.Children.Add(UrbeTheme.Icon(icon, 17, UrbeTheme.Accent));
        content.Children.Add(Label(text, 13, FontWeight.SemiBold, UrbeTheme.Accent));
        var b = new Button { Content = content, MinHeight = 36, Padding = new Thickness(10, 0), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, VerticalContentAlignment = VerticalAlignment.Center };
        b.Click += (_, _) => click();
        return b;
    }

    private static Button More(string text, string? icon, Action click)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        if (icon is not null) content.Children.Add(UrbeTheme.Icon(icon, 18, UrbeTheme.Text2));
        content.Children.Add(Label(text, 14, FontWeight.SemiBold, UrbeTheme.Text2));
        var b = new Button
        {
            Content = content, MinHeight = 44, Margin = new Thickness(0, 10, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(12), Background = Brushes.Transparent, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
            BorderThickness = new Thickness(1)
        };
        b.Click += (_, _) => click();
        return b;
    }

    private static Button Mini(string icon, string tip, Action click, double size = 36)
    {
        var b = new Button { Content = UrbeTheme.Icon(icon, 18, UrbeTheme.Text3), Width = size, Height = size, Padding = new Thickness(0), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(b, tip);
        AutomationName(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    private static Border Pill(string text, string kind)
    {
        var (bg, fg) = kind switch
        {
            "ok" => (UrbeTheme.Brush(UrbeTheme.Ok, .14), UrbeTheme.Ok),
            "err" => (UrbeTheme.Brush(UrbeTheme.Danger, .12), UrbeTheme.Danger),
            "warn" => (UrbeTheme.Brush(UrbeTheme.Warn, .14), UrbeTheme.Warn),
            _ => (UrbeTheme.Brush(UrbeTheme.Surface3), UrbeTheme.Text2)
        };
        return new Border { Background = bg, CornerRadius = new CornerRadius(999), Padding = new Thickness(10, 4), VerticalAlignment = VerticalAlignment.Top, Child = Label(text, 12, FontWeight.SemiBold, fg) };
    }

    private static Border Banner(string kind, string text)
    {
        var (bg, fg, icon) = kind == "err" ? (UrbeTheme.Brush(UrbeTheme.Danger, .12), UrbeTheme.Danger, (string?)null) : (UrbeTheme.Brush(UrbeTheme.Warn, .12), UrbeTheme.Text, CustomizePanel.Icons.Shield);
        var row = new DockPanel();
        if (icon is not null)
        {
            var i = UrbeTheme.Icon(icon, 20, UrbeTheme.Warn);
            i.Margin = new Thickness(0, 0, 10, 0); i.VerticalAlignment = VerticalAlignment.Top;
            DockPanel.SetDock(i, Dock.Left);
            row.Children.Add(i);
        }
        row.Children.Add(new TextBlock { Text = text, FontFamily = UrbeTheme.UiFont, FontSize = 13.5, Foreground = UrbeTheme.Brush(fg), TextWrapping = TextWrapping.Wrap });
        return new Border { Background = bg, CornerRadius = new CornerRadius(12), Padding = new Thickness(14, 12), Margin = new Thickness(0, 0, 0, 16), Child = row };
    }

    private Border RangeRow(string path, double value, double min, double max, double step, string label, string unit)
    {
        string Show(double v) => LegacyCustomize.JsNumber(Math.Round(v, 2)) + unit;
        var output = new TextBlock { Text = Show(value), FontFamily = UrbeTheme.MonoFont, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) };
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true, SmallChange = step, LargeChange = step * 2, MinHeight = 28 };
        AutomationName(slider, label);
        bool committed = true;
        slider.ValueChanged += (_, e) => { output.Text = Show(e.NewValue); committed = false; Customization.Preview(Patch(path, JsonValue.Create(Math.Round(e.NewValue, 2)))); };
        async void Commit()
        {
            if (committed) return;
            committed = true;
            await Set(path, JsonValue.Create(Math.Round(slider.Value, 2)));
        }
        slider.AddHandler(PointerReleasedEvent, (_, _) => Commit(), RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
        slider.AddHandler(PointerCaptureLostEvent, (_, _) => Commit(), RoutingStrategies.Direct, handledEventsToo: true);
        slider.AddHandler(KeyUpEvent, (_, _) => Commit(), RoutingStrategies.Bubble, handledEventsToo: true);
        var head = new DockPanel();
        DockPanel.SetDock(output, Dock.Right);
        head.Children.Add(output);
        head.Children.Add(Label(label, 14.5, FontWeight.SemiBold));
        var stack = new StackPanel { Spacing = 6, Children = { head, slider } };
        return new Border { Tag = "row", Child = stack, MinHeight = 52, Padding = new Thickness(0, 6), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line) };
    }

    private Border ColorRow(string key, string hex, bool own)
    {
        var ctl = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        if (own) ctl.Children.Add(Mini(Icons.Restore, "Voltar à cor do tema", async () =>
        {
            var c = (JsonObject)Customization.Config.DeepClone();
            ((JsonObject)c["cores"]!).Remove(key);
            await Customization.SaveAsync(c);
        }));
        ctl.Children.Add(ColorEditor.Button(hex, ColorNames[key],
            h => Customization.Preview(Patch("cores." + key, JsonValue.Create(h))),
            async h => await Set("cores." + key, JsonValue.Create(h))));
        return Row(ColorNames[key], own ? "personalizada" : "do tema", ctl);
    }

    private Button ThemeCard(string id, string name, bool light, IReadOnlyDictionary<string, string> colors, bool on)
    {
        string C(string k) => colors.GetValueOrDefault(k, "#000000");
        var prev = new Canvas { Height = 62 };
        var card = new Border { Child = prev, Background = new SolidColorBrush(Color.Parse(C("fundo"))), BorderBrush = new SolidColorBrush(Color.Parse(colors.GetValueOrDefault("linha2", C("linha")))), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), ClipToBounds = true, Height = 62 };
        void Add(double x, double y, double w, double h, string fill, double r, bool rightBottom = false)
        {
            var b = new Border { Width = w, Height = h, CornerRadius = new CornerRadius(r), Background = new SolidColorBrush(Color.Parse(fill)) };
            Canvas.SetLeft(b, x); Canvas.SetTop(b, y);
            prev.Children.Add(b);
        }
        prev.SizeChanged += (_, e) =>
        {
            prev.Children.Clear();
            double w = e.NewSize.Width;
            Add(8, 8, Math.Max(0, w * .6 - 8), 44, C("superficie"), 5);
            Add(14, 16, w * .38, 5, C("texto"), 3);
            Add(14, 27, w * .28, 4, colors.GetValueOrDefault("texto3", C("texto")), 3);
            Add(w - 32, 28, 22, 22, C("destaque"), 7);
        };
        var name_ = new DockPanel();
        if (on) { var ck = UrbeTheme.Icon(Icons.Check, 16, UrbeTheme.Accent); DockPanel.SetDock(ck, Dock.Right); name_.Children.Add(ck); }
        name_.Children.Add(Label(name, 13.5, FontWeight.SemiBold));
        var stack = new StackPanel { Spacing = 6, Children = { card, name_, new TextBlock { Text = light ? "claro" : "escuro", FontFamily = UrbeTheme.UiFont, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(0, -4, 0, 0) } } };
        var b = new Button
        {
            Content = stack, Padding = new Thickness(8), CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Surface), HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Line2), BorderThickness = new Thickness(on ? 2 : 1)
        };
        AutomationName(b, name);
        b.Click += async (_, _) => await Set("tema", JsonValue.Create(id));
        return b;
    }

    // ---------------------------------------------------------------- Aparência

    private void AppearanceSection()
    {
        var c = Customization.Config;
        var pal = Customization.Palette;
        var own = Sec("cores");
        string theme = S(c, "tema", "escuro");
        var cards = new AutoFillPanel(132, 10);
        foreach (var p in LegacyCustomizeTheme.Presets) cards.Children.Add(ThemeCard(p.Id, p.Name, p.Light, p.Colors, theme == p.Id));
        foreach (var t in Customization.Themes())
        {
            var baseColors = LegacyCustomizeTheme.PresetById(t.Light ? "claro" : "escuro")!.Colors.ToDictionary(k => k.Key, k => k.Value);
            foreach (var kv in t.Colors) baseColors[kv.Key] = kv.Value;
            cards.Children.Add(ThemeCard(t.Id, t.Name, t.Light, baseColors, theme == t.Id));
        }
        var save = Link("Salvar como tema", Icons.Download, async () =>
        {
            var n = await Dialogs.PromptAsync(new PromptOptions("Salvar tema", "Nome", "Meu tema", Hint: "Guarda as cores atuais em Personalização/temas para reaplicar depois ou compartilhar."));
            if (string.IsNullOrWhiteSpace(n)) return;
            try
            {
                var path = await Customization.SaveThemeAsync(n);
                var cfg = (JsonObject)Customization.Config.DeepClone();
                cfg["tema"] = path; cfg["cores"] = new JsonObject();
                await Customization.SaveAsync(cfg);
                Toast("Tema salvo");
            }
            catch (Exception e) when (e is InvalidOperationException or IOException) { await Dialogs.AlertAsync("Tema", e.Message); }
        });
        _body.Children.Add(BlockWith("Tema", save, cards));

        var sw = new WrapPanel();
        foreach (var a in Accents)
        {
            bool on = pal.Colors["destaque"] == a;
            var b = new Button
            {
                Width = 38, Height = 38, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(0), CornerRadius = new CornerRadius(19), Background = new SolidColorBrush(Color.Parse(a)),
                BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Text : UrbeTheme.Line2), BorderThickness = new Thickness(on ? 2 : 1)
            };
            AutomationName(b, "Destaque " + a);
            b.Click += async (_, _) => await Set("cores.destaque", JsonValue.Create(a));
            sw.Children.Add(b);
        }
        _body.Children.Add(Block("Cor de destaque", sw));

        var rows = new List<Control>();
        foreach (var k in _moreColors ? LegacyCustomize.Colors : MainColors) rows.Add(ColorRow(k, pal.Colors[k], own[k] is not null));
        rows.Add(More(_moreColors ? "Menos cores" : "Todas as cores (" + LegacyCustomize.Colors.Length + ")", null, () => { _moreColors = !_moreColors; Render(); }));
        _body.Children.Add(BlockWith("Cores", own.Count > 0 ? Link("Restaurar", Icons.Restore, async () => await Customization.ResetSectionAsync("cores")) : null, [.. rows]));

        var forma = Sec("forma");
        _body.Children.Add(Block("Forma",
            Row("Cantos", null, Seg("forma.cantos", S(forma, "cantos", "suaves"), [("retos", "Retos"), ("suaves", "Suaves"), ("redondos", "Redondos")])),
            Row("Densidade", "Espaço entre itens de listas e menus", Seg("forma.densidade", S(forma, "densidade", "normal"), [("compacta", "Compacta"), ("normal", "Normal"), ("confortavel", "Ampla")])),
            Toggle("forma.vidro", Bo(forma, "vidro", true), "Efeito de vidro", "Desfoque atrás de barras e painéis. Desligar deixa celulares simples mais leves. (O app nativo não usa desfoque; a escolha vale na versão web.)"),
            Row("Animações", null, Seg("animacoes", S(c, "animacoes", "sistema"), [("sistema", "Do sistema"), ("ligadas", "Ligadas"), ("reduzidas", "Reduzidas")]))));
    }

    // ---------------------------------------------------------------- Texto, Editor

    private Control FontTiles(string path, string value)
    {
        var grid = new AutoFillPanel(96, 8) { Margin = new Thickness(0, 0, 0, 6) };
        foreach (var f in LegacyCustomizeTheme.FontData)
        {
            bool on = value == f.Id;
            var content = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(new TextBlock { Text = "Aa", FontFamily = UrbeAppearance.ToFamily(f.Css), FontSize = 24, Foreground = UrbeTheme.Brush(UrbeTheme.Text), HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(new TextBlock { Text = f.Name, FontFamily = UrbeTheme.UiFont, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center });
            grid.Children.Add(FontTile(content, on, async () => await Set(path, JsonValue.Create(f.Id)), f.Name));
        }
        bool custom = !LegacyCustomizeTheme.FontData.Any(f => f.Id == value);
        var other = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        other.Children.Add(UrbeTheme.Icon(Icons.Plus, 22, UrbeTheme.Text));
        other.Children.Add(new TextBlock { Text = custom ? value : "Outra…", FontFamily = UrbeTheme.UiFont, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center });
        grid.Children.Add(FontTile(other, custom, async () =>
        {
            var v = await Dialogs.PromptAsync(new PromptOptions("Outra fonte", "Nome da fonte instalada", "", Hint: "Ex.: Inter, Georgia, \"Fira Sans\". Ela precisa existir no aparelho."));
            if (!string.IsNullOrWhiteSpace(v)) await Set(path, JsonValue.Create(v.Trim()));
        }, "Outra fonte"));
        return grid;
    }

    private static Button FontTile(Control content, bool on, Func<Task> click, string name)
    {
        var b = new Button
        {
            Content = content, MinHeight = 74, Padding = new Thickness(6, 8), CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center, Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Line2), BorderThickness = new Thickness(on ? 2 : 1)
        };
        AutomationName(b, name);
        b.Click += async (_, _) => await click();
        return b;
    }

    private void TextSection()
    {
        var t = Sec("texto");
        _body.Children.Add(Block("Fonte da interface", FontTiles("texto.fonte", S(t, "fonte", "sistema")), RangeRow("texto.tamanho", N(t, "tamanho", 15), 12, 22, 1, "Tamanho", "px")));
        var sample = new Border
        {
            Margin = new Thickness(0, 12, 0, 0), Padding = new Thickness(16, 14), CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Bg),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    SampleText("Exemplo de nota", 1.25, true),
                    SampleText("Uma cidade se lê como um livro: cada casa guarda uma ideia, e as ruas mostram como elas se ligam.", 1, false),
                    new TextBlock { Text = "[[Outra nota]]", FontFamily = UrbeTheme.MonoFont, FontSize = N(t, "tamanhoEditor", 17) * .85, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) }
                }
            }
        };
        TextBlock SampleText(string text, double scale, bool bold) => new()
        {
            Text = text, FontFamily = UrbeTheme.EditorFont, FontSize = N(t, "tamanhoEditor", 17) * scale, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap,
            LineHeight = N(t, "tamanhoEditor", 17) * scale * N(t, "alturaLinha", 1.7), Foreground = UrbeTheme.Brush(UrbeTheme.Text)
        };
        _body.Children.Add(Block("Fonte das notas", FontTiles("texto.fonteEditor", S(t, "fonteEditor", "sistema")),
            RangeRow("texto.tamanhoEditor", N(t, "tamanhoEditor", 17), 13, 28, 1, "Tamanho", "px"),
            RangeRow("texto.alturaLinha", N(t, "alturaLinha", 1.7), 1.2, 2.2, .05, "Altura da linha", ""), sample));
    }

    private void EditorSection()
    {
        var e = Sec("editor");
        _body.Children.Add(Block("Editor de notas",
            Row("Largura do texto", "Em telas grandes", Seg("editor.largura", S(e, "largura", "media"), [("estreita", "Estreita"), ("media", "Média"), ("larga", "Larga"), ("total", "Toda")])),
            Row("Abrir notas no modo", "Visual mostra formatado; Texto mostra o Markdown", Seg("editor.modoInicial", S(e, "modoInicial", "visual"), [("visual", "Visual"), ("texto", "Texto")])),
            Toggle("editor.ortografia", Bo(e, "ortografia", true), "Corretor ortográfico", "Sublinha palavras com erro enquanto você escreve")));
    }

    // ---------------------------------------------------------------- Cidade → Cores do chão

    private Border GroundColorsBlock()
    {
        var pal = Sec("cidade")["paleta"] as JsonObject ?? [];
        var inv = new Dictionary<string, string>();
        foreach (var k in LegacyCustomize.Keys(pal))
            foreach (var id in LegacyCustomize.BiomeId(k) ?? [])
                if (pal[k] is JsonArray a && a.Count > 0) inv[id] = a[0]!.GetValue<string>();
        var grid = new AutoFillPanel(150, 8);
        foreach (var (id, name) in LegacyCustomizeTheme.BiomeNameData)
        {
            bool own = inv.ContainsKey(id);
            var value = inv.GetValueOrDefault(id) ?? LegacyWorldPixelTextures.DefaultPalette(id)[0];
            var row = new DockPanel();
            var pick = ColorEditor.Button(value, name,
                h => Customization.Preview(Patch("cidade.paleta", new JsonObject { [PortugueseBiome[id]] = new JsonArray(h) })),
                async h => await Customization.SetAsync(Patch("cidade.paleta", new JsonObject { [PortugueseBiome[id]] = new JsonArray(h) })), 36, 36, 8);
            pick.Margin = new Thickness(0, 0, 10, 0);
            DockPanel.SetDock(pick, Dock.Left);
            row.Children.Add(pick);
            if (own)
            {
                var reset = Mini(Icons.Restore, "Cor original", async () =>
                {
                    var c = (JsonObject)Customization.Config.DeepClone();
                    var p = (JsonObject)((JsonObject)c["cidade"]!)["paleta"]!;
                    foreach (var k in p.Select(x => x.Key).ToList()) if ((LegacyCustomize.BiomeId(k) ?? []).Contains(id)) p.Remove(k);
                    await Customization.SaveAsync(c);
                }, 30);
                DockPanel.SetDock(reset, Dock.Right);
                row.Children.Add(reset);
            }
            row.Children.Add(Label(name, 13, FontWeight.SemiBold));
            grid.Children.Add(new Border { Child = row, Padding = new Thickness(4, 4, 6, 4), MinHeight = 46, CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(own ? UrbeTheme.Accent : UrbeTheme.Line), BorderThickness = new Thickness(1) });
        }
        var restore = pal.Count > 0 ? Link("Restaurar", Icons.Restore, async () =>
        {
            var c = (JsonObject)Customization.Config.DeepClone();
            ((JsonObject)c["cidade"]!)["paleta"] = new JsonObject();
            await Customization.SaveAsync(c);
        }) : null;
        return BlockWith("Cores do chão", restore, Help("Toque num bioma para trocar a cor. Para desenhar o chão pixel a pixel, use Texturas."), grid);
    }

    // ---------------------------------------------------------------- toast

    /// <summary>toast(m): a message at the bottom for two seconds.</summary>
    public void Toast(string message)
    {
        if (_toast is null)
        {
            _toast = new Border
            {
                Background = UrbeTheme.Brush(UrbeTheme.Surface3), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(999),
                Padding = new Thickness(16, 10), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 28), IsHitTestVisible = false
            };
            Children.Add(_toast);
        }
        _toast.Child = Label(message, 14, FontWeight.SemiBold);
        _toast.IsVisible = true;
        ToastText = message;
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastTimer.Tick += (_, _) => { _toast.IsVisible = false; _toastTimer.Stop(); };
        _toastTimer.Start();
    }

    /// <summary>The last toast message (tests and accessibility).</summary>
    public string ToastText { get; private set; } = "";

    // ---------------------------------------------------------------- Texturas

    private (string Path, JsonObject? Data, string Content)? ActivePack()
    {
        var p = Customization.ResolvePack((Customization.Config["cidade"] as JsonObject)?["texturas"]?.GetValue<string>());
        if (p is null) return null;
        JsonObject? data = null;
        try { data = LegacyCustomize.ParseJs(p.Value.Content) as JsonObject; } catch (System.Text.Json.JsonException) { }
        return (p.Value.Path, data, p.Value.Content);
    }

    private async Task SavePack((string Path, JsonObject? Data, string Content) pack, JsonObject data)
    {
        try { await Customization.UpsertAsync(pack.Path, LegacyCustomize.Stringify(data, 2) + "\n"); }
        catch (InvalidOperationException e) { await Dialogs.AlertAsync("Texturas", e.Message); }
    }

    private static string? GroundKey(JsonObject d, string id) =>
        (d["chao"] as JsonObject)?.Select(x => x.Key).FirstOrDefault(k => (LegacyCustomize.BiomeId(k) ?? []).Length == 1 && (LegacyCustomize.BiomeId(k) ?? []).Contains(id));

    private static string? BuildingKey(JsonObject d, string id) =>
        (d["construcoes"] as JsonObject)?.Select(x => x.Key).FirstOrDefault(k => LegacyTexturePack.BuildingId(k) == id);

    private void TexturesSection()
    {
        var active = (Customization.Config["cidade"] as JsonObject)?["texturas"]?.GetValue<string>() ?? "";
        var packRow = new WrapPanel();
        Button PackButton(string title, string sub, bool on, Func<Task> click, bool dashed = false)
        {
            var content = new StackPanel { Spacing = 2 };
            content.Children.Add(Label(title, 14, FontWeight.SemiBold, dashed ? UrbeTheme.Accent : UrbeTheme.Text));
            if (sub.Length > 0) content.Children.Add(new TextBlock { Text = sub, FontFamily = UrbeTheme.UiFont, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 160 });
            var b = new Button
            {
                Content = content, MinWidth = 120, MinHeight = 56, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(12), HorizontalContentAlignment = HorizontalAlignment.Left,
                VerticalContentAlignment = VerticalAlignment.Center, Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Line2), BorderThickness = new Thickness(on ? 2 : 1)
            };
            AutomationName(b, title);
            b.Click += async (_, _) => await click();
            return b;
        }
        packRow.Children.Add(PackButton("Nenhum", "arte original", active.Length == 0, () => Set("cidade.texturas", JsonValue.Create(""))));
        foreach (var p in Customization.Packs()) packRow.Children.Add(PackButton(p.Name, p.Path.Split('/').Last(), p.Active, () => Set("cidade.texturas", JsonValue.Create(p.Path))));
        packRow.Children.Add(PackButton("Novo pacote", "", false, NewPackAsync, true));

        var pk = ActivePack();
        if (pk is null)
        {
            _body.Children.Add(Block("Pacote de texturas", packRow, Help("Um pacote guarda desenhos próprios para o chão de cada bioma e para as construções. Crie um e desenhe pixel a pixel, ou use imagens do aparelho.")));
            return;
        }
        if (pk.Value.Data is null)
        {
            var open = SmallButton("Abrir arquivo", false);
            open.HorizontalAlignment = HorizontalAlignment.Left;
            var path = pk.Value.Path;
            open.Click += (_, _) => { Close(); RequestOpen(path); };
            _body.Children.Add(Block("Pacote de texturas", packRow, Banner("err", "O arquivo " + path + " não é um JSON válido. Abra-o para corrigir."), open));
            return;
        }
        var data = pk.Value.Data;
        var chao = data["chao"] as JsonObject ?? [];
        var ground = new AutoFillPanel(104, 10);
        foreach (var id in GroundIds)
        {
            bool own = chao.Any(x => (LegacyCustomize.BiomeId(x.Key) ?? []).Contains(id));
            int index = LegacyWorldPixelTextures.Biomes.ToList().IndexOf(id);
            var preview = PixelImage(Customization.Ground.Textures[index][0], 16, 16, 64, 64);
            ground.Children.Add(TextureCell(LegacyCustomizeTheme.BiomeName(id), preview, own,
                () => _ = DrawGroundAsync(id), () => _ = ImageAsync(id, false), own ? () => _ = ClearAsync(id, false) : null));
        }
        var cons = data["construcoes"] as JsonObject ?? [];
        var buildings = new AutoFillPanel(104, 10);
        foreach (var (id, name) in LegacyCustomizeTheme.BuildingNameData)
        {
            bool own = cons.Any(x => LegacyTexturePack.BuildingId(x.Key) == id);
            var art = BuildingPixels(id);
            buildings.Children.Add(TextureCell(name, PixelImage(art.Rgba, art.Width, art.Height, 60, 70), own,
                () => _ = DrawBuildingAsync(id), () => _ = ImageAsync(id, true), own ? () => _ = ClearAsync(id, true) : null));
        }
        var json = Link("Ver JSON", Icons.Code, () => { Close(); RequestOpen(pk.Value.Path); });
        _body.Children.Add(BlockWith("Pacote de texturas", json, packRow));
        if (Customization.GroundErrors.Count > 0) _body.Children.Add(Block("Avisos do pacote", Issues(Customization.GroundErrors.Select(e => new LegacyCustomize.Issue("", e)))));
        _body.Children.Add(Block("Chão", Help("Cada bioma é um quadrado de 16×16 pixels que se repete pelo mapa."), ground));
        _body.Children.Add(Block("Construções", Help("Desenho de 48×56 pixels, como as casas originais. Imagens de outro tamanho são ajustadas."), buildings));
    }

    /// <summary>The picture a building has now (pack image or the original art on the grass style).</summary>
    private LegacyTexturePack.Art BuildingPixels(string id)
    {
        if (Customization.BuildingArt.TryGetValue(id, out var own)) return own;
        return new LegacyTexturePack.Art(LegacyWorldBuildings.CreateBuilding(id, LegacyWorldBuildings.StyleForBiome("grass"), 0, false), LegacyWorldBuildings.Width, LegacyWorldBuildings.Height);
    }

    private static Control PixelImage(byte[] rgba, int w, int h, double width, double height)
    {
        var img = new Image { Source = Pixels.ToBitmap(rgba, w, h), Width = width, Height = height, Stretch = Stretch.Fill };
        RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.None);
        return new Border { Child = img, CornerRadius = new CornerRadius(6), ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center };
    }

    private Border TextureCell(string name, Control preview, bool own, Action draw, Action image, Action? clear)
    {
        var act = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        act.Children.Add(Mini(Icons.Pencil, "Desenhar " + name, draw, 34));
        act.Children.Add(Mini(Icons.Image, "Imagem para " + name, image, 34));
        if (clear is not null) act.Children.Add(Mini(Icons.Restore, "Voltar ao original", clear, 34));
        var stack = new StackPanel { Spacing = 6, Children = { preview, new TextBlock { Text = name, FontFamily = UrbeTheme.UiFont, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap }, act } };
        return new Border { Child = stack, Padding = new Thickness(8), CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(own ? UrbeTheme.Accent : UrbeTheme.Line), BorderThickness = new Thickness(1) };
    }

    private async Task NewPackAsync()
    {
        var name = await Dialogs.PromptAsync(new PromptOptions("Novo pacote de texturas", "Nome", "Meu pacote", Confirm: "Criar"));
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim();
        var p = Customization.PackFolder + System.Text.RegularExpressions.Regex.Replace(name, @"[\\/:*?""<>|]", "-") + ".json";
        for (int i = 2; Customization.Documents.ContainsKey(p); i++) p = System.Text.RegularExpressions.Regex.Replace(p, @"( \d+)?\.json$", " " + i + ".json");
        try
        {
            await Customization.UpsertAsync(p, LegacyCustomize.Stringify(new JsonObject { ["nome"] = name, ["chao"] = new JsonObject(), ["construcoes"] = new JsonObject() }, 2) + "\n");
            await Set("cidade.texturas", JsonValue.Create(p));
            Toast("Pacote criado e ativado");
        }
        catch (InvalidOperationException e) { await Dialogs.AlertAsync("Texturas", e.Message); }
    }

    private async Task ClearAsync(string id, bool building)
    {
        var pk = ActivePack();
        if (pk?.Data is not { } d) return;
        if (building)
        {
            if (d["construcoes"] is not JsonObject cons) return;
            foreach (var k in cons.Select(x => x.Key).ToList()) if (LegacyTexturePack.BuildingId(k) == id) cons.Remove(k);
        }
        else
        {
            if (d["chao"] is not JsonObject chao) return;
            if (GroundKey(d, id) is { } k) chao.Remove(k);
        }
        await SavePack(pk.Value, d);
    }

    private static string[] FromRgba(byte[] data, int w, int h)
    {
        var o = new string[w * h];
        for (int k = 0; k < o.Length; k++) o[k] = data[k * 4 + 3] < 128 ? "" : "#" + data[k * 4].ToString("x2") + data[k * 4 + 1].ToString("x2") + data[k * 4 + 2].ToString("x2");
        return o;
    }

    private static List<string> ColorsOf(string[] px)
    {
        var count = new Dictionary<string, int>();
        foreach (var c in px) if (c.Length > 0) count[c] = count.GetValueOrDefault(c) + 1;
        return count.OrderByDescending(x => x.Value).Take(8).Select(x => x.Key).ToList();   // stable, like Array.sort
    }

    /// <summary>toGrid(px, w, h): the pixels as {cores, pixels} with one letter per colour.</summary>
    public static JsonObject ToGrid(string[] px, int w, int h)
    {
        const string letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var map = new Dictionary<string, char>();
        var cores = new JsonObject();
        var rows = new JsonArray();
        for (int j = 0; j < h; j++)
        {
            var r = new System.Text.StringBuilder();
            for (int i = 0; i < w; i++)
            {
                var c = px[j * w + i];
                if (c.Length == 0) { r.Append('.'); continue; }
                if (!map.TryGetValue(c, out var l))
                {
                    if (map.Count >= letters.Length) { r.Append(r.Length > 0 ? r[^1] : '.'); continue; }
                    l = letters[map.Count]; map[c] = l; cores[l.ToString()] = c;
                }
                r.Append(l);
            }
            rows.Add(r.ToString());
        }
        return new JsonObject { ["cores"] = cores, ["pixels"] = rows };
    }

    private async Task DrawGroundAsync(string id)
    {
        var pk = ActivePack();
        if (pk?.Data is not { } d) return;
        int index = LegacyWorldPixelTextures.Biomes.ToList().IndexOf(id);
        var px = FromRgba(Customization.Ground.Textures[index][0], 16, 16);
        var baseColors = Customization.Ground.Palette[id];
        var colors = baseColors.Concat(ColorsOf(px)).Distinct().ToList();
        var edited = await EditPixelsAsync("Chão: " + LegacyCustomizeTheme.BiomeName(id), 16, 16, px, colors);
        if (edited is null) return;
        edited = edited.Select(c => c.Length > 0 ? c : baseColors[0]).ToArray();
        var chao = d["chao"] as JsonObject ?? new JsonObject();
        d["chao"] = chao;
        if (GroundKey(d, id) is { } k) chao.Remove(k);
        chao[PortugueseBiome[id]] = ToGrid(edited, 16, 16);
        await SavePack(pk.Value, d);
        Toast("Textura salva");
    }

    private async Task DrawBuildingAsync(string id)
    {
        var pk = ActivePack();
        if (pk?.Data is not { } d) return;
        var art = BuildingPixels(id);
        var src = new LegacyTexturePack.DecodedImage(art.Rgba, art.Width, art.Height);
        var px = FromRgba(TextureImages.Resize(src, 48, 56, false), 48, 56);
        var edited = await EditPixelsAsync(LegacyCustomizeTheme.BuildingNameData.First(b => b.Id == id).Name, 48, 56, px, ColorsOf(px).Concat(["#000000", "#ffffff"]).ToList());
        if (edited is null) return;
        var cons = d["construcoes"] as JsonObject ?? new JsonObject();
        d["construcoes"] = cons;
        foreach (var k in cons.Select(x => x.Key).ToList()) if (LegacyTexturePack.BuildingId(k) == id) cons.Remove(k);
        cons[PortugueseBuilding[id]] = ToGrid(edited, 48, 56);
        await SavePack(pk.Value, d);
        Toast("Construção salva");
    }

    private async Task ImageAsync(string id, bool building)
    {
        var pk = ActivePack();
        if (pk?.Data is not { } d) return;
        var bytes = await PickImageFileAsync();
        if (bytes is null) return;
        var url = TextureImages.ReduceToDataUrl(bytes, building ? 96 : 16, building ? 112 : 16);
        if (url is null) { await Dialogs.AlertAsync("Imagem", "Não consegui ler essa imagem."); return; }
        if (building)
        {
            var cons = d["construcoes"] as JsonObject ?? new JsonObject();
            d["construcoes"] = cons;
            foreach (var k in cons.Select(x => x.Key).ToList()) if (LegacyTexturePack.BuildingId(k) == id) cons.Remove(k);
            cons[PortugueseBuilding[id]] = new JsonObject { ["imagem"] = url };
        }
        else
        {
            var chao = d["chao"] as JsonObject ?? new JsonObject();
            d["chao"] = chao;
            if (GroundKey(d, id) is { } k) chao.Remove(k);
            chao[PortugueseBiome[id]] = new JsonObject { ["imagem"] = url };
        }
        await SavePack(pk.Value, d);
        Toast(building ? "Construção trocada" : "Textura trocada");
    }

    private async Task<byte[]?> PickImageFileAsync()
    {
        if (ImagePicker is not null) return await ImagePicker();
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return null;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, FileTypeFilter = [FilePickerFileTypes.ImageAll] });
        if (files.Count == 0) return null;
        await using var s = await files[0].OpenReadAsync();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        return ms.ToArray();
    }

    /// <summary>Replaces the platform file picker (tests).</summary>
    public Func<Task<byte[]?>>? ImagePicker { get; set; }

    /// <summary>editorPixels(): the modal pixel editor; null when cancelled.</summary>
    public Task<string[]?> EditPixelsAsync(string title, int w, int h, string[] initial, IReadOnlyList<string> colors)
    {
        var editor = new PixelEditor(title, w, h, initial, colors);
        Children.Add(editor);
        Editor = editor;
        return editor.Result;
    }

    /// <summary>The pixel editor currently open (tests).</summary>
    public PixelEditor? Editor { get; private set; }

    private void RequestOpen(string path) => OpenFileRequested?.Invoke(path);

    // ---------------------------------------------------------------- Estilos CSS

    private void StylesSection()
    {
        var c = Customization.Config;
        var list = Customization.Styles();
        if (Customization.SafeMode) _body.Children.Add(Banner("warn", "Modo seguro ligado: estilos e plugins estão desativados."));
        _body.Children.Add(Banner("warn", "O app nativo não tem CSS: os estilos e o CSS rápido ficam guardados no tema.json e valem na versão web do Urbe (1.8.4). Aqui eles não mudam a aparência; use Aparência, Texto e Editor."));
        var items = new List<Control>();
        if (list.Count == 0) items.Add(Help("Nenhum estilo ainda. Estilos são arquivos .css na pasta Personalização/estilos que mudam qualquer detalhe visual."));
        foreach (var s in list)
        {
            var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(Label(s.Name, 14.5, FontWeight.SemiBold));
            text.Children.Add(new TextBlock { Text = s.Path, FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap });
            var act = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            act.Children.Add(Mini(Icons.Edit, "Editar", () => { Close(); RequestOpen(s.Path); }));
            var sw = Switch(s.On);
            sw.Cursor = new Cursor(StandardCursorType.Hand);
            AutomationName(sw, "Ligar " + s.Name);
            sw.PointerReleased += async (_, e) => { e.Handled = true; await Set("estilos", new JsonObject { [s.Path] = !s.On }); };
            act.Children.Add(sw);
            var dock = new DockPanel();
            DockPanel.SetDock(act, Dock.Right);
            dock.Children.Add(act);
            dock.Children.Add(text);
            items.Add(new Border { Tag = "row", Child = dock, MinHeight = 52, Padding = new Thickness(0, 6), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line) });
        }
        items.Add(More("Novo estilo", Icons.Plus, async () => { await NewStyleAsync(); Render(); }));
        _body.Children.Add(Block("Estilos", [.. items]));
        var css = new TextBox
        {
            Text = S(c, "css"), AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MinHeight = 7 * 20, FontFamily = UrbeTheme.MonoFont, FontSize = 13, PlaceholderText = "/* suas regras */",
            Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12)
        };
        var apply = SmallButton("Aplicar", true);
        apply.Click += async (_, _) => { await Set("css", JsonValue.Create(css.Text ?? "")); Toast("CSS aplicado"); };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { apply } };
        _body.Children.Add(Block("CSS rápido", Help("Regras aplicadas por último, guardadas no tema.json. Ex.: #renderedPreview h1 { color: var(--ui-accent) }"), css, actions));
    }

    private async Task NewStyleAsync()
    {
        var name = await Dialogs.PromptAsync(new PromptOptions("Novo estilo", "Nome do arquivo", "meu-estilo", Confirm: "Criar"));
        if (string.IsNullOrWhiteSpace(name)) return;
        var p = LegacyCustomize.Folder + "/estilos/" + System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\.css$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase), @"[\\/:*?""<>|]", "-") + ".css";
        try
        {
            if (!Customization.Documents.ContainsKey(p))
                await Customization.UpsertAsync(p, "/* " + name.Trim() + " — estilo do Urbe.\n   Variáveis: --ui-accent, --ui-bg, --ui-surface, --ui-text, --ui-font, --ui-editor-font, --ui-r-m...\n   Veja o Tutorial, em Personalização. */\n\n/* títulos das notas na cor de destaque */\n#renderedPreview h1 { color: var(--ui-accent); }\n");
            await Set("estilos", new JsonObject { [p] = true });
            Toast("Estilo criado e ligado");
        }
        catch (InvalidOperationException e) { await Dialogs.AlertAsync("Estilos", e.Message); }
    }

    // ---------------------------------------------------------------- Plugins

    private void PluginsSection()
    {
        var states = new Dictionary<string, (string Label, string Kind)>
        {
            ["aprovado"] = ("Aprovado · não executa no nativo", "warn"), ["desligado"] = ("Desligado", ""), ["mudou"] = ("Código mudou", "warn"), ["seguro"] = ("Modo seguro", "warn"),
            ["invalido"] = ("Com erros", "err")
        };
        _body.Children.Add(Banner("warn", "Plugins são programas: podem ler e mudar suas notas. Só ligue código que você entende ou que veio de alguém de confiança. Se o arquivo mudar, o Urbe desliga o plugin até você revisar."));
        // the native plugins (ADD-0021): each declares what it may do, and nothing else is allowed
        if (NativePlugins?.Invoke() is { Count: > 0 } natives)
        {
            var rows = new List<Control>();
            foreach (var (m, on) in natives)
            {
                var perms = string.Join(" · ", m.Permissions.Select(x => PluginPermissions.Labels.GetValueOrDefault(x, x)));
                rows.Add(Toggle(on && !Customization.SafeMode, m.Name + " v" + m.Version, m.Description + " Pode: " + perms + ".", v =>
                {
                    SetNativePlugin?.Invoke(m.Id, v);
                    Render();
                    return Task.CompletedTask;
                }));
            }
            _body.Children.Add(Block("Plugins nativos", [.. rows]));
        }
        _body.Children.Add(Banner("warn", "Plugins .plugin.json (os que você ou o Assistente criam) rodam no app depois de ligados, e só fazem o que declaram. Os .js da 1.8.4 não rodam no app nativo (DEC-0044): aqui você só os vê e aprova."));
        var items = new List<Control>();
        var list = Customization.Plugins();
        if (list.Count == 0) items.Add(Help("Nenhum plugin nesta cidade. Crie um a partir de um modelo e veja o código funcionando."));
        foreach (var p in list)
        {
            var native = Urbe.Core.Plugins.VaultPluginSpec.IsPluginPath(p.Path);
            var (label, kind) = native && p.Status == "aprovado" ? ("Ligado", "ok") : states.GetValueOrDefault(p.Status, ("Desligado", ""));
            var name = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            name.Children.Add(Label(p.Name, 14.5, FontWeight.SemiBold));
            if (p.Version.Length > 0) name.Children.Add(new TextBlock { Text = "v" + p.Version, FontFamily = UrbeTheme.MonoFont, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center });
            var text = new StackPanel { Spacing = 2, Children = { name, new TextBlock { Text = p.Description.Length > 0 ? p.Description : p.Path, FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap } } };
            var head = new DockPanel();
            var pill = Pill(label, kind);
            DockPanel.SetDock(pill, Dock.Right);
            head.Children.Add(pill);
            head.Children.Add(text);
            var act = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var view = Link("Ver código", Icons.Code, () => { Close(); RequestOpen(p.Path); });
            act.Children.Add(view);
            Button Pb(string t, bool primary, Func<Task> run) { var b = SmallButton(t, primary); b.Margin = new Thickness(8, 0, 0, 0); b.MinHeight = 40; b.Click += async (_, _) => await run(); return b; }
            if (p.Status == "mudou") { act.Children.Add(Pb("Revisei, ligar", true, () => ApprovePluginAsync(p.Path))); act.Children.Add(Pb("Desligar", false, () => { Customization.DisablePlugin(p.Path); Render(); return Task.CompletedTask; })); }
            else if (p.Status is "aprovado" or "seguro") act.Children.Add(Pb("Desligar", false, () => { Customization.DisablePlugin(p.Path); Render(); return Task.CompletedTask; }));
            else if (p.Status != "invalido") act.Children.Add(Pb("Ligar", true, () => ApprovePluginAsync(p.Path)));
            var stack = new StackPanel { Children = { head } };
            if (p.Error is not null) stack.Children.Add(Banner("err", p.Error));
            stack.Children.Add(act);
            items.Add(new Border { Child = stack, Padding = new Thickness(0, 12), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line) });
        }
        items.Add(More("Novo plugin", Icons.Plus, async () => await NewPluginAsync()));
        _body.Children.Add(Block("Plugins desta cidade", [.. items]));
        _body.Children.Add(Block("Segurança", Toggle(Customization.SafeMode, "Modo seguro", "Desliga todos os plugins e estilos, sem apagar nada. Útil se algo quebrar a tela.", v => { Customization.SafeMode = v; return Task.CompletedTask; })));
    }

    private async Task ApprovePluginAsync(string path)
    {
        if (Urbe.Core.Plugins.VaultPluginSpec.IsPluginPath(path))
        {
            var spec = Customization.Documents.TryGetValue(path, out var text) ? Urbe.Core.Plugins.VaultPluginSpec.Parse(path, text).Spec : null;
            if (spec is null) return;
            var perms = spec.Declared.Count == 0 ? "nada além de mostrar o próprio painel" : string.Join(", ", spec.Declared.Select(x => PluginPermissions.Labels.GetValueOrDefault(x, x).ToLowerInvariant()));
            var go = await Dialogs.ChooseAsync("Ligar " + spec.Name + "?", [new DialogItem("Ligar"), new DialogItem("Cancelar")],
                "Este plugin pode: " + perms + ". Ele roda neste aparelho enquanto o arquivo não mudar; se mudar, desliga até você ligar de novo.");
            if (go != 0) return;
            Customization.EnablePlugin(path);
            Render();
            Toast("Plugin ligado: " + spec.Name);
            return;
        }
        var ok = await Dialogs.ChooseAsync("Ligar plugin?", [new DialogItem("Ligar"), new DialogItem("Cancelar")],
            "Registrar a aprovação de " + path.Split('/').Last() + " neste aparelho? O código é de confiança só enquanto não mudar. O app nativo não executa plugins; a aprovação vale quando a cidade for aberta na versão web.");
        if (ok != 0) return;
        Customization.EnablePlugin(path);
        Render();
        Toast("Plugin aprovado (não executa no nativo)");
    }

    private async Task NewPluginAsync()
    {
        var m = await Dialogs.ChooseAsync("Novo plugin",
            [new DialogItem("Plugin nativo (cronômetro)", Icons.Puzzle, "Roda no app: um painel com cronômetro e voltas. Edite o .plugin.json ou peça ao Assistente outro plugin."),
             .. LegacyPlugins.Templates.Select(t => new DialogItem(t.Name + " (.js da 1.8.4)", Icons.Puzzle, t.Description))],
            "Escolha um modelo para começar. O arquivo fica em Personalização/plugins e você pode editar à vontade.");
        if (m is not { } choice) return;
        if (choice == 0)
        {
            var nativeName = await Dialogs.PromptAsync(new PromptOptions("Nome do plugin", Value: "Cronômetro", Confirm: "Criar"));
            if (string.IsNullOrWhiteSpace(nativeName)) return;
            try
            {
                var path = await Customization.CreateVaultPluginAsync(nativeName);
                Toast("Plugin criado: " + path.Split('/').Last() + " (desligado)");
                Render();
            }
            catch (InvalidOperationException e) { await Dialogs.AlertAsync("Plugins", e.Message); }
            return;
        }
        var i = choice - 1;
        var tpl = LegacyPlugins.Templates[i];
        var name = await Dialogs.PromptAsync(new PromptOptions("Nome do plugin", Value: tpl.Name, Confirm: "Criar"));
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var path = await Customization.CreatePluginAsync(name, tpl.Id);
            Toast("Plugin criado: " + path.Split('/').Last() + " (desligado)");
            Render();
        }
        catch (InvalidOperationException e) { await Dialogs.AlertAsync("Plugins", e.Message); }
    }
}
