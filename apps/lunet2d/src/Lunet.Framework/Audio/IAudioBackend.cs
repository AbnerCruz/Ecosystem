namespace Lunet.Audio;

/// <summary>Contrato entre o framework e o motor de áudio da plataforma (SoundPool no Android).</summary>
public interface IAudioBackend : IDisposable
{
    /// <summary>Registra um efeito sonoro (WAV/OGG/MP3 em bytes). Devolve um id &gt; 0.</summary>
    int LoadSound(byte[] data, string name);

    void UnloadSound(int soundId);

    /// <summary>Começa a tocar. Devolve o id do fluxo, ou 0 se não foi possível (ex.: som ainda carregando).</summary>
    int Play(int soundId, float volume, float pan, float pitch, bool loop);

    void SetStream(int streamId, float volume, float pan, float pitch);
    void Stop(int streamId);
}

/// <summary>Áudio desligado: aceita chamadas e não toca nada.</summary>
public sealed class NullAudioBackend : IAudioBackend
{
    private int _next = 1;
    public int LoadSound(byte[] data, string name) => _next++;
    public void UnloadSound(int soundId) { }
    public int Play(int soundId, float volume, float pan, float pitch, bool loop) => 0;
    public void SetStream(int streamId, float volume, float pan, float pitch) { }
    public void Stop(int streamId) { }
    public void Dispose() { }
}
