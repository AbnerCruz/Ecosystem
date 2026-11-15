using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Avalonia.Input.Platform;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>Select, copy, cut, paste and select all — on the phone and with a mouse, across the app (15/11/2026).</summary>
public sealed class SelectionClipboardUiTests
{
    private static Button BubbleButton(EditorOverlay editor, string name) =>
        editor.Surface.Bubble.GetLogicalDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == name);

    private static void Press(Button b) { b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs(); }

    private static async Task<string?> Clipboard(Window window) =>
        await (TopLevel.GetTopLevel(window)!.Clipboard!).TryGetTextAsync();

    [Fact]
    public async Task AFingerHeldOnAWordInVisualSelectsItAndTheBubbleCopiesCutsAndPastes() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(async () =>
        {
            var (editor, window) = EditorFlowUiTests.Open("Primeira palavra aqui.\n\nSegundo bloco.\n");
            var surface = editor.Surface;
            var holder = surface.BlockView(surface.BlockStarts[0])!;
            var at = EditorFlowUiTests.PointOf(window, holder, "palavra", 2);
            var local = window.TranslatePoint(at, holder)!.Value;
            // the long press (the holder's Holding handler goes here)
            surface.TapBlock(surface.BlockStarts[0], holder, holder.InputHitTest(local) as Visual, local, select: true);
            Dispatcher.UIThread.RunJobs();
            var box = surface.EditBox!;
            Assert.Equal("palavra", box.SelectedText);
            Assert.True(surface.Bubble.IsVisible);
            foreach (var name in new[] { "Copiar", "Recortar", "Colar", "Selecionar tudo" })
                Assert.True(BubbleButton(editor, name).IsVisible, name);
            Press(BubbleButton(editor, "Copiar"));
            Assert.Equal("palavra", await Clipboard(window));
            // cut takes it out of the note
            box.SelectionStart = 0; box.SelectionEnd = "Primeira ".Length;
            Dispatcher.UIThread.RunJobs();
            Press(BubbleButton(editor, "Recortar"));
            Assert.Equal("Primeira ", await Clipboard(window));
            Assert.StartsWith("palavra aqui.", editor.Editor.Text!.Replace("\r\n", "\n"));
            // paste at the end of the block
            box.SelectionStart = box.SelectionEnd = box.Text!.Length;
            box.RaiseEvent(new ContextRequestedEventArgs());
            Dispatcher.UIThread.RunJobs();
            Assert.True(surface.Bubble.IsVisible);
            Assert.False(BubbleButton(editor, "Negrito").IsVisible);   // only the clipboard with nothing selected
            Assert.False(BubbleButton(editor, "Copiar").IsVisible);
            Press(BubbleButton(editor, "Colar"));
            for (int i = 0; i < 5; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.StartsWith("palavra aqui.Primeira \n\nSegundo", editor.Editor.Text!.Replace("\r\n", "\n"));
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task SelectAllInTheBubbleSelectsTheBlockThenTheWholeNote() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = EditorFlowUiTests.Open("Um bloco.\n\nDois blocos.\n");
            var box = EditorFlowUiTests.Edit(editor, 1);
            box.SelectionStart = 0; box.SelectionEnd = 4;
            Dispatcher.UIThread.RunJobs();
            Press(BubbleButton(editor, "Selecionar tudo"));
            Assert.Equal("Dois blocos.", editor.Surface.EditBox!.SelectedText);
            Press(BubbleButton(editor, "Selecionar tudo"));
            Assert.Equal("Um bloco.\n\nDois blocos.", editor.Surface.EditBox!.SelectedText);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task EveryTextFieldOfTheAppHasThePortugueseMenuWithSelectAll() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Copiar", Application.Current!.Resources["StringTextFlyoutCopyText"]);
            Assert.Equal("Colar", Application.Current!.Resources["StringTextFlyoutPasteText"]);
            var field = new TextBox { Text = "algum texto" };
            var host = new Window { Content = field };
            host.Show();
            field.Focus();
            Dispatcher.UIThread.RunJobs();
            var menu = Assert.IsType<MenuFlyout>(field.ContextFlyout);
            var names = menu.ItemsSource!.Cast<MenuItem>().Select(i => i.Header as string).ToList();
            Assert.Equal(["Recortar", "Copiar", "Colar", "Selecionar tudo"], names);
            host.Close();
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task AFingerHeldOnAReadOnlyTextSelectsTheWordAndOffersCopy() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var text = new SelectableTextBlock { Text = "Resposta do assistente com palavras", FontSize = 16 };
            var window = new Window { Width = 400, Height = 200, Content = text };
            window.Show();
            window.UpdateLayout();
            var r = text.TextLayout.HitTestTextPosition("Resposta do ".Length + 2);
            var point = new Point(r.X + 1, r.Y + r.Height / 2);
            // a touch held still (the gesture recogniser's event; its constructor is internal)
            var press = new PointerEventArgs(InputElement.PointerPressedEvent, text, new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true), window, text.TranslatePoint(point, window)!.Value, 0, new PointerPointProperties(), KeyModifiers.None);
            var hold = (HoldingRoutedEventArgs)typeof(HoldingRoutedEventArgs).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .First(c => c.GetParameters().Length == 4).Invoke([HoldingState.Started, point, PointerType.Touch, press]);
            text.RaiseEvent(hold);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("assistente", text.SelectedText);
            var menu = Assert.IsType<MenuFlyout>(text.ContextFlyout);
            Assert.True(menu.IsOpen);
            Assert.Equal(["Copiar", "Selecionar tudo"], menu.ItemsSource!.Cast<MenuItem>().Select(i => i.Header as string));
            menu.Hide();
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ANoteInReadingModeCanBeSelectedAndCopied() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = EditorFlowUiTests.Open("Texto para copiar no modo leitura.\n");
            editor.Surface.ReadOnly = true;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var text = editor.Surface.BlockView(editor.Surface.BlockStarts[0])!.GetVisualDescendants().OfType<SelectableTextBlock>().First();
            var r = text.TextLayout.HitTestTextPosition("Texto para ".Length + 2);
            var point = new Point(r.X + 1, r.Y + r.Height / 2);
            var press = new PointerEventArgs(InputElement.PointerPressedEvent, text, new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true), window, text.TranslatePoint(point, window)!.Value, 0, new PointerPointProperties(), KeyModifiers.None);
            var hold = (HoldingRoutedEventArgs)typeof(HoldingRoutedEventArgs).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .First(c => c.GetParameters().Length == 4).Invoke([HoldingState.Started, point, PointerType.Touch, press]);
            text.RaiseEvent(hold);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("copiar", text.SelectedText);
            Assert.False(editor.Surface.IsEditing);   // reading: nothing turns into an edit box
            ((MenuFlyout)text.ContextFlyout!).Hide();
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ASelectionHandleLetGoBelowTheBlockCarriesTheSelectionThere() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = EditorFlowUiTests.Open("Um dois três.\n\nQuatro cinco seis.\n\nSete oito.\n");
            var surface = editor.Surface;
            var box = EditorFlowUiTests.Edit(editor, 0);
            box.SelectionStart = "Um ".Length; box.SelectionEnd = "Um dois".Length;
            Dispatcher.UIThread.RunJobs();
            // the end handle dragged down to "cinco" in the next block
            var holder = surface.BlockView(surface.BlockStarts[1])!;
            var at = EditorFlowUiTests.PointOf(window, holder, "cinco", 0);
            Assert.True(surface.HandleLetGo(window.TranslatePoint(at, surface)!.Value));
            box = surface.EditBox!;
            Assert.Equal("Um dois três.\n\nQuatro cinco seis.", box.Text);
            Assert.Equal("dois três.\n\nQuatro ", box.SelectedText);
            // and the start handle dragged up from a lower block keeps the end
            var (e2, w2) = EditorFlowUiTests.Open("Um dois três.\n\nQuatro cinco seis.\n");
            var b2 = EditorFlowUiTests.Edit(e2, 1);
            b2.SelectionStart = 0; b2.SelectionEnd = "Quatro".Length;
            Dispatcher.UIThread.RunJobs();
            var h2 = e2.Surface.BlockView(e2.Surface.BlockStarts[0])!;
            var at2 = EditorFlowUiTests.PointOf(w2, h2, "três", 0);
            Assert.True(e2.Surface.HandleLetGo(w2.TranslatePoint(at2, e2.Surface)!.Value));
            Assert.Equal("três.\n\nQuatro", e2.Surface.EditBox!.SelectedText);
            w2.Close();
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task DraggingTheSelectionHandleOfTheBoxPastItsEdgeIsHeard() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var (editor, window) = EditorFlowUiTests.Open("Um dois três.\n\nQuatro cinco seis.\n");
            var surface = editor.Surface;
            var box = EditorFlowUiTests.Edit(editor, 0);
            box.Focus();
            box.SelectionStart = 3; box.SelectionEnd = 7;
            Dispatcher.UIThread.RunJobs();
            // a selection handle (the platform's, in the overlay) dragged and let go over "cinco"
            var handle = new Avalonia.Controls.Primitives.TextSelectionHandle { Width = 20, Height = 20 };
            editor.Children.Add(handle);
            Dispatcher.UIThread.RunJobs();
            var holder = surface.BlockView(surface.BlockStarts[1])!;
            var at = EditorFlowUiTests.PointOf(window, holder, "cinco", 0);
            handle.RaiseEvent(new VectorEventArgs { RoutedEvent = Avalonia.Controls.Primitives.Thumb.DragStartedEvent, Source = handle });
            window.MouseMove(at);
            handle.RaiseEvent(new VectorEventArgs { RoutedEvent = Avalonia.Controls.Primitives.Thumb.DragCompletedEvent, Source = handle });
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("dois três.\n\nQuatro ", surface.EditBox!.SelectedText);
            editor.Children.Remove(handle);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
