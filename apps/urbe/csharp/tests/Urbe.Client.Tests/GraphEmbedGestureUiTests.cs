using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-067: "grafo incorporado é navegável e sua interação não bloqueia a edição/rolagem da nota". A graph in a note
/// works like a map on a web page (cooperative gestures): one finger and the wheel scroll the note; two fingers, Ctrl
/// with the wheel, or the mouse dragging move and zoom the graph; a tap on a card opens what it points to.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task GraphEmbed_ScrollsTheNoteWithOneFingerAndMovesWithTwo() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var graph = GraphDocument.Create("Mapa");
        var card = graph.AddReference(GraphNodeKind.Note, "Plano.md", 0, 0);
        graph.AddText("Outra coisa", 320, 160);
        var canvas = new GraphCanvasView { Height = 300, Cooperative = true };
        canvas.Show(graph, readOnly: true);
        GraphNode? opened = null;
        canvas.NodeOpened += n => opened = n;
        var scroll = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 500 }, canvas, new Border { Height = 1500 } } } };
        var window = new Window { Width = 412, Height = 760, Content = scroll };
        window.Show();
        try
        {
            scroll.Offset = new Vector(0, 300);
            for (int i = 0; i < 4; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            var middle = canvas.TranslatePoint(new Point(canvas.Bounds.Width / 2, 150), window)!.Value;
            double zoom = canvas.Zoom;
            var origin = canvas.Origin;

            // the wheel scrolls the note; Ctrl with the wheel zooms the graph
            window.MouseWheel(middle, new Vector(0, -1));
            Dispatcher.UIThread.RunJobs();
            Assert.True(scroll.Offset.Y > 300, "a nota rolou: " + scroll.Offset.Y);
            Assert.Equal(zoom, canvas.Zoom);
            var offset = scroll.Offset.Y;
            middle = canvas.TranslatePoint(new Point(canvas.Bounds.Width / 2, 150), window)!.Value;
            window.MouseWheel(middle, new Vector(0, 1), RawInputModifiers.Control);
            Dispatcher.UIThread.RunJobs();
            Assert.NotEqual(zoom, canvas.Zoom);
            Assert.Equal(offset, scroll.Offset.Y);

            // one finger: the graph does not move under it (the note's scroll takes it), and a hint says how
            origin = canvas.Origin;
            var noteAt = scroll.Offset.Y;
            var finger = window.TouchBegin(middle, RawInputModifiers.None);
            for (int k = 1; k <= 8; k++) window.TouchMove(finger, middle + new Point(0, -12 * k), RawInputModifiers.None);
            window.TouchEnd(finger, middle + new Point(0, -96), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(origin, canvas.Origin);
            Assert.True(scroll.Offset.Y > noteAt, "a nota rolou com um dedo: " + noteAt + " → " + scroll.Offset.Y);
            Assert.True(canvas.HintShown);

            // a tap on the card opens its note (once the note's scroll has stopped: a tap during a fling only stops it)
            for (int k = 0; k < 40; k++) { await Task.Delay(25); Dispatcher.UIThread.RunJobs(); }
            scroll.Offset = new Vector(0, 300);   // the graph back on screen
            for (int k = 0; k < 4; k++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            var at = canvas.ScreenOf(card);
            var tap = canvas.TranslatePoint(at, window)!.Value;
            var touch = window.TouchBegin(tap, RawInputModifiers.None);
            window.TouchEnd(touch, tap, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(card.Id, opened?.Id);
            // two fingers move it
            middle = canvas.TranslatePoint(new Point(canvas.Bounds.Width / 2, 150), window)!.Value;
            var one = window.TouchBegin(middle, RawInputModifiers.None);
            var two = window.TouchBegin(middle + new Point(60, 0), RawInputModifiers.None);
            window.TouchMove(one, middle + new Point(-50, 30), RawInputModifiers.None);
            window.TouchMove(two, middle + new Point(10, 30), RawInputModifiers.None);
            window.TouchEnd(one, middle + new Point(-50, 30), RawInputModifiers.None);
            window.TouchEnd(two, middle + new Point(10, 30), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.NotEqual(origin, canvas.Origin);

            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
