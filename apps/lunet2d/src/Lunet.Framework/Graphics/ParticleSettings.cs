using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Configuração imutável de vida, velocidade radial, gravidade e aparência das partículas.</summary>
/// <example><code>var effect = new ParticleSettings(lifetimeSeconds: 1.5, minSpeed: 40, maxSpeed: 120, gravity: new Vector2(0, 100));</code></example>
public sealed class ParticleSettings
{
    /// <summary>Configura um efeito radial; valores numéricos devem ser finitos.</summary>
    /// <param name="lifetimeSeconds">Vida positiva em segundos.</param>
    /// <param name="minSpeed">Velocidade mínima não negativa, em unidades por segundo.</param>
    /// <param name="maxSpeed">Velocidade máxima, pelo menos minSpeed.</param>
    /// <param name="gravity">Aceleração constante; padrão zero.</param>
    /// <param name="startSize">Tamanho inicial do quad em unidades do jogo, não negativo.</param>
    /// <param name="endSize">Tamanho final, não negativo.</param>
    /// <param name="startColor">Cor inicial; padrão branco.</param>
    /// <param name="endColor">Cor final; padrão branco transparente.</param>
    public ParticleSettings(double lifetimeSeconds = 1, float minSpeed = 50, float maxSpeed = 150, Vector2? gravity = null,
        float startSize = 8, float endSize = 0, Color? startColor = null, Color? endColor = null)
    {
        if (!double.IsFinite(lifetimeSeconds) || lifetimeSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(lifetimeSeconds));
        if (!float.IsFinite(minSpeed) || minSpeed < 0) throw new ArgumentOutOfRangeException(nameof(minSpeed));
        if (!float.IsFinite(maxSpeed) || maxSpeed < minSpeed) throw new ArgumentOutOfRangeException(nameof(maxSpeed));
        if (!float.IsFinite(startSize) || startSize < 0) throw new ArgumentOutOfRangeException(nameof(startSize));
        if (!float.IsFinite(endSize) || endSize < 0) throw new ArgumentOutOfRangeException(nameof(endSize));
        var g = gravity ?? Vector2.Zero;
        if (!float.IsFinite(g.X) || !float.IsFinite(g.Y)) throw new ArgumentOutOfRangeException(nameof(gravity));
        LifetimeSeconds = lifetimeSeconds;
        MinSpeed = minSpeed; MaxSpeed = maxSpeed;
        Gravity = g; StartSize = startSize; EndSize = endSize;
        StartColor = startColor ?? Color.White;
        EndColor = endColor ?? Color.White.WithAlpha(0);
    }
    /// <summary>Vida em segundos.</summary>
    public double LifetimeSeconds { get; }
    /// <summary>Velocidade radial mínima.</summary>
    public float MinSpeed { get; }
    /// <summary>Velocidade radial máxima.</summary>
    public float MaxSpeed { get; }
    /// <summary>Aceleração constante.</summary>
    public Vector2 Gravity { get; }
    /// <summary>Tamanho inicial do quad.</summary>
    public float StartSize { get; }
    /// <summary>Tamanho final do quad.</summary>
    public float EndSize { get; }
    /// <summary>Cor inicial.</summary>
    public Color StartColor { get; }
    /// <summary>Cor final.</summary>
    public Color EndColor { get; }

    /// <summary>Direção central da emissão, em graus (0 = direita, 90 = para baixo). Só vale com <see cref="Spread"/> menor que 360.</summary>
    public float Direction { get; init; }
    /// <summary>Abertura do cone de emissão, em graus (360 = todas as direções, o padrão; 0 = um raio só).</summary>
    public float Spread { get; init; } = 360;
    /// <summary>Variação aleatória da vida, de 0 (todas iguais) a 1 (cada partícula vive entre quase zero e o dobro da vida base).</summary>
    public float LifetimeVariance { get; init; }
    /// <summary>Variação aleatória do tamanho, de 0 a 1.</summary>
    public float SizeVariance { get; init; }
    /// <summary>Onde as partículas nascem em volta da origem.</summary>
    public ParticleShape Shape { get; init; } = ParticleShape.Point;
    /// <summary>Tamanho da forma de emissão: raio do círculo (X), largura e altura do retângulo, comprimento da linha (X).</summary>
    public Vector2 ShapeSize { get; init; }
    /// <summary>Resistência do ar: quanto a velocidade cai por segundo (0 = nenhuma).</summary>
    public float Drag { get; init; }
    /// <summary>Rotação inicial máxima aleatória, em graus.</summary>
    public float RotationVariance { get; init; }
    /// <summary>Giro em graus por segundo: mínimo e máximo sorteados por partícula.</summary>
    public float MinAngularVelocity { get; init; }
    /// <summary>Giro máximo em graus por segundo.</summary>
    public float MaxAngularVelocity { get; init; }
    /// <summary>Gradiente de cor ao longo da vida (posições de 0 a 1, em ordem). Vazio usa <see cref="StartColor"/> → <see cref="EndColor"/>.</summary>
    public IReadOnlyList<ParticleColorStop> ColorStops { get; init; } = [];
    /// <summary>Sugestão de mistura aditiva (brilho de fogo, magia, faíscas): desenhe com <c>BlendState.Additive</c>.</summary>
    public bool Additive { get; init; }

