using Android.Content;
using Android.Provider;
using Lunet.Core;
using Uri = Android.Net.Uri;

namespace Lunet.Android;

/// <summary>Direct access through the granted tree, with document IDs resolved by the provider.</summary>
internal sealed class AndroidSafProjectFiles : IProjectFiles
{
    private readonly ContentResolver resolver;
    private readonly Uri tree;
    private readonly string prefix;
    private readonly FolderIndex index;

    private sealed class FolderIndex
    {
        public readonly Dictionary<string, (long Time, List<Entry> Entries)> Children = new(StringComparer.Ordinal);
    }

    public AndroidSafProjectFiles(ContentResolver resolver, Uri tree) : this(resolver, tree, "", new FolderIndex()) { }

    private AndroidSafProjectFiles(ContentResolver resolver, Uri tree, string prefix, FolderIndex index)
    {
        this.resolver = resolver;
        this.tree = tree;
        this.prefix = prefix;
        this.index = index;
    }
    private const string DirectoryMime = "vnd.android.document/directory";
    private const string TemporaryPrefix = ".lunet-write-";
    private const string BackupPrefix = TemporaryPrefix + "backup-";
    private sealed record Entry(string Name, string Id, bool IsDirectory);
    public string TreeKey => tree.ToString()!;
    public string FolderPath => prefix;
    public string Location => TreeKey + "/" + prefix;
    public string? LocalDirectory => null;

    private string PathOf(string path) => prefix.Length == 0 ? path : Join(prefix, path);
    private static string Join(string folder, string name) => folder.Length == 0 ? name : name.Length == 0 ? folder : folder + "/" + name;
    private static string NameOf(string path) => path[(path.LastIndexOf('/') + 1)..];
    private static string ParentOf(string path) => path[..Math.Max(0, path.LastIndexOf('/'))];
    private Uri Document(string id) => DocumentsContract.BuildDocumentUriUsingTree(tree, id)
        ?? throw new IOException("Documento indisponível.");
    private string Root => DocumentsContract.GetTreeDocumentId(tree) ?? throw new IOException("Pasta inválida.");

    private T Access<T>(Func<T> operation)
    {
        try { lock (index) return operation(); }
        catch (Java.Lang.SecurityException ex) { throw new UnauthorizedAccessException("Reconecte a pasta de projetos.", ex); }
        catch (Java.Lang.Exception ex) { throw new IOException(ex.Message, ex); }
    }

