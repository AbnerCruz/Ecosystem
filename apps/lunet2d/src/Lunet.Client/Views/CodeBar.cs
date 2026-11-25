using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Editor;

namespace Lunet.Client.Views;

/// <summary>The three views of the code bar.</summary>
public enum CodeBarTab { Keys, Shortcuts, Methods }

/// <summary>
/// The code bar (owner, 25/11: typing is the system keyboard's; the tabs stay): one row docked right above the system
/// keyboard, in the thumb's reach, as Acode's QuickTools, Textastic's extra key row and Drafts' action bar do.
/// <list type="bullet">
/// <item><b>Teclas</b>: the symbols code needs ({ } ( ) ; = " &lt; &gt;), Tab and arrows; while a word is being typed,
/// the Roslyn and API suggestions take the row (Gboard's strip).</item>
/// <item><b>Atalhos</b>: undo, redo, line start/end, comment, duplicate, the command palette.</item>
/// <item><b>Métodos</b>: the API catalog (Recentes, Mais usados, namespaces and types) opens in the keyboard's place;
/// inserting a member returns to the code with the keyboard.</item>
/// </list>
/// Nothing runs on each key: suggestions are recomputed after a short pause, from the text near the caret.
/// </summary>
public sealed class CodeBar : UserControl
{
    /// <summary>Height of the bar's row, dp (a 48 dp touch target).</summary>
    public const double RowHeight = 48;

    /// <summary>Pause after typing before the suggestions are recomputed.</summary>
    public static readonly TimeSpan SuggestionDelay = TimeSpan.FromMilliseconds(120);

    private readonly ITextSurface _target;
    private readonly MethodCatalog _catalog;
    private readonly Func<IEnumerable<(string Path, string Text)>> _projectSources;
    private readonly Action? _openPalette;
    private readonly Action _hide;
    private readonly Action<bool> _systemKeyboard;
    private readonly StackPanel _tabs = new() { Orientation = Orientation.Horizontal, Spacing = 0, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
    private readonly ScrollViewer _rowScroll;
    private readonly Border _methods = new() { IsVisible = false };
    private readonly HashSet<string> _openNodes = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _suggest = new() { Interval = SuggestionDelay };
    private string? _analysisText;
    private int _analysisCaret = -1;
    private IReadOnlyList<Lunet.Editor.CompletionItem> _completions = [];
    private Lunet.Editor.CallSignatureHelp? _signature;

    /// <param name="systemKeyboard">Turns the system keyboard on (typing) or off (while Métodos is open).</param>
    public CodeBar(ITextSurface target, MethodCatalog catalog, Func<IEnumerable<(string Path, string Text)>> projectSources,
        Action? openPalette, Action hide, Action<bool> systemKeyboard)
    {
        _target = target;
        _catalog = catalog;
        _projectSources = projectSources;
        _openPalette = openPalette;
        _hide = hide;
        _systemKeyboard = systemKeyboard;
        Focusable = false;
        AutomationProperties.SetName(this, "Barra de código");
        _suggest.Tick += (_, _) => { _suggest.Stop(); RefreshRow(); };

        _rowScroll = new ScrollViewer
        {
            Content = _row, Focusable = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        var close = IconKey(LunetIcons.ChevronDown, "Fechar o teclado", () => _hide());
        close.Name = "HideKeyboard";
        var strip = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), Height = RowHeight };
        strip.Children.Add(_tabs);
        var divider = new Border { Width = 1, Margin = new Thickness(4, 12), Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong) };
        Grid.SetColumn(divider, 1);
        strip.Children.Add(divider);
        Grid.SetColumn(_rowScroll, 2);
        strip.Children.Add(_rowScroll);
        Grid.SetColumn(close, 3);
        strip.Children.Add(close);

