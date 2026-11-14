using Lunet.Studio;

namespace Lunet.Tests;

/// <summary>Seleção de pixels, movimento, escala e rotação (sprite studio). Operações puras, sem Android.</summary>
public class SpriteSelectionTests
{
    private const uint Red = 0xffff0000, Blue = 0xff0000ff;

    private static SpriteDocument Square(int size, int x, int y, int w, int h, uint color = Red)
    {
        var doc = SpriteDocument.Create(size);
        doc.Rectangle(0, x, y, x + w - 1, y + h - 1, color, filled: true);
        return doc;
    }

    [Fact]
    public void RectangleSelectionCoversInclusiveCornersAndClipsToImage()
    {
        var sel = SpriteSelection.FromRectangle(8, 8, 6, 6, 20, 20);
        Assert.Equal(4, sel.Count);
        Assert.Equal((6, 6, 2, 2), sel.Bounds);
        Assert.True(SpriteSelection.FromRectangle(8, 8, 30, 30, 40, 40).IsEmpty);
    }

    [Fact]
    public void LassoSelectsInsideOfPolygonOnly()
    {
        var triangle = new[] { (0, 0), (9, 0), (0, 9) };
        var sel = SpriteSelection.FromPolygon(10, 10, triangle);
        Assert.True(sel.Contains(1, 1));
        Assert.False(sel.Contains(8, 8));
        Assert.True(sel.Count > 15 && sel.Count < 60);
        Assert.True(SpriteSelection.FromPolygon(10, 10, [(1, 1), (2, 2)]).IsEmpty);
    }

    [Fact]
    public void MagicWandSelectsConnectedSameColorOnly()
    {
        var doc = Square(8, 0, 0, 2, 2);
        doc.Frames[0][7 * 8 + 7] = Red; // isolated pixel with the same color
        var sel = SpriteSelection.FromColor(doc, 0, 0, 0, tolerance: 0);
        Assert.Equal(4, sel.Count);
        Assert.False(sel.Contains(7, 7));
        Assert.Equal(59, SpriteSelection.FromColor(doc, 0, 5, 5, 0).Count); // transparent background: 64 - 4 - 1 isolated
    }

