using System.Numerics;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Input;

namespace Lunet.Tests;

[Collection("Frame allocation")]
public class ParticleTests
{
    private static Texture2D Texture(GraphicsDevice device) => Texture2D.CreateSolid(device, 8, 8, Color.White);
    private static ParticleSettings Still(double lifetime = 1) => new(lifetime, 0, 0, startSize: 10, endSize: 10);
    private static Vector2[] Centers(RecordingBackend backend) => backend.Batches.SelectMany(b => Enumerable.Range(0, b.QuadCount)
        .Select(i => (b.Vertices[i * 4].Position + b.Vertices[i * 4 + 2].Position) / 2)).ToArray();

    [Fact]
    public void BurstsSaturateThePoolAndReuseExpiredSlotsWithoutOwningTheTexture()
    {
        var device = new GraphicsDevice(new RecordingBackend(), 360, 640);
        using var texture = Texture(device);
        var emitter = new ParticleEmitter(texture, Still(), 8);
        Assert.Equal(8, emitter.Burst(int.MaxValue, Vector2.Zero));
        Assert.Equal(0, emitter.Burst(100, Vector2.One));
        emitter.Update(1); Assert.Equal(0, emitter.Count);
        Assert.Equal(3, emitter.Burst(3, Vector2.One));
        emitter.Clear(); Assert.Equal(0, emitter.Count); Assert.False(texture.IsDisposed);
    }

    [Fact]
    public void BallisticMovementSizeAndRgbaReachTheExistingBatch()
    {
        var backend = new RecordingBackend();
        var device = new GraphicsDevice(backend, 360, 640);
        using var texture = Texture(device);
        var settings = new ParticleSettings(1, 0, 0, new Vector2(0, 100), 10, 20, new Color(0, 0, 0, 255), new Color(255, 255, 255, 0));
        var emitter = new ParticleEmitter(texture, settings);
        emitter.Burst(1, new Vector2(100, 200)); emitter.Update(0.5);
        var batch = new SpriteBatch(device); batch.Begin(); emitter.Draw(batch); batch.End();
        Assert.Equal(new Vector2(100, 212.5f), Assert.Single(Centers(backend)));
        var v = backend.Batches[^1].Vertices;
        Assert.Equal(new Vector2(15, 15), v[2].Position - v[0].Position);
        Assert.Equal(Vector2.Zero, v[0].TexCoord); Assert.Equal(Vector2.One, v[2].TexCoord);
        Assert.Equal(new Color(128, 128, 128, 128).PackedRgba, v[0].Color);
        emitter.Update(0.5); Assert.Equal(0, emitter.Count);
    }

