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
            if (deltaSeconds >= lifetime - p.Age || p.Age + deltaSeconds >= lifetime)
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
        var velocity = _random.NextDirection() * _random.NextFloat(Settings.MinSpeed, Settings.MaxSpeed);
        _particles[Count++] = new Particle { Origin = position, Velocity = velocity, Age = age };
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
        var s = Settings;
        for (int i = 0; i < Count; i++)
        {
            ref readonly var p = ref _particles[i];
            var t = p.Age;
            var amount = t / s.LifetimeSeconds;
            var size = (float)((1 - amount) * s.StartSize + amount * s.EndSize);
            var x = (float)(p.Origin.X + p.Velocity.X * t + 0.5 * s.Gravity.X * t * t);
            var y = (float)(p.Origin.Y + p.Velocity.Y * t + 0.5 * s.Gravity.Y * t * t);
            var left = x - size / 2;
            var top = y - size / 2;
            if (!float.IsFinite(left) || !float.IsFinite(top) || !float.IsFinite(left + size) || !float.IsFinite(top + size) || size == 0) continue;
            var color = new Color(Channel(s.StartColor.R, s.EndColor.R, amount), Channel(s.StartColor.G, s.EndColor.G, amount),
                Channel(s.StartColor.B, s.EndColor.B, amount), Channel(s.StartColor.A, s.EndColor.A, amount));
            batch.Draw(Texture, new RectangleF(left, top, size, size), null, color, 0, Vector2.Zero);
        }
    }
    private static byte Channel(byte from, byte to, double amount) => (byte)Math.Round((1 - amount) * from + amount * to);
}
