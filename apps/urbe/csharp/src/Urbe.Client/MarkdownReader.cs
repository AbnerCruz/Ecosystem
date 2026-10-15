using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The rendered note of the 1.8.4 editor (#renderedPreview): the element tree of src/editor/markdown.js
/// (MarkdownTree) drawn with the sizes, colours and spacing of theme.css / base.css, natively. No HTML, no WebView.
/// </summary>
public sealed class MarkdownReader
{
    private const double Size = UrbeTheme.EditorSize;     // --ui-editor-size

    /// <summary>A [[wikilink]] was tapped (target note name).</summary>
    public Action<string>? WikiLinkTapped { get; set; }
    /// <summary>A task checkbox was toggled: index of the list line inside the block, new state.</summary>
    public Action<int, bool>? TaskToggled { get; set; }
    /// <summary>A formula was tapped: its index among the formulas of this text, in order (math/editor.js: a click on .umath
    /// opens the pane to edit it).</summary>
    public Action<int>? MathTapped { get; set; }
    private int _math;
    /// <summary>The 💬 of a word's comments was tapped (UC-49): its index among the comment runs of this text, in order.</summary>
    public Action<int>? CommentsTapped { get; set; }
    private int _notes;

    private int _listLine;

    public Control Build(IReadOnlyList<RichNode> nodes)
    {
        _listLine = 0;
        var panel = new StackPanel { Orientation = Orientation.Vertical };
        foreach (var n in nodes) AddBlock(panel, n);
        return panel;
    }

    private void AddBlock(Panel parent, RichNode n)
    {
        if (n.Tag == RichNode.TextTag) { if (n.Text.Trim().Length > 0) parent.Children.Add(Para(n.Text)); return; }
        parent.Children.Add(Block(n));
    }

    private Control Block(RichNode n)
    {
        switch (n.Tag)
        {
            case "h1": return Heading(n, 1.9, (FontWeight)750, new Thickness(0, .2 * Size, 0, .5 * Size));
            case "h2": return Heading(n, 1.4, FontWeight.Bold, new Thickness(0, 1.4 * Size, 0, .4 * Size));
            case "h3": return Heading(n, 1.15, (FontWeight)650, new Thickness(0, 1.2 * Size, 0, .3 * Size));
            case "h4": case "h5": case "h6": return Heading(n, 1, FontWeight.Bold, new Thickness(0, 1.33 * Size, 0, 1.33 * Size));
            case "p" when n.HasClass(NoteMarks.TagClass): return Tagged(n);
            case "p": { var t = Text(n.Children, Size); t.Margin = new Thickness(0, .6 * Size); return t; }
            case "hr": return new Border { Height = 1, Background = UrbeTheme.Brush(UrbeTheme.Line2), Margin = new Thickness(0, 2 * Size) };
            case "ul": case "ol": return List(n);
            case "blockquote":
                return new Border
                {
                    Margin = new Thickness(0, Size), Padding = new Thickness(Size, .1 * Size, 0, .1 * Size),
                    BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(3, 0, 0, 0),
                    Child = Text(n.Children, Size, UrbeTheme.Text2)
                };
            case "pre": return Pre(n);
            case "table": return Table(n);
            case "div" when n.HasClass("callout"): return Callout(n);
            case "div" when n.HasClass("umath-block"):
                return new Border { Margin = new Thickness(0, 14), Padding = new Thickness(12, 10), Child = MathNode(n, Size + 3, true) };
            case "section" when n.Attrs.ContainsKey("data-frontmatter-card"): return Frontmatter(n);
            default: return Text(n.Children.Count == 0 ? [n] : n.Children, Size);
        }
    }

    /// <summary>A paragraph that starts with #Tag: (DEC-0045, UC-43): the tag's colour as a bar and a soft background, the
    /// tag as a small label above the text.</summary>
    private Control Tagged(RichNode n)
    {
        var tag = n.Attr("data-tag");
        var color = UrbeTheme.TagColor(tag);
        var label = new TextBlock
        {
            Text = "#" + tag, FontSize = Size * .72, FontWeight = FontWeight.Bold, LetterSpacing = .3,
            Foreground = UrbeTheme.Brush(color), Margin = new Thickness(0, 0, 0, 2)
        };
        var body = Text(n.Children, Size);
        return new Border
        {
            Margin = new Thickness(0, .5 * Size), Padding = new Thickness(.75 * Size, .45 * Size),
            CornerRadius = new CornerRadius(8),
            Background = UrbeTheme.Brush(color, .1), BorderBrush = UrbeTheme.Brush(color, .9), BorderThickness = new Thickness(3, 0, 0, 0),
            Child = new StackPanel { Children = { label, body } }
        };
    }

    private static Control Para(string text) => new TextBlock { Text = text, Margin = new Thickness(0, .6 * Size), TextWrapping = TextWrapping.Wrap };

    private Control Heading(RichNode n, double em, FontWeight weight, Thickness margin)
    {
        var t = Text(n.Children, Size * em, UrbeTheme.Text, weight, UrbeTheme.UiFont);
        t.Margin = margin;
        t.LineHeight = Size * em * 1.25;
        return t;
    }

    // ------------------------------------------------------------------ inline content

    private sealed class RichText : TextBlock
    {
        public List<(int Start, int Length, string Name)> Wiki { get; } = [];
        public List<(int Start, int Length, string Url)> Links { get; } = [];
        protected override Type StyleKeyOverride => typeof(TextBlock);
    }

    private TextBlock Text(IReadOnlyList<RichNode> children, double size, string color = UrbeTheme.Text, FontWeight weight = FontWeight.Normal, FontFamily? family = null)
    {
        var tb = new RichText
        {
            FontSize = size, Foreground = UrbeTheme.Brush(color), FontWeight = weight, TextWrapping = TextWrapping.Wrap,
            LineHeight = size * 1.7,
            FontFamily = family ?? UrbeTheme.UiFont
        };
        var length = 0;
        foreach (var c in children) Inline(tb, c, tb.Inlines!, ref length, new Style { Color = color });
        tb.Tapped += OnTextTapped;
        return tb;
    }

    private readonly record struct Style(bool Bold = false, bool Italic = false, bool Strike = false, bool Code = false, bool Link = false, string Color = UrbeTheme.Text)
    {
        public Style With(bool? bold = null, bool? italic = null, bool? strike = null, bool? code = null, bool? link = null, string? color = null) =>
            new(bold ?? Bold, italic ?? Italic, strike ?? Strike, code ?? Code, link ?? Link, color ?? Color);
    }

    private void Inline(RichText host, RichNode n, InlineCollection into, ref int length, Style st)
    {
        switch (n.Tag)
        {
            case RichNode.TextTag: AddRun(host, into, n.Text, st, ref length); return;
            case "br": into.Add(new LineBreak()); length += 1; return;
            case "strong": case "b": st = st.With(bold: true); break;
            case "em": case "i": st = st.With(italic: true); break;
            case "del": case "s": st = st.With(strike: true); break;
            case "code": st = st.With(code: true); break;
            case "a": st = st.With(link: true, color: UrbeTheme.Accent); break;
            case "span" when n.HasClass("wikilink"):
            {
                var start = length;
                var inner = n.Children.Select(Plain).Aggregate("", (a, b) => a + b);
                var run = new Run(inner)
                {
                    Foreground = UrbeTheme.Brush(UrbeTheme.Accent), Background = UrbeTheme.Brush(UrbeTheme.Accent, .14)
                };
                if (st.Bold) run.FontWeight = FontWeight.Bold;
                if (st.Italic) run.FontStyle = FontStyle.Italic;
                into.Add(run); length += inner.Length;
                host.Wiki.Add((start, inner.Length, n.Attr("data-note-name")));
                return;
            }
            case "span" when n.HasClass(NoteMarks.CommentClass):
            {
                // %%comentário%% (DEC-0045, UC-43): the writer's note to self, set apart and never printed
                var inner = n.Children.Select(Plain).Aggregate("", (a, b) => a + b);
                into.Add(new Run(inner)
                {
                    Foreground = UrbeTheme.Brush(UrbeTheme.Comment), Background = UrbeTheme.Brush(UrbeTheme.Warn, .12), FontStyle = FontStyle.Italic
                });
                length += inner.Length;
                return;
            }
            case "span" when n.HasClass(NoteMarks.NotesClass):
            {
                // palavra{"comentário"} (UC-49): a small 💬 raised over the end of the word; a tap opens the comments
                var notes = System.Text.Json.JsonSerializer.Deserialize<string[]>(n.Attr("data-comments")) ?? [];
                int index = _notes++;
                var mark = new Button
                {
                    Content = new TextBlock { Text = notes.Length > 1 ? "💬" + notes.Length : "💬", FontSize = Size * .62, Foreground = UrbeTheme.Brush(UrbeTheme.Accent) },
                    Padding = new Thickness(1, 0), MinWidth = 0, MinHeight = 0, Margin = new Thickness(0, 0, 0, Size * .55),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                };
                ToolTip.SetTip(mark, string.Join("\n", notes));
                Avalonia.Automation.AutomationProperties.SetName(mark, notes.Length == 1 ? "1 comentário" : notes.Length + " comentários");
                mark.Click += (_, e) => { e.Handled = true; CommentsTapped?.Invoke(index); };
                mark.Tapped += (_, e) => e.Handled = true;   // the block does not become an edit box under the 💬
                into.Add(new InlineUIContainer(mark) { BaselineAlignment = BaselineAlignment.Top });
                length += 1;
                return;
            }
            case "span" when n.HasClass(NoteMarks.AiClass):
            {
                // {ia:"modelo"} (UC-51): text written by the Assistente — a tiny green "modelo" after it
                var model = n.Attr("data-model");
                into.Add(new Run(" " + model) { FontSize = Size * .55, Foreground = UrbeTheme.Brush("#3fb86b"), BaselineAlignment = BaselineAlignment.Superscript });
                length += model.Length + 1;
                return;
            }
            case "span" when n.HasClass("umath"):
                into.Add(new InlineUIContainer(MathNode(n, Size + 2, n.HasClass("umath-display")))); length += 1; return;
            case "img":
                into.Add(new Run("[imagem" + (n.Attr("alt").Length > 0 ? ": " + n.Attr("alt") : "") + "]") { Foreground = UrbeTheme.Brush(UrbeTheme.Text3) });
                length += 8 + n.Attr("alt").Length; return;
            case "input": return; // task checkbox: drawn by the list item
        }
        if (n.Tag == "a")
        {
            var start = length;
            foreach (var c in n.Children) Inline(host, c, into, ref length, st);
            host.Links.Add((start, length - start, n.Attr("href")));
            return;
        }
        foreach (var c in n.Children) Inline(host, c, into, ref length, st);
    }

    private static void AddRun(RichText host, InlineCollection into, string text, Style st, ref int length)
    {
        if (text.Length == 0) return;
        // only bold/italic marks set the run's weight and style: plain text keeps the block's (a heading is 750/700/650)
        var run = new Run(text) { Foreground = UrbeTheme.Brush(st.Code ? "#f0d9a8" : st.Color) };
        if (st.Bold) run.FontWeight = FontWeight.Bold;
        if (st.Italic) run.FontStyle = FontStyle.Italic;
        if (st.Code) { run.FontFamily = UrbeTheme.MonoFont; run.FontSize = Size * .88; run.Background = UrbeTheme.Brush(UrbeTheme.Surface2); }
        var deco = new TextDecorationCollection();
        if (st.Strike) deco.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
        if (st.Link) deco.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
        if (deco.Count > 0) run.TextDecorations = deco;
        into.Add(run);
        length += text.Length;
    }

    private static string Plain(RichNode n) => n.Tag == RichNode.TextTag ? n.Text : string.Concat(n.Children.Select(Plain));

    private void OnTextTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not RichText tb || (tb.Wiki.Count == 0 && tb.Links.Count == 0)) return;
        var hit = tb.TextLayout.HitTestPoint(e.GetPosition(tb));
        var pos = hit.TextPosition;
        foreach (var (s, l, name) in tb.Wiki)
            if (pos >= s && pos < s + l) { e.Handled = true; WikiLinkTapped?.Invoke(name); return; }
        foreach (var (s, l, url) in tb.Links)
            if (pos >= s && pos < s + l && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto")
            {
                e.Handled = true;
                _ = TopLevel.GetTopLevel(tb)?.Launcher.LaunchUriAsync(uri);
                return;
            }
    }

    // ------------------------------------------------------------------ lists, code, tables, callouts

    private Control List(RichNode list)
    {
        var ordered = list.Tag == "ol";
        var panel = new StackPanel { Margin = new Thickness(0, .25 * Size) };
        var n = 0;
        foreach (var li in list.Children.Where(c => c.Tag == "li"))
        {
            n++;
            var task = li.HasClass("task");
            var line = _listLine++;
            var inline = li.Children.Where(c => c.Tag is not ("ul" or "ol")).ToList();
            var nested = li.Children.Where(c => c.Tag is "ul" or "ol").ToList();
            var content = new StackPanel();
            var text = Text(inline, Size);
            text.Margin = new Thickness(0, .25 * Size, 0, 0);
            content.Children.Add(text);
            foreach (var sub in nested) content.Children.Add(List(sub));
            var checkedBox = task && li.Children.FirstOrDefault(c => c.Tag == "input")?.Attrs.ContainsKey("checked") == true;
            Control marker = task ? Checkbox(checkedBox, line) : new TextBlock
            {
                Text = ordered ? n + "." : "•", Foreground = UrbeTheme.Brush(UrbeTheme.Text3), FontSize = Size,
                LineHeight = Size * 1.7, Margin = new Thickness(0, .25 * Size, 8, 0), HorizontalAlignment = HorizontalAlignment.Right,
                MinWidth = ordered ? 22 : 12
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(task ? 0 : 1.4 * Size - 22 > 0 ? 6 : 0, 0, 0, 0) };
            Grid.SetColumn(content, 1);
            row.Children.Add(marker);
            row.Children.Add(content);
            panel.Children.Add(row);
        }
        panel.Margin = new Thickness(panel.Margin.Left + 8, panel.Margin.Top, 0, panel.Margin.Bottom);
        return panel;
    }

    private Control Checkbox(bool isChecked, int line)
    {
        var box = new Border
        {
            Width = 18, Height = 18, CornerRadius = new CornerRadius(4), Margin = new Thickness(0, .25 * Size + 5, 8, 0),
            BorderThickness = new Thickness(1.5), BorderBrush = UrbeTheme.Brush(isChecked ? UrbeTheme.Accent : UrbeTheme.Text3),
            Background = isChecked ? UrbeTheme.Brush(UrbeTheme.Accent) : Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Top, Cursor = new Cursor(StandardCursorType.Hand),
            Child = isChecked ? new Path { Data = Geometry.Parse("M3 9l3.5 3.5L14 5"), Stroke = UrbeTheme.Brush(UrbeTheme.AccentInk), StrokeThickness = 2, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Width = 16, Height = 16 } : null
        };
        box.Tapped += (_, e) => { e.Handled = true; TaskToggled?.Invoke(line, !isChecked); };
        return box;
    }

    private static Control Pre(RichNode pre)
    {
        var code = string.Concat(pre.Children.Select(Plain));
        var inner = new StackPanel();
        inner.Children.Add(new TextBlock
        {
            Text = code, FontFamily = UrbeTheme.MonoFont, FontSize = Size * .88, Foreground = UrbeTheme.Brush(UrbeTheme.Text),
            TextWrapping = TextWrapping.NoWrap
        });
        var lang = pre.Attr("data-lang");
        var grid = new Grid();
        grid.Children.Add(new ScrollViewer { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, Content = inner });
        if (lang.Length > 0)
            grid.Children.Add(new TextBlock
            {
                Text = lang.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = UrbeTheme.Brush(UrbeTheme.Text3),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -8, -6, 0), IsHitTestVisible = false
            });
        return new Border
        {
            Margin = new Thickness(0, Size), Padding = new Thickness(16, 14), CornerRadius = new CornerRadius(12),
            Background = UrbeTheme.Brush(UrbeTheme.Surface), BorderBrush = UrbeTheme.Brush(UrbeTheme.Line), BorderThickness = new Thickness(1), Child = grid
        };
    }

    private Control Table(RichNode table)
    {
        var rows = table.Children.SelectMany(c => c.Tag is "thead" or "tbody" ? c.Children : [c]).Where(c => c.Tag == "tr").ToList();
        var columns = rows.Count == 0 ? 0 : rows[0].Children.Count(c => c.Tag is "th" or "td");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", System.Math.Max(1, columns)))) };
        for (var r = 0; r < rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var cells = rows[r].Children.Where(c => c.Tag is "th" or "td").ToList();
            for (var c = 0; c < cells.Count && c < columns; c++)
            {
                var cell = cells[c];
                var head = cell.Tag == "th";
                var t = Text(cell.Children, Size * .94 , UrbeTheme.Text, head ? FontWeight.SemiBold : FontWeight.Normal);
                var align = cell.Attr("style").Contains("center") ? TextAlignment.Center : cell.Attr("style").Contains("right") ? TextAlignment.Right : TextAlignment.Left;
                t.TextAlignment = align;
                var b = new Border
                {
                    BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2), BorderThickness = new Thickness(.5), Padding = new Thickness(10, 8), Child = t,
                    Background = head ? UrbeTheme.Brush(UrbeTheme.Surface2) : (r % 2 == 0 ? UrbeTheme.Brush("#ffffff", .025) : null)
                };
                Grid.SetRow(b, r); Grid.SetColumn(b, c);
                grid.Children.Add(b);
            }
        }
        return new Border { Margin = new Thickness(0, Size), CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = grid };
    }

    private Control Callout(RichNode n)
    {
        var type = n.Attr("data-callout");
        var color = type switch
        {
            "tip" or "success" => UrbeTheme.Ok,
            "warning" or "question" or "todo" => "#f1c46e",
            "danger" or "bug" => UrbeTheme.Danger,
            _ => UrbeTheme.Accent
        };
        var title = n.Children.FirstOrDefault(c => c.HasClass("callout-title"));
        var body = n.Children.FirstOrDefault(c => c.HasClass("callout-body"));
        var titleNodes = title?.Children ?? [];
        var emptyTitle = titleNodes.All(c => c.Tag == "br");
        var panel = new StackPanel();
        var tt = emptyTitle
            ? new TextBlock { Text = title?.Attr("data-default") ?? type, TextWrapping = TextWrapping.Wrap }
            : Text(titleNodes, Size, color);
        tt.FontWeight = FontWeight.Bold; tt.Foreground = UrbeTheme.Brush(color); tt.FontSize = Size; tt.Margin = new Thickness(0, 0, 0, 4);
        panel.Children.Add(tt);
        if (body is not null && !body.Children.All(c => c.Tag == "br")) panel.Children.Add(Text(body.Children, Size));
        return new Border
        {
            Margin = new Thickness(0, Size), Padding = new Thickness(14, 10, 14, 12), CornerRadius = new CornerRadius(12),
            BorderBrush = UrbeTheme.Brush(color), BorderThickness = new Thickness(4, 0, 0, 0),
            Background = UrbeTheme.Brush(color, .1), Child = panel
        };
    }

    private static Control Frontmatter(RichNode card)
    {
        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var row in card.Children.Where(c => c.HasClass("frontmatterRow")))
        {
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(0, 3, 10, 3) };
            foreach (var part in row.Children)
            {
                if (part.HasClass("frontmatterKey"))
                    line.Children.Add(new TextBlock { Text = Plain(part).ToLowerInvariant(), FontSize = 10, Foreground = UrbeTheme.Brush("#718895"), VerticalAlignment = VerticalAlignment.Center });
                else
                {
                    var chips = part.Children.Where(c => c.HasClass("frontmatterChip")).ToList();
                    if (chips.Count == 0)
                        line.Children.Add(new TextBlock { Text = Plain(part), FontSize = 11, Foreground = UrbeTheme.Brush("#cbd7dd"), VerticalAlignment = VerticalAlignment.Center });
                    else
                        foreach (var chip in chips)
                            line.Children.Add(new Border
                            {
                                CornerRadius = new CornerRadius(999), Background = UrbeTheme.Brush("#162630"), Padding = new Thickness(6, 2),
                                Child = new TextBlock { Text = Plain(chip), FontSize = 10, Foreground = UrbeTheme.Brush("#bcd7e6") }
                            });
                }
            }
            wrap.Children.Add(line);
        }
        return new Border { Margin = new Thickness(0, 0, 0, 18), Padding = new Thickness(0, 0, 0, 12), BorderBrush = UrbeTheme.Brush("#20313c"), BorderThickness = new Thickness(0, 0, 0, 1), Child = wrap };
    }

    // ------------------------------------------------------------------ math (MathRenderer SVG → native paths)

    private Control MathNode(RichNode atom, double size, bool display)
    {
        var view = MathView(atom, size, display);
        int index = _math++;
        if (MathTapped is not { } tapped) return view;
        var holder = new Border { Background = Brushes.Transparent, Child = view, HorizontalAlignment = view.HorizontalAlignment, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
        holder.Tapped += (_, e) => { e.Handled = true; tapped(index); };
        return holder;
    }

    private static Control MathView(RichNode atom, double size, bool display)
    {
        var tex = atom.Attr("data-tex");
        try
        {
            var result = MathRenderer.RenderSvg(tex, display, (float)size);
            if (result.Success && MathSvg.TryBuild(result.Svg, UrbeTheme.Text) is { } view)
                return new Border { HorizontalAlignment = display ? HorizontalAlignment.Center : HorizontalAlignment.Left, Child = view };
            return RawMath(atom, result.Diagnostic ?? "", size);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or NotSupportedException)
        {
            return RawMath(atom, ex.Message, size);
        }
    }

    // .umath-raw: the formula source in the mono font, warn colour (src/styles/math.css)
    private static Control RawMath(RichNode atom, string diagnostic, double size)
    {
        var t = new TextBlock
        {
            Text = atom.Attr("data-open") + atom.Attr("data-tex") + atom.Attr("data-close"), FontFamily = UrbeTheme.MonoFont, FontSize = 13,
            Foreground = UrbeTheme.Brush("#f1c46e"), TextWrapping = TextWrapping.Wrap
        };
        if (diagnostic.Length > 0) ToolTip.SetTip(t, diagnostic);
        _ = size;
        return t;
    }
}

