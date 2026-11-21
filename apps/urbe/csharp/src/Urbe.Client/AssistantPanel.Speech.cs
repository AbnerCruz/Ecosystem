using Urbe.Core.Assistant;

namespace Urbe.Client;

public sealed partial class AssistantPanel
{
    public AiSpeechChoice? SpeechFor(string? reference = null)
    {
        Load();
        var choice = AiSpeechChoice.Parse(reference);
        if (choice is null)
        {
            if (!string.IsNullOrEmpty(reference)) return null;
            choice = AiSpeechChoice.Parse(_cfg.MediaFor(_conv.AgentId).Voice);
        }
        return choice is not null && _cfg.Providers.Any(p => p.Id == choice.ProviderId && AssistantProviders.SupportsSpeech(p))
            ? choice : null;
    }

    public async Task<byte[]> SynthesizeSpeechAsync(AiSpeechChoice choice, string text, CancellationToken cancel)
    {
        var provider = _cfg.Providers.FirstOrDefault(p => p.Id == choice.ProviderId)
            ?? throw new AiProviderException("Reconecte o provedor de voz nas configurações.");
        if (choice.Format == "mp3" && !PlatformVideo.CanDecode)
            throw new AiProviderException("Este aparelho precisa de um decodificador de MP3 para esta voz. Escolha um modelo com WAV ou PCM.");
        var bytes = await Providers.GenerateSpeechAsync(provider, choice, text, cancel);
        if (choice.Format != "mp3") return bytes;
        var directory = Directory.CreateTempSubdirectory("numa-tts-");
        try
        {
            var source = Path.Combine(directory.FullName, "speech.mp3");
            var target = Path.Combine(directory.FullName, "speech.wav");
            await File.WriteAllBytesAsync(source, bytes, cancel);
            if (!await PlatformVideo.ToWavAsync(source, target, cancel))
                throw new AiProviderException("Não foi possível decodificar o áudio do provedor.");
            return await File.ReadAllBytesAsync(target, cancel);
        }
        finally
        {
            try { directory.Delete(true); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private async Task<NarrationVoiceChoice?> PickProviderVoiceAsync()
    {
        Load();
        var providers = _cfg.Providers.Where(AssistantProviders.SupportsSpeech).ToList();
        if (providers.Count == 0)
        {
            await _app.Dialogs.AlertAsync("Voz da narração", "Conecte um provedor com API de síntese de voz para escolher um modelo.");
            return null;
        }
        var index = await _app.Dialogs.ChooseAsync("Provedor de voz",
            providers.Select(p => new DialogItem(p.Name ?? p.Preset)).ToList());
        if (index is not int providerIndex) return null;
        var provider = providers[providerIndex];
        try
        {
            var models = (await Providers.ListSpeechModelsAsync(provider))
                .Where(m => m.Format != "mp3" || PlatformVideo.CanDecode).ToList();
            if (models.Count == 0)
            {
                await _app.Dialogs.AlertAsync("Modelo de voz", "O catálogo deste provedor não informou modelos de síntese compatíveis.");
                return null;
            }
            var modelIndex = await _app.Dialogs.ChooseAsync("Modelo de voz",
                models.Select(m => new DialogItem(m.Name, Detail: m.Id)).ToList());
            if (modelIndex is not int selected) return null;
            var model = models[selected];
            string? voice;
            if (provider.Preset == "custom")
                voice = await _app.Dialogs.PromptAsync(new PromptOptions("Voz do provedor", "Identificador da voz"));
            else
            {
                var voices = model.Voices;
                var picked = await _app.Dialogs.ChooseAsync("Voz gerada por IA", voices.Select(v => new DialogItem(v)).ToList());
                voice = picked is int i ? voices[i] : null;
            }
            return string.IsNullOrWhiteSpace(voice) ? null : new(new AiSpeechChoice(provider.Id, model.Id, voice.Trim(), model.Format).Reference);
        }
        catch (AiProviderException e)
        {
            await _app.Dialogs.AlertAsync("Voz da narração", e.Message);
            return null;
        }
    }

    public Task<NarrationVoiceChoice?> ChooseProviderVoiceAsync() => PickProviderVoiceAsync();
}
