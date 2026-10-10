using System.Numerics;
using Lunet.Physics;

namespace Lunet.Tests;

public sealed class PhysicsWorldTests
{
    [Fact]
    public void DynamicBallFallsOntoGround_WithoutPassingThrough()
    {
        using var world = new PhysicsWorld(new Vector2(0, 10));
        var ground = world.CreateBody(PhysicsBodyType.Static, new Vector2(0, 3));
        ground.AddFixture(Collider2D.Box(20, 1));
        var ball = world.CreateBody(PhysicsBodyType.Dynamic, Vector2.Zero);
        ball.AddFixture(Collider2D.Circle(0.5f));
        bool collided = false;
        for (int i = 0; i < 180; i++)
        {
            world.Step(1f / 60f);
            collided |= world.ContactBeginCount > 0;
        }
        Assert.True(collided);
        Assert.InRange(ball.Position.Y, 1.8f, 2.1f);
        Assert.True(ball.Mass > 0);
        Assert.Equal(2, world.BodyCount);
    }

    [Fact]
    public void ImpulseAndGravityAreAppliedOnlyToDynamicBody()
    {
        using var world = new PhysicsWorld(Vector2.Zero);
        var dynamic = world.CreateBody(PhysicsBodyType.Dynamic, Vector2.Zero);
        dynamic.AddFixture(Collider2D.Box(1, 1));
        dynamic.ApplyImpulse(new Vector2(10, 0));
        world.Step(1f / 60f);
        Assert.True(dynamic.Position.X > 0);
        var staticBody = world.CreateBody(PhysicsBodyType.Static, new Vector2(8, 3));
        staticBody.AddFixture(Collider2D.Box(1, 1));
        world.Gravity = new Vector2(0, 10);
        for (int i = 0; i < 40; i++) world.Step(1f / 60f);
        Assert.Equal(3f, staticBody.Position.Y);
    }

    [Fact]
    public void JointAndRaycastWork_AndDestroyInvalidatesHandles()
    {
        using var world = new PhysicsWorld(Vector2.Zero);
        var a = world.CreateBody(PhysicsBodyType.Dynamic, new Vector2(0, 0));
        a.AddFixture(Collider2D.Circle(1));
        var b = world.CreateBody(PhysicsBodyType.Dynamic, new Vector2(4, 0));
        var fixture = b.AddFixture(Collider2D.Box(1, 1));
        var joint = world.CreateDistanceJoint(a, b, 4);
        Assert.True(joint.IsValid);
        Assert.True(world.Raycast(new Vector2(2, 0), Vector2.UnitX, 4, out var hit));
        Assert.Same(fixture, hit.Fixture);
        Assert.InRange(hit.Distance, 1.4f, 1.6f);
        b.Destroy();
        Assert.False(fixture.IsValid);
        Assert.False(joint.IsValid);
        Assert.Equal(1, world.BodyCount);
    }

    [Fact]
    public void GuardsRejectNaNsInvalidShapesAndCrossWorldJoints()
    {
        using var world = new PhysicsWorld(Vector2.Zero);
        using var other = new PhysicsWorld(Vector2.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => Collider2D.Circle(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Collider2D.Box(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.CreateBody(PhysicsBodyType.Dynamic, new Vector2(float.NaN, 0)));
        var a = world.CreateBody(PhysicsBodyType.Dynamic, Vector2.Zero);
        var b = other.CreateBody(PhysicsBodyType.Dynamic, Vector2.Zero);
        Assert.Throws<ArgumentException>(() => world.CreateDistanceJoint(a, b, 2));
        Assert.Throws<ArgumentException>(() => a.AddFixture(default));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Raycast(Vector2.Zero, Vector2.Zero, 1, out _));
    }

    [Fact]
    public void DisposeIsIdempotent_AndRejectsStaleAccess()
    {
        var world = new PhysicsWorld(Vector2.Zero);
        var body = world.CreateBody(PhysicsBodyType.Dynamic, Vector2.Zero);
        world.Dispose();
        world.Dispose();
        Assert.True(world.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = body.Position);
        Assert.Throws<ObjectDisposedException>(() => world.Step(1f/60));
    }
}
