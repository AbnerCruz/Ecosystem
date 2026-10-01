using Android.Media;
using Lunet.Audio;

namespace Lunet.Android;

/// <summary>Áudio de efeitos com SoundPool (baixa latência). Músicas longas virão numa etapa posterior.</summary>
internal sealed class AndroidAudioBackend : IAudioBackend
{
    private readonly SoundPool _pool;
    private readonly string _cacheDirectory;
    private readonly HashSet<int> _loaded = [];
    private readonly Dictionary<int, string> _files = [];
    private readonly Dictionary<int, string> _musicFiles = [];
    private MediaPlayer? _player;
    private int _nextMusic = 1;

    public AndroidAudioBackend(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(cacheDirectory);
        var attributes = new AudioAttributes.Builder()!
            .SetUsage(AudioUsageKind.Game)!
            .SetContentType(AudioContentType.Sonification)!
            .Build()!;
        _pool = new SoundPool.Builder()!.SetMaxStreams(16)!.SetAudioAttributes(attributes)!.Build()!;
        _pool.LoadComplete += (_, e) =>
        {
            if (e.Status == 0) lock (_loaded) _loaded.Add(e.SampleId);
        };
    }

    public int LoadSound(byte[] data, string name)
    {
        // SoundPool só carrega de arquivo/descritor: grava no cache do app.
        var file = Path.Combine(_cacheDirectory, Guid.NewGuid().ToString("N") + Path.GetExtension(name));
        File.WriteAllBytes(file, data);
        var id = _pool.Load(file, 1);
        _files[id] = file;
        return id;
    }

    public void UnloadSound(int soundId)
    {
        _pool.Unload(soundId);
        lock (_loaded) _loaded.Remove(soundId);
        if (_files.Remove(soundId, out var file)) File.Delete(file);
    }

    public int Play(int soundId, float volume, float pan, float pitch, bool loop)
    {
        lock (_loaded)
        {
            if (!_loaded.Contains(soundId)) return 0;
        }
        var (left, right) = Channels(volume, pan);
        return _pool.Play(soundId, left, right, 1, loop ? -1 : 0, pitch);
    }

    public void SetStream(int streamId, float volume, float pan, float pitch)
    {
        var (left, right) = Channels(volume, pan);
        _pool.SetVolume(streamId, left, right);
        _pool.SetRate(streamId, pitch);
    }

    public void Stop(int streamId) => _pool.Stop(streamId);

    public void PauseSounds() => _pool.AutoPause();

    public void ResumeSounds() => _pool.AutoResume();

    public int LoadMusic(byte[] data, string name)
    {
        var file = Path.Combine(_cacheDirectory, "music-" + Guid.NewGuid().ToString("N") + Path.GetExtension(name));
        File.WriteAllBytes(file, data);
        var id = _nextMusic++;
        _musicFiles[id] = file;
        return id;
    }

    public void UnloadMusic(int musicId)
    {
        if (_musicFiles.Remove(musicId, out var file))
        {
            if (_currentMusic == musicId) StopMusic();
            File.Delete(file);
        }
    }

    private int _currentMusic;

    public void PlayMusic(int musicId, float volume, bool loop)
    {
        StopMusic();
        if (!_musicFiles.TryGetValue(musicId, out var file)) return;
        var player = new MediaPlayer();
        try
        {
            player.SetDataSource(file);
            player.Looping = loop;
            player.SetVolume(volume, volume);
            player.Prepare();
            player.Start();
        }
        catch
        {
            player.Release();
            throw;
        }
        _player = player;
        _currentMusic = musicId;
    }

    public void SetMusicVolume(float volume) => _player?.SetVolume(volume, volume);

    public void PauseMusic()
    {
        if (_player is { IsPlaying: true }) _player.Pause();
    }

    public void ResumeMusic() => _player?.Start();

    public void StopMusic()
    {
        if (_player is null) return;
        try { _player.Stop(); }
        catch (Java.Lang.IllegalStateException)
        {
            // Já parado ou nunca iniciado: liberar logo abaixo resolve.
        }
        _player.Release();
        _player = null;
        _currentMusic = 0;
    }

    private static (float Left, float Right) Channels(float volume, float pan) =>
        (volume * Math.Min(1f, 1f - pan), volume * Math.Min(1f, 1f + pan));

    public void Dispose()
    {
        StopMusic();
        foreach (var file in _musicFiles.Values) File.Delete(file);
        _musicFiles.Clear();
        _pool.Release();
        foreach (var file in _files.Values) File.Delete(file);
        _files.Clear();
    }
}
