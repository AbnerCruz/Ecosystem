using System.Text;
using Urbe.Core;
using Urbe.UI;

namespace Urbe.UI.Tests;

public sealed class WorkspaceSessionTests
{
    [Fact]
    public void VaultLoadFeedsExplorerWithFoldersAndAllVisibleArtifactKinds()
    {
        using var session = new WorkspaceSession();
        var snapshot = VaultReader.Read(
        [
            new VaultFile("Inbox.md", Encoding.UTF8.GetBytes("# Inbox")),
            new VaultFile("Pasta/Nota.md", Encoding.UTF8.GetBytes("texto")),
            new VaultFile("Páginas/Inicio.page.json", Encoding.UTF8.GetBytes("{}")),
            new VaultFile("imagem.png", [1, 2, 3]),
            new VaultFile(".urbe/vault.json", Encoding.UTF8.GetBytes("""{"formatVersion":1}"""))
        ]);

        session.Load(snapshot);

        var root = session.ExplorerEntries();

        Assert.Contains(root, entry => entry.IsFolder && entry.Name == "Pasta");
        Assert.Contains(root, entry => entry.IsFolder && entry.Name == "Páginas");
        Assert.Contains(root, entry => !entry.IsFolder && entry.Name == "Inbox.md");
        Assert.Contains(root, entry => !entry.IsFolder && entry.Name == "imagem.png");
        Assert.DoesNotContain(root, entry => entry.Path.StartsWith(".urbe", StringComparison.Ordinal));

        var pages = session.ExplorerEntries("Páginas");
        var page = Assert.Single(pages);
        Assert.Equal(ArtifactType.Page, page.Type);
        Assert.True(page.Editable);
    }

    [Fact]
    public void SourceAndVisualRoundTripUseCanonicalMarkdownDomain()
    {
        using var session = new WorkspaceSession();
        session.LoadDocuments(
        [
            new DocumentInput
            {
                Path = "Notas/A.md",
                Content = "---\ntags: [x]\n---\n# Antigo\n\n[[B]]"
            },
            new DocumentInput
            {
                Path = "Notas/B.md",
                Content = "# B"
            }
        ]);

        Assert.True(
            session.Open(
                "Notas/A.md",
                new EditorOpenOrigin(EditorOpenOriginKind.Explorer, "explorer")));
        Assert.Contains("<h1>Antigo</h1>", session.PreviewHtml, StringComparison.Ordinal);

        session.SetMode(EditorSurfaceMode.Source);
        var source = session.UpdateSource("---\ntags: [x]\n---\n# Novo\n\n[[B]]");

        Assert.NotNull(source);
        Assert.Equal(EditorSurfaceMode.Source, session.Mode);
        Assert.Contains("<h1>Novo</h1>", session.PreviewHtml, StringComparison.Ordinal);
        Assert.Equal("B", Assert.Single(session.Links()).Title);

        session.SetMode(EditorSurfaceMode.Visual);
        var visual = session.UpdateVisualHtml("<h1>Visual</h1><p><strong>forte</strong></p>");

        Assert.NotNull(visual);
        Assert.StartsWith("---\ntags: [x]\n---", visual!.Content, StringComparison.Ordinal);
        Assert.Contains("# Visual", visual.Content, StringComparison.Ordinal);
        Assert.Contains("**forte**", visual.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorOriginStackReturnsToTheActualOpeningSurface()
    {
        using var session = new WorkspaceSession();
        session.LoadDocuments(
        [
            new DocumentInput { Path = "A.md", Content = "[[B]]" },
            new DocumentInput { Path = "B.md", Content = "B" }
        ]);

        Assert.True(
            session.Open(
                "A.md",
                new EditorOpenOrigin(EditorOpenOriginKind.Search, "explorer?query=a")));
        Assert.True(session.OpenLinked("B", "editor?path=A.md"));

        Assert.True(session.TryPopOrigin(out var linkOrigin));
        Assert.Equal(EditorOpenOriginKind.Link, linkOrigin.Kind);
        Assert.Equal("editor?path=A.md", linkOrigin.Href);

        Assert.True(session.TryPopOrigin(out var searchOrigin));
        Assert.Equal(EditorOpenOriginKind.Search, searchOrigin.Kind);
        Assert.Equal("explorer?query=a", searchOrigin.Href);
    }

    [Fact]
    public void ExplorerCreationIsCollisionSafeAndSearchUsesKnowledgeIndex()
    {
        using var session = new WorkspaceSession();
        session.LoadDocuments(
        [
            new DocumentInput { Path = "Ideias/Nova.md", Content = "primeira" }
        ]);

        var created = session.CreateNote("Nova", "Ideias", "conteúdo roma");
        var second = session.CreateNote("Nova", "Ideias", "outro");

        Assert.NotNull(created);
        Assert.NotNull(second);
        Assert.Equal("Ideias/Nova-2.md", created!.Path);
        Assert.Equal("Ideias/Nova-3.md", second!.Path);
        Assert.Contains(
            session.Search("roma"),
            document => document.Id == created.Id);
    }

    [Fact]
    public void ReadOnlyVaultBlocksEditsAndCreation()
    {
        using var session = new WorkspaceSession();
        session.LoadDocuments(
        [
            new DocumentInput { Path = "A.md", Content = "a" }
        ],
        readOnly: true);

        Assert.True(
            session.Open(
                "A.md",
                new EditorOpenOrigin(EditorOpenOriginKind.Explorer, "explorer")));

        Assert.Null(session.UpdateSource("b"));
        Assert.Null(session.UpdateVisualHtml("<p>b</p>"));
        Assert.Null(session.CreateNote("B"));
        Assert.Equal("a", session.CurrentDocument!.Content);
    }
}
