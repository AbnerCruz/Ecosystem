namespace Lunet.Audio;

/// <summary>Efeito sonoro curto carregado na memória. Crie via <c>Content.LoadSound</c>.</summary>
public sealed class SoundEffect : IDisposable
{
    private readonly IAudioBackend _backend;

    internal SoundEffect(IAudioBackend backend, int id, string name)
    {
        _backend = backend;
        Id = id;
        Name = name;
    }

    public string Name { get; }
    public bool IsDisposed { get; private set; }
    internal int Id { get; }

    /// <summary>Toca uma vez com volume 1.</summary>
    public SoundInstance Play() => Play(1f, 0f, 1f, false);

    /// <param name="volume">0 a 1.</param>
    /// <param name="pan">−1 (esquerda) a 1 (direita).</param>
    /// <param name="pitch">Velocidade relativa: 0,5 a 2 (1 = normal).</param>
    public SoundInstance Play(float volume, float pan, float pitch, bool loop)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        volume = Math.Clamp(volume, 0f, 1f);
        pan = Math.Clamp(pan, -1f, 1f);
        pitch = Math.Clamp(pitch, 0.5f, 2f);
        return new SoundInstance(_backend, _backend.Play(Id, volume, pan, pitch, loop), volume, pan, pitch);
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        _backend.UnloadSound(Id);
    }
}

/// <summary>Uma reprodução em andamento de um <see cref="SoundEffect"/>.</summary>
public sealed class SoundInstance
{
    private readonly IAudioBackend _backend;
    private float _volume, _pan, _pitch;

    internal SoundInstance(IAudioBackend backend, int streamId, float volume, float pan, float pitch)
    {
        _backend = backend;
        StreamId = streamId;
        _volume = volume; _pan = pan; _pitch = pitch;
    }

    internal int StreamId { get; }

    /// <summary>Falso se o som não pôde ser iniciado (ainda carregando ou sem canais livres).</summary>
    public bool Started => StreamId != 0;

    public float Volume { get => _volume; set { _volume = Math.Clamp(value, 0f, 1f); Apply(); } }
    public float Pan { get => _pan; set { _pan = Math.Clamp(value, -1f, 1f); Apply(); } }
    public float Pitch { get => _pitch; set { _pitch = Math.Clamp(value, 0.5f, 2f); Apply(); } }

    public void Stop()
    {
        if (Started) _backend.Stop(StreamId);
    }

    private void Apply()
    {
        if (Started) _backend.SetStream(StreamId, _volume, _pan, _pitch);
    }
}
