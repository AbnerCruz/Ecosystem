using System.Collections.Concurrent;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Urbe.Client;
using Urbe.Core;

// UC-66 (NN-016): the widget records the quick note itself. The microphone is used only between the tap and the stop,
// with the "Gravando…" notification on screen; the audio stays in the app's private files until its line is written.
[assembly: UsesPermission(Android.Manifest.Permission.RecordAudio)]
[assembly: UsesPermission(Android.Manifest.Permission.ForegroundService)]
[assembly: UsesPermission("android.permission.FOREGROUND_SERVICE_MICROPHONE")]   // Android 14+
[assembly: UsesPermission("android.permission.POST_NOTIFICATIONS")]              // Android 13+
// a short vibration confirms start and save while the screen may be off (no view to give haptic feedback)
[assembly: UsesPermission(Android.Manifest.Permission.Vibrate)]

namespace Urbe.AndroidHost;

/// <summary>UC-66: the queue, the permission and the save of the widget's voice notes.</summary>
internal static class VoiceNotes
{
    /// <summary>The queue file and the recordings: app-private, never in the vault.</summary>
    public static VoiceNoteQueueFile Queue(Context context) =>
        new(System.IO.Path.Combine(context.FilesDir?.AbsolutePath ?? System.IO.Path.GetTempPath(), "voz", "fila.json"));

    public static bool HasMicrophone(Context context) =>
        context.CheckSelfPermission(Android.Manifest.Permission.RecordAudio) == Permission.Granted;

    /// <summary>What the first tap asks: the microphone, and on Android 13+ showing the notifications.</summary>
    public static string[] Permissions() => OperatingSystem.IsAndroidVersionAtLeast(33)
        ? [Android.Manifest.Permission.RecordAudio, "android.permission.POST_NOTIFICATIONS"]
        : [Android.Manifest.Permission.RecordAudio];

    /// <summary>The app's backlog: kept recordings transcribed again and written through the open app.</summary>
    public static IVoiceNoteBacklog Backlog(Context context) =>
        new VoiceNoteQueueBacklog(Queue(context), wav => VoiceSpeech.TranscribeFileAsync(context, wav), VoiceNoteService.IsBusy);

    /// <summary>
    /// Appends the line with the verified write: through the open app when there is one (its notes and vault stay in
    /// step), else straight to the vault folder chosen in the app (the persisted SAF tree). True only when written.
    /// </summary>
    public static async Task<bool> SaveAsync(string text, DateTime saidAt)
    {
        if (await PlatformVoice.TrySaveInAppAsync(text, saidAt, TimeSpan.FromSeconds(15)) is { } inApp) return inApp;
        try
        {
            var storage = new AndroidVaultStorage();
            var snapshot = await storage.RestoreAsync();
            if (snapshot is null || snapshot.IsReadOnly) return false;
            var path = QuickNotes.PathFor(saidAt);
            var doc = snapshot.Documents.FirstOrDefault(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
            if (doc is not null && doc.Text is null) return false;
            var made = QuickNotes.Append(doc?.Text, text, saidAt);
            if (made is null) return false;
            if (doc is not null) await storage.SaveExistingTextFileAsync(doc.Path, made.Value.Content);
            else
            {
                await storage.CreateDirectoryAsync(QuickNotes.Folder);
                await storage.CreateTextFileAsync(path, made.Value.Content);
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException or NotSupportedException or Java.Lang.Exception)
        {
            CrashLog.Write(ex, "voz");
            return false;
        }
    }

    /// <summary>A short vibration: a tick when recording starts, a confirm when saved.</summary>
    public static void Vibrate(Context context, bool confirm)
    {
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26)) return;
            Vibrator? vibrator;
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
                vibrator = (context.GetSystemService(Context.VibratorManagerService) as VibratorManager)?.DefaultVibrator;
            else
            {
#pragma warning disable CS0618, CA1422 // VIBRATOR_SERVICE is the way before Android 12
                vibrator = context.GetSystemService(Context.VibratorService) as Vibrator;
#pragma warning restore CS0618, CA1422
            }
            if (vibrator is null || !vibrator.HasVibrator) return;
            var effect = OperatingSystem.IsAndroidVersionAtLeast(29)
                ? VibrationEffect.CreatePredefined(confirm ? VibrationEffect.EffectDoubleClick : VibrationEffect.EffectTick)
                : VibrationEffect.CreateOneShot(confirm ? 40 : 20, VibrationEffect.DefaultAmplitude);
            if (effect is not null) vibrator.Vibrate(effect);
        }
        catch (Java.Lang.Exception) { }
    }
}

