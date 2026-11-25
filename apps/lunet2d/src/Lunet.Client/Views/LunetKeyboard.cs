using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lunet.Client.Shell;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>The three tabs of the Lunet keyboard (one surface).</summary>
public enum KeyboardTab { Keys, Shortcuts, Methods }

/// <summary>
/// The IDE's own keyboard (owner's order on PR #1040; study <c>docs/research/teclado-nativo-ide-2026-11-25.md</c>):
/// it replaces the system keyboard while editing code. Tabs: Teclas (Gboard-like), Atalhos (the code keys), Métodos
/// (the API and the game's members). Nothing in it can take focus, so the editor keeps caret, selection and focus
/// when you switch tabs; edits go straight to the editor, one undo step each.
/// </summary>
public sealed class LunetKeyboard : UserControl
{
    private readonly ITextSurface _target;
    private readonly MethodCatalog _catalog;
    private readonly Func<IEnumerable<(string Path, string Text)>> _projectSources;
    private readonly Action _useSystemKeyboard;
    private readonly Border _body = new();
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    private readonly StackPanel _variants = new() { Orientation = Orientation.Horizontal, Spacing = 2, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly HashSet<string> _openNodes = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _hold = new();
    private ShiftState _shift = ShiftState.Off;
    private DateTime _lastShiftTap = DateTime.MinValue;
    private bool _symbols;

    public LunetKeyboard(ITextSurface target, MethodCatalog catalog, Func<IEnumerable<(string Path, string Text)>> projectSources, Action useSystemKeyboard)
    {
        _target = target;
        _catalog = catalog;
        _projectSources = projectSources;
        _useSystemKeyboard = useSystemKeyboard;
        Focusable = false;
        AutomationProperties.SetName(this, "Teclado Lunet");

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Height = 40 };
        header.Children.Add(_tabs);
        var top = new Panel { Children = { header, _variants } };
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(_body);
        Content = new Border
        {
            Child = root, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(3, 0, 3, 3),
        };
        Show(KeyboardTab.Keys);
    }

    public KeyboardTab Tab { get; private set; }
    public ShiftState Shift => _shift;
    public bool SymbolsPage => _symbols;
    public MethodCatalog Catalog => _catalog;