    [Fact]
    public void LiftClearsSourceAndCommitWithoutChangeRestoresIdentically()
    {
        var doc = Square(16, 4, 4, 3, 3);
        var before = (uint[])doc.Frames[0].Clone();
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(16, 16, 4, 4, 6, 6));
        Assert.Equal(9, floating.Compose().Count(p => p == Red));
        floating.Commit(doc, 0);
        Assert.Equal(before, doc.Frames[0]);
    }

    [Fact]
    public void MoveTranslatesPixelsAndLeavesNoTrail()
    {
        var doc = Square(16, 2, 2, 2, 2);
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(16, 16, 2, 2, 3, 3));
        floating.MoveBy(5, 3);
        floating.Commit(doc, 0);
        var px = doc.Frames[0];
        Assert.Equal(Red, px[5 * 16 + 7]);
        Assert.Equal(Red, px[6 * 16 + 8]);
        Assert.Equal(0u, px[2 * 16 + 2]);
        Assert.Equal(4, px.Count(p => p == Red));
    }

    [Fact]
    public void DuplicateKeepsTheOriginalInPlace()
    {
        var doc = Square(16, 1, 1, 2, 2);
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(16, 16, 1, 1, 2, 2), copy: true);
        floating.MoveBy(6, 0);
        floating.Commit(doc, 0);
        Assert.Equal(8, doc.Frames[0].Count(p => p == Red));
    }

    [Fact]
    public void MovingOutsideTheImageClipsInsteadOfWrappingOrThrowing()
    {
        var doc = Square(8, 5, 5, 3, 3);
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(8, 8, 5, 5, 7, 7));
        floating.MoveBy(2, 2); // only the top-left 1×1 of the 3×3 stays inside
        floating.Commit(doc, 0);
        Assert.Equal(1, doc.Frames[0].Count(p => p == Red));
        Assert.Equal(Red, doc.Frames[0][7 * 8 + 7]);
    }

    [Fact]
    public void Rotate90SwapsDimensionsAndMovesPixelsClockwise()
    {
        var doc = SpriteDocument.Create(8);
        doc.Frames[0][2 * 8 + 2] = Red;   // top-left of a 2×3 block
        doc.Frames[0][2 * 8 + 3] = Blue;  // top-right
        doc.Frames[0][4 * 8 + 2] = 0xff00ff00; // bottom-left
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(8, 8, 2, 2, 3, 4));
        Assert.Equal((2, 3), (floating.Width, floating.Height));
        floating.Rotate90(clockwise: true);
        Assert.Equal((3, 2), (floating.Width, floating.Height));
        var composed = floating.Compose();
        // Clockwise: old top-left goes to top-right, old top-right to bottom-right.
        var coords = Enumerable.Range(0, composed.Length).Where(i => composed[i] != 0).ToArray();
        Assert.Equal(3, coords.Length);
        int redAt = Array.IndexOf(composed, Red), blueAt = Array.IndexOf(composed, Blue);
        Assert.True(redAt % 8 > 0 && blueAt % 8 == redAt % 8 && blueAt / 8 == redAt / 8 + 1);
    }

    [Fact]
    public void FlipsMirrorTheFloatingPixelsAroundItsCenter()
    {
        var doc = SpriteDocument.Create(8);
        doc.Frames[0][1 * 8 + 1] = Red;
        doc.Frames[0][1 * 8 + 2] = Blue;
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(8, 8, 1, 1, 2, 1));
        floating.FlipHorizontal();
        var composed = floating.Compose();
        Assert.Equal(Blue, composed[1 * 8 + 1]);
        Assert.Equal(Red, composed[1 * 8 + 2]);
    }

    [Fact]
    public void ScaleDoublesTheFootprintWithNearestNeighbourAndCommitsInsideBounds()
    {
        var doc = Square(16, 6, 6, 2, 2);
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(16, 16, 6, 6, 7, 7));
        floating.SetScale(2, 2);
        floating.Commit(doc, 0);
        Assert.Equal(16, doc.Frames[0].Count(p => p == Red));
        Assert.Equal(Red, doc.Frames[0][5 * 16 + 5]);
        Assert.Equal(Red, doc.Frames[0][8 * 16 + 8]);
    }

    [Fact]
    public void FreeRotationKeepsPixelCountRoughlyAndNeverCorruptsOutsidePixels()
    {
        var doc = Square(32, 12, 12, 8, 8);
        doc.Frames[0][0] = Blue;
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(32, 32, 12, 12, 19, 19));
        floating.RotationDegrees = 45;
        floating.Commit(doc, 0);
        int red = doc.Frames[0].Count(p => p == Red);
        Assert.InRange(red, 52, 76);          // area is preserved within nearest-neighbour error
        Assert.Equal(Blue, doc.Frames[0][0]); // pixels outside the selection are untouched
    }

    [Fact]
    public void LassoMaskKeepsTransparentCornersOutOfTheFloatingLayer()
    {
        var doc = Square(16, 2, 2, 6, 6);
        var tri = SpriteSelection.FromPolygon(16, 16, [(2, 2), (7, 2), (2, 7)]);
        var floating = SpriteFloating.Lift(doc, 0, tri);
        floating.MoveBy(8, 8);
        floating.Commit(doc, 0);
        // The lower-right half of the original square must still be there.
        Assert.Equal(Red, doc.Frames[0][7 * 16 + 7]);
        Assert.Equal(0u, doc.Frames[0][2 * 16 + 2]);
        Assert.Equal(Red, doc.Frames[0][10 * 16 + 10]);
    }

    [Fact]
    public void FootprintTracksTheTransformedBoundsForTheHandles()
    {
        var doc = Square(32, 10, 10, 4, 4);
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(32, 32, 10, 10, 13, 13));
        var c = floating.Corners();
        Assert.Equal(4, c.Length);
        Assert.Equal((10f, 10f), c[0]);
        Assert.Equal((14f, 14f), c[2]);
        floating.SetScale(2, 1);
        c = floating.Corners();
        Assert.Equal((8f, 10f), c[0]);
        Assert.Equal((16f, 14f), c[2]);
    }

    [Fact]
    public void HistoryUndoRestoresTheFrameAfterACommittedTransform()
    {
        var doc = Square(16, 2, 2, 2, 2);
        var history = new SpriteHistory();
        history.CheckpointFrame(doc, 0);
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(16, 16, 2, 2, 3, 3));
        floating.MoveBy(4, 4);
        floating.Commit(doc, 0);
        doc = history.Undo(doc);
        Assert.Equal(Red, doc.Frames[0][2 * 16 + 2]);
        Assert.Equal(0u, doc.Frames[0][6 * 16 + 6]);
    }

    [Fact]
    public void OutlineOfARectangleHasOneEdgePerBorderCell()
    {
        var sel = SpriteSelection.FromRectangle(8, 8, 2, 2, 4, 3); // 3×2
        Assert.Equal(10, sel.OutlineSegments().Count);
        Assert.Empty(SpriteSelection.FromRectangle(8, 8, 20, 20, 21, 21).OutlineSegments());
    }

    [Fact]
    public void EraseClearsOnlySelectedPixels()
    {
        var doc = Square(8, 0, 0, 4, 4);
        SpriteSelection.FromRectangle(8, 8, 1, 1, 2, 2).Erase(doc, 0);
        Assert.Equal(12, doc.Frames[0].Count(p => p == Red));
        Assert.Equal(0u, doc.Frames[0][1 * 8 + 1]);
    }

    [Fact]
    public void StampLeavesACopyBehindAndKeepsFloating()
    {
        var doc = Square(16, 2, 2, 2, 2);
        var floating = SpriteFloating.Lift(doc, 0, SpriteSelection.FromRectangle(16, 16, 2, 2, 3, 3));
        floating.Stamp(doc, 0);
        floating.MoveBy(6, 0);
        floating.Commit(doc, 0);
        Assert.Equal(8, doc.Frames[0].Count(p => p == Red));
        Assert.Equal(Red, doc.Frames[0][2 * 16 + 2]);
        Assert.Equal(Red, doc.Frames[0][2 * 16 + 8]);
    }
}
