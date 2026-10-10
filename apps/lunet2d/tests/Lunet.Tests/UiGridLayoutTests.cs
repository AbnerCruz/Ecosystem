using System.Numerics;
using Lunet.Graphics;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class UiGridLayoutTests
{
    private static void Near(RectangleF actual, float x, float y, float w, float h)
    {
        Assert.InRange(actual.X, x - .005f, x + .005f);
        Assert.InRange(actual.Y, y - .005f, y + .005f);
        Assert.InRange(actual.Width, w - .005f, w + .005f);
        Assert.InRange(actual.Height, h - .005f, h + .005f);
    }

    [Fact]
    public void ColumnsFollowViewportAndComputeRowsForScroll()
    {
        var grid = new UiGridLayout(80, 48, spacing: 8, padding: 12);
        var cells = new RectangleF[9];
        var portrait = new RectangleF(0, 0, 360, 640);
        Assert.Equal(3, grid.Arrange(portrait, cells.Length, cells));
        Near(cells[0], 12, 12, 320f / 3, 48);
        Near(cells[2], 12f + (320f / 3 + 8) * 2, 12, 320f / 3, 48);
        Near(cells[3], 12, 68, 320f / 3, 48);
        Assert.Equal(184, grid.GetContentHeight(portrait, 9));

        var landscape = new RectangleF(5, 10, 800, 360);
        Assert.Equal(8, grid.Arrange(landscape, cells.Length, cells));
        Near(cells[8], 17, 78, 90, 48);
        Assert.Equal(128, grid.GetContentHeight(landscape, 9));
    }

    [Fact]
    public void MaxColumnsReservesWiderCellsInLandscapes()
    {
        var grid = new UiGridLayout(64, 52, spacing: 4, padding: 8, maxColumns: 4);
        var cells = new RectangleF[11];
        Assert.Equal(4, grid.Arrange(new(20, 30, 960, 360), 11, cells));
        Near(cells[0], 28, 38, 233, 52);
        Near(cells[4], 28, 94, 233, 52);
        Assert.Equal(180, grid.GetContentHeight(new(20, 30, 960, 360), 11));
        Assert.Equal(4, grid.MaxColumns);
        Assert.Equal(64, grid.MinimumCellWidth);
    }

    [Fact]
    public void NarrowAndZeroViewportRemainFiniteAndHitTestSafe()
    {
        var grid = new UiGridLayout(120, 48, spacing: 8, padding: 20);
        var cells = new RectangleF[3];
        Assert.Equal(1, grid.Arrange(new(10, 15, 50, 80), 3, cells));
        Near(cells[0], 30, 35, 10, 48);
        Assert.Equal(1, grid.Arrange(new(10, 15, 0, 0), 3, cells));
        Near(cells[0], 10, 35, 0, 48);
        Assert.False(cells[0].Contains(new Vector2(10, 40)));
    }

    [Fact]
    public void EmptyGridLeavesOutputUntouched()
    {
        var grid = new UiGridLayout(64, 48);
        var cells = new[] { new RectangleF(4, 8, 16, 32) };
        Assert.Equal(0, grid.GetColumnCount(new(0, 0, 300, 300), 0));
        Assert.Equal(0, grid.GetContentHeight(new(0, 0, 300, 300), 0));
        Assert.Equal(0, grid.Arrange(new(0, 0, 300, 300), 0, cells));
        Assert.Equal(new RectangleF(4, 8, 16, 32), cells[0]);
    }

    [Fact]
    public void InvalidParametersAndOverflowNeverPartiallyFillOutput()
    {
        foreach (float bad in new[] { 0, -1, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UiGridLayout(bad, 48));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UiGridLayout(48, bad));
        }
        foreach (float bad in new[] { -1, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UiGridLayout(48, 48, spacing: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UiGridLayout(48, 48, padding: bad));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => new UiGridLayout(48, 48, maxColumns: -1));
        Assert.Throws<InvalidOperationException>(() => default(UiGridLayout).GetColumnCount(new(0, 0, 100, 100), 3));
        var grid = new UiGridLayout(48, 48);
        var marker = new RectangleF(2, 4, 6, 8);
        var cells = new[] { marker, marker };
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Arrange(new(0, 0, 100, 100), -1, cells));
        Assert.Throws<ArgumentException>(() => grid.Arrange(new(0, 0, 100, 100), 3, cells));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Arrange(new(float.NaN, 0, 100, 100), 2, cells));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Arrange(new(0, 0, -10, 100), 2, cells));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Arrange(new(float.MaxValue, 0, float.MaxValue, 100), 2, cells));
        var tall = new UiGridLayout(48, float.MaxValue);
        Assert.Throws<OverflowException>(() => tall.Arrange(new(0, 0, 48, 48), 2, cells));
        Assert.Throws<OverflowException>(() => tall.GetContentHeight(new(0, 0, 48, 48), 2));
        Assert.Equal(marker, cells[0]);
        Assert.Equal(marker, cells[1]);
    }

    [Fact]
    public void HotPathReflowsWithoutManagedAllocations()
    {
        var grid = new UiGridLayout(72, 48, spacing: 8, padding: 12, maxColumns: 6);
        var cells = new RectangleF[40];
        void Update()
        {
            grid.Arrange(new(0, 0, 360, 640), 40, cells);
            grid.Arrange(new(10, 10, 820, 360), 40, cells);
            grid.GetContentHeight(new(10, 10, 820, 360), 40);
        }
        for (int i = 0; i < 100; i++) Update();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Update();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }


    [Fact]
    public void VisibleWindowReturnsOnlyIntersectingRowsAndCellBoundsMatchFullLayout()
    {
        var grid = new UiGridLayout(60, 40, spacing: 10, padding: 10);
        var area = new RectangleF(0, 0, 200, 110);
        var all = new RectangleF[100];
        Assert.Equal(2, grid.Arrange(area, 100, all));
        grid.GetVisibleRange(area, 100, 0, out int first, out int end);
        Assert.Equal((0, 4), (first, end));
        grid.GetVisibleRange(area, 100, 55, out first, out end);
        Assert.Equal((2, 8), (first, end));
        grid.GetVisibleRange(area, 100, 50, out first, out end);
        Assert.Equal((2, 6), (first, end));
        grid.GetVisibleRange(area, 100, 100, out first, out end);
        Assert.Equal((4, 8), (first, end));
        grid.GetVisibleRange(area, 100, 6000, out first, out end);
        Assert.Equal((0, 0), (first, end));
        for (int i = 0; i < all.Length; i++)
            Assert.Equal(all[i], grid.GetCellBounds(area, 100, i));
        Near(grid.GetCellBounds(area, 100, 5), 105, 110, 85, 40);
    }

    [Fact]
    public void VisibleWindowHandlesEmptyAndGapOnlyViewport()
    {
        var grid = new UiGridLayout(60, 40, spacing: 10, padding: 10);
        grid.GetVisibleRange(new(0, 0, 200, 100), 0, 0, out int first, out int end);
        Assert.Equal((0, 0), (first, end));
        grid.GetVisibleRange(new(0, 0, 200, 0), 100, 5, out first, out end);
        Assert.Equal((0, 0), (first, end));
        grid.GetVisibleRange(new(0, 0, 200, 5), 100, 52, out first, out end);
        Assert.Equal((0, 0), (first, end));
        grid.GetVisibleRange(new(0, 0, 200, 5), 100, 61, out first, out end);
        Assert.Equal((2, 4), (first, end));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetVisibleRange(new(0, 0, 200, 100), 100, float.NaN, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetVisibleRange(new(0, 0, 200, 100), 100, -1, out _, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetCellBounds(new(0, 0, 200, 100), 100, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetCellBounds(new(0, 0, 200, 100), 100, -1));
    }

    [Fact]
    public void HugeInventoryVisibleQueriesAreConstantTimeAndAllocationFree()
    {
        var grid = new UiGridLayout(48, 48, spacing: 4, padding: 8);
        var area = new RectangleF(0, 0, 300, 160);
        void Probe()
        {
            grid.GetVisibleRange(area, 100_000_000, 10_000_000, out int first, out int end);
            Assert.True(end - first <= 25);
            Assert.True(first >= 0 && end <= 100_000_000);
            _ = grid.GetCellBounds(area, 100_000_000, first);
        }
        Probe();
        for (int i = 0; i < 100; i++) Probe();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            grid.GetVisibleRange(area, 100_000_000, 10_000_000, out int first, out int end);
            _ = grid.GetCellBounds(area, 100_000_000, first);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineInventoryExampleCompilesAndRunsInGameHost()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "grade-ui.md")))
            root = root.Parent;
        Assert.NotNull(root);
        var guide = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "grade-ui.md"));
        var source = guide.Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(
            new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var built = compiler.Compile("GridUiGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(built.Success, string.Join("\n", built.Diagnostics));
        Assert.DoesNotContain(built.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(built.Assembly!, built.Symbols);
        var backend = new RecordingBackend();
        var host = new GameHost(loaded.Game, backend);
        Assert.True(host.Start(360, 640), host.Fault?.ToString());
        host.Tick(1d / 60);
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.NotEmpty(backend.Batches);
        host.Resize(800, 360);
        host.Tick(1d / 60);
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        host.Stop();
    }
}