    /// <summary>Switches tab without touching the editor's focus, caret or selection.</summary>
    public void Show(KeyboardTab tab)
    {
        Tab = tab;
        _tabs.Children.Clear();
        foreach (var (t, label) in new[] { (KeyboardTab.Keys, "Teclas"), (KeyboardTab.Shortcuts, "Atalhos"), (KeyboardTab.Methods, "Métodos") })
        {
            var selected = t == tab;
            var chip = new Border
            {
                Padding = new Thickness(LunetTokens.Space.M, 0), MinWidth = 72, Margin = new Thickness(0, 4),
                CornerRadius = new CornerRadius(LunetTokens.Radius.Pill),
                Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : "#00000000"),
                Child = new TextBlock
                {
                    Text = label, FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Label,
                    FontWeight = selected ? FontWeight.SemiBold : FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = LunetTokens.Brush(selected ? LunetTokens.Color.Primary : LunetTokens.Color.TextSecondary),
                },
            };
            AutomationProperties.SetName(chip, label);
            OnTap(chip, () => Show(t));
            _tabs.Children.Add(chip);
        }
        _body.Child = tab switch
        {
            KeyboardTab.Keys => KeysPage(),
            KeyboardTab.Shortcuts => ShortcutsPage(),
            _ => MethodsPage(),
        };
    }

    // ---------------- editing (public: tests and hardware shortcuts use the same paths as touch) ----------------

    /// <summary>Applies one edit to the editor as one undo step and puts the caret where the edit says.</summary>
    public void Apply(CodeEdit edit)
    {
        _target.Replace(edit.Start, edit.End, edit.Insert, edit.Caret);
    }

    /// <summary>Types text at the caret (replacing the selection).</summary>
    public void Type(string text)
    {
        var start = _target.SelectionStart;
        Apply(new CodeEdit(start, _target.SelectionEnd, text, start + text.Length));
    }

    /// <summary>A key of the Teclas tab, as a tap (variants/gestures are separate paths).</summary>
    public void Press(KeyDef key)
    {
        var text = _target.Text;
        switch (key.Action)
        {
            case KeyAction.Text:
                Type(KeyboardLayout.Typed(key, _shift));
                SetShift(KeyboardLayout.AfterTyping(_shift));
                break;
            case KeyAction.Space: Type(" "); break;
            case KeyAction.Enter: Apply(KeyboardLayout.Enter(text, _target.SelectionStart, _target.SelectionEnd)); break;
            case KeyAction.Backspace:
                if (KeyboardLayout.Backspace(text, _target.SelectionStart, _target.SelectionEnd) is { } edit) Apply(edit);
                break;
            case KeyAction.Shift:
                var now = DateTime.UtcNow;
                SetShift(KeyboardLayout.Toggle(_shift, now - _lastShiftTap));
                _lastShiftTap = now;
                break;
            case KeyAction.Symbols: _symbols = true; Show(KeyboardTab.Keys); break;
            case KeyAction.Letters: _symbols = false; Show(KeyboardTab.Keys); break;
            case KeyAction.System: _useSystemKeyboard(); break;
        }
    }

    /// <summary>A code key of the Atalhos tab (symbols, pairs, blocks, Tab, arrows).</summary>
    public void Shortcut(string symbol)
    {
        var text = _target.Text;
        if (CodeKeys.Apply(text, _target.SelectionStart, _target.SelectionEnd, symbol) is { } edit) Apply(edit);
        else
        {
            var caret = CodeKeys.Move(text, _target.Caret, symbol);
            _target.Caret = caret;
        }
    }

    /// <summary>Inserts a catalog member at the caret and records it in Recentes/Mais usados.</summary>
    public void Insert(CatalogMember member)
    {
        Apply(MethodCatalog.Insertion(member, _target.Text, _target.SelectionStart, _target.SelectionEnd));
        _catalog.RecordUse(member);
    }

    private void SetShift(ShiftState shift)
    {
        if (_shift == shift) return;
        _shift = shift;
        if (Tab == KeyboardTab.Keys && !_symbols) _body.Child = KeysPage();
    }

    // ---------------- Teclas ----------------

    private Control KeysPage()
    {
        var rows = new Grid();
        var layout = _symbols ? KeyboardLayout.Symbols : KeyboardLayout.Letters;
        foreach (var _ in layout) rows.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        for (int r = 0; r < layout.Count; r++)
        {
            var row = new Grid();
            foreach (var key in layout[r]) row.ColumnDefinitions.Add(new ColumnDefinition(key.Width, GridUnitType.Star));
            for (int c = 0; c < layout[r].Count; c++)
            {
                var k = KeyFace(layout[r][c]);
                Grid.SetColumn(k, c);
                row.Children.Add(k);
            }
            Grid.SetRow(row, r);
            rows.Children.Add(row);
        }
        return rows;
    }

    private Control KeyFace(KeyDef key)
    {
        var special = key.Action != KeyAction.Text;
        var label = key.Action == KeyAction.Shift
            ? _shift == ShiftState.Locked ? "⇪" : "⇧"
            : KeyboardLayout.Typed(key, _shift);
        var face = new Grid();
        face.Children.Add(new TextBlock
        {
            Text = label, FontFamily = key.Action == KeyAction.Space ? LunetTokens.UiFont : LunetTokens.MonoFont,
            FontSize = key.Action == KeyAction.Space ? LunetTokens.Type.Caption : 18,
            Foreground = LunetTokens.Brush(key.Action == KeyAction.Shift && _shift != ShiftState.Off ? LunetTokens.Color.Primary
                : key.Action == KeyAction.Space ? LunetTokens.Color.TextSecondary : LunetTokens.Color.TextPrimary),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        if (key.Variants is { Count: > 0 } v)
            face.Children.Add(new TextBlock
            {
                Text = v[0], FontSize = 9, Margin = new Thickness(0, 1, 4, 0), Foreground = LunetTokens.Brush(LunetTokens.Color.TextTertiary),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            });
        var restColor = special ? LunetTokens.Color.SurfaceOverlay : LunetTokens.Color.SurfaceRaised;
        var border = new Border
        {
            Child = face, Margin = new Thickness(2), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
            Background = LunetTokens.Brush(key.Action == KeyAction.Enter ? LunetTokens.Color.PrimarySoft : restColor),
        };
        AutomationProperties.SetName(border, key.Action switch
        {
            KeyAction.Backspace => "Apagar", KeyAction.Enter => "Nova linha", KeyAction.Space => "Espaço",
            KeyAction.Shift => "Maiúsculas", KeyAction.Symbols => "Números e símbolos", KeyAction.Letters => "Letras",
            KeyAction.System => "Usar o teclado do sistema", _ => label,
        });
        WireKey(border, key, restColor);
        return border;
    }

    /// <summary>Press: highlight now (latency); release: commit. Hold: repeat or variants; drag: glide cursor/delete.</summary>
    private void WireKey(Border border, KeyDef key, string restColor)
    {
        Point origin = default;
        int caretAtStart = 0;
        bool consumed = false, pressed = false;
        int repeats = 0;
        border.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            e.Pointer.Capture(border);
            pressed = true; consumed = false; repeats = 0;
            origin = e.GetPosition(this);
            caretAtStart = _target.Caret;
            border.Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong);
            _hold.Stop();
            _hold.Interval = key.Repeats ? KeyboardLayout.RepeatStart : KeyboardLayout.LongPress;
            _hold.Tick -= OnHold;
            _hold.Tick += OnHold;
            if (key.Repeats || key.Variants is { Count: > 0 }) _hold.Start();
            void OnHold(object? s, EventArgs a)
            {
                if (!pressed) { _hold.Stop(); return; }
                if (key.Repeats)
                {
                    consumed = true; repeats++;
                    _hold.Interval = KeyboardLayout.RepeatInterval;
                    Press(key);
                }
                else { _hold.Stop(); consumed = true; ShowVariants(key); }
            }
        };
        border.PointerMoved += (_, e) =>
        {
            if (!pressed) return;
            var dx = e.GetPosition(this).X - origin.X;
            if (_variants.IsVisible) { HighlightVariant(e.GetPosition(_variants).X); return; }
            if (key.Action == KeyAction.Space && Math.Abs(dx) >= KeyboardLayout.CursorStep)
            {
                _hold.Stop(); consumed = true;
                var caret = KeyboardLayout.GlideCursor(_target.Text, caretAtStart, dx);
                _target.Caret = caret;
            }
            else if (key.Action == KeyAction.Backspace && KeyboardLayout.GlideWords(dx) > 0) { _hold.Stop(); consumed = true; }
        };
        border.PointerReleased += (_, e) =>
        {
            e.Handled = true;
            if (!pressed) return;
            pressed = false;
            _hold.Stop();
            e.Pointer.Capture(null);
            border.Background = LunetTokens.Brush(key.Action == KeyAction.Enter ? LunetTokens.Color.PrimarySoft : restColor);
            var dx = e.GetPosition(this).X - origin.X;
            if (_variants.IsVisible) { CommitVariant(); return; }
            if (key.Action == KeyAction.Backspace && KeyboardLayout.GlideWords(dx) is > 0 and var words)
            {
                if (KeyboardLayout.Backspace(_target.Text, _target.SelectionStart, _target.SelectionEnd, words) is { } edit) Apply(edit);
                return;
            }
            if (!consumed) Press(key);
        };
        border.PointerCaptureLost += (_, _) => { pressed = false; _hold.Stop(); border.Background = LunetTokens.Brush(restColor); };
    }

    private string? _variantChoice;

    /// <summary>The long-press popup: the key's variants above the keys; slide to choose, lift to type.</summary>
    public void ShowVariants(KeyDef key)
    {
        _variants.Children.Clear();
        foreach (var v in key.Variants ?? [])
        {
            var text = _shift == ShiftState.Off ? v : v.ToUpperInvariant();
            _variants.Children.Add(new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(LunetTokens.Radius.S), Tag = text,
                Background = LunetTokens.Brush(LunetTokens.Color.SurfaceOverlay),
                Child = new TextBlock { Text = text, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary) },
            });
        }
        _variants.IsVisible = _variants.Children.Count > 0;
        HighlightVariant(0);
    }

    private void HighlightVariant(double x)
    {
        _variantChoice = null;
        for (int i = 0; i < _variants.Children.Count; i++)
        {
            var b = (Border)_variants.Children[i];
            bool on = x >= i * 42 && x < (i + 1) * 42 || (x < 0 && i == 0) || (x >= _variants.Children.Count * 42 && i == _variants.Children.Count - 1);
            b.Background = LunetTokens.Brush(on ? LunetTokens.Color.Primary : LunetTokens.Color.SurfaceOverlay);
            ((TextBlock)b.Child!).Foreground = LunetTokens.Brush(on ? LunetTokens.Color.OnPrimary : LunetTokens.Color.TextPrimary);
            if (on) _variantChoice = (string)b.Tag!;
        }
    }

    /// <summary>Types the highlighted variant and closes the popup.</summary>
    public void CommitVariant()
    {
        if (_variantChoice is { } v) { Type(v); SetShift(KeyboardLayout.AfterTyping(_shift)); }
        _variants.IsVisible = false;
        _variants.Children.Clear();
        _variantChoice = null;
    }

    // ---------------- Atalhos ----------------

    private Control ShortcutsPage()
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,*,*"), ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*") };
        for (int i = 0; i < CodeKeys.Bar.Count; i++)
        {
            var key = CodeKeys.Bar[i];
            var cell = ShortcutKey(key);
            Grid.SetRow(cell, i / 5); Grid.SetColumn(cell, i % 5);
            grid.Children.Add(cell);
        }
        var actions = new (string Label, string Name, Action Do)[]
        {
            ("↶", "Desfazer", () => _target.Undo()),
            ("↷", "Refazer", () => _target.Redo()),
            ("⇤", "Início da linha", () => MoveLineEdge(start: true)),
            ("⇥|", "Fim da linha", () => MoveLineEdge(start: false)),
            ("⌫", "Apagar", () => Press(new KeyDef("⌫", KeyAction.Backspace))),
        };
        for (int i = 0; i < actions.Length; i++)
        {
            var (label, name, act) = actions[i];
            var b = SimpleKey(label, name, act);
            Grid.SetRow(b, 2); Grid.SetColumn(b, i);
            grid.Children.Add(b);
        }
        return grid;
    }

    private void MoveLineEdge(bool start)
    {
        var text = _target.Text;
        var caret = Math.Clamp(_target.Caret, 0, text.Length);
        int pos = start ? (caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1) : (text.IndexOf('\n', caret) is var n && n >= 0 ? n : text.Length);
        _target.Caret = pos;
    }

    private Control ShortcutKey(CodeKey key)
    {
        var face = new Grid();
        face.Children.Add(new TextBlock { Text = key.Tap, FontFamily = LunetTokens.MonoFont, FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        void Hint(string? s, HorizontalAlignment h, VerticalAlignment v)
        {
            if (s is null) return;
            face.Children.Add(new TextBlock { Text = s, FontFamily = LunetTokens.MonoFont, FontSize = 11, Foreground = LunetTokens.Brush(LunetTokens.Color.TextTertiary), HorizontalAlignment = h, VerticalAlignment = v, Margin = new Thickness(5, 2) });
        }
        Hint(key.Up, HorizontalAlignment.Center, VerticalAlignment.Top);
        Hint(key.Down, HorizontalAlignment.Center, VerticalAlignment.Bottom);
        Hint(key.Left, HorizontalAlignment.Left, VerticalAlignment.Center);
        Hint(key.Right, HorizontalAlignment.Right, VerticalAlignment.Center);
        var border = new Border { Child = face, Margin = new Thickness(2), CornerRadius = new CornerRadius(LunetTokens.Radius.S), Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised) };
        AutomationProperties.SetName(border, key.Tap);
        Point? origin = null;
        border.PointerPressed += (_, e) => { e.Handled = true; origin = e.GetPosition(border); e.Pointer.Capture(border); border.Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong); };
        border.PointerReleased += (_, e) =>
        {
            e.Handled = true;
            border.Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised);
            if (origin is not { } o) return;
            var p = e.GetPosition(border);
            origin = null;
            e.Pointer.Capture(null);
            Shortcut(key.For(CodeKeys.Resolve(p.X - o.X, p.Y - o.Y)));
        };
        border.PointerCaptureLost += (_, _) => { origin = null; border.Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised); };
        return border;
    }

    private Border SimpleKey(string label, string name, Action act)
    {
        var b = new Border
        {
            Margin = new Thickness(2), CornerRadius = new CornerRadius(LunetTokens.Radius.S), Background = LunetTokens.Brush(LunetTokens.Color.SurfaceOverlay),
            Child = new TextBlock { Text = label, FontSize = 17, Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };
        AutomationProperties.SetName(b, name);
        OnTap(b, act);
        return b;
    }

    // ---------------- Métodos ----------------

    private Control MethodsPage()
    {
        _catalog.SetProjectSources(_projectSources());
        var list = new StackPanel { Spacing = 1, Margin = new Thickness(LunetTokens.Space.Xxs, 0) };
        void Section(string title, IReadOnlyList<CatalogMember> members)
        {
            if (members.Count == 0) return;
            list.Children.Add(SectionTitle(title));
            foreach (var m in members) list.Children.Add(MemberRow(m, qualified: true));
        }
        Section("Recentes", _catalog.Recent());
        Section("Mais usados", _catalog.MostUsed());
        list.Children.Add(SectionTitle("Catálogo"));
        foreach (var ns in _catalog.Tree())
        {
            var nsKey = "ns:" + ns.Name;
            list.Children.Add(Node(ns.Name, depth: 0, open: _openNodes.Contains(nsKey), () => Toggle(nsKey)));
            if (!_openNodes.Contains(nsKey)) continue;
            foreach (var type in ns.Types)
            {
                var typeKey = "t:" + ns.Name + "." + type.Name;
                list.Children.Add(Node(type.Name, depth: 1, open: _openNodes.Contains(typeKey), () => Toggle(typeKey)));
                if (!_openNodes.Contains(typeKey)) continue;
                foreach (var m in type.Members) list.Children.Add(MemberRow(m, qualified: false));
            }
        }
        var scroll = new ScrollViewer { Content = list, Focusable = false, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        return scroll;
    }

    private void Toggle(string key)
    {
        if (!_openNodes.Remove(key)) _openNodes.Add(key);
        var offset = (_body.Child as ScrollViewer)?.Offset ?? default;
        _body.Child = MethodsPage();
        if (_body.Child is ScrollViewer sv) Avalonia.Threading.Dispatcher.UIThread.Post(() => sv.Offset = offset, DispatcherPriority.Loaded);
    }

    /// <summary>Opens or closes a namespace/type node of the catalog (tests use the same path as touch).</summary>
    public void ToggleNode(string ns, string? type = null) => Toggle(type is null ? "ns:" + ns : "t:" + ns + "." + type);

    private static Control SectionTitle(string text) => new TextBlock
    {
        Text = text.ToUpperInvariant(), FontSize = LunetTokens.Type.Caption, FontWeight = FontWeight.SemiBold,
        Foreground = LunetTokens.Brush(LunetTokens.Color.Primary), Margin = new Thickness(LunetTokens.Space.S, LunetTokens.Space.S, 0, 2),
    };

    private Control Node(string label, int depth, bool open, Action toggle)
    {
        var row = new Border
        {
            MinHeight = 40, Padding = new Thickness(LunetTokens.Space.S + depth * 16, 0, LunetTokens.Space.S, 0),
            Background = LunetTokens.Brush("#00000000"),
            Child = new TextBlock
            {
                Text = (open ? "▾ " : "▸ ") + label, FontFamily = depth == 0 ? LunetTokens.UiFont : LunetTokens.MonoFont,
                FontSize = LunetTokens.Type.Callout, FontWeight = depth == 0 ? FontWeight.SemiBold : FontWeight.Normal,
                Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary), VerticalAlignment = VerticalAlignment.Center,
            },
        };
        AutomationProperties.SetName(row, (open ? "Fechar " : "Abrir ") + label);
        OnTap(row, toggle);
        return row;
    }

    private Control MemberRow(CatalogMember m, bool qualified)
    {
        var key = "m:" + m.Key;
        var expanded = _openNodes.Contains(key);
        var name = qualified ? $"{m.Type}.{m.Name}" : m.Name;
        var stack = new StackPanel { Spacing = 1 };
        var titleRow = new DockPanel();
        if (m.Signatures.Count > 1)
        {
            var more = new Border
            {
                Width = 48, Background = LunetTokens.Brush("#00000000"),
                Child = new TextBlock { Text = (expanded ? "▴ " : "▾ ") + "+" + (m.Signatures.Count - 1), FontSize = LunetTokens.Type.Caption, Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            AutomationProperties.SetName(more, $"{m.Signatures.Count} sobrecargas");
            OnTap(more, () => Toggle(key));
            DockPanel.SetDock(more, Dock.Right);
            titleRow.Children.Add(more);
        }
        titleRow.Children.Add(new TextBlock
        {
            Text = name + (m.IsCallable ? "()" : ""), FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Callout,
            Foreground = LunetTokens.Brush(LunetTokens.Color.Code), TextTrimming = TextTrimming.CharacterEllipsis,
        });
        stack.Children.Add(titleRow);
        foreach (var sig in expanded ? m.Signatures : m.Signatures.Take(1))
            stack.Children.Add(new TextBlock { Text = Short(sig), FontFamily = LunetTokens.MonoFont, FontSize = 11, Foreground = LunetTokens.Brush(LunetTokens.Color.TextTertiary), TextTrimming = TextTrimming.CharacterEllipsis });
        var row = new Border
        {
            MinHeight = 44, Padding = new Thickness(LunetTokens.Space.S + (qualified ? 0 : 32), 4, LunetTokens.Space.Xs, 4),
            Background = LunetTokens.Brush("#00000000"), Child = stack,
        };
        AutomationProperties.SetName(row, "Inserir " + name);
        AutomationProperties.SetHelpText(row, m.Summary);
        OnTap(row, () => Insert(m));
        return row;
    }

    /// <summary>A signature without the access modifiers, to fit a phone line.</summary>
    private static string Short(string signature)
    {
        foreach (var w in new[] { "public ", "static ", "virtual ", "abstract ", "sealed ", "override ", "readonly " })
            signature = signature.Replace(w, "", StringComparison.Ordinal);
        return signature;
    }

    /// <summary>A tap that never moves focus away from the editor (a drag in a list scrolls and does not tap).</summary>
    private static void OnTap(Control c, Action act)
    {
        Point? down = null;
        c.PointerPressed += (_, e) => { down = e.GetPosition(c); };
        c.PointerReleased += (_, e) =>
        {
            if (down is not { } d) return;
            down = null;
            var p = e.GetPosition(c);
            if (Math.Abs(p.X - d.X) > 10 || Math.Abs(p.Y - d.Y) > 10) return;
            e.Handled = true;
            act();
        };
    }
}
