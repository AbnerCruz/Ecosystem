using System.Numerics;
using System.Reflection;
using Lunet;
using Lunet.Core;
using Lunet.Input;
using Lunet.Physics;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public sealed class AdvancedPhysicsLaboratoryTests
{
    [Fact]
    public void LaboratoryJointsKeepConstraintsAfterImpulsePauseAndRestart()
    {
        using var lab = new PhysicsLab();
        lab.Tap(180, 198);
        Assert.Equal(1, lab.Read<int>("physicsScenario"));
        var world = lab.Read<PhysicsWorld>("physicsWorld");
        var pendulum = lab.Read<RigidBody2D>("physicsPendulum");
        var slider = lab.Read<RigidBody2D>("physicsSlider");
        var start = pendulum.Position;
        lab.Tick(60);
        Assert.True(Vector2.Distance(start, pendulum.Position) > 0.2f);
        Assert.InRange(Vector2.Distance(pendulum.Position, new Vector2(-2, -2)), 1.9f, 2.1f);
        Assert.InRange(slider.Position.Y, -1.02f, -0.98f);
        Assert.InRange(slider.Position.X, 1.5f, 2.5f);
        lab.Tap(180, 489);
        lab.Tick(180);
        Assert.InRange(slider.Position.Y, -1.02f, -0.98f);
        Assert.InRange(Vector2.Distance(pendulum.Position, new Vector2(-2, -2)), 1.9f, 2.1f);

        // Um botão pressionado antes da pausa não executa ao soltar depois dela.
        lab.Touch(TouchPhase.Pressed, 180, 198);
        lab.Host.Pause(); lab.Host.Resume();
        lab.Touch(TouchPhase.Released, 180, 198);
        Assert.Equal(1, lab.Read<int>("physicsScenario"));
        lab.Tap(180, 540);
        Assert.True(world.IsDisposed);
        Assert.NotSame(world, lab.Read<PhysicsWorld>("physicsWorld"));
        Assert.Throws<ObjectDisposedException>(() => pendulum.Position);
        Assert.InRange(lab.Read<RigidBody2D>("physicsSlider").Position.X, 1f, 1.2f);
    }

    [Fact]
    public void LaboratoryFilterChangesActualCollisionAndCountsSensorAndContactEnds()
    {
        using var lab = new PhysicsLab();
        lab.Tap(180, 198); lab.Tap(180, 198);
        Assert.Equal(2, lab.Read<int>("physicsScenario"));
        Assert.True(lab.Read<Fixture2D>("physicsVisitor").IsValid);
        lab.Tick(100);
        Assert.InRange(lab.Read<RigidBody2D>("physicsBall").Position.X, -0.7f, -0.5f);
        Assert.True(lab.Read<int>("physicsContactBegins") > 0);
        lab.Tick(70);
        Assert.True(lab.Read<int>("physicsContactEnds") > 0);
        Assert.True(lab.Read<int>("physicsSensorEvents") > 0);
        Assert.True(lab.Read<int>("physicsSensorEnds") > 0);
        Assert.True(lab.Read<bool>("physicsRayHit"));

        var oldWorld = lab.Read<PhysicsWorld>("physicsWorld");
        lab.Tap(180, 489);
        Assert.True(oldWorld.IsDisposed);
        Assert.False(lab.Read<bool>("physicsFilter"));
        lab.Tick(100);
        Assert.True(lab.Read<RigidBody2D>("physicsBall").Position.X > 0);
        Assert.Equal(0, lab.Read<int>("physicsContactBegins"));
        Assert.Equal(0, lab.Read<int>("physicsContactEnds"));
        Assert.True(lab.Read<int>("physicsSensorEvents") > 0);
        Assert.True(lab.Read<int>("physicsSensorEnds") > 0);
        Assert.True(lab.Read<bool>("physicsRayHit"));

        // Voltar aos cenários anteriores destrói o mundo antigo e conserva SAT/carga.
        var filterWorld = lab.Read<PhysicsWorld>("physicsWorld");
        lab.Tap(180, 198);
        Assert.True(filterWorld.IsDisposed);
        Assert.Equal(0, lab.Read<int>("physicsScenario"));
        lab.Tap(180, 489);
        Assert.True(lab.Read<bool>("physicsStress"));
        Assert.All(lab.Read<RigidBody2D[]>("physicsStressBodies"), Assert.NotNull);
        lab.Tap(180, 156);
        Assert.False(lab.Read<bool>("physicsVisible"));
        lab.Tick(5);
    }

    private sealed class PhysicsLab : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "lunet-physics-lab-" + Guid.NewGuid().ToString("N"));
        private readonly Lunet.Runtime.LoadedGame loaded;
        private readonly RecordingBackend backend = new();
        public GameHost Host { get; }

        public PhysicsLab()
        {
            var project = new ProjectStore(root).Create("Physics", ProjectTemplate.Lab);
            var compiler = new Lunet.Compiler.GameCompiler(
                new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var compiled = compiler.Compile("PhysicsLab", project.LoadSources()
                .Select(x => new Lunet.Compiler.SourceFile(x.Path, x.Text)).ToList());
            Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
            loaded = Lunet.Runtime.GameLoader.Load(compiled.Assembly!, compiled.Symbols);
            Host = new GameHost(loaded.Game, backend,
                new Lunet.Content.DirectoryContentSource(Path.Combine(project.Directory, "Content")));
            Assert.True(Host.Start(720, 1280), Host.Fault?.ToString());
            Tick(1);
            Tap(315, 23); // índice
            Tap(268, 432); // módulo 10
            Assert.Equal(9, Read<int>("page"));
            Tap(180, 156); // física Box2D
            Assert.True(Read<bool>("physicsVisible"));
        }

        public T Read<T>(string name) => (T)loaded.Game.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(loaded.Game)!;

        public void Tick(int frames)
        {
            for (int i = 0; i < frames; i++) Host.Tick(1.0 / 60);
            Assert.False(Host.IsFaulted, Host.Fault?.ToString());
        }

        public void Touch(TouchPhase phase, float x, float y)
        {
            Host.SetSurfaceTouches([new TouchPoint(1, phase,
                loaded.Game.GraphicsDevice.VirtualToSurface(new Vector2(x, y)))]);
            Tick(1);
        }

        public void Tap(float x, float y)
        {
            Touch(TouchPhase.Pressed, x, y); Touch(TouchPhase.Released, x, y);
            Host.SetSurfaceTouches([]); Tick(1);
        }

        public void Dispose()
        {
            var world = Read<PhysicsWorld>("physicsWorld");
            Host.Stop();
            Assert.True(world.IsDisposed);
            Assert.Empty(backend.LiveTargets);
            Assert.Empty(backend.LiveShaders);
            loaded.Dispose();
            Directory.Delete(root, true);
        }
    }
}
