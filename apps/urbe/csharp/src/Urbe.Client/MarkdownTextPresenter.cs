using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The text of a Visual block being written, drawn formatted: the Markdown stays as it is (every character is in the
/// text, the caret and the selection move over all of them), but bold is bold, a link is a link, code is code, and the
/// marks (<c>**</c>, <c>[[</c>, <c>#</c>) are dimmed. Writing a block then looks like the block drawn, with its marks
/// showing, instead of turning into plain source (MarkdownSyntax decides the spans).
/// </summary>
public sealed class MarkdownTextPresenter : TextPresenter
{
    private Size _constraint;

    /// <summary>Draws the text formatted (false: as plain text, as a code block is).</summary>
    public bool Formatted { get; set; } = true;

    protected override Type StyleKeyOverride => typeof(TextPresenter);

    // as Avalonia's MathUtilities (internal): the same tolerance, so the constraint is the one the base keeps
    private static bool Close(double a, double b) => a == b || Math.Abs(a - b) < (Math.Abs(a) + Math.Abs(b) + 10.0) * 2.2204460492503131E-016;
    private static bool Zero(double v) => Math.Abs(v) < 10.0 * 2.2204460492503131E-016;

    protected override Size MeasureOverride(Size availableSize)
    {
        _constraint = availableSize;
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // the same new constraint the base takes when the width changed (TextPresenter.ArrangeOverride)
        if (!Close(_constraint.Width, finalSize.Width)) _constraint = new Size(Math.Ceiling(finalSize.Width), double.PositiveInfinity);
        return base.ArrangeOverride(finalSize);
    }

    protected override TextLayout CreateTextLayout()
    {
        var text = Text;
        // the keyboard's word being composed keeps the platform's underline (the base draws it)
        if (!Formatted || string.IsNullOrEmpty(text) || !string.IsNullOrEmpty(PreeditText) || PasswordChar != default) return base.CreateTextLayout();
        var spans = MarkdownSyntax.Spans(text);
        if (spans.Count == 0) return base.CreateTextLayout();
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var overrides = new List<Avalonia.Utilities.ValueSpan<TextRunProperties>>(spans.Count);
        foreach (var s in spans) overrides.Add(new Avalonia.Utilities.ValueSpan<TextRunProperties>(s.Start, s.Length, Properties(s.Style, typeface)));
        var maxWidth = Zero(_constraint.Width) ? double.PositiveInfinity : _constraint.Width;
        var maxHeight = Zero(_constraint.Height) ? double.PositiveInfinity : _constraint.Height;
        return new TextLayout(text, typeface, FontSize, Foreground, TextAlignment, TextWrapping, null, null, FlowDirection,
            maxWidth, maxHeight, LineHeight, LetterSpacing, 0, FontFeatures, overrides);
    }

    private TextRunProperties Properties(MarkdownSyntax.Style style, Typeface basis)
    {
        var weight = style.HasFlag(MarkdownSyntax.Style.Bold) ? FontWeight.Bold : basis.Weight;
        var slant = style.HasFlag(MarkdownSyntax.Style.Italic) || style.HasFlag(MarkdownSyntax.Style.Comment) ? FontStyle.Italic : basis.Style;
        var family = style.HasFlag(MarkdownSyntax.Style.Code) ? UrbeTheme.MonoFont : basis.FontFamily;
        var typeface = new Typeface(family, slant, weight, basis.Stretch);
        IBrush? ink = Foreground;
        if (style.HasFlag(MarkdownSyntax.Style.Link) || style.HasFlag(MarkdownSyntax.Style.Tag) || style.HasFlag(MarkdownSyntax.Style.Bullet)) ink = UrbeTheme.Brush(UrbeTheme.Accent);
        if (style.HasFlag(MarkdownSyntax.Style.Math)) ink = UrbeTheme.Brush(UrbeTheme.Accent, .85);
        if (style.HasFlag(MarkdownSyntax.Style.Comment)) ink = UrbeTheme.Brush(UrbeTheme.Text3);
        if (style.HasFlag(MarkdownSyntax.Style.Mark)) ink = UrbeTheme.Brush(UrbeTheme.Text3, .8);
        IBrush? back = style.HasFlag(MarkdownSyntax.Style.Code) ? UrbeTheme.Brush(UrbeTheme.Surface2)
            : style.HasFlag(MarkdownSyntax.Style.Highlight) ? UrbeTheme.Brush(UrbeTheme.Warn, .28) : null;
        TextDecorationCollection? deco = null;
        if (style.HasFlag(MarkdownSyntax.Style.Strike) && !style.HasFlag(MarkdownSyntax.Style.Mark)) deco = TextDecorations.Strikethrough;
        else if (style.HasFlag(MarkdownSyntax.Style.Link) && !style.HasFlag(MarkdownSyntax.Style.Mark)) deco = TextDecorations.Underline;
        var size = style.HasFlag(MarkdownSyntax.Style.Code) ? FontSize * .92 : FontSize;
        return new GenericTextRunProperties(typeface, size, deco, ink, back, fontFeatures: FontFeatures);
    }

    /// <summary>
    /// A text box that draws its text with a <see cref="MarkdownTextPresenter"/>: the same parts as Fluent's (a scroll
    /// viewer around the presenter, both named as TextBox asks), bound to the box the same way.
    /// </summary>
    public static FuncControlTemplate<TextBox> Template(Func<bool> formatted) => new((box, scope) =>
    {
        var presenter = new MarkdownTextPresenter { Name = "PART_TextPresenter", Formatted = formatted() };
        presenter[!!TextPresenter.TextProperty] = box[!!TextBox.TextProperty];
        presenter[!TextPresenter.CaretIndexProperty] = box[!TextBox.CaretIndexProperty];
        presenter[!TextPresenter.SelectionStartProperty] = box[!TextBox.SelectionStartProperty];
        presenter[!TextPresenter.SelectionEndProperty] = box[!TextBox.SelectionEndProperty];
        presenter[!TextPresenter.CaretBlinkIntervalProperty] = box[!TextBox.CaretBlinkIntervalProperty];
        presenter[!TextPresenter.TextAlignmentProperty] = box[!TextBox.TextAlignmentProperty];
        presenter[!TextPresenter.TextWrappingProperty] = box[!TextBox.TextWrappingProperty];
        presenter[!TextPresenter.LineHeightProperty] = box[!TextBox.LineHeightProperty];
        presenter[!TextPresenter.LetterSpacingProperty] = box[!TextBox.LetterSpacingProperty];
        presenter[!TextPresenter.PasswordCharProperty] = box[!TextBox.PasswordCharProperty];
        presenter[!TextPresenter.RevealPasswordProperty] = box[!TextBox.RevealPasswordProperty];
        presenter[!TextPresenter.SelectionBrushProperty] = box[!TextBox.SelectionBrushProperty];
        presenter[!TextPresenter.SelectionForegroundBrushProperty] = box[!TextBox.SelectionForegroundBrushProperty];
        presenter[!TextPresenter.CaretBrushProperty] = box[!TextBox.CaretBrushProperty];
        presenter[!Layoutable.HorizontalAlignmentProperty] = box[!ContentControl.HorizontalContentAlignmentProperty];
        presenter[!Layoutable.VerticalAlignmentProperty] = box[!ContentControl.VerticalContentAlignmentProperty];
        scope.Register(presenter.Name, presenter);
        var scroll = new ScrollViewer
        {
            Name = "PART_ScrollViewer", Content = presenter,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            BringIntoViewOnFocusChange = false
        };
        scope.Register(scroll.Name, scroll);
        var border = new Border
        {
            [!Border.BackgroundProperty] = box[!TemplatedControl.BackgroundProperty],
            [!Border.BorderBrushProperty] = box[!TemplatedControl.BorderBrushProperty],
            [!Border.BorderThicknessProperty] = box[!TemplatedControl.BorderThicknessProperty],
            [!Border.CornerRadiusProperty] = box[!TemplatedControl.CornerRadiusProperty],
            [!Decorator.PaddingProperty] = box[!TemplatedControl.PaddingProperty],
            Child = scroll
        };
        return border;
    });
}
