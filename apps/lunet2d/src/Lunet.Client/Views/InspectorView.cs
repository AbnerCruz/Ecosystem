using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Runtime.Inspection;

namespace Lunet.Client.Views;

/// <summary>
/// The Inspector of the running game (M6): the game's fields and properties, refreshed live, edited in place. Godot's
/// Remote tree and Unity's Inspector in play mode teach the same two rules, kept here: the values are those of the game
/// that runs, and a change lasts until it stops (the code is untouched). A filter finds a field in a big game.
/// </summary>
public sealed class InspectorView : UserControl
{
    /// <summary>How often values are read again while the panel is open.</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(300);

    private sealed class Row(InspectorItem item, Control view)
    {
        public InspectorItem Item = item;
        public Control View = view;
        public Action<InspectorItem>? Update;
    }

    private readonly LunetShell _shell;
    private readonly DispatcherTimer _timer = new() { Interval = RefreshInterval };
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly TextBox _filter = new()
    {
        PlaceholderText = "Filtrar campos", MinHeight = LunetTokens.MinTouch, FontFamily = LunetTokens.UiFont,
        FontSize = LunetTokens.Type.Callout, VerticalContentAlignment = VerticalAlignment.Center,
    };
    private readonly List<Row> _rows = [];
    private readonly DockPanel _root = new();
    private readonly ScrollViewer _scroll = new();
    private string _signature = "";
    private int _skipped; // rows of the model not shown (the class-name header)

