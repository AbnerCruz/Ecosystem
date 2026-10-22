using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Owner's print (UC-48): on the phone each destination of the dock sat at the left of its cell; Notas and Assistente are centred in their halves.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Dock_EachDestinationIsCentredInItsHalfOnAPhone() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 400, Height = 860, Content = view };
            window.Show();
            try
            {
                await Pump(200);
                foreach (var (label, column) in new[] { ("Notas", 0), ("Assistente", 1) })
                {
                    var text = view.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Text == label && t.IsEffectivelyVisible)
                        .First(t => t.FindLogicalAncestorOfType<Button>() is { } b && Grid.GetColumn(b) == column);
                    var button = text.FindLogicalAncestorOfType<Button>()!;
                    Assert.InRange(button.Bounds.Width, 400 / 2.0 - 2, 400 / 2.0 + 2);
                    var centre = text.TranslatePoint(new Point(text.Bounds.Width / 2, 0), view)!.Value.X;
                    Assert.InRange(centre, 400 / 4.0 * (2 * column + 1) - 3, 400 / 4.0 * (2 * column + 1) + 3);
                }
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
