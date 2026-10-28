using Urbe.Core;

namespace Urbe.Client;

/// <summary>Native-platform vault boundary. The Core remains the canonical parser/writer;
/// clients receive immutable snapshots, never filesystem or Android APIs.</summary>
public interface IUrbeVaultStorage
{
    bool IsConnected { get; }
    /// <summary>Opaque native-only identity of the currently selected vault; never passed to HTML.</summary>
    string? AtlasVaultSessionKey => null;
    Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default);
    Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default);
    /// <summary>First safe slice: update one EXISTING Markdown file, with conflict checks,
    /// recovery copy, byte verification and no modifications to other vault entries.</summary>
    Task SaveExistingNoteAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Creates ONE new user text file (a new note, a Personalização JSON) that does not
    /// exist yet, with the same checks; folders on its path are created when missing.</summary>
    Task CreateTextFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-63: creates a file that is not text (see <see cref="EnsureWritableBinaryPath"/>) with exactly these bytes,
    /// never over an existing file, and reads it back. The text files the vault verifies are not touched.
    /// </summary>
    Task CreateBinaryFileAsync(string path, byte[] bytes, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento não grava arquivos que não são texto.");

    /// <summary>Writes a user-provided attachment byte-for-byte, text or binary, using the existing verified vault writer.</summary>
    Task CreateImportedFileAsync(string path, byte[] bytes, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento não importa arquivos da conversa.");

    /// <summary>Rewrites ONE existing user text file of any editable text type (e.g. tema.json).</summary>
    Task SaveExistingTextFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Atomic compare-and-write for one existing user text file. The SHA-256 comparison is inside the writer lock.</summary>
    Task SaveExistingTextFileIfUnchangedAsync(string path, string content, byte[] expectedSha256, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este vault não oferece gravação condicional segura.");

    /// <summary>Creates ONE user folder (a "Nova pasta"), and the folders above it, inside
    /// the vault. Never inside .urbe/ or any hidden folder; an existing folder is accepted, a file with
    /// that name is refused. No file of the vault is touched. Hosts must override it.</summary>
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não cria pastas.");

    /// <summary>The name of the vault's folder (Disco.cidade in the 1.8.4: the brand shows it), or null.</summary>
    string? VaultName => null;

    /// <summary>
    /// The vault as the import/restore transaction sees it (ImportTransaction: exclusive writer, every file listed,
    /// read and written byte for byte, removed); null when the host cannot restore. While the transaction holds it no
    /// other write runs, and afterwards the host's view of the folder is refreshed.
    /// </summary>
    IImportStorage? ImportStorage => null;

    /// <summary>abrirCidade(Disco.cidade): the folder already open read again from disk (after a restore); null when none is open.</summary>
    Task<VaultSnapshot?> ReloadAsync(CancellationToken cancellationToken = default) => Task.FromResult<VaultSnapshot?>(null);

    /// <summary>
    /// Every file of the vault as it is on disk now, notes and attachments alike (exportarVault reads them all for the
    /// .zip), without following links out of the folder and within the same limits as the open. Read only: nothing is
    /// written. Hosts must override it.
    /// </summary>
    Task<IReadOnlyList<VaultFile>> ReadAllFilesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não exporta o vault.");

    /// <summary>
    /// The paths of every file of the vault, attachments included, without reading them (the Explorer lists every file
    /// with its extension, DEC-0045, UC-38). Hidden entries (".urbe", ".git"…) and links out of the folder are left out.
    /// Read only. A host that cannot list returns none.
    /// </summary>
    Task<IReadOnlyList<string>> ListFilePathsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    /// <summary>
    /// Renames or moves ONE attachment of the vault (renomearAsset / moverAsset): a file that is not a note or text
    /// document the Urbe reads (those move with <see cref="MoveTextFileAsync"/>), never inside <c>.urbe</c>. Nothing with
    /// the new name may exist; the folder of <paramref name="to"/> must exist; afterwards the file is there with the same
    /// size and gone from where it was, and the notes are untouched. A host that cannot move files throws.
    /// </summary>
    Task MoveFileAsync(string from, string to, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não move anexos.");

    /// <summary>
    /// Deletes ONE attachment of the vault (removerAsset: "O arquivo será apagado do vault", after the person confirms).
    /// Only a file that is not a note or text document the Urbe reads, never inside <c>.urbe</c>; the notes are
    /// untouched. A host that cannot delete files throws.
    /// </summary>
    Task DeleteFileAsync(string path, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não exclui anexos.");

    /// <summary>An attachment path the Urbe may move or delete: safe, outside .urbe, and not a text document it reads.</summary>
    static void EnsureAttachmentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Contains(':'))
            throw new InvalidDataException("Caminho inválido no vault: " + path);
        var parts = path.Split('/');
        if (parts.Any(s => s.Length == 0 || s is "." or ".." || s.StartsWith('.') || s.EndsWith(' ')))
            throw new InvalidOperationException("Caminho interno ou inseguro no vault: " + path);
        if (TextDocument(path)) throw new InvalidOperationException("Notas e documentos de texto não são anexos: " + path);
    }

    /// <summary>The documents the Urbe reads as text (they belong to the vault's baseline, not to the attachments).</summary>
    static bool TextDocument(string path) =>
        new[] { ".md", ".markdown", ".txt", ".json", ".yaml", ".yml", ".html", ".htm", ".css", ".js", ".mjs", ".csv" }
            .Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Renames ONE folder of the vault (Renomear pasta in the Explorer), keeping it in
    /// the same parent: <paramref name="from"/> and <paramref name="to"/> differ only in the last name. With the same
    /// safeguards as moving a note: nothing changed outside the Urbe since the open, nothing with the new name already
    /// there, and afterwards every file read back identical under the new path. A host that cannot rename throws.
    /// </summary>
    Task RenameFolderAsync(string from, string to, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não renomeia pastas.");

    /// <summary>The parent of both paths must be the same, and both must be safe folder paths.</summary>
    static void EnsureFolderRename(string from, string to)
    {
        EnsureWritableFolderPath(from);
        EnsureWritableFolderPath(to);
        static string Parent(string p) => p.Contains('/') ? p[..p.LastIndexOf('/')] : "";
        if (!string.Equals(Parent(from), Parent(to), StringComparison.Ordinal))
            throw new InvalidOperationException("Renomear pasta não muda a pasta de lugar: " + from + " → " + to);
        if (string.Equals(from, to, StringComparison.Ordinal)) throw new InvalidOperationException("O nome é o mesmo: " + from);
    }

    /// <summary>The baseline after a folder rename: every file under <paramref name="from"/> must now be under
    /// <paramref name="to"/> with the same bytes, and nothing else may have changed.</summary>
    static void EnsureRenamed(IReadOnlyDictionary<string, VaultFile> baseline, IReadOnlyDictionary<string, VaultFile> observed, string from, string to)
    {
        if (observed.Count != baseline.Count) throw new IOException("Verificação pós-gravação encontrou arquivos inesperados.");
        foreach (var pair in baseline)
        {
            var key = pair.Key.StartsWith(from + "/", StringComparison.OrdinalIgnoreCase) ? to + pair.Key[from.Length..] : pair.Key;
            if (!observed.TryGetValue(key, out var actual) || !actual.Bytes.Span.SequenceEqual(pair.Value.Bytes.Span))
                throw new IOException("Verificação pós-gravação detectou divergência em: " + key);
        }
    }

    /// <summary>
    /// One file of the vault for its preview (openFilePreview → blobDoArquivo): its size and, when it is at most
    /// <paramref name="maxBytes"/>, its bytes. Null when the file is not there (the 1.8.4's "Arquivo não encontrado").
    /// Read only; never follows a link out of the folder. A host that cannot read files returns null.
    /// </summary>
    Task<VaultFileRead?> ReadFileAsync(string path, long maxBytes, CancellationToken cancellationToken = default) =>
        Task.FromResult<VaultFileRead?>(null);

    /// <summary>
    /// Opens one file of the vault in another app of the device (the 1.8.4's "Abrir" of a PDF; audio and video too,
    /// which the native app does not play). Nothing is written. False when the host cannot or the file is not there.
    /// </summary>
    Task<bool> OpenExternallyAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <summary>Removes ONE user folder when it is empty (the 1.8.4 sync's FS.apagarPasta after a folder is deleted in
    /// Notas: removeEntry of a folder that still holds anything fails and is ignored). A folder holding any entry, a
    /// link, a file with that name or a missing folder is left as it is and false returned; nothing else is touched.</summary>
    Task<bool> RemoveEmptyDirectoryAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <summary>
    /// Moves ONE existing user text file to <paramref name="to"/>, its bytes unchanged (a note renamed: the
    /// 1.8.4 writes the new path and removes the old one in its flush). Never over another file; a change
    /// of case only is done through a temporary name. The new file is written and verified before the old
    /// one is removed, so an interruption leaves a copy, never a loss; every file of the vault is verified
    /// afterwards. Hosts must override it.
    /// </summary>
    Task MoveTextFileAsync(string from, string to, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não move arquivos.");

    /// <summary>
    /// The checks of a move on these files: both paths writable text files, <paramref name="from"/> a
    /// readable document of a vault that may be written, <paramref name="to"/> free (or the same file
    /// with another case). Returns the source path as it is on disk and its exact bytes.
    /// </summary>
    static (string From, byte[] Bytes, bool CaseOnly) PlanMove(IReadOnlyList<VaultFile> files, string from, string to)
    {
        ArgumentNullException.ThrowIfNull(files);
        EnsureWritableTextPath(from);
        EnsureWritableTextPath(to);
        if (string.Equals(from, to, StringComparison.Ordinal))
            throw new InvalidOperationException("O arquivo já tem esse nome: " + to);
        var snapshot = VaultReader.Read(files);
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal ||
            snapshot.FutureFiles.Any(f => string.Equals(f, from, StringComparison.OrdinalIgnoreCase) || string.Equals(f, to, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Formato futuro de vault, arquivo protegido ou operação interrompida: escrita recusada.");
        var source = files.SingleOrDefault(f => string.Equals(f.Path, from, StringComparison.Ordinal))
                     ?? throw new FileNotFoundException("O arquivo não existe na pasta.", from);
        if (snapshot.Documents.SingleOrDefault(d => string.Equals(d.Path, from, StringComparison.Ordinal))?.Text is null)
            throw new InvalidDataException("O arquivo não pôde ser lido como texto seguro.");
        bool caseOnly = string.Equals(from, to, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && files.Any(f => string.Equals(f.Path, to, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Já existe um arquivo com esse nome: " + to);
        return (source.Path, source.Bytes.ToArray(), caseOnly);
    }

    /// <summary>
    /// Moves ONE user text file to the trash like the 1.8.4 flush after trash.trash: .urbe/trash.v2.json is written
    /// (<paramref name="trashJson"/>, which must hold that file's document with its current text) and verified FIRST,
    /// then the file is removed and every file of the vault is verified. A trash of another version or a vault that
    /// may not be written is refused. Hosts must override it.
    /// </summary>
    Task TrashTextFileAsync(string path, string trashJson, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não usa a lixeira.");

    /// <summary>
    /// The checks of a move to the trash on these files: <paramref name="path"/> a readable text document of a vault
    /// that may be written, the trash file not of a future/unknown version, and <paramref name="trashJson"/> a version 2
    /// trash with an item for that path holding exactly its text. Returns the file's path on disk, the trash file's
    /// path (its case on disk, or the default) and the trash bytes.
    /// </summary>
    static (string Path, string TrashPath, byte[] TrashBytes) PlanTrash(IReadOnlyList<VaultFile> files, string path, string trashJson)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(trashJson);
        EnsureWritableTextPath(path);
        var snapshot = VaultReader.Read(files);
        var trashPath = files.FirstOrDefault(f => string.Equals(f.Path, LegacyTrash.Path, StringComparison.OrdinalIgnoreCase))?.Path ?? LegacyTrash.Path;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal ||
            snapshot.FutureFiles.Any(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase) || string.Equals(f, trashPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Formato futuro de vault, lixeira protegida ou operação interrompida: exclusão recusada.");
        var source = files.SingleOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal))
                     ?? throw new FileNotFoundException("O arquivo não existe na pasta.", path);
        var text = snapshot.Documents.SingleOrDefault(d => string.Equals(d.Path, path, StringComparison.Ordinal))?.Text
                   ?? throw new InvalidDataException("O arquivo não pôde ser lido como texto seguro.");
        System.Text.Json.Nodes.JsonObject? root;
        try { root = System.Text.Json.Nodes.JsonNode.Parse(trashJson) as System.Text.Json.Nodes.JsonObject; }
        catch (System.Text.Json.JsonException) { root = null; }
        bool held = root?["version"]?.GetValue<int>() == 2 && root["items"] is System.Text.Json.Nodes.JsonArray items &&
                    items.Any(i => i?["document"] is System.Text.Json.Nodes.JsonObject d &&
                                   (string?)d["path"] == path && (string?)d["content"] == text);
        if (!held)
            throw new InvalidDataException("A lixeira não guarda o texto atual do arquivo: exclusão recusada.");
        return (source.Path, trashPath, System.Text.Encoding.UTF8.GetBytes(trashJson));
    }

    /// <summary>
    /// UC-65: moves MANY user text files to the trash at once (a folder, a selection): .urbe/trash.v2.json, holding each
    /// of them with its current text, is written and verified FIRST, then the files are removed and every file of the
    /// vault is verified, once for all of them. The same refusals as <see cref="TrashTextFileAsync"/>. A host without
    /// it moves them one by one.
    /// </summary>
    async Task TrashTextFilesAsync(IReadOnlyList<string> paths, string trashJson, CancellationToken cancellationToken = default)
    {
        foreach (var path in paths) await TrashTextFileAsync(path, trashJson, cancellationToken);
    }

    /// <summary>
    /// The checks of <see cref="TrashTextFilesAsync"/> on these files, read once: every path a readable text document
    /// of a vault that may be written, listed once, and held by <paramref name="trashJson"/> with exactly its text.
    /// Returns the files' paths on disk, the trash file's path and the trash bytes.
    /// </summary>
    static (IReadOnlyList<string> Paths, string TrashPath, byte[] TrashBytes) PlanTrashMany(IReadOnlyList<VaultFile> files, IReadOnlyList<string> paths, string trashJson)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(trashJson);
        if (paths.Count == 0) throw new ArgumentException("Nenhum arquivo para a lixeira.", nameof(paths));
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Count) throw new ArgumentException("Um arquivo aparece duas vezes.", nameof(paths));
        foreach (var path in paths) EnsureWritableTextPath(path);
        var snapshot = VaultReader.Read(files);
        var trashPath = files.FirstOrDefault(f => string.Equals(f.Path, LegacyTrash.Path, StringComparison.OrdinalIgnoreCase))?.Path ?? LegacyTrash.Path;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal ||
            snapshot.FutureFiles.Any(f => string.Equals(f, trashPath, StringComparison.OrdinalIgnoreCase) || paths.Contains(f, StringComparer.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Formato futuro de vault, lixeira protegida ou operação interrompida: exclusão recusada.");
        System.Text.Json.Nodes.JsonObject? root;
        try { root = System.Text.Json.Nodes.JsonNode.Parse(trashJson) as System.Text.Json.Nodes.JsonObject; }
        catch (System.Text.Json.JsonException) { root = null; }
        if (root?["version"]?.GetValue<int>() != 2 || root["items"] is not System.Text.Json.Nodes.JsonArray items)
            throw new InvalidDataException("A lixeira não guarda o texto atual dos arquivos: exclusão recusada.");
        var held = new HashSet<(string, string)>();
        foreach (var i in items)
            if (i?["document"] is System.Text.Json.Nodes.JsonObject d && (string?)d["path"] is { } p && (string?)d["content"] is { } c) held.Add((p, c));
        var byPath = files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var texts = snapshot.Documents.Where(d => d.Text is not null).ToDictionary(d => d.Path, d => d.Text!, StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (!byPath.ContainsKey(path)) throw new FileNotFoundException("O arquivo não existe na pasta.", path);
            if (!texts.TryGetValue(path, out var text)) throw new InvalidDataException("O arquivo não pôde ser lido como texto seguro: " + path);
            if (!held.Contains((path, text))) throw new InvalidDataException("A lixeira não guarda o texto atual de " + path + ": exclusão recusada.");
        }
        return (paths, trashPath, System.Text.Encoding.UTF8.GetBytes(trashJson));
    }

    /// <summary>
    /// Rewrites .urbe/trash.v2.json alone (a note restored, an item excluded for good, the trash emptied), with the same
    /// safeguards as a note: nothing changed outside Urbe, a trash of another version or a vault that may not be
    /// written is never rewritten, the bytes are verified and every file of the vault too. Hosts must override it.
    /// </summary>
    Task SaveTrashAsync(string trashJson, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não usa a lixeira.");

    /// <summary>The checks of a trash rewrite on these files; returns the trash file's path (its case on disk) and bytes.</summary>
    static (string TrashPath, byte[] TrashBytes) PlanTrashWrite(IReadOnlyList<VaultFile> files, string trashJson)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(trashJson);
        var snapshot = VaultReader.Read(files);
        var trashPath = files.FirstOrDefault(f => string.Equals(f.Path, LegacyTrash.Path, StringComparison.OrdinalIgnoreCase))?.Path ?? LegacyTrash.Path;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal ||
            snapshot.FutureFiles.Any(f => string.Equals(f, trashPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Formato futuro de vault, lixeira protegida ou operação interrompida: escrita recusada.");
        LegacyTrash.Read(trashJson, null, out var unreadable);
        if (unreadable) throw new InvalidDataException("A lixeira a gravar não é da versão 2.");
        return (trashPath, System.Text.Encoding.UTF8.GetBytes(trashJson));
    }

    /// <summary>
    /// Writes .urbe/tutorial.json (tutorial.js marcar: {"versao","em"}), the mark that the Tutorial was already created in
    /// this vault, with the same safeguards and verification as the trash. Hosts must override it.
    /// </summary>
    Task SaveTutorialMarkAsync(string json, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não grava a marca do Tutorial.");

    /// <summary>
    /// Rewrites .urbe/history.v2.json (Versões anteriores, history.js) alone, with the trash's safeguards: a history
    /// of another version, a protected vault or an interrupted operation is never written. Hosts must override it.
    /// </summary>
    Task SaveHistoryAsync(string historyJson, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não grava as versões anteriores.");

    /// <summary>
    /// Rewrites .urbe/identity.json (identity.js: {version:1, docs:{id:{path, fingerprint, seen}}}), the derived record
    /// that lets a note renamed outside the app keep its id, with the trash's safeguards. Hosts must override it.
    /// </summary>
    Task SaveIdentityAsync(byte[] identityJson, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não grava a identidade das notas.");

    /// <summary>
    /// Appends a maintenance record to .urbe/vault.json (P.recordMaintenance: {kind, at, …}, the last 20 kept), with the
    /// trash's safeguards: a vault.json of a newer format, a protected vault or an interrupted operation is never written.
    /// </summary>
    Task SaveMaintenanceAsync(System.Text.Json.Nodes.JsonObject maintenance, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não registra manutenções.");

    static (string Path, byte[] Bytes) PlanMaintenanceWrite(IReadOnlyList<VaultFile> files, System.Text.Json.Nodes.JsonObject maintenance)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(maintenance);
        const string meta = ".urbe/vault.json";
        var snapshot = VaultReader.Read(files);
        var current = files.FirstOrDefault(f => string.Equals(f.Path, meta, StringComparison.OrdinalIgnoreCase));
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal || snapshot.FormatState is VaultFormatState.Future or VaultFormatState.Corrupt ||
            snapshot.FutureFiles.Any(f => string.Equals(f, meta, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Formato futuro de vault ou operação interrompida: escrita recusada.");
        return (current?.Path ?? meta, VaultWriter.SetVaultMaintenance(current?.Bytes, "urbe-native-preview", DateTimeOffset.UtcNow, (System.Text.Json.Nodes.JsonObject)maintenance.DeepClone()));
    }

    /// <summary>The checks of an identity rewrite: never over a sidecar of a newer version, a protected vault or an interrupted operation.</summary>
    static (string Path, byte[] Bytes) PlanIdentityWrite(IReadOnlyList<VaultFile> files, byte[] identityJson)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(identityJson);
        var snapshot = VaultReader.Read(files);
        var path = files.FirstOrDefault(f => string.Equals(f.Path, VaultIdentity.Path, StringComparison.OrdinalIgnoreCase))?.Path ?? VaultIdentity.Path;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal ||
            snapshot.FutureFiles.Any(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Formato futuro de vault, identidade protegida ou operação interrompida: escrita recusada.");
        if (VaultIdentity.Parse(identityJson, out _) is null) throw new InvalidDataException("Identidade das notas inválida.");
        return (path, identityJson);
    }

    /// <summary>The checks of a history rewrite on these files: version 2 JSON, never over a file the 1.8.4 would keep.</summary>
    static (string Path, byte[] Bytes) PlanHistoryWrite(IReadOnlyList<VaultFile> files, string historyJson)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(historyJson);
        var snapshot = VaultReader.Read(files);
        var path = files.FirstOrDefault(f => string.Equals(f.Path, LegacyHistory.Path, StringComparison.OrdinalIgnoreCase))?.Path ?? LegacyHistory.Path;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal ||
            snapshot.FutureFiles.Any(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Formato futuro de vault, histórico protegido ou operação interrompida: escrita recusada.");
        if (files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)) is { } current)
        {
            LegacyHistory.Read(System.Text.Encoding.UTF8.GetString(current.Bytes.Span).TrimStart('\uFEFF'), null, out var foreign);
            if (foreign) throw new InvalidOperationException("O histórico do vault é de outra versão: escrita recusada.");
        }
        LegacyHistory.Read(historyJson, null, out var unreadable);
        if (unreadable) throw new InvalidDataException("O histórico a gravar não é da versão 2.");
        return (path, System.Text.Encoding.UTF8.GetBytes(historyJson));
    }

    /// <summary>The checks of the Tutorial's mark on these files: a JSON object with "versao", never on a protected vault.</summary>
    static (string Path, byte[] Bytes) PlanTutorialMarkWrite(IReadOnlyList<VaultFile> files, string json)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(json);
        const string mark = ".urbe/tutorial.json";
        var snapshot = VaultReader.Read(files);
        var path = files.FirstOrDefault(f => string.Equals(f.Path, mark, StringComparison.OrdinalIgnoreCase))?.Path ?? mark;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal ||
            snapshot.FutureFiles.Any(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Formato futuro de vault ou operação interrompida: escrita recusada.");
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object || !doc.RootElement.TryGetProperty("versao", out _))
                throw new InvalidDataException("Marca do Tutorial inválida.");
        }
        catch (System.Text.Json.JsonException) { throw new InvalidDataException("Marca do Tutorial inválida."); }
        return (path, System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Rewrites .urbe/notas.json (the notes' ids, dates and folders: NoteLibrary, ADD-0028) alone, with the trash's
    /// safeguards: a record of a newer version, a protected vault or an interrupted operation is never written, the bytes
    /// are verified and every file of the vault too. Hosts must override it.
    /// </summary>
    Task SaveNotesRecordAsync(string json, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não grava o registro das notas.");

    /// <summary>The checks of a notas.json rewrite: a record of the version this app knows, never over a newer file.</summary>
    static (string Path, byte[] Bytes) PlanNotesRecordWrite(IReadOnlyList<VaultFile> files, string json)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(json);
        var snapshot = VaultReader.Read(files);
        var existing = files.FirstOrDefault(f => string.Equals(f.Path, NoteLibrary.Path, StringComparison.OrdinalIgnoreCase));
        var path = existing?.Path ?? NoteLibrary.Path;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal)
            throw new InvalidOperationException("Vault protegido ou operação interrompida: escrita recusada.");
        if (existing is not null && NoteLibrary.IsFuture(System.Text.Encoding.UTF8.GetString(existing.Bytes.Span)))
            throw new InvalidOperationException("O registro das notas é de uma versão mais nova do Urbe: escrita recusada.");
        if (!NoteLibrary.IsRecord(json)) throw new InvalidDataException("notas.json inválido.");
        return (path, System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Rewrites .urbe/area.json (UC-85: how the boxes of the Área de trabalho look and are ordered) alone, with the
    /// trash's safeguards: a file of a newer version, a protected vault or an interrupted operation is never written, the
    /// bytes are verified and every file of the vault too. Hosts must override it.
    /// </summary>
    Task SaveAreaLayoutAsync(string json, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não grava a aparência das caixas.");

    /// <summary>The checks of an area.json rewrite: a layout of the version this app knows, never over a newer or unreadable file.</summary>
    static (string Path, byte[] Bytes) PlanAreaLayoutWrite(IReadOnlyList<VaultFile> files, string json)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(json);
        var snapshot = VaultReader.Read(files);
        var existing = files.FirstOrDefault(f => string.Equals(f.Path, AreaLayout.Path, StringComparison.OrdinalIgnoreCase));
        var path = existing?.Path ?? AreaLayout.Path;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal)
            throw new InvalidOperationException("Vault protegido ou operação interrompida: escrita recusada.");
        if (existing is not null && !AreaLayout.IsLayout(System.Text.Encoding.UTF8.GetString(existing.Bytes.Span).TrimStart('\uFEFF')))
            throw new InvalidOperationException("A aparência das caixas é de uma versão mais nova do Urbe: escrita recusada.");
        if (!AreaLayout.IsLayout(json)) throw new InvalidDataException("area.json inválido.");
        return (path, System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Rewrites .urbe/constelacao.json (UC-72: the Constelação's positions, derived from the notes) alone, with the same
    /// safeguards as area.json. Hosts must override it.
    /// </summary>
    Task SaveConstellationAsync(string json, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não grava a Constelação.");

    /// <summary>The checks of a constelacao.json rewrite: a file of the version this app knows, never over a newer or unreadable one.</summary>
    static (string Path, byte[] Bytes) PlanConstellationWrite(IReadOnlyList<VaultFile> files, string json)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(json);
        var snapshot = VaultReader.Read(files);
        var existing = files.FirstOrDefault(f => string.Equals(f.Path, Constellation.Path, StringComparison.OrdinalIgnoreCase));
        var path = existing?.Path ?? Constellation.Path;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal)
            throw new InvalidOperationException("Vault protegido ou operação interrompida: escrita recusada.");
        if (existing is not null && !Constellation.IsConstellation(System.Text.Encoding.UTF8.GetString(existing.Bytes.Span).TrimStart('\uFEFF')))
            throw new InvalidOperationException("A Constelação é de uma versão mais nova do Urbe: escrita recusada.");
        if (!Constellation.IsConstellation(json)) throw new InvalidDataException("constelacao.json inválido.");
        return (path, System.Text.Encoding.UTF8.GetBytes(json));
    }

    /// <summary>A folder path Urbe may create: vault-relative, no traversal, drive syntax, or hidden segment.</summary>
    static void EnsureWritableFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Contains(':'))
            throw new InvalidDataException("Caminho inválido no vault: " + path);
        if (path.Split('/').Any(s => s.Length == 0 || s is "." or ".." || s.StartsWith('.') || s.EndsWith(' ') || s.EndsWith('.')))
            throw new InvalidOperationException("Caminho interno ou inseguro no vault: " + path);
    }

    /// <summary>The user files Urbe may write: the 1.8.4 text types, inside the vault, never in
    /// .urbe/ or any hidden folder, never with traversal or drive syntax.</summary>
    static void EnsureWritableTextPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Contains(':'))
            throw new InvalidDataException("Caminho inválido no vault: " + path);
        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || s is "." or ".." || s.StartsWith('.')))
            throw new InvalidOperationException("Caminho interno ou inseguro no vault: " + path);
        if (!WritableExtensions.Contains(Path.GetExtension(path)))
            throw new NotSupportedException("Tipo de arquivo que o Urbe não grava: " + path);
    }

    /// <summary>
    /// The files that are not text Urbe may create (UC-63: an image the Assistente made; an attachment imported):
    /// pictures, PDFs, audio and video, inside the vault, never in .urbe/ or any hidden folder.
    /// </summary>
    static void EnsureWritableBinaryPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Contains(':'))
            throw new InvalidDataException("Caminho inválido no vault: " + path);
        if (path.Split('/').Any(s => s.Length == 0 || s is "." or ".." || s.StartsWith('.') || s.EndsWith(' ') || s.EndsWith('.')))
            throw new InvalidOperationException("Caminho interno ou inseguro no vault: " + path);
        if (!BinaryExtensions.Contains(Path.GetExtension(path)))
            throw new NotSupportedException("Tipo de arquivo que o Urbe não grava: " + path);
    }

    /// <summary>Use the existing text/binary allowlists and hidden-path protections without decoding the original bytes.</summary>
    static void EnsureWritableImportedPath(string path)
    {
        if (WritableExtensions.Contains(Path.GetExtension(path))) EnsureWritableTextPath(path);
        else EnsureWritableBinaryPath(path);
    }

    static readonly HashSet<string> BinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".pdf", ".mp3", ".m4a", ".ogg", ".oga", ".wav", ".flac", ".mp4", ".webm", ".mov"
    };

    static readonly HashSet<string> WritableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".html", ".htm", ".js", ".mjs", ".css", ".json", ".yaml", ".yml", ".csv"
    };
}

/// <summary>A file read for its preview: its size, and its bytes when they were within the limit asked.</summary>
public sealed record VaultFileRead(long Size, byte[]? Bytes);
