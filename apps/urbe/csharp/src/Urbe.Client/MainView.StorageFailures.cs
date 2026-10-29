using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// CST-004/007: a write the storage reports as failed is checked on the disk. A phone's folder can write the bytes and
/// still lose the answer: then it happened, and the app goes on as if the call had worked (the same note, its id kept)
/// instead of showing a screen the disk no longer matches, where every new try stops at "já existe". When it really
/// did not happen, the person is told what failed, in their words, and can try again; nothing changed.
/// </summary>
public sealed partial class MainView
{
    /// <summary>The text the vault holds at <paramref name="path"/> now; null when there is no such file (or it cannot be read).</summary>
    private async Task<string?> OnDiskAsync(string path)
    {
        if (_vault is null) return null;
        try
        {
            return await _vault.ReadFileAsync(path, long.MaxValue) is { Bytes: { } bytes }
                ? System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿') : null;
        }
        catch (Exception) { return null; }
    }

    /// <summary>The trash on the disk holds the document <paramref name="id"/>.</summary>
    private async Task<bool> TrashOnDiskHoldsAsync(string id) => (await OnDiskAsync(LegacyTrash.Path))?.Contains("\"" + id + "\"", StringComparison.Ordinal) == true;

    /// <summary>
    /// Runs <paramref name="write"/>. When it fails, <paramref name="happened"/> looks at the disk: if the write is there,
    /// it counts as done. Otherwise "<paramref name="failure"/>" says why, with Tentar de novo. False when the person closes it.
    /// </summary>
    private async Task<bool> WriteOrAskAsync(string failure, Func<Task> write, Func<Task<bool>> happened)
    {
        while (true)
        {
            try
            {
                await write();
                return true;
            }
            catch (Exception error)
            {
                if (await happened()) return true;
                if (!await Dialogs.ConfirmAsync(failure, StorageProblems.Explain(error) + "\n\nNada foi alterado no vault.", "Tentar de novo", cancel: "Fechar"))
                    return false;
            }
        }
    }

    /// <summary>
    /// Applies <see cref="InterruptedWrites.Plan"/>: the previous text goes back into an empty note, or next to a note
    /// that holds another one; a copy is removed only once its text is read back in a note. A step that fails leaves
    /// the copy, for the next opening. Returns what to tell the person, or null when nothing they wrote changed.
    /// </summary>
    private async Task<string?> RecoverInterruptedWritesAsync(VaultSnapshot snapshot)
    {
        // the copies are not text the vault reads: they are read here, next to the notes they belong to
        var files = new Dictionary<string, VaultFile>(snapshot.Files, StringComparer.OrdinalIgnoreCase);
        foreach (var copy in _attachments.Where(InterruptedWrites.IsCopy).ToList())
            if (await _vault!.ReadFileAsync(copy, long.MaxValue) is { Bytes: { } bytes }) files[copy] = new VaultFile(copy, bytes);
        var plan = InterruptedWrites.Plan(files);
        var restored = new List<string>();
        var kept = new List<(string Note, string Copy)>();
        foreach (var recovery in plan)
        {
            try
            {
                var into = recovery.Target;
                if (recovery.Resolution == InterruptedWrites.Resolution.Restore)
                {
                    if (await OnDiskAsync(recovery.Target) is null) await _vault!.CreateTextFileAsync(recovery.Target, recovery.Text);
                    else await _vault!.SaveExistingTextFileAsync(recovery.Target, recovery.Text);
                }
                else if (recovery.Resolution == InterruptedWrites.Resolution.KeepBoth)
                {
                    into = recovery.KeepAs!;
                    await _vault!.CreateTextFileAsync(into, recovery.Text);
                }
                if (await OnDiskAsync(into) != recovery.Text) continue;
                await _vault!.DeleteFileAsync(recovery.Copy);
                if (recovery.Resolution == InterruptedWrites.Resolution.Restore) restored.Add(NoteLibrary.FullNoteName(recovery.Target));
                else if (recovery.Resolution == InterruptedWrites.Resolution.KeepBoth)
                    kept.Add((NoteLibrary.FullNoteName(recovery.Target), NoteLibrary.FullNoteName(into)));
            }
            catch (Exception) { /* the copy stays where it is: nothing is lost, the next opening tries again */ }
        }
        if (restored.Count == 0 && kept.Count == 0) return null;
        var told = "O Urbe fechou no meio de uma gravação.";
        if (restored.Count > 0) told += " " + string.Join(", ", restored.Select(n => "“" + n + "”")) + (restored.Count == 1 ? " voltou" : " voltaram") + " ao texto completo de antes.";
        foreach (var (note, copy) in kept) told += " “" + note + "”: as duas versões ficaram, a anterior em “" + copy + "”.";
        return told;
    }
}
