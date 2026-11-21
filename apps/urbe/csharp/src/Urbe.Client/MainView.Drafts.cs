using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-144: the text typed in a note and not saved yet is kept in the app's private folder a moment after each pause,
/// off the UI thread; if the app ends before the autosave (a crash, the system closing it), opening the note again
/// offers that text back. Nothing is written in the vault and nothing is replaced without the person choosing it.
/// </summary>
public sealed partial class MainView
{
    private static readonly TimeSpan DraftPause = TimeSpan.FromMilliseconds(300);
    private readonly DispatcherTimer _draftTimer = new() { Interval = DraftPause };
    private NoteDraft? _draftPending;
    private Task _draftWrites = Task.CompletedTask;

    /// <summary>The drafts' folder: next to the app's local state; null (no drafts) in previews and tests without it.</summary>
    private NoteDraftStore? Drafts => UrbeApp.LocalStatePath is { } state && Path.GetDirectoryName(state) is { } dir
        ? _draftStore ??= new NoteDraftStore(Path.Combine(dir, "rascunhos")) : null;
    private NoteDraftStore? _draftStore;

    private string? DraftVault => _vault?.AtlasVaultSessionKey is { } identity ? ReaderSessionCatalog.VaultKey(identity) : null;

    /// <summary>The drafts written so far (tests and the app leaving wait for them).</summary>
    public Task DraftWrites => _draftWrites;

    private void WireDrafts()
    {
        _draftTimer.Tick += (_, _) => WriteDraftNow();
        Editor.DraftChanged += (path, saved, text) =>
        {
            if (Drafts is null || DraftVault is not { } vault) return;
            if (text is null)
            {
                if (_draftPending?.Path == path) { _draftTimer.Stop(); _draftPending = null; }
                var store = Drafts;
                _draftWrites = _draftWrites.ContinueWith(_ => { try { store.Clear(vault, path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }, TaskScheduler.Default);
                return;
            }
            if (_draftPending is { } other && other.Path != path) WriteDraftNow();   // another note's pending draft goes first
            _draftPending = new NoteDraft(vault, path, saved, text, DateTimeOffset.Now);
            _draftTimer.Stop();
            _draftTimer.Start();
        };
        Editor.Shown += path => _ = OfferDraftAsync(path);
    }

    private void WriteDraftNow()
    {
        _draftTimer.Stop();
        if (_draftPending is not { } draft || Drafts is not { } store) return;
        _draftPending = null;
        _draftWrites = _draftWrites.ContinueWith(_ => { try { store.Save(draft); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }, TaskScheduler.Default);
    }

    private async Task OfferDraftAsync(string path)
    {
        if (Drafts is not { } store || DraftVault is not { } vault) return;
        await _draftWrites;
        var draft = await Task.Run(() => store.Get(vault, path));
        // the note is shown first; the offer comes right after, about this same note only
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (draft is null || Editor.Current is not { } note || note.Path != path) return;
        if (!NoteDraftStore.Offers(draft, note.Content)) { _ = Task.Run(() => store.Clear(vault, path)); return; }
        var changed = NoteDraftStore.Changed(draft, note.Content);
        var recover = await Dialogs.ConfirmAsync("Texto não salvo",
            "Esta nota tem um texto digitado às " + draft.At.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) +
            " que não chegou a ser salvo (o app foi fechado antes)." +
            (changed ? "\n\nA nota também mudou depois disso; recuperar põe aquele texto no lugar do atual, e você pode desfazer." : ""),
            "Recuperar", cancel: "Descartar");
        if (Editor.Current?.Path != path) return;
        if (recover) Editor.RestoreText(draft.Text);
        else await Task.Run(() => store.Clear(vault, path));
    }
}
