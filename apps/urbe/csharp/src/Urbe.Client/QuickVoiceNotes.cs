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

    internal static bool TakePending() => Interlocked.Exchange(ref _pending, 0) == 1;
}

/// <summary>UC-56: "Nota rápida" by voice, appended to the day's note in "Notas rápidas" with the verified write.</summary>
public sealed partial class MainView
{
    private Func<Task<string?>>? _voice;
    private bool _voiceBusy;

    /// <summary>The transcriber used here: <see cref="PlatformVoice.Transcribe"/> unless replaced (tests).</summary>
    public Func<Task<string?>>? Voice { get => _voice ?? PlatformVoice.Transcribe; set => _voice = value; }

    /// <summary>True once the first city (vault or Tutorial) is shown.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Called when the first city is up: a widget request made while starting runs now.</summary>
    private void MarkReady()
    {
        IsReady = true;
        if (PlatformVoice.TakePending()) Dispatcher.UIThread.Post(() => _ = QuickVoiceNoteAsync());
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
        var now = at ?? DateTime.Now;
        if (World.City is null || _store is null || !VaultWritable || _vault is null)
        {
            await KeepQuickNoteAsync(text, "O vault está só para leitura. Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para guardar notas rápidas.");
            return false;
        }
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
            ScheduleRoadRebuild();
            _mapSave?.Schedule();
            _mapSave?.MarkSync();
            RefreshNotes();
            World.InvalidateVisual();
            ShowLifeNotice("Nota rápida salva em " + path + ".");
            return true;
        }
        catch (Exception ex)
        {
            await KeepQuickNoteAsync(text, "Não foi possível gravar: " + ex.Message);
            return false;
        }
    }

    private async Task CreateQuickNoteFileAsync(string path, string content)
    {
        if (_contents.ContainsKey(path)) throw new InvalidOperationException("Já existe " + path + ".");
        await EnsureFolderAsync(FolderOf(path));
        await _vault!.CreateTextFileAsync(path, content);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var today = LegacyCity.NowDate(now);
        var doc = _store!.Upsert(new DocumentInput { Path = path, Content = content, Created = today, Modified = today }, "quick.note");
        World.City!.AddDocumentHouse(doc, now);
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
