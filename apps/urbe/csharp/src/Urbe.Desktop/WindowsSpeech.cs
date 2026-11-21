using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using Urbe.Client;

namespace Urbe.Desktop;

internal static class WindowsSpeech
{
    public static void Install()
    {
        if (!OperatingSystem.IsWindows()) return;
        PlatformAudio.StartPreview = (path, start, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();
            return Task.FromResult<IPreviewAudio>(new PreviewAudio(path, start));
        };
    }

    private sealed class PreviewAudio : IPreviewAudio
    {
        private readonly string _alias = "numa" + Guid.NewGuid().ToString("N");
        private readonly StringBuilder _result = new(64);

        public PreviewAudio(string path, double start)
        {
            Send($"open \"{path}\" type waveaudio alias {_alias}");
            try
            {
                Send($"set {_alias} time format milliseconds");
                Send($"play {_alias} from {(int)(start * 1000)}");
            }
            catch { Dispose(); throw; }
        }

        public double Position => int.TryParse(Query("position"), CultureInfo.InvariantCulture, out var position) ? position / 1000d : 0;
        public bool Ended => Query("mode") == "stopped";
        public bool Failed { get; private set; }

        private string? Query(string item)
        {
            _result.Clear();
            if (MciSendString($"status {_alias} {item}", _result, _result.Capacity, IntPtr.Zero) == 0)
                return _result.ToString();
            Failed = true;
            return null;
        }

        private static void Send(string command)
        {
            if (MciSendString(command, null, 0, IntPtr.Zero) != 0)
                throw new IOException("O Windows não conseguiu reproduzir a trilha.");
        }

        public void Dispose() => MciSendString($"close {_alias}", null, 0, IntPtr.Zero);
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "mciSendStringW")]
    private static extern uint MciSendString(string command, StringBuilder? result, int length, IntPtr callback);

}
