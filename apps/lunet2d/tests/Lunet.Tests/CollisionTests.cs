using System.Numerics;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class CollisionTests
{
    private static Vector2[] Box(float x, float y, float size) =>
        [new(x, y), new(x + size, y), new(x + size, y + size), new(x, y + size)];

    [Fact]
    public void AabbIntersection_ReturnsCommonRegion_AndRejectsEdgeContact()
    {
        var a = new RectangleF(0, 0, 10, 10);
        var b = new RectangleF(8, -2, 10, 6);
        Assert.True(Geometry.Intersection(a, b, out var common));
        Assert.Equal(new RectangleF(8, 0, 2, 4), common);
        Assert.True(Geometry.Intersection(b, a, out var reversed));
        Assert.Equal(common, reversed);
        Assert.True(Geometry.Intersection(a, new RectangleF(2, 2, 3, 4), out var contained));
        Assert.Equal(new RectangleF(2, 2, 3, 4), contained);
        foreach (var empty in new[] { new RectangleF(10, 0, 4, 4), new RectangleF(2, 2, 0, 4), new RectangleF(20, 20, 1, 1) })
        {
            Assert.False(Geometry.Intersection(a, empty, out var result));
            Assert.Equal(default, result);
        }
    }

    [Fact]
    public void PointDistances_TreatShapesAsFilled_AndUseClosedBorders()
    {
        var rect = new RectangleF(0, 0, 10, 10);
        Assert.Equal(new Vector2(10, 10), Geometry.ClosestPoint(new Vector2(13, 14), rect));
        Assert.Equal(5, Geometry.Distance(new Vector2(13, 14), rect), 5);
        Assert.Equal(0, Geometry.Distance(new Vector2(5, 5), rect));
        Assert.Equal(0, Geometry.Distance(new Vector2(10, 10), rect));
        // Contains conserva a regra half-open para UI; distância mede a forma fechada.
        Assert.False(rect.Contains(new Vector2(10, 10)));
        Assert.Equal(5, Geometry.Distance(new Vector2(3, 4), new RectangleF(0, 0, 0, 0)), 5);
        var circle = new Circle(Vector2.Zero, 2);
        Assert.Equal(3, Geometry.Distance(new Vector2(3, 4), circle), 5);
        Assert.Equal(0, Geometry.Distance(Vector2.One, circle));
        Assert.Equal(0, Geometry.Distance(new Vector2(2, 0), circle));
    }

    [Fact]
    public void ShapeDistances_AreSymmetric_AndZeroForContactOrContainment()
    {
        var a = new RectangleF(0, 0, 2, 2);
        var b = new RectangleF(5, 6, 2, 2);
        Assert.Equal(5, Geometry.Distance(a, b), 5);
        Assert.Equal(Geometry.Distance(a, b), Geometry.Distance(b, a));
        Assert.Equal(0, Geometry.Distance(a, new RectangleF(2, 0, 2, 2)));
        Assert.Equal(0, Geometry.Distance(a, new RectangleF(0, 0, 1, 1)));
        var circle = new Circle(new Vector2(5, 6), 1);
        Assert.Equal(4, Geometry.Distance(circle, a), 5);
        Assert.Equal(0, Geometry.Distance(new Circle(Vector2.One, 0.5f), a));
        Assert.Equal(0, Geometry.Distance(new Circle(new Vector2(3, 1), 1), a));
        var c = new Circle(Vector2.Zero, 2);
        var d = new Circle(new Vector2(10, 0), 3);
        Assert.Equal(5, Geometry.Distance(c, d));
        Assert.Equal(Geometry.Distance(c, d), Geometry.Distance(d, c));
        Assert.Equal(0, Geometry.Distance(c, new Circle(Vector2.One, 0.1f)));
        Assert.Equal(0, Geometry.Distance(c, new Circle(new Vector2(5, 0), 3)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sat_ContainmentPush_ReallySeparatesBothOrders(bool reverse)
    {
        var inner = Box(2, 3, 2);
        var outer = Box(0, 0, 10);
        var a = reverse ? outer : inner;
        var b = reverse ? inner : outer;
        Assert.True(Geometry.SatOverlap(a, b, out var push));
        Assert.Equal(4, push.Length(), 5); // antes retornava só a largura interna: 2
        Assert.False(Geometry.SatOverlap(a.Select(p => p + push).ToArray(), b, out _));
        Assert.True(Geometry.SatOverlap(a.Select(p => p + push * 0.999f).ToArray(), b, out _));
    }

    [Fact]
    public void Sat_IdenticalShapes_ReversedWinding_AndRepeatedVertices()
    {
        var a = Box(0, 0, 10);
        var b = a.Reverse().ToArray();
        Assert.True(Geometry.SatOverlap(a, b, out var push));
        Assert.Equal(10, push.Length(), 5);
        Assert.False(Geometry.SatOverlap(a.Select(p => p + push).ToArray(), b, out _));
        Vector2[] repeated = [a[0], a[0], a[1], a[2], a[3]];
        Assert.True(Geometry.SatOverlap(repeated, Box(8, 2, 10), out var repeatedPush));
        Assert.Equal(new Vector2(-2, 0), repeatedPush);
        Assert.False(Geometry.SatOverlap(a, Box(10, 0, 10), out var touching));
        Assert.Equal(Vector2.Zero, touching);
    }

    [Fact]
    public void Sat_RejectsDegeneratePolygons_InsteadOfReportingAnUnboundedPush()
    {
        Vector2[][] invalid = [[], [Vector2.Zero], [Vector2.Zero, Vector2.One],
            [Vector2.One, Vector2.One, Vector2.One], [new(1, 1), new(2, 2), new(3, 3)]];
        foreach (var polygon in invalid)
        {
            Assert.False(Geometry.SatOverlap(polygon, Box(0, 0, 10), out var push));
            Assert.Equal(Vector2.Zero, push);
            Assert.False(Geometry.SatOverlap(Box(0, 0, 10), polygon, out _));
        }
    }

    [Fact]
    public void Sat_RotatedContainedPolygon_EscapesWithMinimalPush()
    {
        Vector2[] diamond = [new(5, 3), new(7, 5), new(5, 7), new(3, 5)];
        var outer = Box(0, 0, 10);
        Assert.True(Geometry.SatOverlap(diamond, outer, out var push));
        Assert.Equal(7, push.Length(), 4);
        Assert.False(Geometry.SatOverlap(diamond.Select(p => p + push).ToArray(), outer, out _));
        Assert.True(Geometry.SatOverlap(diamond.Select(p => p + push * 0.999f).ToArray(), outer, out _));
    }

    [Fact]
    public void Rays_ReportTangent_Inside_ParallelAndBehindCases()
    {
        var ray = new Ray2D(Vector2.Zero, Vector2.UnitX);
        Assert.True(ray.Intersects(new Circle(new Vector2(5, 2), 2), out var tangent));
        Assert.Equal(5, tangent, 5);
        Assert.True(ray.Intersects(new RectangleF(-1, -1, 2, 2), out var inside));
        Assert.Equal(0, inside);
        Assert.False(ray.Intersects(new RectangleF(-5, -1, 2, 2), out _));
        Assert.False(ray.Intersects(new RectangleF(5, 1, 2, 2), out _));
        Assert.True(ray.Intersects(new RectangleF(5, 0, 2, 2), out var border));
        Assert.Equal(5, border, 5);
    }

    [Fact]
    public void OfflineGuide_CompilesAndRunsContainmentAndTouchInTheRealRuntime()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "colisao.md"))) root = root.Parent;
        Assert.NotNull(root);
        var guide = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "colisao.md"));
        var source = guide.Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var result = compiler.Compile("CollisionGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols);
        var backend = new RecordingBackend();
        var host = new GameHost(loaded.Game, backend);
        Assert.True(host.Start(360, 640));
        host.Tick(1.0 / 60);
        var position = loaded.Game.GetType().GetField("position", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.Equal(new Vector2(68, 220), position.GetValue(loaded.Game));
        host.SetSurfaceTouches([new Lunet.Input.TouchPoint(0, Lunet.Input.TouchPhase.Moved, new Vector2(20, 100))]);
        host.Tick(1.0 / 60);
        Assert.Equal(new Vector2(20, 100), position.GetValue(loaded.Game));
        Assert.NotEmpty(backend.Batches);
        Assert.False(host.IsFaulted);
    }

    [Fact]
    public void CollisionQueries_DoNotAllocateInTheFrameLoop()
    {
        var a = Box(2, 3, 2);
        var b = Box(0, 0, 10);
        var rect = new RectangleF(0, 0, 10, 10);
        var circle = new Circle(Vector2.One, 2);
        void Queries()
        {
            Geometry.SatOverlap(a, b, out _);
            Geometry.Distance(circle, rect);
            Geometry.Intersection(rect, rect, out _);
        }
        for (var i = 0; i < 1000; i++) Queries();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) Queries();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
