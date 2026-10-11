using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The Visual mode of the 1.8.4 editor (#renderedPreview) natively: the note drawn as the rendered Markdown
/// (MarkdownReader) and edited one block at a time. Tapping a block puts its Markdown in a text box (so the soft keyboard
/// and IME are the platform's own); leaving it draws it again. The selection bubble, the "/" block menu and the
/// [[ suggestions are the ones of visual-tools.js / app.js, acting on the block (LegacyVisualEdit).
/// The browser original edits the formatted text in place (contenteditable): that caret-in-rendered-text editing is the
/// part not ported (PARITY: UC-18 inline WYSIWYG).
/// </summary>
public sealed class VisualSurface : StackPanel
{
    private string _text = "";
    private IReadOnlyList<MarkdownBlock> _blocks = [];
    private int _editStart = -1, _editEnd = -1;      // range of the block being edited, in _text
    private TextBox? _box;
    private bool _prompting;
    private readonly WikiSuggestList _wiki = new();
    private readonly SlashMenu _slash = new();
    private readonly Border _bubble;

    public VisualSurface()
    {
        Spacing = 0;
        _wiki.Chosen += CommitWiki;
        _slash.Chosen += ChooseSlash;
        _bubble = BuildBubble();
        Rebuild();
    }

    /// <summary>The whole note, Markdown (LF), after every edit.</summary>
    public string Text => _text;
    public bool IsEditing => _box is not null;
    public event Action<string>? TextEdited;
    public event Action<string>? WikiLinkTapped;
    /// <summary>The note names matching what follows "[[" (knowledge.search, current note excluded).</summary>
    public Func<string, IReadOnlyList<string>>? WikiCandidates { get; set; }
    /// <summary>The "Link externo" URL prompt (UD.prompt).</summary>
    public Func<Task<string?>>? AskUrl { get; set; }

    public void SetText(string markdown)
    {
        _text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        _box = null;
        _editStart = _editEnd = -1;
        Rebuild();
    }

    /// <summary>Leaves the block being edited (its text is already in <see cref="Text"/>) and draws it again.</summary>
    public void FinishEdit()
    {
        if (_box is null) return;
        _box = null;
        _wiki.Hide();
        _slash.Hide();
        if (_editStart >= 0)
        {
            // an emptied block leaves no trace (the original drops empty paragraphs when serialising)
            var hadText = _editEnd > _editStart;
            if (!hadText) Collapse(_editStart);
        }
        _editStart = _editEnd = -1;
        Rebuild();
        TextEdited?.Invoke(_text);
    }

    private void Collapse(int at)
    {
        var start = at;
        while (start > 0 && _text[start - 1] == '\n') start--;
        var end = at;
        while (end < _text.Length && _text[end] == '\n') end++;
        // keep one blank line between the neighbours
        var keep = start > 0 && end < _text.Length ? "\n\n" : "";
        _text = _text[..start] + keep + _text[end..];
    }

    // ------------------------------------------------------------------ drawing

    private void Rebuild()
    {
        Children.Clear();
        _blocks = MarkdownEngine.SplitBlocks(_text);
        if (_blocks.Count == 0 && _box is null)
        {
            var empty = new Border
            {
                Background = Brushes.Transparent, MinHeight = 44,
                Child = new TextBlock { Text = "Comece a escrever…", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), FontSize = UrbeTheme.EditorSize }
            };
            empty.PointerPressed += (_, e) => { e.Handled = true; AppendBlock(); };
            Children.Add(empty);
        }
        var editorAdded = false;
        foreach (var block in _blocks)
        {
            if (_box is not null && !editorAdded && block.Start >= _editStart)
            {
                editorAdded = true;
                Children.Add(EditorFor());
                if (block.Start == _editStart) continue;   // the block being edited
            }
            Children.Add(Rendered(block));
        }
        if (_box is not null && !editorAdded) Children.Add(EditorFor());
        var tail = new Border { Background = Brushes.Transparent, MinHeight = 160 };
        tail.PointerPressed += (_, e) => { e.Handled = true; AppendBlock(); };
        Children.Add(tail);
    }

    private Control Rendered(MarkdownBlock block)
    {
        var source = _text[block.Start..block.End];
        var reader = new MarkdownReader
        {
            WikiLinkTapped = name => WikiLinkTapped?.Invoke(name),
            TaskToggled = (line, on) => ToggleTask(block, line, on)
        };
        var holder = new Border { Background = Brushes.Transparent, Child = reader.Build(MarkdownTree.Parse(source)) };
        if (block.Kind != "frontmatter")
            holder.PointerPressed += (_, e) =>
            {
                if (e.Handled) return;
                e.Handled = true;
                Edit(block.Start);
            };
        return holder;
    }

    private void ToggleTask(MarkdownBlock block, int line, bool on)
    {
        var lines = _text[block.Start..block.End].Split('\n');
        if (line < 0 || line >= lines.Length) return;
        var l = lines[line];
        var open = l.IndexOf('[');
        if (open < 0 || open + 2 >= l.Length) return;
        lines[line] = l[..(open + 1)] + (on ? "x" : " ") + l[(open + 2)..];
        _text = _text[..block.Start] + string.Join('\n', lines) + _text[block.End..];
        TextEdited?.Invoke(_text);
        Rebuild();
    }

    private void AppendBlock()
    {
        FinishEdit();
        var sep = _text.Length == 0 || _text.EndsWith("\n\n", StringComparison.Ordinal) ? "" : _text.EndsWith('\n') ? "\n" : "\n\n";
        _text += sep;
        var at = _text.Length;
        _text += "\n"; // a pending empty block; removed again if nothing is typed
        _editStart = _editEnd = at;
        StartBox("", at, at);
    }

    private void Edit(int blockStart)
    {
        var before = _text.Length;
        var startedAt = _editStart;
        FinishEdit();
        // the previous block may have changed length: the tapped block moved with it
        var shift = _text.Length - before;
        var target = blockStart > startedAt && startedAt >= 0 ? blockStart + shift : blockStart;
        var block = _blocks.FirstOrDefault(b => b.Start == target) ?? _blocks.FirstOrDefault(b => b.Start <= target && target <= b.End);
        if (block is null) return;
        StartBox(_text[block.Start..block.End], block.Start, block.End, block.Kind);
    }

    private void StartBox(string source, int start, int end, string kind = "paragraph")
    {
        _editStart = start;
        _editEnd = end;
        _box = new TextBox
        {
            Text = source, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6), CornerRadius = new CornerRadius(8),
            FontFamily = kind == "code" ? UrbeTheme.MonoFont : UrbeTheme.UiFont, FontSize = UrbeTheme.EditorSize,
            Foreground = UrbeTheme.Brush(UrbeTheme.Text), Background = UrbeTheme.Brush(UrbeTheme.Accent, .07),
            CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent), SelectionBrush = UrbeTheme.Brush(UrbeTheme.Accent, .35)
        };
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused" })
            _box.Resources[key] = _box.Background;
        foreach (var key in new[] { "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
            _box.Resources[key] = Brushes.Transparent;
        _box.TextChanged += OnBoxChanged;
        _box.KeyDown += OnBoxKey;
        _box.AddHandler(KeyDownEvent, OnBoxKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty || e.Property == TextBox.CaretIndexProperty) UpdateTools();
        };
        _box.LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            // focus moved to something else (not one of our tools, which never take it)
            if (!_prompting && _box is not null && !_box.IsFocused && !IsKeyboardFocusWithin) FinishEdit();
        }, DispatcherPriority.Background);
        Rebuild();
        Dispatcher.UIThread.Post(() =>
        {
            _box?.Focus();
            if (_box is not null) _box.CaretIndex = _box.Text?.Length ?? 0;
        }, DispatcherPriority.Loaded);
    }

    private Control EditorFor() => new StackPanel { Children = { _bubble, _box!, _slash, _wiki } };

    // ------------------------------------------------------------------ editing

    private void OnBoxChanged(object? sender, TextChangedEventArgs e)
    {
        if (_box is null) return;
        var block = _box.Text ?? "";
        _text = _text[.._editStart] + block + _text[_editEnd..];
        _editEnd = _editStart + block.Length;
        TextEdited?.Invoke(_text);
        UpdateTools();
    }

    private void UpdateTools()
    {
        if (_box is null) return;
        var text = _box.Text ?? "";
        var a = Math.Min(_box.SelectionStart, _box.SelectionEnd);
        var b = Math.Max(_box.SelectionStart, _box.SelectionEnd);
        var bubbleOn = b > a && text[a..b].Trim().Length > 0;
        _bubble.IsVisible = bubbleOn;

        var slash = LegacyEditorTools.SlashQuery(text);
        if (slash is not null && _box.CaretIndex == text.Length) _slash.Show(slash); else _slash.Hide();

        var ctx = LegacyEditorTools.FindWikiContext(text, _box.CaretIndex);
        if (ctx is not null && WikiCandidates is { } find) _wiki.Show(find(ctx.Query)); else _wiki.Hide();
    }

    private void OnBoxKey(object? sender, KeyEventArgs e)
    {
        if (_box is null) return;
        if (_slash.IsOpen && _slash.HandleKey(e.Key)) { e.Handled = true; return; }
        if (_wiki.IsOpen && _wiki.HandleKey(e.Key)) { e.Handled = true; return; }
        if (e.Key == Key.Escape) { e.Handled = true; FinishEdit(); return; }
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None && ContinueList()) e.Handled = true;
    }

    // Enter in a list/quote keeps the marker; Enter on an empty item leaves the list (the browser's behaviour in a contenteditable)
    private bool ContinueList()
    {
        var box = _box!;
        var text = box.Text ?? "";
        var caret = Math.Clamp(box.CaretIndex, 0, text.Length);
        var s = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
        var e = text.IndexOf('\n', caret);
        if (e < 0) e = text.Length;
        var line = text[s..e];
        var m = System.Text.RegularExpressions.Regex.Match(line, @"^([ \t]*)(?:(\d+)([.)])|([-*+])|(>))\s?(\[[ xX]\]\s)?(.*)$");
        if (!m.Success || caret != e) return false;
        string marker;
        if (m.Groups[2].Success) marker = m.Groups[1].Value + (int.Parse(m.Groups[2].Value) + 1) + m.Groups[3].Value + " ";
        else if (m.Groups[4].Success) marker = m.Groups[1].Value + m.Groups[4].Value + " " + (m.Groups[6].Success ? "[ ] " : "");
        else marker = "> ";
        if (m.Groups[7].Value.Length == 0)
        {
            // empty item: remove the marker (leave the list)
            box.Text = text[..s] + text[e..];
            box.CaretIndex = s;
            return true;
        }
        box.Text = text[..e] + "\n" + marker + text[e..];
        box.CaretIndex = e + 1 + marker.Length;
        return true;
    }

    private void Apply(LegacyEditorTools.TextEdit? edit)
    {
        if (edit is null || _box is null) return;
        _box.Text = edit.Text;
        _box.SelectionStart = edit.SelectionStart;
        _box.SelectionEnd = edit.SelectionEnd;
        _box.CaretIndex = edit.SelectionEnd;
        UpdateTools();
    }

    private (string Text, int A, int B) Selection()
    {
        var text = _box?.Text ?? "";
        return (text, Math.Min(_box?.SelectionStart ?? 0, _box?.SelectionEnd ?? 0), Math.Max(_box?.SelectionStart ?? 0, _box?.SelectionEnd ?? 0));
    }

    private void CommitWiki(string name)
    {
        if (_box is null) return;
        Apply(LegacyEditorTools.CommitWiki(_box.Text ?? "", _box.CaretIndex, name));
        _wiki.Hide();
    }

    /// <summary>A "/" menu choice, or a bubble/toolbar action by id (h1, h2, h3, list, task, quote, rule, noteLink, link, bold, italic, code, wiki).</summary>
    public void Run(string act)
    {
        if (_box is null) return;
        var (text, a, b) = Selection();
        switch (act)
        {
            case "bold": Apply(LegacyVisualEdit.ToggleWrap(text, a, b, "**")); break;
            case "italic": Apply(LegacyVisualEdit.ToggleWrap(text, a, b, "_")); break;
            case "code": Apply(LegacyVisualEdit.InlineCode(text, a, b)); break;
            case "wiki": Apply(LegacyVisualEdit.WikiOfSelection(text, a, b)); break;
            case "link": _ = LinkAsync(text, a, b); break;
            case "h1": Apply(LegacyVisualEdit.ToggleBlock(text, _box.CaretIndex, "# ")); break;
            case "h2": Apply(LegacyVisualEdit.ToggleBlock(text, _box.CaretIndex, "## ")); break;
            case "h3": Apply(LegacyVisualEdit.ToggleBlock(text, _box.CaretIndex, "### ")); break;
            case "list": Apply(LegacyVisualEdit.ToggleBlock(text, _box.CaretIndex, "- ")); break;
            case "task": Apply(LegacyVisualEdit.ToggleBlock(text, _box.CaretIndex, "- [ ] ")); break;
            case "quote": Apply(LegacyVisualEdit.ToggleBlock(text, _box.CaretIndex, "> ")); break;
            case "noteLink": Apply(LegacyEditorTools.InsertAtCursor(text, a, b, "[[")); break;
            case "rule": InsertRule(); break;
        }
        if (act is "h2" or "quote" or "link" or "wiki") _bubble.IsVisible = false;
    }

    private async Task LinkAsync(string text, int a, int b)
    {
        if (AskUrl is null) return;
        _prompting = true;
        string? url;
        try { url = await AskUrl(); }
        finally { _prompting = false; }
        if (url is null || _box is null) return;
        Apply(LegacyVisualEdit.Link(text, a, b, url));
        _box.Focus();
    }

    private void ChooseSlash(string act)
    {
        if (_box is null) return;
        _slash.Hide();
        if (act == "rule") { InsertRule(); return; }
        // the block is cleared, then the tool runs on the empty block
        _box.Text = "";
        Apply(LegacyVisualEdit.Slash(act));
        if (act == "link") { _box.Text = ""; Run("link"); }
    }

    private void InsertRule()
    {
        if (_box is null) return;
        // hr + a new empty paragraph after it: the block becomes "---" and a fresh block is opened below
        _box.Text = "---";
        var at = _editStart;
        FinishEdit();
        var block = _blocks.FirstOrDefault(x => x.Start == at);
        var end = block?.End ?? _text.Length;
        _text = _text[..end] + "\n\n" + _text[end..];
        Rebuild();
        TextEdited?.Invoke(_text);
        _editStart = _editEnd = end + 2;
        StartBox("", end + 2, end + 2);
    }

    // ------------------------------------------------------------------ the selection bubble (#urbeFormatBubble)

    private Border BuildBubble()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        void Add(string act, Control content, string tip)
        {
            var b = new Button
            {
                Content = content, Width = 36, Height = 36, Padding = new Thickness(0), CornerRadius = new CornerRadius(8), Focusable = false,
                Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Text),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(b, tip);
            b.Click += (_, _) => Run(act);
            row.Children.Add(b);
        }
        void Sep() => row.Children.Add(new Border { Width = 1, Height = 20, Margin = new Thickness(3, 0), Background = UrbeTheme.Brush(UrbeTheme.Line2) });
        Control Ic(string name) => UrbeTheme.Icon(SlashMenu.Icons[name], 18);
        Add("bold", new TextBlock { Text = "B", FontWeight = FontWeight.Bold, FontSize = 15 }, "Negrito");
        Add("italic", new TextBlock { Text = "I", FontStyle = FontStyle.Italic, FontFamily = new FontFamily("Georgia, serif"), FontWeight = FontWeight.SemiBold, FontSize = 15 }, "Itálico");
        Add("code", Ic("code"), "Código");
        Sep();
        Add("wiki", Ic("wikilink"), "Transformar em link de nota");
        Add("link", Ic("link"), "Link externo");
        Sep();
        Add("h2", new TextBlock { Text = "H", FontWeight = FontWeight.Bold, FontSize = 15 }, "Título");
        Add("quote", Ic("quote"), "Citação");
        var card = EditorChrome.Card(row);
        card.IsVisible = true;
        card.Padding = new Thickness(4);
        var holder = new Border { Child = card, IsVisible = false, Margin = new Thickness(0, 0, 0, 4), HorizontalAlignment = HorizontalAlignment.Left };
        return holder;
    }
}
