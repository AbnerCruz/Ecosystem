using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// A vault in a local folder of the computer (Windows/desktop head). The same rules as the
/// Android SAF host: the user's folder is the authority (no hidden copy of notes), the
/// selection is remembered, reads are bounded, and a save only rewrites ONE existing Markdown
/// note after checking that nothing changed outside Urbe, that the canonical writer agrees,
/// with a recovery copy beside it, and verifying every file of the vault afterwards.
/// </summary>
public sealed class FolderVaultStorage : IUrbeVaultStorage
{
    private const long MaxFileBytes = 32L * 1024 * 1024;
    private const long MaxVaultBytes = 256L * 1024 * 1024;
    private const int MaxEntries = 10000;
    private static readonly string[] Included =
        [".md", ".markdown", ".txt", ".json", ".yaml", ".yml", ".html", ".htm", ".css", ".js", ".mjs", ".csv"];

    private readonly Func<CancellationToken, Task<string?>> _pickFolder;
    private readonly string _selectionFile;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _root;
    private IReadOnlyDictionary<string, VaultFile>? _baseline;

    /// <param name="pickFolder">Asks the person for a folder (native picker); null when cancelled.</param>
    /// <param name="selectionFile">Where the chosen folder is remembered (app data, not the vault).</param>
    public FolderVaultStorage(Func<CancellationToken, Task<string?>> pickFolder, string selectionFile)
    {
        _pickFolder = pickFolder ?? throw new ArgumentNullException(nameof(pickFolder));
        _selectionFile = selectionFile ?? throw new ArgumentNullException(nameof(selectionFile));
    }

    public bool IsConnected => _root is not null && _baseline is not null;

    /// <summary>The connected folder, if any.</summary>
    public string? Folder => _root;

