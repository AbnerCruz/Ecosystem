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
    public void CityLotsRepresentPhysicalBairrosAndMarkdownHouses()
    {
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read([
            new VaultFile("Centro/Casa.md", Encoding.UTF8.GetBytes("# Minha casa")),
            new VaultFile("Centro/Rua/Fundo.md", Encoding.UTF8.GetBytes("# Outra casa"))
        ]), ["Centro", "Centro/Praca", "Vazio"]);

        var city = session.ExplorerEntries();
        Assert.Contains(city, item => item.IsFolder && item.Path == "Centro");
        Assert.Contains(city, item => item.IsFolder && item.Path == "Vazio");

        var centro = session.ExplorerEntries("Centro");
        Assert.Contains(centro, item => item.IsFolder && item.Path == "Centro/Praca");
        Assert.Contains(centro, item => item.IsFolder && item.Path == "Centro/Rua");
        Assert.Contains(centro, item => !item.IsFolder && item.Path == "Centro/Casa.md");

        var created = session.CreateNote("Nova casa", "Centro");
        Assert.NotNull(created);
        Assert.True(session.HasUnsavedChanges);
        Assert.Contains(session.ExplorerEntries("Centro"), item =>
            !item.IsFolder && item.Path == created!.Path);
        Assert.True(session.Open(created!.Path,
            new EditorOpenOrigin(EditorOpenOriginKind.Explorer, "cidade")));
        Assert.Equal(created.Id, session.CurrentDocument?.Id);
    }

    [Fact]
    public void ActualEditsBecomeDirtyButNavigationAndLoadingDoNot()
    {
        using var session = new WorkspaceSession();
        var clean = VaultReader.Read([
            new VaultFile("Notas/Primeira.md", Encoding.UTF8.GetBytes("Versão inicial"))
        ]);

        session.Load(clean, ["Notas", "Vazio"]);
        Assert.False(session.HasUnsavedChanges);
        Assert.True(session.SetFolder("Notas"));
        Assert.True(session.Restore("Notas/Primeira.md"));
        Assert.False(session.HasUnsavedChanges);

        var before = session.PersistenceRevision;
        Assert.NotNull(session.UpdateSource("Versão editada"));
        Assert.True(session.HasUnsavedChanges);
        Assert.True(session.PersistenceRevision > before);

        session.MarkSaved();
        Assert.False(session.HasUnsavedChanges);
        Assert.Empty(session.PendingMutations);

        Assert.NotNull(session.CreateNote("Segunda", "Notas"));
        Assert.True(session.HasUnsavedChanges);
        session.MarkSaved();
        Assert.False(session.HasUnsavedChanges);

        Assert.NotNull(session.CreateFolder("Nova"));
        Assert.True(session.HasUnsavedChanges);
        session.MarkSaved();
        Assert.False(session.HasUnsavedChanges);

        Assert.True(session.MoveItem("Notas/Primeira.md", "Nova"));
        Assert.True(session.HasUnsavedChanges);
        session.MarkSaved();
        Assert.False(session.HasUnsavedChanges);

        session.Load(clean, ["Notas", "Vazio"]);
        Assert.False(session.HasUnsavedChanges);
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