        var root = new DockPanel();
        DockPanel.SetDock(strip, Dock.Top);
        root.Children.Add(strip);
        root.Children.Add(_methods);
        Content = new Border
        {
            Child = root, Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 1, 0, 0),
        };
        Show(CodeBarTab.Keys);
    }

    public CodeBarTab Tab { get; private set; }
    public MethodCatalog Catalog => _catalog;

    /// <summary>The API members suggested for the word being typed (when Roslyn has nothing fresher).</summary>
    public IReadOnlyList<CatalogMember> Suggestions { get; private set; } = [];
    /// <summary>The Roslyn completions shown now.</summary>
    public IReadOnlyList<Lunet.Editor.CompletionItem> Completions { get; private set; } = [];
    /// <summary>The call signature shown, when the caret is inside a call.</summary>
    public string? SignatureText { get; private set; }

    /// <summary>Height of the Métodos list (it takes the system keyboard's place), dp.</summary>
    public double MethodsHeight { get; set; } = 300;

    /// <summary>Switches view without moving the editor's caret or selection.</summary>
    public void Show(CodeBarTab tab)
    {
        Tab = tab;
        _tabs.Children.Clear();
        foreach (var (t, label, icon) in new[] { (CodeBarTab.Keys, "Teclas de código", LunetIcons.Braces), (CodeBarTab.Shortcuts, "Atalhos", LunetIcons.Keyboard), (CodeBarTab.Methods, "Métodos", LunetIcons.Function) })
        {
            var selected = t == tab;
            var chip = new Border
            {
                Width = 44, Height = 36, CornerRadius = new CornerRadius(LunetTokens.Radius.M),
                Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : "#00000000"),
                Child = LunetIcons.Create(icon, selected ? LunetTokens.Color.Primary : LunetTokens.Color.TextSecondary, 22),
            };
            AutomationProperties.SetName(chip, label);
            ToolTip.SetTip(chip, label);
            var target = t;
            OnTap(chip, () => Show(target == Tab && target == CodeBarTab.Methods ? CodeBarTab.Keys : target));
            _tabs.Children.Add(chip);
        }
        var methods = tab == CodeBarTab.Methods;
        if (methods)
        {
            _methods.Height = MethodsHeight;
            _methods.Child = MethodsPage();
        }
        else _methods.Child = null;
        _methods.IsVisible = methods;
        _systemKeyboard(!methods); // the catalog takes the keyboard's place; leaving it brings the keyboard back
        RefreshRow();
    }

    // ---------------- edits (public: tests use the same paths as touch) ----------------

    /// <summary>Applies one edit as one undo step and puts the caret where the edit says.</summary>
    public void Apply(CodeEdit edit)
    {
        _target.Replace(edit.Start, edit.End, edit.Insert, edit.Caret);
        TextChanged();
    }

    /// <summary>A code key: symbols with their pairs and blocks, Tab, and the arrows.</summary>
    public void Shortcut(string symbol)
    {
        var text = _target.Text;
        if (CodeKeys.Apply(text, _target.SelectionStart, _target.SelectionEnd, symbol) is { } edit) Apply(edit);
        else _target.Caret = CodeKeys.Move(text, _target.Caret, symbol);
    }

    /// <summary>The text changed (by the system keyboard or the bar): suggestions follow after a short pause.</summary>
    public void TextChanged()
    {
        _suggest.Stop();
        _suggest.Start();
    }

    /// <summary>The analyzer's answer for <paramref name="text"/> at <paramref name="caret"/>, used while it is current.</summary>
    public void SetAnalysis(string text, int caret, IReadOnlyList<Lunet.Editor.CompletionItem> completions, Lunet.Editor.CallSignatureHelp? signature)
    {
        _analysisText = text;
        _analysisCaret = caret;
        _completions = completions;
        _signature = signature;
        RefreshRow();
    }

    public void AcceptCompletion(Lunet.Editor.CompletionItem item)
    {
        var method = item.Kind == Lunet.Editor.CompletionKind.Method;
        var insert = item.InsertText + (method ? "()" : "");
        var start = item.ReplaceStart;
        Apply(new CodeEdit(start, start + item.ReplaceLength, insert, start + insert.Length - (method ? 1 : 0)));
        Haptics.Confirm?.Invoke();
    }

    public void AcceptSuggestion(CatalogMember member)
    {
        var text = _target.Text;
        var caret = _target.Caret;
        var prefix = MethodCatalog.WordBefore(text, caret);
        Apply(MethodCatalog.Insertion(member, text, caret - prefix.Length, caret));
        _catalog.RecordUse(member);
        Haptics.Confirm?.Invoke();
    }

    /// <summary>Inserts a catalog member at the caret, records it in Recentes, and goes back to typing.</summary>
    public void Insert(CatalogMember member)
    {
        Apply(MethodCatalog.Insertion(member, _target.Text, _target.SelectionStart, _target.SelectionEnd));
        _catalog.RecordUse(member);
        Haptics.Confirm?.Invoke();
        Show(CodeBarTab.Keys);
        _target.Focus();
    }

    /// <summary>"Draw(Texture2D texture, [Vector2 position], …)": the active parameter in brackets.</summary>
    public static string SignatureLabel(Lunet.Editor.CallSignatureHelp help)
    {
        var s = help.Signatures[Math.Clamp(help.Selected, 0, help.Signatures.Count - 1)];
        var name = s.Label.Contains('(') ? s.Label[..s.Label.IndexOf('(')] : s.Label;
        var parameters = s.Parameters.Select((p, i) => i == s.ActiveParameter ? "[" + p.Display + "]" : p.Display);
        var more = help.Signatures.Count > 1 ? $"  +{help.Signatures.Count - 1}" : "";
        return $"{name}({string.Join(", ", parameters)}){more}";
    }

    // ---------------- the row ----------------

    /// <summary>Recomputes the row now (the timer calls it after a pause; tests call it directly).</summary>
    public void RefreshRow()
    {
        _row.Children.Clear();
        Completions = []; Suggestions = []; SignatureText = null;
        if (Tab == CodeBarTab.Shortcuts) { ShortcutsRow(); return; }
        if (Tab == CodeBarTab.Methods)
        {
            _row.Children.Add(LunetUi.Text("Toque para inserir no cursor", LunetTokens.Type.Label, LunetTokens.Color.TextSecondary));
            return;
        }
        var text = _target.Text;
        var caret = Math.Clamp(_target.Caret, 0, text.Length);
        var prefix = MethodCatalog.WordBefore(text, caret);
        var fresh = _analysisText == text && _analysisCaret == caret;
        Completions = fresh && prefix.Length > 0 ? _completions.Take(12).ToArray() : [];
        Suggestions = Completions.Count > 0 || prefix.Length == 0 ? [] : _catalog.Suggest(prefix, 8);
        SignatureText = fresh && prefix.Length == 0 && _signature is { Signatures.Count: > 0 } help ? SignatureLabel(help) : null;
        foreach (var item in Completions)
        {
            var completion = item;
            _row.Children.Add(Chip(item.Label + (item.Kind == Lunet.Editor.CompletionKind.Method ? "()" : ""), item.Detail, () => AcceptCompletion(completion)));
        }
        foreach (var m in Suggestions)
        {
            var member = m;
            _row.Children.Add(Chip(m.Name + (m.IsCallable ? "()" : ""), m.Summary, () => AcceptSuggestion(member)));
        }
        if (SignatureText is { } signature)
            _row.Children.Add(new TextBlock
            {
                Text = signature, FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Label, VerticalAlignment = VerticalAlignment.Center,
                Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary), Margin = new Thickness(LunetTokens.Space.Xs, 0),
            });
        if (_row.Children.Count > 0) return;
        // No word being typed: the symbols code needs, then Tab and the arrows.
        foreach (var key in CodeKeys.Bar)
        {
            var symbol = key.Tap;
            _row.Children.Add(TextKey(symbol, symbol, () => Shortcut(symbol)));
        }
    }

    private void ShortcutsRow()
    {
        void Add(string icon, string label, Action act) => _row.Children.Add(IconKey(icon, label, act));
        void AddText(string face, string label, Action act) => _row.Children.Add(TextKey(face, label, act));
        AddText("↶", "Desfazer", () => { _target.Undo(); TextChanged(); });
        AddText("↷", "Refazer", () => { _target.Redo(); TextChanged(); });
        AddText("⇤", "Início da linha", () => MoveLineEdge(start: true));
        AddText("⇥", "Fim da linha", () => MoveLineEdge(start: false));
        AddText("//", "Comentar linhas", () => Line(LineOperations.ToggleComment));
        AddText("⧉", "Duplicar linha", () => Line(LineOperations.DuplicateLines));
        AddText("⇞", "Mover linha para cima", () => Line((t, a, b) => LineOperations.MoveLines(t, a, b, up: true)));
        AddText("⇟", "Mover linha para baixo", () => Line((t, a, b) => LineOperations.MoveLines(t, a, b, up: false)));
        AddText("⌫L", "Apagar linha", () => Line(LineOperations.DeleteLines));
        if (_openPalette is { } palette) Add(LunetIcons.Search, "Comandos", palette);
    }

    private void Line(Func<string, int, int, EditResult> op)
    {
        var r = op(_target.Text, _target.SelectionStart, _target.SelectionEnd);
        _target.Replace(r.Start, r.Start + r.DeleteLength, r.Insert, r.SelectionEnd);
        _target.Select(r.SelectionStart, r.SelectionEnd);
        TextChanged();
    }

    private void Line(Func<string, int, int, EditResult?> op)
    {
        if (op(_target.Text, _target.SelectionStart, _target.SelectionEnd) is { } r) Line((_, _, _) => r);
    }

    private void MoveLineEdge(bool start)
    {
        var text = _target.Text;
        var caret = Math.Clamp(_target.Caret, 0, text.Length);
        _target.Caret = start ? (caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1) : (text.IndexOf('\n', caret) is var n && n >= 0 ? n : text.Length);
    }

    // ---------------- Métodos ----------------

    private Control MethodsPage()
    {
        _catalog.SetProjectSources(_projectSources());
        var list = new StackPanel { Spacing = 1, Margin = new Thickness(LunetTokens.Space.Xxs, 0, LunetTokens.Space.Xxs, LunetTokens.Space.S) };
        void Section(string title, IReadOnlyList<CatalogMember> members)
        {
            if (members.Count == 0) return;
            list.Children.Add(SectionTitle(title));
            foreach (var m in members) list.Children.Add(MemberRow(m, qualified: true));
        }
        var recent = _catalog.Recent();
        Section("Recentes", recent);
        Section("Mais usados", _catalog.MostUsed().Where(m => !recent.Contains(m)).ToArray());
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
        return new ScrollViewer { Content = list, Focusable = false, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private void Toggle(string key)
    {
        if (!_openNodes.Remove(key)) _openNodes.Add(key);
        var offset = (_methods.Child as ScrollViewer)?.Offset ?? default;
        _methods.Child = MethodsPage();
        if (_methods.Child is ScrollViewer sv) Avalonia.Threading.Dispatcher.UIThread.Post(() => sv.Offset = offset, DispatcherPriority.Loaded);
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
            MinHeight = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.S + depth * 16, 0, LunetTokens.Space.S, 0),
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
        var name = qualified ? $"{m.Type}.{m.Name}" : m.Name;
        var stack = new StackPanel { Spacing = 1 };
        stack.Children.Add(new TextBlock
        {
            Text = name + (m.IsCallable ? "()" : ""), FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Callout,
            Foreground = LunetTokens.Brush(LunetTokens.Color.Code), TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (m.Signatures.Count > 0)
            stack.Children.Add(new TextBlock
            {
                Text = Short(m.Signatures[0]) + (m.Signatures.Count > 1 ? $"  +{m.Signatures.Count - 1}" : ""),
                FontFamily = LunetTokens.MonoFont, FontSize = 11, Foreground = LunetTokens.Brush(LunetTokens.Color.TextTertiary), TextTrimming = TextTrimming.CharacterEllipsis,
            });
        if (!string.IsNullOrWhiteSpace(m.Summary))
            stack.Children.Add(new TextBlock { Text = m.Summary, FontSize = 12, Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary), TextTrimming = TextTrimming.CharacterEllipsis });
        var row = new Border
        {
            MinHeight = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.S + (qualified ? 0 : 32), 4, LunetTokens.Space.Xs, 4),
            Background = LunetTokens.Brush("#00000000"), Child = stack,
        };
        AutomationProperties.SetName(row, "Inserir " + name);
        AutomationProperties.SetHelpText(row, m.Summary);
        OnTap(row, () => Insert(m));
        return row;
    }

    private static string Short(string signature)
    {
        foreach (var w in new[] { "public ", "static ", "virtual ", "abstract ", "sealed ", "override ", "readonly " })
            signature = signature.Replace(w, "", StringComparison.Ordinal);
        return signature;
    }

    // ---------------- components ----------------

    private static Border TextKey(string face, string label, Action act)
    {
        var key = new Border
        {
            MinWidth = 44, Height = 38, Padding = new Thickness(LunetTokens.Space.S, 0), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
            Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            Child = new TextBlock
            {
                Text = face, FontFamily = LunetTokens.MonoFont, FontSize = 18, FontWeight = FontWeight.SemiBold,
                Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        AutomationProperties.SetName(key, label);
        OnTap(key, () => { Haptics.KeyTap?.Invoke(); act(); });
        return key;
    }

    private static Border IconKey(string icon, string label, Action act)
    {
        var key = new Border
        {
            Width = LunetTokens.MinTouch, Height = RowHeight, Background = LunetTokens.Brush("#00000000"),
            Child = LunetIcons.Create(icon, LunetTokens.Color.TextSecondary, 22),
        };
        AutomationProperties.SetName(key, label);
        ToolTip.SetTip(key, label);
        OnTap(key, act);
        return key;
    }

    private static Border Chip(string text, string help, Action act)
    {
        var chip = new Border
        {
            Height = 38, Padding = new Thickness(LunetTokens.Space.S, 0), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
            Background = LunetTokens.Brush(LunetTokens.Color.PrimarySoft),
            Child = new TextBlock
            {
                Text = text, FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Callout,
                Foreground = LunetTokens.Brush(LunetTokens.Color.Code), VerticalAlignment = VerticalAlignment.Center,
            },
        };
        AutomationProperties.SetName(chip, "Sugestão " + text);
        AutomationProperties.SetHelpText(chip, help);
        OnTap(chip, act);
        return chip;
    }

    /// <summary>A tap that never takes focus from the editor (a drag scrolls and does not tap).</summary>
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
