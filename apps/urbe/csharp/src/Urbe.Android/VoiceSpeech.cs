using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Speech;

namespace Urbe.AndroidHost;

/// <summary>What a recognition session gave: the text (null when none) and the error code when it failed.</summary>
internal sealed record SpeechOutcome(string? Text, SpeechRecognizerError? Error)
{
    /// <summary>True when the recognizer said there was nothing to understand (not a failure of the engine).</summary>
    public bool NothingSaid => Error is SpeechRecognizerError.NoMatch or SpeechRecognizerError.SpeechTimeout;
}

/// <summary>
/// UC-66: one on-device <see cref="SpeechRecognizer"/> session in pt-BR, either listening to the microphone itself
/// (before Android 13) or reading the PCM the Urbe records (Android 13+, EXTRA_AUDIO_SOURCE). Segments and partial
/// results are joined so a pause in the middle does not cut the note. Create and use on the main thread only.
/// </summary>
internal sealed class SpeechSession : Java.Lang.Object, IRecognitionListener
{
    public const int SampleRate = 16000;
    // RecognizerIntent extras by their documented string values (API 33 names), so the binding's names do not matter
    private const string ExtraAudioSource = "android.speech.extra.AUDIO_SOURCE";
    private const string ExtraAudioSourceChannelCount = "android.speech.extra.AUDIO_SOURCE_CHANNEL_COUNT";
    private const string ExtraAudioSourceEncoding = "android.speech.extra.AUDIO_SOURCE_ENCODING";
    private const string ExtraAudioSourceSamplingRate = "android.speech.extra.AUDIO_SOURCE_SAMPLING_RATE";
    private const string ExtraSegmentedSession = "android.speech.extra.SEGMENTED_SESSION";
    private const string ExtraCompleteSilence = "android.speech.extras.SPEECH_INPUT_COMPLETE_SILENCE_LENGTH_MILLIS";
    private const string ExtraPossiblyCompleteSilence = "android.speech.extras.SPEECH_INPUT_POSSIBLY_COMPLETE_SILENCE_LENGTH_MILLIS";

    private readonly SpeechRecognizer _recognizer;
    private readonly ParcelFileDescriptor? _source;
    private readonly TaskCompletionSource<SpeechOutcome> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<string> _segments = [];
    private string? _partial;
    private bool _finished;

    /// <summary>The words so far (segments and the current partial), on the main thread.</summary>
    public Action<string>? Heard { get; set; }

    public Task<SpeechOutcome> Result => _done.Task;

    private SpeechSession(SpeechRecognizer recognizer, ParcelFileDescriptor? source)
    {
        _recognizer = recognizer;
        _source = source;
    }

    /// <summary>
    /// Starts a session: from <paramref name="source"/> (raw 16 kHz mono PCM 16-bit, Android 13+) or from the
    /// microphone when null. The on-device recognizer is preferred when asked and available. Null when the device
    /// has no recognizer at all.
    /// </summary>
    public static SpeechSession? Start(Context context, ParcelFileDescriptor? source, bool preferOnDevice)
    {
        SpeechRecognizer? recognizer = null;
        if (preferOnDevice && OperatingSystem.IsAndroidVersionAtLeast(31) && SpeechRecognizer.IsOnDeviceRecognitionAvailable(context))
            recognizer = SpeechRecognizer.CreateOnDeviceSpeechRecognizer(context);
        else if (SpeechRecognizer.IsRecognitionAvailable(context))
            recognizer = SpeechRecognizer.CreateSpeechRecognizer(context);
        if (recognizer is null) return null;
        var session = new SpeechSession(recognizer, source);
        recognizer.SetRecognitionListener(session);
        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraLanguage, "pt-BR");
        intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
        intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
        intent.PutExtra(ExtraCompleteSilence, 2500L);
        intent.PutExtra(ExtraPossiblyCompleteSilence, 2500L);
        if (source is not null)
        {
            intent.PutExtra(ExtraAudioSource, source);
            intent.PutExtra(ExtraAudioSourceChannelCount, 1);
            intent.PutExtra(ExtraAudioSourceEncoding, (int)Android.Media.Encoding.Pcm16bit);
            intent.PutExtra(ExtraAudioSourceSamplingRate, SampleRate);
            // the session lasts until the audio ends, with results per segment, instead of stopping at the first pause
            intent.PutExtra(ExtraSegmentedSession, ExtraAudioSource);
        }
        try { recognizer.StartListening(intent); }
        catch (Java.Lang.Exception) { session.Finish(null, SpeechRecognizerError.Client); }
        return session;
    }

    /// <summary>Microphone mode: stop listening and deliver what was heard.</summary>
    public void StopListening()
    {
        if (_finished) return;
        try { _recognizer.StopListening(); } catch (Java.Lang.Exception) { }
    }

    /// <summary>Ends now with what was heard so far (timeouts).</summary>
    public void Abort()
    {
        if (_finished) return;
        try { _recognizer.Cancel(); } catch (Java.Lang.Exception) { }
        Finish(Joined(_partial), _segments.Count > 0 || _partial is not null ? null : SpeechRecognizerError.Client);
    }

    private string? Joined(string? last)
    {
        var parts = _segments.ToList();
        if (!string.IsNullOrWhiteSpace(last) && (parts.Count == 0 || parts[^1] != last)) parts.Add(last);
        var text = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        return text.Length == 0 ? null : text;
    }

    private void Finish(string? text, SpeechRecognizerError? error)
    {
        if (_finished) return;
        _finished = true;
        try { _recognizer.Destroy(); } catch (Java.Lang.Exception) { }
        try { _source?.Close(); } catch (Java.IO.IOException) { }
        _done.TrySetResult(new SpeechOutcome(text, text is null ? error : null));
    }

    private static string? First(Bundle? bundle) =>
        bundle?.GetStringArrayList(SpeechRecognizer.ResultsRecognition)?.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

    public void OnResults(Bundle? results) => Finish(Joined(First(results) ?? _partial), SpeechRecognizerError.NoMatch);

    public void OnPartialResults(Bundle? partialResults)
    {
        if (First(partialResults) is not { } partial) return;
        _partial = partial;
        if (Joined(partial) is { } text) Heard?.Invoke(text);
    }

    public void OnSegmentResults(Bundle segmentResults)
    {
        if (First(segmentResults) is not { } segment) return;
        _segments.Add(segment);
        _partial = null;
        if (Joined(null) is { } text) Heard?.Invoke(text);
    }

    public void OnEndOfSegmentedSession() => Finish(Joined(_partial), SpeechRecognizerError.NoMatch);

    public void OnError([GeneratedEnum] SpeechRecognizerError error) =>
        Finish(_segments.Count > 0 ? Joined(_partial) : null, error);

    public void OnBeginningOfSpeech() { }
    public void OnBufferReceived(byte[]? buffer) { }
    public void OnEndOfSpeech() { }
    public void OnEvent(int eventType, Bundle? @params) { }
    public void OnReadyForSpeech(Bundle? @params) { }
    public void OnRmsChanged(float rmsdB) { }
}

