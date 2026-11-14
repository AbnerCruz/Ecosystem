using System.Numerics;

namespace Lunet.Graphics;

/// <summary>Partículas radiais com pool de capacidade fixa, emissão contínua ou bursts e desenho no SpriteBatch existente.</summary>
/// <example><code>
/// var particles = new ParticleEmitter(texture, new ParticleSettings(), capacity: 256, seed: 42);
/// particles.Burst(40, position);
/// particles.Update(1.0 / 60);
/// batch.Begin();
/// particles.Draw(batch);
/// batch.End();
/// </code></example>
/// <remarks>Não possui/libera a textura. Update/Draw/Burst não alocam; custo limitado à capacidade. Quando cheio, descarta emissão excedente sem fila. Ordem de desenho não é estável após expiração. Use na thread do jogo.</remarks>
public sealed class ParticleEmitter
{
    private struct Particle
    {
        public Vector2 Origin;
        public Vector2 Velocity;
        public double Age;
        public double Lifetime;
        public float SizeScale;
        public float Rotation;
        public float AngularVelocity;
    }
    private readonly Particle[] _particles;
    private readonly RandomSource _random;
    private Vector2 _position;
    private float _rate;
    private double _emissionElapsed;

    /// <summary>Cria um emissor sem emissão contínua ativa.</summary>
    /// <param name="texture">Textura compartilhada desenhada inteira em cada partícula.</param>
    /// <param name="settings">Configuração imutável do efeito.</param>
    /// <param name="capacity">Número máximo positivo de partículas vivas.</param>
    /// <param name="seed">Semente determinística das velocidades.</param>
    public ParticleEmitter(Texture2D texture, ParticleSettings settings, int capacity = 256, ulong seed = 1)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(capacity, 0);
        settings.Validate();
        Texture = texture; Settings = settings;
        _particles = new Particle[capacity];
        _random = new RandomSource(seed);
    }
    /// <summary>Textura compartilhada; o jogo mantém seu ownership.</summary>
    public Texture2D Texture { get; }
    /// <summary>Configuração imutável das partículas.</summary>
    public ParticleSettings Settings { get; }
    /// <summary>Capacidade do pool.</summary>
    public int Capacity => _particles.Length;
    /// <summary>Quantidade viva; excedentes são descartados.</summary>
    public int Count { get; private set; }
    /// <summary>Origem da emissão contínua; mover não move partículas já emitidas.</summary>
    public Vector2 Position
    {
        get => _position;
        set { ValidatePosition(value); _position = value; }
    }
    /// <summary>Partículas por segundo, finito e não negativo. Padrão zero; mudar a taxa reinicia a fração acumulada.</summary>
    public float EmissionRate
    {
        get => _rate;
        set
        {
            if (!float.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_rate == value) return;
            _rate = value;
            _emissionElapsed = 0;
        }
    }
    /// <summary>Se a emissão contínua está ligada. Stop não remove partículas existentes.</summary>
    public bool IsEmitting { get; private set; }
    /// <summary>Liga a emissão; o primeiro nascimento ocorre após 1/EmissionRate segundo.</summary>
    public void Start() => IsEmitting = true;
    /// <summary>Para novos nascimentos e descarta a fração acumulada; partículas existentes continuam envelhecendo.</summary>
    public void Stop() { IsEmitting = false; _emissionElapsed = 0; }
    /// <summary>Remove todas as partículas e reinicia a fração de emissão, sem mudar taxa/estado/semente.</summary>
    public void Clear() { Count = 0; _emissionElapsed = 0; }
    /// <summary>Emite imediatamente até o limite livre do pool; funciona mesmo com emissão contínua parada.</summary>
    /// <param name="count">Quantidade não negativa desejada.</param>
    /// <param name="position">Origem finita das novas partículas.</param>
    /// <returns>Quantidade realmente emitida.</returns>
    public int Burst(int count, Vector2 position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ValidatePosition(position);
        var emitted = Math.Min(count, Capacity - Count);
        for (int i = 0; i < emitted; i++) Spawn(position, 0);
        return emitted;
    }
    /// <summary>Avança o efeito e emite apenas nascimentos ainda vivos no fim do intervalo.</summary>
    /// <param name="deltaSeconds">Tempo simulado finito e não negativo.</param>
    /// <remarks>Um delta grande não percorre todos os nascimentos históricos. Aceita os nascimentos mais recentes que cabem no pool; a origem é Position no momento do Update. Não interpola a trajetória do emissor.</remarks>
    public void Update(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        if (deltaSeconds == 0) return;
        var lifetime = Settings.LifetimeSeconds;
        for (int i = 0; i < Count;)
        {
            ref var p = ref _particles[i];
            if (deltaSeconds >= p.Lifetime - p.Age || p.Age + deltaSeconds >= p.Lifetime)
                _particles[i] = _particles[--Count];
            else { p.Age += deltaSeconds; i++; }
        }
        if (!IsEmitting || _rate == 0) return;
        var interval = 1.0 / _rate;
        var wait = interval - _emissionElapsed;
        if (deltaSeconds < wait)
        {
            _emissionElapsed += deltaSeconds;
            return;
        }
        var elapsedAfterFirst = deltaSeconds - wait;
        var due = 1 + Math.Floor(elapsedAfterFirst / interval);
        var remainder = elapsedAfterFirst % interval;
        _emissionElapsed = remainder;
        var surviving = Math.Max(0, Math.Ceiling((lifetime - remainder) / interval));
        var emitted = (int)Math.Min(Capacity - Count, Math.Min(due, surviving));
        for (int i = 0; i < emitted; i++)
        {
            var age = remainder + i * interval;
            if (age < lifetime) Spawn(_position, age);
        }
    }
    private void Spawn(Vector2 position, double age)
    {
        var s = Settings;
        // Defaults draw exactly the same random numbers as before (direction, then speed), so old effects are unchanged.
        Vector2 direction;
        if (s.Spread >= 360) direction = _random.NextDirection();
        else
        {
            var angle = (s.Direction + _random.NextFloat(-s.Spread / 2, s.Spread / 2)) * MathF.PI / 180;
            direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        var velocity = direction * _random.NextFloat(s.MinSpeed, s.MaxSpeed);
        var origin = position + s.Shape switch
        {
            ParticleShape.Circle => _random.NextDirection() * (MathF.Sqrt(_random.NextFloat()) * s.ShapeSize.X),
            ParticleShape.Rectangle => new Vector2(_random.NextFloat(-0.5f, 0.5f) * s.ShapeSize.X, _random.NextFloat(-0.5f, 0.5f) * s.ShapeSize.Y),
            ParticleShape.Line => new Vector2(_random.NextFloat(-0.5f, 0.5f) * s.ShapeSize.X, 0),
            _ => Vector2.Zero,
        };
        double lifetime = s.LifetimeVariance > 0
            ? s.LifetimeSeconds * Math.Max(0.05, 1 + _random.NextFloat(-s.LifetimeVariance, s.LifetimeVariance)) : s.LifetimeSeconds;
        float sizeScale = s.SizeVariance > 0 ? Math.Max(0.05f, 1 + _random.NextFloat(-s.SizeVariance, s.SizeVariance)) : 1;
        float rotation = s.RotationVariance > 0 ? _random.NextFloat(-s.RotationVariance, s.RotationVariance) * MathF.PI / 180 : 0;
        float spin = s.MaxAngularVelocity != 0 || s.MinAngularVelocity != 0
            ? _random.NextFloat(s.MinAngularVelocity, s.MaxAngularVelocity) * MathF.PI / 180 : 0;
        if (age >= lifetime) return;
        _particles[Count++] = new Particle { Origin = origin, Velocity = velocity, Age = age, Lifetime = lifetime, SizeScale = sizeScale, Rotation = rotation, AngularVelocity = spin };
    }

    /// <summary>
    /// Copia o estado das partículas vivas para <paramref name="destination"/> (posição, tamanho, rotação, cor, vida),
    /// para desenhar com outra técnica (prévias de editor, efeitos customizados). Não aloca.
    /// </summary>
    /// <param name="destination">Destino com espaço para até <see cref="Count"/> itens.</param>
    /// <returns>Quantas foram copiadas.</returns>
    public int Snapshot(Span<ParticleSnapshot> destination)
    {
        int written = 0;
        var s = Settings;
        for (int i = 0; i < Count && written < destination.Length; i++)
        {
            ref readonly var p = ref _particles[i];
            var t = p.Age;
            var amount = t / p.Lifetime;
            var size = (float)((1 - amount) * s.StartSize + amount * s.EndSize) * p.SizeScale;
            Vector2 offset;
            if (s.Drag > 0)
            {
                // v' = g - k v  →  x(t) = (v0 - g/k)(1 - e^{-kt})/k + g t / k
                double k = s.Drag, e = 1 - Math.Exp(-k * t);
                offset = new Vector2((float)((p.Velocity.X - s.Gravity.X / k) * e / k + s.Gravity.X * t / k),
                    (float)((p.Velocity.Y - s.Gravity.Y / k) * e / k + s.Gravity.Y * t / k));
            }
            else offset = new Vector2((float)(p.Velocity.X * t + 0.5 * s.Gravity.X * t * t), (float)(p.Velocity.Y * t + 0.5 * s.Gravity.Y * t * t));
            var center = p.Origin + offset;
            if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || !float.IsFinite(size) || size == 0) continue;
            destination[written++] = new ParticleSnapshot(center, size, p.Rotation + (float)(p.AngularVelocity * t), s.ColorAt(amount), (float)amount);
        }
        return written;
    }
    private static void ValidatePosition(Vector2 value)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) throw new ArgumentOutOfRangeException(nameof(value));
    }
    /// <summary>Desenha os quads vivos dentro de um lote já aberto; câmera, clipping, blend e sampler vêm desse lote.</summary>
    /// <param name="batch">Lote do jogo, entre Begin e End.</param>
    /// <remarks>Movimento balístico analítico evita dependência do tamanho do passo. Regiões fora do intervalo numérico float são omitidas do desenho; a vida continua avançando. Não faz culling de tela nem depth sorting.</remarks>
    public void Draw(SpriteBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        _snapshot ??= new ParticleSnapshot[Capacity];
        int count = Snapshot(_snapshot);
        var half = new Vector2(Texture.Width / 2f, Texture.Height / 2f);
        for (int i = 0; i < count; i++)
        {
            ref readonly var p = ref _snapshot[i];
            var left = p.Position.X - p.Size / 2;
            var top = p.Position.Y - p.Size / 2;
            if (!float.IsFinite(left + p.Size) || !float.IsFinite(top + p.Size)) continue;
            if (p.Rotation == 0) batch.Draw(Texture, new RectangleF(left, top, p.Size, p.Size), null, p.Color, 0, Vector2.Zero);
            else batch.Draw(Texture, new RectangleF(p.Position.X, p.Position.Y, p.Size, p.Size), null, p.Color, p.Rotation, half);
        }
    }

    private ParticleSnapshot[]? _snapshot;
}
