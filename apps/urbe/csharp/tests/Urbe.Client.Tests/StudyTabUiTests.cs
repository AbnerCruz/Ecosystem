using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// Estudo (owner, 04/11): one screen for research → sources → writing. The radial's Estudo opens the note with the
    /// source pane; a site opens beside it; the grip moves the line between source and note; a link of the note opens
    /// in the pane, not over the note.
    /// </summary>
    [Fact]
    public async Task Study_TheSourceAndTheNoteShareTheScreenAndTheGripMovesTheLine() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var old = StudyWebViews.CreateBrowser;
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            FakeStudyWeb? browser = null;
            StudyWebViews.CreateBrowser = address => { browser = new FakeStudyWeb(); browser.Navigate(address); return browser; };
            window.Width = 412; window.Height = 860;
            await Settle(window, view);
            await view.StartStudyAsync();
            await Settle(window, view);
            Assert.True(view.Editor.IsVisible);
            Assert.True(view.StudyOpen);
            Assert.Equal("Estudo", view.ActiveDestination);
            // phone: the source above the note, about half of it
            var pane = view.Editor.Pinned;
            var body = view.Editor.Surface.FindLogicalAncestorOfType<ScrollViewer>()!;
            Assert.True(pane.TranslatePoint(default, window)!.Value.Y < body.TranslatePoint(default, window)!.Value.Y);
            var before = pane.Bounds.Height;
            Assert.InRange(before, 250, 450);

            // a site beside the note
            await pane.OpenWebAsync("https://example.org/aula");
            await Settle(window, view);
            Assert.True(pane.ShowsWeb);
            Assert.Equal("https://example.org/aula", browser!.Address);
            Snapshot(window, "native-study-phone.png");

            // the grip: dragging it down gives the source more room
            var grip = view.Editor.GetLogicalDescendants().OfType<Border>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Divisória entre a fonte e a nota");
            var at = grip.TranslatePoint(new Point(grip.Bounds.Width / 2, grip.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, MouseButton.Left);
            window.MouseMove(at + new Point(0, 120));
            window.MouseUp(at + new Point(0, 120), MouseButton.Left);
            await Settle(window, view);
            Assert.True(pane.Bounds.Height > before + 60, before + " → " + pane.Bounds.Height);

            // a link of the note opens in the pane
            await view.Editor.FollowLinkAsync("https://example.org/outra");
            await Settle(window, view);
            Assert.Equal("https://example.org/outra", browser.Address);
            Assert.False(view.StudyBrowser.IsVisible);

            // wide: the source on the left of the note
            window.Width = 1280; window.Height = 800;
            await Settle(window, view);
            Assert.True(view.Editor.SourceBeside);
            Assert.True(pane.TranslatePoint(default, window)!.Value.X < body.TranslatePoint(default, window)!.Value.X);
            Snapshot(window, "native-study-wide.png");
            return true;
        }
        finally { StudyWebViews.CreateBrowser = old; window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
