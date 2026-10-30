using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// Importar (app.js v21ImportPicker + importarEntradasDestino) and the backups of the vault: documents are written
/// (and verified) into the vault one by one; a vault, an export or a backup is restored through the ImportTransaction,
/// which keeps a copy of everything it replaces first. Nothing is kept in memory only: without a writable vault folder
/// these actions refuse, like "Nova nota".
/// </summary>
public sealed partial class MainView
{
    private const string NoVaultMessage = "Abra a pasta do seu vault (Configurações → Pasta no dispositivo) para criar pastas e importar.";

    /// <summary>Replaces the system picker (tests, other platforms): true = a whole folder, false = files.</summary>
    public Func<bool, Task<ImportSelection?>>? ImportPicker { get; set; }

    private bool VaultWritable => _vault is { IsConnected: true } && _snapshot is { IsReadOnly: false };

    private bool _importBusy;

    /// <summary>v21ImportPicker + importarEntradasDestino for documents: choose files or a folder, check,
    /// confirm, then every new text document is written (and verified) into the vault.</summary>
    public async Task ImportAsync(string? folder = null)
    {
        if (_library is null) return;
        if (_importBusy)
        {
            ShowLifeNotice("Uma importação está em andamento. Aguarde o resultado.");
            return;
        }
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Importar", NoVaultMessage);
            return;
        }
        _importBusy = true;
        try
        {
            var mode = await Dialogs.ChooseAsync("O que deseja importar?",
            [
                new DialogItem("Arquivos", UrbeTheme.Icons.Upload, "Selecione um ou vários arquivos. O Urbe identifica automaticamente o conteúdo."),
                new DialogItem("Pasta inteira", UrbeTheme.Icons.Folder, "Selecione um vault ou uma pasta com seus documentos.")
            ]);
            if (mode is null) return;
            ImportSelection? selection;
            try { selection = await PickImportAsync(mode == 1); }
            catch (Exception ex)
            {
                await Dialogs.AlertAsync("Não foi possível abrir a seleção", StorageProblems.Explain(ex));
                return;
            }
            if (selection is null) return;
            await ImportSelectionAsync(selection, folder);
        }
        finally { _importBusy = false; }
    }

    /// <summary>The checked selection into the vault (everything after the system picker).</summary>
    public async Task ImportSelectionAsync(ImportSelection selection, string? folder = null)
    {
        if (selection.Sources.Count == 0)
        {
            await Dialogs.AlertAsync("Nenhum arquivo encontrado", "A seleção está vazia. Escolha uma pasta com documentos ou um backup.");
            return;
        }
        ImportInspection inspection;
        try
        {
            ShowLifeNotice("Conferindo os arquivos…");
            inspection = await ImportPipeline.InspectAsync(selection);
            if (inspection.SourceConflicts is { Count: > 0 } conflicts)
            {
                var chosen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var conflict in conflicts)
                {
                    var pick = await Dialogs.ChooseAsync("Arquivos com o mesmo nome",
                        conflict.Candidates.Select((c, i) => new DialogItem("Versão " + (i + 1) + " — " + c.Path, Detail: c.Bytes.Length + " bytes")).ToList(),
                        conflict.Path + " tem versões diferentes na seleção. Escolha qual importar ou cancele para selecionar novamente.");
                    if (pick is null)
                    {
                        ShowLifeNotice("Importação cancelada.");
                        return;
                    }
                    chosen[conflict.Path] = pick.Value;
                }
                inspection = ImportPipeline.ResolveSources(inspection, chosen);
            }
        }
        catch (InvalidDataException ex)
        {
            await Dialogs.AlertAsync("Não foi possível importar", StorageProblems.Explain(ex));
            return;
        }
        if (inspection.Kind != ImportKind.Documents)
        {
            await RestoreSelectionAsync(inspection);
            return;
        }

        var plan = ImportDocuments.Plan(inspection, _contents, folder is { Length: > 0 } ? folder : null, _attachments);
        var description = $"{inspection.DocumentCount} documento(s) e {inspection.AssetCount} arquivo(s). Os documentos serão incorporados ao vault atual, mantendo suas pastas e referências."
            + (plan.Attachments.Count > 0 ? $" {plan.Attachments.Count} anexo(s) (imagens, PDFs, áudio, vídeo) vêm junto." : "")
            + (plan.Skipped.Count > 0 ? $" {plan.Skipped.Count} arquivo(s) de tipos que o Urbe não grava (ou metadados) ficam de fora." : "")
            + (plan.Kept.Count > 0 ? $" {plan.Kept.Count} arquivo(s) já existem com conteúdo diferente e serão mantidos como estão." : "");
        if (await Dialogs.ChooseAsync("Documentos para incorporar", [new DialogItem("Continuar")], description) is null)
        {
            ShowLifeNotice("Importação cancelada.");
            return;
        }
        int written = 0, total = plan.New.Count + plan.Attachments.Count;
        try
        {
            foreach (var (path, content) in plan.New)
            {
                if (written % 10 == 0) ShowLifeNotice($"Importando {written} de {total}…");
                await WriteVaultFileAsync(path, content, exists: false);
                written++;
            }
            foreach (var (path, bytes) in plan.Attachments)
            {
                if (written % 10 == 0) ShowLifeNotice($"Importando {written} de {total}…");
                await EnsureFolderAsync(DirOf(path));
                await _vault!.CreateBinaryFileAsync(path, bytes);
                _attachments.Add(path);
                written++;
            }
            if (plan.Attachments.Count > 0) RefreshNotes();
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível importar", $"{written} de {total} arquivo(s) foram gravados antes do erro: {StorageProblems.Explain(ex)}");
            if (plan.Attachments.Count > 0) RefreshNotes();
            return;
        }
        ShowLifeNotice($"Importação concluída. {written} arquivo(s) gravado(s); {plan.Unchanged} preservado(s), sem nova gravação.");
    }

    /// <summary>
    /// app.js importarEntradasDestino for a vault, an export or a backup: confirm, write the pending note, the notes'
    /// record and history, stop the record's saves, plan against the vault as it is on disk, confirm the replacements
    /// ("Restaurar com backup"), run the ImportTransaction (every replaced or removed file is copied to
    /// .urbe/backup/import-… first, with the recovery journal) and reopen the vault. On a failure the journal is
    /// recovered and the vault reopened.
    /// </summary>
    private async Task RestoreSelectionAsync(ImportInspection inspection)
    {
        if (_vault?.ImportStorage is not { } storage || _snapshot is null)
        {
            await Dialogs.AlertAsync("Não foi possível importar", NoVaultMessage);
            return;
        }
        if (_snapshot.IsReadOnly || _snapshot.IsMapReadOnly || _snapshot.FutureFiles.Count > 0)
        {
            await Dialogs.AlertAsync("Não foi possível importar", "O vault atual está protegido. Nenhum dado será alterado.");
            return;
        }
        if (!await Dialogs.ConfirmAsync("Vault para restauração", $"{inspection.DocumentCount} documento(s) e {inspection.AssetCount} arquivo(s). O vault de origem será restaurado com suas notas e metadados. Um backup do vault atual será preservado.", "Continuar"))
        {
            ShowLifeNotice("Importação cancelada.");
            return;
        }
        bool suspended = false;
        try
        {
            // rodarSinc(true) + persistence.flush + suspend: what is pending goes to the vault, then nothing else writes
            if (Editor.HasChanges && !await Editor.SaveAsync()) throw new IOException("A nota aberta não pôde ser salva. Nenhum dado foi alterado.");
            await SaveHistoryAsync();
            if (_notesSave is { } saves)
            {
                await saves.FlushAsync();
                saves.Stop();
                _notesSave = null;
            }
            suspended = true;
            foreach (var tab in Editor.Tabs.ToList()) await Editor.CloseTabAsync(tab);
            var files = new List<VaultFile>();
            foreach (var path in await storage.ListAsync(CancellationToken.None))
                if (await storage.ReadAsync(path, CancellationToken.None) is { } bytes) files.Add(new VaultFile(path, bytes.ToArray()));
            var current = VaultReader.Read(files);
            var revision = ImportTransaction.Revision(files);
            var plan = ImportPipeline.Plan(inspection, current, revision);
            if (plan.Conflicts.Count > 0)
            {
                if (!await Dialogs.ConfirmAsync("Restaurar este vault?", $"A restauração substituirá {plan.Conflicts.Count} arquivo(s) do vault atual. Eles serão guardados em backup antes de qualquer alteração.", "Restaurar com backup"))
                {
                    ShowLifeNotice("Importação cancelada.");
                    return;
                }
                plan = ImportPipeline.Plan(inspection, current, revision, plan.Conflicts.ToDictionary(c => c.Path, _ => ImportConflictChoice.Replace, StringComparer.OrdinalIgnoreCase));
            }
            ShowLifeNotice("Importando…");
            var result = await ImportPipeline.ExecuteAsync(plan, new ImportTransaction(storage));
            suspended = false;
            await ReopenVaultAsync();
            await ApplyBackupPreferencesAsync(inspection);
            ShowLifeNotice($"Vault restaurado. {result.Written} arquivo(s) gravado(s); {result.Unchanged} preservado(s), sem nova gravação.");
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível importar", ex.Message.Length > 0 ? StorageProblems.Explain(ex) : "A seleção não pôde ser lida. O vault atual foi preservado.");
        }
        finally
        {
            if (suspended)
            {
                try
                {
                    await new ImportTransaction(storage).RecoverAsync();
                    await ReopenVaultAsync();
                }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
                {
                    ShowLifeNotice("O vault aguarda recuperação. Preserve o backup e reabra o aplicativo.");
                }
            }
        }
    }

    /// <summary>
    /// workspace.backups + workspace.restoreBackup (backup.js list/restore): the backups kept in .urbe/backup (before a
    /// migration, a reorganization or an import), newest first; the one chosen is restored through the same import
    /// transaction, so what it replaces is itself kept in a new backup first.
    /// </summary>
    public async Task ShowBackupsAsync()
    {
        if (_vault?.ImportStorage is not { } storage || !VaultWritable)
        {
            await Dialogs.AlertAsync("Backups do vault", NoVaultMessage);
            return;
        }
        var token = CancellationToken.None;
        var paths = await storage.ListAsync(token);
        var backups = new List<(string Dir, string At, string Detail)>();
        foreach (var path in paths)
        {
            var match = System.Text.RegularExpressions.Regex.Match(path, @"^\.urbe/backup/([^/]+)/manifest\.json$");
            if (!match.Success || await storage.ReadAsync(path, token) is not { } bytes) continue;
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(bytes);
                var root = json.RootElement;
                string at = root.TryGetProperty("at", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.String ? a.GetString()! : "";
                string from = root.TryGetProperty("from", out var fr) ? fr.ToString() : "";
                int count = root.TryGetProperty("files", out var f) && f.ValueKind == System.Text.Json.JsonValueKind.Array ? f.GetArrayLength()
                    : root.TryGetProperty("journal", out var j) && j.TryGetProperty("entries", out var e) && e.ValueKind == System.Text.Json.JsonValueKind.Array ? e.GetArrayLength() : 0;
                var dir = ".urbe/backup/" + match.Groups[1].Value;
                if (at.Length == 0) at = match.Groups[1].Value;
                var label = root.TryGetProperty("kind", out _) ? "Antes de importar" : from == "layout" ? "Antes de reorganizar a cidade" : from.Length > 0 ? "Antes de migrar o formato" : "Backup";
                backups.Add((dir, at, label + " · " + count + " arquivo(s)"));
            }
            catch (System.Text.Json.JsonException) { /* an unreadable manifest is not offered */ }
        }
        if (backups.Count == 0)
        {
            ShowLifeNotice("Nenhum backup neste vault. O Urbe guarda um backup em .urbe/backup antes de migrar ou importar.");
            return;
        }
        backups.Sort((x, y) => string.CompareOrdinal(y.At, x.At));
        static string When(string at) => DateTimeOffset.TryParse(at, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
            ? t.ToLocalTime().ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) : at;
        var pick = await Dialogs.ChooseAsync("Backups do vault", backups.Select(b => new DialogItem(When(b.At).Length > 0 ? When(b.At) : b.Dir, Detail: b.Detail)).ToList(),
            "Escolha um backup para restaurar. O que ele substituir será guardado antes em outro backup.");
        if (pick is null) return;
        var chosen = backups[pick.Value].Dir + "/";
        var sources = new List<ImportSource>();
        foreach (var path in paths.Where(p => p.StartsWith(chosen, StringComparison.Ordinal)))
        {
            var bytes = await storage.ReadAsync(path, token);
            if (bytes is null) continue;
            var copy = bytes.Value.ToArray();
            sources.Add(new ImportSource(path[chosen.Length..], _ => ValueTask.FromResult<Stream>(new MemoryStream(copy, writable: false))));
        }
        ImportInspection inspection;
        try { inspection = await ImportPipeline.InspectAsync(new ImportSelection(sources, IsDirectory: true)); }
        catch (InvalidDataException ex)
        {
            await Dialogs.AlertAsync("Não foi possível restaurar", StorageProblems.Explain(ex));
            return;
        }
        await RestoreSelectionAsync(inspection);
    }

    /// <summary>
    /// "Preferências do backup" (importarEntradasDestino + UrbeExportManifest.applyState): an export carries this device's
    /// allowed preferences; applied only when the person says so, because plugin approvals come with them.
    /// </summary>
    private async Task ApplyBackupPreferencesAsync(ImportInspection inspection)
    {
        if (inspection.Manifest?.State is not { } state || state.LocalStorage.Count == 0 && (state.Plugins?.Count ?? 0) == 0) return;
        if (!await Dialogs.ConfirmAsync("Preferências do backup",
                "Aplicar as preferências deste backup? Permissões de plugins incluídas também serão autorizadas neste aparelho; mantenha as atuais se não confiar na origem.",
                "Aplicar preferências", cancel: "Manter atuais"))
            return;
        foreach (var (key, value) in VaultExportStatePolicy.Apply(Customization.Device.All(), state, "Urbe").Writes)
            Customization.Device.Set(key, value);
        SyncPlugins();
    }

    /// <summary>abrirCidade(Disco.cidade): the same folder read again from disk.</summary>
    private async Task ReopenVaultAsync()
    {
        if (await _vault!.ReloadAsync() is { } snapshot) await ShowVaultAsync(snapshot);
    }

    private async Task<ImportSelection?> PickImportAsync(bool folder)
    {
        if (ImportPicker is not null) return await ImportPicker(folder);
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return null;
        var sources = new List<ImportSource>();
        if (folder)
        {
            var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Importar pasta", AllowMultiple = false });
            if (picked.Count == 0) return null;
            async Task Walk(IStorageFolder dir, string prefix, int depth)
            {
                if (depth > 32) throw new IOException("A pasta ultrapassa 32 níveis.");
                await foreach (var item in dir.GetItemsAsync())
                {
                    if (item is IStorageFolder sub) await Walk(sub, prefix + item.Name + "/", depth + 1);
                    else if (item is IStorageFile file) sources.Add(new ImportSource(prefix + file.Name, async _ => await file.OpenReadAsync()));
                }
            }
            await Walk(picked[0], "", 0);
        }
        else
        {
            var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Importar arquivos", AllowMultiple = true });
            foreach (var file in picked) sources.Add(new ImportSource(file.Name, async _ => await file.OpenReadAsync()));
        }
        return sources.Count == 0 ? null : new ImportSelection(sources, folder);
    }
}

