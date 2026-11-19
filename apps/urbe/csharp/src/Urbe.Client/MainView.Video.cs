using System.Globalization;
using Urbe.Core;
using Urbe.Core.Assistant;

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
        studio.SubtitlesRequested += format => _ = ExportPublicationSubtitlesAsync(format);
        studio.AddAudioRequested += () => _ = AddPublicationAudioAsync();
        studio.AdjustAudioRequested += () => _ = AdjustPublicationAudioAsync();
        studio.NarrateRequested += () => _ = NarratePublicationVideoAsync();
        studio.CanNarrate = PlatformAudio.SynthesizeToWav is not null;
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

    /// <summary>#956: strokes that draw themselves, the annotations of explainer videos (SVG path data, fitted to the box).</summary>
    internal static readonly (string Label, string Data)[] VideoStrokes =
    [
        ("Seta", "M 0 50 L 90 50 M 66 26 L 90 50 L 66 74"),
        ("Sublinhado", "M 0 12 C 30 2, 70 22, 100 8"),
        ("Visto", "M 0 52 L 34 86 L 100 0"),
        ("Círculo", "M 50 2 C 80 2, 100 22, 100 50 C 100 80, 76 100, 48 98 C 20 96, 0 76, 2 48 C 4 22, 26 4, 56 6"),
    ];

    private async Task AddPublicationVideoElementAsync(string sceneId)
    {
        var draft = _publicationDraft;
        if (_publicationsStudio?.Project is not { Video: { } video } project) return;
        var scene = video.Scenes.FirstOrDefault(s => s.Id == sceneId);
        if (scene is null) return;
        var kind = await Dialogs.ChooseAsync("Objeto da cena", [
            new DialogItem("Texto", Detail: "Uma frase na tela"), new DialogItem("Roteiro", Detail: "O texto da nota desta cena"),
            new DialogItem("Forma", Detail: "Retângulo ou elipse"), new DialogItem("Imagem do vault"),
            new DialogItem("Gráfico", Detail: "Uma função, como y = sin(x), traçada durante a cena"), new DialogItem("Fórmula", Detail: "TeX, como \\frac{a}{b}"),
            new DialogItem("Traço", Detail: "Seta, sublinhado, visto ou círculo que se desenha na tela")]);
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
            case 6:
                var stroke = await Dialogs.ChooseAsync("Traço", VideoStrokes.Select(t => new DialogItem(t.Label)).ToList());
                if (stroke is { } st)
                    element = new VideoElement
                    {
                        Kind = "path", Text = VideoStrokes[st].Data, Width = 0.3, Height = 0.2, Color = "#ffd166", FontSize = 0.06,
                        Keyframes = [new() { Property = "progress", Time = 0, Value = 0 }, new() { Property = "progress", Time = 0.8, Value = 1, Easing = "easeOut" }]
                    };
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
            // music comes in and leaves softly (1 s in, 2 s out); a voice or an effect starts and stops as recorded
            FadeIn = k == 1 ? 1 : 0, FadeOut = k == 1 ? 2 : 0,
            Start = double.Parse(start.Replace(',', '.'), CultureInfo.InvariantCulture)
        };
        _publicationsStudio.UpdateProject(project with { Video = project.Video! with { Audio = [.. project.Video.Audio, clip] } });
        ShowLifeNotice("Áudio adicionado à trilha do vídeo: " + System.IO.Path.GetFileName(clip.Path));
    }

    /// <summary>
    /// CST-172: the narration made by the device's voice (offline, no provider, nothing paid): each scene's script, as
    /// written in its note, becomes a WAV in Narração/&lt;publicação&gt;/ placed at the scene's start as a voice track. Narrating
    /// again replaces those tracks (the old files stay in the vault).
    /// </summary>
    /// <summary>Plays or pauses the open video publication's preview (the panel's button; emulator tests use it too).</summary>
    public void PlayVideoPreview() => _publicationsStudio?.VideoPanel.TogglePlay();

    public async Task NarratePublicationVideoAsync()
    {
        var draft = _publicationDraft;
        if (PlatformAudio.SynthesizeToWav is not { } speak || draft is null || await PrepareVideoAsync(draft) is not { } prepared) return;
        var (project, timeline) = prepared;
        var scenes = timeline.Scenes.Where(s => s.Script.Trim().Length > 0).ToList();
        if (scenes.Count == 0) { await Dialogs.AlertAsync("Narrar o roteiro", "As notas das cenas ainda não têm texto para narrar."); return; }
        var folder = "Narração/" + ArtifactModel.SafeName(project.Title) + "/";
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "urbe-narration-" + Guid.NewGuid().ToString("N"));
        var spoken = new List<(VideoTimelineScene Scene, string Wav, double Seconds)>();
        using var cancel = new CancellationTokenSource();
        try
        {
            Directory.CreateDirectory(temp);
            for (int i = 0; i < scenes.Count; i++)
            {
                _notice.Show("Narrando a cena " + (i + 1) + " de " + scenes.Count, TimeSpan.FromHours(1), "Cancelar", cancel.Cancel);
                var wav = System.IO.Path.Combine(temp, i + ".wav");
                if (!await speak(scenes[i].Script.Trim(), 0, wav, cancel.Token))
                {
                    _notice.Hide();
                    await Dialogs.AlertAsync("Narrar o roteiro", "A voz do aparelho não conseguiu falar a cena " + (i + 1) + ". Nada foi mudado no vídeo.");
                    return;
                }
                double seconds;
                await using (var read = File.OpenRead(wav)) seconds = VideoAudio.ReadWavInfo(read)?.Seconds ?? 0;
                spoken.Add((scenes[i], wav, seconds));
            }
            if (!SameVideoDraft(draft) || _publicationsStudio?.Project is not { Video: { } video } current) return;
            // #956: a scene shorter than its voice grows to hold it; the voice is placed where the scene now starts
            var fitted = video.FitToVoices(spoken.Where(x => x.Seconds > 0).ToDictionary(x => x.Scene.Scene.Id, x => x.Seconds));
            var grown = fitted.Scenes.Zip(video.Scenes).Count(p => p.First.Duration != p.Second.Duration);
            var starts = VideoTimeline.Build(current with { Video = fitted }, _ => null).Scenes.ToDictionary(x => x.Scene.Id, x => x.Start);
            var clips = new List<VideoAudioClip>();
            for (int i = 0; i < spoken.Count; i++)
            {
                var path = folder + "Cena " + (i + 1) + ".wav";
                for (int n = 2; _attachments.Contains(path); n++) path = folder + "Cena " + (i + 1) + " (" + n + ").wav";
                await ((IAssistantApp)this).CreateBinaryFileAsync(path, await File.ReadAllBytesAsync(spoken[i].Wav, cancel.Token));
                clips.Add(new VideoAudioClip { Kind = "voice", Path = path, Start = starts[spoken[i].Scene.Scene.Id], Length = spoken[i].Seconds > 0 ? spoken[i].Seconds : null });
            }
            if (!SameVideoDraft(draft)) return;
            var kept = fitted.Audio.Where(a => !(a.Kind == "voice" && a.Path.StartsWith(folder, StringComparison.Ordinal))).ToList();
            _publicationsStudio.UpdateProject(current with { Video = fitted with { Audio = [.. kept, .. clips] } });
            _notice.Show("Narração pronta: " + clips.Count + (clips.Count == 1 ? " cena" : " cenas") +
                (grown == 0 ? "." : grown == 1 ? "; uma cena ficou mais longa para caber a fala." : "; " + grown + " cenas ficaram mais longas para caber a fala."),
                TimeSpan.FromSeconds(6), kind: Ui.ToastKind.Success);
        }
        catch (OperationCanceledException) { _notice.Show("Narração parada; o vídeo não mudou."); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            _notice.Hide();
            await Dialogs.AlertAsync("Narrar o roteiro", StorageProblems.Explain(ex));
        }
        finally { try { Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    /// <summary>The track's shape (VideoAudio.Waveform), or "" when the file cannot be read or decoded here.</summary>
    private async Task<string> AudioShapeAsync(string path)
    {
        if (_vault is not { } vault) return "";
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "urbe-shape-" + Guid.NewGuid().ToString("N"));
        try
        {
            if ((await vault.ReadFileAsync(path, FilePreviewPanel.MaxBytes))?.Bytes is not { } bytes) return "";
            Directory.CreateDirectory(temp);
            var source = System.IO.Path.Combine(temp, "fonte" + System.IO.Path.GetExtension(path));
            var wav = System.IO.Path.Combine(temp, "som.wav");
            await File.WriteAllBytesAsync(source, bytes);
            if (!await PlatformVideo.ToWavAsync(source, wav, CancellationToken.None)) return "";
            await using var stream = File.OpenRead(wav);
            return VideoAudio.Waveform(stream);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException) { return ""; }
        finally { try { Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    /// <summary>CST-172: one track of the video's sound: its volume (0–400 %), how it comes in and leaves, or out.</summary>
    public async Task AdjustPublicationAudioAsync()
    {
        var draft = _publicationDraft;
        if (_publicationsStudio?.Project is not { Video: { Audio.Count: > 0 } video } project) return;
        static string Kind(string k) => k switch { "voice" => "Voz", "music" => "Música", _ => "Efeito" };
        static string N(double v) => v.ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
        var pick = await Dialogs.ChooseAsync("Áudios do vídeo", video.Audio.Select(a => new DialogItem(System.IO.Path.GetFileName(a.Path),
            Detail: Kind(a.Kind) + " · começa em " + N(a.Start) + " s · volume " + N(a.Volume * 100) + " % · entra em " + N(a.FadeIn) + " s, sai em " + N(a.FadeOut) + " s")).ToList());
        if (pick is not { } i || !SameVideoDraft(draft)) return;
        var clip = video.Audio[i];
        var shape = await AudioShapeAsync(clip.Path);
        var action = await Dialogs.ChooseAsync(System.IO.Path.GetFileName(clip.Path),
            [new DialogItem("Volume"), new DialogItem("Entrada suave"), new DialogItem("Saída suave"), new DialogItem("Tirar do vídeo")],
            shape.Length > 0 ? shape : null);
        if (action is not { } a || !SameVideoDraft(draft)) return;
        VideoAudioClip? changed = clip;
        if (a == 3) changed = null;
        else
        {
            var (title, label, value, max) = a switch
            {
                0 => ("Volume", "Porcentagem (100 = como foi gravado)", clip.Volume * 100, 400.0),
                1 => ("Entrada suave", "Segundos subindo do silêncio (0 = começa direto)", clip.FadeIn, 60.0),
                _ => ("Saída suave", "Segundos descendo até o silêncio (0 = termina direto)", clip.FadeOut, 60.0)
            };
            var typed = await Dialogs.PromptAsync(new PromptOptions(title, Label: label, Value: N(value), Confirm: "Aplicar",
                Validate: v => double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && x >= 0 && x <= max ? null : "Escreva um número de 0 a " + N(max) + "."));
            if (typed is null || !SameVideoDraft(draft)) return;
            var x = double.Parse(typed.Replace(',', '.'), CultureInfo.InvariantCulture);
            changed = a switch { 0 => clip with { Volume = x / 100 }, 1 => clip with { FadeIn = x }, _ => clip with { FadeOut = x } };
        }
        var audio = video.Audio.ToList();
        if (changed is null) audio.RemoveAt(i); else audio[i] = changed;
        _publicationsStudio.UpdateProject(project with { Video = video with { Audio = audio } });
        ShowLifeNotice(changed is null ? "Áudio tirado do vídeo." : "Áudio ajustado.");
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

    /// <summary>CST-172: the captions as SRT (players, editors) or WebVTT (the web's &lt;track&gt;), from the same cues.</summary>
    public async Task ExportPublicationSubtitlesAsync(string format = "srt")
    {
        if (_publicationDraft is not { } draft || await PrepareVideoAsync(draft) is not { } prepared) return;
        bool vtt = format == "vtt";
        var bytes = System.Text.Encoding.UTF8.GetBytes(vtt ? prepared.Timeline.Vtt() : prepared.Timeline.Srt());
        var name = ArtifactModel.SafeName(prepared.Project.Title) + (vtt ? ".vtt" : ".srt");
        try
        {
            var saved = SavePageFile is { } save ? await save(name, bytes) : await SavePublicationFileAsync(name, bytes, vtt ? "vtt" : "srt", vtt ? "text/vtt" : "application/x-subrip");
            if (saved == true) ShowLifeNotice(vtt ? "Legendas VTT salvas." : "Legendas SRT salvas.");
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
        // CST-186: the space it needs is checked before any work, never discovered full halfway
        long sources = 0;
        foreach (var clip in timeline.Audio) sources += vault is null ? 0 : (await vault.ReadFileAsync(clip.Path, 0))?.Size ?? 0;
        var needed = VideoBudget.DiskNeeded(timeline.Width, timeline.Height, timeline.Fps, timeline.Duration, timeline.Audio.Count, sources);
        if (FreeSpace(temp) is { } free && free < needed)
        {
            try { Directory.Delete(temp, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            await Dialogs.AlertAsync("Exportar MP4", "Este vídeo precisa de até " + PublicationPackage.Size(needed) + " livres enquanto é gerado, e o aparelho tem " +
                PublicationPackage.Size(free) + ". Libere espaço ou encurte o vídeo.");
            return;
        }
        using var cancel = new CancellationTokenSource();
        _videoExport = cancel;
        var panel = studio.VideoPanel;
        panel.Stop();
        panel.SetProgress(new VideoExportProgress(0, timeline.FrameCount, "Preparando"));
        var skipped = new List<string>();
        try
        {
            var target = System.IO.Path.Combine(temp, ArtifactModel.SafeName(project.Title) + ".mp4");
            var progress = new Progress<VideoExportProgress>(p => panel.SetProgress(p));
            var result = await EncodeVideoAsync(timeline, encoder, temp, target, skipped, progress, cancel.Token);
            panel.SetProgress(null, "MP4 verificado: H.264 " + timeline.Width + "×" + timeline.Height + ", " + result.Info.Video!.SampleCount + " quadros, " +
                PublicationVideoPanel.Clock(result.Info.Seconds) + (result.Info.Audio is null ? ", sem som" : ", com som") + ".");
            bool? saved;
            if (SavePageFile is { } save) saved = await save(System.IO.Path.GetFileName(target), await File.ReadAllBytesAsync(target, CancellationToken.None));
            else saved = await SaveVideoFileAsync(target);
            if (saved == true)
            {
                LastVideoExport = result;
                var focus = WhatChangedSinceLastMp4(timeline, project.Video?.Rendered);
                if (_publicationsStudio?.Project is { Video: { } video } current)
                    _publicationsStudio.UpdateProject(current with
                    {
                        Video = video with { Rendered = timeline.SceneKeys() },
                        LastExport = PublicationExportRecord.Of(current, "mp4", id => _store?.Get(id)?.Content, DateTimeOffset.Now)
                    });
                ShowLifeNotice("Vídeo MP4 salvo." + focus + (skipped.Count > 0 ? " Sem o áudio de: " + string.Join(", ", skipped.Select(System.IO.Path.GetFileName)) + " (formato que este aparelho não lê)." : ""),
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

    async Task<byte[]?> IAssistantApp.SpeakAsync(string text, CancellationToken cancel)
    {
        if (PlatformAudio.SynthesizeToWav is not { } speak) return null;
        var wav = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "urbe-voz-" + Guid.NewGuid().ToString("N") + ".wav");
        try { return await speak(text, 0, wav, cancel) ? await File.ReadAllBytesAsync(wav, cancel) : null; }
        finally { try { File.Delete(wav); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    /// <summary>#956: render_video — the same render, encoding and check as the Estúdio's "Exportar MP4", for the Assistente.</summary>
    async Task<VideoRenderOutput> IAssistantApp.RenderVideoAsync(VideoTimeline timeline, CancellationToken cancel)
    {
        if (PlatformVideo.Encoder() is not { } encoder) throw new AiToolException(PlatformVideo.Unavailable);
        var temp = Directory.CreateTempSubdirectory("urbe-video-").FullName;
        try
        {
            var needed = VideoBudget.DiskNeeded(timeline.Width, timeline.Height, timeline.Fps, timeline.Duration, timeline.Audio.Count, 0);
            if (FreeSpace(temp) is { } free && free < needed)
                throw new AiToolException("Este vídeo precisa de até " + PublicationPackage.Size(needed) + " livres enquanto é gerado, e o aparelho tem " + PublicationPackage.Size(free) + ".");
            var skipped = new List<string>();
            var target = System.IO.Path.Combine(temp, "video.mp4");
            var result = await EncodeVideoAsync(timeline, encoder, temp, target, skipped, null, cancel);
            return new VideoRenderOutput(await File.ReadAllBytesAsync(target, cancel),
                "H.264 " + timeline.Width + "×" + timeline.Height + ", " + result.Info.Video!.SampleCount + " quadros, " +
                PublicationVideoPanel.Clock(result.Info.Seconds) + (result.Info.Audio is null ? ", sem som" : ", com som") + ", verificado", skipped);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { throw new AiToolException("O MP4 não foi gerado: " + e.Message); }
        finally { try { Directory.Delete(temp, true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }

    /// <summary>
    /// The MP4 of a timeline at <paramref name="target"/>: its images and sound read from the vault, the soundtrack mixed
    /// once into a WAV in <paramref name="temp"/>, then rendered, encoded and verified (the Estúdio's export and the
    /// Assistente's render_video). Sound files this device cannot decode go to <paramref name="skipped"/>.
    /// </summary>
    internal async Task<VideoExportResult> EncodeVideoAsync(VideoTimeline timeline, IVideoEncoder encoder, string temp, string target, List<string> skipped,
        IProgress<VideoExportProgress>? progress, CancellationToken cancel)
    {
        var vault = _vault;
        // the images and the sound, read from the vault; the soundtrack is mixed once into a WAV
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in VideoFrameRenderer.FilesUsed(timeline))
            if (vault is not null && (await vault.ReadFileAsync(path, 64L << 20, cancel))?.Bytes is { } bytes) files[path] = bytes;
        string? soundtrack = null;
        if (timeline.Audio.Count > 0)
        {
            var decoded = new List<(VideoAudioClip Clip, Stream Wav)>();
            try
            {
                int n = 0;
                foreach (var clip in timeline.Audio)
                {
                    var read = vault is null ? null : await vault.ReadFileAsync(clip.Path, 1L << 30, cancel);
                    if (read?.Bytes is not { } bytes) { skipped.Add(clip.Path); continue; }
                    // the original and its WAV never share a name (a .wav track was copied onto itself and the export failed)
                    var source = System.IO.Path.Combine(temp, "origem" + n + System.IO.Path.GetExtension(clip.Path));
                    var wav = System.IO.Path.Combine(temp, "a" + n++ + ".wav");
                    await File.WriteAllBytesAsync(source, bytes, cancel);
                    if (await PlatformVideo.ToWavAsync(source, wav, cancel)) decoded.Add((clip, File.OpenRead(wav)));
                    else skipped.Add(clip.Path);
                }
                if (decoded.Count > 0)
                {
                    soundtrack = System.IO.Path.Combine(temp, "trilha.wav");
                    await Task.Run(() => { using var output = File.Create(soundtrack); VideoAudio.Mix(decoded, timeline.Duration, output, cancel); }, cancel);
                }
            }
            finally { foreach (var (_, wav) in decoded) wav.Dispose(); }
        }
        using var renderer = new VideoFrameRenderer(timeline, files);
        return await VideoExport.RunAsync(renderer, encoder, target, soundtrack, progress, cancel);
    }

    /// <summary>
    /// #956: after a new MP4, which scenes look different from the last one (an edited word, a moved element), so the
    /// person sees the change landed where it was made and the rest came out the same.
    /// </summary>
    public static string WhatChangedSinceLastMp4(VideoTimeline timeline, IReadOnlyDictionary<string, string>? rendered)
    {
        if (timeline.ChangedSince(rendered) is not { } changed) return "";
        if (changed.Count == 0) return " Nenhuma cena mudou desde o último MP4.";
        if (changed.Count == timeline.Scenes.Count) return timeline.Scenes.Count == 1 ? "" : " Todas as cenas mudaram desde o último MP4.";
        string Name(VideoTimelineScene s) => (timeline.Scenes.ToList().IndexOf(s) + 1) + " (" + PublicationVideoPanel.Clock(s.Start) + "–" + PublicationVideoPanel.Clock(s.End) + ")";
        return (changed.Count == 1 ? " Mudou só a cena " : " Mudaram as cenas ") + string.Join(", ", changed.Take(4).Select(Name)) + (changed.Count > 4 ? "…" : "") +
            "; as outras saíram iguais ao último MP4.";
    }

    /// <summary>Free bytes where the export writes, or null when the platform does not say.</summary>
    public static Func<string, long?> FreeSpace { get; set; } = path =>
    {
        try { return new DriveInfo(path).AvailableFreeSpace; }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return null; }
    };

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
        await ExportFiles.WriteAndCheckAsync(file.OpenWriteAsync, file.OpenReadAsync, () => File.OpenRead(path));
        return true;
    }
}
