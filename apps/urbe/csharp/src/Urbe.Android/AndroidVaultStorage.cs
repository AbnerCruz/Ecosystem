using System.Collections.ObjectModel;
using System.Text;
using Android.Content;
using Android.Provider;
using AndroidUri = Android.Net.Uri;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.AndroidHost;

/// <summary>Native Android SAF host, extracted from the already-tested MAUI AndroidVaultHost.
/// The authority is the user's physical folder; no hidden app copy of notes.</summary>
public sealed class AndroidVaultStorage : IUrbeVaultStorage
{
    private const string PreferenceKey = "urbe.native.android.vault.tree.v1";
    private const string DirectoryMime = "vnd.android.document/directory";
    private const string DocumentIdColumn = "document_id";
    private const string DisplayNameColumn = "_display_name";
    private const string MimeTypeColumn = "mime_type";
    private const long MaxFileBytes = 32L * 1024 * 1024;
    private const long MaxVaultBytes = 256L * 1024 * 1024;
    private const int MaxEntries = 10000;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AndroidUri? _tree;
    private IReadOnlyDictionary<string, VaultFile>? _baseline;
    public bool IsConnected => _tree is not null && _baseline is not null;

    private static Android.Content.ISharedPreferences Preferences =>
        Android.App.Application.Context.GetSharedPreferences("urbe-native-vault", FileCreationMode.Private)
        ?? throw new IOException("Preferências do Android indisponíveis.");

