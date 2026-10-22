using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// Observation of 22/10/2026 on the phone: scrolling selected things and opened the keyboard, and a selected word had
/// no handles. A touch that scrolls, or only stops a scroll, is not a tap nor a hold on what is under the finger.
/// </summary>
public sealed class TouchIntentTests
{
    [Fact]
    public void ATouchWhileTheContentStillGlidesOnlyStopsIt()
    {
        var intent = new TouchIntent();
        intent.Moved(1000);
        intent.Press(touch: true, offset: 300, nowMs: 1000 + TouchIntent.SettleMs - 10);
        Assert.True(intent.StoppedScroll);
        Assert.False(intent.Allows(300));
        // a touch once it has stopped is for the item
        intent.Press(touch: true, offset: 300, nowMs: 1000 + TouchIntent.SettleMs + 50);
        Assert.False(intent.StoppedScroll);
        Assert.True(intent.Allows(300));
    }

    [Fact]
    public void ATouchThatScrolledIsNotForTheItem()
    {
        var intent = new TouchIntent();
        intent.Press(touch: true, offset: 100, nowMs: 5000);
        Assert.True(intent.Allows(100 + TouchIntent.Slop - 1));
        Assert.False(intent.Allows(100 + TouchIntent.Slop + 1));
        Assert.False(intent.Allows(100 - 40));
    }

    [Fact]
    public void TheMouseNeverStopsAScroll()
    {
        var intent = new TouchIntent();
        intent.Moved(1000);
        intent.Press(touch: false, offset: 0, nowMs: 1001);
        Assert.True(intent.Allows(0));
    }

    [Fact]
    public void AWindowTheSystemShrinksForTheKeyboardIsNeverPushedUp()
    {
        // Android up to 14 (adjustResize, not edge-to-edge): the black band between the app and the keys
        Assert.Equal(0, MainView.KeyboardCover(2000, 1300, windowShrinks: true));
        Assert.Equal(700, MainView.KeyboardCover(2000, 1300, windowShrinks: false));
    }

