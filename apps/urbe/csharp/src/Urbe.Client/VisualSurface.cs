using Avalonia.VisualTree;
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

    /// <summary>The bubble's "Fixar em painel": the selected text of the block (DEC-0045, UC-39).</summary>
    public event Action<string>? PinRequested;

    /// <summary>The text selected in the block being edited, or null.</summary>
    public string? SelectedText
    {
        get
        {
            if (_box is null) return null;
            var (text, a, b) = Selection();
            return b > a ? text[a..b] : null;
        }
    }

    /// <summary>The toolbar buttons that are on for the block being edited (empty when none is), read from its text (DEC-0045).</summary>
    public event Action<IReadOnlySet<string>>? ActiveToolsChanged;
    /// <summary>The note names matching what follows "[[" (knowledge.search, current note excluded).</summary>
    public Func<string, IReadOnlyList<string>>? WikiCandidates { get; set; }
    /// <summary>The "Link externo" URL prompt (UD.prompt).</summary>
    public Func<Task<string?>>? AskUrl { get; set; }

    public void SetText(string markdown)
    {
        _text = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        // the block being edited goes: its buttons must not stay lit (UC-55)
        if (_box is not null) ActiveToolsChanged?.Invoke(new HashSet<string>());
        _box = null;
        _editStart = _editEnd = -1;
        Rebuild();
    }

    /// <summary>Leaves the block being edited (its text is already in <see cref="Text"/>) and draws it again.</summary>
    public void Undo() => _box?.Undo();
    public void Redo() => _box?.Redo();

    public void FinishEdit()
    {
        if (_box is null) return;
        _box = null;
        ActiveToolsChanged?.Invoke(new HashSet<string>());
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
        _blocks = WithContainers(MarkdownEngine.SplitBlocks(_text), NoteContainers.Parse(_text));
        if (_blocks.Count == 0 && _box is null)
        {
            var empty = new Border
            {
                Background = Brushes.Transparent, MinHeight = 44,
                Child = new TextBlock { Text = "Comece a escrever…", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), FontSize = UrbeTheme.EditorSize }
            };
            empty.Tapped += (_, e) => { e.Handled = true; AppendBlock(); };
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
        tail.Tapped += (_, e) => { e.Handled = true; AppendBlock(); };
        Children.Add(tail);
    }

    /// <summary>A `:::tipo` … `:::` container (UC-44) is one block: the blocks it covers become a single "container"
    /// block, edited as its whole source.</summary>
    internal static IReadOnlyList<MarkdownBlock> WithContainers(IReadOnlyList<MarkdownBlock> blocks, IReadOnlyList<NoteContainer> boxes)
    {
        if (boxes.Count == 0) return blocks;
        var result = new List<MarkdownBlock>();
        int b = 0;
        foreach (var box in boxes)
        {
            while (b < blocks.Count && blocks[b].End <= box.Start) result.Add(blocks[b++]);
            int start = box.Start, end = box.End;
            // a block that reaches into the container goes with it (and so does the previous merged one it touches)
            while (b < blocks.Count && blocks[b].Start < end) { start = Math.Min(start, blocks[b].Start); end = Math.Max(end, blocks[b].End); b++; }
            while (result.Count > 0 && result[^1].End > start) { start = Math.Min(start, result[^1].Start); end = Math.Max(end, result[^1].End); result.RemoveAt(result.Count - 1); }
            result.Add(new MarkdownBlock(start, end, "container"));
        }
        while (b < blocks.Count) result.Add(blocks[b++]);
        return result;
    }

    private Control Rendered(MarkdownBlock block)
    {
        var source = _text[block.Start..block.End];
        var container = block.Kind == "container";
        var reader = new MarkdownReader
        {
            WikiLinkTapped = name => WikiLinkTapped?.Invoke(name),
            // a task inside a container: the tap edits the block (its list lines are not the block's lines)
            TaskToggled = container ? null : (line, on) => ToggleTask(block, line, on),
            MathTapped = index => _ = EditFormulaAsync(block.Start, index),
            CommentsTapped = index => _ = CommentsAsync(block.Start, index)
        };
        var holder = new Border { Background = Brushes.Transparent, Child = container ? reader.BuildMarkdown(source) : reader.Build(NoteMarks.Parse(source)) };
        // a tap (press and release without moving), never the press: a finger that starts a scroll on a block
        // must scroll, not turn the block into an edit box under it (the 1.8.4's preview scrolls the same way)
        if (block.Kind != "frontmatter")
            holder.Tapped += (_, e) =>
            {
                if (e.Handled) return;
                e.Handled = true;
                // the caret goes where the finger touched (before: at the end, and a second tap was needed — UC-48)
                _tapAt = e.GetPosition(holder);
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
        // a double tap selects the word under the finger, to copy, cut or delete (UC-48; the mouse already does it)
        _box.DoubleTapped += (_, e) =>
        {
            if (_box is null || e.Pointer.Type == Avalonia.Input.PointerType.Mouse) return;
            var (from, to) = WordAt(_box.Text ?? "", _box.CaretIndex);
            if (to <= from) return;
            _box.SelectionStart = from;
            _box.SelectionEnd = to;
            e.Handled = true;
        };
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
        var tap = _tapAt;
        _tapAt = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (_box is null) return;
            _box.Focus();
            _box.CaretIndex = CaretFromTap(_box, tap) ?? _box.Text?.Length ?? 0;
            // a box focused by the app does not always bring up the phone's keyboard: ask for it (UC-48)
            PlatformInput.ShowKeyboard?.Invoke();
        }, DispatcherPriority.Loaded);
    }

    private Point? _tapAt;

    /// <summary>The letters and digits around a position (the word a double tap selects).</summary>
    internal static (int From, int To) WordAt(string text, int at)
    {
        at = Math.Clamp(at, 0, text.Length);
        static bool Word(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '\'';
        if (at == text.Length || !Word(text[at])) { if (at > 0 && Word(text[at - 1])) at--; else return (at, at); }
        int from = at, to = at;
        while (from > 0 && Word(text[from - 1])) from--;
        while (to < text.Length && Word(text[to])) to++;
        return (from, to);
    }

    /// <summary>The character under the point tapped on the rendered block, in the edit box laid out at the same place.</summary>
    private static int? CaretFromTap(TextBox box, Point? tap)
    {
        if (tap is not { } p) return null;
        var presenter = box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().FirstOrDefault();
        if (presenter?.TextLayout is not { } layout) return null;
        var hit = layout.HitTestPoint(new Point(Math.Max(0, p.X - box.Padding.Left), Math.Max(0, p.Y - box.Padding.Top)));
        var at = hit.TextPosition + (hit.IsTrailing ? 1 : 0);
        return Math.Clamp(at, 0, box.Text?.Length ?? 0);
    }

    private Control EditorFor()
    {
        // the bubble, the box and the menus are reused: take them out of the panel of the previous build first
        Control[] parts = [_bubble, _box!, _slash, _wiki];
        foreach (var c in parts) if (c.Parent is Panel old) old.Children.Remove(c);
        var panel = new StackPanel();
        panel.Children.AddRange(parts);
        return panel;
    }

    // ------------------------------------------------------------------ editing

    private void OnBoxChanged(object? sender, TextChangedEventArgs e)
    {
        if (_box is null) return;
        var block = _box.Text ?? "";
        _editStart = Math.Clamp(_editStart, 0, _text.Length);
        _editEnd = Math.Clamp(_editEnd, _editStart, _text.Length);
        _text = _text[.._editStart] + block + _text[_editEnd..];
        _editEnd = _editStart + block.Length;
        TextEdited?.Invoke(_text);
        UpdateTools();
    }

    private void UpdateTools()
    {
        if (_box is null) return;
        var text = _box.Text ?? "";
        // while a key or the keyboard (IME) changes the text, the selection can still point past its end
        var (a, b) = Range(_box);
        var bubbleOn = b > a && text[a..b].Trim().Length > 0;
        _bubble.IsVisible = bubbleOn;
        ActiveToolsChanged?.Invoke(LegacyEditorTools.ActiveTools(text, a, b));

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
        if (m.Groups[2].Success) marker = m.Groups[1].Value + (System.Numerics.BigInteger.Parse(m.Groups[2].Value) + 1) + m.Groups[3].Value + " ";
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

    /// <summary>The box's selection as an ordered range inside its text.</summary>
    internal static (int A, int B) Range(TextBox box)
    {
        var n = (box.Text ?? "").Length;
        int a = Math.Clamp(Math.Min(box.SelectionStart, box.SelectionEnd), 0, n), b = Math.Clamp(Math.Max(box.SelectionStart, box.SelectionEnd), 0, n);
        return (a, b);
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
        var (a, b) = _box is null ? (0, 0) : Range(_box);
        return (text, a, b);
    }

    private void CommitWiki(string name)
    {
        if (_box is null) return;
        Apply(LegacyEditorTools.CommitWiki(_box.Text ?? "", _box.CaretIndex, name));
        _wiki.Hide();
    }

    /// <summary>
    /// A button of the formatting bar in the Visual mode (app.js .mdBtn handler when editorViewMode is 'preview'): it acts
    /// on the block being edited; with none, v23GarantirBloco focuses the text, which puts the caret at the start of its
    /// first block, or adds an empty paragraph to an empty note.
    /// </summary>
    public void Toolbar(string id)
    {
        EnsureBox();
        if (_box is null) return;
        Run(id);
    }

    /// <summary>v23GarantirBloco: the block being edited, else the first one (caret at its start), else a new paragraph.</summary>
    private void EnsureBox()
    {
        if (_box is null)
        {
            if (_blocks.Count > 0)
            {
                Edit(_blocks[0].Start);
                // the caret at the start of the block's text: after its Markdown mark (# , > , - , - [ ] , 1. )
                if (_box is not null)
                {
                    var mark = System.Text.RegularExpressions.Regex.Match(_box.Text ?? "", @"^(#{1,6}\s+|>\s?|[-*+]\s+(\[[ xX]\]\s+)?|\d+[.)]\s+)");
                    _box.SelectionStart = _box.SelectionEnd = _box.CaretIndex = mark.Success ? mark.Length : 0;
                }
            }
            else AppendBlock();
        }
    }

    /// <summary>
    /// math/editor.js startFormula in the Visual mode, then insertAtom: the pane opens with the selected text; a formula in
    /// the text goes in place of the selection as $tex$ (with a space before it when it follows a letter, as the original
    /// does after a text node); a display formula ($$…$$) takes the block when it is empty, else goes after it, and the
    /// caret moves to a new paragraph below.
    /// </summary>
    /// <summary>The host's formula pane: the LaTeX, whether it is a display formula, and whether it edits one already there.</summary>
    public Func<string, bool, bool, Task<FormulaPane.Result?>>? AskFormula { get; set; }

    /// <summary>
    /// A tap on a formula of a drawn block (math/editor.js: click on .umath → openPane "Editar fórmula"): Salvar keeps its
    /// delimiters with the new LaTeX (updateAtom); switching No texto/Destaque turns a display formula into a paragraph with
    /// $tex$, or takes an inline one out of its paragraph and puts $$ … $$ after it; Remover (or an emptied formula) takes it
    /// out (removeAtom: a display formula leaves its block empty, which then goes).
    /// </summary>
    /// <summary>The comments on a word (UC-49): shown, and the one written added at the end of the run, in the .md.</summary>
    public Func<IReadOnlyList<string>, Task<string?>>? AskComment { get; set; }

    private async Task CommentsAsync(int blockStart, int index)
    {
        if (AskComment is not { } ask) return;
        var before = _text.Length;
        var editingStart = _editStart;
        FinishEdit();
        var shift = _text.Length - before;
        var start = blockStart > editingStart && editingStart >= 0 ? blockStart + shift : blockStart;
        var block = _blocks.FirstOrDefault(b => b.Start == start);
        if (block is null) return;
        var groups = NoteMarks.CommentGroups(_text[block.Start..block.End]);
        if (index < 0 || index >= groups.Count) return;
        string? added;
        _prompting = true;
        try { added = await ask(groups[index].Comments); }
        finally { _prompting = false; }
        if (string.IsNullOrWhiteSpace(added)) return;
        var g = groups[index];
        var source = NoteMarks.AddComment(_text[block.Start..block.End], g, added);
        _text = _text[..block.Start] + source + _text[block.End..];
        Rebuild();
        TextEdited?.Invoke(_text);
    }

    private async Task EditFormulaAsync(int blockStart, int index)
    {
        if (AskFormula is not { } ask) return;
        var before = _text.Length;
        var editingStart = _editStart;
        FinishEdit();
        var shift = _text.Length - before;
        var start = blockStart > editingStart && editingStart >= 0 ? blockStart + shift : blockStart;
        var block = _blocks.FirstOrDefault(b => b.Start == start);
        if (block is null) return;
        var source = _text[block.Start..block.End];
        var formulas = MathEditing.Scan(source);
        if (index < 0 || index >= formulas.Count) return;
        var f = formulas[index];
        FormulaPane.Result? r;
        _prompting = true;
        try { r = await ask(f.Tex, f.Block, true); }
        finally { _prompting = false; }
        if (r is null) return;
        // the block's new text, and a display formula that goes after it (an inline one switched to Destaque)
        string text, after = "";
        if (r.Remove || r.Tex.Length == 0) text = source[..f.Start] + source[f.End..];
        else if (r.Block == f.Block) text = source[..f.Start] + f.Open + r.Tex + f.Close + source[f.End..];
        else if (!r.Block) text = source[..f.Start] + "$" + r.Tex + "$" + source[f.End..];
        else { text = source[..f.Start] + source[f.End..]; after = "$$\n" + r.Tex + "\n$$"; }
        if (text.Trim().Length == 0) { text = after; after = ""; }
        var insert = text + (after.Length > 0 ? "\n\n" + after : "");
        _text = _text[..block.Start] + insert + _text[block.End..];
        // an emptied block leaves no trace: only the blank lines at that spot are joined (never elsewhere in the note)
        if (insert.Length == 0) Collapse(block.Start);
        Rebuild();
        TextEdited?.Invoke(_text);
    }

    public async Task FormulaAsync(Func<string, bool, Task<FormulaPane.Result?>> ask, bool block)
    {
        EnsureBox();
        if (_box is null) return;
        var (text, a, b) = Selection();
        FormulaPane.Result? r;
        _prompting = true;
        try { r = await ask(text[a..b].Trim(), block); }
        finally { _prompting = false; }
        if (r is null || r.Remove || r.Tex.Length == 0 || _box is null) { _box?.Focus(); return; }
        if (!r.Block)
        {
            var raw = "$" + r.Tex + "$";
            var insert = a > 0 && !System.Text.RegularExpressions.Regex.IsMatch(text[a - 1].ToString(), @"[\s\u200b(\[{]") ? " " + raw : raw;
            Apply(new LegacyEditorTools.TextEdit(text[..a] + insert + text[b..], a + insert.Length, a + insert.Length));
            _box?.Focus();
            return;
        }
        var display = "$$\n" + r.Tex + "\n$$";
        var at = _editStart;
        if (text.Trim().Length == 0)
        {
            _box.Text = display;
            FinishEdit();
        }
        else
        {
            FinishEdit();
            var current = _blocks.FirstOrDefault(x => x.Start == at);
            var after = current?.End ?? _text.Length;
            _text = _text[..after] + "\n\n" + display + _text[after..];
            Rebuild();
            at = after + 2;
        }
        var mathBlock = _blocks.FirstOrDefault(x => x.Start >= at);
        var end = mathBlock?.End ?? _text.Length;
        _text = _text[..end] + "\n\n" + _text[end..];
        Rebuild();
        TextEdited?.Invoke(_text);
        _editStart = _editEnd = end + 2;
        StartBox("", end + 2, end + 2);
    }

    /// <summary>A "/" menu choice, or a bubble/toolbar action by id (h1, h2, h3, list, task, quote, rule, noteLink, link, bold, italic, code, wiki).</summary>
    public void Run(string act)
    {
        if (_box is null) return;
        var (text, a, b) = Selection();
        switch (act)
        {
            // a lit button takes its marks off, even with only the caret inside them (UC-55)
            case "bold" or "italic" or "code": Apply(LegacyVisualEdit.ToggleMark(text, a, b, act)); break;
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
            case "pin": if (b > a) PinRequested?.Invoke(text[a..b]); break;
            case "codeblock": Apply(LegacyVisualEdit.CodeBlock(text, _box.CaretIndex)); break;
            case "box": _ = BoxAsync(text, a, b); break;
            case "comment":
                // from the "/" menu the typed "/query" goes first
                if (LegacyEditorTools.SlashQuery(text) is not null) { text = ""; a = b = 0; }
                Apply(LegacyVisualEdit.Comment(text, a, b));
                break;
        }
        if (act is "h2" or "quote" or "link" or "wiki" or "pin") _bubble.IsVisible = false;
    }

    /// <summary>The host's question for a new box (UC-44): its type and optional title, or null to cancel.</summary>
    public Func<Task<(string Type, string Title)?>>? AskBox { get; set; }

    // Caixa: `:::tipo título` … `:::` around the selection of the block, or empty with the caret inside
    private async Task BoxAsync(string text, int a, int b)
    {
        if (AskBox is null) return;
        if (LegacyEditorTools.SlashQuery(text) is not null) { text = ""; a = b = 0; }
        (string Type, string Title)? r;
        _prompting = true;
        try { r = await AskBox(); }
        finally { _prompting = false; }
        if (_box is null) return;
        if (r is { } box) Apply(NoteContainers.Wrap(text, a, b, box.Type, box.Title));
        _box.Focus();
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
        // math/editor.js items: startFormula(false / true) on the emptied block
        if (act is "formula" or "formulaBlock") { if (AskFormula is { } ask) _ = FormulaAsync((t, b) => ask(t, b, false), act == "formulaBlock"); return; }
        Apply(LegacyVisualEdit.Slash(act));
        if (act == "link") { _box.Text = ""; Run("link"); }
        if (act == "box") Run("box");
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
        Sep();
        Add("comment", new TextBlock { Text = "%%", FontWeight = FontWeight.Bold, FontSize = 13 }, "Comentário");
        Add("pin", UrbeTheme.Icon(UrbeTheme.Icons.Pin, 18), "Fixar em painel");
        var card = EditorChrome.Card(row);
        card.IsVisible = true;
        card.Padding = new Thickness(4);
        var holder = new Border { Child = card, IsVisible = false, Margin = new Thickness(0, 0, 0, 4), HorizontalAlignment = HorizontalAlignment.Left };
        return holder;
    }
}
