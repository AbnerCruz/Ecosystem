using System.Text;
using Urbe.Core;
using Urbe.UI;

namespace Urbe.Core.Tests;

public sealed class WorkspacePhysicalFoldersTests
{
    [Fact]
    public void EmptyPhysicalBairrosAppearAfterReopeningVaultWithoutSyntheticDocuments()
    {
        using var session = new WorkspaceSession();
        var snapshot = VaultReader.Read(Array.Empty<VaultFile>());

        session.Load(snapshot, ["Bairro", "Bairro/Vazio", "Outros"]);

        Assert.Empty(session.Paths);
        Assert.Empty(session.Documents.List());
        Assert.Contains(session.ExplorerEntries(), item =>
            item.IsFolder && item.Path == "Bairro");
        Assert.Contains(session.ExplorerEntries("Bairro"), item =>
            item.IsFolder && item.Path == "Bairro/Vazio");

        // This is what Android startup does after relisting the SAF tree.
        session.Load(VaultReader.Read(Array.Empty<VaultFile>()),
            ["Bairro", "Bairro/Vazio", "Outros"]);

        Assert.Contains("Bairro/Vazio", session.Folders);
        Assert.Contains(session.ExplorerEntries("Bairro"), item =>
            item.IsFolder && item.Name == "Vazio");
        Assert.Empty(session.PendingMutations);
    }

    [Fact]
    public void IgnoreSystemTraversalAndMalformedPhysicalFolderPaths()
    {
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read([
            new VaultFile("Bairro/nota.md", Encoding.UTF8.GetBytes("Olá"))
        ]), [
            "Bairro",
            "Bairro/Vazio",
            ".urbe/backup",
            "../escape",
            "Bairro/../fora",
            "Bairro//duplicada",
            "/absoluta",
            "Bairro/.",
            "Bairro/nota.md"
        ]);

        Assert.Contains("Bairro", session.Folders);
        Assert.Contains("Bairro/Vazio", session.Folders);
        Assert.DoesNotContain(session.Folders, path =>
            path.StartsWith(".urbe", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("..", StringComparison.Ordinal) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.EndsWith("nota.md", StringComparison.Ordinal));
        Assert.Contains(session.ExplorerEntries("Bairro"), entry =>
            !entry.IsFolder && entry.Path == "Bairro/nota.md");
    }
}
