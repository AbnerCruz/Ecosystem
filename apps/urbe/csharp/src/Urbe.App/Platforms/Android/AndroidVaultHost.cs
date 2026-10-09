using System.Collections.ObjectModel;
using Android.Content;
using AndroidUri = Android.Net.Uri;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Urbe.Core;
using Urbe.UI;

namespace Urbe.App;

/// <summary>
/// User-owned Android Storage Access Framework document tree. No all-files
/// permission, no hidden internal copy of the canonical vault.
/// </summary>
public sealed class AndroidVaultHost : IVaultHost
{
    private const string PreferenceKey = "urbe.csharp.android.vault.tree.v1";
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

    public bool IsAvailable => true;
    public bool IsConnected => _tree is not null && _baseline is not null;
    public string? DisplayName => IsConnected ? "Pasta do dispositivo (Android)" : null;

    public async Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default)
    {
        var activity = Platform.CurrentActivity as MainActivity
            ?? throw new InvalidOperationException("O seletor precisa da Activity principal do Urbe.");
        var selected = await MainThread.InvokeOnMainThreadAsync(activity.PickVaultFolderAsync);
        if (selected is null)
            return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var files = await Task.Run(() => ReadFiles(selected, cancellationToken), cancellationToken);
            var snapshot = VaultReader.Read(files);
            _tree = selected;
            _baseline = Snapshot(files);
            Preferences.Default.Set(PreferenceKey, selected.ToString());
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected)
            return null;
        var stored = Preferences.Default.Get(PreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tree = AndroidUri.Parse(stored);
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken);
            var snapshot = VaultReader.Read(files);
            _tree = tree;
            _baseline = Snapshot(files);
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(WorkspaceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsReadOnly)
            throw new InvalidOperationException("Este vault está em modo somente leitura.");

        var documents = session.Documents.List()
            .Select(document => new VaultWriteDocument(
                document.Id, document.Path, document.Content))
            .ToArray();
        var mutations = session.PendingMutations.ToArray();
        if (mutations.Any(mutation => mutation.Kind == WorkspaceMutationKind.Move))
            throw new NotSupportedException(
                "Mover notas e pastas ainda não é gravado por esta beta Android. " +
                "Reabra a pasta para descartar a movimentação temporária antes de salvar.");
        var initialRevision = session.Revision;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");

            var updated = await Task.Run(() =>
            {
                var current = ReadFiles(tree, cancellationToken);
                EnsureUnchanged(baseline, current);
                var plan = VaultWriter.Plan(new VaultWriteRequest
                {
                    Files = current,
                    Documents = documents,
                    AppVersion = "urbe-csharp-beta"
                });
                if (!plan.Writable)
                    throw new InvalidOperationException("Formato de vault futuro: escrita bloqueada.");

                // Folder creation is explicit in WorkspaceSession, including empty folders.
                foreach (var mutation in mutations)
                {
                    if (mutation.Kind == WorkspaceMutationKind.CreateFolder)
                        EnsureDirectory(tree, mutation.TargetPath);
                }

                // Plan orders document writes, then metadata, then removals.
                // Physical removals are deliberately postponed until every new
                // or rewritten document has been verified by the provider.
                var removals = new List<string>();
                foreach (var operation in plan.Operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (operation.Kind == VaultOperationKind.Remove)
                    {
                        removals.Add(operation.Path);
                        continue;
                    }
                    if (operation.Bytes is not { } payload)
                        throw new InvalidDataException("Operação de escrita sem bytes.");

                    WriteVerified(tree, operation.Path, payload.ToArray());
                }

                foreach (var path in removals)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var target = FindDocument(tree, path);
                    if (target is not null &&
                        !DocumentsContract.DeleteDocument(Resolver, target))
                        throw new IOException("Não foi possível concluir a remoção: " + path);
                }

                var persisted = ReadFiles(tree, cancellationToken);
                var observed = Snapshot(persisted);
                foreach (var pair in plan.Files)
                {
                    if (!observed.TryGetValue(pair.Key, out var file) ||
                        !file.Bytes.Span.SequenceEqual(pair.Value.Bytes.Span))
                        throw new IOException("Falhou a verificação pós-salvamento: " + pair.Key);
                }
                return (Files: persisted, Snapshot: observed);
            }, cancellationToken);

            _baseline = updated.Snapshot;
            if (session.Revision == initialRevision)
                session.MarkSaved();

        }
        finally
        {
            _gate.Release();
        }
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
        var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, id);
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
        stream.CopyTo(buffer);
        if (buffer.Length > MaxFileBytes)
            throw new IOException("Arquivo acima do limite de segurança da beta.");
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
