using System.Numerics;
using Lunet.Physics;

namespace Lunet.Tests;

public sealed class AdvancedPhysicsTests
{
    [Fact]
    public void CapsuleAndSegment_ProduceValidFixtures_AndRaycast()
    {
        using var world = new PhysicsWorld(Vector2.Zero);
        var staticBody = world.CreateBody(PhysicsBodyType.Static, new Vector2(4, 0));
        var capsule = staticBody.AddFixture(Collider2D.Capsule(0.5f, 2f));
        Assert.True(world.Raycast(Vector2.Zero, Vector2.UnitX, 10f, out var hit));
        Assert.Same(capsule, hit.Fixture);
        Assert.InRange(hit.Distance, 3.4f, 3.6f);
        var wall = world.CreateBody(PhysicsBodyType.Static, new Vector2(7, 0));
        var segment = wall.AddFixture(Collider2D.Segment(new Vector2(0, -2), new Vector2(0, 2)));
        Assert.True(segment.IsValid);
        Assert.Throws<ArgumentException>(() => Collider2D.Segment(Vector2.Zero, Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Collider2D.Capsule(0, 1));
        Assert.True(Collider2D.Capsule(0.5f, 0).IsCircle);
    }

    [Fact]
    public void SensorBeginsAndEnds_WhenVisitorMovesAway()
    {
        using var world = new PhysicsWorld(Vector2.Zero);
        var sensorBody = world.CreateBody(PhysicsBodyType.Static, Vector2.Zero);
        var sensor = sensorBody.AddFixture(Collider2D.Box(4, 4), sensor: true);
        var body = world.CreateBody(PhysicsBodyType.Dynamic, new Vector2(-4, 0));
        var visitor = body.AddFixture(Collider2D.Circle(0.3f));
        body.LinearVelocity = new Vector2(8, 0);
        bool entered = false, exited = false;
        for (int i = 0; i < 100; i++)
        {
            world.Step(1f / 60f);
            if (world.SensorBeginCount > 0)
            {
                Span<Contact> buffer = stackalloc Contact[1];
                Assert.Equal(1, world.CopySensorBegins(buffer));
                Assert.Same(sensor, buffer[0].A);
                Assert.Same(visitor, buffer[0].B);
                entered = true;
            }
            if (world.SensorEndCount > 0) exited = true;
        }
        Assert.True(sensor.IsSensor);
        Assert.True(entered);
        Assert.True(exited);
    }

    [Fact]
    public void PhysicalContactsEndAfterSeparatingTheBodies()
    {
        using var world = new PhysicsWorld(Vector2.Zero);
        var fixedBody = world.CreateBody(PhysicsBodyType.Static, Vector2.Zero);
        fixedBody.AddFixture(Collider2D.Box(3, 3));
        var dynamic = world.CreateBody(PhysicsBodyType.Dynamic, new Vector2(0, -2));
        dynamic.AddFixture(Collider2D.Circle(0.75f));
        dynamic.LinearVelocity = new Vector2(0, 1);
        bool began = false, ended = false;
        for (int i = 0; i < 70; i++)
        {
            world.Step(1f / 60f);
            if (world.ContactBeginCount > 0) began = true;
        }
        dynamic.SetTransform(new Vector2(0, -10), 0);
        world.Step(1f / 60f);
        ended = world.ContactEndCount > 0;
        Assert.True(began);
        Assert.True(ended);
    }

    [Fact]
    public void CollisionCategoryAndRaycastFiltersDoNotLeakBackend()
    {
        using var world = new PhysicsWorld(Vector2.Zero);
        var body = world.CreateBody(PhysicsBodyType.Static, new Vector2(4, 0));
        var fixture = body.AddFixture(Collider2D.Circle(1));
        fixture.SetCollisionFilter(categoryBits: 2, maskBits: 1);
        Assert.False(world.RaycastFiltered(Vector2.Zero, Vector2.UnitX, 10, 1, 1, out _));
        Assert.True(world.RaycastFiltered(Vector2.Zero, Vector2.UnitX, 10, 1, 2, out var hit));
        Assert.Same(fixture, hit.Fixture);
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.SetCollisionFilter(0));
        fixture.Destroy();
        Assert.False(fixture.IsValid);
        Assert.Throws<ObjectDisposedException>(() => fixture.SetCollisionFilter(1));
        Assert.Equal(1, world.BodyCount);
    }

    [Fact]
    public void RevoluteAndPrismaticJointsKeepConstraintsDuringSimulation()
    {
        using var world = new PhysicsWorld(new Vector2(0, 10));
        var anchor = world.CreateBody(PhysicsBodyType.Static, Vector2.Zero);
        var pendulum = world.CreateBody(PhysicsBodyType.Dynamic, new Vector2(0, 2));
        pendulum.AddFixture(Collider2D.Circle(0.3f));
        var revolute = world.CreateRevoluteJoint(anchor, pendulum, Vector2.Zero);
        Assert.True(revolute.IsValid);
        var slider = world.CreateBody(PhysicsBodyType.Dynamic, new Vector2(5, 0));
        slider.AddFixture(Collider2D.Circle(0.3f));
        var rail = world.CreatePrismaticJoint(anchor, slider, new Vector2(5, 0), Vector2.UnitX);
        Assert.True(rail.IsValid);
        for (int i = 0; i < 120; i++) world.Step(1f / 60f);
        Assert.InRange(pendulum.Position.Length(), 1.7f, 2.3f);
        Assert.InRange(MathF.Abs(slider.Position.Y), 0f, 0.4f);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.CreatePrismaticJoint(anchor, slider, Vector2.Zero, Vector2.Zero));
        rail.Destroy();
        Assert.False(rail.IsValid);
    }

    [Fact]
    public void BodyAngleForcesAndInvalidInputsAreChecked()
    {
        using var world = new PhysicsWorld(Vector2.Zero);
        var b = world.CreateBody(PhysicsBodyType.Dynamic, Vector2.Zero);
        b.AddFixture(Collider2D.Box(2, 1));
        b.SetTransform(new Vector2(2, 3), MathF.PI / 3);
        Assert.InRange(b.Rotation, 1.04f, 1.05f);
        b.AngularVelocity = 0.2f;
        b.ApplyForce(new Vector2(30, 0));
        world.Step(1f / 60f);
        Assert.True(b.LinearVelocity.X > 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => b.SetTransform(Vector2.Zero, float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => b.AngularVelocity = float.NaN);
    }
}