/// <summary>
/// UC-66: the widget's recorder. A tap starts it (foreground service, type microphone; no activity opens), a second
/// tap or the notification stops it, and it stops by itself only as <see cref="VoiceCapturePolicy"/> says (CST-056: a pause
/// to think no longer ends the note). A tap while the previous note is still being saved starts the next one (CST-055);
/// the recognizer serves one recording at a time, so a later one waits for it with its audio kept.
/// Android 13+: the Urbe records 16 kHz PCM to a private WAV and streams it to the on-device recognizer;
/// before 13 the recognizer listens to the microphone itself. The line is appended to "Notas rápidas/&lt;mês&gt;.md"
/// with the verified write; the recording leaves the queue only after that, so turning the phone off loses nothing.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeMicrophone)]
public sealed class VoiceNoteService : Service
{
    private const string ActionToggle = "app.urbe.voice.TOGGLE";
    private const string ActionStart = "app.urbe.voice.START";
    private const string ActionStop = "app.urbe.voice.STOP";
    private const string RecordingChannel = "urbe-voz";
    private const string DoneChannel = "urbe-voz-salva";
    private const int RecordingId = 6601;
    private const int DoneId = 6602;
    private const int ChunkMillis = 100;

    /// <summary>True while the microphone is recording (the widget shows "Gravando…").</summary>
    public static volatile bool IsRecording;

    /// <summary>The queue entries being recorded or saved now (the app's retry leaves them alone).</summary>
    private static readonly ConcurrentDictionary<string, byte> Active = new();

    public static bool IsBusy(string id) => Active.ContainsKey(id);

    /// <summary>One recording at a time uses the recognizer; a later one waits with its audio kept.</summary>
    private static readonly SemaphoreSlim Recognizer = new(1, 1);

    private volatile bool _stop;
    private int _saving;
    private volatile int _elapsed;
    private SpeechSession? _live;
    private string _heard = "";
    private long _lastNotify;
    private int _session;

    private string Title() => IsRecording ? VoiceCapturePolicy.RecordingTitle(_elapsed, Volatile.Read(ref _saving)) : VoiceCapturePolicy.SavingTitle(Math.Max(1, Volatile.Read(ref _saving)));

    /// <summary>A short toast, always from the main thread.</summary>
    private void Say(string text, ToastLength length) =>
        MainThread.Post(() => { try { Toast.MakeText(this, text, length)?.Show(); } catch (Java.Lang.Exception) { } });

    public static Intent ToggleIntent(Context context) => new Intent(context, typeof(VoiceNoteService)).SetAction(ActionToggle)!;
    public static Intent StartIntent(Context context) => new Intent(context, typeof(VoiceNoteService)).SetAction(ActionStart)!;

    public override IBinder? OnBind(Intent? intent) => null;

    [return: GeneratedEnum]
    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        var action = intent?.Action ?? ActionToggle;
        // every start through startForegroundService must be answered with startForeground
        bool busy = IsRecording || Volatile.Read(ref _saving) > 0;
        bool foreground = Foreground(RecordingNotification(busy ? Title() : VoiceCapturePolicy.RecordingTitle(0, 0), _heard));
        if (!foreground && !busy)
        {
            if (!VoiceNotes.HasMicrophone(this))
                NotifyDone("Permita o microfone para gravar", "Toque aqui ou no widget para permitir.", permission: true);
            else
                NotifyDone("Não foi possível gravar agora", "Abra o Urbe e use Criar → Nota rápida.", permission: false);
            VoiceNoteWidget.Refresh(this, recording: false);
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        if (IsRecording)
        {
            if (action != ActionStart) RequestStop();
            return StartCommandResult.NotSticky;
        }
        // the notification's tap while only saving: nothing to stop; the saving goes on
        if (action == ActionStop) { Finish(); return StartCommandResult.NotSticky; }
        Begin();
        return StartCommandResult.NotSticky;
    }

