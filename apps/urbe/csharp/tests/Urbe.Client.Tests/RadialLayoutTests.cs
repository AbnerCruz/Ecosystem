using Urbe.Client.Ui;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-043: the geometry of the phone's radial menu. The items sit on a quarter arc around the trigger in the bottom
/// right corner (up to the left, where the thumb reaches), always at the same places, and a press-drag-release picks
/// the item in the direction of the drag (a marking menu).
/// </summary>
public sealed class RadialLayoutTests
{
    [Fact]
    public void Positions_ThreeItemsGoFromStraightUpToStraightLeft()
    {
        var p = RadialLayout.Positions(3, 100);
        Assert.Equal(3, p.Count);
        Assert.Equal(0, p[0].X, 6); Assert.Equal(-100, p[0].Y, 6);          // up
        Assert.Equal(-70.710678, p[1].X, 4); Assert.Equal(-70.710678, p[1].Y, 4); // diagonal
        Assert.Equal(-100, p[2].X, 6); Assert.Equal(0, p[2].Y, 6);          // left
    }

    [Fact]
    public void Positions_OneItemIsInTheMiddleOfTheArc()
    {
        var p = Assert.Single(RadialLayout.Positions(1, 80));
        Assert.Equal(-56.568542, p.X, 4);
        Assert.Equal(-56.568542, p.Y, 4);
    }

    [Fact]
    public void Positions_AllAtTheSameDistance()
    {
        foreach (var p in RadialLayout.Positions(5, 128))
            Assert.Equal(128, Math.Sqrt(p.X * p.X + p.Y * p.Y), 6);
    }

    [Theory]
    [InlineData(0, -60, 0)]      // a drag up
    [InlineData(-50, -45, 1)]    // up and left
    [InlineData(-70, 5, 2)]      // left, a little low
    [InlineData(-20, -80, 0)]    // nearer up than the diagonal
    [InlineData(-80, -25, 2)]    // nearer left
    public void Pick_FollowsTheDirectionOfTheDrag(double dx, double dy, int expected) =>
        Assert.Equal(expected, RadialLayout.Pick(dx, dy, 3, deadZone: 24));

    [Theory]
    [InlineData(5, -5)]          // inside the dead zone: still a tap
    [InlineData(-10, -15)]
    public void Pick_NothingInsideTheDeadZone(double dx, double dy) =>
        Assert.Equal(-1, RadialLayout.Pick(dx, dy, 3, deadZone: 24));

    [Theory]
    [InlineData(60, 0)]          // to the right, away from the arc
    [InlineData(0, 60)]          // down
    [InlineData(50, 50)]
    public void Pick_NothingAwayFromTheArc(double dx, double dy) =>
        Assert.Equal(-1, RadialLayout.Pick(dx, dy, 3, deadZone: 24));

    [Fact]
    public void Pick_DraggedFarAwayCancels()
    {
        Assert.Equal(1, RadialLayout.Pick(-150, -150, 3, 24, maxDistance: 240));
        Assert.Equal(-1, RadialLayout.Pick(-300, -300, 3, 24, maxDistance: 240));
    }

    [Fact]
    public void Pick_NoItemsPicksNothing() => Assert.Equal(-1, RadialLayout.Pick(-50, -50, 0, 24));
}
