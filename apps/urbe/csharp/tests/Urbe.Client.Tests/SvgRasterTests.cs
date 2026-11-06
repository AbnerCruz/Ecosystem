using SkiaSharp;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>CST-169: the Urbe's own drawings (graphs, formulas) as PNG beside their vector original.</summary>
public sealed class SvgRasterTests
{
    private static (int Ink, int Blue) Count(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png);
        int ink = 0, blue = 0;
        for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                var c = bitmap.GetPixel(x, y);
                if (c.Red < 200 || c.Green < 200 || c.Blue < 200) ink++;
                if (c.Blue > 200 && c.Red < 140 && c.Green > 100 && c.Green < 180) blue++;
            }
        return (ink, blue);
    }

    [Fact]
    public void Graph_DrawsAxesTextAndTheCurveInItsColour()
    {
        var svg = PublicationOfficeSvg(FunctionPlot.Parse("título: Seno\ny = sin(x)\nx: -6..6").ToSvg(640, 400));
        var png = SvgRaster.ToPng(svg, 1280, 800);
        Assert.NotNull(png);
        using var bitmap = SKBitmap.Decode(png!.Png);
        Assert.Equal(1280, bitmap.Width);
        var (ink, blue) = Count(png.Png);
        Assert.True(ink > 5000, "nothing drawn: " + ink);
        Assert.True(blue > 1000, "the curve (#4f8cff) is missing: " + blue);
    }

    [Fact]
    public void Formula_PathsAreFilled_AndBrokenSvgIsRefused()
    {
        var math = MathRenderer.RenderSvg("\\frac{a}{b}", true, 18);
        Assert.True(math.Success);
        var png = SvgRaster.ToPng(math.Svg!, 200, 200);
        Assert.True(Count(png!.Png).Ink > 200);
        Assert.Null(SvgRaster.ToPng("<svg", 10, 10));
        Assert.Null(SvgRaster.ToPng("<svg xmlns='http://www.w3.org/2000/svg'/>", 0, 10));
    }

    private static string PublicationOfficeSvg(string svg) => svg.Replace("width='100%'", "width='640' height='400'").Replace("currentColor", "#25272c");
}