    public async Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default)
    {
        var selected = await _pickFolder(cancellationToken);
        if (string.IsNullOrWhiteSpace(selected)) return null;
        var snapshot = await OpenAsync(Path.GetFullPath(selected), cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_selectionFile))!);
        await File.WriteAllTextAsync(_selectionFile, JsonSerializer.Serialize(new Selection(_root!)), cancellationToken);
        return snapshot;
    }

    public async Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected || !File.Exists(_selectionFile)) return null;
        Selection? stored;
        try { stored = JsonSerializer.Deserialize<Selection>(await File.ReadAllTextAsync(_selectionFile, cancellationToken)); }
        catch (JsonException) { return null; }
        if (stored is null || string.IsNullOrWhiteSpace(stored.Folder)) return null;
        if (!Directory.Exists(stored.Folder))
            throw new DirectoryNotFoundException("A pasta do vault não existe mais: " + stored.Folder);
        return await OpenAsync(stored.Folder, cancellationToken);
    }

    private async Task<VaultSnapshot> OpenAsync(string root, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var files = await Task.Run(() => ReadFiles(root, cancellationToken), cancellationToken);
            var snapshot = VaultReader.Read(files);
            _root = root;
            _baseline = Snapshot(files);
            return snapshot;
        }
        finally { _gate.Release(); }
    }

    public Task SaveExistingNoteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        if (!IsSupportedNote(path)) throw new NotSupportedException("Esta etapa grava somente notas Markdown existentes.");
        return WriteTextFileAsync(path, content, create: false, cancellationToken);
    }

    public Task CreateTextFileAsync(string path, string content, CancellationToken cancellationToken = default) =>
        WriteTextFileAsync(path, content, create: true, cancellationToken);

    public Task SaveExistingTextFileAsync(string path, string content, CancellationToken cancellationToken = default) =>
        WriteTextFileAsync(path, content, create: false, cancellationToken);

    public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        IUrbeVaultStorage.EnsureWritableFolderPath(path);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var root = _root ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            if (_baseline is null) throw new InvalidOperationException("Reabra a pasta do vault.");
            await Task.Run(() =>
            {
                var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine([root, .. Segments(path)]));
                if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new InvalidDataException("Caminho fora do vault: " + path);
                // every step must be a real folder: never a file, never a link out of the vault
                var current = rootFull;
                foreach (var segment in Segments(path))
                {
                    current = Path.Combine(current, segment);
                    if (File.Exists(current)) throw new IOException("Já existe um arquivo com esse nome: " + path);
                    if (Directory.Exists(current) && new DirectoryInfo(current).LinkTarget is not null)
                        throw new IOException("A pasta é um atalho e não pode ser usada: " + path);
                }
                Directory.CreateDirectory(full);
                if (!Directory.Exists(full)) throw new IOException("A pasta criada não passou na verificação: " + path);
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public string? VaultName => _root is null ? null : Path.GetFileName(Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar));

    public async Task<IReadOnlyList<VaultFile>> ReadAllFilesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var root = _root ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            return await Task.Run(() => ReadFiles(root, cancellationToken, includeAll: true), cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<string>> ListFilePathsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var root = _root ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            return await Task.Run(() =>
            {
                var paths = new List<string>();
                void Walk(string dir, string prefix, int depth)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (depth > 32 || paths.Count >= MaxEntries) return;
                    foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos().OrderBy(e => e.Name, StringComparer.Ordinal))
                    {
                        if (entry.LinkTarget is not null || entry.Name.StartsWith('.')) continue;
                        if (entry is DirectoryInfo sub) Walk(sub.FullName, prefix + entry.Name + "/", depth + 1);
                        else if (paths.Count < MaxEntries) paths.Add(prefix + entry.Name);
                    }
                }
                Walk(root, string.Empty, 0);
                return (IReadOnlyList<string>)paths;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> RemoveEmptyDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        IUrbeVaultStorage.EnsureWritableFolderPath(path);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var root = _root ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            if (_baseline is null) throw new InvalidOperationException("Reabra a pasta do vault.");
            return await Task.Run(() =>
            {
                var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                var full = Path.GetFullPath(Path.Combine([root, .. Segments(path)]));
                if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return false;
                // the exact folder (same case), a real folder on every step, never a link
                var current = rootFull;
                foreach (var segment in Segments(path))
                {
                    var parent = current;
                    current = Path.Combine(current, segment);
                    if (!Directory.Exists(current) || new DirectoryInfo(current).LinkTarget is not null) return false;
                    if (!Directory.EnumerateDirectories(parent).Any(d => Path.GetFileName(d) == segment)) return false;
                }
                if (Directory.EnumerateFileSystemEntries(full).Any()) return false;
                Directory.Delete(full, recursive: false);
                return !Directory.Exists(full);
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Writes ONE user text file (a note, or a Personalização JSON): creates it when
    /// <paramref name="create"/> and it does not exist yet, else rewrites the existing one.</summary>
    private async Task WriteTextFileAsync(string path, string content, bool create, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        IUrbeVaultStorage.EnsureWritableTextPath(path);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var root = _root ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            if (create && baseline.ContainsKey(path))
                throw new IOException("Já existe um arquivo com esse nome: " + path);
            if (!create && !baseline.ContainsKey(path))
                throw new FileNotFoundException("O arquivo não existe na pasta.", path);
            var files = await Task.Run(() => ReadFiles(root, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var snapshot = VaultReader.Read(files);
            if (snapshot.IsReadOnly || snapshot.FutureFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Formato futuro de vault ou arquivo protegido: escrita recusada.");
            var existing = snapshot.Documents.SingleOrDefault(d => string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
            if (!create && existing?.Text is null)
                throw new InvalidDataException("O arquivo não pôde ser lido como texto seguro.");
            var target = existing?.Path ?? path;

            // The canonical writer must agree; only this one file is written.
            var documents = snapshot.Documents.Where(d => d.Text is not null)
                .Select(d => new VaultWriteDocument(d.Id, d.Path,
                    string.Equals(d.Path, target, StringComparison.OrdinalIgnoreCase) ? content : d.Text!))
                .ToList();
            if (create) documents.Add(new VaultWriteDocument("doc-" + Guid.NewGuid().ToString("N"), target, content));
            var plan = VaultWriter.Plan(new VaultWriteRequest { Files = files, Documents = documents, AppVersion = "urbe-native-preview" });
            if (!plan.Writable || !plan.Files.ContainsKey(target))
                throw new InvalidOperationException("O plano canônico recusou o arquivo.");
            var expected = Encoding.UTF8.GetBytes(content);
            var planned = plan.Files[target].Bytes.ToArray();
            if (!planned.AsSpan().SequenceEqual(expected) &&
                !(planned.Length == expected.Length + 3 && planned[0] == 0xef && planned[1] == 0xbb && planned[2] == 0xbf &&
                  planned.AsSpan(3).SequenceEqual(expected)))
                throw new InvalidDataException("O plano canônico modificaria o conteúdo inesperadamente.");

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteVerified(root, target, planned, create);
                var observed = Snapshot(ReadFiles(root, cancellationToken));
                foreach (var pair in baseline)
                {
                    if (!observed.TryGetValue(pair.Key, out var actual) ||
                        !actual.Bytes.Span.SequenceEqual(string.Equals(pair.Key, target, StringComparison.OrdinalIgnoreCase)
                            ? planned : pair.Value.Bytes.Span))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + pair.Key);
                }
                if (create && (!observed.TryGetValue(target, out var made) || !made.Bytes.Span.SequenceEqual(planned)))
                    throw new IOException("O arquivo criado não passou na verificação: " + target);
                if (observed.Count != baseline.Count + (create ? 1 : 0))
                    throw new IOException("Verificação pós-gravação encontrou arquivos inesperados.");
                _baseline = observed;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task MoveTextFileAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var root = _root ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(root, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var (source, bytes, caseOnly) = IUrbeVaultStorage.PlanMove(files, from, to);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceFull = Inside(root, source);
                if (caseOnly)
                {
                    // the same file on a case-insensitive disk: through a temporary copy the vault does not list
                    var temp = sourceFull + ".urbe-move-" + Guid.NewGuid().ToString("N");
                    CreateVerified(temp, bytes, source);
                    File.Delete(sourceFull);
                    CreateVerified(Inside(root, to), bytes, to);
                    File.Delete(temp);
                }
                else
                {
                    CreateVerified(Inside(root, to), bytes, to);
                    File.Delete(sourceFull);
                }
                var observed = Snapshot(ReadFiles(root, cancellationToken));
                if (observed.Count != baseline.Count)
                    throw new IOException("Verificação pós-gravação encontrou arquivos inesperados.");
                foreach (var pair in baseline)
                {
                    var key = string.Equals(pair.Key, source, StringComparison.OrdinalIgnoreCase) ? to : pair.Key;
                    if (!observed.TryGetValue(key, out var actual) || !actual.Bytes.Span.SequenceEqual(pair.Value.Bytes.Span))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + key);
                }
                if (!observed.Keys.Contains(to, StringComparer.Ordinal))
                    throw new IOException("O arquivo movido não passou na verificação: " + to);
                _baseline = observed;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task TrashTextFileAsync(string path, string trashJson, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var root = _root ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(root, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var (source, trashPath, trashBytes) = IUrbeVaultStorage.PlanTrash(files, path, trashJson);
            bool create = !baseline.ContainsKey(trashPath);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                // the trash holds the text before the file goes
                WriteVerified(root, trashPath, trashBytes, create);
                File.Delete(Inside(root, source));
                var observed = Snapshot(ReadFiles(root, cancellationToken));
                if (observed.Count != baseline.Count - 1 + (create ? 1 : 0) || observed.ContainsKey(source))
                    throw new IOException("Verificação pós-gravação encontrou arquivos inesperados.");
                foreach (var pair in baseline)
                {
                    if (string.Equals(pair.Key, source, StringComparison.OrdinalIgnoreCase)) continue;
                    var expected = string.Equals(pair.Key, trashPath, StringComparison.OrdinalIgnoreCase) ? trashBytes : pair.Value.Bytes.ToArray();
                    if (!observed.TryGetValue(pair.Key, out var actual) || !actual.Bytes.Span.SequenceEqual(expected))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + pair.Key);
                }
                if (!observed.TryGetValue(trashPath, out var written) || !written.Bytes.Span.SequenceEqual(trashBytes))
                    throw new IOException("A lixeira não passou na verificação: " + trashPath);
                _baseline = observed;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public Task SaveTrashAsync(string trashJson, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanTrashWrite(files, trashJson), "A lixeira", cancellationToken);

    public Task SaveHistoryAsync(string historyJson, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanHistoryWrite(files, historyJson), "O histórico", cancellationToken);

    public Task SaveTutorialMarkAsync(string json, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanTutorialMarkWrite(files, json), "A marca do Tutorial", cancellationToken);

    /// <summary>One internal file of .urbe/ (the trash, the Tutorial's mark), planned on the files as they are now,
    /// written with verification of it and of every other file.</summary>
    private async Task SaveUrbeFileAsync(Func<IReadOnlyList<VaultFile>, (string, byte[])> plan, string what, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var root = _root ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(root, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var (target, planned) = plan(files);
            bool create = !baseline.ContainsKey(target);
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteVerified(root, target, planned, create);
                var observed = Snapshot(ReadFiles(root, cancellationToken));
                foreach (var pair in baseline)
                {
                    if (!observed.TryGetValue(pair.Key, out var actual) ||
                        !actual.Bytes.Span.SequenceEqual(string.Equals(pair.Key, target, StringComparison.OrdinalIgnoreCase) ? planned : pair.Value.Bytes.Span))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + pair.Key);
                }
                if (!observed.TryGetValue(target, out var made) || !made.Bytes.Span.SequenceEqual(planned) ||
                    observed.Count != baseline.Count + (create ? 1 : 0))
                    throw new IOException(what + " não passou na verificação: " + target);
                _baseline = observed;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    /// <summary>The full path of a vault path, refused when it leaves the vault.</summary>
    private static string Inside(string root, string path)
    {
        var full = Path.GetFullPath(Path.Combine([root, .. Segments(path)]));
        if (!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Caminho fora do vault: " + path);
        return full;
    }

    /// <summary>A new file with exactly these bytes, never over an existing one, read back.</summary>
    private static void CreateVerified(string full, byte[] content, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using (var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(content);
            stream.Flush(flushToDisk: true);
        }
        if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(content))
            throw new IOException("O arquivo criado não passou na verificação: " + path);
    }

    public async Task SaveMapAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var root = _root ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(root, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var (target, planned) = IUrbeVaultStorage.PlanMapWrite(files, json);
            bool create = !baseline.ContainsKey(target);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteVerified(root, target, planned, create);
                var observed = Snapshot(ReadFiles(root, cancellationToken));
                foreach (var pair in baseline)
                {
                    if (!observed.TryGetValue(pair.Key, out var actual) ||
                        !actual.Bytes.Span.SequenceEqual(string.Equals(pair.Key, target, StringComparison.OrdinalIgnoreCase)
                            ? planned : pair.Value.Bytes.Span))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + pair.Key);
                }
                if (create && (!observed.TryGetValue(target, out var made) || !made.Bytes.Span.SequenceEqual(planned)))
                    throw new IOException("O mapa criado não passou na verificação: " + target);
                if (observed.Count != baseline.Count + (create ? 1 : 0))
                    throw new IOException("Verificação pós-gravação encontrou arquivos inesperados.");
                _baseline = observed;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    // ---------------------------------------------------------------- reading

    private static List<VaultFile> ReadFiles(string root, CancellationToken token, bool includeAll = false)
    {
        var files = new List<VaultFile>();
        long entries = 0;
        void Walk(string dir, string prefix, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > 32) throw new IOException("O vault ultrapassa 32 níveis de pastas.");
            var info = new DirectoryInfo(dir);
            foreach (var entry in info.EnumerateFileSystemInfos().OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                if (++entries > MaxEntries) throw new IOException("Limite de itens excedido; use um vault menor nesta beta.");
                // Never follow links out of the chosen folder.
                if (entry.LinkTarget is not null) continue;
                var path = prefix + entry.Name;
                if (entry is DirectoryInfo sub) { Walk(sub.FullName, path + "/", depth + 1); continue; }
                if (!includeAll && !IsIncluded(path)) continue;
                var file = (FileInfo)entry;
                if (file.Length > MaxFileBytes) throw new IOException("Arquivo grande demais para esta beta: " + path);
                files.Add(new VaultFile(path, File.ReadAllBytes(file.FullName)));
            }
        }
        Walk(root, string.Empty, 0);
        if (files.Sum(f => (long)f.Bytes.Length) > MaxVaultBytes)
            throw new IOException("Vault excede o limite temporário de leitura da beta.");
        return files;
    }

    private static bool IsSupportedNote(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    private static bool IsIncluded(string path) =>
        path.StartsWith(".urbe/", StringComparison.OrdinalIgnoreCase) ||
        Included.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, VaultFile> Snapshot(IEnumerable<VaultFile> files) =>
        new ReadOnlyDictionary<string, VaultFile>(files.ToDictionary(f => f.Path, f => f, StringComparer.OrdinalIgnoreCase));

    private static void EnsureUnchanged(IReadOnlyDictionary<string, VaultFile> original, IEnumerable<VaultFile> current)
    {
        var recent = Snapshot(current);
        if (original.Count != recent.Count ||
            original.Any(pair => !recent.TryGetValue(pair.Key, out var file) || !pair.Value.Bytes.Span.SequenceEqual(file.Bytes.Span)))
            throw new IOException("A pasta foi modificada fora do Urbe. Reabra o vault antes de salvar para evitar sobrescrever dados.");
    }

    private static string[] Segments(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Contains(':'))
            throw new InvalidDataException("Caminho inválido no vault: " + path);
        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || s is "." or ".."))
            throw new InvalidDataException("Caminho inseguro no vault: " + path);
        return segments;
    }

    // ---------------------------------------------------------------- writing

    private static void WriteVerified(string root, string path, byte[] content, bool create)
    {
        var full = Path.GetFullPath(Path.Combine([root, .. Segments(path)]));
        if (!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Caminho fora do vault: " + path);
        if (create)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            using (var stream = new FileStream(full, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(content))
                throw new IOException("O arquivo criado não passou na verificação: " + path);
            return;
        }
        var previous = File.ReadAllBytes(full);
        if (previous.AsSpan().SequenceEqual(content)) return;

        // A recoverable copy in the same folder before replacing; a crash leaves it visible.
        var backup = full + ".urbe-recovery-" + Guid.NewGuid().ToString("N");
        File.WriteAllBytes(backup, previous);
        if (!File.ReadAllBytes(backup).AsSpan().SequenceEqual(previous))
            throw new IOException("Cópia de segurança inválida: " + Path.GetFileName(backup));
        try
        {
            using (var stream = new FileStream(full, FileMode.Truncate, FileAccess.Write, FileShare.None))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }
            if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(content))
                throw new IOException("Gravação não confirmada: " + path);
        }
        catch
        {
            try
            {
                File.WriteAllBytes(full, previous);
                if (File.ReadAllBytes(full).AsSpan().SequenceEqual(previous)) File.Delete(backup);
            }
            catch
            {
                // Leave the recoverable copy visible for manual recovery.
            }
            throw;
        }
        File.Delete(backup);
    }

    private sealed record Selection(string Folder);
}
