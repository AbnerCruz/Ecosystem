using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-114/129: the touch selection handles of a TextBox stop relaying the whole text at every key while they are
/// hidden, and still follow the selection when they are shown.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static readonly MethodInfo CanvasShow = typeof(TextBox).Assembly
        .GetType("Avalonia.Controls.Primitives.TextSelectionHandleCanvas")!
        .GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(bool)])!;

    /// <summary>The caret handle: the canvas's first child (Avalonia 12.1.3), placed by Canvas.Left/Top.</summary>
    private static Point CaretHandleAt(Visual canvas)
    {
        var handle = (Control)((Canvas)canvas).Children[0];
        return new Point(Canvas.GetLeft(handle), Canvas.GetTop(handle));
    }

    [Fact]
    public void QuietSelectionHandles_FindsTheCanvasOfThisAvalonia() =>
        // an Avalonia upgrade that moves these members makes the editor slow again without breaking anything: say it
        Assert.True(QuietSelectionHandles.Supported, "Avalonia's TextSelectionHandleCanvas changed: review QuietSelectionHandles.");

    [Fact]
    public async Task QuietSelectionHandles_HiddenHandlesStayStill_ShownHandlesFollowTheCaret() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var text = string.Concat(Enumerable.Range(1, 40).Select(i => $"Linha {i} de um texto longo que quebra na tela do celular.\n"));
        var box = new TextBox { Text = text, TextWrapping = TextWrapping.Wrap, Width = 360 };
        EditorInput.ConfigureMultiline(box);
        var window = new Window { Width = 412, Height = 900, Content = new ScrollViewer { Content = box } };
        window.Show();
        box.Focus();
        await BenchPump(window);
        var canvas = QuietSelectionHandles.CanvasOf(box);
        Assert.NotNull(canvas);

        // hidden: moving the caret and typing leave the hidden handle where it was (nothing was laid out for it)
        box.CaretIndex = 10;
        await BenchPump(window);
        Assert.False(canvas!.IsVisible);
        var still = CaretHandleAt(canvas);
        box.CaretIndex = text.Length - 5;
        window.KeyTextInput("x");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(still, CaretHandleAt(canvas));
        Assert.Equal(text.Insert(text.Length - 5, "x"), box.Text);

        // shown (a touch on the text shows them): placed at the caret, and they follow it from then on
        CanvasShow.Invoke(canvas, [true]);
        await BenchPump(window);
        Assert.True(canvas.IsVisible);
        var atEnd = CaretHandleAt(canvas);
        Assert.NotEqual(still, atEnd);
        box.CaretIndex = 3;
        Dispatcher.UIThread.RunJobs();
        var atStart = CaretHandleAt(canvas);
        Assert.True(atStart.Y < atEnd.Y, $"the shown handle did not follow the caret ({atEnd} → {atStart})");

        // a typed key hides them again (Avalonia's own rule, still heard)
        window.KeyTextInput("y");
        Dispatcher.UIThread.RunJobs();
        Assert.False(canvas.IsVisible);
        window.Close();
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task QuietSelectionHandles_TheNoteEditorsAreQuieted() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        await view.LoadAsync(TutorialNotes.Load());
        view.Editor.Open(new CityNote("Tutorial/Estudo.md", BenchNote()));
        view.Editor.SetMode(EditorMode.Fonte);
        await BenchPump(window);
        view.Editor.Editor.Focus();
        view.Editor.Editor.CaretIndex = 100;
        await BenchPump(window);
        var canvas = QuietSelectionHandles.CanvasOf(view.Editor.Editor);
        Assert.NotNull(canvas);
        var still = CaretHandleAt(canvas!);
        view.Editor.Editor.CaretIndex = view.Editor.Editor.Text!.Length - 10;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(still, CaretHandleAt(canvas!));
        window.Close();
        return true;
    }, CancellationToken.None));
}