/// <summary>What a documents import will do to the current vault (importarEntradasDestino for text files).</summary>
public sealed record ImportDocumentsPlan(IReadOnlyList<(string Path, string Content)> New, IReadOnlyList<string> Kept,
    IReadOnlyList<string> Skipped, int Unchanged)
{
    /// <summary>UC-63: the new attachments (pictures, PDFs, audio, video) to create beside the documents.</summary>
    public IReadOnlyList<(string Path, byte[] Bytes)> Attachments { get; init; } = [];
}

public static class ImportDocuments
{
    /// <summary>The selection's text documents (under the target folder when one is chosen) against the
    /// vault's: new ones are written, identical ones are left alone, different ones are KEPT as they are
    /// (the native vault has no backup-and-replace yet); attachments the app writes (pictures, PDFs, audio, video) are
    /// created when new and kept when a file is already there; other files and hidden metadata are skipped.</summary>
    public static ImportDocumentsPlan Plan(ImportInspection inspection, IReadOnlyDictionary<string, string> current, string? targetFolder,
        IReadOnlySet<string>? attachments = null)
    {
        var files = new HashSet<string>(attachments ?? new HashSet<string>(), StringComparer.OrdinalIgnoreCase);
        var made = new List<(string, byte[])>();
        var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in current) existing[pair.Key] = pair.Value;
        var added = new List<(string, string)>();
        var kept = new List<string>();
        var skipped = new List<string>();
        int unchanged = 0;
        foreach (var file in inspection.Files.Values.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var path = string.IsNullOrEmpty(targetFolder) ? file.Path : targetFolder + "/" + file.Path;
            if (path.Split('/').Any(s => s.StartsWith('.'))) { skipped.Add(path); continue; }
            if (IUrbeVaultStorage.BinaryExtensions.Contains(System.IO.Path.GetExtension(path)))
            {
                if (files.Contains(path) || existing.ContainsKey(path)) kept.Add(path);
                else if (file.Bytes.Length > 32L * 1024 * 1024) skipped.Add(path);
                else { made.Add((path, file.Bytes.ToArray())); files.Add(path); }
                continue;
            }
            if (!IUrbeVaultStorage.WritableExtensions.Contains(System.IO.Path.GetExtension(path)))
            {
                skipped.Add(path);
                continue;
            }
            var text = new UTF8Encoding(false, true).GetString(file.Bytes.Span);
            if (existing.TryGetValue(path, out var old))
            {
                if (old == text) unchanged++; else kept.Add(path);
            }
            else added.Add((path, text));
        }
        return new ImportDocumentsPlan(added, kept, skipped, unchanged) { Attachments = made };
    }

}
