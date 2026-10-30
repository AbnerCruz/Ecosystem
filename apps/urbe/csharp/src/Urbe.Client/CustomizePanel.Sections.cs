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

/// <summary>The sections of src/customize/panel.js beyond Arquivo: Aparência, Texto, Editor, Estilos CSS and Plugins.</summary>
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
                    SampleText("Um vault se lê como um livro: cada nota guarda uma ideia, e os links mostram como elas se ligam.", 1, false),
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
            Toggle("editor.ortografia", Bo(e, "ortografia", true), "Corretor ortográfico", "Sublinha palavras com erro enquanto você escreve"),
            Toggle(Customization.BlockTimes, "Horário de cada bloco", "Mostra, ao lado de cada bloco, quando ele mudou pela última vez (pelas Versões anteriores)",
                v => { Customization.BlockTimes = v; return Task.CompletedTask; })));
        _body.Children.Add(BarBlock());
    }

    /// <summary>
    /// CST-019: the formatting bar of the editor (the one over the keyboard on the phone), by touch: each button on or off
    /// and in which order, separators included; Desfazer and Refazer always stay first. Kept in Personalização/editor.json
    /// ("barra"), the same file as before, whose "/" menu is left as it is.
    /// </summary>
    private Border BarBlock()
    {
        var ids = EditorOverlay.ToolbarIds;
        var bar = (Customization.EditorBar(ids) ?? EditorOverlay.ToolbarOrder).Where(x => x is not ("undo" or "redo")).ToList();
        while (bar.Count > 0 && bar[0] == "|") bar.RemoveAt(0);
        var off = ids.Where(x => x is not ("undo" or "redo") && !bar.Contains(x)).ToList();

        async Task Save(List<string> next)
        {
            // separators at the ends or side by side are tidied when read; the default comes back as the default
            var full = new List<string> { "undo", "redo", "|" };
            full.AddRange(next);
            try { await Customization.SetEditorBarAsync(full.SequenceEqual(EditorOverlay.ToolbarOrder) ? null : full); }
            catch (InvalidOperationException ex) { await Dialogs.AlertAsync("Barra do editor", ex.Message); }
            Render();
        }
        List<string> Moved(int from, int to)
        {
            var next = bar.ToList();
            (next[from], next[to]) = (next[to], next[from]);
            return next;
        }

        var rows = new List<Control>();
        for (int i = 0; i < bar.Count; i++)
        {
            var at = i;
            var id = bar[i];
            var name = id == "|" ? "Separador" : EditorOverlay.ToolbarTitle(id);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            var up = Mini(UrbeTheme.Icons.ArrowUp, "Subir " + name, () => _ = Save(Moved(at, at - 1)));
            // the arrow that has nowhere to go is hidden, keeping its place so the rows line up
            if (at == 0) { up.IsEnabled = false; up.Opacity = 0; }
            var down = Mini(UrbeTheme.Icons.ArrowDown, "Descer " + name, () => _ = Save(Moved(at, at + 1)));
            if (at == bar.Count - 1) { down.IsEnabled = false; down.Opacity = 0; }
            var remove = Mini(UrbeTheme.Icons.Close, "Tirar " + name + " da barra", () => { var next = bar.ToList(); next.RemoveAt(at); _ = Save(next); });
            actions.Children.Add(up);
            actions.Children.Add(down);
            actions.Children.Add(remove);
            rows.Add(Row(name, null, actions));
        }
        foreach (var id in off)
        {
            var add = Mini(Icons.Plus, "Pôr " + EditorOverlay.ToolbarTitle(id) + " na barra", () => _ = Save([.. bar, id]));
            rows.Add(Row(EditorOverlay.ToolbarTitle(id), "Fora da barra", add));
        }
        // a new separator goes before the last button (one at the end would be tidied away); it can then be moved
        rows.Add(More("Separador", Icons.Plus, () => { var next = bar.ToList(); next.Insert(Math.Max(0, next.Count - 1), "|"); _ = Save(next); }));
        var restore = SmallButton("Padrão", false);
        AutomationName(restore, "Barra padrão");
        restore.Click += async (_, _) => { try { await Customization.SetEditorBarAsync(null); } catch (InvalidOperationException ex) { await Dialogs.AlertAsync("Barra do editor", ex.Message); } Render(); };
        rows.Add(Help("Desfazer e Refazer ficam sempre no começo. O menu “/” continua em Personalização/editor.json."));
        return BlockWith("Barra de formatação", restore, [.. rows]);
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
        if (list.Count == 0) items.Add(Help("Nenhum plugin neste vault. Crie um a partir de um modelo e veja o código funcionando."));
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
        _body.Children.Add(Block("Plugins deste vault", [.. items]));
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
