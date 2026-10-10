using System.Numerics;
using Lunet.Graphics;
using Lunet.UI;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class UiStackLayoutTests
{
    private static void Near(RectangleF actual, float x, float y, float width, float height)
    {
        Assert.InRange(actual.X, x - 0.001f, x + 0.001f);
        Assert.InRange(actual.Y, y - 0.001f, y + 0.001f);
        Assert.InRange(actual.Width, width - 0.001f, width + 0.001f);
        Assert.InRange(actual.Height, height - 0.001f, height + 0.001f);
    }

    [Fact]
    public void VerticalDistributesFixedAndFlexWithoutChangingParent()
    {
        var specs = new[] { UiStackItem.Fixed(40), UiStackItem.Flex(), UiStackItem.Fixed(20) };
        var result = new RectangleF[4];
        var layout = UiStackLayout.Vertical(5, 10);
        Assert.False(layout.IsHorizontal);
        Assert.Equal(5, layout.Spacing);
        Assert.Equal(10, layout.Padding);
        layout.Arrange(new RectangleF(0, 0, 300, 200), specs, result);
        Near(result[0], 10, 10, 280, 40);
        Near(result[1], 10, 55, 280, 110);
        Near(result[2], 10, 170, 280, 20);
        Assert.Equal(default, result[3]);
        Assert.True(specs[1].IsFlexible);
        Assert.Equal(1, specs[1].Extent);
    }

    [Fact]
    public void HorizontalSupportsWeightedFlexAndCrossAxisAlignment()
    {
        var spec = new[] { UiStackItem.Fixed(80, 40), UiStackItem.Flex(2, 50), UiStackItem.Flex(1) };
        var result = new RectangleF[3];
        var layout = UiStackLayout.Horizontal(6, 10, UiStackAlignment.Center);
        Assert.True(layout.IsHorizontal);
        Assert.Equal(UiStackAlignment.Center, layout.Alignment);
        layout.Arrange(new(0, 0, 400, 120), spec, result);
        Near(result[0], 10, 40, 80, 40);
        Near(result[1], 96, 35, 192, 50);
        Near(result[2], 294, 10, 96, 100);
        layout = UiStackLayout.Horizontal(6, 10, UiStackAlignment.End);
        layout.Arrange(new(0, 0, 400, 120), spec, result);
        Near(result[0], 10, 70, 80, 40);
        Near(result[1], 96, 60, 192, 50);
        Near(result[2], 294, 10, 96, 100);
    }

    [Fact]
    public void FixedOverflowPreservesHeightsAndFlexCollapses()
    {
        var specs = new[] { UiStackItem.Fixed(50), UiStackItem.Flex(), UiStackItem.Fixed(20) };
        var result = new RectangleF[3];
        UiStackLayout.Vertical(spacing: 10).Arrange(new(0, 0, 80, 40), specs, result);
        Near(result[0], 0, 0, 80, 50);
        Near(result[1], 0, 60, 80, 0);
        Near(result[2], 0, 70, 80, 20);
        UiStackLayout.Vertical(padding: 20).Arrange(new(0, 0, 0, 0), [UiStackItem.Flex()], result);
        Near(result[0], 0, 0, 0, 0);
    }

    [Fact]
    public void ResizeReflowsInsteadOfKeepingStaleTouchBounds()
    {
        var layout = UiStackLayout.Vertical(12, 8);
        var specs = new[] { UiStackItem.Fixed(50), UiStackItem.Flex(), UiStackItem.Fixed(50) };
        var result = new RectangleF[3];
        var button = new TouchButton(new RectangleF(0, 0, 10, 10));
        layout.Arrange(new RectangleF(0, 0, 360, 640), specs, result);
        button.Bounds = result[0];
        Near(button.Bounds, 8, 8, 344, 50);
        layout.Arrange(new RectangleF(20, 30, 880, 330), specs, result);
        button.Bounds = result[0];
        Near(button.Bounds, 28, 38, 864, 50);
        Near(result[2], 28, 302, 864, 50);
    }

    [Fact]
    public void AlignmentStretchOverridesExplicitCrossWidth()
    {
        var items = new[] { UiStackItem.Fixed(40, 12) };
        var rectangles = new RectangleF[1];
        UiStackLayout.Vertical(0, 8, UiStackAlignment.Stretch).Arrange(new(10, 20, 200, 100), items, rectangles);
        Near(rectangles[0], 18, 28, 184, 40);
        UiStackLayout.Vertical(0, 8, UiStackAlignment.Start).Arrange(new(10, 20, 200, 100), items, rectangles);
        Near(rectangles[0], 18, 28, 12, 40);
    }

    [Fact]
    public void InvalidOptionsAndBrokenGeometriesAreRejected()
    {
        foreach (float v in new[] { -1f, float.NaN, float.PositiveInfinity })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UiStackItem.Fixed(v));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiStackItem.Fixed(5, v));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiStackItem.Flex(v));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiStackItem.Flex(2, v));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiStackLayout.Vertical(v));
            Assert.Throws<ArgumentOutOfRangeException>(() => UiStackLayout.Horizontal(0, v));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => UiStackItem.Flex(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => UiStackLayout.Vertical(0, 0, (UiStackAlignment)90));
        var specs = new[] { UiStackItem.Fixed(10), UiStackItem.Flex(1) };
        var one = new RectangleF[1];
        var layout = UiStackLayout.Vertical();
        Assert.Throws<ArgumentException>(() => layout.Arrange(new(0, 0, 100, 100), specs, one));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Arrange(new(float.NaN, 0, 100, 50), specs, new RectangleF[2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => layout.Arrange(new(0, 0, -1, 50), specs, new RectangleF[2]));
        Assert.Throws<OverflowException>(() => layout.Arrange(new(float.MaxValue, 0, float.MaxValue, 50), specs, new RectangleF[2]));
        Assert.Throws<OverflowException>(() => layout.Arrange(new(0, float.MaxValue - 64, 100, 64), [UiStackItem.Fixed(500)], new RectangleF[1]));
    }

    [Fact]
    public void HotPathHasNoManagedAllocationsEvenOnChangingScreenSize()
    {
        var layout = UiStackLayout.Vertical(spacing: 6, padding: 8);
        var specs = new[] { UiStackItem.Fixed(44), UiStackItem.Flex(2), UiStackItem.Fixed(36), UiStackItem.Flex(1) };
        var rects = new RectangleF[4];
        void Loop() { layout.Arrange(new(0, 0, 360, 640), specs, rects); layout.Arrange(new(2, 20, 860, 360), specs, rects); }
        for (var i = 0; i < 100; i++) Loop();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) Loop();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void OfflineGameExampleCompilesAndRunsWithRealGameHost()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "docs", "guides", "stack-ui.md")))
            root = root.Parent;
        Assert.NotNull(root);
        var guide = File.ReadAllText(Path.Combine(root!.FullName, "docs", "guides", "stack-ui.md"));
        var code = guide.Split("```csharp\n")[1].Split("```")[0];
        var compiler = new Lunet.Compiler.GameCompiler(
            new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        var built = compiler.Compile("StackUiGuide", [new Lunet.Compiler.SourceFile("Game.cs", code)]);
        Assert.True(built.Success, string.Join("\n", built.Diagnostics));
        Assert.DoesNotContain(built.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
        using var loaded = Lunet.Runtime.GameLoader.Load(built.Assembly!, built.Symbols);
        var backend = new RecordingBackend();
        var host = new GameHost(loaded.Game, backend);
        Assert.True(host.Start(360, 640), host.Fault?.ToString());
        host.Tick(1.0 / 60);
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        Assert.NotEmpty(backend.Batches);
        host.Resize(800, 360);
        host.Tick(1.0 / 60);
        Assert.False(host.IsFaulted, host.Fault?.ToString());
        host.Stop();
    }
}
