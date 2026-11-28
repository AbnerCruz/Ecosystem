using System.Numerics;
using Lunet.Compiler;
using Lunet.Core;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Lunet.Tests;

public sealed class IntegerGeometryInteropTests
{
    [Fact]
    public void ConvertsWithoutChangingComponentMeaning()
    {
        Assert.Equal(new Vector2(-12, 7), new Point(-12, 7).ToVector2());
        Assert.Equal(new Vector2(32, 64), new Size(32, 64).ToVector2());
        Assert.Equal(Vector2.Zero, Size.Empty.ToVector2());
        Assert.Equal(new Vector2(-3, 0), new Size(-3, 0).ToVector2());
        Assert.Equal((float)int.MaxValue, new Point(int.MaxValue, 0).ToVector2().X);
    }

    [Fact]
    public void RectangleFormsAreEquivalentAndKeepContinuousGeometry()
    {
        Point position = new(-10, 20);
        Size size = new(32, 64);
        RectangleF expected = new(-10, 20, 32, 64);
        Assert.Equal(expected, new RectangleF(position, size));
        Assert.Equal(expected, new RectangleF(position.ToVector2(), size));
        Assert.Equal(expected, new RectangleF(position, size.ToVector2()));
        Assert.Equal(expected, new RectangleF(position.ToVector2(), size.ToVector2()));
        Assert.Equal(new RectangleF(0.5f, 1.25f, 32, 64), new RectangleF(new Vector2(0.5f, 1.25f), size));
        Assert.Equal(new RectangleF(-10, 20, 0.5f, 1.25f), new RectangleF(position, new Vector2(0.5f, 1.25f)));
        Assert.True(expected.Contains(position));
        Assert.False(expected.Contains(new Point(22, 84)));
        Assert.Equal(expected.Contains(position.ToVector2()), expected.Contains(position));
    }

    [Fact]
    public void OldUntypedCallsAndNewTypedCallsCompileInRealGameCompiler()
    {
        var compiler = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
        const string code = """
            public sealed class TestGame : Game { }
            public sealed class Coin
            {
                private Transform2D transform;
                public Coin(Point position, Size size)
                {
                    transform.Position = position.ToVector2();
                    transform.Scale = size.ToVector2();
                }
                public RectangleF Rect => new(transform.Position, transform.Scale);
                public RectangleF[] Test(Point p, Size s, Vector2 v) => [
                    new RectangleF(p, s), new RectangleF(v, s), new RectangleF(p, v),
                    new RectangleF(new(0.5f, 1.25f), new(10.5f, 20.25f)),
                    new RectangleF(new(1, 2), new(3, 4)), new RectangleF(default, default),
                    new RectangleF(position: p, size: s), new RectangleF(position: default, size: s),
                    new RectangleF(p, new(2.5f, 3.5f))
                ];
                public bool Hit(RectangleF r) => r.Contains(new(1.5f, 2.5f)) || r.Contains(default);
                public Color Tint => Color.Red;
                public void Surface(GraphicsDevice graphics, Size s) => graphics.Resize(s);
            }
            """;
        var result = compiler.Compile("integer_geometry", [
            new SourceFile(ProjectImports.Path, ProjectImports.Source()),
            new SourceFile("Coin.cs", code)
        ]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
    }
}
