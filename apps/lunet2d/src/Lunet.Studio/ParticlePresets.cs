using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Studio;

/// <summary>A ready effect of the Particle Studio (LUNET-504): settings plus how the emitter runs it.</summary>
public sealed record ParticlePreset(string Id, string Name, ParticleSettings Settings, float EmissionRate, int MaxParticles, int Burst, string Hint);

/// <summary>Starting points for common 2D game effects; every value is editable in the studio.</summary>
public static class ParticlePresets
{
    private static Color Hex(uint rgb, byte alpha = 255) => Color.FromHex(rgb).WithAlpha(alpha);

    public static IReadOnlyList<ParticlePreset> All { get; } =
    [
        new("fire", "Fogo", new ParticleSettings(0.9, 20, 60, new Vector2(0, -60), 18, 4, Hex(0xFFE066), Hex(0xFF2200, 0))
        {
            Direction = -90, Spread = 35, LifetimeVariance = 0.4f, SizeVariance = 0.3f, Shape = ParticleShape.Line, ShapeSize = new(18, 0), Additive = true,
            ColorStops = [new(0, Hex(0xFFF2A8)), new(0.35, Hex(0xFFA000)), new(0.75, Hex(0xE03000, 160)), new(1, Hex(0x401010, 0))],
        }, 70, 200, 0, "Contínuo; ponha o emissor na base da tocha."),
        new("smoke", "Fumaça", new ParticleSettings(2.4, 10, 30, new Vector2(8, -25), 12, 40, Hex(0x9A9AA6, 150), Hex(0x6A6A76, 0))
        {
            Direction = -90, Spread = 40, LifetimeVariance = 0.3f, SizeVariance = 0.4f, Drag = 0.4f, RotationVariance = 180, MinAngularVelocity = -30, MaxAngularVelocity = 30,
        }, 14, 80, 0, "Contínuo e lento; combina com Fogo."),
        new("sparks", "Faíscas", new ParticleSettings(0.6, 120, 260, new Vector2(0, 420), 4, 1, Hex(0xFFF6C0), Hex(0xFF8A00, 0))
        { Spread = 360, LifetimeVariance = 0.5f, Drag = 1.5f, Additive = true }, 0, 120, 40, "Burst ao bater ou acertar."),
        new("explosion", "Explosão", new ParticleSettings(0.7, 60, 240, Vector2.Zero, 26, 6, Hex(0xFFF0A0), Hex(0x501000, 0))
        {
            Spread = 360, LifetimeVariance = 0.4f, SizeVariance = 0.5f, Drag = 3, Additive = true,
            ColorStops = [new(0, Hex(0xFFFFFF)), new(0.15, Hex(0xFFD040)), new(0.5, Hex(0xFF5010, 200)), new(1, Hex(0x302020, 0))],
        }, 0, 160, 90, "Burst único; some sozinha."),
        new("rain", "Chuva", new ParticleSettings(1.2, 380, 460, new Vector2(0, 200), 3, 3, Hex(0x9EC9FF, 200), Hex(0x9EC9FF, 120))
        { Direction = 100, Spread = 4, Shape = ParticleShape.Line, ShapeSize = new(420, 0) }, 160, 400, 0, "Ponha o emissor acima da tela, no centro."),
        new("snow", "Neve", new ParticleSettings(6, 10, 30, new Vector2(0, 18), 5, 5, Hex(0xFFFFFF, 230), Hex(0xFFFFFF, 0))
        { Direction = 90, Spread = 60, Shape = ParticleShape.Line, ShapeSize = new(420, 0), SizeVariance = 0.6f, LifetimeVariance = 0.3f }, 18, 160, 0, "Contínuo acima da tela."),
        new("magic", "Magia", new ParticleSettings(1.1, 20, 70, new Vector2(0, -20), 8, 0, Hex(0xC8A0FF), Hex(0x40E0FF, 0))
        {
            Spread = 360, Shape = ParticleShape.Circle, ShapeSize = new(20, 0), LifetimeVariance = 0.4f, SizeVariance = 0.5f, Additive = true,
            MinAngularVelocity = -180, MaxAngularVelocity = 180,
        }, 40, 120, 30, "Em volta de um item ou personagem."),
        new("dust", "Poeira de passos", new ParticleSettings(0.5, 20, 50, new Vector2(0, -10), 6, 14, Hex(0xC8B090, 180), Hex(0xC8B090, 0))
        { Direction = -90, Spread = 120, Drag = 2, SizeVariance = 0.4f }, 0, 40, 6, "Burst pequeno a cada passo ou pouso."),
        new("bubbles", "Bolhas", new ParticleSettings(2.5, 15, 35, new Vector2(0, -30), 6, 9, Hex(0xBFF4FF, 200), Hex(0xBFF4FF, 0))
        { Direction = -90, Spread = 30, Shape = ParticleShape.Rectangle, ShapeSize = new(40, 8), SizeVariance = 0.6f }, 6, 60, 0, "Debaixo d'água."),
        new("confetti", "Confete", new ParticleSettings(2.2, 120, 220, new Vector2(0, 180), 7, 7, Hex(0xFF4D6D), Hex(0x4DD2FF))
        {
            Direction = -90, Spread = 70, Drag = 1.2f, RotationVariance = 180, MinAngularVelocity = -540, MaxAngularVelocity = 540,
            ColorStops = [new(0, Hex(0xFF4D6D)), new(0.33, Hex(0xFFD24D)), new(0.66, Hex(0x5BE37D)), new(1, Hex(0x4DD2FF))],
        }, 0, 200, 80, "Burst de vitória."),
    ];

