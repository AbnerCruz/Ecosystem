using System.Globalization;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// "Versões anteriores" (history.js + urbeVersoes, 1.8.4): every change of a note keeps its previous text — except
/// changes less than 5 s after the last (typing) — up to 40 per note, in .urbe/history.v2.json; a note sent to the
/// trash keeps its last text. The editor's Mais ações lists them and restores one after confirming.
/// </summary>
public sealed partial class MainView
{
    private LegacyHistory _history = new();
    private bool _historyReadOnly, _historyDirty;
    private string? _historyNote, _historyText;

    /// <summary>When a note was created (ms), for the block times of the blocks the whole history already had.</summary>
    private static long? CreatedMs(UrbeDocument doc) =>
        DateTimeOffset.TryParse(doc.Created, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at.ToUnixTimeMilliseconds() : null;

    /// <summary>The history as it is now (tests).</summary>
    public LegacyHistory History => _history;

    private void LoadHistory(Func<string, string?> text)
    {
        _history = LegacyHistory.Read(text(LegacyHistory.Path), text(LegacyHistory.PathV1), out _historyReadOnly);
        _historyDirty = false;
        _historyNote = _historyText = null;
    }

    /// <summary>legacy.documents syncBuilding('editor.input') → document:updated → history.capture, on every change.</summary>
    private void CaptureEditorHistory()
    {
        if (Editor.Current is not { } note) return;
        var id = _store?.Get(note.Path)?.Id;
        if (id is null) return;
        if (_historyNote != note.Path)
        {
            _historyNote = note.Path;   // the note just opened: its text is the one to keep at its first change
            _historyText = note.Content;
            return;
        }
        var before = _historyText ?? "";
        _historyText = note.Content;
        if (!_historyReadOnly && _history.Capture(id, note.Path, before, note.Content, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
            _historyDirty = true;
    }

    /// <summary>workspace.js flush writes the history with the notes: after a save, the versions kept since.</summary>
    private async Task SaveHistoryAsync()
    {
        if (!_historyDirty || _historyReadOnly || _vault is null || !VaultWritable) return;
        try
        {
            await _vault.SaveHistoryAsync(_history.Export());
            _historyDirty = false;
        }
        catch (Exception) { /* kept dirty: the next save tries again; the note itself is already saved */ }
    }

    /// <summary>captureRemoved: the trashed note's last text.</summary>
    private async Task CaptureRemovedAsync(UrbeDocument doc)
    {
        CaptureRemoved(doc);
        await SaveHistoryAsync();
    }

    /// <summary>captureRemoved in memory; the history is written by the next save.</summary>
    private void CaptureRemoved(UrbeDocument doc)
    {
        if (_historyReadOnly) return;
        _history.CaptureRemoved(doc, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        _historyDirty = true;
    }

    /// <summary>
    /// urbeVersoes: "Versões anteriores" with each version ("Hoje às 14:05" or the date, and its first 70 characters);
    /// "Restaurar esta versão?" with up to 600 characters; then the text goes back into the editor ("Versão restaurada").
    /// </summary>
    public async Task ShowVersionsAsync()
    {
        if (Editor.Current is not { } note || _store?.Get(note.Path) is not { } doc) return;
        var versions = _history.List(doc.Id);
        if (versions.Count == 0)
        {
            await Dialogs.AlertAsync("Versões anteriores", "Esta nota ainda não tem versões guardadas. A cada mudança o Urbe guarda a versão anterior (até 40 por nota).");
            return;
        }
        static string When(long ms)
        {
            var t = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
            var day = t.Date == DateTime.Today ? "Hoje" : t.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            return day + " às " + t.ToString("HH:mm", CultureInfo.InvariantCulture);
        }
        var options = versions.Select(v =>
        {
            var text = System.Text.RegularExpressions.Regex.Replace(v.Content, @"\s+", " ").Trim();
            return new DialogItem(When(v.Timestamp), UrbeTheme.Icons.Clock, text.Length > 70 ? text[..70] + "…" : text.Length > 0 ? text : "(vazia)");
        }).ToList();
        var i = await Dialogs.ChooseAsync("Versões anteriores", options,
            "Escolha uma versão para ver e restaurar. A versão atual também fica guardada, então dá para voltar.");
        if (i is not { } index) return;
        var chosen = versions[index];
        var preview = chosen.Content.Length > 600 ? chosen.Content[..600] + "…" : chosen.Content;
        if (!await Dialogs.ConfirmAsync("Restaurar esta versão?", When(chosen.Timestamp) + "\n\n" + preview, "Restaurar")) return;
        // hist.restore → docs.upsert({...d, content}) → loadFile: the editor shows it and saves it as any change
        if (Editor.Current?.Path == note.Path) Editor.Editor.Text = chosen.Content;
        ShowLifeNotice("Versão restaurada");
    }
}
