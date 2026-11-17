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
        PlatformAudio.SynthesizeToWav = (text, voice, target, cancel) => Task.Run(() => OperatingSystem.IsWindows() && Speak(voiceType, streamType, text, voice, target), cancel);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
            if (count > 0) voice.Voice = voices.Item(Math.Min(host, count - 1));
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