    private void Begin()
    {
        _stop = false;
        _heard = "";
        _elapsed = 0;
        IsRecording = true;
        var created = DateTimeOffset.Now;
        var id = created.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..6];
        Active[id] = 0;
        VoiceNoteWidget.Refresh(this, recording: true);
        VoiceNotes.Vibrate(this, confirm: false);
        int token = ++_session;
        MainThread.PostDelayed(() => { if (token == _session && IsRecording) RequestStop(); }, VoiceCapturePolicy.MaxMillis);
        _ = OperatingSystem.IsAndroidVersionAtLeast(33) ? RecordAsync(id, created, token) : ListenAsync(id, created, token);
    }

    private void RequestStop()
    {
        _stop = true;
        // microphone mode: the recognizer delivers what it heard
        MainThread.Post(() => { if (_live is { } live && !OperatingSystem.IsAndroidVersionAtLeast(33)) live.StopListening(); });
    }

    /// <summary>Android 13+: record the WAV (kept) and stream the same PCM to the recognizer.</summary>
    private async Task RecordAsync(string id, DateTimeOffset created, int token)
    {
        var queue = VoiceNotes.Queue(this);
        var wavName = id + ".wav";
        var wavPath = System.IO.Path.Combine(queue.Folder, wavName);
        var entry = new VoiceNoteEntry(id, created, wavName, null);
        // the recognizer listens live only when no earlier note is still using it; otherwise this one waits for it
        bool recognizer = Recognizer.Wait(0);
        bool recording = true;
        try
        {
            Directory.CreateDirectory(queue.Folder);
            queue.Add(entry);
            var pipe = recognizer ? ParcelFileDescriptor.CreatePipe() : null;
            var session = pipe is { Length: >= 2 } ? await MainThread.RunAsync(() => SpeechSession.Start(this, pipe[0], preferOnDevice: true)) : null;
            if (session is not null) { _live = session; session.Heard = t => { if (token == _session) Heard(t); }; }
            else if (pipe is not null) foreach (var end in pipe) end.Close();
            var capture = await Task.Run(() => Capture(wavPath, session is null ? null : pipe![1], session));
            Interlocked.Increment(ref _saving);
            recording = false;
            if (token == _session) IsRecording = false;
            VoiceNoteWidget.Refresh(this, recording: IsRecording);
            if (!capture.HeardSpeech)
            {
                if (session is not null) await MainThread.RunAsync(() => { session.Abort(); return true; });
                queue.Remove(id);
                Say(capture.Error ?? "Não ouvi nada. Toque e fale sua nota.", ToastLength.Short);
                return;
            }
            UpdateRecording(Title(), IsRecording ? _heard : null);
            if (!recognizer) { await Recognizer.WaitAsync(); recognizer = true; }
            string? text = null;
            if (session is null)
            {
                try { text = QuickNotes.Clean(await VoiceSpeech.TranscribeFileAsync(this, wavPath, preferOnDevice: true)); }
                catch (InvalidOperationException) { }
            }
            else
            {
                bool early = capture.LiveEndedEarly;   // the engine ended before the audio did: transcribe the whole file
                if (await Task.WhenAny(session.Result, Task.Delay(20_000)) != session.Result)
                    await MainThread.RunAsync(() => { session.Abort(); return true; });
                var live = await session.Result;
                text = early ? null : QuickNotes.Clean(live.Text);
                if (text is null)
                {
                    try { text = QuickNotes.Clean(await VoiceSpeech.TranscribeFileAsync(this, wavPath, preferOnDevice: false)); }
                    catch (InvalidOperationException) { }
                    text ??= QuickNotes.Clean(live.Text);
                }
            }
            if (text is null)
            {
                queue.Replace(entry with { Attempts = 1, Error = "Não foi possível transcrever." });
                NotifyDone("Nota de voz guardada", "O Urbe transcreve quando você abrir o app.", permission: false);
                return;
            }
            if (recognizer) { Recognizer.Release(); recognizer = false; }
            await SaveAsync(queue, entry with { Text = text });
        }
        catch (Exception ex)
        {
            // the WAV and the entry stay: the app offers them again
            CrashLog.Write(ex, "voz");
            NotifyDone("Nota de voz guardada", "O Urbe tenta de novo quando você abrir o app.", permission: false);
        }
        finally
        {
            if (recognizer) Recognizer.Release();
            if (recording) { if (token == _session) IsRecording = false; }
            else Interlocked.Decrement(ref _saving);
            Active.TryRemove(id, out _);
            Finish();
        }
    }

    /// <summary>Before Android 13: the recognizer listens to the microphone; only the text is kept.</summary>
    private async Task ListenAsync(string id, DateTimeOffset created, int token)
    {
        var queue = VoiceNotes.Queue(this);
        // here the recognizer is the microphone: a new note waits until the previous one has been understood
        await Recognizer.WaitAsync();
        bool recognizer = true, recording = true;
        try
        {
            var session = await MainThread.RunAsync(() => SpeechSession.Start(this, null, preferOnDevice: false));
            if (session is null)
            {
                IsRecording = false;
                Say("Este aparelho não tem reconhecimento de voz.", ToastLength.Long);
                return;
            }
            _live = session;
            session.Heard = t => { if (token == _session) Heard(t); };
            if (await Task.WhenAny(session.Result, Task.Delay(VoiceCapturePolicy.MaxMillis + 20_000)) != session.Result)
                await MainThread.RunAsync(() => { session.Abort(); return true; });
            var outcome = await session.Result;
            Recognizer.Release();
            recognizer = false;
            Interlocked.Increment(ref _saving);
            recording = false;
            if (token == _session) IsRecording = false;
            VoiceNoteWidget.Refresh(this, recording: IsRecording);
            if (QuickNotes.Clean(outcome.Text) is not { } text)
            {
                Say(outcome.NothingSaid ? "Não ouvi nada. Toque e fale sua nota." : "Não consegui transcrever. Tente de novo.", ToastLength.Short);
                return;
            }
            var entry = new VoiceNoteEntry(id, created, null, text);
            queue.Add(entry);
            await SaveAsync(queue, entry);
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex, "voz");
            NotifyDone("Nota de voz não salva", ex.Message, permission: false);
        }
        finally
        {
            if (recognizer) Recognizer.Release();
            if (recording) { if (token == _session) IsRecording = false; }
            else Interlocked.Decrement(ref _saving);
            Active.TryRemove(id, out _);
            Finish();
        }
    }

    private async Task SaveAsync(VoiceNoteQueueFile queue, VoiceNoteEntry entry)
    {
        queue.Replace(entry);
        bool saved;
        await VoiceNoteQueueBacklog.SaveGate.WaitAsync();
        try
        {
            saved = await VoiceNotes.SaveAsync(entry.Text!, entry.SaidAt);
            if (saved) queue.Remove(entry.Id);
            else queue.Replace(entry with { Attempts = entry.Attempts + 1, Error = "Não foi possível gravar no vault." });
        }
        finally { VoiceNoteQueueBacklog.SaveGate.Release(); }
        if (saved)
        {
            NotifyDone("Salvo em " + QuickNotes.Folder, FirstWords(entry.Text!), permission: false);
            VoiceNotes.Vibrate(this, confirm: true);
        }
        else NotifyDone("Nota de voz guardada", "Abra o Urbe e escolha a pasta do vault para salvá-la.", permission: false);
    }

    private static string FirstWords(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length <= 8 ? text : string.Join(' ', words.Take(8)) + "…";
    }

    private readonly record struct CaptureResult(bool HeardSpeech, string? Error, bool LiveEndedEarly = false);

    /// <summary>
    /// The recording loop (background thread): 100 ms chunks to the WAV (flushed every second, so a killed app
    /// keeps the audio) and to the recognizer's pipe through a queue that never blocks the microphone.
    /// </summary>
    private CaptureResult Capture(string wavPath, ParcelFileDescriptor? pipeWrite, SpeechSession? live)
    {
        int rate = SpeechSession.SampleRate;
        int chunkBytes = rate * 2 * ChunkMillis / 1000;
        int min = AudioRecord.GetMinBufferSize(rate, ChannelIn.Mono, Android.Media.Encoding.Pcm16bit);
        AudioRecord? record = null;
        var feed = new BlockingCollection<byte[]>(boundedCapacity: 1200);
        Task? feeder = null;
        if (pipeWrite is not null)
            feeder = Task.Run(() =>
            {
                using var output = new ParcelFileDescriptor.AutoCloseOutputStream(pipeWrite);
                try { foreach (var chunk in feed.GetConsumingEnumerable()) output.Write(chunk, 0, chunk.Length); }
                catch (Java.IO.IOException) { foreach (var _ in feed.GetConsumingEnumerable()) { } }   // engine stopped reading
            });
        bool heard = false;
        try
        {
            record = new AudioRecord(AudioSource.VoiceRecognition, rate, ChannelIn.Mono, Android.Media.Encoding.Pcm16bit, Math.Max(min, chunkBytes * 4));
            if (record.State != State.Initialized) return new CaptureResult(false, "O microfone está ocupado por outro app.");
            using var wav = new FileStream(wavPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            Wav.WriteHeader(wav, rate, 0);
            record.StartRecording();
            var buffer = new byte[chunkBytes];
            long total = 0, sinceFlush = 0;
            int elapsed = 0, loud = 0, silence = 0, shown = -1;
            double floor = -1;
            while (!_stop && !VoiceCapturePolicy.ShouldStop(elapsed, heard, silence))
            {
                int read = record.Read(buffer, 0, buffer.Length);
                if (read <= 0) { if (read < 0) break; continue; }
                wav.Write(buffer, 0, read);
                total += read;
                sinceFlush += read;
                if (sinceFlush >= rate * 2) { wav.Flush(true); sinceFlush = 0; }
                if (feeder is not null && !feed.IsAddingCompleted) feed.TryAdd(buffer.AsSpan(0, read).ToArray());
                elapsed += read * 1000 / (rate * 2);
                // voice activity: loud chunks against an adaptive noise floor
                double rms = Wav.Rms(buffer, read);
                floor = floor < 0 ? rms : rms < floor ? rms : floor + (rms - floor) * 0.02;
                bool voice = rms > Math.Max(350, floor * 3);
                if (voice) { loud++; silence = 0; if (loud >= 3) heard = true; }
                else { loud = 0; silence += ChunkMillis; }
                // the time on the notification, once a second (CST-056: the person sees there is no hurry)
                _elapsed = elapsed;
                if (elapsed / 1000 != shown) { shown = elapsed / 1000; UpdateRecording(Title(), _heard); }
            }
            bool early = live?.Result.IsCompleted == true;
            try { record.Stop(); } catch (Java.Lang.IllegalStateException) { }
            Wav.WriteHeader(wav, rate, total);
            wav.Flush(true);
            return new CaptureResult(heard, null, early);
        }
        finally
        {
            record?.Release();
            feed.CompleteAdding();
            try { feeder?.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { }
        }
    }

    private void Heard(string text)
    {
        _heard = text;
        long now = System.Environment.TickCount64;
        if (now - _lastNotify < 400) return;
        _lastNotify = now;
        UpdateRecording(Title(), text);
    }

    private void Finish()
    {
        // another note is still being recorded or saved: the service (and its notification) stays
        if (IsRecording || Volatile.Read(ref _saving) > 0) { UpdateRecording(Title(), IsRecording ? _heard : null); return; }
        _live = null;
        VoiceNoteWidget.Refresh(this, recording: false);
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(24)) StopForeground(StopForegroundFlags.Remove);
        }
        catch (Java.Lang.Exception) { }
        StopSelf();
    }

    public override void OnDestroy()
    {
        _stop = true;
        IsRecording = false;
        VoiceNoteWidget.Refresh(this, recording: false);
        base.OnDestroy();
    }

    // ---- notifications ----

    private void Channels()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26) || GetSystemService(NotificationService) is not NotificationManager manager) return;
        manager.CreateNotificationChannel(new NotificationChannel(RecordingChannel, "Gravando nota rápida", NotificationImportance.Low));
        manager.CreateNotificationChannel(new NotificationChannel(DoneChannel, "Nota rápida salva", NotificationImportance.Low));
    }

    private Notification.Builder Builder(string channel)
    {
        Channels();
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) return new Notification.Builder(this, channel);
#pragma warning disable CS0618, CA1422 // channels exist only from Android 8
        return new Notification.Builder(this);
