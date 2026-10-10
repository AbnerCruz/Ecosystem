using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class TouchGridViewTests
{
    private static TouchPoint T(int id, TouchPhase phase, float x = 45, float y = 50)
        => new(id, phase, new Vector2(x, y));

    private static void Frame(TouchGridView grid, InputState input, params TouchPoint[] touches)
    {
        input.SetTouches(touches);
        grid.Update(input, 1f / 60f);
    }

    [Fact]
    public void TapSelectsCellOnlyOnReleaseAndReactivatesWithoutSelectionChange()
    {
        var grid = new TouchGridView(new(20, 30, 200, 130), 100,
            new UiGridLayout(60, 40, spacing: 10, padding: 10));
        var input = new InputState();
        Frame(grid, input, T(1, TouchPhase.Pressed, 45, 50));
        Assert.Equal(-1, grid.SelectedIndex);
        Assert.True(grid.IsCaptured);
        Frame(grid, input, T(1, TouchPhase.Released, 45, 50));
        Assert.Equal(0, grid.ActivatedIndex);
        Assert.Equal(0, grid.SelectedIndex);
        Assert.True(grid.WasSelectionChanged);
        Frame(grid, input);
        Assert.Equal(-1, grid.ActivatedIndex);
        Frame(grid, input, T(1, TouchPhase.Pressed, 45, 50));
        Frame(grid, input, T(1, TouchPhase.Released, 45, 50));
        Assert.Equal(0, grid.ActivatedIndex);
        Assert.False(grid.WasSelectionChanged);
        Frame(grid, input, T(2, TouchPhase.Pressed, 150, 50));
        Frame(grid, input, T(2, TouchPhase.Released, 150, 50));
        Assert.Equal(1, grid.SelectedIndex);
        Assert.True(grid.WasSelectionChanged);
    }

    [Fact]
    public void SpacesGapsAndDraggingNeverActivateCells()
    {
        var grid = new TouchGridView(new(20, 30, 200, 130), 100,
            new UiGridLayout(60, 40, spacing: 10, padding: 10));
        var input = new InputState();
        Assert.Equal(-1, grid.HitTest(new Vector2(112, 60)));
        Assert.Equal(-1, grid.HitTest(new Vector2(40, 82)));
        Frame(grid, input, T(1, TouchPhase.Pressed, 40, 82));
        Frame(grid, input, T(1, TouchPhase.Released, 40, 82));
        Assert.Equal(-1, grid.ActivatedIndex);
        Frame(grid, input, T(2, TouchPhase.Pressed, 50, 130));
        Frame(grid, input, T(2, TouchPhase.Moved, 50, 55));
        Assert.True(grid.IsDragging);
        Assert.True(grid.OffsetY > 0);
        Frame(grid, input, T(2, TouchPhase.Released, 50, 55));
        Assert.Equal(-1, grid.ActivatedIndex);
        Assert.Equal(-1, grid.SelectedIndex);
    }

    [Fact]
    public void ResizeCancelsTouchAndReflowsColumnsWithoutLosingSelection()
    {
        var grid = new TouchGridView(new(0, 0, 200, 130), 100,
            new UiGridLayout(60, 40, spacing: 10, padding: 10));
        var input = new InputState();
        grid.Select(95);
        grid.ScrollToItem(95);
        Assert.True(grid.OffsetY > 0);
        int before = grid.Layout.GetColumnCount(grid.Bounds, grid.ItemCount);
        Frame(grid, input, T(1, TouchPhase.Pressed, 20, 30));
        grid.Bounds = new(0, 0, 700, 250);
        Assert.False(grid.IsCaptured);
        Assert.True(grid.Layout.GetColumnCount(grid.Bounds, grid.ItemCount) > before);
        Assert.Equal(95, grid.SelectedIndex);
        Frame(grid, input, T(1, TouchPhase.Released, 20, 30));
        Assert.Equal(-1, grid.ActivatedIndex);
        grid.ItemCount = 10;
        Assert.Equal(-1, grid.SelectedIndex);
        grid.ScrollTo(0);
        grid.GetVisibleRange(out int first, out int end);
        Assert.True(end > first);
    }

    [Fact]
    public void MultitouchAndCancelledTouchesCannotCreateGhostSelections()
    {
        var grid = new TouchGridView(new(0, 0, 200, 130), 100,
            new UiGridLayout(60, 40, spacing: 10, padding: 10));
        var input = new InputState();
        Frame(grid, input, T(-1, TouchPhase.Pressed, 20, 30), T(5, TouchPhase.Pressed, 150, 30));
        Frame(grid, input, T(5, TouchPhase.Released, 150, 30), T(-1, TouchPhase.Moved, 20, 30));
        Assert.Equal(-1, grid.ActivatedIndex);
        Frame(grid, input, T(-1, TouchPhase.Cancelled, 20, 30));
        Assert.False(grid.IsCaptured);
        Frame(grid, input, T(-1, TouchPhase.Released, 20, 30));
        Assert.Equal(-1, grid.SelectedIndex);
    }

    [Fact]
    public void HugeGridUsesVisibleRangeAndDeterministicHitTest()
    {
        var grid = new TouchGridView(new(0, 0, 300, 160), 1_000_000,
            new UiGridLayout(48, 48, spacing: 4, padding: 8));
        grid.ScrollTo(100_000);
        grid.GetVisibleRange(out int first, out int end);
        Assert.InRange(end - first, 1, 25);
        Assert.Equal(first, grid.HitTest(new Vector2(30, 20)));
        var cell = grid.GetItemBounds(first);
        Assert.True(float.IsFinite(cell.Y));
        grid.ScrollToItem(900_000, center: true);
        grid.GetVisibleRange(out first, out end);
        Assert.InRange(900_000, first, end - 1);
    }

    [Fact]
    public void RenderingUsesExistingBackendAndRejectsInvalidInputs()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        device.Resize(360, 640);
        var batch = new SpriteBatch(device);
        var font = SpriteFont.CreateDefault(device);
        var grid = new TouchGridView(new(10, 20, 300, 130), 24,
            new UiGridLayout(60, 40, spacing: 8, padding: 8));
        var labels = Enumerable.Range(0, 24).Select(i => "ITEM " + i).ToArray();
        grid.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue);
        Assert.NotEmpty(backend.Batches);
        grid.Select(19);
        grid.ScrollToItem(19);
        grid.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue);
        Assert.Throws<ArgumentException>(() => grid.Draw(batch, font, Array.Empty<string>(), TouchButtonStyle.Default, Color.Blue));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue, 0));
        labels[19] = null!;
        Assert.Throws<ArgumentException>(() => grid.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue));
        labels[19] = "ITEM 19";
        grid.Draw(batch, font, labels, TouchButtonStyle.Default, Color.Blue);
    }

    [Fact]
    public void ValidationsDoNotPartiallyChangeExistingState()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TouchGridView(new(0,0,200,120), -1, new UiGridLayout(40,40)));
        Assert.Throws<InvalidOperationException>(() => new TouchGridView(new(0,0,200,120), 2, default));
        var grid = new TouchGridView(new(0,0,200,120), 5, new UiGridLayout(40,40));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.ItemCount = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Bounds = new(float.NaN,0,200,120));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Select(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.ScrollTo(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.ScrollToItem(5));
        Assert.Throws<ArgumentNullException>(() => grid.Update(null!, 1f/60));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Update(new InputState(), -1f));
        Assert.Equal(5, grid.ItemCount);
        Assert.Equal(new RectangleF(0,0,200,120), grid.Bounds);
    }

    [Fact]
    public void HotPathDragAndHitTestAvoidManagedAllocations()
    {
        var grid = new TouchGridView(new(0,0,300,160), 500,
            new UiGridLayout(48,48,spacing:4,padding:8));
        var input = new InputState();
        void Cycle()
        {
            input.SetTouches([T(1, TouchPhase.Pressed, 40,120)]); grid.Update(input,1f/60);
            input.SetTouches([T(1, TouchPhase.Moved, 40,40)]); grid.Update(input,1f/60);
            input.SetTouches([T(1, TouchPhase.Released, 40,40)]); grid.Update(input,1f/60);
            input.SetTouches([]); grid.Update(input,1f/60);
            grid.GetVisibleRange(out _,out _);
            grid.HitTest(new Vector2(40,40));
            grid.ScrollTo(0);
        }
        for (int i=0;i<100;i++) Cycle();
        long before=GC.GetAllocatedBytesForCurrentThread();
        for (int i=0;i<100;i++) Cycle();
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-before);
    }
}
