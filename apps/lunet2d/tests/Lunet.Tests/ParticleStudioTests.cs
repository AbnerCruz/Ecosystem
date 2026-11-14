using System.Numerics;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Studio;

namespace Lunet.Tests;

/// <summary>LUNET-504: richer particles (cone, shapes, drag, gradient, variance, spin), JSON effects and studio presets.</summary>
public sealed class ParticleStudioTests
{
    private static (GraphicsDevice Device, Texture2D Texture) Device()
    {
        var device = new GraphicsDevice(new HeadlessGraphicsBackend(), 360, 640);
        return (device, Texture2D.CreateCircle(device, 8, Color.White));
    }

    private static ParticleSnapshot[] Run(ParticleSettings settings, int burst, double seconds, Vector2 at = default)
    {
        var (_, texture) = Device();
        var emitter = new ParticleEmitter(texture, settings, 512, seed: 5);
        emitter.Burst(burst, at);
        emitter.Update(seconds);
        var snapshot = new ParticleSnapshot[emitter.Count];
        return snapshot[..emitter.Snapshot(snapshot)];
    }

    [Fact]
    public void Cone_SendsParticlesOnlyInsideTheSpread()
    {
        var up = Run(new ParticleSettings(2, 100, 100) { Direction = -90, Spread = 20 }, 200, 0.5);
        Assert.All(up, p =>
        {
            var angle = MathF.Atan2(p.Position.Y, p.Position.X) * 180 / MathF.PI;
            Assert.InRange(angle, -101, -79);
        });
    }

    [Fact]
    public void Shapes_SpawnInsideTheirArea()
    {
        var circle = Run(new ParticleSettings(2, 0, 0) { Shape = ParticleShape.Circle, ShapeSize = new(30, 0) }, 200, 0.01);
        Assert.All(circle, p => Assert.True(p.Position.Length() <= 30.01f));
        var line = Run(new ParticleSettings(2, 0, 0) { Shape = ParticleShape.Line, ShapeSize = new(100, 0) }, 200, 0.01);
        Assert.All(line, p => { Assert.InRange(p.Position.X, -50, 50); Assert.Equal(0, p.Position.Y, 3); });
        var box = Run(new ParticleSettings(2, 0, 0) { Shape = ParticleShape.Rectangle, ShapeSize = new(40, 10) }, 200, 0.01);
        Assert.All(box, p => { Assert.InRange(p.Position.X, -20, 20); Assert.InRange(p.Position.Y, -5, 5); });
    }

    [Fact]
    public void Drag_SlowsParticlesDown()
    {
        var free = Run(new ParticleSettings(5, 100, 100) { Direction = 0, Spread = 0 }, 1, 2)[0];
        var dragged = Run(new ParticleSettings(5, 100, 100) { Direction = 0, Spread = 0, Drag = 2 }, 1, 2)[0];
        Assert.Equal(200, free.Position.X, 1);
        Assert.InRange(dragged.Position.X, 45, 52); // (1 - e^-4)/2 * 100
    }

    [Fact]
    public void Gradient_And_Variance_And_Spin()
    {
        var settings = new ParticleSettings(1) { ColorStops = [new(0, Color.Red), new(0.5, Color.Green), new(1, Color.Blue)] };
        Assert.Equal(Color.Green, settings.ColorAt(0.5));
        Assert.Equal(Color.Red, settings.ColorAt(0));
        Assert.Equal(Color.Blue, settings.ColorAt(1));
        var varied = Run(new ParticleSettings(1, 0, 0, startSize: 10, endSize: 10) { SizeVariance = 0.5f }, 100, 0.1);
        Assert.InRange(varied.Max(p => p.Size) - varied.Min(p => p.Size), 3, 10);
        var lives = Run(new ParticleSettings(1, 0, 0) { LifetimeVariance = 0.5f }, 200, 0.8);
        Assert.InRange(lives.Length, 40, 180); // some died early, some live longer
        var spun = Run(new ParticleSettings(2, 0, 0) { MinAngularVelocity = 90, MaxAngularVelocity = 90 }, 1, 1)[0];
        Assert.Equal(MathF.PI / 2, spun.Rotation, 3);
    }

