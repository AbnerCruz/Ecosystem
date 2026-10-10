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

    public async Task SaveExistingNoteAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!IsSupportedNote(path)) throw new NotSupportedException("Esta etapa grava somente notas Markdown existentes.");
        // Disallow traversal, malformed and hidden/metadata targets before any I/O.
        Segments(path);
        if (path.StartsWith(".urbe/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Arquivos internos não podem ser editados como notas.");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            if (!baseline.ContainsKey(path))
                throw new FileNotFoundException("Nota não existe na pasta; criação virá na próxima etapa.", path);
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken);
            EnsureUnchanged(baseline, files);
            var snapshot = VaultReader.Read(files);
            if (snapshot.IsReadOnly || snapshot.FutureFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Formato futuro de vault ou nota protegida: escrita recusada.");
            var note = snapshot.Documents.SingleOrDefault(d =>
                string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
            if (note?.Text is null)
                throw new InvalidDataException("Nota não pôde ser lida como Markdown seguro.");

            // The canonical writer must agree that the document is writable. This is a single-file
            // edit: do NOT apply unrelated migration, journal or delete operations from the plan.
            var documents = snapshot.Documents.Where(d => d.Text is not null)
                .Select(d => new VaultWriteDocument(d.Id, d.Path,
                    string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase) ? content : d.Text!))
                .ToArray();
            var plan = VaultWriter.Plan(new VaultWriteRequest
            {
                Files = files,
                Documents = documents,
                AppVersion = "urbe-native-preview"
            });
            if (!plan.Writable || !plan.Files.ContainsKey(path))
                throw new InvalidOperationException("O plano canônico recusou a nota.");
            var expected = Encoding.UTF8.GetBytes(content);
            // The writer can preserve UTF-8 BOM from an existing note; use its canonical bytes.
            var plannedBytes = plan.Files[path].Bytes.ToArray();
            if (!plannedBytes.AsSpan().SequenceEqual(expected) &&
                !(plannedBytes.Length == expected.Length + 3 && plannedBytes[0] == 0xef &&
                  plannedBytes[1] == 0xbb && plannedBytes[2] == 0xbf &&
                  plannedBytes.AsSpan(3).SequenceEqual(expected)))
                throw new InvalidDataException("O plano canônico modificaria o conteúdo Markdown inesperadamente.");

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteVerified(tree, path, plannedBytes);
                var persisted = ReadFiles(tree, cancellationToken);
                var observed = Snapshot(persisted);
                foreach (var pair in baseline)
                {
                    if (!observed.TryGetValue(pair.Key, out var actual) ||
                        !actual.Bytes.Span.SequenceEqual(
                            string.Equals(pair.Key, path, StringComparison.OrdinalIgnoreCase)
                                ? plannedBytes : pair.Value.Bytes.Span))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + pair.Key);
                }
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

    private static List<VaultFile> ReadFiles(AndroidUri tree, CancellationToken token)
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
                if (!IsIncluded(path))
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