    /// <summary>Cor na fração <paramref name="amount"/> da vida (0 a 1).</summary>
    /// <param name="amount">Fração da vida.</param>
    /// <returns>A cor interpolada.</returns>
    public Color ColorAt(double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        if (ColorStops.Count == 0) return Lerp(StartColor, EndColor, amount);
        if (amount <= ColorStops[0].Position) return ColorStops[0].Color;
        for (int i = 1; i < ColorStops.Count; i++)
        {
            var (a, b) = (ColorStops[i - 1], ColorStops[i]);
            if (amount <= b.Position)
                return Lerp(a.Color, b.Color, b.Position <= a.Position ? 1 : (amount - a.Position) / (b.Position - a.Position));
        }
        return ColorStops[^1].Color;
    }

    private static Color Lerp(Color a, Color b, double t) => new(Channel(a.R, b.R, t), Channel(a.G, b.G, t), Channel(a.B, b.B, t), Channel(a.A, b.A, t));
    private static byte Channel(byte from, byte to, double amount) => (byte)Math.Round((1 - amount) * from + amount * to);

    /// <summary>Recusa valores que o emissor não sabe simular (NaN, negativos, gradiente fora de ordem).</summary>
    public void Validate()
    {
        static bool Ok(float v, float min, float max) => float.IsFinite(v) && v >= min && v <= max;
        if (!Ok(Spread, 0, 360) || !float.IsFinite(Direction) || !Ok(LifetimeVariance, 0, 1) || !Ok(SizeVariance, 0, 1) ||
            !float.IsFinite(ShapeSize.X) || !float.IsFinite(ShapeSize.Y) || ShapeSize.X < 0 || ShapeSize.Y < 0 || !Ok(Drag, 0, 100) ||
            !Ok(RotationVariance, 0, 360) || !float.IsFinite(MinAngularVelocity) || !float.IsFinite(MaxAngularVelocity) || MaxAngularVelocity < MinAngularVelocity)
            throw new ArgumentOutOfRangeException(nameof(ParticleSettings), "Configuração de partículas fora dos limites.");
        for (int i = 0; i < ColorStops.Count; i++)
            if (!double.IsFinite(ColorStops[i].Position) || ColorStops[i].Position is < 0 or > 1 || i > 0 && ColorStops[i].Position < ColorStops[i - 1].Position)
                throw new ArgumentOutOfRangeException(nameof(ColorStops), "Gradiente de cor fora de ordem ou de 0–1.");
    }

    /// <summary>Salva o efeito em JSON (o formato dos arquivos <c>.particles.json</c> do Particle Studio).</summary>
    /// <returns>Texto JSON.</returns>
    public string ToJson() => ParticleJson.Write(this);

    /// <summary>Salva o efeito em JSON com a taxa de emissão e o limite de partículas que o emissor deve usar.</summary>
    /// <param name="emissionRate">Partículas por segundo da emissão contínua (0 = só bursts).</param>
    /// <param name="maxParticles">Capacidade do emissor.</param>
    /// <returns>Texto JSON.</returns>
    public string ToJson(float emissionRate, int maxParticles) => ParticleJson.Write(this, emissionRate, maxParticles);

    /// <summary>Lê um efeito salvo por <see cref="ToJson()"/> ou pelo Particle Studio, validando os valores.</summary>
    /// <param name="json">Texto JSON.</param>
    /// <returns>O efeito.</returns>
    public static ParticleSettings FromJson(string json) => ParticleJson.Read(json);
}

/// <summary>Onde as partículas nascem em volta da origem do emissor.</summary>
public enum ParticleShape
{
    /// <summary>Todas no mesmo ponto.</summary>
    Point,
    /// <summary>Dentro de um círculo de raio <see cref="ParticleSettings.ShapeSize"/>.X.</summary>
    Circle,
    /// <summary>Dentro de um retângulo centrado de tamanho <see cref="ParticleSettings.ShapeSize"/>.</summary>
    Rectangle,
    /// <summary>Numa linha horizontal centrada de comprimento <see cref="ParticleSettings.ShapeSize"/>.X (chuva, neve).</summary>
    Line,
}

/// <summary>Uma cor do gradiente de vida das partículas.</summary>
/// <example>
/// <code>
/// var fogo = new ParticleSettings(lifetimeSeconds: 0.8, minSpeed: 30, maxSpeed: 60)
/// {
///     ColorStops = [new(0, Color.Yellow), new(0.5, Color.FromHex(0xFF6A00)), new(1, Color.Red.WithAlpha(0))],
/// };
/// </code>
/// </example>
/// <param name="Position">Fração da vida, de 0 a 1.</param>
/// <param name="Color">Cor nesse ponto.</param>
public readonly record struct ParticleColorStop(double Position, Color Color);

