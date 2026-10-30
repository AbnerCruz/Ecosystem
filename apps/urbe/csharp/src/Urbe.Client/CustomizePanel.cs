using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// src/customize/panel.js natively: "Personalização" with its sections; a wide window with the
/// section list on the left, full screen with scrolling tabs on a phone (≤ 760 px). Visual from
/// styles/customize.css (.uc).
/// </summary>
public sealed partial class CustomizePanel : Grid
{
        private static readonly (string Id, string Name, string Icon)[] Sections =
    [
        ("aparencia", "Aparência", Icons.Palette), ("texto", "Texto", Icons.Type), ("editor", "Editor", Icons.Edit),
        ("estilos", "Estilos CSS", Icons.Brush),
        ("tags", "Tags", Icons.Palette), ("plugins", "Plugins", Icons.Puzzle), ("arquivo", "Arquivo", Icons.Code)
    ];

    private readonly Border _card = new();
    private readonly Grid _layout = new();
    private readonly StackPanel _nav = new() { Spacing = 2 };
    private ScrollViewer _navScroll = new();
    private ScrollViewer _bodyScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly StackPanel _body = new();
    private string _section = "aparencia";
    private bool _narrow;
    private TextBox? _json;
    private TextBlock? _jsonState;

    public CustomizePanel(Customization customization, UrbeDialogs dialogs)
    {
        Customization = customization;
        Dialogs = dialogs;
        IsVisible = false;
        Background = new SolidColorBrush(Color.FromArgb(140, 5, 6, 8)); // rgba(5,6,8,.55)
        _card.Background = UrbeTheme.Brush(UrbeTheme.Surface);
        _card.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _card.BoxShadow = BoxShadows.Parse("0 18 60 0 #8C000000");
        _card.ClipToBounds = true;
        _card.Child = _layout;
        Children.Add(_card);
        PointerPressed += (_, e) => { if (e.Source == this) Close(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape && !Dialogs.IsOpen) { e.Handled = true; Close(); } };
        Focusable = true;
        Customization.Applied += () => { if (IsVisible && !Customization.IsPreview && _section != "arquivo") Render(); };
    }

    public Customization Customization { get; }
    public UrbeDialogs Dialogs { get; }
    public string Section => _section;
    /// <summary>abrirNota(p): the panel asks the app to open a vault file.</summary>
    public event Action<string>? OpenFileRequested;

    /// <summary>The native plugins with whether each is on (the host fills it; ADD-0021).</summary>
    public Func<IReadOnlyList<(PluginManifest Manifest, bool On)>>? NativePlugins { get; set; }

    /// <summary>Turns a native plugin on or off.</summary>
    public Action<string, bool>? SetNativePlugin { get; set; }

    /// <summary>abrir(qual).</summary>
    public void Open(string? section = null)
    {
        if (section is not null && Sections.Any(s => s.Id == section)) _section = section;
        IsVisible = true;
        BuildFrame();
        Render();
        Focus();
    }