    [Fact]
    public void TheTextBoxSelectionHandlesAreReachable() =>
        // fails if an Avalonia update renames the internal calls behind the handles
        Assert.NotNull(TouchTextScroll.HandleApi);
}

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Touch_AScrollNeverHoldsNorOpensARowOfTheExplorer() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-touch-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            for (int i = 0; i < 40; i++) File.WriteAllText(Path.Combine(vault, $"Nota {i:00}.md"), $"# Nota {i:00}\n");
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                var e = view.Explorer;
                // long enough that the scroll below always comes first on a loaded machine
                e.HoldDelay = TimeSpan.FromMilliseconds(300);
                var scroll = e.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Extent.Height > s.Viewport.Height);
                Control RowOf(string text) => e.GetVisualDescendants().OfType<Button>()
                    .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == text) && b.MinHeight == 48);
                Point Centre(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;

                // a finger on a row that then scrolls the list (the row moves with it): no hold, no open
                var at = Centre(RowOf("Nota 03.md"));
                window.MouseDown(at, MouseButton.Left);
                scroll.Offset = new Vector(0, scroll.Offset.Y + 120);
                Dispatcher.UIThread.RunJobs();
                await Pump(450);
                Assert.Empty(e.SelectedPaths);
                window.MouseUp(at, MouseButton.Left);
                await Pump(60);
                Assert.False(view.Editor.IsVisible);

                // a still finger holds, as before
                scroll.Offset = default;
                await Pump(30);
                at = Centre(RowOf("Nota 03.md"));
                window.MouseDown(at, MouseButton.Left);
                await Pump(450);
                Assert.Equal(["Nota 03.md"], e.SelectedPaths);
                window.MouseUp(at, MouseButton.Left);
                window.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Touch_AScrollOverABlockDoesNotOpenItForEditing() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            var text = "# Longa\n\n" + string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"Parágrafo {i} com algumas palavras para rolar."));
            await view.LoadAsync([new Urbe.Client.CityNote("Longa.md", text)]);
            await Settle(window, view);
            view.OpenNoteByPath("Longa.md");
            Dispatcher.UIThread.RunJobs();
            await Pump(60);
            var surface = view.Editor.Surface;
            Assert.False(surface.IsEditing);
            var scroll = surface.GetVisualAncestors().OfType<ScrollViewer>().First();
            var holder = surface.Children.OfType<Border>().Skip(3).First();
            var at = holder.TranslatePoint(new Point(20, 10), window)!.Value;

            // pressed, then the note scrolled under the finger: the tap that follows is not for the block
            window.MouseDown(at, MouseButton.Left);
            scroll.Offset = new Vector(0, scroll.Offset.Y + 80);
            Dispatcher.UIThread.RunJobs();
            holder.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, PointerArgs(holder)));
            window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(surface.IsEditing);

            // a real tap (nothing scrolled) opens it
            scroll.Offset = default;
            await Pump(30);
            at = holder.TranslatePoint(new Point(20, 10), window)!.Value;
            window.MouseDown(at, MouseButton.Left);
            holder.RaiseEvent(new TappedEventArgs(InputElement.TappedEvent, PointerArgs(holder)));
            window.MouseUp(at, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(surface.IsEditing);
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task Touch_ASelectedWordShowsTheSelectionHandles() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync([new Urbe.Client.CityNote("Nota.md", "# Nota\n\nUma frase com várias palavras.\n")]);
            await Settle(window, view);
            view.OpenNoteByPath("Nota.md");
            view.Editor.SetMode(EditorMode.Fonte);
            Dispatcher.UIThread.RunJobs();
            await Pump(60);
            var box = view.Editor.Editor;
            box.Focus();
            box.SelectionStart = 12;
            box.SelectionEnd = 17;
            Assert.True(TouchTextScroll.ShowSelectionHandles(box));
            // no selection, nothing to show
            box.SelectionEnd = box.SelectionStart;
            Assert.False(TouchTextScroll.ShowSelectionHandles(box));
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task Touch_ASlowFingerScrollOverTheExplorerSelectsNothing() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-touch2-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            for (int i = 0; i < 40; i++) File.WriteAllText(Path.Combine(vault, $"Nota {i:00}.md"), $"# Nota {i:00}\n");
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                var e = view.Explorer;
                e.HoldDelay = TimeSpan.FromMilliseconds(250);
                var scroll = e.GetVisualDescendants().OfType<ScrollViewer>().First(s => s.Extent.Height > s.Viewport.Height);
                var row = e.GetVisualDescendants().OfType<Button>()
                    .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Nota 05.md") && b.MinHeight == 48);
                var at = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
                // a slow scroll: the finger moves up a little at a time, longer than a hold
                var finger = window.TouchBegin(at, RawInputModifiers.None);
                for (int i = 1; i <= 10; i++)
                {
                    window.TouchMove(finger, new Point(at.X, at.Y - i * 15), RawInputModifiers.None);
                    await Pump(40);
                }
                Assert.True(scroll.Offset.Y > 40, "rolou " + scroll.Offset.Y);
                Assert.Empty(e.SelectedPaths);
                window.TouchEnd(finger, new Point(at.X, at.Y - 150), RawInputModifiers.None);
                await Pump(60);
                Assert.Empty(e.SelectedPaths);
                Assert.False(view.Editor.IsVisible);
                window.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task Touch_ASlowFingerScrollOverABlockNeitherSelectsNorEdits() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            var text = "# Longa\n\n" + string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"Parágrafo {i} com algumas palavras para rolar."));
            await view.LoadAsync([new Urbe.Client.CityNote("Longa.md", text)]);
            await Settle(window, view);
            view.OpenNoteByPath("Longa.md");
            Dispatcher.UIThread.RunJobs();
            await Pump(60);
            var surface = view.Editor.Surface;
            var scroll = surface.GetVisualAncestors().OfType<ScrollViewer>().First();
            var holder = surface.Children.OfType<Border>().Skip(4).First();
            var at = holder.TranslatePoint(new Point(40, 10), window)!.Value;
            var finger = window.TouchBegin(at, RawInputModifiers.None);
            for (int i = 1; i <= 12; i++)
            {
                window.TouchMove(finger, new Point(at.X, at.Y - i * 12), RawInputModifiers.None);
                await Pump(50);   // well past the hold time, while the note scrolls under the finger
            }
            Assert.True(scroll.Offset.Y > 40, "rolou " + scroll.Offset.Y);
            Assert.False(surface.IsEditing);
            window.TouchEnd(finger, new Point(at.X, at.Y - 144), RawInputModifiers.None);
            await Pump(60);
            Assert.False(surface.IsEditing);
            window.Close();
            return true;
        }, CancellationToken.None));

    [Fact]
    public async Task Touch_ATapThatStopsTheGlideDoesNotOpenABlock() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            var text = "# Longa\n\n" + string.Join("\n\n", Enumerable.Range(1, 80).Select(i => $"Parágrafo {i} com algumas palavras para rolar."));
            await view.LoadAsync([new Urbe.Client.CityNote("Longa.md", text)]);
            await Settle(window, view);
            view.OpenNoteByPath("Longa.md");
            Dispatcher.UIThread.RunJobs();
            await Pump(60);
            var surface = view.Editor.Surface;
            var scroll = surface.GetVisualAncestors().OfType<ScrollViewer>().First();
            // a quick flick: the note keeps gliding after the finger leaves
            var start = new Point(200, 700);
            var finger = window.TouchBegin(start, RawInputModifiers.None);
            for (int i = 1; i <= 6; i++) { window.TouchMove(finger, new Point(200, 700 - i * 60), RawInputModifiers.None); await Task.Delay(8); }
            window.TouchEnd(finger, new Point(200, 340), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
            var gliding = scroll.Offset.Y;
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
            Assert.True(scroll.Offset.Y > gliding, $"a nota não deslizou ({gliding} → {scroll.Offset.Y})");
            // a tap while it glides only stops it: the block under the finger does not open, no keyboard
            var tap = window.TouchBegin(new Point(200, 400), RawInputModifiers.None);
            window.TouchEnd(tap, new Point(200, 400), RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            await Pump(40);
            Assert.False(surface.IsEditing);
            window.Close();
            return true;
        }, CancellationToken.None));

    private static PointerEventArgs PointerArgs(Visual source)
    {
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
        return new PointerEventArgs(InputElement.PointerMovedEvent, source, pointer, (Visual)TopLevel.GetTopLevel(source)!, default, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None);
    }
}
