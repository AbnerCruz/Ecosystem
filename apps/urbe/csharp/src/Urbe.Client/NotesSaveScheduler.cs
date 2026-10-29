namespace Urbe.Client;

/// <summary>
/// When .urbe/notas.json (the notes' ids, dates and folders, ADD-0028) is written, with the 1.8.4's persistence timing:
/// <list type="bullet">
/// <item>marcarSinc: a change waits 120 ms (coalescing), then the save is asked for (schedule);</item>
/// <item>workspace.js schedule: the first request starts a 900 ms timer that later requests do NOT restart; flush then
/// asks for the current record and writes it only when its text differs from what is on disk; a request while a write
/// runs makes flush loop again;</item>
/// <item>workspace.js flushPending (pagehide/visibilitychange hidden): a pending save is written at once.</item>
/// </list>
/// The text comes from <c>produce</c> on the caller's thread; null means the save is refused and nothing is written.
/// </summary>
public sealed class NotesSaveScheduler
{
    public static readonly TimeSpan SyncDelay = TimeSpan.FromMilliseconds(120);
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(900);

    private readonly Func<string?> _produce;
    private readonly Func<string, Task> _write;
    private readonly Func<TimeSpan, Task> _delay;
    private string? _onDisk;
    private bool _syncTimer, _timer, _pending, _busy, _stopped;
    private int _timerGeneration;

    /// <param name="produce">The current record, or null when it must not be written.</param>
    /// <param name="write">Writes it (IUrbeVaultStorage.SaveNotesRecordAsync).</param>
    /// <param name="onDisk">The text read at the open (workspace snapshot), or null.</param>
    public NotesSaveScheduler(Func<string?> produce, Func<string, Task> write, string? onDisk, Func<TimeSpan, Task>? delay = null)
    {
        _produce = produce ?? throw new ArgumentNullException(nameof(produce));
        _write = write ?? throw new ArgumentNullException(nameof(write));
        _onDisk = onDisk;
        _delay = delay ?? (t => Task.Delay(t));
    }

    /// <summary>workspace.js state: idle, dirty, saving, saved, refused or error.</summary>
    public string State { get; private set; } = "idle";

    /// <summary>The last error or refusal, for the person.</summary>
    public string? Problem { get; private set; }

    public event EventHandler? StateChanged;

    /// <summary>The record as the Urbe last read or wrote it (null before the first).</summary>
    public string? OnDisk => _onDisk;

    /// <summary>A save is waiting (the 120 ms or 900 ms timer, or a request during a write).</summary>
    public bool HasPending => _syncTimer || _timer || _pending;

    /// <summary>A save is being written.</summary>
    public bool IsBusy => _busy;

    /// <summary>marcarSinc(): agendarSalvar after a change of the notes.</summary>
    public async void MarkSync()
    {
        if (_stopped || _syncTimer) return;
        _syncTimer = true;
        await _delay(SyncDelay);
        _syncTimer = false;
        if (!_stopped) Schedule();
    }

    /// <summary>workspace.js schedule(): document:created/updated/removed.</summary>
    public async void Schedule()
    {
        if (_stopped) return;
        _pending = true;
        SetState("dirty");
        if (_timer) return;
        _timer = true;
        int generation = ++_timerGeneration;
        await _delay(SaveDelay);
        if (!_timer || generation != _timerGeneration) return; // flushed earlier
        _timer = false;
        await FlushCoreAsync(swallow: true);
    }

    /// <summary>flushPending(): write what is waiting now (app closing or going to the background).</summary>
    public Task FlushAsync()
    {
        if (!HasPending) return Task.CompletedTask;
        _timer = false;
        _timerGeneration++;
        _pending = true;
        return FlushCoreAsync(swallow: false);
    }

    /// <summary>The vault was closed or replaced: nothing more is written for it.</summary>
    public void Stop() => _stopped = true;

    private async Task FlushCoreAsync(bool swallow)
    {
        if (_stopped) return;
        if (_busy) { _pending = true; return; }
        _busy = true;
        _pending = true;
        try
        {
            while (_pending)
            {
                _pending = false;
                SetState("saving");
                var text = _produce();
                if (text is null) { SetState("refused"); return; }
                if (text != _onDisk)
                {
                    await _write(text);
                    _onDisk = text;
                }
                Problem = null;
            }
            SetState("saved");
        }
        catch (Exception ex)
        {
            Problem = ex.Message;
            SetState("error");
            if (!swallow) throw;
        }
        finally { _busy = false; }
    }

    /// <summary>Records why the record was not written (produce returned null).</summary>
    public void Refused(string reason) => Problem = reason;

    private void SetState(string state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