    /// <summary>fechar().</summary>
    public void Close() => IsVisible = false;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        bool narrow = e.NewSize.Width <= 760;
        if (narrow != _narrow || _layout.Children.Count == 0) { _narrow = narrow; if (IsVisible) { BuildFrame(); Render(); } }
        SizeCard();
    }

    private void SizeCard()
    {
        if (_narrow)
        {
            _card.Width = double.NaN; _card.Height = double.NaN;
            _card.HorizontalAlignment = HorizontalAlignment.Stretch; _card.VerticalAlignment = VerticalAlignment.Stretch;
            _card.CornerRadius = new CornerRadius(0); _card.BorderThickness = new Thickness(0);
        }
        else
        {
            _card.Width = Math.Min(980, Math.Max(0, Bounds.Width - 32)); _card.Height = Math.Min(760, Math.Max(0, Bounds.Height - 32));
            _card.HorizontalAlignment = HorizontalAlignment.Center; _card.VerticalAlignment = VerticalAlignment.Center;
            _card.CornerRadius = new CornerRadius(UrbeAppearance.Radius(3)); _card.BorderThickness = new Thickness(1);
        }
    }

    private void BuildFrame()
    {
        _narrow = Bounds.Width > 0 && Bounds.Width <= 760;
        SizeCard();
        _layout.Children.Clear();
        // the section list and the body move to fresh scrollers (a control has one parent)
        var offset = _bodyScroll.Offset;
        _navScroll.Content = null;
        _bodyScroll.Content = null;
        _navScroll = new ScrollViewer { Content = _nav };
        _bodyScroll = new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Offset = offset };
        _layout.RowDefinitions.Clear();
        _layout.ColumnDefinitions.Clear();
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center,
            Children = { UrbeTheme.Icon(Icons.Brush, 20, UrbeTheme.Accent), Label("Personalização", 17, FontWeight.Bold) } };
        var close = new Button
        {
            Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 20, UrbeTheme.Text2), Width = 44, Height = 44, CornerRadius = new CornerRadius(12),
            Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        ToolTip.SetTip(close, "Fechar");
        close.Click += (_, _) => Close();
        var head = new DockPanel { Margin = _narrow ? new Thickness(16, 8, 8, 4) : new Thickness(20, 12, 12, 12) };
        DockPanel.SetDock(close, Dock.Right);
        head.Children.Add(close);
        head.Children.Add(title);
        var headBox = new Border { Child = head, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = _narrow ? new Thickness(0) : new Thickness(0, 0, 0, 1) };

        _nav.Children.Clear();
        _nav.Orientation = _narrow ? Orientation.Horizontal : Orientation.Vertical;
        _nav.Spacing = _narrow ? 6 : 2;
        _nav.Margin = _narrow ? new Thickness(12, 4, 12, 10) : new Thickness(10);
        foreach (var s in Sections) _nav.Children.Add(NavButton(s.Id, s.Name, s.Icon));
        _navScroll.HorizontalScrollBarVisibility = _narrow ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
        _navScroll.VerticalScrollBarVisibility = _narrow ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        var navBox = new Border { Child = _navScroll, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = _narrow ? new Thickness(0, 0, 0, 1) : new Thickness(0, 0, 1, 0) };
        _body.Margin = _narrow ? new Thickness(14, 14, 14, 40) : new Thickness(22, 18, 22, 40);

        if (_narrow)
        {
            _layout.RowDefinitions = new RowDefinitions("Auto,Auto,*");
            SetRow(navBox, 1);
            SetRow(_bodyScroll, 2);
        }
        else
        {
            _layout.RowDefinitions = new RowDefinitions("Auto,*");
            _layout.ColumnDefinitions = new ColumnDefinitions("220,*");
            SetColumnSpan(headBox, 2);
            SetRow(navBox, 1);
            SetRow(_bodyScroll, 1);
            SetColumn(_bodyScroll, 1);
        }
        _layout.Children.Add(headBox);
        _layout.Children.Add(navBox);
        _layout.Children.Add(_bodyScroll);
    }

    private Button NavButton(string id, string name, string icon)
    {
        bool on = id == _section;
        var b = new Button
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = _narrow ? 7 : 12, VerticalAlignment = VerticalAlignment.Center,
                Children = { UrbeTheme.Icon(icon, _narrow ? 17 : 20, on ? UrbeTheme.Accent : UrbeTheme.Text2), Label(name, _narrow ? 13.5 : 14, FontWeight.SemiBold, on ? UrbeTheme.Accent : UrbeTheme.Text2) } },
            MinHeight = _narrow ? 40 : 44, Padding = new Thickness(_narrow ? 14 : 12, 0), HorizontalAlignment = _narrow ? HorizontalAlignment.Left : HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(_narrow ? 999 : 12), Background = on ? UrbeTheme.AccentSoft : Brushes.Transparent,
            BorderThickness = new Thickness(_narrow && !on ? 1 : 0), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2)
        };
        AutomationName(b, name);   // CST-105: a screen reader says the section, not "botão"
        b.Click += (_, _) => { _section = id; BuildFrame(); _bodyScroll.Offset = default; Render(); };
        return b;
    }

    // ---------------------------------------------------------------- render

    /// <summary>render(): the current section.</summary>
    public void Render()
    {
        _body.Children.Clear();
        _json = null;
        _jsonState = null;
        switch (_section)
        {
            case "aparencia": AppearanceSection(); break;
            case "texto": TextSection(); break;
            case "editor": EditorSection(); break;
            case "estilos": StylesSection(); break;
            case "tags": TagsSection(); break;
            case "plugins": PluginsSection(); break;
            case "arquivo": FileSection(); break;
        }
    }

    /// <summary>
    /// Tags (DEC-0045, UC-43): the colour of each #Tag: paragraph. The default tags and those already chosen are listed,
    /// each with the palette; "Padrão" goes back to the app's colour; "Adicionar tag" names another. Kept in
    /// Personalização/tags.json, never in tema.json.
    /// </summary>
    private void TagsSection()
    {
        var tags = TagStyles.Defaults.Concat(Customization.TagColors.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var add = new Button
        {
            Content = new TextBlock { Text = "Adicionar tag", FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text) },
            Padding = new Thickness(12, 6), MinHeight = 40, CornerRadius = new CornerRadius(10), Background = UrbeTheme.Brush(UrbeTheme.Surface3)
        };
        add.Click += async (_, _) =>
        {
            var name = (await Dialogs.PromptAsync(new PromptOptions("Adicionar tag", Label: "Nome", Placeholder: "Ex.: Personagem",
                Hint: "Escreva “#Nome: texto” no começo de um parágrafo para usar.", Confirm: "Adicionar",
                Validate: v => TagStyles.IsTag((v ?? "").Trim().TrimStart('#').TrimEnd(':')) ? null : "Use letras, números, _ ou -, começando por letra.")))?.Trim().TrimStart('#').TrimEnd(':');
            if (string.IsNullOrEmpty(name)) return;
            await Customization.SetTagColorAsync(name, UrbeTheme.TagColor(name));
            Render();
        };
        var rows = tags.Select(TagRow).ToArray();
        _body.Children.Add(BlockWith("Tags", add, rows));
        _body.Children.Add(new TextBlock
        {
            Text = "Um parágrafo que começa com “#Todo:” ganha o estilo da tag Todo: a cor e, se você escolher, um ícone antes do nome (toque no ícone escolhido de novo para tirar). O arquivo fica em Personalização/tags.json.",
            FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0)
        });
    }

    /// <summary>The icons offered for a tag; tapping the chosen one again takes it off.</summary>
    private static readonly string[] TagIconChoices = ["✅", "💡", "⚠️", "❓", "📌", "⭐", "🔥", "📝", "🎯", "📚"];

    private Border TagRow(string tag)
    {
        var current = UrbeTheme.TagColor(tag);
        var swatches = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var color in UrbeTheme.TagChoices)
        {
            var c = color;
            var chosen = string.Equals(c, current, StringComparison.OrdinalIgnoreCase);
            var dot = new Button
            {
                Width = 40, Height = 40, Padding = new Thickness(0), Margin = new Thickness(2), CornerRadius = new CornerRadius(20),
                Background = UrbeTheme.Brush(c), BorderBrush = UrbeTheme.Brush(chosen ? UrbeTheme.Text : UrbeTheme.Line2), BorderThickness = new Thickness(chosen ? 2 : 1)
            };
            ToolTip.SetTip(dot, c);
            AutomationName(dot, "#" + tag + " " + c);
            dot.Click += async (_, _) => { await Customization.SetTagColorAsync(tag, c); Render(); };
            swatches.Children.Add(dot);
        }
        if (Customization.TagColors.ContainsKey(tag))
        {
            var reset = new Button { Content = "Padrão", FontSize = 12, Padding = new Thickness(8, 4), Margin = new Thickness(6, 3, 0, 3), CornerRadius = new CornerRadius(8), Background = Brushes.Transparent };
            reset.Click += async (_, _) => { await Customization.SetTagColorAsync(tag, null); Render(); };
            swatches.Children.Add(reset);
        }
        var chip = new Border
        {
            Background = UrbeTheme.Brush(current, .12), BorderBrush = UrbeTheme.Brush(current), BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 4), HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock { Text = (UrbeTheme.TagIcon(tag) is { } shown ? shown + " " : "") + "#" + tag, FontWeight = FontWeight.Bold, FontSize = 13, Foreground = UrbeTheme.Brush(current) }
        };
        // CST-024: an icon before the name, from a short set or none
        var icons = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var chosenIcon = UrbeTheme.TagIcon(tag);
        foreach (var icon in TagIconChoices)
        {
            var i = icon;
            var on = i == chosenIcon;
            var b = new Button
            {
                Content = new TextBlock { Text = i, FontSize = 17 }, Width = 40, Height = 40, Padding = new Thickness(0), Margin = new Thickness(2), CornerRadius = new CornerRadius(8),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                Background = on ? UrbeTheme.Brush(current, .2) : Brushes.Transparent, BorderBrush = UrbeTheme.Brush(on ? current : UrbeTheme.Line2), BorderThickness = new Thickness(on ? 2 : 1)
            };
            AutomationName(b, "#" + tag + " ícone " + i);
            b.Click += async (_, _) => { await Customization.SetTagIconAsync(tag, on ? null : i); Render(); };
            icons.Children.Add(b);
        }
        var row = new StackPanel { Spacing = 6, Children = { chip, swatches, icons } };
        swatches.HorizontalAlignment = HorizontalAlignment.Left;
        return new Border { Tag = "row", Child = row, Padding = new Thickness(0, 8), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line) };
    }

    private void FileSection()
    {
        var text = Customization.FileContent ?? LegacyCustomize.Stringify(new JsonObject { ["$schema"] = "urbe-tema-1" }, 2) + "\n";
        _json = new TextBox
        {
            Text = text, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MinHeight = 14 * 20, FontFamily = UrbeTheme.MonoFont, FontSize = 13,
            Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2),
            BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12)
        };
        _jsonState = new TextBlock { FontFamily = UrbeTheme.UiFont, FontSize = 13, Margin = new Thickness(2, 8, 2, 0), TextWrapping = TextWrapping.Wrap };
        _json.TextChanged += (_, _) => ValidateJson();
        var restore = SmallButton("Restaurar padrão", false);
        restore.Click += async (_, _) =>
        {
            var ok = await Dialogs.ChooseAsync("Restaurar padrão?", [new DialogItem("Restaurar", Danger: true), new DialogItem("Cancelar")],
                "Volta cores, texto e editor ao padrão. Temas salvos, estilos e plugins continuam na pasta.");
            if (ok == 0) { await Customization.ResetAsync(); Render(); }
        };
        var openNote = SmallButton("Abrir como nota", false);
        openNote.Click += async (_, _) =>
        {
            if (Customization.FileContent is null) await Customization.SetAsync(new JsonObject());
            if (Customization.FileContent is not null) { Close(); OpenFileRequested?.Invoke(LegacyCustomize.FilePath); }
        };
        var apply = SmallButton("Aplicar", true);
        apply.Click += async (_, _) =>
        {
            var n = ValidateJson();
            if (n is null || n.Errors.Count > 0) { await Dialogs.AlertAsync("Personalização", "Corrija os erros antes de aplicar"); return; }
            await Customization.WriteFileAsync(_json!.Text ?? "");
            Render();
        };
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { restore, openNote, apply } };
        foreach (var child in actions.Children) child.Margin = new Thickness(8, 0, 0, 8);
        _body.Children.Add(Block(LegacyCustomize.FilePath,
            Help("Tudo desta tela fica neste arquivo. Você pode editar aqui, abrir como nota, copiar para outro vault ou pedir ao Assistente que crie um tema para você."),
            _json, _jsonState, actions));
        if (Customization.Warnings.Count > 0)
            _body.Children.Add(Block("Avisos", Issues(Customization.Warnings)));
        _body.Children.Add(Block("Pedir ao Assistente", Help("Exemplos: “crie um tema sépia com fonte serifada e cantos retos”, “deixe o mapa sempre à noite e esconda os animais”, “faça um plugin que conta as palavras de hoje”. O Assistente conhece o formato e mostra a alteração antes de gravar.")));
        ValidateJson();
    }

    /// <summary>validarJson().</summary>
    private LegacyCustomize.Result? ValidateJson()
    {
        if (_json is null || _jsonState is null) return null;
        var n = Customization.Normalize(_json.Text ?? "");
        if (n.Errors.Count > 0)
        {
            _jsonState.Foreground = UrbeTheme.Brush(UrbeTheme.Danger);
            _jsonState.Text = string.Join("\n", n.Errors.Select(e => (e.Path.Length > 0 ? e.Path : "(arquivo)") + " " + e.Message));
        }
        else
        {
            _jsonState.Foreground = UrbeTheme.Brush(UrbeTheme.Ok);
            _jsonState.Text = "JSON válido" + (n.Warnings.Count > 0 ? " · " + n.Warnings.Count + " aviso(s)" : "");
        }
        return n;
    }

    // ---------------------------------------------------------------- pieces (customize.css)

    private static TextBlock Label(string text, double size, FontWeight weight, string color = UrbeTheme.Text) =>
        new() { Text = text, FontFamily = UrbeTheme.UiFont, FontSize = size, FontWeight = weight, Foreground = UrbeTheme.Brush(color), VerticalAlignment = VerticalAlignment.Center };

    private static TextBlock Help(string text) =>
        new() { Text = text, FontFamily = UrbeTheme.UiFont, FontSize = 13.5, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };

    private static Control Issues(IEnumerable<LegacyCustomize.Issue> issues)
    {
        var list = new StackPanel { Spacing = 4 };
        foreach (var w in issues)
            list.Children.Add(new TextBlock { Text = "• " + w.Path + " " + w.Message, FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text2), TextWrapping = TextWrapping.Wrap });
        return list;
    }

    /// <summary>bloco(titulo, corpo).</summary>
    private static Border Block(string title, params Control[] body) => BlockWith(title, null, body);

    /// <summary>bloco(titulo, corpo, extra): <paramref name="extra"/> sits at the right of the header.</summary>
    private static Border BlockWith(string title, Control? extra, params Control[] body)
    {
        var stack = new StackPanel();
        var heading = new TextBlock
        {
            Text = title.ToUpperInvariant(), FontFamily = UrbeTheme.UiFont, FontSize = 13, FontWeight = FontWeight.Bold, LetterSpacing = .52,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text2), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center
        };
        if (extra is null) { heading.Margin = new Thickness(0, 0, 0, 10); stack.Children.Add(heading); }
        else
        {
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(extra, Dock.Right);
            head.Children.Add(extra);
            head.Children.Add(heading);
            stack.Children.Add(head);
        }
        bool first = true;
        foreach (var b in body)
        {
            if (b is Border row && row.Tag as string == "row")
            {
                row.BorderThickness = first ? new Thickness(0) : new Thickness(0, 1, 0, 0);
                first = false;
            }
            stack.Children.Add(b);
        }
        return new Border
        {
            Child = stack, Margin = new Thickness(0, 0, 0, 18), Padding = new Thickness(16, 14), CornerRadius = new CornerRadius(16),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(1), Background = UrbeTheme.Brush(UrbeTheme.Surface2)
        };
    }

    /// <summary>.uc-row: title and detail on the left, the control on the right.</summary>
    private Border Row(string title, string? detail, Control control)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Label(title, 14.5, FontWeight.SemiBold));
        if (detail is not null)
            text.Children.Add(new TextBlock { Text = detail, FontFamily = UrbeTheme.UiFont, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextWrapping = TextWrapping.Wrap });
        Control content;
        if (_narrow && control is Border { Tag: "seg" })
            content = new StackPanel { Spacing = 8, Children = { text, control } };   // ≤520: column, segment stretched
        else
        {
            var dock = new DockPanel();
            control.Margin = new Thickness(14, 0, 0, 0);
            DockPanel.SetDock(control, Dock.Right);
            dock.Children.Add(control);
            dock.Children.Add(text);
            content = dock;
        }
        return new Border { Tag = "row", Child = content, MinHeight = 52, Padding = new Thickness(0, 6), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line) };
    }

    /// <summary>chave(nome, valor, rótulo, detalhe): an iOS-like switch that writes <paramref name="path"/> of tema.json.</summary>
    private Border Toggle(string path, bool value, string title, string detail) =>
        Toggle(value, title, detail, v => Customization.SetAsync(Patch(path, JsonValue.Create(v))));

    private Border Toggle(bool value, string title, string detail, Func<bool, Task> change)
    {
        var row = Row(title, detail, Switch(value));
        row.Background = Brushes.Transparent;
        row.Cursor = new Cursor(StandardCursorType.Hand);
        row.Tag = "row";
        AutomationName(row, title);
        row.PointerReleased += async (_, e) =>
        {
            e.Handled = true;
            await change(!value);
        };
        return row;
    }

    /// <summary>The track and knob of .uc-toggle i.</summary>
    private static Border Switch(bool value)
    {
        var knob = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = UrbeTheme.Brush(value ? UrbeTheme.AccentInk : UrbeTheme.Text2),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(value ? 21 : 3, 0, 0, 0) };
        return new Border { Width = 46, Height = 28, CornerRadius = new CornerRadius(14), Child = knob,
            Background = UrbeTheme.Brush(value ? UrbeTheme.Accent : UrbeTheme.Surface3), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(value ? 0 : 1) };
    }

    /// <summary>seg(nome, valor, opções): a segmented control.</summary>
    private Border Seg(string path, string value, (string Value, string Label)[] options)
    {
        var grid = new UniformGrid { Rows = 1 };
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var (v, label) in options)
        {
            bool on = v == value;
            var b = new Button
            {
                Content = label, MinHeight = 40, Padding = new Thickness(12, 0), CornerRadius = new CornerRadius(7), FontFamily = UrbeTheme.UiFont, FontSize = 13,
                FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(on ? UrbeTheme.Text : UrbeTheme.Text2),
                Background = on ? UrbeTheme.Brush(UrbeTheme.Surface3) : Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch
            };
            if (on) b.Classes.Add("on");
            b.Click += async (_, _) => await Customization.SetAsync(Patch(path, JsonValue.Create(v)));
            if (_narrow) grid.Children.Add(b); else stack.Children.Add(b);
        }
        return new Border
        {
            Tag = "seg", Child = _narrow ? grid : stack, Padding = new Thickness(3), CornerRadius = new CornerRadius(11), Background = UrbeTheme.Brush(UrbeTheme.Bg),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static Button SmallButton(string text, bool primary) => new()
    {
        Content = text, MinHeight = 40, Padding = new Thickness(14, 0), CornerRadius = new CornerRadius(12), FontFamily = UrbeTheme.UiFont, FontSize = 14,
        FontWeight = FontWeight.SemiBold, VerticalContentAlignment = VerticalAlignment.Center,
        Background = UrbeTheme.Brush(primary ? UrbeTheme.Accent : UrbeTheme.Surface3), Foreground = UrbeTheme.Brush(primary ? UrbeTheme.AccentInk : UrbeTheme.Text),
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(primary ? 0 : 1)
    };

    private static void AutomationName(Control c, string name) => Avalonia.Automation.AutomationProperties.SetName(c, name);

    /// <summary>patchDe(path, v).</summary>
    private static JsonObject Patch(string path, JsonNode value)
    {
        var keys = path.Split('.');
        var root = new JsonObject();
        var x = root;
        for (int i = 0; i < keys.Length - 1; i++) { var n = new JsonObject(); x[keys[i]] = n; x = n; }
        x[keys[^1]] = value;
        return root;
    }

    /// <summary>ui/icons.js shapes used by the panel.</summary>
    public static class Icons
    {
        public const string Palette = "M12 3a9 9 0 1 0 0 18c1.1 0 1.7-.8 1.7-1.7 0-.5-.2-.9-.5-1.2-.3-.3-.5-.7-.5-1.2 0-.9.8-1.7 1.7-1.7h2A4.6 4.6 0 0 0 21 10.6C21 6.4 17 3 12 3z M8.7 11a1.2 1.2 0 1 0-2.4 0 1.2 1.2 0 1 0 2.4 0z M11.2 7a1.2 1.2 0 1 0-2.4 0 1.2 1.2 0 1 0 2.4 0z M16.2 7a1.2 1.2 0 1 0-2.4 0 1.2 1.2 0 1 0 2.4 0z";
        public const string Type = "M5 7V5h14v2M12 5v14M9 19h6";
        public const string Edit = "M4.5 19.5h4l10-10-4-4-10 10z M13 7l4 4";
        public const string City = "M3 21h18 M5 21V9l5-3v15 M10 21V4l9 4v13 M13 10h3M13 14h3M13 18h3";
        public const string Image = "M6 4.5h12a2.5 2.5 0 0 1 2.5 2.5v10a2.5 2.5 0 0 1-2.5 2.5H6A2.5 2.5 0 0 1 3.5 17V7A2.5 2.5 0 0 1 6 4.5z M10.6 10a1.6 1.6 0 1 0-3.2 0 1.6 1.6 0 1 0 3.2 0z M20.5 16l-5-5-8.5 8.5";
        public const string Brush = "M18.4 3.6a2 2 0 0 1 2.9 2.9l-8.8 8.8-2.9-2.9z M9.6 12.4c-2 0-3.6 1.6-3.6 3.6 0 1.4-1 2.5-2.5 2.5 1 1.6 2.8 2.5 4.6 2.5 2.8 0 5-2.2 5-5z";
        public const string Puzzle = "M9 4.5a2 2 0 1 1 4 0V6h4a1 1 0 0 1 1 1v4h-1.5a2 2 0 1 0 0 4H18v4a1 1 0 0 1-1 1h-4v-1.5a2 2 0 1 0-4 0V20H5a1 1 0 0 1-1-1v-4h1.5a2 2 0 1 0 0-4H4V7a1 1 0 0 1 1-1h4z";
        public const string Code = "M9 7.5L4.5 12 9 16.5M15 7.5l4.5 4.5-4.5 4.5";
        public const string Check = "M5 12.5l4.5 4.5L19 7.5";
        public const string Restore = "M4.5 12a7.5 7.5 0 1 0 2.2-5.3L4.5 9 M4.5 4.5V9H9";
        public const string Download = "M12 4.5v11M7.5 11l4.5 4.5 4.5-4.5 M4.5 15v3a2 2 0 0 0 2 2h11a2 2 0 0 0 2-2v-3";
        public const string Shield = "M12 3 4.5 6v5.5c0 4.6 3.2 8.4 7.5 9.5 4.3-1.1 7.5-4.9 7.5-9.5V6z M9 12l2 2 4-4";
        public const string Pencil = "M4 20l1-4L16.5 4.5a2.1 2.1 0 0 1 3 3L8 19z M14.5 6.5l3 3";
        public const string Bucket = "M5 11l7-7 7 7-7 7z M19.5 14.5s1.5 2 1.5 3a1.5 1.5 0 0 1-3 0c0-1 1.5-3 1.5-3z";
        public const string Dropper = "M14.5 4.5l5 5M17 7l-9.5 9.5L5 19l2.5-2.5L17 7z M13 3l8 8";
        public const string Plus = "M12 5v14M5 12h14";
    }
}
