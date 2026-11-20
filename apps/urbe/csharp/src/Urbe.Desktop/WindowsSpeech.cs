using System.Runtime.InteropServices;
using System.Security;
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
        if (!OperatingSystem.IsWindows() || Type.GetTypeFromProgID("SAPI.SpVoice") is not { } voiceType ||
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
