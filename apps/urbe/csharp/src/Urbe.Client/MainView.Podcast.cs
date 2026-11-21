using Urbe.Core.Assistant;
using Urbe.Core;
using Urbe.Core.Study;

namespace Urbe.Client;

/// <summary>#956: the session's podcast. The agent writes the script in a note; the device's voices speak it into one WAV.</summary>
public sealed partial class MainView
{
    private async Task CreatePodcastScriptAsync(string sessionPath, StudySession session, IEnumerable<string> sources)
    {
        var path = sessionPath[..^StudySession.Suffix.Length] + Podcast.Suffix;
        if (!_contents.ContainsKey(path)) await SaveNoteAsync(new CityNote(path, Podcast.Draft(session, sources)));
        OpenNoteByPath(path);
        if (NoteMarks.AgentCalls(_contents[path]).FirstOrDefault() is { } call) await AskAgentInNoteAsync(call);
    }

    public bool CanSpeakPodcast(string? path) =>
        VaultWritable && path?.EndsWith(Podcast.Suffix, StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>Speaks the open podcast script into <c>Nome.podcast.wav</c> beside it and opens the audio.</summary>
    public async Task<string?> SpeakPodcastAsync(string path)
    {
        if (Editor.Current?.Path == path && Editor.HasChanges && !await Editor.SaveAsync()) return null;
        if (!CanSpeakPodcast(path) || !_contents.TryGetValue(path, out var script)) return null;
        var voice = Assistant.SpeechFor();
        if (voice is null)
        {
            await Dialogs.AlertAsync("Gerar áudio do podcast", "Selecione um modelo de voz no perfil do agente.");
            return null;
        }
        var turns = Podcast.Turns(script);
        if (turns.Count == 0)
        {
            await Dialogs.AlertAsync("Gerar áudio do podcast", "O roteiro ainda não tem falas. Cada fala é um parágrafo assim: **Ana:** texto.");
            return null;
        }
        var target = path[..^3] + ".wav";
        for (int n = 2; _attachments.Contains(target); n++) target = path[..^3] + " " + n + ".wav";
        var temp = Path.Combine(Path.GetTempPath(), "urbe-podcast-" + Guid.NewGuid().ToString("N"));
        using var cancel = new CancellationTokenSource();
        try
        {
            Directory.CreateDirectory(temp);
            var parts = new List<string>();
            for (int i = 0; i < turns.Count; i++)
            {
                _notice.Show("Gerando o podcast: fala " + (i + 1) + " de " + turns.Count, TimeSpan.FromHours(1), "Cancelar", cancel.Cancel);
                var part = Path.Combine(temp, i + ".wav");
                var speaker = turns[i].Voice == 0 ? voice : voice with
                {
                    Voice = Assistant.Config.Providers.Any(p => p.Id == voice.ProviderId && p.Preset == "openai")
                        ? AssistantProviders.SpeechVoices(voice.Model).First(v => v != voice.Voice) : voice.Voice
                };
                await File.WriteAllBytesAsync(part, await Assistant.SynthesizeSpeechAsync(speaker, turns[i].Text, cancel.Token), cancel.Token);
                parts.Add(part);
            }
            var joined = Path.Combine(temp, "podcast.wav");
            Podcast.JoinWav(parts, joined);
            await ((IAssistantApp)this).CreateBinaryFileAsync(target, await File.ReadAllBytesAsync(joined, cancel.Token));
            await SaveHistoryAsync();
            _notice.Show("Podcast pronto: " + Path.GetFileName(target), TimeSpan.FromSeconds(8), "Ouvir", () => _ = Editor.Pinned.OpenFileAsync(target), Ui.ToastKind.Success);
            return target;
        }
        catch (OperationCanceledException) { _notice.Show("Podcast parado; nada foi criado."); return null; }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or InvalidDataException or AiProviderException)
        {
            _notice.Hide();
            await Dialogs.AlertAsync("Gerar áudio do podcast", StorageProblems.Explain(ex) + " Nada foi criado.");
            return null;
        }
        finally { try { Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
