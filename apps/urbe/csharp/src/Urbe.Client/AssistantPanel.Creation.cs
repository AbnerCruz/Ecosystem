using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client;

public sealed partial class AssistantPanel
{
    private static string? LocalSourcePath(string id) =>
        UrbeApp.AssistantFolder is { } folder && Guid.TryParseExact(id, "N", out _)
            ? Path.Combine(folder, "sources", id)
            : null;

    private async Task AttachLocalSourceAsync(IStorageFile file, AiConversation conversation)
    {
        var id = Guid.NewGuid().ToString("N");
        var path = LocalSourcePath(id);
        if (path is null) { _app.Notify("O armazenamento privado de fontes está indisponível."); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bool attached = false;
        try
        {
            await using var input = await file.OpenReadAsync();
            long length;
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            {
                await input.CopyToAsync(output);
                length = output.Length;
            }
            if (!ReferenceEquals(conversation, _conv) || AttachmentsFull()) return;
            _pendingAttachments.Add(new AiChatBlock
            {
                Hidden = true, FileName = file.Name, LocalSourceId = id,
                Text = "[Fonte anexada localmente: " + System.Text.Json.JsonSerializer.Serialize(file.Name) +
                    "; " + VaultFileKinds.FormatBytes(length) + ". O conteúdo binário ficou neste dispositivo. Use apenas o conteúdo legível fornecido.]"
            });
            attached = true;
        }
        finally
        {
            if (!attached)
                try { File.Delete(path); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    private void ShowCreationBrief(string kind)
    {
        if (ConversationBusy(_conv.Id)) { _app.Notify("Aguarde a resposta atual antes de iniciar outra criação."); return; }
        var conversation = _conv;
        var body = OpenView("Gerar " + kind);
        var focus = new TextBox { PlaceholderText = "Assunto, objetivo e público", Text = _input.Text };
        var options = new TextBox
        {
            PlaceholderText = kind switch
            {
                "Podcast" => "Duração, estilo e apresentadores",
                "Vídeo" => "Duração, proporção e estilo visual",
                _ => "Quantidade, dificuldade e tópicos"
            }
        };
        var language = new TextBox { PlaceholderText = "Idioma", Text = "Português do Brasil" };
        var sourceList = new StackPanel { Spacing = 6 };
        var selected = new Dictionary<AiChatBlock, CheckBox>();
        void ShowSources()
        {
            var excluded = selected.Where(pair => pair.Value.IsChecked != true).Select(pair => pair.Key).ToHashSet();
            sourceList.Children.Clear();
            selected.Clear();
            foreach (var source in _pendingAttachments)
            {
                var name = source.FileName.Length > 0 ? source.FileName : (source.Text ?? "").Split('\n')[0];
                var check = new CheckBox { Content = name, IsChecked = !excluded.Contains(source) };
                selected[source] = check;
                sourceList.Children.Add(check);
            }
            if (selected.Count == 0) sourceList.Children.Add(Hint("Nenhum arquivo selecionado."));
        }

        body.Children.Add(Section("O que você quer criar?", focus, options, language));
        body.Children.Add(Section("Fontes", sourceList, TextButton("Anexar arquivos", async () =>
        {
            await AttachDeviceFileAsync();
            if (ReferenceEquals(conversation, _conv)) ShowSources();
        })));
        ShowSources();
        var status = Hint("");
        body.Children.Add(status);
        var submit = TextButton("Enviar configurações e gerar", () => { }, primary: true);
        submit.Click += async (_, _) =>
        {
            if (!ReferenceEquals(conversation, _conv) || ConversationBusy(conversation.Id)) return;
            if (string.IsNullOrWhiteSpace(focus.Text))
            {
                status.Text = "Informe o assunto e o objetivo.";
                focus.Focus();
                return;
            }
            if (_cfg.Providers.Count == 0 || ModelId().Length == 0)
            {
                status.Text = "Configure um modelo de conversa para este agente antes de gerar.";
                return;
            }
            var included = selected.Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key).ToList();
            var excluded = _pendingAttachments.Where(source => !included.Contains(source)).ToList();
            var instructions = kind switch
            {
                "Podcast" => "Gere um podcast: salve o roteiro em uma nota .podcast.md, no formato **Nome:** fala, e crie uma publicação editável com a nota como fonte. Entregue o rascunho no Estúdio.",
                "Vídeo" => "Crie um vídeo de motion graphics por código com create_video e entregue a publicação editável.",
                _ => "Crie flashcards com create_flashcards na biblioteca nativa e informe o conjunto criado."
            };
            submit.IsEnabled = false;
            using var cancel = new CancellationTokenSource();
            var stop = TextButton("Cancelar leitura", cancel.Cancel);
            body.Children.Add(stop);
            try
            {
                var prepared = new List<AiChatBlock>();
                foreach (var source in included)
                {
                    var ready = source;
                    if (source.LocalSourceId is { } id && LocalSourcePath(id) is { } path &&
                        VaultFileKinds.Classify(source.FileName) is VaultFileKinds.Audio or VaultFileKinds.Video &&
                        PlatformAudio.Available && File.Exists(path))
                    {
                        status.Text = "Lendo o áudio de " + source.FileName + "…";
                        try
                        {
                            var transcript = await ReadSourceTranscriptAsync(path, cancel.Token);
                            if (transcript.Length > 0)
                                ready = new AiChatBlock { Hidden = true, FileName = source.FileName, LocalSourceId = id,
                                    Text = source.Text + "\nTranscrição local por instante:\n" + transcript };
                        }
                        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException)
                        {
                            _app.Notify("Não foi possível transcrever " + source.FileName + ".");
                        }
                    }
                    prepared.Add(ready);
                }
                cancel.Token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(conversation, _conv) || ViewTitle != "Gerar " + kind) return;
                _pendingAttachments.Clear();
                _pendingAttachments.AddRange(prepared);
                Send(instructions + "\nUse as fontes marcadas. Informe lacunas se uma fonte não estiver legível.\n" +
                    "Assunto, objetivo e público: " + focus.Text.Trim() + "\nPreferências: " + options.Text?.Trim() +
                    "\nIdioma: " + language.Text?.Trim());
                _pendingAttachments.AddRange(excluded);
                RefreshAttachments();
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { status.Text = "Leitura cancelada."; }
            finally { body.Children.Remove(stop); submit.IsEnabled = true; }
        };
        body.Children.Add(submit);
    }

    private static async Task<string> ReadSourceTranscriptAsync(string source, CancellationToken cancel)
    {
        var directory = Directory.CreateTempSubdirectory("numa-transcricao-");
        try
        {
            var wav = Path.Combine(directory.FullName, "fonte.wav");
            if (!await PlatformAudio.DecodeToWav!(source, wav, null, cancel)) return "";
            await using var stream = File.OpenRead(wav);
            var info = VideoAudio.ReadWavInfo(stream);
            if (info is null || info.Channels != 1 || info.SampleRate != 16000) return "";
            var piece = Path.Combine(directory.FullName, "trecho.wav");
            var transcript = new System.Text.StringBuilder();
            const int framesPerPart = 16000 * 25;
            stream.Position = info.DataOffset;
            for (long at = 0; at < info.Frames; at += framesPerPart)
            {
                int count = (int)Math.Min(framesPerPart, info.Frames - at);
                var bytes = new byte[count * sizeof(short)];
                await stream.ReadExactlyAsync(bytes, cancel);
                var samples = new short[count];
                Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
                await File.WriteAllBytesAsync(piece, LongAudio.Wav(samples), cancel);
                if (await PlatformAudio.TranscribeWav!(piece, cancel) is { Length: > 0 } text)
                    transcript.Append('[').Append(TimeSpan.FromSeconds(at / 16000d)).Append("] ").AppendLine(text);
                if (transcript.Length >= AssistantTextAttachments.MaxTextBytes / 4)
                {
                    transcript.AppendLine("[Transcrição parcial. Restante não enviado.]");
                    break;
                }
            }
            return transcript.ToString();
        }
        finally
        {
            try { directory.Delete(true); } catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }
}
