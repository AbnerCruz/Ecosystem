using Android.Content;
using Android.Provider;
using Lunet.Core;
using Uri = Android.Net.Uri;

namespace Lunet.Android;

/// <summary>Revision I/O through DocumentsContract. The provider's document IDs remain opaque.</summary>
internal sealed class AndroidProjectFolder(ContentResolver resolver, Uri tree) : IProjectFolderTree
{
    private const string PendingPrefix = "lunet-pending-";
    private const string RevisionPrefix = "lunet-revision-";

    private Uri Root => DocumentsContract.BuildDocumentUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree))!;

    public void CheckPermission()
    {
        if (resolver.PersistedUriPermissions?.Any(p => p.Uri?.Equals(tree) == true && p.IsReadPermission && p.IsWritePermission) != true)
            throw new UnauthorizedAccessException("Reconecte a pasta para autorizar leitura e escrita.");
    }

    public string? Latest() => Revisions().FirstOrDefault();

    public IReadOnlyList<string> Revisions()
    {
        CheckPermission();
        using var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree));
        using var cursor = resolver.Query(children!, ["_display_name", "document_id"], null, null, null)
            ?? throw new IOException("A pasta está indisponível.");
        var revisions = new List<string>();
        while (cursor.MoveToNext())
        {
            var name = cursor.GetString(0);
            if (name is not null && IsRevision(name)) revisions.Add(name);
        }
        return revisions.OrderDescending(StringComparer.Ordinal).ToArray();
    }

    public string Publish(string archive, string digest, string? expectedRevision)
    {
        if (Latest() != expectedRevision)
            throw new IOException("A pasta tem outra revisão. Preserve as duas cópias e confira qual deseja recuperar.");
        var ticks = DateTime.UtcNow.Ticks;
        if (expectedRevision is not null && long.TryParse(expectedRevision.AsSpan(RevisionPrefix.Length, 19), out var parentTicks)
            && parentTicks < DateTime.MaxValue.Ticks)
            ticks = Math.Max(ticks, parentTicks + 1);
        var name = RevisionPrefix + ticks.ToString("D19", System.Globalization.CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N") + "-" + digest + ".zip";
        using var root = Root;
        using var pending = DocumentsContract.CreateDocument(resolver, root, "application/zip",
            PendingPrefix + Guid.NewGuid().ToString("N") + ".zip")
            ?? throw new IOException("O provedor recusou criar a revisão.");
        Uri? published = null;
        try
        {
            using (var local = File.OpenRead(archive))
            using (var remote = resolver.OpenOutputStream(pending, "w") ?? throw new IOException("A pasta não aceita gravação."))
                local.CopyTo(remote);

            Verify(pending, digest);
            if (Latest() != expectedRevision)
                throw new IOException("A pasta mudou durante a gravação. A versão anterior foi preservada.");

            published = DocumentsContract.RenameDocument(resolver, pending, name)
                ?? throw new IOException("O provedor não permite concluir a revisão por renomeação.");
            Verify(published, digest);
            if (Find(name) is not { } installed)
                throw new IOException("O provedor alterou o nome da revisão.");
            installed.Dispose();
            return name;
        }
        catch
        {
            try { DocumentsContract.DeleteDocument(resolver, published ?? pending); }
            catch (Exception ex) when (ex is Java.Lang.Exception or IOException or UnauthorizedAccessException) { }
            throw;
        }
        finally { published?.Dispose(); }
    }

    public void Restore(string name, string archive, string destination)
    {
        if (!IsRevision(name)) throw new InvalidDataException("Nome de revisão inválido.");
        using var document = Find(name) ?? throw new IOException("A revisão selecionada deixou de estar disponível.");
        using (var remote = resolver.OpenInputStream(document) ?? throw new IOException("A revisão não pôde ser lida."))
        using (var local = File.Create(archive))
            remote.CopyTo(local);
        ProjectFolderSnapshot.Restore(archive, DigestInName(name), destination);
    }

    public void VerifyRevision(string name, string digest)
    {
        using var document = Find(name) ?? throw new IOException("A revisão deixou de estar disponível.");
        Verify(document, digest);
    }

    private Uri? Find(string name)
    {
        using var children = DocumentsContract.BuildChildDocumentsUriUsingTree(tree, DocumentsContract.GetTreeDocumentId(tree));
        using var cursor = resolver.Query(children!, ["_display_name", "document_id"], null, null, null)
            ?? throw new IOException("A pasta está indisponível.");
        while (cursor.MoveToNext())
        {
            if (cursor.GetString(0) != name) continue;
            return DocumentsContract.BuildDocumentUriUsingTree(tree, cursor.GetString(1));
        }
        return null;
    }

    private void Verify(Uri document, string digest)
    {
        using var stream = resolver.OpenInputStream(document) ?? throw new IOException("Não foi possível reler a revisão.");
        var actual = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(stream));
        if (actual != digest) throw new IOException("A gravação no provedor ficou incompleta.");
    }

    private static bool IsRevision(string name) => name.StartsWith(RevisionPrefix, StringComparison.Ordinal)
        && name.Length == RevisionPrefix.Length + 19 + 1 + 32 + 1 + 64 + 4
        && name.EndsWith(".zip", StringComparison.Ordinal)
        && name.AsSpan(RevisionPrefix.Length, 19).IndexOfAnyExceptInRange('0', '9') < 0
        && name.AsSpan(name.Length - 68, 64).ToString().All(char.IsAsciiHexDigitLower);

    private static string DigestInName(string name) => name[^68..^4];
}
