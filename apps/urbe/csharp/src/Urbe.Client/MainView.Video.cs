using System.Globalization;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-170/209: a Publication laid out as a video. The scenes are its notes (their text is the script and the
/// subtitles); the timeline, objects and sound references are in the .publication.json; the MP4 is encoded on the
/// device, checked, and only then offered to save.
/// </summary>
public sealed partial class MainView
{
    private static readonly string[] VideoImageExtensions = [".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp"];
    private static readonly string[] VideoAudioExtensions = [".wav", ".mp3", ".m4a", ".aac", ".ogg", ".opus", ".flac"];
    private CancellationTokenSource? _videoExport;

    /// <summary>Where the last verified MP4 was saved by the host hook (tests), or null.</summary>
    internal VideoExportResult? LastVideoExport { get; private set; }

    private void WirePublicationVideo(PublicationStudioView studio)
    {
        studio.VideoExportRequested += () => _ = ExportPublicationVideoAsync();
        studio.VideoCancelRequested += () => _videoExport?.Cancel();
        studio.SubtitlesRequested += () => _ = ExportPublicationSubtitlesAsync();
        studio.AddAudioRequested += () => _ = AddPublicationAudioAsync();
        studio.AddVideoElementRequested += id => _ = AddPublicationVideoElementAsync(id);
    }

    /// <summary>The images the video shows, read from the vault before drawing (a missing one is drawn as an empty frame).</summary>
    private async Task LoadPublicationVideoFilesAsync()
    {
        if (_publicationsStudio?.Project?.Video is not { } video || _vault is not { } vault) return;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in video.Scenes.SelectMany(s => s.Elements).Where(e => e.Kind == "image" && e.Source is not null).Select(e => e.Source!).Distinct())
            try { if ((await vault.ReadFileAsync(path, 64L << 20))?.Bytes is { } bytes) files[path] = bytes; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException) { }
        if (_publicationsStudio is { } studio && files.Count > 0 && !files.Keys.All(studio.VideoFiles.ContainsKey))
        {
            studio.VideoFiles = files;
            studio.Refresh();
        }
    }

    private bool SameVideoDraft(PublicationDraft? draft) =>
        draft is not null && ReferenceEquals(_publicationDraft, draft) && ReferenceEquals(_store, draft.Store) && _publicationsStudio?.IsVisible == true;

    private async Task AddPublicationVideoElementAsync(string sceneId)
    {
        var draft = _publicationDraft;
        if (_publicationsStudio?.Project is not { Video: { } video } project) return;
        var scene = video.Scenes.FirstOrDefault(s => s.Id == sceneId);
        if (scene is null) return;
        var kind = await Dialogs.ChooseAsync("Objeto da cena", [
            new DialogItem("Texto", Detail: "Uma frase na tela"), new DialogItem("Roteiro", Detail: "O texto da nota desta cena"),
            new DialogItem("Forma", Detail: "Retângulo ou elipse"), new DialogItem("Imagem do vault"),
            new DialogItem("Gráfico", Detail: "Uma função, como y = sin(x), traçada durante a cena"), new DialogItem("Fórmula", Detail: "TeX, como \\frac{a}{b}")]);
        if (kind is not { } k) return;
        // every object enters with a short fade; keyframes and motions stay editable in the project
        var fadeIn = new List<VideoKeyframe> { new() { Property = "opacity", Time = 0, Value = 0 }, new() { Property = "opacity", Time = 0.5, Value = 1, Easing = "easeOut" } };
        VideoElement? element = null;
        switch (k)
        {
            case 0:
                var text = await Dialogs.PromptAsync(new PromptOptions("Texto na tela", Label: "Texto", Confirm: "Adicionar"));
                if (!string.IsNullOrWhiteSpace(text)) element = new VideoElement { Kind = "text", Text = text.Trim(), Y = 0.62, Keyframes = fadeIn };
                break;
            case 1: element = new VideoElement { Kind = "script", Y = 0.6, Height = 0.45, FontSize = 0.045, Keyframes = fadeIn }; break;
            case 2:
                var shape = await Dialogs.ChooseAsync("Forma", [new DialogItem("Retângulo"), new DialogItem("Elipse")]);
                if (shape is { } sh) element = new VideoElement { Kind = "shape", Text = sh == 0 ? "rect" : "ellipse", Width = 0.25, Height = 0.25, Color = "#4f8cff", Keyframes = fadeIn };
                break;
            case 3:
                var images = _attachments.Where(p => VideoImageExtensions.Contains(System.IO.Path.GetExtension(p).ToLowerInvariant()))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
                if (images.Count == 0) { await Dialogs.AlertAsync("Imagem", "Não há imagens no vault."); return; }
                var image = await Dialogs.ChooseAsync("Imagem da cena", images.Select(p => new DialogItem(System.IO.Path.GetFileName(p), Detail: p)).ToList());
                if (image is { } i) element = new VideoElement { Kind = "image", Source = images[i], Width = 0.6, Height = 0.6, Keyframes = fadeIn };
                break;
            case 4:
                var plot = await Dialogs.PromptAsync(new PromptOptions("Gráfico", Label: "Função", Value: "y = sin(x)", Confirm: "Adicionar"));
                if (!string.IsNullOrWhiteSpace(plot))
                    element = new VideoElement
                    {
                        Kind = "plot", Text = plot.Trim(), Width = 0.7, Height = 0.5,
                        Keyframes = [new() { Property = "progress", Time = 0, Value = 0 }, new() { Property = "progress", Time = Math.Max(0.5, scene.Duration * 0.7), Value = 1, Easing = "linear" }]
                    };
                break;
            case 5:
                var tex = await Dialogs.PromptAsync(new PromptOptions("Fórmula", Label: "TeX", Confirm: "Adicionar"));
                if (!string.IsNullOrWhiteSpace(tex)) element = new VideoElement { Kind = "math", Text = tex.Trim(), Width = 0.5, Height = 0.25, Keyframes = fadeIn };
                break;
        }
        if (element is null || !SameVideoDraft(draft) || _publicationsStudio.Project?.Video?.Scenes.FirstOrDefault(s => s.Id == sceneId) is not { } current) return;
        var updated = _publicationsStudio.Project!;
        _publicationsStudio.UpdateProject(updated with { Video = updated.Video!.WithScene(current with { Elements = [.. current.Elements, element] }) });
        if (element.Kind == "image") await LoadPublicationVideoFilesAsync();
    }

    private async Task AddPublicationAudioAsync()
    {
        var draft = _publicationDraft;
        if (_publicationsStudio?.Project is not { Video: not null }) return;
        var sounds = _attachments.Where(p => VideoAudioExtensions.Contains(System.IO.Path.GetExtension(p).ToLowerInvariant()))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        if (sounds.Count == 0) { await Dialogs.AlertAsync("Áudio", "Não há arquivos de áudio no vault (wav, mp3, m4a, ogg, flac)."); return; }
        var chosen = await Dialogs.ChooseAsync("Áudio do vídeo", sounds.Select(p => new DialogItem(System.IO.Path.GetFileName(p), Detail: p)).ToList());
        if (chosen is not { } c) return;
        var kind = await Dialogs.ChooseAsync("Tipo de áudio", [new DialogItem("Voz (narração)"), new DialogItem("Música"), new DialogItem("Efeito")]);
        if (kind is not { } k) return;
        var start = await Dialogs.PromptAsync(new PromptOptions("Começa em", Label: "Segundos desde o início do vídeo", Value: "0", Confirm: "Adicionar",
            Validate: v => double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s) && s >= 0 ? null : "Escreva um número de segundos."));
        if (start is null || !SameVideoDraft(draft)) return;
        var project = VideoComposition.Synced(_publicationsStudio.Project!);
        var clip = new VideoAudioClip
        {
            Kind = VideoAudioClip.Kinds[k], Path = sounds[c], Volume = k == 1 ? 0.6 : 1,
            Start = double.Parse(start.Replace(',', '.'), CultureInfo.InvariantCulture)
        };
        _publicationsStudio.UpdateProject(project with { Video = project.Video! with { Audio = [.. project.Video.Audio, clip] } });
        ShowLifeNotice("Áudio adicionado à trilha do vídeo: " + System.IO.Path.GetFileName(clip.Path));
    }

    private async Task<(PublicationProject Project, VideoTimeline Timeline)?> PrepareVideoAsync(PublicationDraft draft)
    {
        if (_publicationsCatalog is not { } catalog || !await SavePublicationAsync()) return null;
        if (!ReferenceEquals(_publicationDraft, draft) || !ReferenceEquals(_store, draft.Store) || draft.Dirty) return null;
        VideoTimeline timeline;
        try { timeline = VideoTimeline.Build(draft.Project, catalog); }
        catch (Exception e) when (e is InvalidDataException or NotSupportedException)
        { await Dialogs.AlertAsync("Vídeo indisponível", e.Message); return null; }
        if (!timeline.IsComplete)
        { await Dialogs.AlertAsync("Fontes indisponíveis", "Resolva as notas ausentes antes de exportar: " + timeline.MissingDocumentIds.Count + " referência(s)."); return null; }
        if (timeline.FrameCount == 0)
        { await Dialogs.AlertAsync("Vídeo vazio", "Adicione notas à publicação: cada nota vira uma cena."); return null; }
        return (draft.Project, timeline);
    }

    public async Task ExportPublicationSubtitlesAsync()
    {
        if (_publicationDraft is not { } draft || await PrepareVideoAsync(draft) is not { } prepared) return;
        var bytes = System.Text.Encoding.UTF8.GetBytes(prepared.Timeline.Srt());
        var name = ArtifactModel.SafeName(prepared.Project.Title) + ".srt";
        try
        {
            var saved = SavePageFile is { } save ? await save(name, bytes) : await SavePublicationFileAsync(name, bytes, "srt", "application/x-subrip");
            if (saved == true) ShowLifeNotice("Legendas SRT salvas.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        { await Dialogs.AlertAsync("Não consegui salvar as legendas", StorageProblems.Explain(e)); }
    }

    /// <summary>Renders, encodes and verifies the MP4; offers it to save only when the file checked out.</summary>
    public async Task ExportPublicationVideoAsync()
    {
        if (_videoExport is not null || _publicationDraft is not { } draft || _publicationsStudio is not { } studio) return;
        if (await PrepareVideoAsync(draft) is not { } prepared) return;
        var (project, timeline) = prepared;
        if (PlatformVideo.Encoder() is not { } encoder)
        {
            await Dialogs.AlertAsync("Exportar MP4", PlatformVideo.Unavailable);
            return;
        }
        var vault = _vault;
        var temp = Directory.CreateTempSubdirectory("urbe-video-").FullName;
        using var cancel = new CancellationTokenSource();
        _videoExport = cancel;
        var panel = studio.VideoPanel;
        panel.Stop();
        panel.SetProgress(new VideoExportProgress(0, timeline.FrameCount, "Preparando"));
        var skipped = new List<string>();
        try
        {
            // the images and the sound, read from the vault; the soundtrack is mixed once into a WAV
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var path in VideoFrameRenderer.FilesUsed(timeline))
                if (vault is not null && (await vault.ReadFileAsync(path, 64L << 20, cancel.Token))?.Bytes is { } bytes) files[path] = bytes;
            string? soundtrack = null;
            if (timeline.Audio.Count > 0)
            {
                var decoded = new List<(VideoAudioClip Clip, Stream Wav)>();
                try
                {
                    int n = 0;
                    foreach (var clip in timeline.Audio)
                    {
                        var read = vault is null ? null : await vault.ReadFileAsync(clip.Path, 1L << 30, cancel.Token);
                        if (read?.Bytes is not { } bytes) { skipped.Add(clip.Path); continue; }
                        var source = System.IO.Path.Combine(temp, "a" + n + System.IO.Path.GetExtension(clip.Path));
                        var wav = System.IO.Path.Combine(temp, "a" + n++ + ".wav");
                        await File.WriteAllBytesAsync(source, bytes, cancel.Token);
                        if (await PlatformVideo.ToWavAsync(source, wav, cancel.Token)) decoded.Add((clip, File.OpenRead(wav)));
                        else skipped.Add(clip.Path);
                    }
                    if (decoded.Count > 0)
                    {
                        soundtrack = System.IO.Path.Combine(temp, "trilha.wav");
                        await Task.Run(() => { using var output = File.Create(soundtrack); VideoAudio.Mix(decoded, timeline.Duration, output, cancel.Token); }, cancel.Token);
                    }
                }
                finally { foreach (var (_, wav) in decoded) wav.Dispose(); }
            }
            using var renderer = new VideoFrameRenderer(timeline, files);
            var target = System.IO.Path.Combine(temp, ArtifactModel.SafeName(project.Title) + ".mp4");
            var progress = new Progress<VideoExportProgress>(p => panel.SetProgress(p));
            var result = await VideoExport.RunAsync(renderer, encoder, target, soundtrack, progress, cancel.Token);
            panel.SetProgress(null, "MP4 verificado: H.264 " + timeline.Width + "×" + timeline.Height + ", " + result.Info.Video!.SampleCount + " quadros, " +
                PublicationVideoPanel.Clock(result.Info.Seconds) + (result.Info.Audio is null ? ", sem som" : ", com som") + ".");
            bool? saved;
            if (SavePageFile is { } save) saved = await save(System.IO.Path.GetFileName(target), await File.ReadAllBytesAsync(target, CancellationToken.None));
            else saved = await SaveVideoFileAsync(target);
            if (saved == true)
            {
                LastVideoExport = result;
                ShowLifeNotice("Vídeo MP4 salvo." + (skipped.Count > 0 ? " Sem o áudio de: " + string.Join(", ", skipped.Select(System.IO.Path.GetFileName)) + " (formato que este aparelho não lê)." : ""),
                    skipped.Count > 0 ? Ui.ToastKind.Warning : Ui.ToastKind.Success);
            }
        }
        catch (OperationCanceledException)
        {
            panel.SetProgress(null, "Exportação cancelada. Nenhum arquivo parcial ficou.");
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            panel.SetProgress(null, "O vídeo não foi gerado: " + e.Message);
            await Dialogs.AlertAsync("Não foi possível exportar o vídeo", e.Message);
        }
        finally
        {
            _videoExport = null;
            try { Directory.Delete(temp, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private async Task<bool?> SaveVideoFileAsync(string path)
    {
        if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
            throw new NotSupportedException("Este aparelho não permite salvar arquivos.");
        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Exportar vídeo", SuggestedFileName = System.IO.Path.GetFileName(path), DefaultExtension = "mp4",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("MP4") { Patterns = ["*.mp4"], MimeTypes = ["video/mp4"] }]
        });
        if (file is null) return null;
        await using (var output = await file.OpenWriteAsync())
        await using (var input = File.OpenRead(path))
        {
            if (output.CanSeek) output.SetLength(0);
            await input.CopyToAsync(output);
            await output.FlushAsync();
        }
        return true;
    }
}
