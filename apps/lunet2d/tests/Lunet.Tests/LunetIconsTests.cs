using Lunet.Studio;

namespace Lunet.Tests;

public class LunetIconsTests
{
    [Fact]
    public void EveryIconParsesAndStaysInsideTheGrid()
    {
        foreach (var name in LunetIcons.Names)
        {
            var icon = LunetIcons.Get(name);
            Assert.NotEmpty(icon.Parts);
            foreach (var part in icon.Parts)
            {
                Assert.NotEmpty(part.Segments);
                Assert.Equal(VectorOp.Move, part.Segments[0].Op);
                foreach (var s in part.Segments.Where(s => s.Op != VectorOp.Close))
                    foreach (var v in new[] { s.X1, s.Y1, s.X2, s.Y2, s.X3, s.Y3 })
                        Assert.InRange(v, -0.01f, VectorIcon.Grid + 0.01f);
            }
        }
    }

    [Fact]
    public void EveryLegacyGlyphMapsToAnExistingIcon()
    {
        foreach (var (glyph, name) in LunetIcons.GlyphMap)
            Assert.True(LunetIcons.TryGet(name, out _), $"{glyph} → {name} não existe");
    }

    [Theory]
    [InlineData("▶ Rodar", "play", "Rodar", false)]
    [InlineData("⌨\nCódigo", "code", "Código", true)]
    [InlineData("✦ IA", "sparkles", "IA", false)]
    [InlineData("↶", "undo", "", false)]
    public void LabelsWithALeadingGlyphSplitIntoIconAndText(string text, string expectedIcon, string expectedLabel, bool stacked)
    {
        Assert.True(LunetIcons.TrySplitLabel(text, out var icon, out var label, out var isStacked));
        Assert.Equal(expectedIcon, icon.Name);
        Assert.Equal(expectedLabel, label);
        Assert.Equal(stacked, isStacked);
    }

    [Theory]
    [InlineData("Salvar cópia WAV")]
    [InlineData("1px")]
    [InlineData("")]
    public void PlainTextIsLeftAlone(string text) => Assert.False(LunetIcons.TrySplitLabel(text, out _, out _, out _));

    [Fact]
    public void ShapeCommandsExpandIntoClosedPaths()
    {
        var circle = VectorIcon.Parse("c", "O 12 12 5");
        Assert.Equal(VectorOp.Close, circle.Parts[0].Segments[^1].Op);
        Assert.Equal(4, circle.Parts[0].Segments.Count(s => s.Op == VectorOp.Cubic));
        Assert.Throws<FormatException>(() => VectorIcon.Parse("bad", "M1 1 L"));
        Assert.Throws<FormatException>(() => VectorIcon.Parse("bad", "X1 1"));
        Assert.Contains("<path", VectorIcon.Parse("p", "M1 1 L2 2").ToSvg());
    }

    [Fact]
    public void SketchToolsHaveDistinctIcons()
    {
        var tools = new[] { "pencil", "eraser", "bucket", "picker", "line", "rect", "rect-filled", "select", "lasso", "wand", "move", "rotate" };
        Assert.Equal(tools.Length, tools.Select(t => LunetIcons.Get(t).ToSvg()).Distinct().Count());
    }
}
