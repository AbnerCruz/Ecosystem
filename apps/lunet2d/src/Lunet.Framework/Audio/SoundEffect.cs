namespace Lunet.Audio;

/// <summary>Efeito sonoro curto carregado na memória. Crie via <c>Content.LoadSound</c>.</summary>
/// <example>
/// <code>
/// var jump = content.LoadSound("Audio/jump.wav");
/// jump.Play(volume: 0.8f, pan: 0f, pitch: 1.2f, loop: false);
/// </code>
/// </example>
public sealed class SoundEffect : IDisposable
{
    private readonly IAudioBackend _backend;
    private readonly AudioMixer _mixer;

    internal SoundEffect(IAudioBackend backend, AudioMixer mixer, int id, string name)
    {
        _backend = backend;
        _mixer = mixer;
        Id = id;
        Name = name;
    }

    /// <summary>Caminho do som em Content.</summary>
    public string Name { get; }
    /// <summary>Verdadeiro depois de liberado.</summary>
    public bool IsDisposed { get; private set; }
    internal int Id { get; }

    /// <summary>Toca uma vez com volume 1 no barramento Sfx.</summary>
    /// <returns>A reprodução iniciada, para ajustar ou parar.</returns>
    public SoundInstance Play() => Play(1f, 0f, 1f, false);

    /// <summary>Toca com volume, balanço, altura e repetição à escolha.</summary>
    /// <param name="volume">0 a 1.</param>
    /// <param name="pan">−1 (esquerda) a 1 (direita).</param>
    /// <param name="pitch">Velocidade relativa: 0,5 a 2 (1 = normal).</param>
    /// <param name="bus">Barramento de volume; nulo = Sfx.</param>
    /// <param name="loop">Se verdadeiro, repete até ser parado.</param>
    /// <returns>A reprodução iniciada, para ajustar ou parar.</returns>
    public SoundInstance Play(float volume, float pan, float pitch, bool loop, AudioBus? bus = null)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return _mixer.Play(_backend, Id, Math.Clamp(volume, 0f, 1f), Math.Clamp(pan, -1f, 1f), Math.Clamp(pitch, 0.5f, 2f), loop, bus ?? _mixer.Sfx);
    }

    /// <summary>Libera o som.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _backend.UnloadSound(Id);
    }
}

/// <summary>Uma reprodução em andamento de um <see cref="SoundEffect"/>.</summary>
/// <example>
/// <code>
/// var engine = sound.Play(0.6f, 0f, 1f, true);
/// engine.Pitch = 1.5f;
/// engine.FadeTo(0f, 1f, stopWhenDone: true);
/// </code>
/// </example>
public sealed class SoundInstance
{
    private readonly IAudioBackend _backend;
    private readonly AudioMixer _mixer;
    private float _volume, _pan, _pitch;
    private float _appliedVolume = -1f, _appliedPan = 2f, _appliedPitch = -1f;
    private float _fadeStart, _fadeTarget, _fadeElapsed, _fadeDuration;
    private bool _stopWhenFaded;

    internal SoundInstance(IAudioBackend backend, AudioMixer mixer, AudioBus bus, int streamId, float volume, float pan, float pitch)
    {
        _backend = backend;
        _mixer = mixer;
        Bus = bus;
        StreamId = streamId;
        _volume = volume; _pan = pan; _pitch = pitch;
        _appliedVolume = volume * mixer.EffectiveOf(bus);
        _appliedPan = pan;
        _appliedPitch = pitch;
    }

    internal int StreamId { get; }
    /// <summary>Barramento que multiplica o volume desta reprodução.</summary>
    public AudioBus Bus { get; }

    /// <summary>Falso se o som não pôde ser iniciado (ainda carregando ou sem canais livres).</summary>
    public bool Started => StreamId != 0;

    /// <summary>Verdadeiro depois de Stop ou do fim de um fade-out.</summary>
    public bool IsStopped { get; private set; }

    /// <summary>Volume desta reprodução, de 0 a 1 (antes do barramento).</summary>
    public float Volume { get => _volume; set { _volume = Math.Clamp(value, 0f, 1f); _fadeDuration = 0; } }
    /// <summary>Balanço de −1 (esquerda) a 1 (direita).</summary>
    public float Pan { get => _pan; set => _pan = Math.Clamp(value, -1f, 1f); }
    /// <summary>Velocidade relativa de 0,5 a 2.</summary>
    public float Pitch { get => _pitch; set => _pitch = Math.Clamp(value, 0.5f, 2f); }

    /// <summary>Muda o volume gradualmente. Com <paramref name="stopWhenDone"/>, para o som ao terminar (fade-out).</summary>
    /// <param name="volume">Volume de 0 (mudo) a 1 (máximo).</param>
    /// <param name="seconds">Duração, em segundos.</param>
    /// <param name="stopWhenDone">Se verdadeiro, para o som quando o volume chegar ao valor final.</param>
    public void FadeTo(float volume, float seconds, bool stopWhenDone = false)
    {
        volume = Math.Clamp(volume, 0f, 1f);
        if (seconds <= 0)
        {
            Volume = volume;
            if (stopWhenDone) Stop();
            return;
        }
        _fadeStart = _volume;
        _fadeTarget = volume;
        _fadeElapsed = 0;
        _fadeDuration = seconds;
        _stopWhenFaded = stopWhenDone;
    }

    /// <summary>Para a reprodução.</summary>
    public void Stop()
    {
        if (IsStopped) return;
        IsStopped = true;
        if (Started) _backend.Stop(StreamId);
    }

    internal void Update(float deltaSeconds)
    {
        if (IsStopped || !Started) return;
        if (_fadeDuration > 0)
        {
            _fadeElapsed += deltaSeconds;
            var t = Math.Min(1f, _fadeElapsed / _fadeDuration);
            _volume = _fadeStart + (_fadeTarget - _fadeStart) * t;
            if (t >= 1f)
            {
                _fadeDuration = 0;
                if (_stopWhenFaded) { Stop(); return; }
            }
        }
        var effective = _volume * _mixer.EffectiveOf(Bus);
        if (MathF.Abs(effective - _appliedVolume) > 0.001f || _pan != _appliedPan || _pitch != _appliedPitch)
        {
            _backend.SetStream(StreamId, effective, _pan, _pitch);
            _appliedVolume = effective;
            _appliedPan = _pan;
            _appliedPitch = _pitch;
        }
    }
}