    public static ParticlePreset Find(string id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];
}

/// <summary>Editable copy of an effect for the studio's sliders; <see cref="Build"/> validates and creates the immutable settings.</summary>
public sealed class ParticleDraft
{
    public double Lifetime = 1;
    public float MinSpeed = 50, MaxSpeed = 150, GravityX, GravityY, StartSize = 8, EndSize, Direction, Spread = 360;
    public float LifetimeVariance, SizeVariance, ShapeWidth, ShapeHeight, Drag, RotationVariance, MinSpin, MaxSpin;
    public ParticleShape Shape = ParticleShape.Point;
    public Color StartColor = Color.White, EndColor = Color.White.WithAlpha(0);
    public List<ParticleColorStop> Stops = [];
    public bool Additive;
    public float EmissionRate = 30;
    public int MaxParticles = 256, Burst = 30;

    public static ParticleDraft From(ParticlePreset preset)
    {
        var d = From(preset.Settings);
        d.EmissionRate = preset.EmissionRate; d.MaxParticles = preset.MaxParticles; d.Burst = preset.Burst;
        return d;
    }

    public static ParticleDraft From(ParticleSettings s) => new()
    {
        Lifetime = s.LifetimeSeconds, MinSpeed = s.MinSpeed, MaxSpeed = s.MaxSpeed, GravityX = s.Gravity.X, GravityY = s.Gravity.Y,
        StartSize = s.StartSize, EndSize = s.EndSize, Direction = s.Direction, Spread = s.Spread, LifetimeVariance = s.LifetimeVariance,
        SizeVariance = s.SizeVariance, ShapeWidth = s.ShapeSize.X, ShapeHeight = s.ShapeSize.Y, Drag = s.Drag, RotationVariance = s.RotationVariance,
        MinSpin = s.MinAngularVelocity, MaxSpin = s.MaxAngularVelocity, Shape = s.Shape, StartColor = s.StartColor, EndColor = s.EndColor,
        Stops = s.ColorStops.ToList(), Additive = s.Additive,
    };

    public ParticleSettings Build()
    {
        var settings = new ParticleSettings(Math.Max(0.02, Lifetime), Math.Max(0, Math.Min(MinSpeed, MaxSpeed)), Math.Max(MinSpeed, MaxSpeed),
            new Vector2(GravityX, GravityY), Math.Max(0, StartSize), Math.Max(0, EndSize), StartColor, EndColor)
        {
            Direction = Direction, Spread = Math.Clamp(Spread, 0, 360), LifetimeVariance = Math.Clamp(LifetimeVariance, 0, 1),
            SizeVariance = Math.Clamp(SizeVariance, 0, 1), Shape = Shape, ShapeSize = new Vector2(Math.Max(0, ShapeWidth), Math.Max(0, ShapeHeight)),
            Drag = Math.Clamp(Drag, 0, 100), RotationVariance = Math.Clamp(RotationVariance, 0, 360),
            MinAngularVelocity = Math.Min(MinSpin, MaxSpin), MaxAngularVelocity = Math.Max(MinSpin, MaxSpin),
            ColorStops = Stops.OrderBy(s => s.Position).Select(s => s with { Position = Math.Clamp(s.Position, 0, 1) }).ToArray(), Additive = Additive,
        };
        settings.Validate();
        return settings;
    }

    public string ToJson() => Build().ToJson(Math.Clamp(EmissionRate, 0, 10_000), Math.Clamp(MaxParticles, 1, 10_000));
}
