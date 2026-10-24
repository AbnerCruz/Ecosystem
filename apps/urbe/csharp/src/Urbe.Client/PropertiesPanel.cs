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

    private readonly StackPanel _body = new() { Spacing = 14 };
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _summary = new();
    private readonly TextBlock _chevron = new() { Text = "›", FontSize = 20 };
    private string _front = "";
    private bool _readOnly;

    /// <summary>The note's text now (the host finishes any block being written first).</summary>
    public Func<string>? TextSource { get; set; }

    /// <summary>The tags of the vault, for suggestions.</summary>
    public Func<IEnumerable<string>>? TagSource { get; set; }

    public UrbeDialogs? Dialogs { get; set; }

    /// <summary>The note's new text after a change of the form.</summary>
    public event Action<string>? Changed;

    public PropertiesPanel()
    {
        Margin = new Thickness(12, 6, 12, 8);
        CornerRadius = new CornerRadius(14);
        BorderThickness = new Thickness(1);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2, .8);
        Background = UrbeTheme.Brush(UrbeTheme.Accent, .035);
        ClipToBounds = true;
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), MinHeight = 44, Background = Brushes.Transparent, Margin = new Thickness(12, 0, 8, 0) };
        var glyph = UrbeTheme.Icon(UrbeTheme.Icons.List, 17, UrbeTheme.Accent, 1.7);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        glyph.Margin = new Thickness(0, 0, 9, 0);
        var title = new TextBlock { Text = "Propriedades", FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(title, 1);
        _summary.FontSize = 12;
        _summary.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _summary.VerticalAlignment = VerticalAlignment.Center;
        _summary.TextTrimming = TextTrimming.CharacterEllipsis;
        _summary.MaxLines = 1;
        _summary.Margin = new Thickness(8, 0, 4, 0);
        Grid.SetColumn(_summary, 2);
        _chevron.Foreground = UrbeTheme.Brush(UrbeTheme.Text3);
        _chevron.VerticalAlignment = VerticalAlignment.Center;
        _chevron.RenderTransformOrigin = RelativePoint.Center;
        Grid.SetColumn(_chevron, 3);
        head.Children.Add(glyph);
        head.Children.Add(title);
        head.Children.Add(_summary);
        head.Children.Add(_chevron);
        head.Cursor = new Cursor(StandardCursorType.Hand);
        head.Tapped += (_, e) => { e.Handled = true; Toggle(!_expanded); };
        Avalonia.Automation.AutomationProperties.SetName(head, "Propriedades da nota");
        _scroll = new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(14, 4, 14, 14), Child = _body },
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            MaxHeight = 520, IsVisible = false
        };
        Child = new StackPanel { Children = { head, new Border { Height = 1, Background = UrbeTheme.Brush(UrbeTheme.Line2, .6), IsVisible = false, Name = "rule" }, _scroll } };
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
        if (Child is StackPanel { Children: [_, Border rule, ..] }) rule.IsVisible = open;
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
        var other = props.Items.Count(p => p.Key.Length > 0 && !p.Key.Equals("aliases", StringComparison.OrdinalIgnoreCase) && !p.Key.Equals("tags", StringComparison.OrdinalIgnoreCase));
        var parts = new List<string>();
        if (aliases > 0) parts.Add(aliases == 1 ? "1 alias" : aliases + " aliases");
        if (tags > 0) parts.Add(tags == 1 ? "1 tag" : tags + " tags");
        if (other > 0) parts.Add(other == 1 ? "1 propriedade" : other + " propriedades");
        _summary.Text = parts.Count > 0 ? string.Join(" · ", parts) : (_readOnly ? "nenhuma" : "adicionar");
        if (!_scroll.IsVisible) return;
        _body.Children.Clear();
        _body.Children.Add(ListRow("Aliases", "aliases", props.Aliases, "outro nome da nota", false));
        _body.Children.Add(ListRow("Tags", "tags", props.Tags, "nova tag", true));
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

    private static TextBlock Label(string text) => new()
    {
        Text = text, FontSize = 11.5, FontWeight = FontWeight.SemiBold, LetterSpacing = .3,
        Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(2, 0, 0, 5)
    };

    private TextBox Input(string value, string hint)
    {
        var box = new TextBox
        {
            Text = value, PlaceholderText = hint, AcceptsReturn = false, MinHeight = 40, Padding = new Thickness(12, 9),
            CornerRadius = new CornerRadius(10), FontSize = 14, FontFamily = UrbeTheme.UiFont,
            Background = UrbeTheme.Brush(UrbeTheme.Bg), Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(1), CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent),
            IsReadOnly = _readOnly
        };
        box.Resources["TextControlBackgroundFocused"] = UrbeTheme.Brush(UrbeTheme.Bg);
        box.Resources["TextControlBorderBrushFocused"] = UrbeTheme.Brush(UrbeTheme.Accent);
        return box;
    }

    private Control Row(NoteProperty p)
    {
        var head = new DockPanel { LastChildFill = true };
        var label = Label(p.Key);
        if (!_readOnly)
        {
            var remove = new Button
            {
                Content = new TextBlock { Text = "×", FontSize = 16, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }, Padding = new Thickness(8, 0), MinHeight = 0, MinWidth = 0,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0, -6, 0, 0), HorizontalAlignment = HorizontalAlignment.Right
            };
            Avalonia.Automation.AutomationProperties.SetName(remove, "Remover " + p.Key);
            ToolTip.SetTip(remove, "Remover a propriedade");
            remove.Click += (_, _) => Apply(n => n.Remove(p.Key), rebuild: true);
            DockPanel.SetDock(remove, Dock.Right);
            head.Children.Add(remove);
        }
        head.Children.Add(label);
        var holder = new StackPanel { Children = { head } };
        switch (p.Kind)
        {
            case PropertyKind.List:
                return ListRow(p.Key, p.Key, p.Items, "novo item", false);
            case PropertyKind.Check:
            {
                var on = new CheckBox { Content = p.Value == "true" ? "Sim" : "Não", IsChecked = p.Value == "true", IsEnabled = !_readOnly, MinHeight = 36 };
                on.IsCheckedChanged += (_, _) =>
                {
                    on.Content = on.IsChecked == true ? "Sim" : "Não";
                    Apply(n => n.Set(p.Key, PropertyKind.Check, on.IsChecked == true ? "true" : "false"), rebuild: false);
                };
                holder.Children.Add(on);
                break;
            }
            case PropertyKind.Raw:
                holder.Children.Add(new TextBlock
                {
                    Text = "Propriedade avançada: edite na Fonte.", FontSize = 12.5, FontStyle = FontStyle.Italic, TextWrapping = TextWrapping.Wrap,
                    Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(2, 0)
                });
                break;
            default:
            {
                var box = Input(p.Value, p.Kind switch { PropertyKind.Date => "AAAA-MM-DD", PropertyKind.Number => "0", _ => "valor" });
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
                    holder.Children.Add(line);
                }
                else holder.Children.Add(box);
                break;
            }
        }
        return holder;
    }

    private static Button MiniButton(string text) => new()
    {
        Content = new TextBlock { Text = text, FontSize = 12.5, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) },
        Padding = new Thickness(12, 8), Margin = new Thickness(8, 0, 0, 0), CornerRadius = new CornerRadius(10),
        Background = UrbeTheme.Brush(UrbeTheme.Accent, .12), BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Stretch
    };

    private Control ListRow(string label, string key, IReadOnlyList<string> items, string hint, bool tags)
    {
        var chips = new WrapPanel { Orientation = Orientation.Horizontal };
        void Save(IEnumerable<string> next) => Apply(n => n.SetList(key, next), rebuild: true);
        foreach (var item in items)
        {
            var text = new TextBlock { Text = (tags ? "#" : "") + item, FontSize = 13, Foreground = UrbeTheme.Brush(UrbeTheme.Text), VerticalAlignment = VerticalAlignment.Center };
            var line = new StackPanel { Orientation = Orientation.Horizontal, Children = { text } };
            if (!_readOnly)
            {
                var x = new Button
                {
                    Content = new TextBlock { Text = "×", FontSize = 14, Foreground = UrbeTheme.Brush(UrbeTheme.Text3) }, Padding = new Thickness(6, 0, 0, 0), MinHeight = 0, MinWidth = 0,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Center
                };
                Avalonia.Automation.AutomationProperties.SetName(x, "Remover " + item);
                var captured = item;
                x.Click += (_, _) => Save(items.Where(i => i != captured));
                line.Children.Add(x);
            }
            chips.Children.Add(new Border
            {
                Child = line, Padding = new Thickness(11, 5, 8, 5), Margin = new Thickness(0, 0, 6, 6), CornerRadius = new CornerRadius(999),
                Background = UrbeTheme.Brush(UrbeTheme.Accent, tags ? .16 : .08), BorderThickness = new Thickness(1), BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent, .3)
            });
        }
        var holder = new StackPanel { Children = { Label(label), chips } };
        if (_readOnly)
        {
            if (items.Count == 0) holder.Children.Add(new TextBlock { Text = "—", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), Margin = new Thickness(2, 0) });
            return holder;
        }
        var input = Input("", hint);
        input.Margin = new Thickness(0, 2, 0, 0);
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
        holder.Children.Add(input);
        if (tags && TagSource is not null)
        {
            var offered = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            holder.Children.Add(offered);
            void Suggest()
            {
                offered.Children.Clear();
                var typed = NoteProperties.NormalizeTag(input.Text ?? "");
                foreach (var tag in TagSource().Where(t => !items.Contains(t, StringComparer.OrdinalIgnoreCase) && (typed.Length == 0 || t.Contains(typed, StringComparison.OrdinalIgnoreCase))).Take(8))
                {
                    var chip = new Button
                    {
                        Content = new TextBlock { Text = "#" + tag, FontSize = 12.5, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) }, Padding = new Thickness(10, 5), Margin = new Thickness(0, 0, 6, 6),
                        CornerRadius = new CornerRadius(999), Background = UrbeTheme.Brush(UrbeTheme.Surface2), BorderThickness = new Thickness(0), MinHeight = 0
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
        return holder;
    }

    // ------------------------------------------------------------------ a new property

    private Control AddButton()
    {
        var add = new Button
        {
            Content = new TextBlock { Text = "+  Adicionar propriedade", FontSize = 13.5, Foreground = UrbeTheme.Brush(UrbeTheme.Accent), FontWeight = FontWeight.SemiBold },
            Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(10), HorizontalAlignment = HorizontalAlignment.Left,
            Background = UrbeTheme.Brush(UrbeTheme.Accent, .1), BorderThickness = new Thickness(0)
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
