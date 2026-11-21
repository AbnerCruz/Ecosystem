using System.Runtime.InteropServices;
using System.Security;
using System.Globalization;
using System.Text;
using Microsoft.CSharp.RuntimeBinder;
using Urbe.Client;

namespace Urbe.Desktop;

/// <summary>
/// #956 on Windows: the podcast's turns spoken by the system's own speech engine (SAPI, offline) into WAV files.
/// Two Portuguese voices when Windows has them; otherwise the second host speaks the same voice at a lower pitch.
/// </summary>
internal static class WindowsSpeech
{
    private const int CreateForWrite = 3, Wav22kHz16BitMono = 22, IsXml = 8, IsNotXml = 16;

    public static void Install()
    {
        if (!OperatingSystem.IsWindows()) return;
        PlatformAudio.StartPreview = (path, start, cancel) =>
        {
            cancel.ThrowIfCancellationRequested();
            return Task.FromResult<IPreviewAudio>(new PreviewAudio(path, start));
        };
        if (Type.GetTypeFromProgID("SAPI.SpVoice") is not { } voiceType ||
            Type.GetTypeFromProgID("SAPI.SpFileStream") is not { } streamType) return;
        PlatformAudio.PlayWav = (path, cancel) => Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows()) return;
            using var stop = cancel.Register(() => PlaySound(null, IntPtr.Zero, 0));
            PlaySound(path, IntPtr.Zero, SndFilename | SndNoDefault);   // synchronous: returns at the end or when stopped
        }, cancel);
        PlatformAudio.Voices = () => OperatingSystem.IsWindows() ? Names(voiceType) : [];
        PlatformAudio.SynthesizeToWav = (text, voice, target, cancel) => Task.Run(() => OperatingSystem.IsWindows() && Speak(voiceType, streamType, text, voice, target), cancel);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private const uint SndFilename = 0x00020000, SndNoDefault = 0x0002;

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, EntryPoint = "PlaySoundW")]
    private static extern bool PlaySound(string? sound, IntPtr module, uint flags);

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

    /// <summary>The Portuguese voices Windows has (all voices when it has none in Portuguese), by description.</summary>
    private static IReadOnlyList<string> Names(Type voiceType)
    {
        dynamic? voice = null;
        try
        {
            voice = Activator.CreateInstance(voiceType)!;
            dynamic voices = voice.GetVoices("Language=416", "");
            if (voices.Count == 0) voices = voice.GetVoices("", "");
            var names = new List<string>();
            for (int i = 0; i < voices.Count; i++) names.Add((string)voices.Item(i).GetDescription());
            return names;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { return []; }
        finally { if (voice is not null) Marshal.FinalReleaseComObject(voice); }
    }

    private static bool Speak(Type voiceType, Type streamType, string text, int host, string target)
    {
        dynamic? voice = null, file = null;
        try
        {
            voice = Activator.CreateInstance(voiceType)!;
            file = Activator.CreateInstance(streamType)!;
            dynamic voices = voice.GetVoices("Language=416", ""); // pt-BR
            int count = voices.Count;
            if (count == 0) { voices = voice.GetVoices("", ""); count = voices.Count; }
            // the person's voice speaks first; the podcast's second host takes the next one
            int preferred = -1;
            for (int i = 0; i < count && PlatformAudio.PreferredVoice is { } name; i++)
                if ((string)voices.Item(i).GetDescription() == name) { preferred = i; break; }
            int at = preferred < 0 ? Math.Min(host, count - 1) : host == 0 ? preferred : (preferred + 1) % Math.Max(1, count);
            if (count > 0) voice.Voice = voices.Item(at);
            file.Format.Type = Wav22kHz16BitMono;
            file.Open(target, CreateForWrite, false);
            voice.AudioOutputStream = file;
            bool lower = host == 1 && count < 2;
            voice.Speak(lower ? "<pitch absmiddle=\"-6\">" + SecurityElement.Escape(text) + "</pitch>" : text, lower ? IsXml : IsNotXml);
            file.Close();
            return true;
        }
        catch (Exception e) when (e is COMException or RuntimeBinderException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
        finally
        {
            if (file is not null) Marshal.FinalReleaseComObject(file);
            if (voice is not null) Marshal.FinalReleaseComObject(voice);
        }
    }
}
