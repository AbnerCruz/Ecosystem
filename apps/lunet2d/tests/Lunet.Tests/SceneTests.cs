using System.Numerics;
using Lunet.Graphics;
using Lunet.Scenes;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class SceneTests
{
    private sealed class Probe : Component2D
    {
        public int Updates, Draws;
        public Action? OnUpdate, OnDraw;
        public GameTime LastTime;
        public override void Update(GameTime time) { Updates++; LastTime = time; OnUpdate?.Invoke(); }
        public override void Draw(SpriteBatch batch, GameTime time) { Draws++; LastTime = time; OnDraw?.Invoke(); }
    }
    private sealed class EqualProbe : Component2D
    {
        public override bool Equals(object? obj) => obj is EqualProbe;
        public override int GetHashCode() => 0;
    }
    private static SpriteBatch Batch() => new(new GraphicsDevice(new RecordingBackend(), 360, 640));

    [Fact]
    public void OwnershipIsExclusiveAndTransfersPreserveComponents()
    {
        var a = new Scene2D(); var b = new Scene2D(); var entity = new Entity2D(); var other = new Entity2D(); var probe = new Probe();
        Assert.Equal(Transform2D.Identity.ToMatrix(), entity.Transform.ToMatrix());
        entity.Add(probe); a.Add(entity);
        Assert.Same(a, entity.Scene); Assert.Same(entity, probe.Entity);
        Assert.Throws<InvalidOperationException>(() => b.Add(entity)); Assert.Throws<InvalidOperationException>(() => a.Add(entity));
        Assert.Throws<InvalidOperationException>(() => other.Add(probe)); Assert.Throws<InvalidOperationException>(() => entity.Add(probe));
        Assert.False(b.Remove(entity)); Assert.False(other.Remove(probe));
        Assert.True(a.Remove(entity)); Assert.Null(entity.Scene); Assert.Same(entity, probe.Entity);
        b.Add(entity); Assert.Same(b, entity.Scene); Assert.Same(entity, b[0]); Assert.Same(probe, entity[0]);
        Assert.True(entity.Remove(probe)); Assert.Null(probe.Entity); other.Add(probe);
        b.Clear(); Assert.Null(entity.Scene); Assert.Same(other, probe.Entity);
        other.Clear(); Assert.Null(probe.Entity); Assert.Equal(0, other.Count);
    }

    [Fact]
    public void NullsIndicesAndReferenceIdentityAreChecked()
    {
        var scene = new Scene2D(); var entity = new Entity2D();
        Assert.Throws<ArgumentNullException>(() => scene.Add(null!)); Assert.Throws<ArgumentNullException>(() => scene.Remove(null!));
        Assert.Throws<ArgumentNullException>(() => entity.Add(null!)); Assert.Throws<ArgumentNullException>(() => entity.Remove(null!));
        Assert.Throws<ArgumentNullException>(() => scene.Draw(null!, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => scene[-1]); Assert.Throws<ArgumentOutOfRangeException>(() => entity[0]);
        var first = new EqualProbe(); var second = new EqualProbe(); entity.Add(first); entity.Add(second);
        Assert.True(entity.Remove(second)); Assert.Same(first, entity[0]); Assert.Same(entity, first.Entity); Assert.Null(second.Entity);
        entity.Clear(); Assert.Null(first.Entity);
        Assert.Null(entity.Get<Probe>()); entity.Add(new Probe()); entity.Add(new Probe()); Assert.Same(entity[0], entity.Get<Component2D>());
    }

    [Fact]
    public void TraversalUsesInsertionOrderAndPassesTimeUnchanged()
    {
        var scene = new Scene2D(); var log = new List<int>(); var a = new Entity2D(); var b = new Entity2D();
        var x = new Probe { OnUpdate = () => log.Add(1), OnDraw = () => log.Add(11) };
        var y = new Probe { OnUpdate = () => log.Add(2), OnDraw = () => log.Add(12) };
        var z = new Probe { OnUpdate = () => log.Add(3), OnDraw = () => log.Add(13) };
        a.Add(x); a.Add(y); b.Add(z); scene.Add(a); scene.Add(b);
        var time = new GameTime(45, 0.25f, 0.3f); scene.Update(time); scene.Draw(Batch(), time);
        Assert.Equal(new[] { 1, 2, 3, 11, 12, 13 }, log);
        Assert.Equal(time, x.LastTime); Assert.Equal(time, z.LastTime);
        scene.Remove(a); scene.Add(a); log.Clear(); scene.Update(time); Assert.Equal(new[] { 3, 1, 2 }, log);
    }

    [Fact]
    public void UpdateAndVisibilityFlagsAreIndependentAtAllLevels()
    {
        var scene = new Scene2D(); var entity = new Entity2D(); var probe = new Probe(); entity.Add(probe); scene.Add(entity); var batch = Batch();
        scene.IsEnabled = false; scene.Update(default); scene.Draw(batch, default); Assert.Equal(0, probe.Updates); Assert.Equal(1, probe.Draws);
        scene.IsEnabled = true; scene.IsVisible = false; scene.Update(default); scene.Draw(batch, default); Assert.Equal(1, probe.Updates); Assert.Equal(1, probe.Draws);
        scene.IsVisible = true; entity.IsEnabled = false; scene.Update(default); scene.Draw(batch, default); Assert.Equal(1, probe.Updates); Assert.Equal(2, probe.Draws);
        entity.IsEnabled = true; entity.IsVisible = false; scene.Update(default); scene.Draw(batch, default); Assert.Equal(2, probe.Updates); Assert.Equal(2, probe.Draws);
        entity.IsVisible = true; probe.IsEnabled = false; scene.Update(default); scene.Draw(batch, default); Assert.Equal(2, probe.Updates); Assert.Equal(3, probe.Draws);
        probe.IsEnabled = true; probe.IsVisible = false; scene.Update(default); scene.Draw(batch, default); Assert.Equal(3, probe.Updates); Assert.Equal(3, probe.Draws);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StructuralMutationAndReentryFailBeforeChangingOwnership(bool draw)
    {
        var scene = new Scene2D(); var entity = new Entity2D(); var other = new Entity2D(); var probe = new Probe(); var extra = new Probe();
        entity.Add(probe); scene.Add(entity); scene.Add(other); var batch = Batch();
        Action action = () =>
        {
            Assert.Throws<InvalidOperationException>(() => scene.Add(new Entity2D()));
            Assert.Throws<InvalidOperationException>(() => scene.Remove(entity));
            Assert.Throws<InvalidOperationException>(() => scene.Clear());
            Assert.Throws<InvalidOperationException>(() => entity.Add(extra));
            Assert.Throws<InvalidOperationException>(() => entity.Remove(probe));
            Assert.Throws<InvalidOperationException>(() => entity.Clear());
            Assert.Throws<InvalidOperationException>(() => other.Add(extra));
            Assert.Throws<InvalidOperationException>(() => scene.Update(default));
            Assert.Throws<InvalidOperationException>(() => scene.Draw(batch, default));
        };
        if (draw) probe.OnDraw = action; else probe.OnUpdate = action;
        if (draw) scene.Draw(batch, default); else scene.Update(default);
        Assert.Equal(2, scene.Count); Assert.Equal(1, entity.Count); Assert.Equal(0, other.Count); Assert.Null(extra.Entity);
        Assert.Same(scene, entity.Scene); Assert.Same(entity, probe.Entity);
        entity.Add(extra); Assert.Equal(2, entity.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallbackExceptionsPropagateAndAlwaysReleaseTraversalLock(bool draw)
    {
        var scene = new Scene2D(); var entity = new Entity2D(); var probe = new Probe(); var tail = new Probe();
        var expected = new ApplicationException("game error"); Action action = () => throw expected;
        if (draw) probe.OnDraw = action; else probe.OnUpdate = action;
        entity.Add(probe); entity.Add(tail); scene.Add(entity);
        Assert.Same(expected, Assert.Throws<ApplicationException>(() => { if (draw) scene.Draw(Batch(), default); else scene.Update(default); }));
        Assert.Equal(0, tail.Updates + tail.Draws); Assert.True(entity.Remove(probe)); scene.Remove(entity); scene.Add(entity);
        if (draw) scene.Draw(Batch(), default); else scene.Update(default);
        Assert.Equal(1, tail.Updates + tail.Draws);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlagsChangedDuringCallbacksTakeEffectBeforeRemainingCallbacks(bool draw)
    {
        var scene = new Scene2D(); var entity = new Entity2D(); var other = new Entity2D(); var probe = new Probe(); var tail = new Probe(); var last = new Probe();
        entity.Add(probe); entity.Add(tail); other.Add(last); scene.Add(entity); scene.Add(other); var batch = Batch();
        Action action = () => { if (draw) scene.IsVisible = false; else scene.IsEnabled = false; };
        if (draw) probe.OnDraw = action; else probe.OnUpdate = action;
        if (draw) scene.Draw(batch, default); else scene.Update(default);
        Assert.Equal(0, tail.Updates + tail.Draws); Assert.Equal(0, last.Updates + last.Draws);
        scene.IsEnabled = scene.IsVisible = true;
        action = () => { if (draw) entity.IsVisible = false; else entity.IsEnabled = false; };
        if (draw) probe.OnDraw = action; else probe.OnUpdate = action;
        if (draw) scene.Draw(batch, default); else scene.Update(default);
        Assert.Equal(0, tail.Updates + tail.Draws); Assert.Equal(1, last.Updates + last.Draws);
    }

    [Fact]
    public void TraversalAndLookupDoNotAllocateAfterWarmup()
    {
        var scene = new Scene2D(); var batch = Batch();
        for (int i = 0; i < 20; i++) { var entity = new Entity2D(); entity.Add(new Probe()); scene.Add(entity); }
        void Run() { scene.Update(default); scene.Draw(batch, default); scene[0].Get<Probe>(); }
        for (int i = 0; i < 2000; i++) Run();
        long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 2000; i++) Run();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void NewLaboratoryRunsSceneDemoWithoutEditingCode()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-scene-lab-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new Lunet.Core.ProjectStore(root).Create("SceneLab", Lunet.Core.ProjectTemplate.Lab);
            var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var result = compiler.Compile("SceneLab", project.LoadSources().Select(s => new Lunet.Compiler.SourceFile(s.Path, s.Text)).ToList());
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
            using var loaded = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols);
            var backend = new RecordingBackend();
            var host = new GameHost(loaded.Game, backend, new Lunet.Content.DirectoryContentSource(Path.Combine(project.Directory, "Content")));
            Assert.True(host.Start(720, 1280), host.Fault?.ToString()); host.Tick(1.0 / 60);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            object Field(string name) => loaded.Game.GetType().GetField(name, flags)!.GetValue(loaded.Game)!;
            void Touch(Lunet.Input.TouchPhase phase, float x, float y)
            {
                host.SetSurfaceTouches([new Lunet.Input.TouchPoint(1, phase, loaded.Game.GraphicsDevice.VirtualToSurface(new Vector2(x, y)))]);
                host.Tick(1.0 / 60);
            }
            void Tap(float x, float y) { Touch(Lunet.Input.TouchPhase.Pressed, x, y); Touch(Lunet.Input.TouchPhase.Released, x, y); host.SetSurfaceTouches([]); host.Tick(1.0 / 60); }
            for (int i = 0; i < 5; i++) Tap(100, 23); Assert.Equal(5, Field("page"));
            var scene = (Scene2D)Field("scene"); var entity = (Entity2D)Field("sceneActor"); var motion = (Component2D)Field("sceneMotion");
            var start = entity.Transform.Position; for (int i = 0; i < 10; i++) host.Tick(1.0 / 60); Assert.NotEqual(start, entity.Transform.Position);
            Tap(180, 374); Assert.False(motion.IsEnabled); var paused = entity.Transform.Position;
            host.Tick(1.0 / 60); Assert.Equal(paused, entity.Transform.Position); Assert.True(entity.IsVisible);
            Tap(180, 430); Assert.False(entity.IsVisible); Tap(180, 374); Assert.True(motion.IsEnabled);
            start = entity.Transform.Position; host.Tick(1.0 / 60); Assert.NotEqual(start, entity.Transform.Position);
            Tap(180, 486); Assert.Equal(0, scene.Count); Assert.Null(entity.Scene); Assert.Same(entity, motion.Entity);
            start = entity.Transform.Position; host.Tick(1.0 / 60); Assert.Equal(start, entity.Transform.Position);
            Tap(180, 486); Assert.Equal(1, scene.Count); Assert.Same(scene, entity.Scene);
            Tap(180, 430); Assert.True(entity.IsVisible);
            var position = entity.Transform.Position;
            Assert.Contains(backend.Batches, b => b.Vertices.Any(v => Vector2.Distance(v.Position, position - new Vector2(24)) < 0.01f));
            Touch(Lunet.Input.TouchPhase.Pressed, 180, 374); host.Pause(); host.Resume();
            Touch(Lunet.Input.TouchPhase.Released, 180, 374); Assert.True(motion.IsEnabled);
            Tap(100, 23); Assert.Equal(6, Field("page")); Tap(100, 23); Assert.Equal(0, Field("page")); start = entity.Transform.Position; host.Tick(1.0 / 60); Assert.Equal(start, entity.Transform.Position);
            Assert.False(host.IsFaulted, host.Fault?.ToString()); host.Stop(); Assert.Empty(backend.LiveTargets); Assert.Empty(backend.LiveShaders);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
