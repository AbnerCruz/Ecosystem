using System.Numerics;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.Tests;

/// <summary>LUNET-503: several players on one device — split screen, touch zones, per-player cameras and turns.</summary>
public sealed class LocalMultiplayerTests
{
    [Theory]
    [InlineData(1, 640, 360)]
    [InlineData(2, 640, 360)]
    [InlineData(2, 360, 640)]
    [InlineData(3, 640, 360)]
    [InlineData(3, 360, 640)]
    [InlineData(4, 640, 360)]
    public void Layout_CoversTheScreenWithoutOverlap(int players, float width, float height)
    {
        var regions = SplitScreen.Layout(players, width, height, gap: 2);
        Assert.Equal(players, regions.Length);
        foreach (var r in regions)
        {
            Assert.True(r.Width > 0 && r.Height > 0);
            Assert.InRange(r.X, 0, width); Assert.InRange(r.Right, 0, width + 0.001f);
            Assert.InRange(r.Y, 0, height); Assert.InRange(r.Bottom, 0, height + 0.001f);
        }
        for (int i = 0; i < regions.Length; i++)
            for (int j = i + 1; j < regions.Length; j++)
                Assert.False(regions[i].Intersects(regions[j]), $"{i} e {j} se sobrepõem");
        var covered = regions.Sum(r => r.Width * r.Height);
        Assert.InRange(covered / (width * height), 0.97, 1.0);
    }

    [Fact]
    public void TwoPlayers_AreSideBySideInLandscape_AndStackedInPortrait()
    {
        var wide = SplitScreen.Layout(2, 640, 360);
        Assert.True(wide[1].X > wide[0].X);
        var tall = SplitScreen.Layout(2, 360, 640);
        Assert.True(tall[1].Y > tall[0].Y);
        Assert.Throws<ArgumentOutOfRangeException>(() => SplitScreen.Layout(5, 640, 360));
    }

    [Fact]
    public void Touches_BelongToThePlayerOfTheirRegion()
    {
        var regions = SplitScreen.Layout(2, 640, 360, gap: 4);
        TouchPoint[] touches = [new(1, TouchPhase.Pressed, new(100, 100)), new(2, TouchPhase.Moved, new(500, 50)), new(3, TouchPhase.Pressed, new(320, 10))];
        Assert.Equal([1], SplitScreen.TouchesOf(0, touches, regions).Select(t => t.Id));
        Assert.Equal([2], SplitScreen.TouchesOf(1, touches, regions).Select(t => t.Id));
        Assert.Equal(-1, SplitScreen.PlayerAt(new(320, 10), regions)); // the gap belongs to nobody
        Assert.Equal(new Vector2(500 - regions[1].X, 50), SplitScreen.ToRegion(new(500, 50), regions[1]));
    }

    [Fact]
    public void SpriteBatch_DrawsEachCameraCenteredAndClippedInItsRegion()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 640, 360);
        device.Resize(640, 360);
        var batch = new SpriteBatch(device);
        var texture = Texture2D.CreateSolid(device, 2, 2, Color.White);
        var regions = SplitScreen.Layout(2, 640, 360, gap: 0);
        var camera = new Camera2D { Position = new Vector2(1000, 1000) };
        batch.Begin(camera, regions[1]);
        batch.Draw(texture, new Vector2(1000, 1000), Color.White); // the camera's center
        batch.End();
        var quad = backend.Batches[^1].Vertices;
        var center = new Vector2(quad.Average(v => v.Position.X), quad.Average(v => v.Position.Y));
        Assert.Equal(regions[1].Center.X + 1, center.X, 1);
        Assert.Equal(regions[1].Center.Y + 1, center.Y, 1);
        Assert.Contains(backend.States, s => s.Scissor is { } clip && clip.X >= 319 && clip.Width <= 321);
        Assert.Throws<ArgumentOutOfRangeException>(() => batch.Begin(camera, new RectangleF(0, 0, 0, 10)));
    }

    [Fact]
    public void Turns_RotateSkipEliminatedAndCountRounds()
    {
        var turns = new TurnOrder(["Ana", "Bia", "Caio"]);
        Assert.Equal("Ana", turns.Current);
        Assert.True(turns.EndTurn(turns.Version));
        Assert.Equal("Bia", turns.Current);
        turns.Eliminate("Caio");
        Assert.True(turns.EndTurn(turns.Version));
        Assert.Equal("Ana", turns.Current);
        Assert.Equal(2, turns.Round);
        turns.Eliminate("Ana");
        Assert.Equal("Bia", turns.Current);
        Assert.True(turns.IsOver);
        Assert.False(turns.EndTurn(turns.Version));
        Assert.Throws<ArgumentException>(() => new TurnOrder(["A", "A"]));
    }

    [Fact]
    public void Turns_RejectMovesOnStaleVersions_AndSurviveJson()
    {
        var turns = new TurnOrder(["Ana", "Bia"]);
        var seenByBoth = turns.Version;
        Assert.True(turns.EndTurn(seenByBoth));
        Assert.False(turns.EndTurn(seenByBoth)); // the second, concurrent move is refused
        Assert.Equal("Bia", turns.Current);
        var again = TurnOrder.FromJson(turns.ToJson());
        Assert.Equal(turns.Current, again.Current);
        Assert.Equal(turns.Version, again.Version);
        Assert.Equal(turns.Round, again.Round);
        Assert.Throws<InvalidDataException>(() => TurnOrder.FromJson("""{"Players":["A"],"Current":5,"Round":1,"Version":0}"""));
        Assert.Throws<InvalidDataException>(() => TurnOrder.FromJson("nada"));
    }
}
