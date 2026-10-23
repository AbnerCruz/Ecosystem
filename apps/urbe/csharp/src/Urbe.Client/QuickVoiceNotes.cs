using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// Speech-to-text the platform offers (UC-56). The Android head sets it to the system recognizer; null where
/// there is none (desktop for now). Returns the text, or null when the person cancelled. Never records audio itself.
/// </summary>
public static class PlatformVoice
{
    /// <summary>Asks the platform to listen and returns the transcribed text (null = cancelled).</summary>
    public static Func<Task<string?>>? Transcribe { get; set; }

    private static int _pending;

    /// <summary>The home-screen widget asked for a quick note: now if the app is ready, else when it is.</summary>
    public static void RequestQuickNote()
    {
        if (UrbeApp.ActiveView is { IsReady: true } view)
            Dispatcher.UIThread.Post(() => _ = view.QuickVoiceNoteAsync());
        else Interlocked.Exchange(ref _pending, 1);
    }

    /// <summary>
    /// UC-66 on Windows: there is no recognizer the app can call, so the note is written in a box where the system's
    /// dictation (<paramref name="hint"/>, Win+H) types what is said, in <paramref name="view"/> or the running app's.
    /// Null when the box was cancelled.
    /// </summary>
    public static Func<Task<string?>> TypedWithDictation(string hint, MainView? view = null) => async () =>
        (view ?? UrbeApp.ActiveView) is { } shown
            ? await shown.Dialogs.PromptAsync(new PromptOptions("Nota rápida", Label: "O que você quer guardar", Placeholder: "Fale ou escreva…",
                Hint: hint, Confirm: "Salvar"))
            : null;

    internal static bool TakePending() => Interlocked.Exchange(ref _pending, 0) == 1;

    /// <summary>
    /// UC-66: voice notes recorded by the widget that are not in the vault yet (Android). Null where there is none.
    /// </summary>
    public static IVoiceNoteBacklog? Backlog { get; set; }

    /// <summary>
    /// UC-66: writes a quick note through the open app (so its notes, index and vault baseline stay in step), waiting
    /// up to <paramref name="wait"/> for a starting app. Null when no app view can write a quick note: the caller
    /// then writes to the vault itself. True/false = the verified write succeeded or failed.
    /// </summary>
    public static async Task<bool?> TrySaveInAppAsync(string text, DateTime at, TimeSpan wait)
    {
        var until = DateTime.UtcNow + wait;
        while (true)
        {
            var view = UrbeApp.ActiveView;
            if (view is null) return null;
            if (view.IsReady)
                return await Dispatcher.UIThread.InvokeAsync(async () =>
                    view.CanSaveQuickNote ? await view.TrySaveQuickNoteAsync(text, at) is null : (bool?)null);
            if (DateTime.UtcNow >= until) return null;
            await Task.Delay(250);
        }
    }
}

/// <summary>
/// UC-66: the recordings the widget kept because their line could not be written yet (no vault, transcription
/// failed, phone off). The app offers to retry them when it opens.
/// </summary>
public interface IVoiceNoteBacklog
{
    /// <summary>How many voice notes are waiting.</summary>
    int Count { get; }

    /// <summary>
    /// Transcribes what still needs it and hands each text to <paramref name="save"/> with the time it was said;
    /// an entry (and its audio) leaves the queue only when <paramref name="save"/> returns true.
    /// </summary>
    Task<(int Saved, int Left)> RetryAsync(Func<string, DateTime, Task<bool>> save);
}

