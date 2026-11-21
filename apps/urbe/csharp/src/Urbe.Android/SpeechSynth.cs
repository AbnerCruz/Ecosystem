using Urbe.Client;

namespace Urbe.AndroidHost;

internal static class SpeechSynth
{
    public static void Install()
    {
        PlatformAudio.StartPreview = StartPreviewAsync;
    }

    private static async Task<IPreviewAudio> StartPreviewAsync(string path, double start, CancellationToken cancel)
    {
        var playback = new PreviewAudio();
        try
        {
            await playback.StartAsync(path, start, cancel);
            return playback;
        }
        catch
        {
            playback.Dispose();
            throw;
        }
    }

    private sealed class PreviewAudio : IPreviewAudio
    {
        private readonly Android.Media.MediaPlayer _player = new();
        private readonly TaskCompletionSource _prepared = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _seeked = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public double Position => _player.CurrentPosition / 1000d;
        public bool Ended { get; private set; }
        public bool Failed { get; private set; }

        public async Task StartAsync(string path, double start, CancellationToken cancel)
        {
            _player.Prepared += (_, _) => _prepared.TrySetResult();
            _player.SeekComplete += (_, _) => _seeked.TrySetResult();
            _player.Completion += (_, _) => Ended = true;
            _player.Error += (_, _) =>
            {
                Failed = true;
                _prepared.TrySetResult();
                _seeked.TrySetResult();
            };
            using (var source = new Java.IO.FileInputStream(path))
                _player.SetDataSource(source.FD);
            _player.PrepareAsync();
            await _prepared.Task.WaitAsync(TimeSpan.FromSeconds(10), cancel);
            if (!Failed && start > 0)
            {
                _player.SeekTo((int)(start * 1000));
                await _seeked.Task.WaitAsync(TimeSpan.FromSeconds(10), cancel);
            }
            cancel.ThrowIfCancellationRequested();
            if (Failed) throw new IOException("O Android não conseguiu reproduzir a trilha.");
            _player.Start();
        }

        public void Dispose()
        {
            _player.Release();
            _player.Dispose();
        }
    }

}
