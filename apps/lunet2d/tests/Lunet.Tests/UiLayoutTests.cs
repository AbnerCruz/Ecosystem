using System.Numerics;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class UiLayoutTests
{
    [Fact]
    public void Stretch_RespectsAsymmetricMarginsAndTranslatedParent()
    {
        var layout = LayoutRect.Stretch(12, 20, 30, 40);
        Assert.Equal(new RectangleF(62, 100, 158, 240), layout.GetBounds(new(50, 80, 200, 300)));
        Assert.Equal(Vector2.Zero, layout.AnchorMin); Assert.Equal(Vector2.One, layout.AnchorMax);
        Assert.Equal(new Vector2(12, 20), layout.OffsetMin); Assert.Equal(new Vector2(-30, -40), layout.OffsetMax);
        Assert.Equal(new RectangleF(10, 20, 0, 0), default(LayoutRect).GetBounds(new(10, 20, 30, 40)));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 50, 80)]
    [InlineData(0.5f, 0.5f, 0.5f, 0.5f, 100, 210)]
    [InlineData(1, 1, 1, 1, 150, 340)]
    public void Fixed_UsesAnchorAndPivotWhileKeepingSize(float ax, float ay, float px, float py, float x, float y)
    {
        var layout = LayoutRect.Fixed(new(ax, ay), new(100, 40), new(px, py));
        Assert.Equal(new RectangleF(x, y, 100, 40), layout.GetBounds(new(50, 80, 200, 300)));
        Assert.Equal(new RectangleF(x + 3, y - 7, 100, 40), LayoutRect.Fixed(new(ax, ay), new(100, 40), new(px, py), new(3, -7)).GetBounds(new(50, 80, 200, 300)));
    }

    [Fact]
    public void PartialStretchAndNestedLayouts_RecomputeAfterParentResize()
    {
        var header = new LayoutRect(Vector2.Zero, new(1, 0), new(12, 8), new(-20, 48));
        Assert.Equal(new RectangleF(22, 28, 168, 40), header.GetBounds(new(10, 20, 200, 300)));
        Assert.Equal(new RectangleF(22, 28, 368, 40), header.GetBounds(new(10, 20, 400, 500)));
        var content = LayoutRect.Stretch(12, 60, 12, 68);
        var button = LayoutRect.Fixed(Vector2.One, new(120, 44), Vector2.One, new(-8, -8));
        var wide = content.GetBounds(new(16, 120, 328, 480));
        var narrow = content.GetBounds(new(16, 120, 180, 360));
        Assert.Equal(new RectangleF(204, 480, 120, 44), button.GetBounds(wide));
        Assert.Equal(new RectangleF(56, 360, 120, 44), button.GetBounds(narrow));
        Assert.True(button.GetBounds(wide).Contains(new(210, 490)));
        Assert.False(button.GetBounds(narrow).Contains(new(210, 490)));
    }

    [Fact]
    public void SmallAndZeroParents_CollapseInvertedLimitsWithoutTouchHits()
    {
        var layout = LayoutRect.Stretch(12, 20, 30, 40);
        var collapsed = layout.GetBounds(new(5, 7, 10, 10));
        Assert.Equal(new RectangleF(17, 27, 0, 0), collapsed);
        Assert.False(collapsed.Contains(collapsed.Position));
        Assert.Equal(new RectangleF(17, 27, 0, 0), layout.GetBounds(new(5, 7, 0, 0)));
        Assert.Equal(new RectangleF(17, 27, 58, 0), layout.GetBounds(new(5, 7, 100, 10)));
        Assert.Equal(new RectangleF(17, 27, 0, 40), layout.GetBounds(new(5, 7, 10, 100)));
    }

    [Fact]
    public void OffsetsCanPlaceContentOutsideParent_WithoutImplicitClipping()
    {
        var layout = LayoutRect.Fixed(Vector2.Zero, new(20, 30), Vector2.One);
        Assert.Equal(new RectangleF(-10, -10, 20, 30), layout.GetBounds(new(10, 20, 100, 100)));
        Assert.Equal(new RectangleF(10, 20, 0, 0), LayoutRect.Fixed(Vector2.Zero, Vector2.Zero).GetBounds(new(10, 20, 100, 100)));
    }

    [Fact]
    public void InvalidAnchorsOffsetsMarginsSizesAndParents_AreRejected()
    {
        foreach (var v in new[] { new Vector2(-0.1f, 0), new Vector2(0, 1.1f), new Vector2(float.NaN, 0), new Vector2(0, float.PositiveInfinity) })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutRect(v, Vector2.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutRect(Vector2.Zero, v));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayoutRect.Fixed(v, Vector2.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayoutRect.Fixed(Vector2.Zero, Vector2.One, v));
        }
        Assert.Throws<ArgumentException>(() => new LayoutRect(new(0.8f, 0), new(0.2f, 1)));
        Assert.Throws<ArgumentException>(() => new LayoutRect(new(0, 0.8f), new(1, 0.2f)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutRect(Vector2.Zero, Vector2.One, new(float.NaN, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutRect(Vector2.Zero, Vector2.One, offsetMax: new(0, float.PositiveInfinity)));
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutRect.Fixed(Vector2.Zero, new(-1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutRect.Fixed(Vector2.Zero, new(1, float.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutRect.Fixed(Vector2.Zero, Vector2.One, offset: new(float.PositiveInfinity, 0)));
        foreach (float bad in new[] { -1, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LayoutRect.Stretch(left: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayoutRect.Stretch(top: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayoutRect.Stretch(right: bad));
            Assert.Throws<ArgumentOutOfRangeException>(() => LayoutRect.Stretch(bottom: bad));
        }
        var layout = LayoutRect.Stretch();
        foreach (var rect in new[] { new RectangleF(float.NaN, 0, 1, 1), new RectangleF(0, 0, -1, 1), new RectangleF(0, 0, 1, -1), new RectangleF(0, 0, 1, float.PositiveInfinity), new RectangleF(float.MaxValue, 0, float.MaxValue, 1) })
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.GetBounds(rect));
    }

    [Fact]
    public void ExtremeOffsetsAndResults_FailRatherThanReturnInfinity()
    {
        Assert.Throws<OverflowException>(() => LayoutRect.Fixed(Vector2.Zero, new(float.MaxValue, 1), Vector2.One, new(-float.MaxValue, 0)));
        var layout = new LayoutRect(Vector2.Zero, Vector2.Zero, new(float.MaxValue, 0), new(float.MaxValue, 10));
        Assert.Throws<OverflowException>(() => layout.GetBounds(new(float.MaxValue, 0, 0, 0)));
        var wide = new LayoutRect(Vector2.Zero, Vector2.One, new(-float.MaxValue, 0), new(float.MaxValue, 0));
        Assert.Throws<OverflowException>(() => wide.GetBounds(new(0, 0, 1, 1)));
        var huge = LayoutRect.Fixed(Vector2.Zero, new(float.MaxValue, float.MaxValue));
        var result = huge.GetBounds(default);
        Assert.Equal(float.MaxValue, result.Width); Assert.True(float.IsFinite(result.Right));
    }

    [Fact]
    public void LayoutResolutionAndFactories_DoNotAllocate()
    {
        var stretch = LayoutRect.Stretch(12, 12, 12, 12); var parent = new RectangleF(10, 20, 300, 500);
        void Resolve()
        {
            for (int j = 0; j < 500; j++)
            {
                var content = stretch.GetBounds(parent);
                LayoutRect.Fixed(Vector2.One, new(120, 44), Vector2.One, new(-12, -12)).GetBounds(content);
                LayoutRect.Stretch(1, 2, 3, 4).GetBounds(content);
            }
        }
        for (int i = 0; i < 100; i++) Resolve(); long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) Resolve(); Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineGuide_CompilesAndKeepsTouchAlignedAfterResizing()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "layout-ui.md"))) root = root.Parent;
        Assert.NotNull(root);
        var source = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "layout-ui.md")).Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var compiled = compiler.Compile("LayoutGuide", [new Lunet.Compiler.SourceFile("Game.cs", source)]);
        Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics)); Assert.DoesNotContain(compiled.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(compiled.Assembly!, compiled.Symbols);
        var backend = new RecordingBackend(); var host = new GameHost(loaded.Game, backend); Assert.True(host.Start(720, 1280)); host.Tick(1.0 / 60);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        object Field(string name) => loaded.Game.GetType().GetField(name, flags)!.GetValue(loaded.Game)!;
        void Touch(float x, float y)
        {
            host.SetSurfaceTouches([new Lunet.Input.TouchPoint(0, Lunet.Input.TouchPhase.Pressed, new(x * 2, y * 2))]); host.Tick(1.0 / 60);
            host.SetSurfaceTouches([]); host.Tick(1.0 / 60);
        }
        Assert.Equal(new RectangleF(212, 544, 120, 44), Field("actionBounds"));
        Touch(270, 565); Assert.Equal(1, Field("clicks"));
        Touch(100, 70); Assert.True((bool)Field("narrow")); Assert.Equal(new RectangleF(64, 424, 120, 44), Field("actionBounds"));
        Touch(270, 565); Assert.Equal(1, Field("clicks"));
        Touch(124, 446); Assert.Equal(2, Field("clicks"));
        var content = (RectangleF)Field("contentBounds"); var marker = (RectangleF)Field("markerBounds"); Assert.Equal(content.Center, marker.Center);
        Assert.Contains(backend.Batches, b => b.Vertices.Any(v => v.Position == new Vector2(64, 424)));
        Assert.False(host.IsFaulted); host.Stop();
    }
}