/// <summary>Estado de uma partícula viva num instante, para desenhar com qualquer técnica.</summary>
/// <example>
/// <code>
/// var particulas = new ParticleEmitter(Texture2D.CreateCircle(GraphicsDevice, 8, Color.White), new ParticleSettings());
/// particulas.Burst(20, new System.Numerics.Vector2(100, 100));
/// Span&lt;ParticleSnapshot&gt; vivas = stackalloc ParticleSnapshot[particulas.Count];
/// int n = particulas.Snapshot(vivas);
/// for (int i = 0; i &lt; n; i++) Log.Info(vivas[i].Position + " " + vivas[i].Size);
/// </code>
/// </example>
/// <param name="Position">Centro.</param>
/// <param name="Size">Lado do quad.</param>
/// <param name="Rotation">Rotação em radianos.</param>
/// <param name="Color">Cor atual.</param>
/// <param name="Life">Fração da vida já vivida (0 a 1).</param>
public readonly record struct ParticleSnapshot(Vector2 Position, float Size, float Rotation, Color Color, float Life);

internal static class ParticleJson
{
    private sealed record Stop(double Position, string Color);
    private sealed record Dto(int Version, double Lifetime, float MinSpeed, float MaxSpeed, float GravityX, float GravityY, float StartSize, float EndSize,
        string StartColor, string EndColor, float Direction, float Spread, float LifetimeVariance, float SizeVariance, string Shape, float ShapeWidth,
        float ShapeHeight, float Drag, float RotationVariance, float MinAngularVelocity, float MaxAngularVelocity, List<Stop>? ColorStops, bool Additive,
        float EmissionRate, int MaxParticles);

    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";
    private static Color Parse(string? hex)
    {
        var h = (hex ?? "").Trim().TrimStart('#');
        if (h.Length is not (6 or 8) || !uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out var v))
            throw new InvalidDataException("Cor inválida: " + hex);
        return h.Length == 6 ? new Color((byte)(v >> 16), (byte)(v >> 8), (byte)v) : new Color((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    public static string Write(ParticleSettings s, float emissionRate = 0, int maxParticles = 256) => System.Text.Json.JsonSerializer.Serialize(
        new Dto(1, s.LifetimeSeconds, s.MinSpeed, s.MaxSpeed, s.Gravity.X, s.Gravity.Y, s.StartSize, s.EndSize, Hex(s.StartColor), Hex(s.EndColor),
            s.Direction, s.Spread, s.LifetimeVariance, s.SizeVariance, s.Shape.ToString(), s.ShapeSize.X, s.ShapeSize.Y, s.Drag, s.RotationVariance,
            s.MinAngularVelocity, s.MaxAngularVelocity, s.ColorStops.Select(c => new Stop(c.Position, Hex(c.Color))).ToList(), s.Additive,
            emissionRate, maxParticles), new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    public static ParticleSettings Read(string json) => ReadWithEmission(json).Settings;

    public static (ParticleSettings Settings, float EmissionRate, int MaxParticles) ReadWithEmission(string json)
    {
        Dto? d;
        try { d = System.Text.Json.JsonSerializer.Deserialize<Dto>(json); }
        catch (System.Text.Json.JsonException ex) { throw new InvalidDataException("Efeito de partículas inválido: " + ex.Message, ex); }
        if (d is null || d.Version != 1) throw new InvalidDataException("Versão de efeito de partículas não suportada.");
        if (!Enum.TryParse<ParticleShape>(d.Shape, out var shape)) throw new InvalidDataException("Forma de emissão desconhecida: " + d.Shape);
        try
        {
            var settings = new ParticleSettings(d.Lifetime, d.MinSpeed, d.MaxSpeed, new Vector2(d.GravityX, d.GravityY), d.StartSize, d.EndSize,
                Parse(d.StartColor), Parse(d.EndColor))
            {
                Direction = d.Direction, Spread = d.Spread, LifetimeVariance = d.LifetimeVariance, SizeVariance = d.SizeVariance, Shape = shape,
                ShapeSize = new Vector2(d.ShapeWidth, d.ShapeHeight), Drag = d.Drag, RotationVariance = d.RotationVariance,
                MinAngularVelocity = d.MinAngularVelocity, MaxAngularVelocity = d.MaxAngularVelocity, Additive = d.Additive,
                ColorStops = (d.ColorStops ?? []).Select(c => new ParticleColorStop(c.Position, Parse(c.Color))).ToArray(),
            };
            settings.Validate();
            if (!float.IsFinite(d.EmissionRate) || d.EmissionRate is < 0 or > 10_000 || d.MaxParticles is < 1 or > 10_000)
                throw new ArgumentOutOfRangeException(nameof(json), "Taxa ou limite de partículas fora dos limites.");
            return (settings, d.EmissionRate, d.MaxParticles);
        }
        catch (ArgumentOutOfRangeException ex) { throw new InvalidDataException(ex.Message, ex); }
    }
}
