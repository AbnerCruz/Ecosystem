using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-058: "importar ou gravar áudio longo, como uma aula, transcrevê-lo e usar IA para organizar a transcrição em uma
/// nota pronta para revisão". An audio file of the vault becomes a note next to it, part by part, with the device's
/// recognizer; it can be stopped, and nothing is left behind then.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static short[] Lecture()
    {
        var random = new Random(3);
        var samples = new List<short>();
        foreach (var (seconds, loud) in new[] { (41.0, true), (1.0, false), (46.0, true), (1.0, false), (41.0, true) })
            for (int i = 0; i < seconds * LongAudio.Rate; i++) samples.Add((short)(loud ? random.Next(-9000, 9000) : random.Next(-30, 30)));
        return [.. samples];
    }

    private static int TempLeft() => Directory.GetDirectories(Path.GetTempPath(), "urbe-audio-*").Length;

    [Fact]
    public async Task Transcription_ALongRecordingBecomesANoteToReview() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, vault) = await OpenRealVaultAsync(("Aulas/Comece.md", "# Aulas\n"), ("Aulas/aula.m4a", "áudio"));
        int calls = 0, tempBefore = TempLeft();
        PlatformAudio.DecodeToWav = (source, target, progress, token) =>
        {
            Assert.Equal("áudio", File.ReadAllText(source));   // the vault's file, copied out to be decoded
            File.WriteAllBytes(target, LongAudio.Wav(Lecture()));
            progress?.Report(1);
            return Task.FromResult(true);
        };
        PlatformAudio.TranscribeWav = (wav, token) => Task.FromResult(++calls == 2 ? null : "parte " + calls);
        try
        {
            Assert.True(view.CanTranscribe("Aulas/aula.m4a"));
            Assert.False(view.CanTranscribe("Aulas/Comece.md"));
            var path = await view.TranscribeAudioToNoteAsync("Aulas/aula.m4a");
            Assert.Equal("Aulas/aula — transcrição.md", path);
            var text = await ReadWhenWritten(Path.Combine(vault, "Aulas", "aula — transcrição.md"));
            Assert.Contains("![[Aulas/aula.m4a]]", text);
            Assert.Contains("**[00:00]** parte 1", text);
            Assert.Contains("(trecho não reconhecido)", text);
            Assert.Contains("**[01:28]** parte 3", text);
            Assert.Equal(3, calls);
            Assert.Equal(path, view.Editor.Current?.Path);
            Assert.Contains("Organizar com o Assistente", view.NoticeActions);
            Assert.Equal(tempBefore, TempLeft());   // the decoded audio does not stay on the device
            return true;
        }
        finally { PlatformAudio.DecodeToWav = null; PlatformAudio.TranscribeWav = null; window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Transcription_StoppedLeavesNothingBehind() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, vault) = await OpenRealVaultAsync(("aula.m4a", "áudio"), ("Comece.md", "# Comece\n"));
        int tempBefore = TempLeft();
        var started = new TaskCompletionSource();
        PlatformAudio.DecodeToWav = (source, target, progress, token) => { File.WriteAllBytes(target, LongAudio.Wav(Lecture())); return Task.FromResult(true); };
        PlatformAudio.TranscribeWav = async (wav, token) => { started.TrySetResult(); await Task.Delay(Timeout.Infinite, token); return "nunca"; };
        try
        {
            var job = view.TranscribeAudioToNoteAsync("aula.m4a");
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(view.Transcribing);
            view.CancelTranscription();
            Assert.Null(await job.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.False(view.Transcribing);
            Assert.Empty(Directory.GetFiles(vault, "*transcrição*"));
            Assert.Equal(tempBefore, TempLeft());
            return true;
        }
        finally { PlatformAudio.DecodeToWav = null; PlatformAudio.TranscribeWav = null; window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
