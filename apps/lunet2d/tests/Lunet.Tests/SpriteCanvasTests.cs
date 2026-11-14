using Lunet.Studio;

namespace Lunet.Tests;

/// <summary>LUNET-486: binary per-frame undo history, brush size and HSV colors for the Sprite Studio.</summary>
public sealed class SpriteCanvasTests
{
    [Fact]
    public void FrameCheckpoints_KeepHundredsOfStepsForLargestSprite()
    {
        var doc = SpriteDocument.Create(128);
        for (int i = 1; i < 64; i++) doc.AddFrame();
        var history = new SpriteHistory();
        for (int step = 0; step < 150; step++)
        {
            history.CheckpointFrame(doc, step % 64);
            doc.Frames[step % 64][step] = 0xff000000u | (uint)step;
        }
        Assert.True(history.UndoCount >= 100, $"apenas {history.UndoCount} passos");
        Assert.True(history.Bytes <= SpriteHistory.MaxBytes);
        for (int step = 149; step >= 100; step--)
        {
            doc = history.Undo(doc);
            Assert.Equal(0u, doc.Frames[step % 64][step]);
        }
        doc = history.Redo(doc);
        Assert.Equal(0xff000000u | 100u, doc.Frames[100 % 64][100]);
    }

    [Fact]
    public void FullCheckpoint_RestoresStructureAndRollbackDiscardsCancelledStroke()
    {
        var doc = SpriteDocument.Create(8);
        var history = new SpriteHistory();
        history.Checkpoint(doc);
        doc.AddFrame(); doc.Fps = 20;
        doc = history.Undo(doc);
        Assert.Single(doc.Frames);
        Assert.Equal(8, doc.Fps);
        Assert.True(history.CanRedo);

        history.CheckpointFrame(doc, 0);
        doc.Frames[0][3] = 0xffffffff;
        doc = history.Rollback(doc);
        Assert.Equal(0u, doc.Frames[0][3]);
        Assert.False(history.CanRedo); // a new gesture clears redo; rolling it back never invents a redo step
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void BrushSize_PaintsSquareAndClipsAtEdges()
    {
        var doc = SpriteDocument.Create(8);
        doc.Brush(0, 0, 0, 0, 0, 0xffff0000, size: 3);
        Assert.Equal(4, doc.Frames[0].Count(p => p == 0xffff0000)); // clipped 2x2 at the corner
        doc.Brush(0, 4, 4, 4, 4, 0xff00ff00, size: 3);
        Assert.Equal(9, doc.Frames[0].Count(p => p == 0xff00ff00));
        doc.Brush(0, 0, 7, 7, 7, 0xff0000ff, size: 1);
        Assert.Equal(8, doc.Frames[0].Count(p => p == 0xff0000ff));
    }

    [Theory]
    [InlineData(0f, 1f, 1f, 0xffff0000u)]
    [InlineData(120f, 1f, 1f, 0xff00ff00u)]
    [InlineData(240f, 1f, 1f, 0xff0000ffu)]
    [InlineData(0f, 0f, 1f, 0xffffffffu)]
    [InlineData(0f, 0f, 0f, 0xff000000u)]
    public void Hsv_RoundTripsWithArgb(float h, float s, float v, uint argb)
    {
        Assert.Equal(argb, SpriteColor.FromHsv(h, s, v, 255));
        var (h2, s2, v2, a) = SpriteColor.ToHsv(argb);
        Assert.Equal(argb, SpriteColor.FromHsv(h2, s2, v2, a));
    }

    [Fact]
    public void ParseHex_AcceptsRgbAndArgb()
    {
        Assert.Equal(0xffff8800u, SpriteColor.ParseHex("#FF8800"));
        Assert.Equal(0x80ff8800u, SpriteColor.ParseHex("80ff8800"));
        Assert.Null(SpriteColor.ParseHex("xyz"));
        Assert.Equal("#FF8800", SpriteColor.ToHex(0xffff8800u));
        Assert.Equal("#80FF8800", SpriteColor.ToHex(0x80ff8800u));
    }

    [Fact]
    public void Palette_ExtractsDistinctOpaqueColorsByFrequency()
    {
        var doc = SpriteDocument.Create(4);
        doc.Brush(0, 0, 0, 3, 0, 0xff112233, 1);
        doc.Frames[0][5] = 0xffabcdef;
        var palette = SpriteColor.Extract(doc, 8);
        Assert.Equal(0xff112233u, palette[0]);
        Assert.Contains(0xffabcdefu, palette);
        Assert.DoesNotContain(0u, palette);
    }
}