/// <summary>UC-66: transcription of a kept recording (the app's "Tentar"), Android 13+.</summary>
internal static class VoiceSpeech
{
    /// <summary>
    /// The text of a WAV kept by the widget (16 kHz mono PCM 16-bit after its 44-byte header), or null when this
    /// Android cannot transcribe a file (before 13) or nothing was understood. Throws when the engine failed.
    /// </summary>
    public static async Task<string?> TranscribeFileAsync(Context context, string wavPath, bool preferOnDevice = true)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33) || !File.Exists(wavPath)) return null;
        var pipe = ParcelFileDescriptor.CreatePipe();
        if (pipe is null || pipe.Length < 2) return null;
        var session = await MainThread.RunAsync(() => SpeechSession.Start(context, pipe[0], preferOnDevice));
        if (session is null)
        {
            pipe[0].Close();
            pipe[1].Close();
            throw new InvalidOperationException("Este aparelho não tem reconhecimento de voz.");
        }
        long length = new FileInfo(wavPath).Length;
        _ = Task.Run(() =>
        {
            using var output = new ParcelFileDescriptor.AutoCloseOutputStream(pipe[1]);
            try
            {
                using var input = File.OpenRead(wavPath);
                input.Seek(Math.Min(44, input.Length), SeekOrigin.Begin);
                var chunk = new byte[6400];
                int read;
                while ((read = input.Read(chunk, 0, chunk.Length)) > 0) output.Write(chunk, 0, read);
            }
            catch (Java.IO.IOException) { }   // the recognizer stopped reading
            catch (IOException) { }
        });
        // generous: the engine may read at real time; 30 s more for the final result
        var limit = TimeSpan.FromSeconds(length / (2.0 * SpeechSession.SampleRate) + 30);
        if (await Task.WhenAny(session.Result, Task.Delay(limit)) != session.Result)
            await MainThread.RunAsync(() => { session.Abort(); return true; });
        var outcome = await session.Result;
        if (outcome.Text is not null || outcome.NothingSaid) return outcome.Text;
        throw new InvalidOperationException("O reconhecimento de voz falhou (" + outcome.Error + ").");
    }
}

/// <summary>Runs on the Android main thread (where SpeechRecognizer must live).</summary>
internal static class MainThread
{
    private static readonly Handler Main = new(Looper.MainLooper!);

    public static bool IsMain => Looper.MyLooper() == Looper.MainLooper;

    public static Task<T> RunAsync<T>(Func<T> work)
    {
        if (IsMain) return Task.FromResult(work());
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Main.Post(() =>
        {
            try { done.TrySetResult(work()); }
            catch (Exception ex) { done.TrySetException(ex); }
        });
        return done.Task;
    }

    public static void Post(Action work) => Main.Post(work);

    public static void PostDelayed(Action work, long millis) => Main.PostDelayed(work, millis);
}
