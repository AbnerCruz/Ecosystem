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
    private const string SizeColumn = "_size";
    private const string LastModifiedColumn = "last_modified";

    /// <summary>
    /// What was read of each file (by document id), kept while the provider reports the same size and modification
    /// time: a save no longer opens every file of the vault twice (UC-48: "demora horrores para salvar"). A file
    /// changed less than 3 s before it was read, or bigger than 512 KB, is never kept, so a change in the same second is always read again.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Size, long Modified, byte[] Bytes)> FileCache = new();

    /// <summary>The bytes of a listed file: from the cache when nothing about it changed, else read and kept.</summary>
    private static byte[] ReadListed(AndroidUri uri, Entry entry)
    {
        if (entry.Modified > 0 && entry.Size >= 0 && FileCache.TryGetValue(entry.DocumentId, out var hit) &&
            hit.Size == entry.Size && hit.Modified == entry.Modified && hit.Bytes.LongLength == entry.Size)
            return hit.Bytes;
        var bytes = ReadBytes(uri);
        // only notes-sized files are kept (images and media are read again), so the cache stays small
        if (entry.Modified > 0 && bytes.LongLength == entry.Size && bytes.LongLength <= 512 * 1024 &&
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - entry.Modified > 3000)
            FileCache[entry.DocumentId] = (entry.Size, entry.Modified, bytes);
        else FileCache.TryRemove(entry.DocumentId, out _);
        return bytes;
    }
    /// <summary>
    /// DEC-0059: the vault has no limit of items, size or depth. Only a data file this large that is not a note nor of
    /// .urbe/ (a CSV, a JSON export) is not kept in memory to check the vault: it is listed and opened like any other file.
    /// </summary>
    private const long InMemoryTextBytes = 32L * 1024 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private AndroidUri? _tree;
    /// <summary>The files listed by the walk of the last opening, for the Explorer's ListFilePathsAsync right after it.</summary>
    private IReadOnlyList<string>? _listing;
    private IReadOnlyDictionary<string, VaultFile>? _baseline;
    public bool IsConnected => _tree is not null && _baseline is not null;
    public string? AtlasVaultSessionKey => _tree?.ToString();

    public IImportStorage? ImportStorage => _tree is null ? null : new Importer(this);

    /// <summary>The folder for ImportTransaction: every file (attachments and .urbe too), under the host's lock.</summary>
    private sealed class Importer(AndroidVaultStorage host) : IImportStorage
    {
        private AndroidUri Tree => host._tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");

        public async ValueTask<IAsyncDisposable> AcquireExclusiveAsync(CancellationToken cancellationToken)
        {
            await host._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Release(host);
        }

        private sealed class Release(AndroidVaultStorage host) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                try { if (host._tree is { } tree) host._baseline = Snapshot(ReadFiles(tree, CancellationToken.None)); }
                finally { host._gate.Release(); }
                return ValueTask.CompletedTask;
            }
        }

        public ValueTask<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<string>>(ReadFiles(Tree, cancellationToken, includeAll: true).Select(f => f.Path).ToList());

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string path, CancellationToken cancellationToken) =>
            ValueTask.FromResult<ReadOnlyMemory<byte>?>(FindDocument(Tree, path) is { } uri ? new ReadOnlyMemory<byte>(ReadBytes(uri)) : (ReadOnlyMemory<byte>?)null);

        public ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
        {
            WriteVerified(Tree, path, bytes.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAsync(string path, CancellationToken cancellationToken)
        {
            if (FindDocument(Tree, path) is { } uri && !DocumentsContract.DeleteDocument(Resolver, uri))
                throw new IOException("O provedor recusou excluir " + path);
            return ValueTask.CompletedTask;
        }
    }

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
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var listing = new List<string>();
            var files = await Task.Run(() => ReadFiles(selected, cancellationToken, listing: listing), cancellationToken).ConfigureAwait(false);
            var snapshot = VaultReader.Read(files, UrbeApp.InstalledVersion);
            _listing = listing;
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
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var selected = AndroidUri.Parse(stored)
                ?? throw new IOException("Pasta Android anteriormente selecionada é inválida.");
            var listing = new List<string>();
            var files = await Task.Run(() => ReadFiles(selected, cancellationToken, listing: listing), cancellationToken).ConfigureAwait(false);
            var snapshot = VaultReader.Read(files, UrbeApp.InstalledVersion);
            _listing = listing;
            _tree = selected;
            _baseline = Snapshot(files);
            return snapshot;
        }
        finally { _gate.Release(); }
    }

    public Task<string?> PeekTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var stored = _tree?.ToString() ?? Preferences.GetString(PreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(stored)) return Task.FromResult<string?>(null);
        // one file of the remembered folder, outside the gate: the whole reading may start meanwhile
        return Task.Run(() =>
        {
            try
            {
                if (AndroidUri.Parse(stored) is not { } tree || FindDocument(tree, path) is not { } uri) return null;
                return new UTF8Encoding(false, true).GetString(ReadBytes(uri)).TrimStart('\uFEFF');
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or DecoderFallbackException or Java.Lang.Exception or ArgumentException) { return null; }
        }, cancellationToken);
    }

    public async Task<VaultSnapshot?> ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tree is not { } tree) return null;
            var listing = new List<string>();
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken, listing: listing), cancellationToken).ConfigureAwait(false);
            _baseline = Snapshot(files);
            _listing = listing;
            return VaultReader.Read(files, UrbeApp.InstalledVersion);
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

    public Task CreateBinaryFileAsync(string path, byte[] bytes, CancellationToken cancellationToken = default) =>
        CreateRawFileAsync(path, bytes, imported: false, cancellationToken);

    public Task CreateImportedFileAsync(string path, byte[] bytes, CancellationToken cancellationToken = default) =>
        CreateRawFileAsync(path, bytes, imported: true, cancellationToken);

    private async Task CreateRawFileAsync(string path, byte[] bytes, bool imported, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (imported) IUrbeVaultStorage.EnsureWritableImportedPath(path);
        else IUrbeVaultStorage.EnsureWritableBinaryPath(path);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            if (_baseline is null) throw new InvalidOperationException("Reabra a pasta do vault.");
            await Task.Run(() =>
            {
                // a folder on the way that exists as a file is refused (EnsureDirectory would walk into it)
                var parent = Root(tree);
                foreach (var segment in Segments(path)[..^1])
                {
                    var child = Children(tree, parent).FirstOrDefault(x => string.Equals(x.Name, segment, StringComparison.Ordinal));
                    if (child is null) break;
                    if (child.MimeType != DirectoryMime) throw new IOException("Já existe um arquivo com esse nome: " + path);
                    parent = DocumentUri(tree, child.DocumentId);
                }
                if (FindDocument(tree, path) is not null) throw new IOException("Já existe um arquivo com esse nome: " + path);
                cancellationToken.ThrowIfCancellationRequested();
                WriteVerified(tree, path, bytes);
                // the provider keeps the name asked (never "foto (1).png") and the bytes read back are the ones written
                if (FindDocument(tree, path) is not { } made || !ReadBytes(made).AsSpan().SequenceEqual(bytes))
                    throw new IOException("O arquivo criado não passou na verificação: " + path);
                _baseline = Snapshot(ReadFiles(tree, cancellationToken));
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public Task SaveExistingTextFileIfUnchangedAsync(string path, string content, byte[] expectedSha256, CancellationToken cancellationToken = default) =>
        WriteTextFileAsync(path, content, create: false, cancellationToken, expectedSha256);

    public Task SaveExistingTextFileAsync(string path, string content, CancellationToken cancellationToken = default) =>
        WriteTextFileAsync(path, content, create: false, cancellationToken);

    public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        IUrbeVaultStorage.EnsureWritableFolderPath(path);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            }, cancellationToken).ConfigureAwait(false);
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
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            return await Task.Run(() => ReadFiles(tree, cancellationToken, includeAll: true), cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<string>> ListFilePathsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            // the walk that just read the vault listed it already (once: later the folder may have changed)
            if (Interlocked.Exchange(ref _listing, null) is { } fresh) return fresh;
            return await Task.Run(() =>
            {
                // names only, from the document provider: no attachment is opened or read
                var paths = new List<string>();
                void Walk(AndroidUri dir, string prefix, int depth)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var entry in Children(tree, dir))
                    {
                        if (entry.Name.StartsWith('.')) continue;
                        if (entry.MimeType == DirectoryMime) Walk(DocumentUri(tree, entry.DocumentId), prefix + entry.Name + "/", depth + 1);
                        else paths.Add(prefix + entry.Name);
                    }
                }
                Walk(Root(tree), string.Empty, 0);
                return (IReadOnlyList<string>)paths;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task MoveFileAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        IUrbeVaultStorage.EnsureAttachmentPath(from);
        IUrbeVaultStorage.EnsureAttachmentPath(to);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken).ConfigureAwait(false);
            EnsureUnchanged(baseline, files);
            await Task.Run(() =>
            {
                static string Parent(string p) => p.Contains('/') ? p[..p.LastIndexOf('/')] : "";
                AndroidUri FolderOf(AndroidUri t, string p) => p.Length == 0 ? Root(t)
                    : FindDocument(t, p) ?? throw new DirectoryNotFoundException("A pasta de destino não existe no vault: " + p);
                var source = FindDocument(tree, from) ?? throw new FileNotFoundException("O arquivo não existe na pasta.", from);
                bool caseOnly = string.Equals(from, to, StringComparison.OrdinalIgnoreCase);
                if (!caseOnly && FindDocument(tree, to) is not null) throw new IOException("Já existe um arquivo com esse nome: " + to);
                var name = to[(to.LastIndexOf('/') + 1)..];
                var moved = source;
                if (!string.Equals(Parent(from), Parent(to), StringComparison.Ordinal))
                    moved = DocumentsContract.MoveDocument(Resolver, source, FolderOf(tree, Parent(from)), FolderOf(tree, Parent(to)))
                        ?? throw new IOException("O provedor não moveu o arquivo: " + from);
                if (!string.Equals(from[(from.LastIndexOf('/') + 1)..], name, StringComparison.Ordinal))
                {
                    if (caseOnly)
                        moved = DocumentsContract.RenameDocument(Resolver, moved, name + ".urbe-move-" + Guid.NewGuid().ToString("N"))
                            ?? throw new IOException("O provedor não renomeou o arquivo: " + from);
                    _ = DocumentsContract.RenameDocument(Resolver, moved, name) ?? throw new IOException("O provedor não renomeou o arquivo: " + from);
                }
                if (FindDocument(tree, to) is null || (!caseOnly && FindDocument(tree, from) is not null))
                    throw new IOException("O arquivo movido não passou na verificação: " + to);
                EnsureUnchanged(baseline, ReadFiles(tree, cancellationToken));
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        IUrbeVaultStorage.EnsureAttachmentPath(path);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken).ConfigureAwait(false);
            EnsureUnchanged(baseline, files);
            await Task.Run(() =>
            {
                var uri = FindDocument(tree, path) ?? throw new FileNotFoundException("O arquivo não existe na pasta.", path);
                if (!DocumentsContract.DeleteDocument(Resolver, uri) || FindDocument(tree, path) is not null)
                    throw new IOException("O provedor não excluiu o arquivo: " + path);
                EnsureUnchanged(baseline, ReadFiles(tree, cancellationToken));
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task RenameFolderAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        IUrbeVaultStorage.EnsureFolderRename(from, to);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken).ConfigureAwait(false);
            EnsureUnchanged(baseline, files);
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var folder = FindDocument(tree, from) ?? throw new DirectoryNotFoundException("A pasta não existe no vault: " + from);
                bool caseOnly = string.Equals(from, to, StringComparison.OrdinalIgnoreCase);
                if (!caseOnly && FindDocument(tree, to) is not null) throw new IOException("Já existe uma pasta ou arquivo com esse nome: " + to);
                var name = to[(to.LastIndexOf('/') + 1)..];
                if (caseOnly)
                {
                    // the providers ignore case: through a temporary name the vault does not list
                    folder = DocumentsContract.RenameDocument(Resolver, folder, name + ".urbe-rename-" + Guid.NewGuid().ToString("N"))
                        ?? throw new IOException("O provedor não renomeou a pasta: " + from);
                }
                _ = DocumentsContract.RenameDocument(Resolver, folder, name)
                    ?? throw new IOException("O provedor não renomeou a pasta: " + from);
                var observed = Snapshot(ReadFiles(tree, cancellationToken));
                IUrbeVaultStorage.EnsureRenamed(baseline, observed, from, to);
                _baseline = observed;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<VaultFileRead?> ReadFileAsync(string path, long maxBytes, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tree is not { } tree) return null;
            return await Task.Run(() =>
            {
                AndroidUri? uri;
                try { uri = FindDocument(tree, path); }
                catch (InvalidDataException) { return null; }
                if (uri is null) return null;
                long size = -1;
                using (var cursor = Resolver.Query(uri, [DocumentsContract.Document.ColumnSize, DocumentsContract.Document.ColumnMimeType], null, null, null))
                    if (cursor is not null && cursor.MoveToFirst())
                    {
                        if (cursor.GetString(1) == DirectoryMime) return null;
                        if (!cursor.IsNull(0)) size = cursor.GetLong(0);
                    }
                if (size > maxBytes) return new VaultFileRead(size, null);
                using var stream = Resolver.OpenInputStream(uri);
                if (stream is null) return null;
                using var buffer = new MemoryStream();
                var chunk = new byte[64 * 1024];
                int read;
                while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (buffer.Length + read > maxBytes) return new VaultFileRead(Math.Max(size, buffer.Length + read), null);
                    buffer.Write(chunk, 0, read);
                }
                return new VaultFileRead(buffer.Length, buffer.ToArray());
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or Java.Lang.Exception) { return null; }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// The document of one file of the vault, for the content viewers inside the Urbe (ADD-0023): the player and the
    /// PDF reader open it directly, read only, through the permission the vault already has. Null when not there.
    /// </summary>
    internal async Task<AndroidUri?> DocumentOfAsync(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tree is not { } tree) return null;
            try { return await Task.Run(() => FindDocument(tree, path), cancellationToken).ConfigureAwait(false); }
            catch (InvalidDataException) { return null; }
        }
        catch (Exception ex) when (ex is IOException or Java.Lang.Exception) { return null; }
        finally { _gate.Release(); }
    }

    public async Task<bool> OpenExternallyAsync(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tree is not { } tree) return false;
            AndroidUri? uri;
            try { uri = await Task.Run(() => FindDocument(tree, path), cancellationToken).ConfigureAwait(false); }
            catch (InvalidDataException) { return false; }
            if (uri is null) return false;
            // the app holding the vault grants the viewer read access to this one document only
            var intent = new Intent(Intent.ActionView);
            intent.SetDataAndType(uri, VaultFileKinds.Mime(path));
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
            var chooser = Intent.CreateChooser(intent, "Abrir com")!;
            chooser.AddFlags(ActivityFlags.NewTask);
            Android.App.Application.Context.StartActivity(chooser);
            return true;
        }
        catch (Exception ex) when (ex is ActivityNotFoundException or Java.Lang.Exception) { return false; }
        finally { _gate.Release(); }
    }

    public bool CanShare => true;

    public async Task<bool> ShareAsync(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_tree is not { } tree) return false;
            AndroidUri? uri;
            try { uri = await Task.Run(() => FindDocument(tree, path), cancellationToken).ConfigureAwait(false); }
            catch (InvalidDataException) { return false; }
            if (uri is null) return false;
            // the receiving app may read this one document only
            var intent = new Intent(Intent.ActionSend);
            intent.SetType(VaultFileKinds.Mime(path));
            intent.PutExtra(Intent.ExtraStream, uri);
            intent.PutExtra(Intent.ExtraTitle, path[(path.LastIndexOf('/') + 1)..]);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission);
            var chooser = Intent.CreateChooser(intent, "Compartilhar")!;
            chooser.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
            Android.App.Application.Context.StartActivity(chooser);
            return true;
        }
        catch (Exception ex) when (ex is ActivityNotFoundException or Java.Lang.Exception) { return false; }
        finally { _gate.Release(); }
    }

    public async Task<bool> RemoveEmptyDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        IUrbeVaultStorage.EnsureWritableFolderPath(path);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Writes ONE user text file: creates it when <paramref name="create"/> and absent,
    /// else rewrites the existing one; same checks and verification for both.</summary>
    private async Task WriteTextFileAsync(string path, string content, bool create, CancellationToken cancellationToken, byte[]? expectedSha256 = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        IUrbeVaultStorage.EnsureWritableTextPath(path);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            if (create && baseline.ContainsKey(path))
                throw new IOException("Já existe um arquivo com esse nome: " + path);
            if (!create && !baseline.ContainsKey(path))
                throw new FileNotFoundException("O arquivo não existe na pasta.", path);
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken).ConfigureAwait(false);
            EnsureUnchanged(baseline, files);
            if (expectedSha256 is not null)
            {
                if (expectedSha256.Length != 32 || files.FirstOrDefault(f => string.Equals(f.Path, path, StringComparison.Ordinal)) is not { } current ||
                    !System.Security.Cryptography.SHA256.HashData(current.Bytes.Span).AsSpan().SequenceEqual(expectedSha256))
                    throw new IOException("Conflito: este mapa foi alterado por outro editor ou não é mais o arquivo aberto.");
            }
            var snapshot = VaultReader.Read(files, UrbeApp.InstalledVersion);
            if (snapshot.IsReadOnly || snapshot.FutureFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Formato futuro de vault ou arquivo protegido: escrita recusada.");
            var existing = snapshot.Documents.SingleOrDefault(d =>
                string.Equals(d.Path, path, StringComparison.OrdinalIgnoreCase));
            if (!create && existing?.Text is null)
                throw new InvalidDataException("O arquivo não pôde ser lido como texto seguro.");
            var target = existing?.Path ?? path;

            // The canonical writer must agree that the document is writable. This is a single-file
            // write: do NOT apply unrelated migration, journal or delete operations from the plan.
            // UC-60: the canonical writer's plan for this one file alone (the same refusals and bytes as the full plan).
            var plannedBytes = VaultWriter.PlanOne(files, snapshot, target, content)
                ?? throw new InvalidOperationException("O plano canônico recusou o arquivo.");
            var expected = Encoding.UTF8.GetBytes(content);
            // The writer can preserve UTF-8 BOM from an existing note; use its canonical bytes.
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
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Moves ONE text file (a renamed note): the new file is written and verified before the old
    /// one is deleted; a change of case only goes through a temporary copy; then every file is verified.</summary>
    public async Task MoveTextFileAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken).ConfigureAwait(false);
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
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Moves ONE text file to the trash: .urbe/trash.v2.json written and verified first, then the file
    /// deleted, then every file verified.</summary>
    public async Task TrashTextFileAsync(string path, string trashJson, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken).ConfigureAwait(false);
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
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task TrashTextFilesAsync(IReadOnlyList<string> paths, string trashJson, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken).ConfigureAwait(false);
            EnsureUnchanged(baseline, files);
            var (sources, trashPath, trashBytes) = IUrbeVaultStorage.PlanTrashMany(files, paths, trashJson);
            var create = !baseline.ContainsKey(trashPath);
            var gone = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var uris = sources.Select(s => FindDocument(tree, s) ?? throw new FileNotFoundException("O arquivo não existe na pasta.", s)).ToList();
                if (create && FindDocument(tree, trashPath) is not null)
                    throw new IOException("A pasta foi modificada fora do Urbe: " + trashPath);
                // the trash holds every text before any file goes
                WriteVerified(tree, trashPath, trashBytes);
                for (int i = 0; i < uris.Count; i++)
                    if (!DocumentsContract.DeleteDocument(Resolver, uris[i]))
                        throw new IOException("O provedor não removeu o arquivo: " + sources[i]);
                var observed = Snapshot(ReadFiles(tree, cancellationToken));
                if (observed.Count != baseline.Count - gone.Count + (create ? 1 : 0) || observed.Keys.Any(gone.Contains))
                    throw new IOException("Verificação pós-gravação encontrou arquivos inesperados.");
                foreach (var pair in baseline)
                {
                    if (gone.Contains(pair.Key)) continue;
                    var expected = string.Equals(pair.Key, trashPath, StringComparison.OrdinalIgnoreCase) ? trashBytes : pair.Value.Bytes.ToArray();
                    if (!observed.TryGetValue(pair.Key, out var actual) || !actual.Bytes.Span.SequenceEqual(expected))
                        throw new IOException("Verificação pós-gravação detectou divergência em: " + pair.Key);
                }
                if (!observed.TryGetValue(trashPath, out var written) || !written.Bytes.Span.SequenceEqual(trashBytes))
                    throw new IOException("A lixeira não passou na verificação: " + trashPath);
                _baseline = observed;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Rewrites .urbe/trash.v2.json only; the same checks and verification as the map.</summary>
    public Task SaveTrashAsync(string trashJson, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanTrashWrite(files, trashJson), "A lixeira", cancellationToken);

    public Task SaveMaintenanceAsync(System.Text.Json.Nodes.JsonObject maintenance, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanMaintenanceWrite(files, maintenance), "O registro de manutenção", cancellationToken);

    public Task SaveIdentityAsync(byte[] identityJson, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanIdentityWrite(files, identityJson), "A identidade das notas", cancellationToken);

    public Task SaveHistoryAsync(string historyJson, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanHistoryWrite(files, historyJson), "O histórico", cancellationToken);

    public Task SaveTutorialMarkAsync(string json, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanTutorialMarkWrite(files, json), "A marca do Tutorial", cancellationToken);

    public Task SaveNotesRecordAsync(string json, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanNotesRecordWrite(files, json), "O registro das notas", cancellationToken);

    public Task SaveAreaLayoutAsync(string json, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanAreaLayoutWrite(files, json), "A aparência das caixas", cancellationToken);

    public Task SaveConstellationAsync(string json, CancellationToken cancellationToken = default) =>
        SaveUrbeFileAsync(files => IUrbeVaultStorage.PlanConstellationWrite(files, json), "A Constelação", cancellationToken);

    /// <summary>One internal file of .urbe/ (the trash, the Tutorial's mark), planned on the files as they are now,
    /// written with verification of it and of every other file.</summary>
    private async Task SaveUrbeFileAsync(Func<IReadOnlyList<VaultFile>, (string, byte[])> plan, string what, CancellationToken cancellationToken, bool createOnly = false)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tree = _tree ?? throw new InvalidOperationException("Selecione a pasta do vault.");
            var baseline = _baseline ?? throw new InvalidOperationException("Reabra a pasta do vault.");
            var files = await Task.Run(() => ReadFiles(tree, cancellationToken), cancellationToken).ConfigureAwait(false);
            EnsureUnchanged(baseline, files);
            var (target, plannedBytes) = plan(files);
            var create = !baseline.ContainsKey(target);
            if (createOnly && !create) throw new IOException(what + " já existe: " + target);
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
            }, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private static ContentResolver Resolver =>
        Android.App.Application.Context.ContentResolver
        ?? throw new IOException("ContentResolver indisponível.");

    private sealed record Entry(string Name, string DocumentId, string MimeType, long Size = -1, long Modified = 0);

    private static AndroidUri Root(AndroidUri tree) =>
        DocumentsContract.BuildDocumentUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree))
        ?? throw new IOException("Não foi possível acessar a raiz da pasta selecionada.");

    private static IReadOnlyList<Entry> Children(AndroidUri tree, AndroidUri parent)
    {
        var id = DocumentsContract.GetDocumentId(parent);
        var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, id)
            ?? throw new IOException("O provedor não retornou a pasta de documentos.");
        using var cursor = Resolver.Query(children,
            [DocumentIdColumn, DisplayNameColumn, MimeTypeColumn, SizeColumn, LastModifiedColumn],
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

            long size = cursor.IsNull(3) ? -1 : cursor.GetLong(3);
            long modified = cursor.IsNull(4) ? 0 : cursor.GetLong(4);
            result.Add(new Entry(name, documentId, mime, size, modified));
        }
        return result;
    }

    private static AndroidUri DocumentUri(AndroidUri tree, string id) =>
        DocumentsContract.BuildDocumentUriUsingTree(tree, id)
        ?? throw new IOException("URI do documento não pôde ser construída.");

    /// <summary>How many files are read at once: the provider answers each one over IPC, so reading them together is what makes the opening quick.</summary>
    private const int ReadersAtOnce = 6;

    /// <summary>
    /// The vault's files, read together (observation of 22/10/2026: the opening was slow). <paramref name="listing"/>, when
    /// given, gets every file of the vault by name, hidden folders out, from the same walk (no second walk to list them).
    /// </summary>
    private static List<VaultFile> ReadFiles(AndroidUri tree, CancellationToken token, bool includeAll = false, List<string>? listing = null)
    {
        var wanted = new List<(string Path, AndroidUri Uri, Entry Entry)>();
        // no depth or item limit (DEC-0059); a folder met twice (a provider that loops) is walked once
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void Walk(AndroidUri dir, string prefix, int depth, bool hidden)
        {
            token.ThrowIfCancellationRequested();
            if (DocumentsContract.GetDocumentId(dir) is { } id && !visited.Add(id)) return;

            foreach (var entry in Children(tree, dir))
            {
                token.ThrowIfCancellationRequested();

                var path = prefix + entry.Name;
                var uri = DocumentUri(tree, entry.DocumentId);
                bool hide = hidden || entry.Name.StartsWith('.');
                if (entry.MimeType == DirectoryMime)
                {
                    Walk(uri, path + "/", depth + 1, hide);
                    continue;
                }
                if (!hide) listing?.Add(path);

                // Read canonical text and metadata; assets stay untouched on
                // disk, and are not rewritten/deleted by a text-only beta.
                if (!includeAll && !IsIncluded(path))
                    continue;
                if (!includeAll && entry.Size > InMemoryTextBytes && !IsSupportedNote(path) && !path.StartsWith(".urbe/", StringComparison.OrdinalIgnoreCase))
                    continue;
                wanted.Add((path, uri, entry));
            }
        }
        Walk(Root(tree), string.Empty, 0, false);

        var read = new byte[wanted.Count][];
        try
        {
            Parallel.For(0, wanted.Count, new ParallelOptions { MaxDegreeOfParallelism = ReadersAtOnce, CancellationToken = token }, i =>
            {
                read[i] = ReadListed(wanted[i].Uri, wanted[i].Entry);
            });
        }
        catch (System.AggregateException e) when (e.InnerExceptions.Count > 0)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerExceptions[0]).Throw();
        }
        var files = new List<VaultFile>(wanted.Count);
        for (int i = 0; i < wanted.Count; i++) files.Add(new VaultFile(wanted[i].Path, read[i]));
        return files;
    }

    private static IReadOnlyList<string> ReadFolders(
        AndroidUri tree, CancellationToken token)
    {
        var folders = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        void Walk(AndroidUri parent, string prefix, int depth)
        {
            token.ThrowIfCancellationRequested();

            var parentId = DocumentsContract.GetDocumentId(parent)
                ?? throw new IOException("Identificador de pasta Android indisponível.");
            if (!visited.Add(parentId))
                throw new IOException("Estrutura circular de pastas no provedor Android.");

            foreach (var child in Children(tree, parent))
            {
                token.ThrowIfCancellationRequested();

                if (child.MimeType != DirectoryMime)
                    continue;

                var path = prefix + child.Name;
                if (path.Split('/').Any(segment => segment.StartsWith('.')))
                    continue; // Internal .urbe tree is not an Explorer folder.

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
        VaultTextFiles.IsText(path);

    private static byte[] ReadBytes(AndroidUri uri)
    {
        using var stream = Resolver.OpenInputStream(uri)
            ?? throw new IOException("Arquivo não pode ser lido: " + uri);
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
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
            throw new VaultChangedOutsideException();
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