/// <summary>The SVG subset SvgMathCanvas writes (path, line) drawn as native shapes in the text colour.</summary>
internal static class MathSvg
{
    public static Control? TryBuild(string svg, string color)
    {
        try
        {
            var root = XDocument.Parse(svg).Root;
            if (root is null) return null;
            var w = double.Parse(root.Attribute("width")?.Value ?? "0", CultureInfo.InvariantCulture);
            var h = double.Parse(root.Attribute("height")?.Value ?? "0", CultureInfo.InvariantCulture);
            if (w <= 0 || h <= 0) return null;
            var canvas = new Canvas { Width = w, Height = h };
            var brush = UrbeTheme.Brush(color);
            foreach (var e in root.Elements())
            {
                var name = e.Name.LocalName;
                var stroke = e.Attribute("stroke")?.Value;
                var fill = e.Attribute("fill")?.Value;
                var width = double.Parse(e.Attribute("stroke-width")?.Value ?? "1", CultureInfo.InvariantCulture);
                if (name == "path" && e.Attribute("d")?.Value is { } d)
                    canvas.Children.Add(new Path
                    {
                        Data = Geometry.Parse(d),
                        Fill = Painted(fill) ? brush : null,
                        Stroke = Painted(stroke) ? brush : null,
                        StrokeThickness = width
                    });
                else if (name == "line")
                    canvas.Children.Add(new Avalonia.Controls.Shapes.Line
                    {
                        StartPoint = new Point(D(e, "x1"), D(e, "y1")), EndPoint = new Point(D(e, "x2"), D(e, "y2")), Stroke = brush, StrokeThickness = width
                    });
            }
            return canvas;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or FormatException or InvalidOperationException)
        {
            return null;
        }
    }

    // the canvas paints its background as a fully transparent path: nothing to draw
    private static bool Painted(string? paint) =>
        paint is not null && paint != "none" && !(paint.StartsWith("rgba(", StringComparison.Ordinal) && paint.EndsWith(",0)", StringComparison.Ordinal));

    private static double D(XElement e, string a) => double.Parse(e.Attribute(a)?.Value ?? "0", CultureInfo.InvariantCulture);
}