/// <summary>
/// The backlog over the queue file: <paramref name="transcribe"/> turns a kept recording (its full path) into text,
/// or null when it still cannot. Entries with neither text nor audio have nothing left to save and are dropped.
/// </summary>
public sealed class VoiceNoteQueueBacklog(VoiceNoteQueueFile queue, Func<string, Task<string?>> transcribe, Func<string?>? busyId = null) : IVoiceNoteBacklog
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Serializes saving and dequeuing between the widget's service and the app (same process).</summary>
    public static SemaphoreSlim SaveGate => Gate;

    public int Count => queue.LoadAdoptingOrphans().Count(e => e.Id != busyId?.Invoke());

    public async Task<(int Saved, int Left)> RetryAsync(Func<string, DateTime, Task<bool>> save)
    {
        await Gate.WaitAsync();
        try
        {
            int saved = 0, left = 0;
            foreach (var entry in queue.LoadAdoptingOrphans())
            {
                if (entry.Id == busyId?.Invoke()) continue;   // still being recorded
                var current = entry;
                if (QuickNotes.Clean(current.Text) is null)
                {
                    var audio = current.Audio is null ? null : Path.Combine(queue.Folder, current.Audio);
                    if (audio is null || !File.Exists(audio)) { queue.Remove(current.Id); continue; }
                    string? text = null, error = null;
                    try { text = await transcribe(audio); }
                    catch (Exception ex) { error = ex.Message; }
                    current = current with { Text = QuickNotes.Clean(text), Attempts = current.Attempts + 1, Error = text is null ? error ?? "Não foi possível transcrever." : null };
                    queue.Replace(current);
                    if (current.Text is null) { left++; continue; }
                }
                bool ok;
                try { ok = await save(current.Text!, current.SaidAt); }
                catch (Exception) { ok = false; }
                if (ok) { queue.Remove(current.Id); saved++; }
                else { queue.Replace(current with { Error = "Não foi possível gravar no vault." }); left++; }
            }
            return (saved, left);
        }
        finally { Gate.Release(); }
    }
}

/// <summary>UC-56: "Nota rápida" by voice, appended to the day's note in "Notas rápidas" with the verified write.</summary>
public sealed partial class MainView
{
    private Func<Task<string?>>? _voice;
    private bool _voiceBusy;

    /// <summary>The transcriber used here: <see cref="PlatformVoice.Transcribe"/> unless replaced (tests).</summary>
    public Func<Task<string?>>? Voice { get => _voice ?? PlatformVoice.Transcribe; set => _voice = value; }

    /// <summary>True once the first vault (or the empty Explorer, with no folder) is shown.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Called when the first vault is shown: a widget request made while starting runs now.</summary>
    private void MarkReady()
    {
        IsReady = true;
        if (PlatformVoice.TakePending()) Dispatcher.UIThread.Post(() => _ = QuickVoiceNoteAsync());
        OfferVoiceBacklog();
    }

    private IVoiceNoteBacklog? _backlog;
    private bool _backlogBusy;

    /// <summary>The widget's waiting voice notes: <see cref="PlatformVoice.Backlog"/> unless replaced (tests).</summary>
    public IVoiceNoteBacklog? VoiceBacklog { get => _backlog ?? PlatformVoice.Backlog; set => _backlog = value; }