    [Fact]
    public void Defaults_KeepOldEffectsIdentical()
    {
        var (_, texture) = Device();
        var a = new ParticleEmitter(texture, new ParticleSettings(), 64, seed: 9);
        a.Burst(10, Vector2.Zero);
        a.Update(0.3);
        var snapshot = new ParticleSnapshot[10];
        a.Snapshot(snapshot);
        // radial motion at speed 50–150 for 0.3 s, no gravity
        Assert.All(snapshot, p => Assert.InRange(p.Position.Length(), 50 * 0.3f - 0.01f, 150 * 0.3f + 0.01f));
    }

    [Fact]
    public void Json_RoundTripsAndRejectsBadValues()
    {
        var preset = ParticlePresets.Find("fire");
        var json = preset.Settings.ToJson(preset.EmissionRate, preset.MaxParticles);
        var again = ParticleSettings.FromJson(json);
        Assert.Equal(preset.Settings.Spread, again.Spread);
        Assert.Equal(preset.Settings.Shape, again.Shape);
        Assert.Equal(preset.Settings.ColorStops.Count, again.ColorStops.Count);
        Assert.Equal(preset.Settings.ColorAt(0.4), again.ColorAt(0.4));
        Assert.Throws<InvalidDataException>(() => ParticleSettings.FromJson(json.Replace("\"Spread\": 35", "\"Spread\": 999")));
        Assert.Throws<InvalidDataException>(() => ParticleSettings.FromJson("{\"Version\":2}"));
        Assert.Throws<InvalidDataException>(() => ParticleSettings.FromJson("não é json"));
    }

    [Fact]
    public void Content_LoadsAStudioEffectAsAReadyEmitter()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-particles-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Effects"));
            var preset = ParticlePresets.Find("snow");
            File.WriteAllText(Path.Combine(root, "Effects", "neve.particles.json"), preset.Settings.ToJson(preset.EmissionRate, preset.MaxParticles));
            var (device, texture) = Device();
            var content = new ContentManager(new DirectoryContentSource(root), device);
            var emitter = content.LoadParticleEmitter("Effects/neve.particles.json", texture);
            Assert.Equal(preset.EmissionRate, emitter.EmissionRate);
            Assert.Equal(preset.MaxParticles, emitter.Capacity);
            emitter.Start();
            emitter.Update(1);
            Assert.True(emitter.Count > 5);
            Assert.Equal(preset.Settings.Shape, content.LoadParticles("Effects/neve.particles.json").Shape);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void EveryPreset_IsValidAndProducesParticles()
    {
        var (_, texture) = Device();
        foreach (var preset in ParticlePresets.All)
        {
            preset.Settings.Validate();
            var emitter = new ParticleEmitter(texture, preset.Settings, preset.MaxParticles) { EmissionRate = preset.EmissionRate };
            if (preset.Burst > 0) emitter.Burst(preset.Burst, Vector2.Zero);
            emitter.Start();
            emitter.Update(0.3);
            Assert.True(emitter.Count > 0, preset.Name);
            var batch = new SpriteBatch(Device().Device);
            batch.Begin();
            emitter.Draw(batch);
            batch.End();
        }
    }
}

public sealed class ParticleDraftTests
{
    [Fact]
    public void Draft_RoundTripsPresetsAndClampsSliders()
    {
        var draft = ParticleDraft.From(ParticlePresets.Find("magic"));
        Assert.Equal(ParticlePresets.Find("magic").Settings.Spread, draft.Build().Spread);
        draft.Spread = 999; draft.MinSpeed = 300; draft.MaxSpeed = 100; draft.MinSpin = 50; draft.MaxSpin = -50; draft.Lifetime = 0;
        var built = draft.Build();
        Assert.Equal(360, built.Spread);
        Assert.True(built.MaxSpeed >= built.MinSpeed);
        Assert.True(built.MaxAngularVelocity >= built.MinAngularVelocity);
        Assert.True(built.LifetimeSeconds > 0);
        Assert.Equal(built.Shape, ParticleSettings.FromJson(draft.ToJson()).Shape);
    }
}