#pragma warning restore CS0618, CA1422
    }

    private Notification RecordingNotification(string title = "Gravando… toque para parar", string? text = null)
    {
        var stop = PendingIntent.GetService(this, 3, new Intent(this, typeof(VoiceNoteService)).SetAction(ActionStop),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        return Builder(RecordingChannel)
            .SetSmallIcon(Resource.Drawable.urbe_voice_mic)
            .SetContentTitle(title)
            .SetContentText(string.IsNullOrWhiteSpace(text) ? "Fale sua nota rápida" : text)
            .SetContentIntent(stop)
            .SetOngoing(true)
            .SetOnlyAlertOnce(true)
            .Build()!;
    }

    private void UpdateRecording(string title, string? text)
    {
        if (GetSystemService(NotificationService) is NotificationManager manager)
            manager.Notify(RecordingId, RecordingNotification(title, text));
    }

    /// <summary>Starts or refreshes the foreground state (type microphone on Android 11+). False when not allowed.</summary>
    private bool Foreground(Notification notification)
    {
        bool microphone = VoiceNotes.HasMicrophone(this);
        try
        {
            // without the permission it is still answered (untyped) so Android does not end the app; then it stops
            if (microphone && OperatingSystem.IsAndroidVersionAtLeast(30)) StartForeground(RecordingId, notification, ForegroundService.TypeMicrophone);
            else StartForeground(RecordingId, notification);
            return microphone;
        }
        catch (Java.Lang.Exception ex)
        {
            // refused (Android 14 without the permission, or a start the system did not see as the person's tap)
            CrashLog.Write(ex, "voz");
            return false;
        }
    }

    /// <summary>A short notification that dismisses itself ("Salvo em Notas rápidas" and the first words).</summary>
    private void NotifyDone(string title, string text, bool permission)
    {
        if (GetSystemService(NotificationService) is not NotificationManager manager) return;
        PendingIntent? open;
        if (permission)
        {
            var ask = new Intent(this, typeof(VoicePermissionActivity));
            ask.SetFlags(ActivityFlags.NewTask);
            open = PendingIntent.GetActivity(this, 4, ask, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }
        else
        {
            var app = new Intent(this, typeof(MainActivity));
            app.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            open = PendingIntent.GetActivity(this, 5, app, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }
        var builder = Builder(DoneChannel)
            .SetSmallIcon(Resource.Drawable.urbe_voice_done)
            .SetContentTitle(title)
            .SetContentText(text)
            .SetContentIntent(open)
            .SetAutoCancel(true);
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && !permission) builder.SetTimeoutAfter(5000);
        try { manager.Notify(DoneId, builder.Build()); } catch (Java.Lang.Exception) { }
    }
}

/// <summary>The 44-byte PCM WAV header and the loudness of a chunk.</summary>
internal static class Wav
{
    public static void WriteHeader(System.IO.Stream stream, int rate, long dataBytes)
    {
        var h = new byte[44];
        void Str(int at, string s) { for (int i = 0; i < 4; i++) h[at + i] = (byte)s[i]; }
        void I32(int at, long v) => BitConverter.TryWriteBytes(h.AsSpan(at, 4), (int)Math.Min(v, int.MaxValue));
        void I16(int at, short v) => BitConverter.TryWriteBytes(h.AsSpan(at, 2), v);
        Str(0, "RIFF"); I32(4, 36 + dataBytes); Str(8, "WAVE");
        Str(12, "fmt "); I32(16, 16); I16(20, 1); I16(22, 1); I32(24, rate); I32(28, rate * 2); I16(32, 2); I16(34, 16);
        Str(36, "data"); I32(40, dataBytes);
        long at = stream.Position;
        stream.Seek(0, SeekOrigin.Begin);
        stream.Write(h, 0, h.Length);
        stream.Seek(Math.Max(at, 44), SeekOrigin.Begin);
    }

    public static double Rms(byte[] pcm, int length)
    {
        long sum = 0;
        int samples = length / 2;
        for (int i = 0; i < samples; i++)
        {
            int s = (short)(pcm[2 * i] | (pcm[2 * i + 1] << 8));
            sum += (long)s * s;
        }
        return samples == 0 ? 0 : Math.Sqrt(sum / (double)samples);
    }
}
