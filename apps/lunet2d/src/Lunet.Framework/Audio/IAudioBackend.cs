namespace Lunet.Audio;

/// <summary>Contrato entre o framework e o motor de áudio da plataforma (SoundPool e MediaPlayer no Android).</summary>
public interface IAudioBackend : IDisposable
{
    /// <summary>Registra um efeito sonoro (WAV/OGG/MP3 em bytes). Devolve um id &gt; 0.</summary>
    int LoadSound(byte[] data, string name);

    /// <summary>Libera um efeito sonoro.</summary>
    /// <param name="soundId">Id devolvido por LoadSound.</param>
    void UnloadSound(int soundId);

    /// <summary>Começa a tocar. Devolve o id do fluxo, ou 0 se não foi possível (ex.: som ainda carregando).</summary>
    int Play(int soundId, float volume, float pan, float pitch, bool loop);

    /// <summary>Muda volume, pan e velocidade de um som em andamento.</summary>
    /// <param name="streamId">Id do fluxo.</param>
    /// <param name="volume">Volume final de 0 a 1.</param>
    /// <param name="pan">Balanço de −1 a 1.</param>
    /// <param name="pitch">Velocidade relativa.</param>
    void SetStream(int streamId, float volume, float pan, float pitch);
    /// <summary>Para um som em andamento.</summary>
    /// <param name="streamId">Id do fluxo.</param>
    void Stop(int streamId);

    /// <summary>Pausa todos os efeitos em andamento (app em segundo plano).</summary>
    void PauseSounds();

    /// <summary>Retoma os efeitos pausados por PauseSounds.</summary>
    void ResumeSounds();

    /// <summary>Registra uma música (arquivo longo tocado em streaming). Devolve um id &gt; 0.</summary>
    int LoadMusic(byte[] data, string name);

    /// <summary>Libera uma música.</summary>
    /// <param name="musicId">Id devolvido por LoadMusic.</param>
    void UnloadMusic(int musicId);

    /// <summary>Toca a música (substitui a que estiver tocando).</summary>
    void PlayMusic(int musicId, float volume, bool loop);

    /// <summary>Muda o volume da música em andamento.</summary>
    /// <param name="volume">Volume final de 0 a 1.</param>
    void SetMusicVolume(float volume);
    /// <summary>Pausa a música.</summary>
    void PauseMusic();
    /// <summary>Retoma a música pausada.</summary>
    void ResumeMusic();
    /// <summary>Para a música.</summary>
    void StopMusic();
}

/// <summary>Áudio desligado: aceita chamadas e não toca nada.</summary>
public sealed class NullAudioBackend : IAudioBackend
{
    private int _next = 1;
    /// <summary>Aceita o som e devolve um id, sem tocar nada.</summary>
    public int LoadSound(byte[] data, string name) => _next++;
    /// <summary>Não faz nada.</summary>
    public void UnloadSound(int soundId) { }
    /// <summary>Não toca; devolve 0 (não iniciado).</summary>
    public int Play(int soundId, float volume, float pan, float pitch, bool loop) => 0;
    /// <summary>Não faz nada.</summary>
    public void SetStream(int streamId, float volume, float pan, float pitch) { }
    /// <summary>Não faz nada.</summary>
    public void Stop(int streamId) { }
    /// <summary>Não faz nada.</summary>
    public void PauseSounds() { }
    /// <summary>Não faz nada.</summary>
    public void ResumeSounds() { }
    /// <summary>Aceita a música e devolve um id, sem tocar nada.</summary>
    public int LoadMusic(byte[] data, string name) => _next++;
    /// <summary>Não faz nada.</summary>
    public void UnloadMusic(int musicId) { }
    /// <summary>Não toca nada.</summary>
    public void PlayMusic(int musicId, float volume, bool loop) { }
    /// <summary>Não faz nada.</summary>
    public void SetMusicVolume(float volume) { }
    /// <summary>Não faz nada.</summary>
    public void PauseMusic() { }
    /// <summary>Não faz nada.</summary>
    public void ResumeMusic() { }
    /// <summary>Não faz nada.</summary>
    public void StopMusic() { }
    /// <summary>Libera recursos (não há nenhum).</summary>
    public void Dispose() { }
}