    public InspectorView(LunetShell shell)
    {
        _shell = shell;
        Inspection = new GameInspection(shell.Game);
        Inspection.Changed += drawn => { if (drawn) _shell.WorkspaceView.Game.Blit(); RefreshValues(); };
        AutomationProperties.SetName(_filter, "Filtrar campos do Inspector");
        _filter.TextChanged += (_, _) => ApplyFilter();
        _timer.Tick += (_, _) => Refresh();
        AttachedToVisualTree += (_, _) => { Refresh(); _timer.Start(); };
        DetachedFromVisualTree += (_, _) => _timer.Stop();
        _shell.Game.StateChanged += () => { if (IsAttachedToVisualTree()) Refresh(); };
        _scroll.Content = new Border { Child = _list, Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.M) };
        Content = _root;
    }

    public GameInspection Inspection { get; }

    /// <summary>Rows currently built (tests read them).</summary>
    public IReadOnlyList<InspectorItem> Rows => _rows.Select(r => r.Item).ToArray();

    private bool IsAttachedToVisualTree() => TopLevel.GetTopLevel(this) is not null;

    public void Refresh()
    {
        var items = Inspection.Items();
        if (Inspection.Game is null)
        {
            if (_signature == "none") return;
            _signature = "none";
            _rows.Clear();
            _list.Children.Clear();
            _root.Children.Clear();
            _root.Children.Add(LunetUi.EmptyState(LunetIcons.Inspector, "O jogo não está rodando",
                "Rode o jogo para ver e mudar os valores dele aqui, ao vivo. A mudança vale até o jogo parar; o código não muda.",
                LunetUi.Button("Rodar", ButtonKind.Primary, LunetIcons.Play, () => _shell.RunGame())));
            return;
        }
        var signature = GameInspection.Signature(items) + "|" + _shell.Game.State; // the note says paused or live
        if (signature != _signature)
        {
            _signature = signature;
            Rebuild(items);
            return;
        }
        for (var i = 0; i < _rows.Count && i + _skipped < items.Count; i++) _rows[i].Item = items[i + _skipped];
        RefreshValues();
    }

    private void RefreshValues()
    {
        foreach (var row in _rows) row.Update?.Invoke(row.Item);
    }

    private void Rebuild(IReadOnlyList<InspectorItem> items)
    {
        _rows.Clear();
        _list.Children.Clear();
        // The first row names the game's class; it goes in the note above, not as a section that repeats a group name.
        var gameName = items.Count > 0 && items[0].Kind == InspectorItemKind.Header && items[0].Depth == 0 ? items[0].Label : null;
        _skipped = gameName is null ? 0 : 1;
        foreach (var item in items.Skip(_skipped))
        {
            var row = new Row(item, new Panel());
            row.View = Build(row);
            _rows.Add(row);
            _list.Children.Add(row.View);
        }
        if (!items.Any(i => i.Kind is InspectorItemKind.Field or InspectorItemKind.Button))
            _list.Children.Add(LunetUi.Secondary("Este jogo ainda não tem campos para mostrar. Campos e propriedades públicos da classe do jogo aparecem aqui; [Inspect] mostra um campo privado."));
        _root.Children.Clear();
        var top = new StackPanel { Spacing = LunetTokens.Space.Xs, Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.Xs) };
        var prefix = gameName is null ? "" : gameName + " · ";
        top.Children.Add(LunetUi.Text(prefix + (_shell.Game.State == GameState.Paused
            ? "pausado: a mudança aparece na hora; avance um quadro para ver o jogo reagir."
            : "valores ao vivo. A mudança vale até o jogo parar; o código não muda."), LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));
        if (items.Count(i => i.Kind == InspectorItemKind.Field) > 12) top.Children.Add(_filter.Parent is null ? _filter : Detach(_filter));
        DockPanel.SetDock(top, Dock.Top);
        _root.Children.Add(top);
        _root.Children.Add(_scroll);
        ApplyFilter();
    }

    private static Control Detach(Control c)
    {
        if (c.Parent is Panel p) p.Children.Remove(c);
        return c;
    }

    private void ApplyFilter()
    {
        var query = _filter.Text?.Trim() ?? "";
        foreach (var row in _rows)
            row.View.IsVisible = query.Length == 0 || row.Item.Kind != InspectorItemKind.Field
                || row.Item.Label.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private Control Build(Row row)
    {
        var item = row.Item;
        var indent = new Thickness(item.Depth * LunetTokens.Space.M, 0, 0, 0);
        switch (item.Kind)
        {
            case InspectorItemKind.Header:
            {
                var t = LunetUi.Text(item.Label, LunetTokens.Type.Caption, LunetTokens.Color.Primary, FontWeight.SemiBold);
                t.Margin = indent + new Thickness(0, LunetTokens.Space.S, 0, LunetTokens.Space.Xxs);
                return t;
            }
            case InspectorItemKind.Label:
            {
                var t = LunetUi.Text(item.Label, LunetTokens.Type.Label, LunetTokens.Color.TextSecondary);
                t.Margin = indent;
                return t;
            }
            case InspectorItemKind.Separator:
                return new Border { Height = 1, Margin = new Thickness(0, LunetTokens.Space.Xs), Background = LunetTokens.Brush(LunetTokens.Color.Border) };
            case InspectorItemKind.Button:
            {
                var b = LunetUi.Button(item.Label, ButtonKind.Secondary, null, () => Inspection.Press(row.Item));
                b.Margin = indent;
                b.HorizontalAlignment = HorizontalAlignment.Left;
                return b;
            }
            default:
                return BuildField(row, indent);
        }
    }

    private Control BuildField(Row row, Thickness indent)
    {
        var item = row.Item;
        var label = new StackPanel { Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(LunetUi.Text(item.Label, LunetTokens.Type.Callout));
        if (!string.IsNullOrEmpty(item.Tooltip)) label.Children.Add(LunetUi.Text(item.Tooltip, LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary));

        var line = new DockPanel { MinHeight = LunetTokens.MinTouch, Margin = indent, LastChildFill = true };
        var column = new StackPanel { Spacing = LunetTokens.Space.Xxs, Margin = new Thickness(0, 2) };
        column.Children.Add(line);

        Control editor;
        if (!item.IsEditable)
        {
            var value = LunetUi.Text("", LunetTokens.Type.Callout, LunetTokens.Color.TextSecondary);
            value.FontFamily = LunetTokens.MonoFont;
            value.TextAlignment = TextAlignment.Right;
            value.TextWrapping = TextWrapping.NoWrap;
            value.TextTrimming = TextTrimming.CharacterEllipsis;
            value.MaxWidth = 220;
            value.VerticalAlignment = VerticalAlignment.Center;
            row.Update = it => value.Text = GameInspection.Text(it);
            editor = value;
        }
        else switch (item.FieldKind)
        {
            case InspectorFieldKind.Bool:
            {
                var toggle = new ToggleSwitch { OnContent = null, OffContent = null, MinHeight = LunetTokens.MinTouch };
                AutomationProperties.SetName(toggle, item.Label);
                var updating = false;
                toggle.IsCheckedChanged += (_, _) => { if (!updating) Inspection.Apply(row.Item, toggle.IsChecked == true); };
                row.Update = it => { updating = true; toggle.IsChecked = it.Getter?.Invoke() is true; updating = false; };
                editor = toggle;
                break;
            }
            case InspectorFieldKind.Enum:
            {
                var combo = new ComboBox { ItemsSource = item.EnumNames, MinHeight = LunetTokens.MinTouch, MinWidth = 140 };
                AutomationProperties.SetName(combo, item.Label);
                var updating = false;
                combo.SelectionChanged += (_, _) =>
                {
                    if (updating || combo.SelectedItem is not string name || row.Item.ValueType is not { } type) return;
                    if (Enum.TryParse(type, name, out var value)) Inspection.Apply(row.Item, value);
                };
                row.Update = it =>
                {
                    if (combo.IsDropDownOpen) return;
                    updating = true; combo.SelectedItem = it.Getter?.Invoke()?.ToString(); updating = false;
                };
                editor = combo;
                break;
            }
            default:
                editor = TextEditor(row, column);
                break;
        }
        DockPanel.SetDock(editor, Dock.Right);
        line.Children.Add(editor);
        line.Children.Add(label);
        row.Update?.Invoke(item);
        return column;
    }

    private Control TextEditor(Row row, StackPanel column)
    {
        var item = row.Item;
        var numeric = item.FieldKind is InspectorFieldKind.Int or InspectorFieldKind.Float;
        var box = new TextBox
        {
            MinHeight = LunetTokens.MinTouch, MinWidth = numeric ? 96 : 160, MaxWidth = 240,
            FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Callout, VerticalContentAlignment = VerticalAlignment.Center,
            AcceptsReturn = item.MultilineLines > 1, TextWrapping = item.MultilineLines > 1 ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };
        if (item.MultilineLines > 1) box.MinHeight = LunetTokens.MinTouch * Math.Min(4, item.MultilineLines) * 0.6;
        AutomationProperties.SetName(box, item.Label);
        if (numeric) TextInputOptions.SetContentType(box, item.FieldKind == InspectorFieldKind.Int ? TextInputContentType.Digits : TextInputContentType.Number);
        var error = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.Danger);
        error.IsVisible = false;

        void Commit()
        {
            if (Inspection.TryApplyText(row.Item, box.Text ?? ""))
            {
                error.IsVisible = false;
                box.BorderBrush = null;
                return;
            }
            error.Text = $"\"{box.Text}\" não é um valor válido para {row.Item.Label}.";
            error.IsVisible = true;
            box.BorderBrush = LunetTokens.Brush(LunetTokens.Color.Danger);
        }
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && !box.AcceptsReturn) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { box.Text = GameInspection.Text(row.Item); error.IsVisible = false; e.Handled = true; }
        };
        box.LostFocus += (_, _) => Commit();

        Control result = box;
        if (item.FieldKind == InspectorFieldKind.Color)
        {
            var swatch = new Border
            {
                Width = 28, Height = 28, CornerRadius = new CornerRadius(LunetTokens.Radius.S), Margin = new Thickness(0, 0, LunetTokens.Space.Xs, 0),
                BorderBrush = LunetTokens.Brush(LunetTokens.Color.BorderStrong), BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center,
            };
            result = new StackPanel { Orientation = Orientation.Horizontal, Children = { swatch, box } };
            row.Update = it =>
            {
                var text = GameInspection.Text(it);
                if (!box.IsFocused && box.Text != text) box.Text = text;
                if (InspectorValue.TryParseColor(text, out var parsed) && parsed is Lunet.Graphics.Color c)
                    swatch.Background = new ImmutableSolidColorBrush(Avalonia.Media.Color.FromArgb(c.A, c.R, c.G, c.B));
            };
        }
        else row.Update = it =>
        {
            var text = GameInspection.Text(it);
            if (!box.IsFocused && box.Text != text) box.Text = text;
        };

        if (numeric && item.Min is { } min && item.Max is { } max && max > min)
        {
            var slider = new Slider { Minimum = min, Maximum = max, MinHeight = LunetTokens.MinTouch, Margin = new Thickness(0, -LunetTokens.Space.Xs, 0, 0) };
            AutomationProperties.SetName(slider, item.Label + " (deslizante)");
            if (item.FieldKind == InspectorFieldKind.Int) { slider.IsSnapToTickEnabled = true; slider.TickFrequency = 1; }
            var updating = false;
            slider.ValueChanged += (_, e) =>
            {
                if (updating) return;
                object value = row.Item.FieldKind == InspectorFieldKind.Int ? (int)Math.Round(e.NewValue) : (float)e.NewValue;
                Inspection.Apply(row.Item, value);
                if (row.Item.ValueType is { } type) box.Text = InspectorValue.Format(type, value);
            };
            var text = row.Update;
            row.Update = it =>
            {
                text(it);
                if (slider.IsPointerOver && slider.IsFocused) return;
                if (it.Getter?.Invoke() is not { } current) return;
                updating = true;
                try { slider.Value = Math.Clamp(Convert.ToDouble(current, System.Globalization.CultureInfo.InvariantCulture), min, max); }
                catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException) { }
                updating = false;
            };
            column.Children.Add(slider);
        }
        column.Children.Add(error);
        return result;
    }
}
