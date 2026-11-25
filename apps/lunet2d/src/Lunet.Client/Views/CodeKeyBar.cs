using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Shell;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// The code bar above the phone's keyboard: ten 48 dp keys, each with a symbol on tap and more by swiping toward a
/// side (hints in the corners). Keys never take focus, so the keyboard stays open while you use them.
/// </summary>
public sealed class CodeKeyBar : UserControl
{
    private readonly Func<TextBox?> _target;

    public CodeKeyBar(Func<TextBox?> target)
    {
        _target = target;
        Focusable = false;
        var grid = new Avalonia.Controls.Primitives.UniformGrid { Rows = 1, Columns = CodeKeys.Bar.Count };
        foreach (var key in CodeKeys.Bar) grid.Children.Add(Key(key));
        Content = new Border
        {
            Child = grid, Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(2),
        };
        AutomationProperties.SetName(this, "Teclas de código");
    }

    /// <summary>Types a symbol (or moves the caret) in the target editor, as one undo step. Public for tests and hardware shortcuts.</summary>
    public bool Type(string symbol)
    {
        if (_target() is not { } box) return false;
        var text = box.Text ?? "";
        if (CodeKeys.Apply(text, box.SelectionStart, box.SelectionEnd, symbol) is { } edit)
        {
            box.SelectionStart = edit.Start;
            box.SelectionEnd = edit.End;
            box.SelectedText = edit.Insert;
            box.SelectionStart = box.SelectionEnd = edit.Caret;
            box.CaretIndex = edit.Caret;
        }
        else
        {
            var caret = CodeKeys.Move(text, box.CaretIndex, symbol);
            box.SelectionStart = box.SelectionEnd = caret;
            box.CaretIndex = caret;
        }
        return true;
    }

    private Control Key(CodeKey key)
    {
        var face = new Grid { Height = LunetTokens.MinTouch };
        face.Children.Add(new TextBlock
        {
            Text = key.Tap, FontFamily = LunetTokens.MonoFont, FontSize = 17, FontWeight = FontWeight.SemiBold,
            Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        void Hint(string? s, HorizontalAlignment h, VerticalAlignment v)
        {
            if (s is null) return;
            face.Children.Add(new TextBlock
            {
                Text = s, FontFamily = LunetTokens.MonoFont, FontSize = 10,
                Foreground = LunetTokens.Brush(LunetTokens.Color.TextTertiary),
                HorizontalAlignment = h, VerticalAlignment = v, Margin = new Thickness(3, 1),
            });
        }
        Hint(key.Up, HorizontalAlignment.Center, VerticalAlignment.Top);
        Hint(key.Down, HorizontalAlignment.Center, VerticalAlignment.Bottom);
        Hint(key.Left, HorizontalAlignment.Left, VerticalAlignment.Center);
        Hint(key.Right, HorizontalAlignment.Right, VerticalAlignment.Center);

        var border = new Border
        {
            Child = face, Margin = new Thickness(2), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
            Background = LunetTokens.Brush(LunetTokens.Color.SurfaceOverlay), Focusable = false,
        };
        AutomationProperties.SetName(border, key.Tap);
        Point? origin = null;
        border.PointerPressed += (_, e) =>
        {
            origin = e.GetPosition(border);
            e.Pointer.Capture(border);
            border.Background = LunetTokens.Brush(LunetTokens.Color.BorderStrong);
            e.Handled = true;
        };
        border.PointerReleased += (_, e) =>
        {
            border.Background = LunetTokens.Brush(LunetTokens.Color.SurfaceOverlay);
            if (origin is not { } o) return;
            var p = e.GetPosition(border);
            origin = null;
            e.Pointer.Capture(null);
            Type(key.For(CodeKeys.Resolve(p.X - o.X, p.Y - o.Y)));
            e.Handled = true;
        };
        border.PointerCaptureLost += (_, _) => { origin = null; border.Background = LunetTokens.Brush(LunetTokens.Color.SurfaceOverlay); };
        return border;
    }
}
