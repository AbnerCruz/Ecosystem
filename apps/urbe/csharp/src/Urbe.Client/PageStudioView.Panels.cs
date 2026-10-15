using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core.Pages;

namespace Urbe.Client;

/// <summary>The studio's panels (Seções, Tema, Página, Editar) and the forms generated from the fields (studio.js fieldHtml).</summary>
public sealed partial class PageStudioView
{
    private static readonly string[] SecLook = ["background", "image", "bgColor", "textColor", "padding", "width", "align", "boxed", "minHeight", "animation"];
    private static readonly string[] SecAdv = ["anchor", "menu", "className", "css"];
    private static readonly (string Key, string Title, string[] Keys, bool Open)[] ThemeGroups =
    [
        ("cores", "Cores e fundo", ["mode", "primary", "accent", "bg", "surface", "text", "muted", "border", "background"], true),
        ("texto", "Texto e títulos", ["headingFont", "bodyFont", "scale", "lineHeight", "headingWeight", "headingCase", "headingSpacing", "headingScale"], false),
        ("formas", "Botões, cartões e links", ["buttonStyle", "cardStyle", "linkStyle", "radius", "shadow"], false),
        ("espaco", "Espaço e movimento", ["spacing", "width", "animations"], false),
        ("css", "CSS próprio", ["css"], false)
    ];
    private static readonly string[] BookKeys = ["pageSize", "margins", "pageNumbers", "runningHead", "chapterStyle", "recto", "justify", "indent"];

    private static IEnumerable<JsonObject> Pick(JsonArray list, IEnumerable<string> keys) =>
        keys.Select(k => list.OfType<JsonObject>().FirstOrDefault(f => (string?)f["key"] == k)).OfType<JsonObject>();

    private static IEnumerable<JsonObject> Rest(JsonArray list, IEnumerable<string> used) =>
        list.OfType<JsonObject>().Where(f => !used.Contains((string?)f["key"]));

    private static StackPanel Stack() => new() { Spacing = 10, Margin = new Thickness(12, 8, 12, 24) };