    public async Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default)
    {
        var activity = MainActivity.Current
            ?? throw new InvalidOperationException("A Activity do Urbe ainda não está pronta.");
        var pending = new TaskCompletionSource<AndroidUri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RunOnUiThread(async () =>
        {
            try { pending.TrySetResult(await activity.PickVaultFolderAsync()); }
            catch (Exception e) { pending.TrySetException(e); }
        });
        var selected = await pending.Task.WaitAsync(cancellationToken);
        if (selected is null) return null;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var files = await Task.Run(() => ReadFiles(selected, cancellationToken), cancellationToken);
            var snapshot = VaultReader.Read(files);
            _tree = selected;
            _baseline = Snapshot(files);
            Preferences.Edit()?.PutString(PreferenceKey, selected.ToString())?.Apply();
            return snapshot;
        }
        finally { _gate.Release(); }
    }

    public async Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected) return null;
        var stored = Preferences.GetString(PreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stored)) return null;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var selected = AndroidUri.Parse(stored)
                ?? throw new IOException("Pasta Android anteriormente selecionada é inválida.");
            var files = await Task.Run(() => ReadFiles(selected, cancellationToken), cancellationToken);
            var snapshot = VaultReader.Read(files);
            _tree = selected;
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
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            if (_baseline is null) throw new InvalidOperationException("Reabra a pasta do vault.");
            await Task.Run(() =>
            {
                // a segment that exists as a file is refused (EnsureDirectory would walk into it)
                var parent = Root(tree);
                foreach (var segment in Segments(path))
                {
                    var child = Children(tree, parent).FirstOrDefault(x => string.Equals(x.Name, segment, StringComparison.Ordinal));
                    if (child is null) break;
                    if (child.MimeType != DirectoryMime) throw new IOException("Já existe um arquivo com esse nome: " + path);
                    parent = DocumentUri(tree, child.DocumentId);
                }
                EnsureDirectory(tree, path);
                if (FindDocument(tree, path) is null) throw new IOException("A pasta criada não passou na verificação: " + path);
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public string? VaultName
    {
        get
        {
            if (_tree is not { } tree) return null;
            try
            {
                using var cursor = Resolver.Query(Root(tree), [DisplayNameColumn], null, null, null);
                return cursor is not null && cursor.MoveToFirst() ? cursor.GetString(0) : null;
            }
            catch (Exception ex) when (ex is IOException or Java.Lang.Exception) { return null; }
        }
    }

    public async Task<IReadOnlyList<VaultFile>> ReadAllFilesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            return await Task.Run(() => ReadFiles(tree, cancellationToken, includeAll: true), cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<string>> ListFilePathsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            return await Task.Run(() =>
            {
                // names only, from the document provider: no attachment is opened or read
                var paths = new List<string>();
                void Walk(AndroidUri dir, string prefix, int depth)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (depth > 32 || paths.Count >= MaxEntries) return;
                    foreach (var entry in Children(tree, dir))
                    {
                        if (entry.Name.StartsWith('.')) continue;
                        if (entry.MimeType == DirectoryMime) Walk(DocumentUri(tree, entry.DocumentId), prefix + entry.Name + "/", depth + 1);
                        else if (paths.Count < MaxEntries) paths.Add(prefix + entry.Name);
                    }
                }
                Walk(Root(tree), string.Empty, 0);
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
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            if (_baseline is null) throw new InvalidOperationException("Reabra a pasta do vault.");
            return await Task.Run(() =>
            {
                // every step must be a folder with exactly that name; the folder itself must hold nothing
                var parent = Root(tree);
                foreach (var segment in Segments(path))
                {
                    var child = Children(tree, parent).FirstOrDefault(x => string.Equals(x.Name, segment, StringComparison.Ordinal));
                    if (child is null || child.MimeType != DirectoryMime) return false;
                    parent = DocumentUri(tree, child.DocumentId);
                }
                if (Children(tree, parent).Count > 0) return false;
                if (!DocumentsContract.DeleteDocument(Resolver, parent)) return false;
                return FindDocument(tree, path) is null;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Writes ONE user text file: creates it when <paramref name="create"/> and absent,
    /// else rewrites the existing one; same checks and verification for both.</summary>
    private async Task WriteTextFileAsync(string path, string content, bool create, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        IUrbeVaultStorage.EnsureWritableTextPath(path);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            if (create && baseline.ContainsKey(path))
                throw new IOException("Já existe um arquivo com esse nome: " + path);
            if (!create && !baseline.ContainsKey(path))
                throw new FileNotFoundException("O arquivo não existe na pasta.", path);
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var snapshot = VaultReader.Read(files);
            if (snapshot.IsReadOnly || snapshot.FutureFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Formato futuro de vault ou arquivo protegido: escrita recusada.");
            var existing = snapshot.Documents.SingleOrDefault(d =>
                string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
            if (!create && existing?.Text is null)
                throw new InvalidDataException("O arquivo não pôde ser lido como texto seguro.");
            var target = existing?.Path ?? path;

            // The canonical writer must agree that the document is writable. This is a single-file
            // write: do NOT apply unrelated migration, journal or delete operations from the plan.
            var documents = snapshot.Documents.Where(d => d.Text is not null)
                .Select(d => new VaultWriteDocument(d.Id, d.Path,
                    string.Equals(d.Path, target, StringComparison.OrdinalIgnoreCase) ? content : d.Text!))
                .ToList();
            if (create) documents.Add(new VaultWriteDocument("doc-" + Guid.NewGuid().ToString("N"), target, content));
            var plan = VaultWriter.Plan(new VaultWriteRequest
            {
                Files = files,
                Documents = documents,
                AppVersion = "urbe-native-preview"
            });
            if (!plan.Writable || !plan.Files.ContainsKey(target))
                throw new InvalidOperationException("O plano canônico recusou o arquivo.");
            var expected = Encoding.UTF8.GetBytes(content);
            // The writer can preserve UTF-8 BOM from an existing note; use its canonical bytes.
            var plannedBytes = plan.Files[target].Bytes.ToArray();
            if (!plannedBytes.AsSpan().SequenceEqual(expected) &&
                !(plannedBytes.Length == expected.Length + 3 && plannedBytes[0] == 0xef &&
                  plannedBytes[1] == 0xbb && plannedBytes[2] == 0xbf &&
                  plannedBytes.AsSpan(3).SequenceEqual(expected)))
                throw new InvalidDataException("O plano canônico modificaria o conteúdo inesperadamente.");

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (create && FindDocument(tree, target) is not null)
                    throw new IOException("Já existe um arquivo com esse nome: " + target);
                WriteVerified(tree, target, plannedBytes);
                var persisted = ReadFiles(tree, cancellationToken);
                var observed = Snapshot(persisted);
                foreach (var pair in baseline)
                {
                    if (!observed.TryGetValue(pair.Key, out var actual) ||
                        !actual.Bytes.Span.SequenceEqual(
                            string.Equals(pair.Key, target, StringComparison.OrdinalIgnoreCase)
                                ? plannedBytes : pair.Value.Bytes.Span))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + pair.Key);
                }
                if (create && (!observed.TryGetValue(target, out var made) || !made.Bytes.Span.SequenceEqual(plannedBytes)))
                    throw new IOException("O arquivo criado não passou na verificação: " + target);
                if (observed.Count != baseline.Count + (create ? 1 : 0))
                    throw new IOException("Verificação pós-gravação encontrou arquivos inesperados.");
                _baseline = observed;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Moves ONE text file (a renamed note): the new file is written and verified before the old
    /// one is deleted; a change of case only goes through a temporary copy; then every file is verified.</summary>
    public async Task MoveTextFileAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var (source, bytes, caseOnly) = IUrbeVaultStorage.PlanMove(files, from, to);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceUri = FindDocument(tree, source)
                    ?? throw new FileNotFoundException("O arquivo não existe na pasta.", source);
                if (caseOnly)
                {
                    // the providers' folders ignore case: through a temporary copy the vault does not list
                    var pieces = Segments(source);
                    var directory = pieces.Length == 1 ? Root(tree) : EnsureDirectory(tree, string.Join("/", pieces[..^1]));
                    var temp = DocumentsContract.CreateDocument(Resolver, directory, "application/octet-stream",
                        pieces[^1] + ".urbe-move-" + Guid.NewGuid().ToString("N"))
                        ?? throw new IOException("Não foi possível criar a cópia temporária: " + source);
                    PutBytes(temp, bytes);
                    if (!ReadBytes(temp).AsSpan().SequenceEqual(bytes))
                        throw new IOException("Cópia temporária inválida: " + source);
                    if (!DocumentsContract.DeleteDocument(Resolver, sourceUri))
                        throw new IOException("O provedor não removeu o nome antigo: " + source);
                    if (FindDocument(tree, to) is not null) throw new IOException("Já existe um arquivo com esse nome: " + to);
                    WriteVerified(tree, to, bytes);
                    DocumentsContract.DeleteDocument(Resolver, temp);
                }
                else
                {
                    if (FindDocument(tree, to) is not null) throw new IOException("Já existe um arquivo com esse nome: " + to);
                    WriteVerified(tree, to, bytes);
                    if (!DocumentsContract.DeleteDocument(Resolver, sourceUri))
                        throw new IOException("O provedor não removeu o arquivo antigo: " + source);
                }
                var observed = Snapshot(ReadFiles(tree, cancellationToken));
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

    /// <summary>Moves ONE text file to the trash: .urbe/trash.v2.json written and verified first, then the file
    /// deleted, then every file verified.</summary>
    public async Task TrashTextFileAsync(string path, string trashJson, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var (source, trashPath, trashBytes) = IUrbeVaultStorage.PlanTrash(files, path, trashJson);
            var create = !baseline.ContainsKey(trashPath);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceUri = FindDocument(tree, source)
                    ?? throw new FileNotFoundException("O arquivo não existe na pasta.", source);
                if (create && FindDocument(tree, trashPath) is not null)
                    throw new IOException("A pasta foi modificada fora do Urbe: " + trashPath);
                WriteVerified(tree, trashPath, trashBytes);
                if (!DocumentsContract.DeleteDocument(Resolver, sourceUri))
                    throw new IOException("O provedor não removeu o arquivo: " + source);
                var observed = Snapshot(ReadFiles(tree, cancellationToken));
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

    /// <summary>Rewrites .urbe/trash.v2.json only; the same checks and verification as the map.</summary>
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
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var (target, plannedBytes) = plan(files);
            var create = !baseline.ContainsKey(target);
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (create && FindDocument(tree, target) is not null)
                    throw new IOException("A pasta foi modificada fora do Urbe: " + target);
                WriteVerified(tree, target, plannedBytes);
                var observed = Snapshot(ReadFiles(tree, cancellationToken));
                foreach (var pair in baseline)
                {
                    if (!observed.TryGetValue(pair.Key, out var actual) ||
                        !actual.Bytes.Span.SequenceEqual(string.Equals(pair.Key, target, StringComparison.OrdinalIgnoreCase) ? plannedBytes : pair.Value.Bytes.Span))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + pair.Key);
                }
                if (!observed.TryGetValue(target, out var made) || !made.Bytes.Span.SequenceEqual(plannedBytes) ||
                    observed.Count != baseline.Count + (create ? 1 : 0))
                    throw new IOException(what + " não passou na verificação: " + target);
                _baseline = observed;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Writes .urbe/mapa.json only; the same checks and verification as a note.</summary>
    public async Task SaveMapAsync(string json, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var (target, plannedBytes) = IUrbeVaultStorage.PlanMapWrite(files, json);
            var create = !baseline.ContainsKey(target);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (create && FindDocument(tree, target) is not null)
                    throw new IOException("A pasta foi modificada fora do Urbe: " + target);
                WriteVerified(tree, target, plannedBytes);
                var persisted = ReadFiles(tree, cancellationToken);
                var observed = Snapshot(persisted);
                foreach (var pair in baseline)
                {
                    if (!observed.TryGetValue(pair.Key, out var actual) ||
                        !actual.Bytes.Span.SequenceEqual(
                            string.Equals(pair.Key, target, StringComparison.OrdinalIgnoreCase)
                                ? plannedBytes : pair.Value.Bytes.Span))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + pair.Key);
                }
                if (create && (!observed.TryGetValue(target, out var made) || !made.Bytes.Span.SequenceEqual(plannedBytes)))
                    throw new IOException("O mapa criado não passou na verificação: " + target);
                if (observed.Count != baseline.Count + (create ? 1 : 0))
                    throw new IOException("Verificação pós-gravação encontrou arquivos inesperados.");
                _baseline = observed;
            }, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private static ContentResolver Resolver =>
        Android.App.Application.Context.ContentResolver
        ?? throw new IOException("ContentResolver indisponível.");

    private sealed record Entry(string Name, string DocumentId, string MimeType);

    private static AndroidUri Root(AndroidUri tree) =>
        DocumentsContract.BuildDocumentUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree))
        ?? throw new IOException("Não foi possível acessar a raiz da pasta selecionada.");

    private static IReadOnlyList<Entry> Children(AndroidUri tree, AndroidUri parent)
    {
        var id = DocumentsContract.GetDocumentId(parent);
        var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, id)
            ?? throw new IOException("O provedor não retornou a pasta de documentos.");
        using var cursor = Resolver.Query(children,
            [DocumentIdColumn, DisplayNameColumn, MimeTypeColumn],
            null, null, null)
            ?? throw new IOException("Falha ao listar documentos da pasta.");
        var result = new List<Entry>();
        while (cursor.MoveToNext())
        {
            var documentId = cursor.GetString(0) ?? string.Empty;
            var name = cursor.GetString(1) ?? string.Empty;
            var mime = cursor.GetString(2) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
                name.Contains('/') || name.Contains('\\'))
                throw new InvalidDataException("Nome de documento inválido encontrado no vault.");

            result.Add(new Entry(name, documentId, mime));
        }
        return result;
    }

    private static AndroidUri DocumentUri(AndroidUri tree, string id) =>
        DocumentsContract.BuildDocumentUriUsingTree(tree, id)
        ?? throw new IOException("URI do documento não pôde ser construída.");

    private static List<VaultFile> ReadFiles(AndroidUri tree, CancellationToken token, bool includeAll = false)
    {
        var files = new List<VaultFile>();
        long total = 0;
        void Walk(AndroidUri dir, string prefix, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > 32)
                throw new IOException("O vault ultrapassa 32 níveis de pastas.");

            foreach (var entry in Children(tree, dir))
            {
                token.ThrowIfCancellationRequested();
                if (++total > MaxEntries)
                    throw new IOException("Limite de itens excedido; use um vault menor nesta beta.");

                var path = prefix + entry.Name;
                var uri = DocumentUri(tree, entry.DocumentId);
                if (entry.MimeType == DirectoryMime)
                {
                    Walk(uri, path + "/", depth + 1);
                    continue;
                }

                // Read canonical text and metadata; assets stay untouched on
                // disk, and are not rewritten/deleted by a text-only beta.
                if (!includeAll && !IsIncluded(path))
                    continue;

                var bytes = ReadBytes(uri);
                if (bytes.LongLength > MaxFileBytes)
                    throw new IOException("Arquivo grande demais para esta beta: " + path);

                files.Add(new VaultFile(path, bytes));
            }
        }
        Walk(Root(tree), string.Empty, 0);
        var size = files.Sum(file => (long)file.Bytes.Length);
        if (size > MaxVaultBytes)
            throw new IOException("Vault excede o limite temporário de leitura da beta.");
        return files;
    }

    private static IReadOnlyList<string> ReadFolders(
        AndroidUri tree, CancellationToken token)
    {
        var folders = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var entries = 0;

        void Walk(AndroidUri parent, string prefix, int depth)
        {
            token.ThrowIfCancellationRequested();
            if (depth > 32)
                throw new IOException("O vault ultrapassa 32 níveis de pastas.");

            var parentId = DocumentsContract.GetDocumentId(parent)
                ?? throw new IOException("Identificador de pasta Android indisponível.");
            if (!visited.Add(parentId))
                throw new IOException("Estrutura circular de pastas no provedor Android.");

            foreach (var child in Children(tree, parent))
            {
                token.ThrowIfCancellationRequested();
                if (++entries > MaxEntries)
                    throw new IOException("Limite de pastas/arquivos excedido.");

                if (child.MimeType != DirectoryMime)
                    continue;

                var path = prefix + child.Name;
                if (path.Split('/').Any(segment => segment.StartsWith('.')))
                    continue; // Internal .urbe tree is not an Explorer bairro.

                folders.Add(path);
                Walk(DocumentUri(tree, child.DocumentId), path + "/", depth + 1);
            }
        }

        Walk(Root(tree), string.Empty, 0);
        return folders.AsReadOnly();
    }

    private static bool IsSupportedNote(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase);

    private static bool IsIncluded(string path) =>
        path.StartsWith(".urbe/", StringComparison.OrdinalIgnoreCase) ||
        new[] { ".md", ".markdown", ".txt", ".json", ".yaml", ".yml",
                ".html", ".htm", ".css", ".js", ".mjs", ".csv" }
            .Contains(System.IO.Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    private static byte[] ReadBytes(AndroidUri uri)
    {
        using var stream = Resolver.OpenInputStream(uri)
            ?? throw new IOException("Arquivo não pode ser lido: " + uri);
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxFileBytes)
                throw new IOException("Arquivo acima do limite de segurança da beta.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static IReadOnlyDictionary<string, VaultFile> Snapshot(IEnumerable<VaultFile> files) =>
        new ReadOnlyDictionary<string, VaultFile>(
            files.ToDictionary(file => file.Path, file => file,
                StringComparer.OrdinalIgnoreCase));

    private static void EnsureUnchanged(
        IReadOnlyDictionary<string, VaultFile> original,
        IEnumerable<VaultFile> current)
    {
        var recent = Snapshot(current);
        if (original.Count != recent.Count ||
            original.Any(pair => !recent.TryGetValue(pair.Key, out var file) ||
                !pair.Value.Bytes.Span.SequenceEqual(file.Bytes.Span)))
            throw new IOException(
                "A pasta foi modificada fora do Urbe. Reabra o vault antes de salvar para evitar sobrescrever dados.");
    }

    private static string[] Segments(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') ||
            path.Contains('\\'))
            throw new InvalidDataException("Caminho inválido no vault: " + path);

        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || s is "." or ".."))
            throw new InvalidDataException("Caminho inseguro no vault: " + path);
        return segments;
    }

    private static AndroidUri? FindChild(AndroidUri tree, AndroidUri parent, string name)
    {
        var child = Children(tree, parent)
            .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));
        return child is null ? null : DocumentUri(tree, child.DocumentId);
    }

    private static AndroidUri? FindDocument(AndroidUri tree, string path)
    {
        var parent = Root(tree);
        foreach (var segment in Segments(path))
        {
            var child = FindChild(tree, parent, segment);
            if (child is null)
                return null;
            parent = child;
        }
        return parent;
    }

    private static AndroidUri EnsureDirectory(AndroidUri tree, string path)
    {
        var parent = Root(tree);
        foreach (var segment in Segments(path))
        {
            var existing = FindChild(tree, parent, segment);
            if (existing is null)
                existing = DocumentsContract.CreateDocument(Resolver, parent, DirectoryMime, segment);
            parent = existing ?? throw new IOException("Não foi possível criar a pasta " + segment);
        }
        return parent;
    }

    private static void PutBytes(AndroidUri uri, byte[] bytes)
    {
        using var stream = Resolver.OpenOutputStream(uri, "wt")
            ?? throw new IOException("O provedor recusou gravação.");
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static void WriteVerified(AndroidUri tree, string path, byte[] content)
    {
        var pieces = Segments(path);
        var fileName = pieces[^1];
        var directory = pieces.Length == 1
            ? Root(tree)
            : EnsureDirectory(tree, string.Join("/", pieces[..^1]));
        var file = FindChild(tree, directory, fileName);

        if (file is null)
        {
            var mime = fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ? "text/markdown"
                : "application/octet-stream";
            file = DocumentsContract.CreateDocument(Resolver, directory, mime, fileName)
                ?? throw new IOException("O provedor não criou o arquivo: " + path);
            PutBytes(file, content);
            if (!ReadBytes(file).AsSpan().SequenceEqual(content))
                throw new IOException("O arquivo criado não passou na verificação: " + path);
            return;
        }

        var previous = ReadBytes(file);
        if (previous.AsSpan().SequenceEqual(content))
            return;

        // Preserve recoverable copy in the *same* user folder before truncating.
        // A crash mid-write leaves the backup visible; no silent data loss.
        var backupName = fileName + ".urbe-recovery-" + Guid.NewGuid().ToString("N");
        var backup = DocumentsContract.CreateDocument(
            Resolver, directory, "application/octet-stream", backupName)
            ?? throw new IOException("Não foi possível criar cópia de segurança: " + path);
        PutBytes(backup, previous);
        if (!ReadBytes(backup).AsSpan().SequenceEqual(previous))
            throw new IOException("Cópia de segurança inválida: " + backupName);

        try
        {
            PutBytes(file, content);
            if (!ReadBytes(file).AsSpan().SequenceEqual(content))
                throw new IOException("Gravação não confirmada: " + path);
        }
        catch
        {
            try
            {
                PutBytes(file, previous);
                if (ReadBytes(file).AsSpan().SequenceEqual(previous))
                    DocumentsContract.DeleteDocument(Resolver, backup);
            }
            catch
            {
                // Leave the recoverable backup visible for manual recovery.
            }
            throw;
        }

        DocumentsContract.DeleteDocument(Resolver, backup);
    }
}