    private List<Entry> Children(string parent)
    {
        var now = Environment.TickCount64;
        if (index.Children.TryGetValue(parent, out var cached) && now - cached.Time < 250)
            return cached.Entries;
        using var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, parent);
        using var reader = resolver.Query(children!, ["_display_name", "document_id", "mime_type"], null, null, null)
            ?? throw new IOException("Não foi possível listar a pasta.");
        var list = new List<Entry>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (reader.MoveToNext())
        {
            if (reader.GetString(0) is { } name && reader.GetString(1) is { } id)
            {
                if (!names.Add(name)) throw new IOException($"O provedor apresentou mais de um item chamado {name}.");
                list.Add(new(name, id, reader.GetString(2) == DirectoryMime));
            }
        }
        index.Children[parent] = (now, list);
        return list;
    }

    private Entry? Resolve(string path, bool createFolders = false)
    {
        var entry = new Entry("", Root, true);
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part is "." or ".." || part.Contains('\\')) throw new IOException("Caminho inválido.");
            if (!entry.IsDirectory) return null;
            var next = Children(entry.Id).Find(item => item.Name == part);
            if (next is null && createFolders)
            {
                using var created = Create(entry.Id, DirectoryMime, part);
                next = new Entry(part, DocumentsContract.GetDocumentId(created)!, true);
            }
            if (next is null) return null;
            entry = next;
        }
        return entry;
    }

    private Uri Create(string parent, string mime, string name)
    {
        using var folder = Document(parent);
        var created = DocumentsContract.CreateDocument(resolver, folder, mime, name)
            ?? throw new IOException("O provedor recusou criar " + name + ".");
        index.Children.Remove(parent);
        if (Children(parent).Any(e => e.Name == name && e.Id == DocumentsContract.GetDocumentId(created)))
            return created;
        TryDelete(created);
        created.Dispose();
        throw new IOException("O provedor alterou o nome de " + name + ".");
    }

    public IReadOnlyList<ProjectFileEntry> List(string folder) => Access<IReadOnlyList<ProjectFileEntry>>(() =>
    {
        if (Resolve(PathOf(folder)) is not { IsDirectory: true } parent) return [];
        var entries = Children(parent.Id);
        var listed = entries.Where(e => !e.Name.StartsWith(TemporaryPrefix, StringComparison.Ordinal))
            .ToDictionary(e => e.Name, e => new ProjectFileEntry(e.Name, e.IsDirectory), StringComparer.Ordinal);
        // If the process died between the two renames, expose the previous version under its original name.
        foreach (var backup in entries.Where(e => !e.IsDirectory && e.Name.StartsWith(BackupPrefix, StringComparison.Ordinal)))
        {
            var name = backup.Name[BackupPrefix.Length..];
            if (name.Length > 0) listed.TryAdd(name, new ProjectFileEntry(name, false));
        }
        return listed.Values.ToArray();
    });

    public bool FileExists(string path) => Access(() => ResolveFile(PathOf(path)) is { IsDirectory: false });
    public bool DirectoryExists(string path) => Access(() => Resolve(PathOf(path)) is { IsDirectory: true });
    public byte[] Read(string path) => Access(() =>
    {
        var entry = ResolveFile(PathOf(path)) ?? throw new FileNotFoundException("Arquivo indisponível.", path);
        using var document = Document(entry.Id);
        return ReadDocument(document);
    });

    private Entry? ResolveFile(string path) => Resolve(path) ?? Resolve(BackupOf(path));
    private static string BackupOf(string path) => Join(ParentOf(path), BackupPrefix + NameOf(path));

    private byte[] ReadDocument(Uri document)
    {
        using var stream = resolver.OpenInputStream(document) ?? throw new IOException("Não foi possível reler o arquivo.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    public void Write(string path, byte[] bytes) => Access(() =>
    {
        index.Children.Clear();
        var full = PathOf(path);
        var name = NameOf(full);
        var parent = Resolve(ParentOf(full), true)!;
        if (!parent.IsDirectory) throw new IOException("O destino não é uma pasta.");
        var backupName = NameOf(BackupOf(full));
        var existing = Resolve(full);
        var backup = Resolve(BackupOf(full));
        if (existing is { IsDirectory: true }) throw new IOException("Já existe uma pasta com esse nome.");
        using var pending = Create(parent.Id, "application/octet-stream", TemporaryPrefix + Guid.NewGuid().ToString("N"));
        Uri? previous = backup is null ? null : Document(backup.Id);
        Uri? installed = null;
        try
        {
            using (var stream = resolver.OpenOutputStream(pending, "wt") ?? throw new IOException("Não foi possível escrever."))
                stream.Write(bytes);
            if (!ReadDocument(pending).AsSpan().SequenceEqual(bytes))
                throw new IOException("A gravação ficou incompleta.");
            if (existing is not null)
            {
                if (previous is not null) { DeleteDocument(previous); previous.Dispose(); previous = null; }
                using var old = Document(existing.Id);
                previous = Rename(old, backupName);
            }
            installed = Rename(pending, name);
            if (Resolve(full) is not { } current || current.Id != DocumentsContract.GetDocumentId(installed)
                || !ReadDocument(installed).AsSpan().SequenceEqual(bytes))
                throw new IOException("O arquivo publicado não conferiu.");
            if (previous is not null) TryDelete(previous);
            return true;
        }
        catch
        {
            if (installed is not null) TryDelete(installed);
            else TryDelete(pending);
            if (previous is not null && Resolve(full) is null)
            {
                using var restored = Rename(previous, name);
            }
            throw;
        }
        finally
        {
            previous?.Dispose();
            installed?.Dispose();
        }
    });

    public void CreateDirectory(string path) => Access(() =>
        Resolve(PathOf(path), true) is { IsDirectory: true } ? true : throw new IOException("O destino não é uma pasta."));

    public void Delete(string path) => Access(() =>
    {
        index.Children.Clear();
        var full = PathOf(path);
        if (path.Length == 0) throw new IOException("Não é possível excluir a pasta raiz.");
        if (Resolve(full) is { } entry)
        {
            using var document = Document(entry.Id);
            DeleteDocument(document);
        }
        if (Resolve(BackupOf(full)) is { } backup)
        {
            using var document = Document(backup.Id);
            DeleteDocument(document);
        }
        return true;
    });

    public void Move(string from, string to) => Access(() =>
    {
        index.Children.Clear();
        var source = PathOf(from);
        var target = PathOf(to);
        var entry = ResolveFile(source) ?? throw new IOException("O item de origem não existe.");
        if (ResolveFile(target) is not null) throw new IOException("O destino já existe.");
        using var document = Document(entry.Id);
        if (ParentOf(source) == ParentOf(target))
        {
            using var renamed = Rename(document, NameOf(target));
            if (Resolve(target)?.Id == DocumentsContract.GetDocumentId(renamed)) return true;
            using var restored = Rename(renamed, entry.Name);
            throw new IOException("O provedor alterou o nome do destino.");
        }
        var origin = Resolve(ParentOf(source))!;
        var destination = Resolve(ParentOf(target), true)!;
        using var originUri = Document(origin.Id);
        using var destinationUri = Document(destination.Id);
        var rename = entry.Name != NameOf(target);
        if (rename && Children(destination.Id).Any(e => e.Name == entry.Name))
            throw new IOException("Já existe um item com o nome original na pasta de destino.");
        using var moved = DocumentsContract.MoveDocument(resolver, document, originUri, destinationUri)
            ?? throw new IOException("O provedor não permite mover o item entre pastas.");
        index.Children.Clear();
        if (rename)
        {
            try
            {
                using var renamed = Rename(moved, NameOf(target));
                if (Resolve(target)?.Id != DocumentsContract.GetDocumentId(renamed))
                {
                    using var restored = Rename(renamed, entry.Name);
                    throw new IOException("O provedor alterou o nome do destino.");
                }
            }
            catch
            {
                using var rolledBack = DocumentsContract.MoveDocument(resolver, moved, destinationUri, originUri);
                throw;
            }
        }
        return true;
    });

    private Uri Rename(Uri document, string name)
    {
        index.Children.Clear();
        return DocumentsContract.RenameDocument(resolver, document, name)
            ?? throw new IOException("O provedor não permite renomear " + name + ".");
    }
    private void DeleteDocument(Uri document)
    {
        index.Children.Clear();
        if (!DocumentsContract.DeleteDocument(resolver, document)) throw new IOException("O provedor não excluiu o item.");
    }
    private void TryDelete(Uri document)
    {
        index.Children.Clear();
        try { DocumentsContract.DeleteDocument(resolver, document); }
        catch (Java.Lang.Exception) { }
    }

    public IProjectFiles Sub(string folder) => new AndroidSafProjectFiles(resolver, tree, PathOf(folder), index);
}
