using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The properties of a note as a form (24/10/2026), above the text in Visual and in Fonte: aliases and tags as chips (tags
/// suggested from the vault), text, number, date and yes/no each with their own control, and "Adicionar propriedade". It
/// reads and writes the frontmatter through <see cref="NoteProperties"/>, so what the form does not understand stays as it
/// was. Folded to one line by default; read-only when the note is only being read.
/// </summary>
public sealed class PropertiesPanel : Border
{
    private static readonly string[] Suggested = ["autor", "status", "data", "url", "prioridade", "concluído", "cssclasses"];
    private bool _expanded;

    private readonly StackPanel _body = new() { Spacing = 2 };
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _summary = new();
    private readonly TextBlock _chevron = new() { Text = "›", FontSize = 18 };
    private string _front = "";
    private IReadOnlyList<InheritedTag> _inherited = [];
    private bool _readOnly;

    /// <summary>The note's text now (the host finishes any block being written first).</summary>
    public Func<string>? TextSource { get; set; }

    /// <summary>The tags of the vault, for suggestions.</summary>
    public Func<IEnumerable<string>>? TagSource { get; set; }

    /// <summary>The tags the note inherits from its project, for its text now (DEC-0058).</summary>
    public Func<string, IReadOnlyList<InheritedTag>>? InheritedSource { get; set; }

    public UrbeDialogs? Dialogs { get; set; }

    /// <summary>The note's new text after a change of the form.</summary>
    public event Action<string>? Changed;

    public PropertiesPanel()
    {
        Margin = new Thickness(14, 4, 14, 6);
        BorderThickness = new Thickness(0, 0, 0, 1);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2, .6);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), MinHeight = 40, Background = Brushes.Transparent };
        _chevron.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _chevron.VerticalAlignment = VerticalAlignment.Center;
        _chevron.RenderTransformOrigin = RelativePoint.Center;
        _chevron.Margin = new Thickness(0, -2, 8, 0);
        _chevron.Width = 12;
        var title = new TextBlock
        {
            Text = "PROPRIEDADES", FontSize = 11, FontWeight = FontWeight.SemiBold, LetterSpacing = .9,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(title, 1);
        _summary.FontSize = 12;
        _summary.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _summary.VerticalAlignment = VerticalAlignment.Center;
        _summary.TextTrimming = TextTrimming.CharacterEllipsis;
        _summary.MaxLines = 1;
        _summary.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(_summary, 2);
        head.Children.Add(_chevron);
        head.Children.Add(title);
        head.Children.Add(_summary);
        head.Cursor = new Cursor(StandardCursorType.Hand);
        head.Tapped += (_, e) => { e.Handled = true; Toggle(!_expanded); };
        Avalonia.Automation.AutomationProperties.SetName(head, "Propriedades da nota");
        _scroll = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(0, 2, 0, 12), Child = _body },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            MaxHeight = 520, IsVisible = false
        };
        Child = new StackPanel { Children = { head, _scroll } };
        Toggle(_expanded);
    }

    /// <summary>Whether the form is open.</summary>
    public bool IsExpanded => _scroll.IsVisible;

    /// <summary>Opens or folds the form.</summary>
    public void Expand(bool open) => Toggle(open);

    /// <summary>The body's room: a part of the screen, with its own scroll beyond it.</summary>
    public void LimitHeight(double screenHeight) => _scroll.MaxHeight = Math.Max(180, screenHeight * .62);

    public bool ReadOnly
    {
        get => _readOnly;
        set { if (_readOnly == value) return; _readOnly = value; Rebuild(); }
    }

    private void Toggle(bool open)
    {
        _expanded = open;
        _scroll.IsVisible = open;
        _chevron.RenderTransform = open ? new RotateTransform(90) : null;
        if (open) Rebuild();
    }

    /// <summary>A note opened or its text changed from outside: the form follows when its frontmatter did.</summary>
    public void Sync(string text, bool force = false)
    {
        var front = FrontOf(text);
        if (!force && front == _front) return;
        _front = front;
        Rebuild(text);
    }

    private static string FrontOf(string text)
    {
        if (!text.StartsWith("---", StringComparison.Ordinal)) return "";
        var end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
        return end < 0 ? "" : text[..end];
    }

    private void Rebuild(string? text = null)
    {
        text ??= TextSource?.Invoke() ?? "";
        var props = NoteProperties.Parse(text);
        var aliases = props.Aliases.Count;
        var tags = props.Tags.Count;
        var inherited = _inherited = InheritedSource?.Invoke(text) ?? [];
        var other = props.Items.Count(p => p.Key.Length > 0 && !p.Key.Equals("aliases", StringComparison.OrdinalIgnoreCase) && !p.Key.Equals("tags", StringComparison.OrdinalIgnoreCase));
        var parts = new List<string>();
        if (aliases > 0) parts.Add(aliases == 1 ? "1 alias" : aliases + " aliases");
        if (tags > 0) parts.Add(tags == 1 ? "1 tag" : tags + " tags");
        if (inherited.Count > 0) parts.Add(inherited.Count == 1 ? "1 herdada" : inherited.Count + " herdadas");
        if (other > 0) parts.Add(other == 1 ? "1 propriedade" : other + " propriedades");
        _summary.Text = parts.Count > 0 ? string.Join(" · ", parts) : (_readOnly ? "nenhuma" : "adicionar");
        if (!_scroll.IsVisible) return;
        _body.Children.Clear();
        _body.Children.Add(ListRow("aliases", "aliases", props.Aliases, "adicionar", false));
        _body.Children.Add(ListRow("tags", "tags", props.Tags, "adicionar", true));
        if (inherited.Count > 0) _body.Children.Add(InheritedRow(inherited));
        foreach (var p in props.Items)
        {
            if (p.Key.Length == 0 || p.Key.Equals("aliases", StringComparison.OrdinalIgnoreCase) || p.Key.Equals("tags", StringComparison.OrdinalIgnoreCase)) continue;
            _body.Children.Add(Row(p));
        }
        if (!_readOnly) _body.Children.Add(AddButton());
    }

    // ------------------------------------------------------------------ writing

    private void Apply(Func<NoteProperties, NoteProperties> change, bool rebuild)
    {
        if (_readOnly) return;
        var now = TextSource?.Invoke() ?? "";
        var next = change(NoteProperties.Parse(now)).Render();
        if (next == now) return;
        _front = FrontOf(next);
        Changed?.Invoke(next);
        if (rebuild) Rebuild(next);
        else RefreshSummary(next);
    }

    private void RefreshSummary(string text)
    {
        var keep = _scroll.IsVisible;
        _scroll.IsVisible = false;
        Rebuild(text);
        _scroll.IsVisible = keep;
    }

    // ------------------------------------------------------------------ rows

    private static string Glyph(string key, PropertyKind kind) => key.ToLowerInvariant() switch
    {
        "aliases" => "@", "tags" => "#", "cssclasses" => "≡",
        _ => kind switch { PropertyKind.Number => "1", PropertyKind.Date => "◷", PropertyKind.Check => "✓", PropertyKind.List => "≡", PropertyKind.Raw => "{}", _ => "Aa" }
    };

    /// <summary>One line of the form: a glyph and the name on the left, the value on the right, and a quiet × to take the property away.</summary>
    private Control Line(string glyph, string name, Control value, Action? remove)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("112,*,Auto"), MinHeight = 38, Margin = new Thickness(0, 1) };
        var key = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 9, 8, 0) };
        key.Children.Add(new TextBlock { Text = glyph, Width = 18, FontSize = 12, TextAlignment = TextAlignment.Center, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), FontWeight = FontWeight.SemiBold });
        key.Children.Add(new TextBlock { Text = name, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 80 });
        grid.Children.Add(key);
        Grid.SetColumn(value, 1);
        value.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(value);
        if (remove is not null && !_readOnly)
        {
            var x = new Button
            {
                Content = new TextBlock { Text = "×", FontSize = 16, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }, Padding = new Thickness(10, 0), MinHeight = 38, MinWidth = 0,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0)
            };
            Avalonia.Automation.AutomationProperties.SetName(x, "Remover " + name);
            ToolTip.SetTip(x, "Remover a propriedade");
            x.Click += (_, _) => remove();
            Grid.SetColumn(x, 2);
            grid.Children.Add(x);
        }
        return grid;
    }

    /// <summary>A field with no box of its own: it shows a soft background when touched.</summary>
    private TextBox Input(string value, string hint)
    {
        var soft = UrbeTheme.Brush(UrbeTheme.Surface2, .9);
        var box = new TextBox
        {
            Text = value, PlaceholderText = hint, AcceptsReturn = false, MinHeight = 36, Padding = new Thickness(10, 8),
            CornerRadius = new CornerRadius(8), FontSize = 14, FontFamily = UrbeTheme.UiFont, BorderThickness = new Thickness(0),
            Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Text), CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent),
            IsReadOnly = _readOnly
        };
        foreach (var name in new[] { "TextControlBackground", "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            box.Resources[name] = Brushes.Transparent;
        box.Resources["TextControlBackgroundPointerOver"] = soft;
        box.Resources["TextControlBackgroundFocused"] = soft;
        return box;
    }

    private Control Row(NoteProperty p)
    {
        Control value;
        switch (p.Kind)
        {
            case PropertyKind.List:
                return ListRow(p.Key, p.Key, p.Items, "adicionar", false, () => Apply(n => n.Remove(p.Key), rebuild: true));
            case PropertyKind.Check:
            {
                var on = new CheckBox { Content = p.Value == "true" ? "Sim" : "Não", IsChecked = p.Value == "true", IsEnabled = !_readOnly, MinHeight = 36, Margin = new Thickness(6, 0, 0, 0) };
                on.IsCheckedChanged += (_, _) =>
                {
                    on.Content = on.IsChecked == true ? "Sim" : "Não";
                    Apply(n => n.Set(p.Key, PropertyKind.Check, on.IsChecked == true ? "true" : "false"), rebuild: false);
                };
                value = on;
                break;
            }
            case PropertyKind.Raw:
                value = new TextBlock
                {
                    Text = "avançada: edite na Fonte", FontSize = 12.5, FontStyle = FontStyle.Italic, TextWrapping = TextWrapping.Wrap,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(10, 0)
                };
                break;
            default:
            {
                var box = Input(p.Value, p.Kind switch { PropertyKind.Date => "AAAA-MM-DD", PropertyKind.Number => "0", _ => "vazio" });
                void Commit()
                {
                    var typed = (box.Text ?? "").Trim();
                    if (p.Kind == PropertyKind.Number && typed.Length > 0 && !decimal.TryParse(typed.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) { box.Text = p.Value; return; }
                    if (p.Kind == PropertyKind.Date && typed.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(typed, @"^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2})?)?$")) { box.Text = p.Value; return; }
                    if (typed == p.Value) return;
                    Apply(n => n.Set(p.Key, p.Kind, typed), rebuild: false);
                }
                box.LostFocus += (_, _) => Commit();
                box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Commit(); } };
                if (p.Kind == PropertyKind.Date && !_readOnly)
                {
                    var today = MiniButton("Hoje");
                    Avalonia.Automation.AutomationProperties.SetName(today, "Hoje");
                    today.Click += (_, _) => Apply(n => n.Set(p.Key, PropertyKind.Date, DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), rebuild: true);
                    var line = new DockPanel();
                    DockPanel.SetDock(today, Dock.Right);
                    line.Children.Add(today);
                    line.Children.Add(box);
                    value = line;
                }
                else value = box;
                break;
            }
        }
        return Line(Glyph(p.Key, p.Kind), p.Key, value, () => Apply(n => n.Remove(p.Key), rebuild: true));
    }

    private static Button MiniButton(string text) => new()
    {
        Content = new TextBlock { Text = text, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) },
        Padding = new Thickness(10, 6), Margin = new Thickness(6, 0, 0, 0), CornerRadius = new CornerRadius(8), MinHeight = 0,
        Background = UrbeTheme.Brush(UrbeTheme.Accent, .1), BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center
    };

    private Control ListRow(string name, string key, IReadOnlyList<string> items, string hint, bool tags, Action? remove = null)
    {
        var chips = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        void Save(IEnumerable<string> next) => Apply(n => n.SetList(key, next), rebuild: true);
        foreach (var item in items)
        {
            var text = new TextBlock { Text = (tags ? "#" : "") + item, FontSize = 13, Foreground = UrbeTheme.Brush(tags ? UrbeTheme.Accent : UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center };
            var line = new StackPanel { Orientation = Orientation.Horizontal, Children = { text } };
            if (!_readOnly)
            {
                var x = new Button
                {
                    Content = new TextBlock { Text = "×", FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }, Padding = new Thickness(7, 0, 0, 0), MinHeight = 0, MinWidth = 0,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center
                };
                Avalonia.Automation.AutomationProperties.SetName(x, "Remover " + item);
                var captured = item;
                x.Click += (_, _) => Save(items.Where(i => i != captured));
                line.Children.Add(x);
            }
            chips.Children.Add(new Border
            {
                Child = line, Padding = new Thickness(10, 4, _readOnly ? 10 : 6, 4), Margin = new Thickness(0, 3, 6, 3), CornerRadius = new CornerRadius(999),
                Background = tags ? UrbeTheme.Brush(UrbeTheme.Accent, .14) : UrbeTheme.Brush(UrbeTheme.Surface2)
            });
        }
        var holder = new StackPanel { Children = { chips } };
        if (_readOnly)
        {
            if (items.Count == 0) chips.Children.Add(new TextBlock { Text = "—", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center });
            return Line(Glyph(key, PropertyKind.List), name, holder, null);
        }
        var input = Input("", hint);
        input.MinWidth = 96;
        input.Width = items.Count == 0 ? 200 : 120;
        input.Margin = new Thickness(0, 1, 0, 1);
        void Add(string typed)
        {
            foreach (var piece in typed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var value = tags ? NoteProperties.NormalizeTag(piece) : piece;
                if (value.Length > 0 && !items.Contains(value, StringComparer.OrdinalIgnoreCase)) items = [.. items, value];
            }
            input.Text = "";
            Save(items);
        }
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter && (input.Text ?? "").Trim().Length > 0) { e.Handled = true; Add(input.Text!); } };
        input.LostFocus += (_, _) => { if ((input.Text ?? "").Trim().Length > 0) Add(input.Text!); };
        chips.Children.Add(input);
        if (tags && TagSource is not null)
        {
            var offered = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
            holder.Children.Add(offered);
            void Suggest()
            {
                offered.Children.Clear();
                var typed = NoteProperties.NormalizeTag(input.Text ?? "");
                // a tag the note already inherits is not offered again
                foreach (var tag in TagSource().Where(t => !items.Contains(t, StringComparer.OrdinalIgnoreCase) && !_inherited.Any(i => i.Tag.Equals(t, StringComparison.OrdinalIgnoreCase)) && (typed.Length == 0 || t.Contains(typed, StringComparison.OrdinalIgnoreCase))).Take(6))
                {
                    var chip = new Button
                    {
                        Content = new TextBlock { Text = "+ #" + tag, FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }, Padding = new Thickness(9, 4), Margin = new Thickness(0, 0, 6, 4),
                        CornerRadius = new CornerRadius(999), Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), MinHeight = 0
                    };
                    var captured = tag;
                    Avalonia.Automation.AutomationProperties.SetName(chip, "Usar a tag " + tag);
                    chip.Click += (_, _) => { input.Text = ""; Save([.. items, captured]); };
                    offered.Children.Add(chip);
                }
            }
            input.TextChanged += (_, _) => Suggest();
            Suggest();
        }
        return Line(Glyph(key, PropertyKind.List), name, holder, remove);
    }

    /// <summary>
    /// The project's tags: each with the folder it comes from ("#worldbuilding · de Droerni"). They are not written in this
    /// note; × refuses one here (sem-tags), which is reversible by removing it from sem-tags.
    /// </summary>
    private Control InheritedRow(IReadOnlyList<InheritedTag> inherited)
    {
        var chips = new WrapPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var tag in inherited)
        {
            // × on the right; when the row is narrow the folder's name gives way first, then the tag (both trimmed, never cut)
            var name = new TextBlock { Text = "#" + tag.Tag, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Accent), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            var from = new TextBlock
            {
                Text = "de " + ProjectTags.FolderName(tag), FontSize = 12, Foreground = UrbeTheme.Brush(UrbeTheme.Text3), VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis
            };
            var words = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(from, 1);
            words.Children.Add(name);
            words.Children.Add(from);
            var line = new DockPanel();
            ToolTip.SetTip(line, ProjectTags.Describe(tag) + " (" + tag.FolderNote + ")");
            Avalonia.Automation.AutomationProperties.SetName(line, ProjectTags.Describe(tag));
            if (!_readOnly)
            {
                var x = new Button
                {
                    Content = new TextBlock { Text = "×", FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }, Padding = new Thickness(8, 0, 0, 0), MinHeight = 0, MinWidth = 0,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center
                };
                Avalonia.Automation.AutomationProperties.SetName(x, "Não herdar " + tag.Tag);
                var captured = tag.Tag;
                x.Click += (_, _) => Apply(n => n.SetList("sem-tags", [.. ItemsOf(n.Get("sem-tags")), captured]), rebuild: true);
                DockPanel.SetDock(x, Dock.Right);
                line.Children.Add(x);
            }
            line.Children.Add(words);
            chips.Children.Add(new Border
            {
                Child = line, Padding = new Thickness(10, 4, _readOnly ? 10 : 6, 4), Margin = new Thickness(0, 3, 6, 3), CornerRadius = new CornerRadius(999),
                BorderThickness = new Thickness(1), BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent, .35), Background = Brushes.Transparent
            });
        }
        return Line(Glyph("tags", PropertyKind.List), "herdadas", chips, null);
    }

    private static IEnumerable<string> ItemsOf(NoteProperty? p) =>
        p is null ? [] : p.Items.Count > 0 ? p.Items : p.Value.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);

    // ------------------------------------------------------------------ a new property

    private Control AddButton()
    {
        var add = new Button
        {
            Content = new TextBlock { Text = "+  Propriedade", FontSize = 13.5, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) },
            Padding = new Thickness(10, 9), Margin = new Thickness(0, 4, 0, 0), CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), MinHeight = 0
        };
        Avalonia.Automation.AutomationProperties.SetName(add, "Adicionar propriedade");
        add.Click += async (_, _) => await AddPropertyAsync();
        return add;
    }

    private async Task AddPropertyAsync()
    {
        if (Dialogs is not { } dialogs) return;
        var present = NoteProperties.Parse(TextSource?.Invoke() ?? "").Items.Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? key = null;
        var items = Suggested.Where(k => !present.Contains(k)).Select(k => new DialogItem(k, UrbeTheme.Icons.List, Run: () => { key = k; return Task.CompletedTask; })).ToList();
        items.Add(new DialogItem("Outra…", UrbeTheme.Icons.Pencil, "Dê o nome que quiser.", Run: async () =>
        {
            key = await dialogs.PromptAsync(new PromptOptions("Nova propriedade", Label: "Nome", Placeholder: "ex.: autor", Confirm: "Continuar",
                Validate: v => System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), @"^[\p{L}\p{N}_ -]{1,40}$") ? (present.Contains(v.Trim()) ? "Já existe." : null) : "Use letras, números, espaço, - ou _."));
            key = key?.Trim();
        }));
        await dialogs.MenuAsync("Nova propriedade", items);
        if (string.IsNullOrEmpty(key)) return;
        var kind = key.ToLowerInvariant() switch
        {
            "aliases" or "tags" or "cssclasses" => (PropertyKind?)PropertyKind.List,
            "data" or "date" or "criado" or "created" or "prazo" => PropertyKind.Date,
            "prioridade" => PropertyKind.Number,
            "concluído" or "concluido" or "done" or "publicado" => PropertyKind.Check,
            "autor" or "status" or "url" => PropertyKind.Text,
            _ => null
        };
        if (kind is null)
        {
            var kinds = new (string Name, PropertyKind Kind)[] { ("Texto", PropertyKind.Text), ("Lista", PropertyKind.List), ("Número", PropertyKind.Number), ("Data", PropertyKind.Date), ("Sim ou não", PropertyKind.Check) };
            PropertyKind? chosen = null;
            await dialogs.MenuAsync("Tipo de “" + key + "”", [.. kinds.Select(k => new DialogItem(k.Name, UrbeTheme.Icons.List, Run: () => { chosen = k.Kind; return Task.CompletedTask; }))]);
            if (chosen is null) return;
            kind = chosen;
        }
        var k2 = key;
        Apply(n => kind switch
        {
            PropertyKind.List => n.SetList(k2, []),
            PropertyKind.Check => n.Set(k2, PropertyKind.Check, "false"),
            PropertyKind.Number => n.Set(k2, PropertyKind.Number, "0"),
            PropertyKind.Date => n.Set(k2, PropertyKind.Date, DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            _ => n.Set(k2, PropertyKind.Text, "")
        }, rebuild: true);
    }
}