    [Fact]
    public void SeedReproducesVelocitiesAndMovingOriginDoesNotMoveOldParticles()
    {
        Vector2[] Run(ulong seed)
        {
            var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
            using var texture = Texture(device);
            var emitter = new ParticleEmitter(texture, new ParticleSettings(minSpeed: 100, maxSpeed: 100), seed: seed);
            emitter.Burst(5, Vector2.Zero); emitter.Position = new Vector2(10000, 10000); emitter.Update(0.5);
            var batch = new SpriteBatch(device); batch.Begin(); emitter.Draw(batch); batch.End();
            return Centers(backend);
        }
        var a = Run(42); Assert.Equal(a, Run(42)); Assert.NotEqual(a[0], Run(43)[0]);
        Assert.All(a, p => Assert.Equal(50, p.Length(), 4));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.249, 0)]
    [InlineData(0.25, 1)]
    [InlineData(1, 4)]
    public void ContinuousEmissionWaitsForWholeIntervals(double seconds, int count)
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        using var texture = Texture(device);
        var emitter = new ParticleEmitter(texture, Still(2)) { EmissionRate = 4 };
        emitter.Start(); emitter.Update(seconds); Assert.Equal(count, emitter.Count);
    }

    [Fact]
    public void ContinuousBirthAgesMatchPartitionedUpdatesWhenThePoolDoesNotSaturate()
    {
        float[] Run(bool split)
        {
            var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
            using var texture = Texture(device);
            var emitter = new ParticleEmitter(texture, new ParticleSettings(2, 0, 0, new Vector2(0, 100))) { EmissionRate = 4 };
            emitter.Start();
            if (split) for (int i = 0; i < 4; i++) emitter.Update(0.25); else emitter.Update(1);
            var batch = new SpriteBatch(device); batch.Begin(); emitter.Draw(batch); batch.End();
            return Centers(backend).Select(p => p.Y).Order().ToArray();
        }
        Assert.Equal(new[] { 0f, 3.125f, 12.5f, 28.125f }, Run(false));
        Assert.Equal(Run(false), Run(true));
    }

    [Fact]
    public void HugeDeltaAndRateRemainBoundedAndDoNotQueueMissedBirths()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        using var texture = Texture(device);
        var emitter = new ParticleEmitter(texture, Still(2), 8) { EmissionRate = float.MaxValue };
        emitter.Start(); emitter.Update(double.MaxValue); Assert.Equal(8, emitter.Count);
        emitter.Stop(); emitter.Update(2); Assert.Equal(0, emitter.Count);
        emitter.EmissionRate = 4; emitter.Start(); emitter.Update(0.25); Assert.Equal(1, emitter.Count);
    }

    [Fact]
    public void StopRateChangesAndClearDiscardOnlyTheirDocumentedState()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        using var texture = Texture(device);
        var emitter = new ParticleEmitter(texture, Still(2), 8) { EmissionRate = 4 };
        emitter.Start(); emitter.Update(0.125); emitter.Stop(); emitter.Start(); emitter.Update(0.125);
        Assert.Equal(0, emitter.Count);
        emitter.Update(0.125); Assert.Equal(1, emitter.Count);
        emitter.EmissionRate = 2; emitter.Update(0.25); Assert.Equal(1, emitter.Count);
        emitter.Clear(); Assert.True(emitter.IsEmitting); Assert.Equal(2, emitter.EmissionRate);
        emitter.Update(0.25); Assert.Equal(0, emitter.Count); emitter.Update(0.25); Assert.Equal(1, emitter.Count);
        emitter.EmissionRate = 0; emitter.Update(10); Assert.Equal(0, emitter.Count);
        Assert.Equal(2, emitter.Burst(2, Vector2.Zero)); // burst funciona com taxa zero
    }

    [Fact]
    public void InvalidInputsFailBeforeChangingState()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        using var texture = Texture(device);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticleEmitter(texture, Still(), 0));
        foreach (var value in new[] { -1d, 0, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => new ParticleSettings(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticleSettings(minSpeed: 10, maxSpeed: 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticleSettings(startSize: float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticleSettings(endSize: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticleSettings(gravity: new Vector2(float.PositiveInfinity, 0)));
        var emitter = new ParticleEmitter(texture, Still()) { Position = Vector2.One, EmissionRate = 4 };
        emitter.Burst(2, Vector2.One);
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Burst(-1, Vector2.Zero));
        foreach (var value in new[] { -1d, double.NaN, double.PositiveInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Update(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.Position = new Vector2(float.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => emitter.EmissionRate = float.PositiveInfinity);
        Assert.Throws<ArgumentNullException>(() => emitter.Draw(null!));
        Assert.Equal(2, emitter.Count); Assert.Equal(Vector2.One, emitter.Position); Assert.Equal(4, emitter.EmissionRate);
    }

    [Fact]
    public void ParticleDrawUsesTheBatchCameraAndClipAndSkipsNumericOverflow()
    {
        var backend = new RecordingBackend(); var device = new GraphicsDevice(backend, 360, 640);
        device.Resize(360, 640);
        using var texture = Texture(device); var batch = new SpriteBatch(device);
        var emitter = new ParticleEmitter(texture, Still()); emitter.Burst(1, new Vector2(500, 300));
        var clip = new RectangleF(0, 100, 360, 400);
        batch.Begin(new Camera2D { Position = new Vector2(500, 300) }, clip: clip); emitter.Draw(batch); batch.End();
        Assert.Equal(new Vector2(180, 320), Assert.Single(Centers(backend)));
        Assert.Equal(new RectI(0, 140, 360, 400), backend.States[^1].Scissor);
        var extreme = new ParticleEmitter(texture, new ParticleSettings(double.MaxValue, 0, 0, new Vector2(float.MaxValue)));
        extreme.Burst(1, Vector2.Zero); extreme.Update(double.MaxValue / 2);
        var before = backend.Batches.Count; batch.Begin(); extreme.Draw(batch); batch.End();
        Assert.Equal(before, backend.Batches.Count); Assert.Equal(1, extreme.Count);
    }

    [Fact]
    public void BurstUpdateAndDrawDoNotAllocateInTheFrameLoop()
    {
        var device = new GraphicsDevice(new NoOpBackend(), 360, 640);
        using var texture = Texture(device); var batch = new SpriteBatch(device);
        var emitter = new ParticleEmitter(texture, new ParticleSettings(), 256) { EmissionRate = 80 };
        emitter.Start();
        void Frame()
        {
            emitter.Burst(2, Vector2.Zero); emitter.Update(1.0 / 60);
            batch.Begin(); emitter.Draw(batch); batch.End();
        }
        for (int i = 0; i < 1000; i++) Frame();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Frame();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void TemplateAndGuideCompileAndRunTouchFlowClearAndCapacityInTheRealRuntime()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "docs/guides/particulas.md"))) root = root.Parent;
        Assert.NotNull(root);
        var code = File.ReadAllText(Path.Combine(root!.FullName, "docs/guides/particulas.md")).Split("```csharp\n")[1].Split("```")[0];
        Assert.Equal(code.Trim(), ProjectTemplates.ParticlesSource("ParticleDemo").Trim());
        var projectRoot = Path.Combine(Path.GetTempPath(), "lunet-particles-" + Guid.NewGuid().ToString("N"));
        try
        {
            var project = new ProjectStore(projectRoot).Create("ParticleDemo", ProjectTemplate.Particles);
            Assert.Equal(code.Trim(), project.ReadText("Game.cs").Trim());
            var compiler = new Lunet.Compiler.GameCompiler(new Lunet.Compiler.LoadedAssembliesReferenceProvider(typeof(Game).Assembly));
            var result = compiler.Compile("ParticleDemo", [new Lunet.Compiler.SourceFile("Game.cs", code)]);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
            Assert.DoesNotContain(result.Diagnostics, d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Warning);
            using var loaded = Lunet.Runtime.GameLoader.Load(result.Assembly!, result.Symbols);
            var backend = new RecordingBackend(); var host = new GameHost(loaded.Game, backend);
            Assert.True(host.Start(720, 1280));
            var emitter = (ParticleEmitter)loaded.Game.GetType().GetField("particles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(loaded.Game)!;
            void Frame(int n = 1) { for (int i = 0; i < n; i++) host.Tick(1.0 / 60); }
            void Tap(float x, float y)
            {
                host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Moved, new Vector2(x, y) * 2)]); Frame();
                host.SetSurfaceTouches([new TouchPoint(1, TouchPhase.Released, new Vector2(x, y) * 2)]); Frame(2);
                host.SetSurfaceTouches([]); Frame(2);
            }
            Assert.Equal(60, emitter.Count);
            Frame(100); Assert.Equal(0, emitter.Count);
            Tap(250, 400); Assert.Equal(new Vector2(250, 400), emitter.Position); Assert.Equal(40, emitter.Count);
            Tap(170, 64); Assert.True(emitter.IsEmitting); Frame(30); Assert.InRange(emitter.Count, 40, 256);
            Tap(170, 64); Assert.False(emitter.IsEmitting); Frame(100); Assert.Equal(0, emitter.Count);
            for (int i = 0; i < 8; i++) Tap(60, 64);
            Assert.Equal(256, emitter.Count);
            Tap(290, 64); Assert.Equal(0, emitter.Count);
            Assert.NotEmpty(backend.Batches); Assert.False(host.IsFaulted, host.Fault?.ToString());
        }
        finally { if (Directory.Exists(projectRoot)) Directory.Delete(projectRoot, true); }
    }
}
