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
        CancellationTokenSource? reading = null;
        var body = OpenView("Gerar " + kind, () =>
        {
            reading?.Cancel();
            CloseView();
        });
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
        var referenceLinks = new TextBox { PlaceholderText = "Links diretos dos vídeos de referência (opcional)" };
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
        if (kind == "Vídeo") body.Children.Add(referenceLinks);
        ShowSources();
        var status = Hint("");
        body.Children.Add(status);
        var submit = new Ui.UrbeButton("Enviar configurações e gerar", Ui.ButtonKind.Primary);
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
            var requestedFocus = focus.Text.Trim();
            var requestedOptions = options.Text?.Trim();
            var requestedLanguage = language.Text?.Trim();
            var requestedLinks = referenceLinks.Text?.Trim();
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
            reading = cancel;
            var stop = TextButton("Cancelar leitura", cancel.Cancel);
            body.Children.Add(stop);
            try
            {
                var prepared = new List<AiChatBlock>();
                var textBudget = AssistantTextAttachments.MaxTotalTextBytes - included.Sum(source =>
                    System.Text.Encoding.UTF8.GetByteCount(source.Text ?? ""));
                foreach (var source in included)
                {
                    var ready = source;
                    if (source.LocalSourceId is { } id && LocalSourcePath(id) is { } path &&
                        VaultFileKinds.Classify(source.FileName) is VaultFileKinds.Audio or VaultFileKinds.Video &&
                        PlatformAudio.Available && textBudget > 0 && File.Exists(path))
                    {
                        status.Text = "Lendo o áudio de " + source.FileName + "…";
                        try
                        {
                            var transcript = await ReadSourceTranscriptAsync(path, cancel.Token);
                            var transcriptBytes = System.Text.Encoding.UTF8.GetByteCount(transcript);
                            if (transcriptBytes > 0 && transcriptBytes <= textBudget)
                            {
                                ready = new AiChatBlock { Hidden = true, FileName = source.FileName, LocalSourceId = id,
                                    Text = source.Text + "\nTranscrição local por instante:\n" + transcript };
                                textBudget -= transcriptBytes;
                            }
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
                    "Assunto, objetivo e público: " + requestedFocus + "\nPreferências: " + requestedOptions +
                    "\nIdioma: " + requestedLanguage + (string.IsNullOrEmpty(requestedLinks) ? "" : "\nReferências de vídeo: " + requestedLinks));
                _pendingAttachments.AddRange(excluded);
                RefreshAttachments();
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested) { status.Text = "Leitura cancelada."; }
            finally { reading = null; body.Children.Remove(stop); submit.IsEnabled = true; }
        };
        body.Children.Add(submit);
    }

    private async Task AddVideoReferenceFramesAsync(AiChatMessage request, AiModelChoice choice,
        AssistantWebSession? web, CancellationToken cancel)
    {
        const string visualGuide = "[Referências visuais de vídeo]";
        if (request.Content.Any(block => block.Hidden && block.Text?.StartsWith(visualGuide, StringComparison.Ordinal) == true)) return;
        var text = request.Content.FirstOrDefault(block => block.Type == "text" && !block.Hidden)?.Text ?? "";
        var local = request.Content.Where(block => block.LocalSourceId is not null &&
            VaultFileKinds.Classify(block.FileName) == VaultFileKinds.Video).ToList();
        var links = text.Contains("vídeo", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("video", StringComparison.OrdinalIgnoreCase) || text.Contains("motion", StringComparison.OrdinalIgnoreCase)
            ? System.Text.RegularExpressions.Regex.Matches(text, @"https?://[^\s<>""\[\]]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                .Select(match => match.Value.TrimEnd('.', ',', ')')).Distinct(StringComparer.Ordinal).ToList()
            : [];
        if (local.Count + links.Count == 0) return;
        if (AiModelCatalog.Find(_cfg, choice)?.Reads(AiModalities.Image) != true)
        {
            _app.Notify("Os quadros de referência exigem um modelo com entrada de imagem reconhecida.");
            return;
        }

        var prepared = new List<AiChatBlock>();
        var notes = new List<string>();
        long bytesUsed = request.Content.Where(block => block.Type == "attachment").Sum(block => (long)block.Content.Length);
        async Task AddSheet(string path, string label)
        {
            var sheet = VideoReferenceFrames.ContactSheet(await VideoReferenceFrames.ReadAsync(path, cancel));
            if (sheet is null) { notes.Add(label + ": quadros indisponíveis."); return; }
            var encoded = Convert.ToBase64String(sheet);
            if (bytesUsed + encoded.Length > MaxAttachmentBytes)
            { notes.Add(label + ": quadros excedem o tamanho do pedido ao modelo."); return; }
            bytesUsed += encoded.Length;
            prepared.Add(new AiChatBlock { Type = "attachment", FileName = label + " — quadros.jpg", MimeType = "image/jpeg", Content = encoded });
        }

        foreach (var source in local.Take(4))
        {
            if (LocalSourcePath(source.LocalSourceId!) is not { } path || !File.Exists(path))
            { notes.Add(source.FileName + ": arquivo indisponível."); continue; }
            try { await AddSheet(path, source.FileName); }
            catch (Exception e) when (e is IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            { notes.Add(source.FileName + ": quadros indisponíveis."); }
        }
        foreach (var url in links.Take(Math.Max(0, 4 - local.Count)))
        {
            if (web is null) { notes.Add("A pesquisa na internet está desativada para as referências por link."); break; }
            var directory = Directory.CreateTempSubdirectory("numa-link-video-");
            var path = Path.Combine(directory.FullName, "referencia");
            try
            {
                await using (var output = File.Create(path))
                    await web.DownloadReferenceVideoAsync(url, output);
                await AddSheet(path, "Vídeo de referência " + (links.IndexOf(url) + 1));
            }
            catch (OperationCanceledException) when (!cancel.IsCancellationRequested)
            { notes.Add("O download de uma referência excedeu o tempo de leitura."); }
            catch (Exception e) when (e is IOException or HttpRequestException or AiToolException or InvalidOperationException or ArgumentException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            { notes.Add("Um link não forneceu vídeo legível. Para páginas de vídeo, anexe o arquivo."); }
            finally
            {
                try { directory.Delete(true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
        if (local.Count + links.Count > 4) notes.Add("Foram selecionadas até quatro referências visuais para este pedido.");
        if (notes.Count > 0) _app.Notify(string.Join(" ", notes));
        request.Content.Add(new AiChatBlock { Hidden = true, Text = visualGuide +
            " Os anexos de quadros são amostras com instantes. Ao criar motion graphics, derive deles " +
            "paleta, tipografia, hierarquia, composição e ritmo observável. Use os mesmos parâmetros e funções " +
            "reutilizáveis nas cenas programadas. Declare o que a amostragem não permite inferir. " + string.Join(" ", notes) });
        request.Content.AddRange(prepared);
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
