using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core.Pages;

namespace Urbe.Client;

/// <summary>The field controls (fieldHtml/listHtml), the free layout's editor (fxEditorHtml), the block library and the JSON panel.</summary>
public sealed partial class PageStudioView
{
    private static TextBlock FieldLabel(JsonObject f) => new()
    {
        Text = (string?)f["label"] + (f["required"] is JsonValue r && r.GetValue<bool>() ? " *" : ""), FontSize = 12, FontWeight = FontWeight.SemiBold,
        Foreground = UrbeTheme.Brush(UrbeTheme.Text2), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4)
    };

    private static TextBox Input(string text, bool multi = false, bool mono = false)
    {
        var t = new TextBox
        {
            Text = text, AcceptsReturn = multi, TextWrapping = multi ? TextWrapping.Wrap : TextWrapping.NoWrap, MinHeight = multi ? 72 : 38, MaxHeight = multi ? 420 : double.PositiveInfinity,
            Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(9), FontSize = 14, Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent)
        };
        if (mono) t.FontFamily = new FontFamily("Cascadia Mono, Consolas, Menlo, monospace");
        return t;
    }

    /// <summary>Typing in a field: one undo step per field, the panel kept (the field keeps the focus).</summary>
    private void Typed(string path, JsonNode? value)
    {
        if (_st is null || _loading) return;
        if (!_st.Set(path, value, key: path)) return;
        Changed(panels: false);
        // refreshLabels: what names a section or an item changed — the list on the left follows (the field keeps the focus)
        if (Wide && _leftTab == "sections" && System.Text.RegularExpressions.Regex.IsMatch(path, @"\.(title|name|q|label|author|caption|path|markdown|text)$"))
            _left.Child = Panel("sections");
    }

    /// <summary>A switch, a choice or a button: a step of its own and the panels redrawn.</summary>
    private void Chosen(string path, JsonNode? value)
    {
        if (_st is null) return;
        if (_st.Set(path, value)) Changed();
    }

    /// <summary>fieldHtml(f, v, path).</summary>
    private Control Field(JsonObject f, JsonNode? v, string path)
    {
        var type = (string?)f["type"] ?? "text";
        var label = (string?)f["label"] ?? "";
        var box = new StackPanel();
        string Text() => v is null ? "" : S(v);
        switch (type)
        {
            case "tree":
                return FreeEditor(path, v as JsonObject);
            case "boolean":
            {
                var c = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text) }, IsChecked = PageEngine.Truthy(v) };
                Avalonia.Automation.AutomationProperties.SetName(c, label);
                c.IsCheckedChanged += (_, _) => Chosen(path, JsonValue.Create(c.IsChecked == true));
                return c;
            }
            case "select":
            {
                var opts = f["options"]!.AsArray().Select(o => S(o)).ToList();
                var labs = (f["labels"] as JsonArray)?.Select(o => S(o)).ToList() ?? opts;
                var cur = Text();
                box.Children.Add(FieldLabel(f));
                var key = (string?)f["key"] ?? "";
                if (opts.Count <= (f["group"] is not null ? 5 : 4) && labs.All(l => l.Length <= 12) && !System.Text.RegularExpressions.Regex.IsMatch(key, "Font|preset", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    var seg = new WrapPanel { Orientation = Orientation.Horizontal };
                    for (int k = 0; k < opts.Count; k++)
                    {
                        var o = opts[k];
                        bool on = o == cur;
                        var b = new Button
                        {
                            Content = new TextBlock { Text = labs[k].Length == 0 ? "Padrão" : labs[k], FontSize = 12, Foreground = UrbeTheme.Brush(on ? UrbeTheme.Text : UrbeTheme.Text2) },
                            Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1),
                            Background = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Surface2, on ? .2 : 1), BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Line2)
                        };
                        Avalonia.Automation.AutomationProperties.SetName(b, label + ": " + (labs[k].Length == 0 ? "Padrão" : labs[k]));
                        var original = f["options"]!.AsArray()[k]?.DeepClone();
                        b.Click += (_, _) => Chosen(path, original);
                        seg.Children.Add(b);
                    }
                    box.Children.Add(seg);
                    return box;
                }
                var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 38 };
                for (int k = 0; k < opts.Count; k++) combo.Items.Add(labs[k].Length == 0 || opts[k].Length == 0 ? "Padrão do tema" : labs[k]);
                combo.SelectedIndex = Math.Max(0, opts.IndexOf(cur));
                Avalonia.Automation.AutomationProperties.SetName(combo, label);
                combo.SelectionChanged += (_, _) => { if (combo.SelectedIndex >= 0) Chosen(path, f["options"]!.AsArray()[combo.SelectedIndex]?.DeepClone()); };
                box.Children.Add(combo);
                return box;
            }
            case "number":
            {
                box.Children.Add(FieldLabel(f));
                double min = f["min"] is JsonValue a ? a.GetValue<double>() : 0, max = f["max"] is JsonValue b2 ? b2.GetValue<double>() : 100;
                var t = Input(Text());
                t.PlaceholderText = "auto";
                Avalonia.Automation.AutomationProperties.SetName(t, label);
                t.TextChanged += (_, _) => Typed(path, JsonValue.Create(t.Text ?? ""));
                var hint = new TextBlock { Text = min.ToString(CultureInfo.InvariantCulture) + " a " + max.ToString(CultureInfo.InvariantCulture), FontSize = 11, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) };
                box.Children.Add(t);
                box.Children.Add(hint);
                return box;
            }
            case "color":
            {
                box.Children.Add(FieldLabel(f));
                var t = Input(Text());
                t.PlaceholderText = "do tema";
                Avalonia.Automation.AutomationProperties.SetName(t, label);
                var sw = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = Color(Text().Length > 0 ? Text() : "#888888"), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0) };
                t.TextChanged += (_, _) =>
                {
                    var val = t.Text ?? "";
                    if (val.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(val, @"^(#[0-9a-f]{3,8}|(rgb|hsl)a?\(.+\))$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return;
                    sw.Background = Color(val.Length > 0 ? val : "#888888");
                    Typed(path, JsonValue.Create(val));
                };
                var row = new DockPanel();
                DockPanel.SetDock(sw, Dock.Left);
                row.Children.Add(sw);
                if (Text().Length > 0)
                {
                    var clear = Mini("×", "Voltar ao tema", () => Chosen(path, JsonValue.Create("")));
                    DockPanel.SetDock(clear, Dock.Right);
                    row.Children.Add(clear);
                }
                row.Children.Add(t);
                box.Children.Add(row);
                return box;
            }
            case "list":
                return ListField(f, v as JsonArray ?? [], path);
            case "image":
            {
                box.Children.Add(FieldLabel(f));
                var val = Text();
                var t = Input(val.Length > 300 ? "(imagem guardada na página)" : val);
                t.IsReadOnly = val.Length > 300;
                t.PlaceholderText = "https://… ou escolha";
                Avalonia.Automation.AutomationProperties.SetName(t, label);
                t.TextChanged += (_, _) => { if (!t.IsReadOnly) Typed(path, JsonValue.Create(t.Text ?? "")); };
                var pick = new Button { Content = "Escolher", Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(9), Background = UrbeTheme.Brush(UrbeTheme.Surface2), Margin = new Thickness(6, 0, 0, 0) };
                pick.Click += async (_, _) =>
                {
                    if (await _host.PickImageAsync() is { } url)
                    {
                        if (url.Length > 2_500_000) Toast("Imagem grande: a página ficará pesada.");
                        Chosen(path, JsonValue.Create(url));
                    }
                };
                var row = new DockPanel();
                DockPanel.SetDock(pick, Dock.Right);
                row.Children.Add(pick);
                if (val.Length > 0)
                {
                    var clear = Mini("×", "Remover imagem", () => Chosen(path, JsonValue.Create("")));
                    DockPanel.SetDock(clear, Dock.Right);
                    row.Children.Add(clear);
                }
                row.Children.Add(t);
                box.Children.Add(row);
                return box;
            }
            case "textarea" or "markdown" or "code":
            {
                box.Children.Add(FieldLabel(f));
                var t = Input(Text(), multi: true, mono: type == "code");
                Avalonia.Automation.AutomationProperties.SetName(t, label);
                t.TextChanged += (_, _) => Typed(path, JsonValue.Create(t.Text ?? ""));
                box.Children.Add(t);
                if (type == "markdown") box.Children.Add(new TextBlock { Text = "Markdown: **negrito**, *itálico*, [link](url), [[Nota]], listas e tabelas.", FontSize = 11, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
                return box;
            }
            case "note" or "folder" or "tag":
            {
                box.Children.Add(FieldLabel(f));
                var list = type == "note" ? _host.Notes().Select(n => n.Path).ToList() : type == "folder" ? _host.Folders().ToList() : _host.Tags().ToList();
                var t = new AutoCompleteBox
                {
                    Text = Text(), ItemsSource = list, FilterMode = AutoCompleteFilterMode.ContainsOrdinal, MinimumPrefixLength = 0, MinHeight = 38,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                Avalonia.Automation.AutomationProperties.SetName(t, label);
                t.TextChanged += (_, _) => Typed(path, JsonValue.Create(t.Text ?? ""));
                box.Children.Add(t);
                return box;
            }
            default:
            {
                box.Children.Add(FieldLabel(f));
                var t = Input(Text());
                if (type == "url") t.PlaceholderText = "https://… ou #secao";
                else if (type == "length") t.PlaceholderText = (string?)f["hint"] ?? "Ex.: 16px, 2rem, 50%";
                Avalonia.Automation.AutomationProperties.SetName(t, label);
                t.TextChanged += (_, _) => Typed(path, JsonValue.Create(t.Text ?? ""));
                box.Children.Add(t);
                return box;
            }
        }
    }

    /// <summary>listHtml: the items with ↑ ↓ duplicate remove, each with its fields, and Adicionar.</summary>
    private Control ListField(JsonObject f, JsonArray items, string path)
    {
        var p = new StackPanel { Spacing = 6 };
        p.Children.Add(new TextBlock { Text = (string?)f["label"] + "  " + items.Count, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) });
        var itemLabel = (string?)f["itemLabel"];
        var fields = f["fields"]!.AsArray().OfType<JsonObject>().ToList();
        for (int k = 0; k < items.Count; k++)
        {
            var it = items[k] as JsonObject ?? [];
            var name = itemLabel is not null ? S(it[itemLabel]).Replace("**", "") : "";
            if (name.Length == 0) name = "Item " + (k + 1);
            int index = k;
            var acts = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    Mini("↑", "Subir", () => { _st!.ListOp(path, "up", index); Changed(); }),
                    Mini("↓", "Descer", () => { _st!.ListOp(path, "down", index); Changed(); }),
                    Mini("⧉", "Duplicar", () => { _st!.ListOp(path, "dup", index); Changed(); }),
                    Mini("🗑", "Remover", () => { _st!.ListOp(path, "del", index); Changed(); })
                }
            };
            acts.Children[0].IsEnabled = k > 0;
            acts.Children[1].IsEnabled = k < items.Count - 1;
            var body = new StackPanel { Spacing = 8, Margin = new Thickness(0, 6, 0, 2) };
            foreach (var sf in fields) body.Children.Add(Field(sf, it[(string)sf["key"]!], path + "." + k.ToString(CultureInfo.InvariantCulture) + "." + (string)sf["key"]!));
            var head = new DockPanel();
            DockPanel.SetDock(acts, Dock.Right);
            head.Children.Add(acts);
            head.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = UrbeTheme.Brush(UrbeTheme.Text) });
            var key = path + "#" + k.ToString(CultureInfo.InvariantCulture);
            var e = new Expander { Header = head, Content = body, IsExpanded = _groups.TryGetValue(key, out var open) ? open : items.Count <= 2, HorizontalAlignment = HorizontalAlignment.Stretch };
            e.Expanded += (_, _) => _groups[key] = true;
            e.Collapsed += (_, _) => _groups[key] = false;
            p.Children.Add(e);
        }
        if (items.Count < (f["max"] is JsonValue m ? m.GetValue<int>() : 60))
        {
            var add = new Button { Content = "＋  Adicionar", Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(9), Background = UrbeTheme.Brush(UrbeTheme.Surface2) };
            add.Click += (_, _) => { _groups[path + "#" + items.Count.ToString(CultureInfo.InvariantCulture)] = true; _st!.ListOp(path, "add"); Changed(); };
            p.Children.Add(add);
        }
        return new Border { Child = p, Padding = new Thickness(10), CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(1) };
    }

    // ---------------------------------------------------------------- free layout (fxEditorHtml)

    private Control FreeEditor(string path, JsonObject? root)
    {
        var st = _st!;
        var p = new StackPanel { Spacing = 10 };
        if (root is null || st.Sections.ElementAtOrDefault(int.Parse(path.Split('.')[1], CultureInfo.InvariantCulture)) is not JsonObject sec) return p;
        var secId = (string)sec["id"]!;
        var (n, parent) = st.FreeCurrent(root);
        bool isRoot = ReferenceEquals(n, root);
        // Estilo para: Computador, Tablet, Celular
        var devs = new WrapPanel();
        foreach (var (key, label) in new[] { ("desktop", "Computador"), ("tablet", "Tablet"), ("mobile", "Celular") })
        {
            bool on = st.Device == key;
            var b = new Button
            {
                Content = new TextBlock { Text = label, FontSize = 12, Foreground = UrbeTheme.Brush(on ? UrbeTheme.Text : UrbeTheme.Text2) }, Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 4, 4),
                CornerRadius = new CornerRadius(8), Background = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Surface2, on ? .2 : 1), BorderBrush = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Line2), BorderThickness = new Thickness(1)
            };
            b.Click += (_, _) => { st.Device = key; RenderPanels(); };
            devs.Children.Add(b);
        }
        p.Children.Add(new StackPanel { Children = { new TextBlock { Text = "Estilo para", FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) }, devs,
            Hint(st.Bucket == "style" ? "Vale para todas as telas; tablet e celular podem mudar o que quiserem." : "Só para " + (st.Bucket == "tablet" ? "tablet e celular" : "celular") + ". O que você não mudar aqui vem do computador.") } });
        // Camadas
        var layers = new List<Control>();
        PageEngine.FreeWalk(root, (k, _, depth) =>
        {
            var l = PageStudio.FreeLabel(k);
            bool on = ReferenceEquals(k, n);
            var id = (string)k["id"]!;
            var b = new Button
            {
                Content = new TextBlock { Text = l.Icon + "  " + l.Label + (l.Text.Length > 0 ? " — " + l.Text : ""), FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = UrbeTheme.Brush(on ? UrbeTheme.Text : UrbeTheme.Text2) },
                Margin = new Thickness(depth * 14, 0, 0, 0), Padding = new Thickness(8, 5), CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left, Background = UrbeTheme.Brush(on ? UrbeTheme.Accent : UrbeTheme.Surface, on ? .18 : 1)
            };
            b.Click += (_, _) => { st.NodeSelected = id; RenderPanels(); };
            layers.Add(b);
            return true;
        });
        var layerBox = new StackPanel { Spacing = 2 };
        foreach (var l in layers) layerBox.Children.Add(l);
        Add(p, Group("fx-layers", "Camadas (" + layers.Count + ")", [layerBox], true));
        // tools and actions
        var info = PageEngine.FreeElement((string?)n["type"])!;
        var add = new Button { Content = info.Box ? "＋ Adicionar dentro" : "＋ Adicionar depois", Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(9), Background = UrbeTheme.Brush(UrbeTheme.Surface2) };
        add.Click += async (_, _) =>
        {
            var types = PageEngine.FreeElements;
            var pick = await _host.Dialogs.ChooseAsync("Adicionar elemento", types.Select(t => new DialogItem(t.Icon + "  " + t.Label)).ToList());
            if (pick is null) return;
            st.FreeOp(secId, "add", types[pick.Value].Type);
            Changed();
        };
        var preset = new Button { Content = "Composição pronta", Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(9), Background = UrbeTheme.Brush(UrbeTheme.Surface2) };
        preset.Click += (_, _) => OpenLibrary(only: "comp");
        p.Children.Add(new WrapPanel { Children = { add, new Border { Width = 6 }, preset } });
        var siblings = parent?["children"] as JsonArray;
        int idx = siblings?.IndexOf(n) ?? -1;
        var prev = idx > 0 ? siblings![idx - 1] as JsonObject : null;
        Control Op(string op, string glyph, string label, bool disabled)
        {
            var b = new Button
            {
                Content = new StackPanel { Children = { new TextBlock { Text = glyph, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UrbeTheme.Brush(op == "del" ? UrbeTheme.Danger : UrbeTheme.Text) }, new TextBlock { Text = label, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) } } },
                Width = 52, Padding = new Thickness(2, 4), Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(8), Background = UrbeTheme.Brush(UrbeTheme.Surface2), IsEnabled = !disabled
            };
            Avalonia.Automation.AutomationProperties.SetName(b, label);
            b.Click += (_, _) => { st.FreeOp(secId, op); Changed(); };
            return b;
        }
        bool gpNone = isRoot || parent is null || PageEngine.FreeFind(root, (string?)parent["id"])?.Parent is null;
        p.Children.Add(new WrapPanel
        {
            Children =
            {
                Op("up", "↑", "Subir", isRoot || idx <= 0), Op("down", "↓", "Descer", isRoot || siblings is null || idx >= siblings.Count - 1),
                Op("out", "⇤", "Sair", gpNone), Op("in", "⇥", "Entrar", isRoot || prev is null || PageEngine.FreeElement((string?)prev["type"])?.Box != true),
                Op("wrap", "▢", "Envolver", isRoot), Op("dup", "⧉", "Duplicar", isRoot), Op("del", "🗑", "Apagar", isRoot)
            }
        });
        // content and style of the element
        var nodeId = (string)n["id"]!;
        var content = n["content"] as JsonObject ?? [];
        foreach (var cf in info.Content.OfType<JsonObject>())
            p.Children.Add(Field(cf, content[(string)cf["key"]!], PageStudio.FreePath(secId, nodeId, "content", (string)cf["key"]!)));
        var bucket = n[st.Bucket] as JsonObject ?? [];
        var baseStyle = n["style"] as JsonObject ?? [];
        foreach (var (g, title) in PageEngine.FreeGroups)
        {
            var fs = PageEngine.FreeStyleFields.OfType<JsonObject>().Where(f => (string?)f["group"] == g &&
                (g != "layout" || info.Box || (string?)f["key"] is "self" or "order") && ((string?)f["key"] != "fit" || (string?)n["type"] == "image")).ToList();
            if (fs.Count == 0) continue;
            int overridden = fs.Count(f => bucket[(string)f["key"]!] is not null);
            var controls = fs.Select(f =>
            {
                var key = (string)f["key"]!;
                var c = Field(f, bucket[key], PageStudio.FreePath(secId, nodeId, st.Bucket, key));
                if (st.Bucket != "style" && bucket[key] is null && baseStyle[key] is { } inherited && c is StackPanel sp)
                    sp.Children.Add(new TextBlock { Text = "no computador: " + S(inherited)[..Math.Min(40, S(inherited).Length)], FontSize = 11, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
                return c;
            }).ToList();
            Add(p, Group("fx-" + g, title + (overridden > 0 ? " · " + overridden : ""), controls, g == "layout" && info.Box));
        }
        if (st.Bucket != "style" && bucket.Count > 0)
        {
            var reset = new Button { Content = "Voltar ao estilo do computador", Padding = new Thickness(12, 6), CornerRadius = new CornerRadius(9), Background = UrbeTheme.Brush(UrbeTheme.Surface2) };
            reset.Click += (_, _) => { st.FreeOp(secId, "reset"); Changed(); };
            p.Children.Add(reset);
        }
        return p;
    }

    // ---------------------------------------------------------------- library (Inserir) and JSON

    private void CloseOverlay()
    {
        _overlay.IsVisible = false;
        _overlay.Children.Clear();
    }

    private Border Card(Control content, double maxWidth = 720) => new()
    {
        Child = content, MaxWidth = maxWidth, Margin = new Thickness(12, 40), Padding = new Thickness(14), CornerRadius = new CornerRadius(18),
        Background = UrbeTheme.Brush(UrbeTheme.Surface2), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch
    };

    /// <summary>The tab the library opened last ("blocos", "comp", "meus", "modelos").</summary>
    public string LibraryTab { get; private set; } = "blocos";

    /// <summary>openLibrary: Blocos, Composições, Meus blocos and Modelos, with a search; also "Trocar tipo de bloco" (blocks only).</summary>
    public void OpenLibrary(string? changeType = null, string? only = null)
    {
        if (_st is null) return;
        var tabs = new List<(string Key, string Label)> { ("blocos", "Blocos"), ("comp", "Composições"), ("meus", "Meus blocos"), ("modelos", "Modelos") };
        if (changeType is not null) tabs = tabs.Where(t => t.Key is "blocos" or "meus").ToList();
        if (only is not null) tabs = tabs.Where(t => t.Key == only).ToList();
        if (!tabs.Any(t => t.Key == LibraryTab)) LibraryTab = tabs[0].Key;
        var search = Input("");
        search.PlaceholderText = "Buscar…";
        Avalonia.Automation.AutomationProperties.SetName(search, "Buscar");
        var body = new StackPanel { Spacing = 8 };
        var tabRow = new WrapPanel();
        void Show()
        {
            foreach (var b in tabRow.Children.OfType<Button>()) MarkChip(b, (string?)b.Tag == LibraryTab);
            body.Children.Clear();
            var q = (search.Text ?? "").Trim().ToLowerInvariant();
            bool Match(string text) => q.Length == 0 || text.ToLowerInvariant().Contains(q, StringComparison.Ordinal);
            Button Tile(string icon, string name, string desc, Func<Task> run)
            {
                var b = new Button
                {
                    Content = new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = icon, FontSize = 20, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) },
                            new TextBlock { Text = name, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), TextWrapping = TextWrapping.Wrap },
                            new TextBlock { Text = desc, FontSize = 11, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap }
                        }
                    },
                    Width = 200, MinHeight = 96, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(10), CornerRadius = new CornerRadius(12), VerticalContentAlignment = VerticalAlignment.Top,
                    Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(1)
                };
                Avalonia.Automation.AutomationProperties.SetName(b, name);
                b.Click += async (_, _) => { CloseOverlay(); await run(); };
                return b;
            }
            switch (LibraryTab)
            {
                case "blocos":
                    foreach (var g in _st.Engine.Blocks.GroupBy(b => b.Group).OrderBy(g => g.Key == "Livre" ? 0 : 1))
                    {
                        var grid = new WrapPanel();
                        foreach (var b in g.Where(b => Match(b.Label + " " + b.Description + " " + b.Type)))
                        {
                            var type = b.Type;
                            grid.Children.Add(Tile(b.Icon, b.Label, b.Description, () => { Insert(type, null, changeType); return Task.CompletedTask; }));
                        }
                        if (grid.Children.Count == 0) continue;
                        if (q.Length == 0) body.Children.Add(Sub(g.Key));
                        body.Children.Add(grid);
                    }
                    break;
                case "comp":
                {
                    var sel = _st.SelectedSection;
                    body.Children.Add(Hint(sel is not null && (string?)sel["type"] == "free" ? "Entra no layout livre selecionado, " + (_st.NodeSelected is not null ? "junto da peça escolhida" : "no fim") + "." : "Entra numa seção nova de layout livre."));
                    var grid = new WrapPanel();
                    foreach (var pr in PageEngine.FreePresets.Where(pr => Match(pr.Label + " " + pr.Key)))
                    {
                        var key = pr.Key;
                        grid.Children.Add(Tile(pr.Icon, pr.Label, "", () => { _st.InsertPreset(key); if (!Wide) _sheet = "edit"; Changed(); return Task.CompletedTask; }));
                    }
                    body.Children.Add(grid);
                    break;
                }
                case "meus":
                {
                    var mine = _host.SavedBlocks();
                    if (mine.Count == 0) body.Children.Add(Hint("Nenhum bloco seu ainda. No menu ⋯ de qualquer seção, toque em “Salvar como bloco reutilizável”."));
                    var grid = new WrapPanel();
                    foreach (var m in mine.Where(m => Match(m.Name + " " + m.Description)))
                    {
                        var saved = m.Section;
                        grid.Children.Add(Tile(_st.Engine.BlockInfo((string?)saved["type"])?.Icon ?? "★", m.Name, m.Description, () => { Insert((string)saved["type"]!, saved, changeType); return Task.CompletedTask; }));
                    }
                    body.Children.Add(grid);
                    break;
                }
                case "modelos":
                {
                    body.Children.Add(Hint("Carregue um modelo nesta página: dá para substituir tudo, só adicionar as seções dele ao fim, ou pegar só o visual."));
                    var grid = new WrapPanel();
                    foreach (var t in PageTemplates.List().Where(t => Match(t.Name + " " + t.Description)))
                    {
                        var id = t.Id;
                        grid.Children.Add(Tile(t.Icon, t.Name, t.Description, () => ApplyTemplateAsync(id, null)));
                    }
                    foreach (var t in _host.SavedTemplates().Where(t => Match(t.Name + " " + t.Description)))
                    {
                        var spec = t.Spec;
                        grid.Children.Add(Tile(S(spec["meta"]?["icon"]) is { Length: > 0 } i ? i : "★", t.Name, t.Description.Length > 0 ? t.Description : "Seu modelo", () => ApplyTemplateAsync(null, spec)));
                    }
                    body.Children.Add(grid);
                    break;
                }
            }
        }
        foreach (var (key, label) in tabs)
        {
            var b = TabChip(label + (key == "meus" && _host.SavedBlocks().Count is > 0 and var c ? "  " + c : ""), () => { LibraryTab = key; Show(); });
            b.Tag = key;
            b.Margin = new Thickness(0, 0, 4, 4);
            tabRow.Children.Add(b);
        }
        search.TextChanged += (_, _) => Show();
        var close = IconButton(UrbeTheme.Icons.Close, "Fechar");
        close.Click += (_, _) => CloseOverlay();
        var head = new DockPanel();
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(search);
        var dock = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(tabRow, Dock.Top);
        dock.Children.Add(head);
        if (tabs.Count > 1) dock.Children.Add(tabRow);
        dock.Children.Add(new ScrollViewer { Content = body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, 8, 0, 0) });
        _overlay.Children.Clear();
        _overlay.Children.Add(new Border { Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(150, 0, 0, 0)) });
        _overlay.Children.Add(Card(dock, 860));
        _overlay.IsVisible = true;
        Show();
    }

    private void Insert(string type, JsonObject? saved, string? changeType)
    {
        if (changeType is not null) _st!.ChangeType(changeType, type, saved);
        else _st!.AddSection(type, saved);
        if (!Wide) _sheet = "edit";
        Changed();
    }

    /// <summary>tplContext: the note or the folder some templates need.</summary>
    private async Task<(string? Title, string? Folder, (string Title, string Path)? Note)?> TemplateContextAsync(PageTemplateInfo? t)
    {
        if (t?.Needs == "note")
        {
            var notes = _host.Notes().Where(n => System.Text.RegularExpressions.Regex.IsMatch(n.Path, @"\.(md|markdown)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).Take(200).ToList();
            if (notes.Count == 0) { await _host.Dialogs.AlertAsync("Nenhuma nota", "Crie uma nota primeiro."); return null; }
            var pick = await _host.Dialogs.ChooseAsync("Qual nota publicar?", notes.Select(n => new DialogItem(n.Title, UrbeTheme.Icons.File, n.Path)).ToList());
            if (pick is null) return null;
            var nd = notes[pick.Value];
            return (nd.Title, null, (nd.Title, nd.Path));
        }
        if (t?.Needs == "folder")
        {
            var folders = _host.Folders().Where(f => !System.Text.RegularExpressions.Regex.IsMatch(f, "^Páginas(/|$)")).OrderBy(f => f, StringComparer.Ordinal).ToList();
            var options = new List<DialogItem> { new("Vault inteiro", UrbeTheme.Icons.Notes) };
            options.AddRange(folders.Select(f => new DialogItem(f[(f.LastIndexOf('/') + 1)..], UrbeTheme.Icons.Folder, f)));
            var pick = await _host.Dialogs.ChooseAsync("Qual pasta?", options);
            if (pick is null) return null;
            var folder = pick.Value == 0 ? "" : folders[pick.Value - 1];
            return (folder.Length > 0 ? folder[(folder.LastIndexOf('/') + 1)..] : "Minhas notas", folder, null);
        }
        return (null, null, null);
    }

    /// <summary>The spec of a template for this page (tplSpec): built, with the page's name where the template shows it.</summary>
    public static JsonObject TemplateSpec(PageEngine engine, string id, string name, (string? Title, string? Folder, (string Title, string Path)? Note) ctx)
    {
        bool person = id is "portfolio" or "resume";
        var title = ctx.Title ?? (person ? "" : name);
        var spec = PageTemplates.Build(engine, id, title, ctx.Folder ?? "", ctx.Note)!;
        var layout = (JsonObject)spec["layout"]!;
        if (PageEngine.Truthy(layout["brand"]) && !person) layout["brand"] = name;
        if (id == "blank" && spec["sections"]!.AsArray().FirstOrDefault() is JsonObject first) first["props"]!["title"] = name;
        if ((string?)layout["format"] == "book")
        {
            layout["runningHead"] = name;
            foreach (var x in spec["sections"]!.AsArray().OfType<JsonObject>())
                if ((string?)x["type"] is "bookcover" or "titlepage") x["props"]!["title"] = name;
        }
        return spec;
    }

    private static readonly DialogItem[] StartModes =
    [
        new("Completo", UrbeTheme.Icons.Star, "Todas as partes, com textos de exemplo para trocar"),
        new("Simplificado", UrbeTheme.Icons.List, "Só as partes principais, listas curtas"),
        new("Só a estrutura", UrbeTheme.Icons.Layers, "Os mesmos blocos e o mesmo visual, sem os textos de exemplo")
    ];

    /// <summary>"Como começar?": full, simple or skeleton; null when cancelled.</summary>
    public static async Task<string?> StartModeAsync(UrbeDialogs dialogs)
    {
        var v = await dialogs.ChooseAsync("Como começar?", StartModes);
        return v switch { 0 => "full", 1 => "simple", 2 => "skeleton", _ => null };
    }

    /// <summary>applyTemplate: Substituir a página, Adicionar as seções ao fim, or Só o visual (an empty page is replaced without asking).</summary>
    private async Task ApplyTemplateAsync(string? id, JsonObject? saved)
    {
        var st = _st!;
        var t = id is null ? null : PageTemplates.Get(id);
        string mode = "replace";
        if (!st.IsEmpty)
        {
            var pick = await _host.Dialogs.ChooseAsync("Carregar “" + (t?.Name ?? "modelo") + "”",
            [
                new DialogItem("Substituir a página", UrbeTheme.Icons.Restore, "Seções, tema e estrutura do modelo (dá para desfazer)"),
                new DialogItem("Adicionar as seções ao fim", UrbeTheme.Icons.Plus, "Mantém o que você já fez e o seu tema"),
                new DialogItem("Só o visual", UrbeTheme.Icons.Brush, "Tema, cores e fontes do modelo; o conteúdo fica")
            ]);
            if (pick is null) return;
            mode = pick.Value switch { 0 => "replace", 1 => "append", _ => "theme" };
        }
        JsonObject spec;
        if (saved is not null)
        {
            spec = st.Engine.Normalize(saved).Spec;
            spec.Remove("template");
        }
        else
        {
            var ctx = await TemplateContextAsync(t);
            if (ctx is null) return;
            spec = TemplateSpec(st.Engine, id!, S(st.Meta["title"]) is { Length: > 0 } n ? n : "Página", ctx.Value);
        }
        if (mode != "theme" && spec["sections"]!.AsArray().Count > 2)
        {
            var v = await StartModeAsync(_host.Dialogs);
            if (v is null) return;
            spec = PageTemplates.Variant(st.Engine, spec, v);
        }
        st.ApplyTemplate(spec, mode);
        Changed();
        Toast(mode == "replace" ? "Modelo carregado. Escolha uma seção para editar." : mode == "append" ? "Seções do modelo adicionadas ao fim." : "Visual do modelo aplicado.");
    }

    /// <summary>openJson: the page's JSON with validation as you type, Formatar, Referência, Copiar and Aplicar.</summary>
    public void OpenJson()
    {
        if (_st is null) return;
        var ta = Input(_st.Json(), multi: true, mono: true);
        ta.MaxHeight = double.PositiveInfinity;
        ta.VerticalAlignment = VerticalAlignment.Stretch;
        Avalonia.Automation.AutomationProperties.SetName(ta, "JSON da página");
        var state = new TextBlock { FontSize = 12, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) };
        var errs = new StackPanel { Spacing = 2 };
        PageNormalized Validate()
        {
            var n = _st!.Engine.Normalize(ta.Text ?? "");
            errs.Children.Clear();
            foreach (var x in n.Errors.Select(e => (e, false)).Concat(n.Warnings.Select(w => (w, true))).Take(30))
                errs.Children.Add(new TextBlock { Text = x.Item1.Path + "  " + x.Item1.Message, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(x.Item2 ? UrbeTheme.Warn : UrbeTheme.Danger) });
            state.Text = n.Errors.Count > 0 ? n.Errors.Count + " erro(s)" : n.Warnings.Count > 0 ? "válido · " + n.Warnings.Count + " aviso(s)" : "válido";
            state.Foreground = UrbeTheme.Brush(n.Errors.Count > 0 ? UrbeTheme.Danger : UrbeTheme.Ok);
            return n;
        }
        ta.TextChanged += (_, _) => Validate();
        Button Small(string text, Action click)
        {
            var b = new Button { Content = text, Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(9), Background = UrbeTheme.Brush(UrbeTheme.Surface), Margin = new Thickness(4, 0, 0, 0) };
            b.Click += (_, _) => click();
            return b;
        }
        var format = Small("Formatar", () =>
        {
            try { ta.Text = Urbe.Core.LegacyCustomize.Stringify(JsonNode.Parse(ta.Text ?? ""), 2); }
            catch (System.Text.Json.JsonException) { }
        });
        var schema = Small("Referência", () => _ = _host.Dialogs.AlertAsync("Referência do formato", PageTemplates.SchemaText()));
        var copy = Small("Copiar", async () =>
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip) await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clip, ta.Text ?? "");
            Toast("JSON copiado.");
        });
        var close = IconButton(UrbeTheme.Icons.Close, "Fechar");
        close.Click += (_, _) => CloseOverlay();
        var apply = new Button
        {
            Content = new TextBlock { Text = "Aplicar", FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk) }, Padding = new Thickness(16, 8), CornerRadius = new CornerRadius(10),
            Background = UrbeTheme.Brush(UrbeTheme.Accent), HorizontalAlignment = HorizontalAlignment.Right
        };
        apply.Click += (_, _) => ApplyJsonText(ta.Text ?? "");
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var tools = new StackPanel { Orientation = Orientation.Horizontal, Children = { format, schema, copy, close } };
        DockPanel.SetDock(tools, Dock.Right);
        head.Children.Add(tools);
        head.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = "JSON da página", FontWeight = FontWeight.Bold, FontSize = 15, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center }, state } });
        var foot = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(apply, Dock.Right);
        foot.Children.Add(apply);
        foot.Children.Add(Hint("Ctrl+Enter aplica · Esc fecha. O Assistente também pode criar e editar este arquivo."));
        var errScroll = new ScrollViewer { Content = errs, MaxHeight = 120 };
        var dock = new DockPanel();
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(foot, Dock.Bottom);
        DockPanel.SetDock(errScroll, Dock.Bottom);
        dock.Children.Add(head);
        dock.Children.Add(foot);
        dock.Children.Add(errScroll);
        dock.Children.Add(ta);
        ta.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control)) { ApplyJsonText(ta.Text ?? ""); e.Handled = true; }
        };
        _overlay.Children.Clear();
        _overlay.Children.Add(new Border { Background = new SolidColorBrush(Avalonia.Media.Color.FromArgb(150, 0, 0, 0)) });
        _overlay.Children.Add(Card(dock, 900));
        _overlay.IsVisible = true;
        JsonEditor = ta;
        Validate();
    }

    /// <summary>The JSON panel's text box while it is open (tests).</summary>
    public TextBox? JsonEditor { get; private set; }

    private void ApplyJsonText(string text)
    {
        var n = _st!.ApplyJson(text);
        if (n.Errors.Count > 0) { Toast("Corrija os erros antes de aplicar."); return; }
        CloseOverlay();
        JsonEditor = null;
        RenderAll();
        Changed();
        Toast("JSON aplicado.");
    }
}
