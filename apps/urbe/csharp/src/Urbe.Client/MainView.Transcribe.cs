using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-058: "Transcrever em nota" on an audio file of the vault. The file is copied out of the vault (it is only read),
/// decoded by the device to 16 kHz mono, cut in pauses (LongAudio.Plan) and transcribed part by part; the note goes next
/// to the recording, with a link to it and each part's time, and the Assistente can organize it. The progress shows
/// with Cancelar; a stop leaves nothing, and the decoded audio never stays on the device.
/// </summary>
public sealed partial class MainView
{
    private CancellationTokenSource? _transcription;

    /// <summary>A transcription is running.</summary>
    public bool Transcribing => _transcription is not null;

    public bool CanTranscribe(string path) =>
        PlatformAudio.Available && VaultWritable && VaultFileKinds.Classify(path) == VaultFileKinds.Audio;

    public void CancelTranscription() => _transcription?.Cancel();

    /// <summary>The actions of the notice shown now (tests).</summary>
    public string NoticeActions => _notice.ActionText ?? "";

    /// <summary>The note's path when it was written; null when stopped or impossible (the person is told why).</summary>
    public async Task<string?> TranscribeAudioToNoteAsync(string audioPath)
    {
        if (!CanTranscribe(audioPath) || _vault is null || _transcription is not null) return null;
        var name = System.IO.Path.GetFileNameWithoutExtension(audioPath);
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "urbe-audio-" + Guid.NewGuid().ToString("N"));
        using var cancel = new CancellationTokenSource();
        _transcription = cancel;
        void Progress(string text) => _notice.Show("Transcrevendo “" + name + "”: " + text, TimeSpan.FromHours(4), "Cancelar", CancelTranscription);
        try
        {
            Directory.CreateDirectory(temp);
            Progress("lendo o áudio…");
            var read = await _vault.ReadFileAsync(audioPath, 1L << 30);
            if (read?.Bytes is not { } bytes)
            {
                await Dialogs.AlertAsync("Transcrever em nota", read is null ? "O arquivo não foi encontrado." : "O áudio é grande demais para transcrever por aqui.");
                return null;
            }
            var source = System.IO.Path.Combine(temp, "fonte" + System.IO.Path.GetExtension(audioPath));
            await File.WriteAllBytesAsync(source, bytes, cancel.Token);
            var wav = System.IO.Path.Combine(temp, "audio.wav");
            var decoding = new Progress<double>(p => Progress("preparando " + Math.Round(p * 100) + "%"));
            if (!await PlatformAudio.DecodeToWav!(source, wav, decoding, cancel.Token))
            {
                await Dialogs.AlertAsync("Transcrever em nota", "Não foi possível ler este arquivo como áudio. Nada foi criado.");
                return null;
            }
            var parts = new List<(TimeSpan At, string? Text)>();
            await using (var audio = File.OpenRead(wav))
            {
                long total = Math.Max(0, (audio.Length - 44) / 2);
                short[] Read(long start, int count)
                {
                    var buffer = new byte[Math.Max(0, (int)Math.Min(count, total - start)) * 2];
                    audio.Seek(44 + start * 2, SeekOrigin.Begin);
                    audio.ReadExactly(buffer);
                    var samples = new short[buffer.Length / 2];
                    Buffer.BlockCopy(buffer, 0, samples, 0, buffer.Length);
                    return samples;
                }
                var plan = LongAudio.Plan(total, Read);
                var piece = System.IO.Path.Combine(temp, "trecho.wav");
                for (int i = 0; i < plan.Count; i++)
                {
                    cancel.Token.ThrowIfCancellationRequested();
                    Progress("trecho " + (i + 1) + " de " + plan.Count);
                    await File.WriteAllBytesAsync(piece, LongAudio.Wav(Read(plan[i].Start, plan[i].Length)), cancel.Token);
                    parts.Add((plan[i].At, await PlatformAudio.TranscribeWav!(piece, cancel.Token)));
                }
            }
            cancel.Token.ThrowIfCancellationRequested();
            var notePath = LongAudio.NotePath(audioPath, p => _contents.ContainsKey(p) || _attachments.Contains(p));
            await WriteVaultFileAsync(notePath, LongAudio.Note(audioPath, parts), exists: false);
            await SaveHistoryAsync();
            OpenNoteByPath(notePath);
            _notice.Show("Transcrição pronta para revisar: " + parts.Count + (parts.Count == 1 ? " trecho." : " trechos."), TimeSpan.FromSeconds(10),
                "Organizar com o Assistente", () => OrganizeTranscription(notePath), Ui.ToastKind.Success);
            return notePath;
        }
        catch (OperationCanceledException)
        {
            _notice.Show("Transcrição parada; nada foi criado.");
            return null;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException)
        {
            _notice.Hide();
            await Dialogs.AlertAsync("Transcrever em nota", StorageProblems.Explain(ex) + " Nada foi criado.");
            return null;
        }
        finally
        {
            _transcription = null;
            try { Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The Assistente opens with the request written (the person picks the agent and sends it).</summary>
    private void OrganizeTranscription(string notePath)
    {
        if (!AssistantOpen) ToggleAssistant();
        Assistant.Input.Text = "Organize a transcrição em [[" + System.IO.Path.GetFileNameWithoutExtension(notePath) + "]] numa nota nova, ao lado, pronta para revisão: " +
            "um resumo curto, os assuntos em títulos, os pontos principais, os termos e as dúvidas, com os horários de onde vieram. Mantenha a transcrição original como está.";
    }
}
