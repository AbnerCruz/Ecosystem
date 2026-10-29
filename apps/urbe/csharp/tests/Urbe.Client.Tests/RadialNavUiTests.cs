using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Client.Ui;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-043 (FeaturesCheck: "substituir as abas por um menu radial no canto inferior direito"): on a phone one round
/// button in the bottom right corner takes the place of the tab bar and of the separate Assistente button; it opens
/// Assistente, Notas and Grafos on a quarter arc, each with its name, and Voltar or a touch outside closes it. A
/// press-drag-release picks an item without the menu stopping open. Wide windows keep their rail.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static string Name(Control c) => Avalonia.Automation.AutomationProperties.GetName(c) ?? "";

    [Fact]
    public async Task RadialNav_ReplacesTheTabsOnPhone() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 360, Height = 760, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            var nav = view.Navigation;
            // no tab bar: Notas/Grafos are not on the screen until the menu opens
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBlock>(),
                t => t.Text is "Notas" or "Grafos" && t.IsEffectivelyVisible && t.FindAncestorOfType<WorkspaceView>() is null);
            Assert.False(view.AssistantLauncher.IsEffectivelyVisible);   // one round button, not two
            var trigger = nav.Trigger;
            Assert.True(trigger.IsEffectivelyVisible);
            Assert.Equal(56, trigger.Bounds.Width);
            Assert.Equal(56, trigger.Bounds.Height);
            var center = trigger.TranslatePoint(new Point(28, 28), view)!.Value;
            Assert.InRange(center.X, 300, 330);   // the bottom right corner, a 16 dp gutter
            Assert.InRange(center.Y, 690, 720);
            Assert.Equal("Menu: Assistente, Notas e Grafos", Name(trigger));
            Snapshot(window, "native-radial-closed.png");

            trigger.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settle(window, view);
            Assert.True(nav.IsOpen);
            Assert.Equal("Fechar menu", Name(trigger));
            var items = nav.Items;
            Assert.Equal(["Assistente", "Notas", "Grafos"], items.Select(Name));
            var rects = new List<Rect>();
            foreach (var item in items)
            {
                Assert.True(item.IsEffectivelyVisible);
                var circle = nav.CircleOf(item);
                Assert.True(circle.Bounds.Width >= UrbeTokens.MinTouch && circle.Bounds.Height >= UrbeTokens.MinTouch);
                var r = new Rect(item.TranslatePoint(new Point(0, 0), view)!.Value, item.Bounds.Size);
                Assert.True(r.X >= 0 && r.Right <= 360 && r.Y >= 0 && r.Bottom <= 760, Name(item) + " fora da tela: " + r);
                Assert.DoesNotContain(rects, o => o.Intersects(r));   // labels never cover another item
                rects.Add(r);
                Assert.Contains(item.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == Name(item) && t.IsEffectivelyVisible);
            }
            Assert.True(nav.IsActive("Notas"));
            Assert.Same(items[0], TopLevel.GetTopLevel(view)!.FocusManager!.GetFocusedElement());   // keyboard and TalkBack start on the menu
            Snapshot(window, "native-radial-open.png");

            // Voltar closes the menu first, and nothing else
            Assert.True(view.HandleSystemBack());
            Assert.False(nav.IsOpen);
            Assert.False(items[0].IsVisible);

            // a touch outside closes it too
            nav.Open();
            nav.Scrim.RaiseEvent(new PointerPressedEventArgs(nav.Scrim, new Pointer(1, PointerType.Touch, true), window,
                new Point(10, 10), 0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
            Assert.False(nav.IsOpen);

            // Grafos: the graphs' chooser; Notas: back to the Área de trabalho
            nav.Open();
            items[2].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settle(window, view);
            Assert.False(nav.IsOpen);
            Assert.True(view.Dialogs.IsOpen);
            view.Dialogs.Cancel();
            await Settle(window, view);

            // Assistente: the panel opens and the round button gives it the corner
            nav.Open();
            items[0].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settle(window, view);
            Assert.True(view.Assistant.IsVisible);
            Assert.False(trigger.IsEffectivelyVisible);
            view.Assistant.Close();
            await Settle(window, view);
            Assert.True(trigger.IsEffectivelyVisible);
            Assert.False(view.AssistantLauncher.IsEffectivelyVisible);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Fact]
    public async Task RadialNav_PressDragReleasePicksWithoutStoppingOpen() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 360, Height = 760, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            var nav = view.Navigation;
            var start = nav.Trigger.TranslatePoint(new Point(28, 28), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(-8, -6));          // a wobble is still a tap
            Assert.False(nav.IsOpen);
            window.MouseMove(start + new Point(-40, -10));        // a drag opens the menu under the finger
            await Settle(window, view);
            Assert.True(nav.IsOpen);
            Assert.Equal("Grafos", nav.Highlighted);
            window.MouseMove(start + new Point(-6, -90));         // up: the Assistente
            Assert.Equal("Assistente", nav.Highlighted);
            window.MouseUp(start + new Point(-6, -90), MouseButton.Left);
            await Settle(window, view);
            Assert.False(nav.IsOpen);
            Assert.True(view.Assistant.IsVisible);
            view.Assistant.Close();
            await Settle(window, view);

            // released back on the button: nothing chosen, the menu closes
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(-60, -60));
            Assert.Equal("Notas", nav.Highlighted);
            window.MouseMove(start + new Point(4, 4));
            Assert.Null(nav.Highlighted);
            window.MouseUp(start + new Point(4, 4), MouseButton.Left);
            await Settle(window, view);
            Assert.False(nav.IsOpen);
            Assert.False(view.Assistant.IsVisible);
            Assert.False(view.Dialogs.IsOpen);

            // dragged far past the arc: a way out, nothing chosen
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(start + new Point(-60, -60));
            window.MouseMove(new Point(2, 300));
            Assert.Null(nav.Highlighted);
            window.MouseUp(new Point(2, 300), MouseButton.Left);
            await Settle(window, view);
            Assert.False(nav.IsOpen);
            Assert.False(view.Explorer.Overlaid);
            Assert.False(view.Dialogs.IsOpen);

            // a plain tap opens and keeps it open (for those who look before choosing)
            window.MouseDown(start, MouseButton.Left);
            window.MouseUp(start, MouseButton.Left);
            await Settle(window, view);
            Assert.True(nav.IsOpen);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Fact]
    public async Task RadialNav_WorksOverTheEditorAndStaysOffWideWindows() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 360, Height = 760, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            view.Editor.Open(new CityNote("Tutorial/Comece aqui.md", TutorialNotes.Load().First(n => n.Path == "Tutorial/Comece aqui.md").Content));
            await Settle(window, view);
            var nav = view.Navigation;
            Assert.True(nav.Trigger.IsEffectivelyVisible);
            nav.Open();
            nav.Items.First(i => Name(i) == "Notas").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settle(window, view);
            Assert.True(view.NotesOverEditor);          // the notes come over the note, which stays open behind
            Assert.True(view.Editor.IsVisible);
            Assert.True(view.HandleSystemBack());
            Assert.False(view.NotesOverEditor);

            window.Width = 1100;
            await Settle(window, view);
            Assert.False(nav.IsEffectivelyVisible);
            Assert.True(view.AssistantLauncher.IsEffectivelyVisible);
            Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Grafos" && t.IsEffectivelyVisible);   // the rail
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
