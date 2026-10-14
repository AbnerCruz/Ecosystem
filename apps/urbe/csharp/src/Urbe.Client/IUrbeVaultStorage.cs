using Urbe.Core;

namespace Urbe.Client;

/// <summary>Native-platform vault boundary. The Core remains the canonical parser/writer;
/// clients receive immutable snapshots, never filesystem or Android APIs.</summary>
public interface IUrbeVaultStorage
{
    bool IsConnected { get; }
    Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default);
    Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default);
    /// <summary>First safe slice: update one EXISTING Markdown file, with conflict checks,
    /// recovery copy, byte verification and no modifications to other vault entries.</summary>
    Task SaveExistingNoteAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Creates ONE new user text file (a new note, a Personalização JSON) that does not
    /// exist yet, with the same checks; folders on its path are created when missing.</summary>
    Task CreateTextFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Rewrites ONE existing user text file of any editable text type (e.g. tema.json).</summary>
    Task SaveExistingTextFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Creates ONE user folder (a "Nova pasta" / drawn region), and the folders above it, inside
    /// the vault. Never inside .urbe/ or any hidden folder; an existing folder is accepted, a file with
    /// that name is refused. No file of the vault is touched. Hosts must override it.</summary>
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não cria pastas.");

    /// <summary>The name of the vault's folder (Disco.cidade in the 1.8.4: the brand shows it), or null.</summary>
    string? VaultName => null;

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
    /// Rewrites .urbe/trash.v2.json alone (a note restored, an item excluded for good, the trash emptied), with the same
    /// safeguards as the map: nothing changed outside Urbe, a trash of another version or a vault that may not be
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
    /// Rewrites .urbe/mundo.json (what the native app adds to the city, DEC-0045, UC-41) alone, with the trash's
    /// safeguards. A file of a newer version is never written over. Hosts must override it.
    /// </summary>
    Task SaveWorldAsync(string json, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não grava o mundo.");

    /// <summary>The checks of a mundo.json rewrite: a JSON object with "versao" this app knows, never over a newer file.</summary>
    static (string Path, byte[] Bytes) PlanWorldWrite(IReadOnlyList<VaultFile> files, string json)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(json);
        var snapshot = VaultReader.Read(files);
        var existing = files.FirstOrDefault(f => string.Equals(f.Path, WorldPrefs.FilePath, StringComparison.OrdinalIgnoreCase));
        var path = existing?.Path ?? WorldPrefs.FilePath;
        if (snapshot.IsReadOnly || snapshot.RecoveredFromJournal)
            throw new InvalidOperationException("Vault protegido ou operação interrompida: escrita recusada.");
        if (existing is not null)
        {
            WorldPrefs.Parse(System.Text.Encoding.UTF8.GetString(existing.Bytes.Span), out var newer);
            if (newer) throw new InvalidOperationException("O mundo desta cidade é de uma versão mais nova do Urbe: escrita recusada.");
        }
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("versao", out var v) || v.GetInt32() != WorldPrefs.Version)
                throw new InvalidDataException("mundo.json inválido.");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or InvalidOperationException)
        { throw new InvalidDataException("mundo.json inválido."); }
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

    /// <summary>
    /// Writes .urbe/mapa.json, and only it (workspace.js is its single writer in 1.8.4, REQ-040),
    /// with the same safeguards as a note: nothing changed outside Urbe since the open, a map of a
    /// future/unknown format or a read-only vault is never rewritten, the canonical writer
    /// (VaultWriter.Plan with MetadataJson) must produce exactly <paramref name="json"/>, a recovery
    /// copy is kept while replacing, and every file of the vault is verified afterwards.
    /// </summary>
    Task SaveMapAsync(string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// The bytes of .urbe/mapa.json the canonical writer plans for <paramref name="json"/> on these
    /// files, refusing what the 1.8.4 refuses (vault.json of a future format, mapa.json of a future or
    /// unknown version). Returns the existing path of the map (its case on disk) or the default path.
    /// </summary>
    static (string Path, byte[] Bytes) PlanMapWrite(IReadOnlyList<VaultFile> files, string json)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(json);
        var snapshot = VaultReader.Read(files);
        if (snapshot.IsReadOnly)
            throw new InvalidOperationException("Formato futuro de vault: escrita recusada.");
        if (snapshot.IsMapReadOnly || snapshot.FutureFiles.Contains(LegacyCityMap.Path, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("O mapa da cidade é de um formato futuro ou desconhecido: escrita recusada.");
        // The 1.8.4 applies an interrupted operation's journal as a whole (every file) before anything
        // else; writing only the map over it is not that, so it is refused.
        if (snapshot.RecoveredFromJournal)
            throw new InvalidOperationException("Há uma operação interrompida (diário de recuperação) neste vault: escrita do mapa recusada.");
        var documents = snapshot.Documents.Where(d => d.Text is not null)
            .Select(d => new VaultWriteDocument(d.Id, d.Path, d.Text!))
            .ToList();
        var plan = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = documents,
            MetadataJson = json,
            AppVersion = "urbe-native-preview"
        });
        var path = files.FirstOrDefault(f => string.Equals(f.Path, LegacyCityMap.Path, StringComparison.OrdinalIgnoreCase))?.Path
                   ?? LegacyCityMap.Path;
        if (!plan.Writable || !plan.Files.TryGetValue(path, out var planned))
            throw new InvalidOperationException("O plano canônico recusou o mapa.");
        var bytes = planned.Bytes.ToArray();
        if (!bytes.AsSpan().SequenceEqual(System.Text.Encoding.UTF8.GetBytes(json)))
            throw new InvalidDataException("O plano canônico modificaria o mapa inesperadamente.");
        return (path, bytes);
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

    static readonly HashSet<string> WritableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".html", ".htm", ".js", ".mjs", ".css", ".json", ".yaml", ".yml", ".csv"
    };
}

/// <summary>A file read for its preview: its size, and its bytes when they were within the limit asked.</summary>
public sealed record VaultFileRead(long Size, byte[]? Bytes);
