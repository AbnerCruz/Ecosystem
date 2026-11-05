using Avalonia.Controls;
using Avalonia.Media;
using Urbe.Core.Assistant;

namespace Urbe.Client;

/// <summary>
/// CST-220: the typed model catalog (<see cref="AiModelCatalog"/>) and the selector (<see cref="AiModelSelector"/>) where the
/// panel decides a model: attachments the chosen model cannot read, visual reading, image generation, the spend estimate and
/// the settings view of what each configured model does. The person's choice is never switched without asking.
/// </summary>
public sealed partial class AssistantPanel
{
    /// <summary>Every usable model, plus <paramref name="choice"/> even when its provider never listed it.</summary>
    private IReadOnlyList<AiModelCapabilities> Candidates(AiModelChoice? choice = null)
    {
        var all = AiModelCatalog.Configured(_cfg).ToList();
        if (choice is not null && !all.Any(m => m.ProviderId == choice.ProviderId && m.ModelId == choice.Model) &&
            AiModelCatalog.Find(_cfg, choice) is { } chosen) all.Add(chosen);
        return all;
    }

    /// <summary>The capabilities of the conversation's model (tests and the costs view).</summary>
    public AiModelCapabilities? CurrentModelCapabilities => AiModelCatalog.Find(_cfg, _cfg.ModelFor(_conv));

    /// <summary>What the selector says about the pending attachments and the conversation's model, or null without media.</summary>
    public AiSelection? AttachmentSelection()
    {
        var media = _pendingAttachments.Where(b => b.Type == "attachment").Select(b => b.MimeType ?? "").ToList();
        if (media.Count == 0) return null;
        var choice = _cfg.ModelFor(_conv);
        return AiModelSelector.Select(AiModelSelector.ForAttachments(media), Candidates(choice), choice);
    }

    /// <summary>
    /// Before sending images or PDFs to a model known not to read them: offer the capable fallback (saying when the files
    /// would leave the device and what it costs), or say that no configured model reads them. True: go on and send.
    /// </summary>
    private async Task<bool> ReviewAttachmentsAsync(AiSelection selection)
    {
        var kind = AiModelCatalog.ModalityNames(selection.Missing == AiModalities.None ? AiModalities.Image : selection.Missing);
        if (selection.Outcome is AiSelectionOutcome.Fallback or AiSelectionOutcome.NeedsConsent && selection.Fallback is { } fallback)
        {
            var use = await _app.Dialogs.ConfirmAsync("O modelo não lê " + kind,
                selection.Reason + " " + selection.Cost.Text + "\n\nUsar " + fallback.Name + " passa a ser o modelo deste agente (como em Modelo).",
                "Usar " + fallback.Name, cancel: "Manter " + (selection.Model?.Name ?? "o modelo"));
            if (use)
            {
                _cfg.AgentModels[_conv.AgentId] = new AiModelChoice(fallback.ProviderId, fallback.ModelId);
                _conv.ProviderId = fallback.ProviderId;
                _conv.Model = fallback.ModelId;
                SaveConfig();
                RenderChrome();
            }
            return true;
        }
        if (selection.Outcome == AiSelectionOutcome.Missing)
            return await _app.Dialogs.ConfirmAsync("Nenhum modelo lê " + kind,
                selection.Reason + " O modelo receberá só o nome dos arquivos. Configure um provedor multimodal para que eles sejam lidos.",
                "Enviar mesmo assim", cancel: "Não enviar");
        return true;
    }

    private async Task SubmitReviewedAsync(string text, AiSelection selection)
    {
        if (!await ReviewAttachmentsAsync(selection))
        {
            _input.Text = text;
            return;
        }
        Send(text.Length > 0 ? text : "Analise os arquivos anexados.");
    }

    /// <summary>The text shown when no image model is chosen for the agent: the configured ones, with their prices.</summary>
    private string ImagesUnavailable()
    {
        var images = Candidates().Where(m => m.Makes(AiModalities.Image) == true).ToList();
        if (images.Count == 0)
            return "Nenhum modelo de imagem nos provedores configurados. Adicione uma chave OpenRouter, OpenAI ou Google Gemini nas configurações do Assistente; não é necessário trocar o modelo do chat.";
        return "Nenhum modelo de imagem foi escolhido para este agente, e nenhuma imagem é gerada sem essa escolha. Disponíveis: " +
            string.Join("; ", images.Select(m => m.Label + " (" + AiModelCatalog.PriceText(m) + ")")) +
            ". Peça à pessoa que escolha um em Configurações → Agentes → Configurar → Modelo de imagem.";
    }

    /// <summary>CST-220: what each model in use can do, read from the one catalog: modalities, tools, context, price, place.</summary>
    public void ShowModelCapabilities()
    {
        var body = OpenView("O que cada modelo faz", () => ShowSettings());
        body.Children.Add(Hint("O Urbe escolhe pelo que cada modelo informa. Quando o modelo escolhido não faz algo (por exemplo, ler uma imagem), ele avisa e sugere outro, sem trocar sozinho. \"Não informado\" nunca quer dizer sim nem não."));
        var all = Candidates(_cfg.ModelFor(_conv));
        var inUse = _cfg.AgentModels.Values.Concat(_cfg.AgentImages.Values).Append(_cfg.ModelFor(_conv))
            .Select(c => (c.ProviderId, c.Model)).ToHashSet();
        foreach (var group in all.GroupBy(m => m.ProviderId))
        {
            var models = group.Where(m => inUse.Contains((m.ProviderId, m.ModelId)) || m.Place == AiModelPlace.Device ||
                                          m.Makes(AiModalities.Image) == true || group.Count() <= 6).ToList();
            var rows = new List<Control>();
            foreach (var m in models)
            {
                var lines = new StackPanel { Spacing = 2 };
                lines.Children.Add(Wrap(m.Name + (inUse.Contains((m.ProviderId, m.ModelId)) ? " · em uso" : ""), 14, UrbeTheme.Text, FontWeight.SemiBold));
                lines.Children.Add(Wrap(m.ModelId, 11, UrbeTheme.Text3));
                foreach (var line in AiModelCatalog.Describe(m)) lines.Children.Add(Wrap(line, 12, UrbeTheme.Text2));
                rows.Add(new Border { Child = lines, BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2, .55), BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
                    Padding = new Avalonia.Thickness(0, 6, 0, 10) });
            }
            int hidden = group.Count() - models.Count;
            if (hidden > 0) rows.Add(Hint("Mais " + hidden + " modelos no catálogo deste provedor; escolha um em Modelo para vê-lo aqui."));
            body.Children.Add(Section(group.First().ProviderName, [.. rows]));
        }
        if (all.Count == 0) body.Children.Add(Hint("Nenhum provedor configurado ainda."));
        var video = AiModelSelector.Video(all);
        body.Children.Add(Section("Vídeo", Wrap(video.Text, 12, video.Available ? UrbeTheme.Text2 : UrbeTheme.Text3)));
        body.Children.Add(Section("Voz", Hint(PlatformAudio.Available
            ? "Transcrição de áudio em nota: feita no aparelho, sem enviar o áudio a um provedor."
            : "Transcrição de áudio indisponível neste aparelho. O Urbe ainda não envia áudio aos modelos.")));
    }
}
