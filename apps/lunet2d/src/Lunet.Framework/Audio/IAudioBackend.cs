namespace Lunet.Audio;

/// <summary>Contrato entre o framework e o motor de áudio da plataforma (SoundPool e MediaPlayer no Android).</summary>
public interface IAudioBackend : IDisposable
{
    /// <summary>Registra um efeito sonoro (WAV/OGG/MP3 em bytes). Devolve um id &gt; 0.</summary>
    int LoadSound(byte[] data, string name);

    void UnloadSound(int soundId);

    /// <summary>Começa a tocar. Devolve o id do fluxo, ou 0 se não foi possível (ex.: som ainda carregando).</summary>
    int Play(int soundId, float volume, float pan, float pitch, bool loop);

    void SetStream(int streamId, float volume, float pan, float pitch);
    void Stop(int streamId);

    /// <summary>Pausa todos os efeitos em andamento (app em segundo plano).</summary>
    void PauseSounds();

    void ResumeSounds();

    /// <summary>Registra uma música (arquivo longo tocado em streaming). Devolve um id &gt; 0.</summary>
    int LoadMusic(byte[] data, string name);

    void UnloadMusic(int musicId);

    /// <summary>Toca a música (substitui a que estiver tocando).</summary>
    void PlayMusic(int musicId, float volume, bool loop);

    void SetMusicVolume(float volume);
    void PauseMusic();
    void ResumeMusic();
    void StopMusic();
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
    public void PauseSounds() { }
    public void ResumeSounds() { }
    public int LoadMusic(byte[] data, string name) => _next++;
    public void UnloadMusic(int musicId) { }
    public void PlayMusic(int musicId, float volume, bool loop) { }
    public void SetMusicVolume(float volume) { }
    public void PauseMusic() { }
    public void ResumeMusic() { }
    public void StopMusic() { }
    public void Dispose() { }
}
