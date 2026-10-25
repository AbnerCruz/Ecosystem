using Android.Media;
using Lunet.Audio;
using Lunet.Android.Audio;

namespace Lunet.Android;

/// <summary>Efeitos SoundPool, música PCM AudioTrack e demais formatos MediaPlayer.</summary>
internal sealed class AndroidAudioBackend : IAudioBackend
{
    private readonly SoundPool _pool;
    private readonly string _cacheDirectory;
    private readonly HashSet<int> _loaded = [];
    private readonly Dictionary<int, string> _files = [];
    private readonly Dictionary<int, string> _musicFiles = [];
    private MediaPlayer? _player;
    private PcmStreamPlayer? _pcm;
    private readonly object _musicSync = new();
    private readonly Action<string> _reportError;
    private long? _completedUnderruns;

    // Session total of measured WAV tracks only, never a fabricated SoundPool metric.
    public long? MusicUnderruns
    {
        get { lock (_musicSync) return _pcm is null ? _completedUnderruns : (_completedUnderruns ?? 0) + _pcm.UnderrunCount; }
    }
    private int _nextMusic = 1;

    public AndroidAudioBackend(string cacheDirectory, Action<string> reportError)
    {
        _cacheDirectory = cacheDirectory;
        _reportError = reportError;
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
        lock (_musicSync)
        {
            StopMusic();
            if (!_musicFiles.TryGetValue(musicId, out var file)) return;
            var source = File.OpenRead(file);
            PcmWaveStream? wave;
            try { wave = PcmWaveStream.TryRead(source); }
            catch { source.Dispose(); throw; }
            if (wave is not null)
            {
                IPcmAudioOutput? output = null;
                try
                {
                    output = new AndroidPcmAudioOutput(wave);
                    _pcm = new PcmStreamPlayer(wave, output, loop, volume, _reportError);
                }
                catch { output?.Dispose(); wave.Dispose(); throw; }
                _currentMusic = musicId;
                return;
            }
            source.Dispose();
            var player = new MediaPlayer();
            try
            {
                player.SetDataSource(file);
                player.Looping = loop;
                player.SetVolume(volume, volume);
                player.Prepare();
                player.Start();
            }
            catch { player.Release(); player.Dispose(); throw; }
            _player = player;
            _currentMusic = musicId;
        }
    }

    public void SetMusicVolume(float volume)
    {
        lock (_musicSync) { _pcm?.SetVolume(volume); _player?.SetVolume(volume, volume); }
    }

    public void PauseMusic()
    {
        lock (_musicSync)
        {
            _pcm?.Pause();
            if (_player is { IsPlaying: true }) _player.Pause();
        }
    }

    public void ResumeMusic()
    {
        lock (_musicSync) { _pcm?.Resume(); _player?.Start(); }
    }

    public void StopMusic()
    {
        lock (_musicSync)
        {
            if (_pcm is not null)
            {
                _pcm.Dispose();
                _completedUnderruns = (_completedUnderruns ?? 0) + _pcm.UnderrunCount;
                _pcm = null;
            }
            if (_player is not null)
            {
                try { _player.Stop(); }
                catch (Java.Lang.IllegalStateException) { /* Already ended; release below. */ }
                _player.Release();
                _player.Dispose();
                _player = null;
            }
            _currentMusic = 0;
        }
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
