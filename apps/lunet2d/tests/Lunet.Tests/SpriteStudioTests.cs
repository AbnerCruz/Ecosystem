using Lunet.Studio;

namespace Lunet.Tests;

public class SpriteStudioTests
{
    [Fact]
    public void SourceRoundTripPreservesFramesTimingAndTransparency()
    {
        var doc = SpriteDocument.Create(16); doc.Stroke(0, 0, 0, 15, 15, 0x80ff0022); doc.Fps = 12; doc.AddFrame(0);
        var reopened = SpriteDocument.Parse(doc.ToJson());
        Assert.Equal(doc.ToJson(), reopened.ToJson());
        Assert.Equal(16, reopened.Frames[0].Count(p => p != 0));
        reopened.Frames[1][0] = 0; Assert.NotEqual(reopened.Frames[0][0], reopened.Frames[1][0]);
    }
    [Fact]
    public void FillRespectsBarrierAndUndoRedoRestoresDeletedFrames()
    {
        var doc = SpriteDocument.Create(16); doc.Stroke(0, 8, 0, 8, 15, 0xffffffff); doc.Fill(0, 0, 0, 0xffff0000);
        Assert.Equal(128, doc.Frames[0].Count(p => p == 0xffff0000)); Assert.Equal(112, doc.Frames[0].Count(p => p == 0));
        doc.AddFrame(0); var history = new SpriteHistory(); history.Checkpoint(doc); doc.RemoveFrame(0);
        doc = history.Undo(doc); Assert.Equal(2, doc.Frames.Count); doc = history.Redo(doc); Assert.Single(doc.Frames);
        doc.RemoveFrame(0); Assert.Single(doc.Frames);
    }
    [Fact]
    public void HorizontalSheetUsesRuntimeFrameOrderAndRgbaChannels()
    {
        var doc = SpriteDocument.Create(1); doc.Frames[0][0] = 0x80442211; doc.AddFrame(); doc.Frames[1][0] = 0xffaabbcc;
        Assert.Equal(new byte[] { 0x44, 0x22, 0x11, 0x80, 0xaa, 0xbb, 0xcc, 0xff }, doc.SheetRgba());
    }
    [Theory]
    [InlineData("{\"Version\":2}")]
    [InlineData("{\"Width\":999999}")]
    [InlineData("{\"Frames\":[]}")]
    [InlineData("{\"Frames\":[null]}")]
    public void UnsupportedSourceIsRejectedWithoutRewriting(string json) => Assert.Throws<InvalidDataException>(() => SpriteDocument.Parse(json));
    [Fact]
    public void RectangleOutlineAndFilledRespectBoundsAndTransparency()
    {
        var doc = SpriteDocument.Create(8);
        doc.Rectangle(0, 1, 1, 5, 4, 0xffff00ff);
        Assert.Equal(14, doc.Frames[0].Count(p => p == 0xffff00ff));
        Assert.Equal(0u, doc.Frames[0][2 * 8 + 3]);
        doc.Rectangle(0, 2, 2, 4, 3, 0x8000ff11, filled: true);
        Assert.Equal(6, doc.Frames[0].Count(p => p == 0x8000ff11));
        doc.Rectangle(0, -100, -100, -1, -1, 0xffffffff, filled: true);
        doc.Rectangle(0, 20, 20, 40, 40, 0xffffffff);
        Assert.DoesNotContain(0xffffffffu, doc.Frames[0]);
        Assert.Equal(doc.ToJson(), SpriteDocument.Parse(doc.ToJson()).ToJson());
    }

    [Fact]
    public void MultiFrameSpriteSheetPreservesAnimatedFramesTimingAndAlpha()
    {
        var animation = SpriteDocument.Create(2);
        animation.Fps = 12;
        animation.Frames[0][0] = 0x80ff0000;
        animation.Frames[0][1] = 0xff00ff00;
        animation.AddFrame();
        animation.Frames[1][2] = 0xff112233;
        animation.AddFrame();
        animation.Frames[2][3] = 0x400000ff;

        var reopened = SpriteDocument.Parse(animation.ToJson());
        Assert.Equal(12, reopened.Fps);
        Assert.Equal(3, reopened.Frames.Count);
        Assert.Equal(6, reopened.Width * reopened.Frames.Count);
        Assert.Equal(2, reopened.Height);
        var bytes = reopened.SheetRgba();

        // Pixel (x, y) in frame N maps to column N*Width + x.
        void Pixel(int x, int y, byte[] rgba)
        {
            var start = (y * reopened.Width * reopened.Frames.Count + x) * 4;
            Assert.Equal(rgba, bytes.AsSpan(start, 4).ToArray());
        }
        Pixel(0, 0, [255, 0, 0, 128]);
        Pixel(1, 0, [0, 255, 0, 255]);
        Pixel(2, 0, [0, 0, 0, 0]);
        Pixel(2, 1, [17, 34, 51, 255]);
        Pixel(5, 1, [0, 0, 255, 64]);
    }

    [Fact]
    public void VerticalFlipDoesNotCorruptOtherFramesOrTransparency()
    {
        var doc = SpriteDocument.Create(3);
        doc.Frames[0][0] = 0x80ff0022;
        doc.Frames[0][8] = 0xffaabbcc;
        doc.AddFrame(0);
        doc.FlipVertical(0);
        Assert.Equal(0x80ff0022u, doc.Frames[0][6]);
        Assert.Equal(0xffaabbccu, doc.Frames[0][2]);
        Assert.Equal(0x80ff0022u, doc.Frames[1][0]);
        Assert.Equal(0xffaabbccu, doc.Frames[1][8]);
        doc.FlipVertical(0);
        Assert.Equal(doc.Frames[1], doc.Frames[0]);
    }

}