    /// <summary>UC-66: "Há N notas de voz para transcrever" with "Tentar", when the widget left some waiting.</summary>
    public void OfferVoiceBacklog()
    {
        int count;
        try { count = VoiceBacklog?.Count ?? 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        if (count <= 0) return;
        var text = count == 1 ? "Há 1 nota de voz para transcrever." : "Há " + count + " notas de voz para transcrever.";
        _notices.Add(text);
        _notice.Show(text, TimeSpan.FromSeconds(12), "Tentar", () => _ = RetryVoiceBacklogAsync(), Ui.ToastKind.Warning);
    }

    /// <summary>Transcribes and writes the waiting voice notes; whatever fails stays waiting.</summary>
    public async Task RetryVoiceBacklogAsync()
    {
        if (VoiceBacklog is not { } backlog || _backlogBusy) return;
        if (!CanSaveQuickNote)
        {
            ShowLifeNotice("Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para guardar as notas de voz.", Ui.ToastKind.Warning);
            return;
        }
        _backlogBusy = true;
        try
        {
            ShowLifeNotice("Transcrevendo notas de voz…");
            var (saved, left) = await backlog.RetryAsync(async (text, at) => await TrySaveQuickNoteAsync(text, at) is null);
            if (left == 0)
                ShowLifeNotice(saved == 1 ? "Nota de voz salva em " + QuickNotes.Folder + "." : saved + " notas de voz salvas em " + QuickNotes.Folder + ".", Ui.ToastKind.Success);
            else
                ShowLifeNotice((saved > 0 ? saved + " salva(s); " : "") + (left == 1 ? "1 continua guardada" : left + " continuam guardadas") + " para tentar depois.", Ui.ToastKind.Warning);
        }
        catch (Exception ex)
        {
            ShowLifeNotice("As notas de voz continuam guardadas: " + ex.Message, Ui.ToastKind.Warning);
        }
        finally { _backlogBusy = false; }
    }

    private DialogItem? QuickVoiceItem() => Voice is null ? null
        : new DialogItem("Nota rápida", UrbeTheme.Icons.Mic, "Fale e o Urbe escreve em “" + QuickNotes.Folder + "”.", Run: QuickVoiceNoteAsync);

    /// <summary>Listens, then appends what was said to today's quick note.</summary>
    public async Task QuickVoiceNoteAsync()
    {
        if (Voice is not { } voice || _voiceBusy) return;
        _voiceBusy = true;
        string? text;
        try { text = await voice(); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Nota rápida", ex.Message);
            return;
        }
        finally { _voiceBusy = false; }
        if (QuickNotes.Clean(text) is null) return;
        await SaveQuickNoteAsync(text!);
    }

    /// <summary>
    /// Appends <paramref name="text"/> to the day's note (created with its heading when missing), file first.
    /// Whatever fails, the words are shown to copy: they are never lost.
    /// </summary>
    public async Task<bool> SaveQuickNoteAsync(string text, DateTime? at = null)
    {
        if (!CanSaveQuickNote)
        {
            await KeepQuickNoteAsync(text, "O vault está só para leitura. Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para guardar notas rápidas.");
            return false;
        }
        if (await TrySaveQuickNoteAsync(text, at) is not { } error) return true;
        await KeepQuickNoteAsync(text, "Não foi possível gravar: " + error);
        return false;
    }

    /// <summary>True when a quick note can be written now (a writable vault).</summary>
    public bool CanSaveQuickNote => _library is not null && VaultWritable && _vault is not null;

    /// <summary>
    /// Appends the entry with the verified write and no dialog: null when written, else why not (the caller keeps
    /// the words). Used by the widget's voice notes (UC-66), which must never interrupt (UX P3).
    /// </summary>
    public async Task<string?> TrySaveQuickNoteAsync(string text, DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        if (QuickNotes.Clean(text) is null) return "Texto vazio.";
        if (!CanSaveQuickNote || _store is null) return "O vault está só para leitura.";
        var path = QuickNotes.PathFor(now);
        try
        {
            bool editing = Editor.IsVisible && Editor.Current?.Path == path;
            if (editing && Editor.HasChanges && !await Editor.SaveAsync())
                throw new InvalidOperationException("A nota de hoje está aberta com alterações que não foram salvas.");
            var doc = _store.Get(path);
            string? existing = doc is null ? null : _contents.GetValueOrDefault(path, doc.Content);
            var made = QuickNotes.Append(existing, text, now)!.Value;
            if (doc is not null) await SaveNoteAsync(new CityNote(path, made.Content));
            else await CreateQuickNoteFileAsync(path, made.Content);
            if (editing) Editor.Open(new CityNote(path, made.Content));
            NotesChanged();
            RefreshNotes();
            ShowLifeNotice("Nota rápida salva em " + path + ".");
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }

    private async Task CreateQuickNoteFileAsync(string path, string content)
    {
        if (_contents.ContainsKey(path)) throw new InvalidOperationException("Já existe " + path + ".");
        await EnsureFolderAsync(DirOf(path));
        await _vault!.CreateTextFileAsync(path, content);
        _library!.AddNote(path, content, Now, "quick.note");
        _contents[path] = content;
    }

    /// <summary>The words that could not be written, in a box to copy.</summary>
    private async Task KeepQuickNoteAsync(string text, string reason)
    {
        var kept = await Dialogs.PromptAsync(new PromptOptions("Nota rápida não salva", Label: "O que você disse",
            Value: QuickNotes.Clean(text) ?? text, Hint: reason, Confirm: "Copiar texto", Cancel: "Fechar", Required: false));
        if (kept is null) return;
        if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) { ShowLifeNotice("Este aparelho não deixou copiar."); return; }
        await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, kept);
        ShowLifeNotice("Texto copiado.");
    }
}
