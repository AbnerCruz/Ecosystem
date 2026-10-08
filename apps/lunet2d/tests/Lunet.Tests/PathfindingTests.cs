using Lunet.Pathfinding;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class PathfindingTests
{
    [Fact]
    public void CardinalPath_IncludesEndpoints_AndPreservesBufferTail()
    {
        var grid = new GridPathfinder(4, 3);
        var path = Enumerable.Repeat(new GridPoint(-7, -8), 20).ToArray();
        var result = grid.FindPath(new(0, 0), new(3, 2), path);
        Assert.Equal(PathStatus.Found, result.Status);
        Assert.Equal(6, result.Length); Assert.Equal(5, result.Cost);
        Assert.Equal(new(0, 0), path[0]); Assert.Equal(new(3, 2), path[5]);
        Assert.Equal(new GridPoint(-7, -8), path[6]);
        Validate(grid, path.AsSpan(0, result.Length), false, result.Cost);
    }

    [Fact]
    public void BufferTooSmall_IsAtomic_AndReturnsRequiredSizeAndCost()
    {
        var grid = new GridPathfinder(5, 1);
        GridPoint[] path = [new(9, 9), new(8, 8)];
        var result = grid.FindPath(new(0, 0), new(4, 0), path);
        Assert.Equal(PathStatus.BufferTooSmall, result.Status);
        Assert.Equal(5, result.Length); Assert.Equal(4, result.Cost);
        Assert.Equal(new GridPoint[] { new(9, 9), new(8, 8) }, path);
        Assert.Equal(result, grid.FindPath(new(0, 0), new(4, 0), Span<GridPoint>.Empty));
    }

    [Fact]
    public void BlockedEndpoints_UnreachableAndSameCell_AreDistinct()
    {
        var grid = new GridPathfinder(3, 3);
        GridPoint[] path = [new(-1, -1)];
        var same = grid.FindPath(new(0, 0), new(0, 0), path);
        Assert.Equal(PathStatus.Found, same.Status); Assert.Equal(1, same.Length); Assert.Equal(0, same.Cost);
        grid.SetCost(new(0, 0), 0);
        Assert.Equal(PathStatus.NotFound, grid.FindPath(new(0, 0), new(0, 0), path).Status);
        grid.SetCost(new(0, 0), 1);
        for (int y = 0; y < 3; y++) grid.SetCost(new(1, y), 0);
        path[0] = new(-1, -1);
        var no = grid.FindPath(new(0, 0), new(2, 0), path, true);
        Assert.Equal(PathStatus.NotFound, no.Status); Assert.Equal(0, no.Length);
        Assert.True(double.IsPositiveInfinity(no.Cost)); Assert.Equal(new(-1, -1), path[0]);
        Assert.Equal(PathStatus.NotFound, grid.FindPath(new(0, 0), new(1, 0), path).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Diagonals_RequireBothSideCellsOpen(bool blockBoth)
    {
        var grid = new GridPathfinder(2, 2);
        var path = new GridPoint[4];
        var direct = grid.FindPath(new(0, 0), new(1, 1), path, true);
        Assert.Equal(2, direct.Length); Assert.Equal(Math.Sqrt(2), direct.Cost);
        grid.SetCost(new(1, 0), 0);
        if (blockBoth) grid.SetCost(new(0, 1), 0);
        var detour = grid.FindPath(new(0, 0), new(1, 1), path, true);
        Assert.Equal(blockBoth ? PathStatus.NotFound : PathStatus.Found, detour.Status);
        if (!blockBoth) { Assert.Equal(3, detour.Length); Assert.Equal(2, detour.Cost); }
    }

    [Fact]
    public void EntryCosts_CanFavorLongerRoutes_AndOriginIsNotCharged()
    {
        var grid = new GridPathfinder(5, 3);
        var path = new GridPoint[15];
        grid.SetCost(new(0, 1), float.MaxValue);
        for (int x = 1; x < 4; x++) grid.SetCost(new(x, 1), 9);
        var result = grid.FindPath(new(0, 1), new(4, 1), path);
        Assert.Equal(6, result.Cost); Assert.Equal(7, result.Length);
        for (int x = 0; x < 5; x++) grid.SetCost(new(x, 2), 0.125f);
        result = grid.FindPath(new(0, 1), new(4, 1), path);
        Assert.Equal(1.625, result.Cost);
        Assert.Contains(new GridPoint(2, 2), path[..result.Length]);
        Validate(grid, path.AsSpan(0, result.Length), false, result.Cost);
    }

    [Fact]
    public void RepeatedSearchesAndEdits_ResetTheWorkspace_AndPreserveCopiedPaths()
    {
        var grid = new GridPathfinder(8, 8);
        var path = new GridPoint[64];
        var first = grid.FindPath(new(0, 0), new(7, 7), path, true);
        var saved = path[..first.Length];
        for (int i = 0; i < 10; i++)
        {
            var again = grid.FindPath(new(0, 0), new(7, 7), path, true);
            Assert.Equal(first, again); Assert.Equal(saved, path[..again.Length]);
            grid.FindPath(new(7, 7), new(0, 0), path);
        }
        grid.SetCost(new(3, 3), 0);
        var changed = grid.FindPath(new(0, 0), new(7, 7), path, true);
        Assert.DoesNotContain(new GridPoint(3, 3), path[..changed.Length]);
        Assert.Contains(new GridPoint(3, 3), saved);
    }

    [Fact]
    public void InvalidInputs_RejectWithoutMutatingCostsOrOutput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridPathfinder(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridPathfinder(2, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridPathfinder(int.MaxValue, 2));
        var grid = new GridPathfinder(2, 2); GridPoint[] path = [new(8, 8)];
        foreach (float value in new[] { -1, float.NaN, float.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetCost(new(1, 1), value));
        Assert.Equal(1, grid.GetCost(new(1, 1)));
        foreach (var cell in new GridPoint[] { new(-1, 0), new(2, 0), new(0, -1), new(0, 2), new(int.MaxValue, int.MinValue) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.GetCost(cell));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.SetCost(cell, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.FindPath(cell, new(1, 1), path));
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.FindPath(new(0, 0), cell, path));
        }
        Assert.Equal(new(8, 8), path[0]);
        grid.SetCost(new(1, 1), float.Epsilon);
        Assert.Equal((double)float.Epsilon, grid.FindPath(new(0, 1), new(1, 1), new GridPoint[2]).Cost);
        grid.SetCost(new(1, 1), float.MaxValue);
        Assert.True(double.IsFinite(grid.FindPath(new(0, 0), new(1, 1), new GridPoint[4], true).Cost));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeededWeightedGrids_MatchIndependentDijkstra(bool diagonals)
    {
        var random = new Random(4206);
        for (int sample = 0; sample < 100; sample++)
        {
            var grid = new GridPathfinder(9, 7);
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                grid.SetCost(new(x, y), random.Next(5) == 0 ? 0 : new[] { 0.125f, 0.5f, 1f, 2f, 12f }[random.Next(5)]);
            var start = new GridPoint(random.Next(9), random.Next(7));
            var goal = new GridPoint(random.Next(9), random.Next(7));
            var path = new GridPoint[63];
            double expected = Dijkstra(grid, start, goal, diagonals);
            var result = grid.FindPath(start, goal, path, diagonals);
            if (double.IsPositiveInfinity(expected)) Assert.Equal(PathStatus.NotFound, result.Status);
            else
            {
                Assert.Equal(PathStatus.Found, result.Status);
                Assert.InRange(Math.Abs(expected - result.Cost), 0, 1e-10);
                Assert.Equal(start, path[0]); Assert.Equal(goal, path[result.Length - 1]);
                Validate(grid, path.AsSpan(0, result.Length), diagonals, result.Cost);
            }
        }
    }

    private static double Dijkstra(GridPathfinder grid, GridPoint start, GridPoint goal, bool diagonal)
    {
        if (grid.GetCost(start) == 0 || grid.GetCost(goal) == 0) return double.PositiveInfinity;
        var distances = Enumerable.Repeat(double.PositiveInfinity, grid.Width * grid.Height).ToArray();
        var closed = new bool[distances.Length]; distances[start.Y * grid.Width + start.X] = 0;
        for (int iteration = 0; iteration < distances.Length; iteration++)
        {
            int at = -1;
            for (int i = 0; i < distances.Length; i++)
                if (!closed[i] && double.IsFinite(distances[i]) && (at == -1 || distances[i] < distances[at])) at = i;
            if (at == -1) break;
            var current = new GridPoint(at % grid.Width, at / grid.Width);
            if (current == goal) return distances[at];
            closed[at] = true;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                int dx = Math.Abs(x - current.X), dy = Math.Abs(y - current.Y);
                if (dx > 1 || dy > 1 || dx + dy == 0 || (!diagonal && dx + dy != 1)) continue;
                var next = new GridPoint(x, y); float cost = grid.GetCost(next);
                if (cost == 0) continue;
                if (dx == 1 && dy == 1 && (grid.GetCost(new(x, current.Y)) == 0 || grid.GetCost(new(current.X, y)) == 0)) continue;
                distances[y * grid.Width + x] = Math.Min(distances[y * grid.Width + x], distances[at] + cost * Math.Sqrt(dx + dy));
            }
        }
        return double.PositiveInfinity;
    }

    private static void Validate(GridPathfinder grid, ReadOnlySpan<GridPoint> path, bool diagonals, double expected)
    {
        double cost = 0;
        for (int i = 1; i < path.Length; i++)
        {
            int dx = Math.Abs(path[i].X - path[i - 1].X), dy = Math.Abs(path[i].Y - path[i - 1].Y);
            Assert.InRange(dx, 0, 1); Assert.InRange(dy, 0, 1); Assert.NotEqual(0, dx + dy);
            Assert.True(diagonals || dx + dy == 1); Assert.True(grid.GetCost(path[i]) > 0);
            if (dx + dy == 2)
            {
                Assert.True(grid.GetCost(new(path[i].X, path[i - 1].Y)) > 0);
                Assert.True(grid.GetCost(new(path[i - 1].X, path[i].Y)) > 0);
            }
            cost += grid.GetCost(path[i]) * Math.Sqrt(dx + dy);
        }
        Assert.InRange(Math.Abs(cost - expected), 0, 1e-10);
    }

    [Fact]
    public void Searches_WithSuccessFailureAndSmallOutput_DoNotAllocate()
    {
        var grid = new GridPathfinder(16, 16); var path = new GridPoint[256];
        void Search()
        {
            grid.FindPath(new(0, 0), new(15, 15), path, true);
            grid.FindPath(new(15, 0), new(0, 15), path.AsSpan(0, 2));
            grid.SetCost(new(0, 0), 0);
            grid.FindPath(new(0, 0), new(15, 15), path);
            grid.SetCost(new(0, 0), 1);
        }
        for (int i = 0; i < 100; i++) Search();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Search();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineGuide_CompilesAndSearchesWithScaledTouchInRealRuntime()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "pathfinding.md"))) root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "pathfinding.md")).Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var compiled = compiler.Compile("PathfindingGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
        Assert.DoesNotContain(compiled.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(compiled.Assembly!, compiled.Symbols);
        var host = new GameHost(loaded.Game, new RecordingBackend()); Assert.True(host.Start(720, 1280));
        host.Tick(1.0 / 60);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        PathResult Result() => (PathResult)loaded.Game.GetType().GetField("result", flags)!.GetValue(loaded.Game)!;
        var original = Result(); Assert.Equal(PathStatus.Found, original.Status);
        void Touch(float x, float y)
        {
            host.SetSurfaceTouches([new Lunet.Input.TouchPoint(0, Lunet.Input.TouchPhase.Pressed, new(x * 2, y * 2))]);
            host.Tick(1.0 / 60);
            host.SetSurfaceTouches([]); host.Tick(1.0 / 60);
        }
        Touch(164, 148); Assert.Equal(PathStatus.NotFound, Result().Status); // parede (4,1)
        Touch(68, 148); Assert.Equal(1, Result().Length); // origem (1,1)
        Touch(292, 148); Assert.Equal(original.Cost, Result().Cost);
        Touch(50, 20); Assert.True(Result().Cost < original.Cost);
        Assert.False(host.IsFaulted);
        host.Stop();
    }
}
