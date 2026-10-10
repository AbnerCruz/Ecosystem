using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class TouchListViewTests
{
    private static TouchPoint T(int id, TouchPhase phase, float x = 50, float y = 24)
        => new(id, phase, new Vector2(x, y));

    private static void Frame(TouchListView list, InputState input, params TouchPoint[] touches)
    {
        input.SetTouches(touches);
        list.Update(input, 1f / 60f);
    }

    [Fact]
    public void TapSelectsOnReleaseOnlyAndReactivationIsDistinctFromSelectionChange()
    {
        var list = new TouchListView(new(0, 0, 200, 120), 12, 40);
        var input = new InputState();
        Frame(list, input, T(5, TouchPhase.Pressed, y: 60));
        Assert.Equal(-1, list.SelectedIndex);
        Assert.True(list.IsCaptured);
        Frame(list, input, T(5, TouchPhase.Released, y: 60));
        Assert.Equal(1, list.SelectedIndex);
        Assert.Equal(1, list.ActivatedIndex);
        Assert.True(list.WasSelectionChanged);
        Frame(list, input);
        Assert.Equal(-1, list.ActivatedIndex);
        Assert.False(list.WasSelectionChanged);
        Frame(list, input, T(5, TouchPhase.Pressed, y: 60));
        Frame(list, input, T(5, TouchPhase.Released, y: 60));
        Assert.Equal(1, list.ActivatedIndex);
        Assert.False(list.WasSelectionChanged);
        Frame(list, input, T(5, TouchPhase.Pressed, y: 100));
        Frame(list, input, T(5, TouchPhase.Released, y: 100));
        Assert.Equal(2, list.ActivatedIndex);
        Assert.True(list.WasSelectionChanged);
    }

    [Fact]
    public void DragScrollAndHorizontalSwipeNeverActivate()
    {
        var list = new TouchListView(new(0, 0, 200, 110), 30, 42, 4);
        var input = new InputState();
        Frame(list, input, T(1, TouchPhase.Pressed, y: 92));
        Frame(list, input, T(1, TouchPhase.Moved, y: 32));
        Assert.Equal(60, list.OffsetY);
        Assert.True(list.IsDragging);
        Frame(list, input, T(1, TouchPhase.Released, y: 32));
        Assert.Equal(-1, list.ActivatedIndex);
        Assert.Equal(-1, list.SelectedIndex);
        Frame(list, input);
        Assert.True(list.OffsetY > 60);
        list.ScrollTo(0);
        Frame(list, input, T(2, TouchPhase.Pressed, 40, 20));
        Frame(list, input, T(2, TouchPhase.Moved, 80, 20));
        Frame(list, input, T(2, TouchPhase.Released, 40, 20));
        Assert.Equal(-1, list.ActivatedIndex);
    }

    [Fact]
    public void HitTestingRespectsSpacingViewportAndOnlyVisibleRange()
    {
        var list = new TouchListView(new(20, 50, 100, 130), 12, 40, 8);
        Assert.Equal(0, list.HitTest(new Vector2(50, 60)));
        Assert.Equal(-1, list.HitTest(new Vector2(50, 95))); // entre linhas
        Assert.Equal(-1, list.HitTest(new Vector2(50, 181))); // fora do viewport
        list.GetVisibleRange(out int first, out int end);
        Assert.Equal(0, first); Assert.Equal(3, end);
        list.ScrollTo(100);
        list.GetVisibleRange(out first, out end);
        Assert.Equal(2, first); Assert.Equal(5, end);
        Assert.Equal(new RectangleF(20, 46, 100, 40), list.GetItemBounds(2));
        Assert.Equal(2, list.HitTest(new Vector2(50, 62)));
        Assert.True(list.GetThumbBounds().Width > 0);
        list.ScrollToItem(8, center: true);
        var bounds = list.GetItemBounds(8);
        Assert.InRange(bounds.Center.Y, 50, 180);
    }

    [Fact]
    public void MultitouchDoesNotStealCapturedFingerOrConfirmOtherRelease()
    {
        var list = new TouchListView(new(0, 0, 200, 120), 10, 45);
        var input = new InputState();
        Frame(list, input, T(-1, TouchPhase.Pressed, y: 20), T(2, TouchPhase.Pressed, y: 65));
        Frame(list, input, T(2, TouchPhase.Released, y: 65), T(-1, TouchPhase.Moved, y: 20));
        Assert.True(list.IsCaptured); Assert.Equal(-1, list.ActivatedIndex);
        Frame(list, input, T(2, TouchPhase.Pressed, y: 65), T(-1, TouchPhase.Released, y: 20));
        Assert.Equal(0, list.ActivatedIndex);
        Frame(list, input, T(2, TouchPhase.Released, y: 65));
        Assert.Equal(-1, list.ActivatedIndex);
    }

    [Theory]
    [InlineData(TouchPhase.Cancelled)]
    [InlineData((TouchPhase)99)]
    public void CancelledAndUnknownTouchPhasesNeverSelect(TouchPhase phase)
    {
        var list = new TouchListView(new(0, 0, 200, 100), 5, 40);
        var input = new InputState();
        Frame(list, input, T(1, TouchPhase.Pressed));
        Frame(list, input, T(1, phase));
        Assert.Equal(-1, list.ActivatedIndex);
        Assert.False(list.IsCaptured);
        Frame(list, input, T(1, TouchPhase.Released));
        Assert.Equal(-1, list.ActivatedIndex);
    }

    [Fact]
    public void DisableAndResizeCancelWithoutDiscardingSelection()
    {
        var list = new TouchListView(new(0, 0, 200, 100), 10, 40);
        var input = new InputState();
        list.Select(7);
        list.ScrollToItem(7);
        Assert.True(list.OffsetY > 0);
        Frame(list, input, T(1, TouchPhase.Pressed));
        list.IsEnabled = false;
        Assert.False(list.IsCaptured);
        Frame(list, input, T(1, TouchPhase.Released));
        Assert.Equal(-1, list.ActivatedIndex); Assert.Equal(7, list.SelectedIndex);
        list.IsEnabled = true;
        list.Bounds = new(0, 0, 200, 500);
        Assert.Equal(0, list.OffsetY);
        list.ItemCount = 3;
        Assert.Equal(-1, list.SelectedIndex);
        list.ItemCount = 0;
        list.GetVisibleRange(out var first, out var end);
        Assert.Equal(0, first); Assert.Equal(0, end);
        Assert.Equal(-1, list.HitTest(new Vector2(20, 20)));
        Frame(list, input, T(2, TouchPhase.Pressed));
        Frame(list, input, T(2, TouchPhase.Released));
        Assert.Equal(-1, list.ActivatedIndex);
    }

    [Fact]
    public void InvalidInputsCannotCorruptExistingLayout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchListView(new(0, 0, 100, 100), -1, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchListView(new(0, 0, 100, 100), 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchListView(new(0, 0, 100, 100), 1, 20, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchListView(new(0, 0, 100, 100), int.MaxValue, float.MaxValue));
        var list = new TouchListView(new(0, 0, 100, 120), 3, 30, 4);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.ItemCount = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.ItemCount = int.MaxValue);
        Assert.Equal(3, list.ItemCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Select(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Select(-2));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.GetItemBounds(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.ScrollTo(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.ScrollToItem(-1));
        Assert.Throws<ArgumentNullException>(() => list.Update(null!, 1f / 60f));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Update(new InputState(), -1f));
        Assert.Equal(0, list.OffsetY);
    }

    [Fact]
    public void RenderDrawsVisibleRowsAndDoesNotLeakBatchOnInvalidText()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        device.Resize(360, 640);
        var batch = new SpriteBatch(device);
        var font = SpriteFont.CreateDefault(device);
        var list = new TouchListView(new(12, 80, 300, 90), 100, 42, 4);
        var labels = new string[100];
        for (int i = 0; i < labels.Length; i++) labels[i] = "MISSAO " + i;
        list.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue);
        Assert.NotEmpty(backend.Batches);
        list.Select(6);
        list.ScrollToItem(6);
        list.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue);
        Assert.NotEmpty(backend.Batches);
        Assert.Throws<ArgumentException>(() => list.Draw(batch, font, Array.Empty<string>(), TouchButtonStyle.Default, Color.Blue));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue, 0));
        labels[6] = null!;
        Assert.Throws<ArgumentException>(() => list.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue));
        labels[6] = "MISSAO";
        list.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue);
    }

    [Fact]
    public void HotPathForTouchAndVisibilityHasZeroManagedAllocations()
    {
        var list = new TouchListView(new(0, 0, 200, 110), 60, 40, 4);
        var input = new InputState();
        void Cycle()
        {
            input.SetTouches([T(1, TouchPhase.Pressed, y: 90)]); list.Update(input, 1f / 60);
            input.SetTouches([T(1, TouchPhase.Moved, y: 35)]); list.Update(input, 1f / 60);
            input.SetTouches([T(1, TouchPhase.Released, y: 35)]); list.Update(input, 1f / 60);
            input.SetTouches([]); list.Update(input, 1f / 60);
            list.GetVisibleRange(out _, out _);
            list.HitTest(new Vector2(50, 20));
            list.ScrollTo(0);
        }
        for (int i = 0; i < 100; i++) Cycle();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Cycle();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineGuideCompilesAndRunsWithRealGameHostAndSelection()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "lista-ui.md")))
            root = root.Parent;
        Assert.NotNull(root);
        var guide = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "lista-ui.md"));
        var code = guide.Split(" of code never ")[0]; // marker unused; preserve exact offline snippet
        string source = code.Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(
            new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var built = compiler.Compile("ListGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(built.Success, string.Join("\n", built.Diagnostics));
        Assert.DoesNotContain(built.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(built.Assembly!, built.Symbols);
        var backend = new RecordingBackend();
        var host = new GameHost(loaded.Game, backend);
        Assert.True(host.Start(360, 640), host.Fault?.ToString());
        host.Tick(1.0 / 60);
        var field = loaded.Game.GetType().GetField("list",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        var list = Assert.IsType<TouchListView>(field!.GetValue(loaded.Game));
        host.SetSurfaceTouches([T(1, TouchPhase.Pressed, 70, 110)]); host.Tick(1.0 / 60);
        host.SetSurfaceTouches([T(1, TouchPhase.Released, 70, 110)]); host.Tick(1.0 / 60);
        Assert.Equal(0, list.SelectedIndex);
        host.SetSurfaceTouches([T(1, TouchPhase.Pressed, 70, 160)]); host.Tick(1.0 / 60);
        host.SetSurfaceTouches([T(1, TouchPhase.Moved, 70, 105)]); host.Tick(1.0 / 60);
        host.SetSurfaceTouches([T(1, TouchPhase.Released, 70, 105)]); host.Tick(1.0 / 60);
        Assert.Equal(0, list.SelectedIndex);
        Assert.True(list.OffsetY > 0);
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.NotEmpty(backend.Batches);
        host.Stop();
    }
}
