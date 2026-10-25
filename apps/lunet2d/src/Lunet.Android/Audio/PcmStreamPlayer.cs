namespace Lunet.Android.Audio;

// Non-blocking writes keep Pause/Stop independent of a blocked native write.
internal interface IPcmAudioOutput : IDisposable
{
    int BufferBytes { get; }
    int UnderrunCount { get; }
    int Write(byte[] buffer, int offset, int count);
    void Play();
    void Pause();
    void Stop();
    void SetVolume(float volume);
}

internal sealed class PcmStreamPlayer : IDisposable
{
    private readonly PcmWaveStream _wave;
    private readonly IPcmAudioOutput _output;
    private readonly bool _loop;
    private readonly Action<string> _reportError;
    private readonly object _sync = new();
    private readonly ManualResetEventSlim _ready = new(true);
    private readonly CancellationTokenSource _cancel = new();
    private readonly Thread _worker;
    private bool _paused, _disposed, _finished, _playing;
    private int _underruns;

    public PcmStreamPlayer(PcmWaveStream wave, IPcmAudioOutput output, bool loop, float volume, Action<string> reportError)
    {
        _wave = wave;
        _output = output;
        _loop = loop;
        _reportError = reportError;
        _output.SetVolume(volume);
        _worker = new Thread(Run) { IsBackground = true, Name = "Lunet PCM music" };
        _worker.Start();
    }

    public int UnderrunCount { get { lock (_sync) return _disposed || _finished ? _underruns : _output.UnderrunCount; } }
    public void SetVolume(float volume) { lock (_sync) { if (!_disposed && !_finished) _output.SetVolume(volume); } }
    public void Pause()
    {
        lock (_sync)
        {
            if (_disposed || _finished || _paused) return;
            _paused = true;
            _playing = false;
            _ready.Reset();
            _output.Pause();
        }
    }
    public void Resume()
    {
        lock (_sync)
        {
            if (_disposed || _finished || !_paused) return;
            _paused = false;
            _ready.Set();
        }
    }

    private void Run()
    {
        try
        {
            // Fixed bounded working set; no whole-song PCM allocation.
            var buffer = new byte[Math.Clamp(_output.BufferBytes, 4096, 262144)];
            int offset = 0, count = 0, primed = 0;
            while (!_cancel.IsCancellationRequested)
            {
                _ready.Wait(_cancel.Token);
                if (offset == count)
                {
                    count = _wave.Read(buffer, _loop);
                    offset = 0;
                    if (count == 0)
                    {
                        lock (_sync)
                        {
                            if (_paused) continue;
                            if (_cancel.IsCancellationRequested) return;
                            // Stop on a streaming AudioTrack drains queued audio. Do not leave
                            // an empty playing track to manufacture an underrun at EOF.
                            _underruns = _output.UnderrunCount;
                            if (!_playing && primed > 0) _output.Play();
                            _output.Stop();
                            _finished = true;
                        }
                        return;
                    }
                }
                int written;
                lock (_sync)
                {
                    if (_paused) continue;
                    if (_cancel.IsCancellationRequested) return;
                    written = _output.Write(buffer, offset, count - offset);
                    if (written < 0 || written > count - offset || written % _wave.FrameBytes != 0)
                        throw new IOException($"Falha na escrita PCM: {written}.");
                    primed = Math.Min(_output.BufferBytes, primed + written);
                    // Prefill before Play (also on resume). A full native buffer returns 0.
                    if (!_playing && (primed >= _output.BufferBytes || written == 0))
                    {
                        _output.Play();
                        _playing = true;
                    }
                }
                offset += written;
                if (written == 0) _cancel.Token.WaitHandle.WaitOne(4);
            }
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_sync)
            {
                try { _underruns = _output.UnderrunCount; _output.Stop(); }
                catch (Exception cleanup) { ex = new AggregateException(ex, cleanup); }
                _finished = true;
            }
            _reportError("Música PCM interrompida: " + ex.Message);
        }
    }

    public void Dispose()
    {
        // Caller serializes disposal; never hold _sync while joining its worker.
        lock (_sync) { if (_disposed) return; _cancel.Cancel(); _ready.Set(); }
        _worker.Join();
        lock (_sync)
        {
            _disposed = true;
            try
            {
                if (!_finished) { _underruns = _output.UnderrunCount; _output.Stop(); }
            }
            finally
            {
                try { _output.Dispose(); }
                finally { _wave.Dispose(); _ready.Dispose(); _cancel.Dispose(); }
            }
        }
    }
}