    private static TextBlock Hint(string text) => new() { Text = text, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) };

    private static TextBlock Sub(string text) => new() { Text = text, FontSize = 13, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0), Foreground = UrbeTheme.Brush(UrbeTheme.Text2) };

    /// <summary>grp(key, title, inner, open): a group that remembers whether it is open.</summary>
    private Control? Group(string key, string title, IEnumerable<Control> inner, bool open = false)
    {
        var items = inner.ToList();
        if (items.Count == 0) return null;
        var body = new StackPanel { Spacing = 10, Margin = new Thickness(0, 8, 0, 4) };
        foreach (var c in items) body.Children.Add(c);
        var e = new Expander
        {
            Header = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) }, Content = body,
            IsExpanded = _groups.TryGetValue(key, out var o) ? o : open, HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = UrbeTheme.Brush(UrbeTheme.Surface), CornerRadius = new CornerRadius(12)
        };
        e.Expanded += (_, _) => _groups[key] = true;
        e.Collapsed += (_, _) => _groups[key] = false;
        return e;
    }

    private static void Add(Panel p, Control? c) { if (c is not null) p.Children.Add(c); }

    private IEnumerable<Control> Form(IEnumerable<JsonObject> fields, JsonObject obj, string basePath) =>
        fields.Select(f => Field(f, obj[(string)f["key"]!], basePath + "." + (string)f["key"]!));

    // ---------------------------------------------------------------- Seções

    private Control SectionsPanel()
    {
        var st = _st!;
        var p = Stack();
        if (st.Sections.Count == 0)
            p.Children.Add(new StackPanel
            {
                Margin = new Thickness(0, 24), Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center,
                Children = { new TextBlock { Text = "Página vazia", FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text) }, Hint("Adicione o primeiro bloco.") }
            });
        foreach (var s in st.Sections.OfType<JsonObject>().ToList())
        {
            var id = (string)s["id"]!;
            var l = st.Label(s);
            bool hidden = PageEngine.Truthy(s["style"]?["hidden"]);
            var main = new Button
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = l.Label, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                        new TextBlock { Text = l.Title.Length > 0 ? l.Title : "—", FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }
                    }
                },
                Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(6, 4)
            };
            Avalonia.Automation.AutomationProperties.SetName(main, l.Label);
            main.Click += (_, _) =>
            {
                st.Select(id);
                if (!Wide) _sheet = "edit";
                RenderPanels();
            };
            var up = Mini("↑", "Mover para cima", () => Move(id, -1));
            var down = Mini("↓", "Mover para baixo", () => Move(id, 1));
            var eye = Mini(hidden ? "◌" : "◉", hidden ? "Mostrar" : "Ocultar", () => { st.ToggleHidden(id); Changed(); });
            var menu = Mini("⋯", "Opções da seção", () => _ = SectionMenuAsync(id));
            var icon = new TextBlock { Text = l.Icon, FontSize = 18, Width = 28, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) };
            var row = new DockPanel();
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Children = { up, down, eye, menu } };
            DockPanel.SetDock(icon, Dock.Left);
            DockPanel.SetDock(tools, Dock.Right);
            row.Children.Add(icon);
            row.Children.Add(tools);
            row.Children.Add(main);
            p.Children.Add(new Border
            {
                Child = row, Padding = new Thickness(6, 4), CornerRadius = new CornerRadius(12), Opacity = hidden ? .55 : 1,
                Background = UrbeTheme.Brush(id == st.Selected ? UrbeTheme.Accent : UrbeTheme.Surface, id == st.Selected ? .16 : 1),
                BorderBrush = UrbeTheme.Brush(id == st.Selected ? UrbeTheme.Accent : UrbeTheme.Line), BorderThickness = new Thickness(1)
            });
        }
        var add = new Button
        {
            Content = "＋  Adicionar bloco", HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(10),
            CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Surface2), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1)
        };
        add.Click += (_, _) => OpenLibrary();
        p.Children.Add(add);
        return p;
    }

    private static Button Mini(string glyph, string tip, Action click)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = glyph, FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) }, Width = 32, Height = 32, Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(b, tip);
        Avalonia.Automation.AutomationProperties.SetName(b, tip);
        b.Click += (_, _) => click();
        return b;
    }

    private void Move(string id, int delta) { _st!.MoveSection(id, delta); Changed(); }

    private void Duplicate(string id) { _st!.DuplicateSection(id); Changed(); }

    private void Remove(string id)
    {
        var t = _st!.RemoveSection(id);
        Changed();
        if (t is not null) Toast(t.Label + " removida. (Desfazer: ↶)");
    }

    /// <summary>sectionMenu(id).</summary>
    private async Task SectionMenuAsync(string id)
    {
        var st = _st!;
        int i = st.IndexOf(id), n = st.Sections.Count;
        var sec = st.Section(id);
        if (sec is null) return;
        var items = new List<DialogItem> { new("Editar", UrbeTheme.Icons.Edit, Run: () => { st.Select(id); if (!Wide) _sheet = "edit"; RenderPanels(); return Task.CompletedTask; }) };
        if (i > 0) items.Add(new DialogItem("Mover para cima", UrbeTheme.Icons.ArrowUp, "Alt+↑", Run: () => { Move(id, -1); return Task.CompletedTask; }));
        if (i < n - 1) items.Add(new DialogItem("Mover para baixo", UrbeTheme.Icons.ArrowUp, "Alt+↓", Run: () => { Move(id, 1); return Task.CompletedTask; }));
        items.Add(new DialogItem("Duplicar", UrbeTheme.Icons.Copy, "Ctrl+D", Run: () => { Duplicate(id); return Task.CompletedTask; }));
        if (PageEngine.FreeConvertible.Contains(S(sec["type"])))
            items.Add(new DialogItem("Converter em layout livre", UrbeTheme.Icons.Layers, "Cada título e parágrafo vira uma peça solta", Run: () =>
            {
                if (!st.ConvertToFree(id, p => p is null ? null : _host.Note(p))) { Toast("Este bloco não pode ser convertido."); return Task.CompletedTask; }
                if (!Wide) _sheet = "edit";
                Changed();
                Toast("Convertido: agora cada título e parágrafo é uma peça solta.");
                return Task.CompletedTask;
            }));
        if ((string?)sec["type"] == "chapters")
            items.Add(new DialogItem("Separar em capítulos", UrbeTheme.Icons.List, "Um bloco de capítulo por nota, editável um a um", Run: () => { SplitChapters(id); return Task.CompletedTask; }));
        items.Add(new DialogItem("Salvar como bloco reutilizável", UrbeTheme.Icons.Star, "Aparece em “Meus blocos” em qualquer página", Run: () => SaveBlockAsync(id)));
        items.Add(new DialogItem("Trocar tipo de bloco…", UrbeTheme.Icons.Layers, Run: () => { OpenLibrary(changeType: id); return Task.CompletedTask; }));
        items.Add(new DialogItem("Remover", UrbeTheme.Icons.Trash, "Delete", Danger: true, Run: () => { Remove(id); return Task.CompletedTask; }));
        await _host.Dialogs.MenuAsync(st.Label(sec).Label, items);
    }

    /// <summary>splitChapters: "Capítulos de uma pasta" becomes one Capítulo block per note.</summary>
    private void SplitChapters(string id)
    {
        var st = _st!;
        var sec = st.Section(id);
        if (sec is null) return;
        var folder = S(sec["props"]?["folder"]).Trim('/').ToLowerInvariant();
        var sort = S(sec["props"]?["sort"]);
        var notes = _host.Notes().Where(d => System.Text.RegularExpressions.Regex.IsMatch(d.Path, @"\.(md|markdown|txt)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
            (folder.Length == 0 || d.Path.ToLowerInvariant().StartsWith(folder + "/", StringComparison.Ordinal))).ToList();
        notes = sort == "title" ? notes.OrderBy(d => d.Title, StringComparer.CurrentCulture).ToList() : notes.OrderBy(d => d.Path, StringComparer.Ordinal).ToList();
        if (notes.Count == 0) { Toast("A pasta não tem notas."); return; }
        var dropCap = sec["props"]?["dropCap"]?.DeepClone();
        st.Break();
        st.Commit(s =>
        {
            var list = (JsonArray)s["sections"]!;
            int i = st.IndexOf(id);
            list.RemoveAt(i);
            foreach (var d in notes.Take(200).Reverse())
                list.Insert(i, st.Engine.NewSection("chapter", new JsonObject { ["title"] = d.Title, ["source"] = "note", ["path"] = d.Path, ["dropCap"] = dropCap?.DeepClone() }));
        });
        st.Select(null);
        Changed();
        Toast(notes.Count + " capítulos separados.");
    }

    /// <summary>saveBlock: the section (content and look) in Páginas/Blocos/&lt;nome&gt;.block.json.</summary>
    private async Task SaveBlockAsync(string id)
    {
        var st = _st!;
        var sec = st.Section(id);
        if (sec is null) return;
        var l = st.Label(sec);
        var name = PageStudio.CleanName(await _host.Dialogs.PromptAsync(new PromptOptions("Salvar como bloco", "Nome do bloco", l.Title.Length > 0 ? l.Title : l.Label,
            Confirm: "Salvar", Hint: "Ele aparece em “Meus blocos” ao tocar em +, em qualquer página.")));
        if (name.Length == 0) return;
        var path = PageStudio.UniquePath("Páginas/Blocos/" + name + ".block.json", _host.Exists);
        var text = Urbe.Core.LegacyCustomize.Stringify(new JsonObject
        {
            ["kind"] = "urbe-block", ["name"] = name, ["description"] = l.Label,
            ["section"] = new JsonObject { ["type"] = sec["type"]!.DeepClone(), ["props"] = sec["props"]!.DeepClone(), ["style"] = sec["style"]!.DeepClone() }
        }, 2) + "\n";
        try
        {
            await _host.CreateFileAsync(path, text);
            Toast("Bloco “" + name + "” salvo em Meus blocos.");
        }
        catch (Exception ex) { await _host.Dialogs.AlertAsync("Não foi possível salvar o bloco", ex.Message); }
    }

    // ---------------------------------------------------------------- Tema and Página

    private Control ThemePanel()
    {
        var st = _st!;
        var p = Stack();
        p.Children.Add(Sub("Temas prontos"));
        var presets = new WrapPanel { Orientation = Orientation.Horizontal };
        var cur = S(st.Theme["preset"]);
        foreach (var t in PageEngine.ThemeList)
        {
            var sw = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            foreach (var k in new[] { "bg", "primary", "accent", "text" })
                sw.Children.Add(new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = Color(S(t.Palette[k])), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1) });
            var b = new Button
            {
                Content = new StackPanel { Spacing = 6, Children = { sw, new TextBlock { Text = t.Label, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) } } },
                Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 8), CornerRadius = new CornerRadius(10), MinWidth = 92,
                Background = UrbeTheme.Brush(t.Key == cur ? UrbeTheme.Accent : UrbeTheme.Surface, t.Key == cur ? .18 : 1),
                BorderBrush = UrbeTheme.Brush(t.Key == cur ? UrbeTheme.Accent : UrbeTheme.Line2), BorderThickness = new Thickness(1)
            };
            Avalonia.Automation.AutomationProperties.SetName(b, t.Label);
            var key = t.Key;
            b.Click += (_, _) => { st.SetPreset(key); Changed(); };
            presets.Children.Add(b);
        }
        p.Children.Add(presets);
        var used = new List<string> { "preset" };
        foreach (var g in ThemeGroups)
        {
            used.AddRange(g.Keys);
            Add(p, Group("t-" + g.Key, g.Title, Form(Pick(st.Engine.ThemeFields, g.Keys), st.Theme, "theme"), g.Open));
        }
        Add(p, Group("t-outros", "Outros", Form(Rest(st.Engine.ThemeFields, used), st.Theme, "theme")));
        return p;
    }

    private static IBrush Color(string css)
    {
        try { return new SolidColorBrush(Avalonia.Media.Color.Parse(css)); }
        catch (FormatException) { return UrbeTheme.Brush(UrbeTheme.Surface3); }
    }

    private Control PagePanel()
    {
        var st = _st!;
        var p = Stack();
        p.Children.Add(Hint("Título, descrição e ícone aparecem na aba do navegador e ao compartilhar."));
        foreach (var c in Form(Rest(st.Engine.MetaFields, ["head"]), st.Meta, "meta")) p.Children.Add(c);
        p.Children.Add(Sub("Estrutura"));
        foreach (var c in Form(Pick(st.Engine.LayoutFields, ["format"]), st.Layout, "layout")) p.Children.Add(c);
        bool book = st.IsBook;
        if (book) Add(p, Group("p-livro", "Livro: página, margens e numeração", Form(Pick(st.Engine.LayoutFields, BookKeys), st.Layout, "layout"), true));
        Add(p, Group("p-site", book ? "Opções de site (não aparecem no livro)" : "Barra, rodapé e botões", Form(Rest(st.Engine.LayoutFields, [.. BookKeys, "format"]), st.Layout, "layout"), !book));
        Add(p, Group("p-head", "Avançado: código no <head>", [.. Form(Pick(st.Engine.MetaFields, ["head"]), st.Meta, "meta"),
            Hint("Entra como está no HTML exportado: estatísticas, fontes próprias, meta tags.")]));
        return p;
    }

    // ---------------------------------------------------------------- Editar

    private Control EditPanel()
    {
        var st = _st!;
        var p = Stack();
        if (st.SelectedSection is not { } s)
        {
            p.Children.Add(new StackPanel
            {
                Margin = new Thickness(0, 24), Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    new TextBlock { Text = "Nada selecionado", FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                    Hint(Wide ? "Escolha uma seção à esquerda." : "Escolha uma seção em Seções para editar.")
                }
            });
            return p;
        }
        var b = st.Engine.BlockInfo(S(s["type"]))!;
        var basePath = "sections." + st.IndexOf((string)s["id"]!).ToString(CultureInfo.InvariantCulture);
        p.Children.Add(new DockPanel
        {
            Children =
            {
                new TextBlock { Text = b.Icon, FontSize = 22, Width = 34, VerticalAlignment = VerticalAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) },
                new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = b.Label, FontWeight = FontWeight.Bold, FontSize = 15, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
                        new TextBlock { Text = b.Description, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }
                    }
                }
            }
        });
        foreach (var c in Form(b.Fields.OfType<JsonObject>(), (JsonObject)s["props"]!, basePath + ".props")) p.Children.Add(c);
        var style = (JsonObject)s["style"]!;
        Add(p, Group("s-look", "Aparência da seção", Form(Pick(st.Engine.SectionFields, SecLook), style, basePath + ".style")));
        Add(p, Group("s-adv", "Avançado: âncora, menu, classe e CSS", [.. Form(Pick(st.Engine.SectionFields, SecAdv), style, basePath + ".style"),
            Hint("No CSS da seção, & é a própria seção. Ex.: & h2{color:tomato}. Só propriedades (sem chaves) valem para a seção inteira.")]));
        foreach (var c in Form(Rest(st.Engine.SectionFields, [.. SecLook, .. SecAdv]), style, basePath + ".style")) p.Children.Add(c);
        var id = (string)s["id"]!;
        var dup = new Button { Content = "Duplicar", Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(10), Background = UrbeTheme.Brush(UrbeTheme.Surface2) };
        dup.Click += (_, _) => Duplicate(id);
        var del = new Button { Content = new TextBlock { Text = "Remover", Foreground = UrbeTheme.Brush(UrbeTheme.Danger) }, Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(10), Background = UrbeTheme.Brush(UrbeTheme.Surface2) };
        del.Click += (_, _) => Remove(id);
        p.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0), Children = { dup, del } });
        return p;
    }
}
