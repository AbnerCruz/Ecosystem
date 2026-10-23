using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
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

    /// <summary>UC-61: the "/" menu's items in the person's order (null: all, as they are).</summary>
    public IReadOnlyList<string>? SlashOrder { get => _slash.Order; set => _slash.Order = value; }
    private readonly Border _bubble;
    private readonly Canvas _bubbleLayer = new() { Height = 0, VerticalAlignment = VerticalAlignment.Top };
    private readonly Border _backdrop = new() { IsHitTestVisible = false };
    private readonly Dictionary<int, Control> _holders = [];

    public VisualSurface()
    {
        Spacing = 0;
        _wiki.Chosen += CommitWiki;
        _slash.Chosen += ChooseSlash;
        _bubble = BuildBubble();
        _bubbleLayer.Children.Add(_bubble);
        Rebuild();
    }

    /// <summary>The body size of the notes (Personalização → Fonte das notas → Tamanho; --ui-editor-size).</summary>
    public double TextSize { get; private set; } = UrbeTheme.EditorSize;

    /// <summary>The line height factor (--ui-editor-line).</summary>
    public double LineFactor { get; private set; } = 1.7;

    /// <summary>Applies the text size and line height of Personalização (the note is drawn again when they change).</summary>
    public void ApplyText(double size, double lineFactor)
    {
        if (size == TextSize && lineFactor == LineFactor) return;
        TextSize = size;
        LineFactor = lineFactor;
        FinishEdit();
        Rebuild();
    }

    /// <summary>A block entered (true) or left (false) edit mode.</summary>
    public event Action<bool>? EditingChanged;

    /// <summary>The text box of the block being edited (tests, the host), or null.</summary>
    public TextBox? EditBox => _box;

    /// <summary>The drawn block that starts at <paramref name="start"/> in the text, or null (tests, the host).</summary>
    public Control? BlockView(int start) => _holders.GetValueOrDefault(start);

    /// <summary>The blocks of the note as drawn (their start in the text, in order).</summary>
    public IReadOnlyList<int> BlockStarts => _blocks.Select(b => b.Start).ToList();

    /// <summary>The selection bubble (tests, screenshots).</summary>
    public Control Bubble => _bubble;

    private ScrollViewer? Scroll => this.FindAncestorOfType<ScrollViewer>();

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
        var was = _box is not null;
        if (was) ActiveToolsChanged?.Invoke(new HashSet<string>());
        _box = null;
        _editStart = _editEnd = -1;
        Rebuild();
        if (was) EditingChanged?.Invoke(false);
    }

    /// <summary>Leaves the block being edited (its text is already in <see cref="Text"/>) and draws it again.</summary>
    public void Undo() => _box?.Undo();
    public void Redo() => _box?.Redo();

    public void FinishEdit()
    {
        if (_box is null) return;
        // the block keeps its place on the screen when it is drawn again (UC-57)
        var scroll = Scroll;
        double? before = scroll is null || _box.Parent is not Visual boxCell ? null : EditorScrolling.TopIn(boxCell, scroll);
        int at = _editStart;
        bool kept = _editEnd > _editStart;
        _box = null;
        ActiveToolsChanged?.Invoke(new HashSet<string>());
        _wiki.Hide();
        _slash.Hide();
        _bubble.IsVisible = false;
        if (_editStart >= 0)
        {
            // an emptied block leaves no trace (the original drops empty paragraphs when serialising)
            if (!kept) Collapse(_editStart);
        }
        _editStart = _editEnd = -1;
        Rebuild();
        if (kept && _holders.GetValueOrDefault(at) is { } drawn)
        {
            if (scroll is not null && before is { } y) KeepAt(drawn, y, scroll);
            Settle(drawn);
        }
        TextEdited?.Invoke(_text);
        EditingChanged?.Invoke(false);
    }

    /// <summary>Scrolls so that <paramref name="c"/>'s top is again at <paramref name="y"/> on the screen.</summary>
    private static void KeepAt(Control c, double y, ScrollViewer scroll)
    {
        scroll.UpdateLayout();
        if (EditorScrolling.TopIn(c, scroll) is not { } now) return;
        var delta = now - y;
        if (Math.Abs(delta) < .5) return;
        EditorScrolling.SetOffsetY(scroll, scroll.Offset.Y + delta);
        scroll.UpdateLayout();
    }

    /// <summary>A block drawn again after editing settles in with a short fade (≤ 150 ms, UX.md P6).</summary>
    private static void Settle(Control c)
    {
        if (UrbeTokens.Motion.Reduced) return;
        c.Transitions = null;
        c.Opacity = .55;
        c.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = UrbeTokens.Motion.Fast, Easing = UrbeTokens.Motion.EaseOut }];
        c.Opacity = 1;
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
        _holders.Clear();
        _blocks = WithContainers(MarkdownEngine.SplitBlocks(_text), NoteContainers.Parse(_text));
        if (_blocks.Count == 0 && _box is null)
        {
            var empty = new Border
            {
                Background = Brushes.Transparent, MinHeight = 44,
                Child = new TextBlock { Text = "Comece a escrever…", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), FontSize = TextSize, FontFamily = UrbeTheme.EditorFont }
            };
            empty.Tapped += (_, e) => { e.Handled = true; if (TouchIntent.IsIntended(empty)) AppendBlock(); };
            Children.Add(empty);
        }
        var editorAdded = false;
        var suggested = _suggestion is null;
        foreach (var block in _blocks)
        {
            if (_box is not null && !editorAdded && block.Start >= _editStart)
            {
                editorAdded = true;
                Children.Add(EditorFor());
                if (block.Start == _editStart) continue;   // the block being edited
            }
            // UC-80: the agent's suggestion takes the place of the lines it changes (or comes before the next block)
            if (_suggestion is { } sg && (block.End > sg.Change.Start && block.Start < sg.Change.End || block.Start >= sg.Change.End))
            {
                if (!suggested) { suggested = true; Children.Add(SuggestionCard(sg)); }
                if (block.Start < sg.Change.End) continue;
            }
            var drawn = Rendered(block);
            _holders[block.Start] = drawn;
            Children.Add(drawn);
        }
        if (!suggested) Children.Add(SuggestionCard(_suggestion!));
        if (_box is not null && !editorAdded) Children.Add(EditorFor());
        var tail = new Border { Background = Brushes.Transparent, MinHeight = 160 };
        tail.Tapped += (_, e) => { e.Handled = true; if (TouchIntent.IsIntended(tail)) AppendBlock(); };
        Children.Add(tail);
    }

    /// <summary>UC-80: an agent's suggestion in the note — the lines it changes, what takes their place, and the answers.</summary>
    public sealed record Suggestion(LineChange Change, string Who, Action Accept, Action Refuse);

    private Suggestion? _suggestion;

    /// <summary>The suggestion shown now, or null.</summary>
    public Suggestion? ShownSuggestion => _suggestion;

    /// <summary>Shows an agent's suggestion in place of the lines it changes (null takes it away).</summary>
    public void ShowSuggestion(Suggestion? suggestion)
    {
        if (_box is not null) FinishEdit();
        _suggestion = suggestion;
        Rebuild();
    }

    private Control SuggestionCard(Suggestion sg)
    {
        var reader = new MarkdownReader { Size = TextSize, LineFactor = LineFactor };
        var replacement = sg.Change.Replacement.TrimEnd('\n');
        var body = replacement.Length > 0 ? reader.Build(NoteMarks.Parse(replacement))
            : new TextBlock { Text = "Apagar estas linhas.", FontSize = TextSize * .9, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) };
        Button Answer(string text, bool primary, Action run)
        {
            var b = new Button
            {
                Content = text, Padding = new Thickness(14, 6), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(0), FontSize = TextSize * .85,
                Background = primary ? UrbeTheme.Brush(UrbeTheme.Accent) : UrbeTheme.Brush(UrbeTheme.Text, .08),
                Foreground = primary ? UrbeTheme.Brush(UrbeTheme.Bg) : UrbeTheme.Brush(UrbeTheme.Text)
            };
            Avalonia.Automation.AutomationProperties.SetName(b, text);
            b.Click += (_, e) => { e.Handled = true; run(); };
            return b;
        }
        return new Border
        {
            Margin = new Thickness(0, .5 * TextSize), Padding = new Thickness(.75 * TextSize, .5 * TextSize), CornerRadius = new CornerRadius(10),
            Background = UrbeTheme.Brush(UrbeTheme.Accent, .1), BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent, .6), BorderThickness = new Thickness(1),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "Sugestão de " + sg.Who, FontSize = TextSize * .75, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) },
                    body,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Answer("Aceitar", true, sg.Accept), Answer("Recusar", false, sg.Refuse) } }
                }
            }
        };
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
            Size = TextSize, LineFactor = LineFactor,
            WikiLinkTapped = name => WikiLinkTapped?.Invoke(name),
            // a task inside a container: the tap edits the block (its list lines are not the block's lines)
            TaskToggled = container ? null : (line, on) => ToggleTask(block, line, on),
            MathTapped = index => _ = EditFormulaAsync(block.Start, index),
            CommentsTapped = index => _ = CommentsAsync(block.Start, index),
            AgentAsked = AgentAsked is null ? null : index => AskAgent(block.Start, index)
        };
        var holder = new Border { Background = Brushes.Transparent, Child = container ? reader.BuildMarkdown(source) : reader.Build(NoteMarks.Parse(source)) };
        // a tap (press and release without moving), never the press: a finger that starts a scroll on a block
        // must scroll, not turn the block into an edit box under it (the 1.8.4's preview scrolls the same way)
        if (block.Kind != "frontmatter")
        {
            holder.Tapped += (_, e) =>
            {
                // a hold already opened this block (the release of the same touch), or a link/checkbox took the tap
                if (e.Handled || !holder.IsAttachedToVisualTree()) return;
                e.Handled = true;
                // a touch that scrolled the note, or only stopped it, does not open the block (22/10/2026)
                if (!TouchIntent.IsIntended(holder)) return;
                TapBlock(block.Start, holder, e.Source as Visual, e.GetPosition(holder), select: false);
            };
            // a long press selects the word under the finger, with the bubble and a haptic tick (UX.md P7, P8)
            holder.AddHandler(InputElement.HoldingEvent, (object? _, HoldingRoutedEventArgs e) =>
            {
                if (e.HoldingState != HoldingState.Started || !holder.IsAttachedToVisualTree()) return;
                // the hold timer does not see a scroll (the block moves with the finger): a finger that scrolled the
                // note, even slowly, never selects nor opens the keyboard (22/10/2026)
                if (!TouchIntent.IsIntended(holder)) return;
                e.Handled = true;
                TapBlock(block.Start, holder, holder.InputHitTest(e.Position) as Visual, e.Position, select: true);
            });
        }
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
        if (_suggestion is not null) return;
        FinishEdit();
        var sep = _text.Length == 0 || _text.EndsWith("\n\n", StringComparison.Ordinal) ? "" : _text.EndsWith('\n') ? "\n" : "\n\n";
        _text += sep;
        var at = _text.Length;
        _text += "\n"; // a pending empty block; removed again if nothing is typed
        _editStart = _editEnd = at;
        StartBox("", at, at);
    }

    /// <summary>
    /// A tap (or a long press: <paramref name="select"/>) on a drawn block: it becomes editable with the caret at the
    /// character under the finger (mapped from the drawn text back to the Markdown), the block staying where it was on
    /// the screen; a long press selects the word there.
    /// </summary>
    public void TapBlock(int blockStart, Control holder, Visual? hit, Point at, bool select)
    {
        if (_suggestion is not null) return;   // an agent's suggestion waits for its answer first (UC-80)
        var scroll = Scroll;
        _anchorY = scroll is null ? null : EditorScrolling.TopIn(holder, scroll);
        _tapAt = at;
        var end = _blocks.FirstOrDefault(b => b.Start == blockStart)?.End ?? blockStart;
        var source = _text[blockStart..Math.Clamp(end, blockStart, _text.Length)];
        _tapCaret = RenderedIndex(holder, hit, at) is { } r ? SourceIndex(source, PlainText(holder), r) : null;
        _selectWord = select;
        Edit(blockStart);
    }

    private double? _anchorY;
    private int? _tapCaret;
    private bool _selectWord;

    /// <summary>The note's text drawn in a block (its text blocks in order, a line break between them).</summary>
    internal static string PlainText(Control holder) => string.Join('\n', TextBlocks(holder).Select(TextOf));

    private static IEnumerable<TextBlock> TextBlocks(Control holder) =>
        holder.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains(MarkdownReader.TextClass));

    private static string TextOf(TextBlock tb)
    {
        if (tb.Inlines is not { Count: > 0 } inlines) return tb.Text ?? "";
        var sb = new System.Text.StringBuilder();
        void Add(Inline i)
        {
            switch (i)
            {
                case Run r: sb.Append(r.Text); break;
                case LineBreak: sb.Append('\n'); break;
                case InlineUIContainer: sb.Append('￼'); break;
                case Span span: foreach (var c in span.Inlines) Add(c); break;
            }
        }
        foreach (var i in inlines) Add(i);
        return sb.ToString();
    }

    /// <summary>The index in <see cref="PlainText"/> of the character under <paramref name="at"/>, or null off the text.</summary>
    private static int? RenderedIndex(Control holder, Visual? hit, Point at)
    {
        var blocks = TextBlocks(holder).ToList();
        var tb = hit?.GetSelfAndVisualAncestors().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains(MarkdownReader.TextClass));
        // off a text (a margin, a bullet): the text block nearest to that height
        tb ??= blocks.OrderBy(t => holder.TranslatePoint(at, t) is { } q ? (q.Y < 0 ? -q.Y : q.Y > t.Bounds.Height ? q.Y - t.Bounds.Height : 0) : double.MaxValue).FirstOrDefault();
        if (tb is null || holder.TranslatePoint(at, tb) is not { } p) return null;
        var h = tb.TextLayout.HitTestPoint(new Point(Math.Max(0, p.X), Math.Clamp(p.Y, 0, Math.Max(0, tb.Bounds.Height - 1))));
        var index = h.TextPosition + (h.IsTrailing ? 1 : 0);
        int offset = 0;
        foreach (var b in blocks)
        {
            if (ReferenceEquals(b, tb)) return offset + Math.Clamp(index, 0, TextOf(b).Length);
            offset += TextOf(b).Length + 1;
        }
        return null;
    }

    /// <summary>
    /// Maps an index in the drawn text to the Markdown source: the characters of the drawn text are found in order in the
    /// source, the marks between them skipped (`**`, `[[`, `# `, `- `…). A drawn character not on the same source line (a
    /// bullet, a placeholder) is passed over.
    /// </summary>
    public static int SourceIndex(string source, string plain, int at)
    {
        int i = 0;
        at = Math.Clamp(at, 0, plain.Length);
        for (int j = 0; j < at && i < source.Length; j++)
        {
            int k = i;
            while (k < source.Length && source[k] != plain[j] && !(plain[j] != '\n' && source[k] == '\n')) k++;
            if (k < source.Length && source[k] == plain[j]) i = k + 1;
        }
        // before the next drawn character: past the marks that open it (the caret lands inside **bold**, not before it)
        if (at < plain.Length)
        {
            int k = i;
            while (k < source.Length && source[k] != plain[at] && source[k] != '\n' && k - i < 8) k++;
            if (k < source.Length && source[k] == plain[at]) i = k;
        }
        return Math.Clamp(i, 0, source.Length);
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

    /// <summary>The box's padding: the text inside sits where the drawn text was.</summary>
    private const double PadX = 12, PadY = 6;

    private void StartBox(string source, int start, int end, string kind = "paragraph")
    {
        _editStart = start;
        _editEnd = end;
        // the box reads like the drawn block: same font, size, line height and place (no jump between the two, UC-57)
        var heading = System.Text.RegularExpressions.Regex.Match(source, @"^(#{1,6})\s");
        var (em, weight, top) = heading.Success
            ? heading.Groups[1].Length switch
            {
                1 => (1.9, (FontWeight)750, .2),
                2 => (1.4, FontWeight.Bold, 1.4),
                3 => (1.15, (FontWeight)650, 1.2),
                _ => (1.0, FontWeight.Bold, 1.33)
            }
            : (kind == "code" ? .88 : 1.0, FontWeight.Normal, kind is "paragraph" ? .6 : .25);
        var size = TextSize * em;
        var margin = Math.Max(0, top * TextSize - PadY);
        _box = new TextBox
        {
            Text = source, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, BorderThickness = new Thickness(0),
            Padding = new Thickness(PadX, PadY), Margin = new Thickness(-PadX, margin, -PadX, margin), CornerRadius = new CornerRadius(UrbeTokens.Radius.M),
            FontFamily = kind == "code" ? UrbeTheme.MonoFont : heading.Success ? UrbeTheme.UiFont : UrbeTheme.EditorFont, FontSize = size, FontWeight = weight,
            LineHeight = heading.Success ? size * 1.25 : size * LineFactor, MinHeight = 0,
            Foreground = weight == FontWeight.Normal ? UrbeTheme.Brush(UrbeTheme.Text, MarkdownReader.BodyInk) : UrbeTheme.Brush(UrbeTheme.Text),
            Background = Brushes.Transparent,
            CaretBrush = UrbeTheme.Brush(UrbeTheme.Accent), SelectionBrush = UrbeTheme.Brush(UrbeTheme.Accent, .35),
            SelectionForegroundBrush = UrbeTheme.Brush(UrbeTheme.Text)
        };
        Avalonia.Automation.AutomationProperties.SetName(_box, "Bloco em edição");
        // the block being written is marked by a soft, warm backdrop that fades in (UX.md P6)
        _backdrop.Margin = _box.Margin;
        _backdrop.CornerRadius = new CornerRadius(UrbeTokens.Radius.M);
        _backdrop.Background = UrbeTheme.Brush(UrbeTheme.Accent, .07);
        _backdrop.BorderBrush = UrbeTheme.Brush(UrbeTheme.Accent, .22);
        _backdrop.BorderThickness = new Thickness(1);
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused" })
            _box.Resources[key] = Brushes.Transparent;
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
            TouchTextScroll.ShowSelectionHandles(_box);
            e.Handled = true;
            Ui.PlatformHaptics.Tick();
        };
        _box.KeyDown += OnBoxKey;
        _box.AddHandler(KeyDownEvent, OnBoxKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _box.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.SelectionStartProperty || e.Property == TextBox.SelectionEndProperty || e.Property == TextBox.CaretIndexProperty) UpdateTools();
            if (e.Property == TextBox.CaretIndexProperty) KeepCaretSoon(typing: false);
        };
        _box.LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            // focus moved to something else (not one of our tools, which never take it)
            if (!_prompting && _box is not null && !_box.IsFocused && !IsKeyboardFocusWithin) FinishEdit();
        }, DispatcherPriority.Background);
        Rebuild();
        var tap = _tapAt;
        var caret = _tapCaret;
        var anchor = _anchorY;
        var select = _selectWord;
        _tapAt = null;
        _tapCaret = null;
        _anchorY = null;
        _selectWord = false;
        var box = _box;
        if (!UrbeTokens.Motion.Reduced) Ui.UrbeMotion.Enter(_backdrop, 0, 0, UrbeTokens.Motion.Fast);
        // laid out now, in the same pass: the caret, the block's place and the keyboard come with the first frame
        var scroll = Scroll;
        if (box.IsAttachedToVisualTree())
        {
            if (scroll is not null) scroll.UpdateLayout(); else UpdateLayout();
            if (scroll is not null && anchor is { } y && box.Parent is Control cell) KeepAt(cell, y, scroll);
            Place(box, tap, caret, select);
        }
        EditingChanged?.Invoke(true);
        // a box not laid out yet (the surface was not on screen): the same once it is
        Dispatcher.UIThread.Post(() => { if (_box == box && !box.IsFocused) Place(box, tap, caret, select); }, DispatcherPriority.Loaded);
    }

    private void Place(TextBox box, Point? tap, int? caret, bool select)
    {
        box.Focus();
        var len = box.Text?.Length ?? 0;
        var at = caret is { } c ? Math.Clamp(c, 0, len) : CaretFromTap(box, tap) ?? len;
        box.SelectionStart = box.SelectionEnd = box.CaretIndex = at;
        if (select)
        {
            var (from, to) = WordAt(box.Text ?? "", at);
            if (to > from) { box.SelectionStart = from; box.SelectionEnd = to; }
            Ui.PlatformHaptics.Hold();
        }
        // a box focused by the app does not always bring up the phone's keyboard: ask for it (UC-48)
        PlatformInput.ShowKeyboard?.Invoke();
    }

    private bool _keepPending, _keepTyping;

    /// <summary>Keeps the caret line in sight after the next layout (typing: in the upper two thirds of the visible area).</summary>
    public void KeepCaretSoon(bool typing)
    {
        _keepTyping |= typing;
        if (_keepPending || _box is null) return;
        _keepPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _keepPending = false;
            var t = _keepTyping;
            _keepTyping = false;
            if (_box is { } box && Scroll is { } scroll) EditorScrolling.KeepCaret(box, scroll, t);
        }, DispatcherPriority.Loaded);
    }

    /// <summary>The visible area changed height: keeps the caret in sight only if it was in sight before (see <see cref="EditorScrolling.InSight"/>).</summary>
    public void KeepCaretAfterResize(double previousHeight)
    {
        if (_box is { } box && Scroll is { } scroll && EditorScrolling.InSight(box, scroll, previousHeight)) KeepCaretSoon(typing: true);
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
        // the box's text sits where the drawn block's text was: the point (in the drawn block) maps through the box's margin
        var q = box.TranslatePoint(new Point(p.X - box.Margin.Left, p.Y - box.Margin.Top), presenter) ?? p;
        var hit = layout.HitTestPoint(new Point(Math.Max(0, q.X), Math.Max(0, q.Y)));
        var at = hit.TextPosition + (hit.IsTrailing ? 1 : 0);
        return Math.Clamp(at, 0, box.Text?.Length ?? 0);
    }

    private Control EditorFor()
    {
        // the backdrop, the box, the bubble and the menus are reused: take them out of the panel of the previous build first
        Control[] parts = [_backdrop, _box!, _bubbleLayer, _slash, _wiki];
        foreach (var c in parts) if (c.Parent is Panel old) old.Children.Remove(c);
        // the bubble floats over the text (a canvas of no height): showing it never pushes the block down (UC-57)
        var cell = new Grid { Children = { _backdrop, _box!, _bubbleLayer } };
        return new StackPanel { Children = { cell, _slash, _wiki }, ZIndex = 1 };
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
        KeepCaretSoon(typing: true);
    }

    private void UpdateTools()
    {
        if (_box is null) return;
        var text = _box.Text ?? "";
        // while a key or the keyboard (IME) changes the text, the selection can still point past its end
        var (a, b) = Range(_box);
        var bubbleOn = b > a && text[a..b].Trim().Length > 0;
        if (bubbleOn) ShowBubble(a); else _bubble.IsVisible = false;
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

    /// <summary>UC-80: a request to an agent asked from the note (its line's button, or Ctrl+Enter on it); the line in the note's text.</summary>
    public event Action<NoteMarks.AgentCall>? AgentAsked;

    /// <summary>The caret in the note's text while a block is edited, else null.</summary>
    public int? SourceCaret => _box is null || _editStart < 0 ? null : _editStart + _box.CaretIndex;

    private void AskAgent(int blockStart, int index)
    {
        var before = _text.Length;
        var editingStart = _editStart;
        FinishEdit();
        var shift = _text.Length - before;
        var start = blockStart > editingStart && editingStart >= 0 ? blockStart + shift : blockStart;
        if (_blocks.FirstOrDefault(b => b.Start == start) is not { } block) return;
        var calls = NoteMarks.AgentCalls(_text[block.Start..block.End]);
        if (index >= 0 && index < calls.Count) AgentAsked?.Invoke(calls[index] with { Start = calls[index].Start + block.Start });
    }

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
        // Slash("codeblock") has no edit of its own: the block becomes an empty fenced block
        if (act == "codeblock") Run("codeblock");
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

    /// <summary>The bubble floats just above the selection (below it at the top of the screen), springing in.</summary>
    private void ShowBubble(int selectionStart)
    {
        if (_box is null) return;
        var was = _bubble.IsVisible;
        _bubble.IsVisible = true;
        _bubble.MaxWidth = Math.Max(120, Bounds.Width + PadX);
        _bubble.Measure(Size.Infinity);
        var size = _bubble.DesiredSize;
        double x = 0, y = -size.Height - 8;
        var presenter = _box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().FirstOrDefault();
        if (presenter is not null)
        {
            var r = presenter.TextLayout.HitTestTextPosition(Math.Clamp(selectionStart, 0, (_box.Text ?? "").Length));
            if (presenter.TranslatePoint(r.TopLeft, _bubbleLayer) is { } p)
            {
                x = p.X - 16;
                y = p.Y - size.Height - 8;
                // too close to the top of the screen: under the line instead
                if (Scroll is { } scroll && presenter.TranslatePoint(r.TopLeft, scroll) is { } s && s.Y - size.Height - 8 < 0)
                    y = p.Y + Math.Max(r.Height, _box.LineHeight) + 8;
            }
        }
        var room = Math.Max(0, Bounds.Width - size.Width);
        Canvas.SetLeft(_bubble, Math.Clamp(x, -PadX / 2, room));
        Canvas.SetTop(_bubble, y);
        if (was || UrbeTokens.Motion.Reduced) return;
        _bubble.RenderTransformOrigin = RelativePoint.Center;
        _bubble.Transitions = null;
        _bubble.Opacity = 0;
        _bubble.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(0.92)");
        _bubble.Transitions =
        [
            new DoubleTransition { Property = OpacityProperty, Duration = UrbeTokens.Motion.Fast, Easing = UrbeTokens.Motion.EaseOut },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = UrbeTokens.Motion.Normal, Easing = UrbeTokens.Motion.Spring }
        ];
        _bubble.Opacity = 1;
        _bubble.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
    }

    private Border BuildBubble()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        void Add(string act, Control content, string tip)
        {
            var b = new Button
            {
                Content = content, Width = 44, Height = 44, Padding = new Thickness(0), CornerRadius = new CornerRadius(UrbeTokens.Radius.M), Focusable = false,
                Background = Brushes.Transparent, Foreground = UrbeTheme.Brush(UrbeTheme.Text),
                HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
            };
            b.Resources["ButtonBackgroundPointerOver"] = UrbeTheme.Brush(UrbeTheme.Surface3, .8);
            b.Resources["ButtonBackgroundPressed"] = UrbeTheme.Brush(UrbeTheme.Surface3);
            ToolTip.SetTip(b, tip);
            Avalonia.Automation.AutomationProperties.SetName(b, tip);
            Ui.UrbeMotion.Pressable(b, haptic: true);
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
        // narrower than all its buttons (a phone): it scrolls sideways, never off the screen
        var card = EditorChrome.Card(new ScrollViewer
        {
            Content = row, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });
        card.IsVisible = true;
        card.Padding = new Thickness(4);
        card.CornerRadius = new CornerRadius(UrbeTokens.Radius.L);
        card.BoxShadow = UrbeTokens.Elevation.Mid;
        return new Border { Child = card, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left };
    }
}
