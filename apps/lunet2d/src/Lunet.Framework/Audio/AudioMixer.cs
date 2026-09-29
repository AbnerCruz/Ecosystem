namespace Lunet.Audio;

/// <summary>
/// Mistura de áudio do jogo (<c>Audio</c>): barramentos de volume, música com fade, fades de efeitos e pausa geral.
/// É atualizada pelo host a cada passo fixo.
/// </summary>
public sealed class AudioMixer
{
    private const int MaxTrackedInstances = 64;

    private readonly IAudioBackend _backend;
    private readonly Dictionary<string, AudioBus> _buses = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SoundInstance> _instances = [];
    private Music? _music;
    private float _musicGain = 1f, _musicTarget = 1f, _musicRate;
    private bool _stopMusicWhenFaded;
    private float _appliedMusicVolume = -1f;
    private bool _paused;

    /// <summary>Cria a mistura de áudio sobre um backend.</summary>
    /// <param name="backend">Motor de áudio da plataforma.</param>
    public AudioMixer(IAudioBackend backend)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        Master = new AudioBus("Master");
        Sfx = GetBus("Sfx");
        MusicBus = GetBus("Music");
    }

    /// <summary>Barramento geral: multiplica o volume de todos os outros.</summary>
    public AudioBus Master { get; }
    /// <summary>Barramento dos efeitos sonoros (padrão de SoundEffect.Play).</summary>
    public AudioBus Sfx { get; }
    /// <summary>Barramento da música.</summary>
    public AudioBus MusicBus { get; }

    /// <summary>Música atual (tocando ou pausada), ou nulo.</summary>
    public Music? CurrentMusic => _music;

    /// <summary>Verdadeiro enquanto o áudio está pausado.</summary>
    public bool IsPaused => _paused;

    /// <summary>Devolve o barramento pelo nome, criando-o se não existir.</summary>
    public AudioBus GetBus(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_buses.TryGetValue(name, out var bus)) _buses[name] = bus = new AudioBus(name);
        return bus;
    }

    internal float EffectiveOf(AudioBus bus) => Master.Effective * bus.Effective;

    internal SoundInstance Play(IAudioBackend backend, int soundId, float volume, float pan, float pitch, bool loop, AudioBus bus)
    {
        var streamId = backend.Play(soundId, volume * EffectiveOf(bus), pan, pitch, loop);
        var instance = new SoundInstance(backend, this, bus, streamId, volume, pan, pitch);
        if (instance.Started)
        {
            _instances.Add(instance);
            if (_instances.Count > MaxTrackedInstances) _instances.RemoveAt(0);
        }
        return instance;
    }

    /// <summary>Toca a música, substituindo a atual. Com <paramref name="fadeInSeconds"/> &gt; 0 o volume sobe gradualmente.</summary>
    public void PlayMusic(Music music, bool loop = true, float fadeInSeconds = 0f)
    {
        ArgumentNullException.ThrowIfNull(music);
        ObjectDisposedException.ThrowIf(music.IsDisposed, music);
        _music = music;
        _stopMusicWhenFaded = false;
        _musicGain = fadeInSeconds > 0 ? 0f : 1f;
        _musicTarget = 1f;
        _musicRate = fadeInSeconds > 0 ? 1f / fadeInSeconds : 0f;
        _appliedMusicVolume = _musicGain * EffectiveOf(MusicBus);
        _backend.PlayMusic(music.Id, _appliedMusicVolume, loop);
        if (_paused) _backend.PauseMusic();
    }

    /// <summary>Para a música; com <paramref name="fadeOutSeconds"/> &gt; 0 ela some gradualmente antes de parar.</summary>
    public void StopMusic(float fadeOutSeconds = 0f)
    {
        if (_music is null) return;
        if (fadeOutSeconds <= 0)
        {
            _backend.StopMusic();
            _music = null;
            return;
        }
        _musicTarget = 0f;
        _musicRate = _musicGain / fadeOutSeconds;
        _stopMusicWhenFaded = true;
    }

    /// <summary>Muda o volume da música gradualmente (0 a 1, relativo ao barramento Music).</summary>
    public void FadeMusicTo(float volume, float seconds)
    {
        _musicTarget = Math.Clamp(volume, 0f, 1f);
        _stopMusicWhenFaded = false;
        if (seconds <= 0) { _musicGain = _musicTarget; _musicRate = 0; }
        else _musicRate = MathF.Abs(_musicTarget - _musicGain) / seconds;
    }

    /// <summary>Pausa música e efeitos (o host chama ao ir para segundo plano ou pausar o jogo).</summary>
    public void PauseAll()
    {
        if (_paused) return;
        _paused = true;
        _backend.PauseSounds();
        if (_music is not null) _backend.PauseMusic();
    }

    /// <summary>Retoma música e efeitos pausados.</summary>
    public void ResumeAll()
    {
        if (!_paused) return;
        _paused = false;
        _backend.ResumeSounds();
        if (_music is not null) _backend.ResumeMusic();
    }

    internal void Update(float deltaSeconds)
    {
        if (_music is not null && _musicRate > 0 && _musicGain != _musicTarget)
        {
            var step = _musicRate * deltaSeconds;
            _musicGain = _musicGain < _musicTarget ? MathF.Min(_musicTarget, _musicGain + step) : MathF.Max(_musicTarget, _musicGain - step);
            if (_musicGain == _musicTarget && _stopMusicWhenFaded)
            {
                _backend.StopMusic();
                _music = null;
            }
        }
        if (_music is not null)
        {
            var volume = _musicGain * EffectiveOf(MusicBus);
            if (MathF.Abs(volume - _appliedMusicVolume) > 0.001f)
            {
                _backend.SetMusicVolume(volume);
                _appliedMusicVolume = volume;
            }
        }

        for (var i = _instances.Count - 1; i >= 0; i--)
        {
            _instances[i].Update(deltaSeconds);
            if (_instances[i].IsStopped) _instances.RemoveAt(i);
        }
    }
}
