using Android.Content;
using Android.OS;
using Android.Speech.Tts;
using Urbe.Client;

namespace Urbe.AndroidHost;

/// <summary>
/// #956 on Android: the podcast's turns spoken by the system TextToSpeech into WAV files (synthesizeToFile, on the
/// device). Two hosts: two different Portuguese voices when the engine has them, else the same voice at two pitches.
/// </summary>
internal sealed class SpeechSynth : UtteranceProgressListener
{
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly TaskCompletionSource<bool> _ready = new();
    private TaskCompletionSource<bool>? _utterance;
    private readonly TextToSpeech _tts;
    private Voice?[] _voices = [null, null];

    private SpeechSynth(Context context)
    {
        _tts = new TextToSpeech(context, new Init(this));
        _tts.SetOnUtteranceProgressListener(this);
    }

    public static void Install(Context context)
    {
        SpeechSynth? synth = null;
        PlatformAudio.SynthesizeToWav = (text, voice, target, cancel) => (synth ??= new SpeechSynth(context)).SpeakAsync(text, voice, target, cancel);
    }

    private sealed class Init(SpeechSynth owner) : Java.Lang.Object, TextToSpeech.IOnInitListener
    {
        public void OnInit(OperationResult status)
        {
            if (status == OperationResult.Success) owner.ChooseVoices();
            owner._ready.TrySetResult(status == OperationResult.Success);
        }
    }

    private List<Voice> _portuguese = [];

    private void ChooseVoices()
    {
        _tts.SetLanguage(Java.Util.Locale.ForLanguageTag("pt-BR"));
        _portuguese = _tts.Voices?.Where(v => v.Locale?.Language == "pt" && !v.IsNetworkConnectionRequired)
            .OrderBy(v => v.Locale?.Country == "BR" ? 0 : 1).ThenBy(v => v.Name).ToList() ?? [];
        PlatformAudio.Voices = () => _portuguese.Select(v => v.Name ?? "").Where(n => n.Length > 0).ToList();
        Prefer();
    }

    /// <summary>The person's voice first (when the device has it), then the next one for the podcast's second host.</summary>
    private void Prefer()
    {
        var first = _portuguese.FirstOrDefault(v => v.Name == PlatformAudio.PreferredVoice) ?? _portuguese.FirstOrDefault();
        _voices = [first, _portuguese.FirstOrDefault(v => v != first)];
    }

    private async Task<bool> SpeakAsync(string text, int voice, string target, CancellationToken cancel)
    {
        if (!await _ready.Task) return false;
        await _one.WaitAsync(cancel);
        try
        {
            Prefer();
            if (_voices[voice] is { } chosen) { _tts.SetVoice(chosen); _tts.SetPitch(1f); }
            else { if (_voices[0] is { } first) _tts.SetVoice(first); _tts.SetPitch(voice == 0 ? 1f : 0.8f); }
            _utterance = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var file = new Java.IO.File(target);
            if (_tts.SynthesizeToFile(text, new Bundle(), file, "fala") != OperationResult.Success) return false;
            using (cancel.Register(() => { _tts.Stop(); _utterance.TrySetCanceled(cancel); }))
                return await _utterance.Task;
        }
        finally { _one.Release(); }
    }

    public override void OnStart(string? utteranceId) { }
    public override void OnDone(string? utteranceId) => _utterance?.TrySetResult(true);
    [Obsolete("Required override of the deprecated callback.")]
    public override void OnError(string? utteranceId) => _utterance?.TrySetResult(false);
    public override void OnError(string? utteranceId, TextToSpeechError errorCode) => _utterance?.TrySetResult(false);
}
