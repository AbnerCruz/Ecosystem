using System.Text;
using Urbe.Core;
using Urbe.UI;

namespace Urbe.Core.Tests;

public sealed class WikiLinkCompletionTests
{
    [Theory]
    [InlineData("[[", 2, "", 0, 2)]
    [InlineData("texto [[Not", 11, "Not", 6, 11)]
    [InlineData("[[Note]]", 4, "No", 0, 8)]
    [InlineData("A [[nota]] B", 8, "nota", 2, 10)]
    public void FindsOnlyWikilinkAtCurrentCaret(
        string markdown, int cursor, string query, int start, int end)
    {
        var result = WikiLinkCompletion.Find(markdown, cursor);
        Assert.NotNull(result);
        Assert.Equal(query, result.Query);
        Assert.Equal(start, result.Start);
        Assert.Equal(end, result.End);
    }

    [Theory]
    [InlineData("normal", 3)]
    [InlineData("[[a]] depois", 11)]
    [InlineData("[[a\nb", 5)]
    [InlineData("[[name|alias]]", 12)]
    [InlineData("antes [[a]] [[outra]]", 11)]
    public void RejectsCaretOutsideActiveWikilink(string markdown, int cursor)
    {
        Assert.Null(WikiLinkCompletion.Find(markdown, cursor));
    }

    [Fact]
    public void CompletingExistingLinkPreservesOutsideMarkdownAndCaret()
    {
        var markdown = "# Livro\nAbra [[Velho]] e leia.";
        var context = WikiLinkCompletion.Find(markdown, 18);
        Assert.NotNull(context);
        var completion = WikiLinkCompletion.Complete(
            markdown, context, "Centro/Documento");
        Assert.NotNull(completion);
        Assert.Equal("# Livro\nAbra [[Centro/Documento]] e leia.",
            completion.Markdown);
        Assert.Equal(completion.Markdown.IndexOf("]]", StringComparison.Ordinal) + 2,
            completion.Cursor);
    }

    [Fact]
    public void StaleCaretOrInvalidTargetCannotReplaceOtherText()
    {
        const string original = "Texto [[Algum";
        var context = WikiLinkCompletion.Find(original, original.Length);
        Assert.NotNull(context);
        Assert.Null(WikiLinkCompletion.Complete(original.Replace("Algum", "Outra"), context, "Destino"));
        Assert.Null(WikiLinkCompletion.Complete(original, context, "[[nested]]"));
        Assert.Null(WikiLinkCompletion.Complete(original, context, "Outra\nnota"));
        Assert.Equal(original, original);
    }

    [Fact]
    public void NewNoteLinkedInSameBairroProducesResolvableWikiLink()
    {
        using var session = new WorkspaceSession();
        session.Load(VaultReader.Read([
            new VaultFile("Centro/Origem.md", Encoding.UTF8.GetBytes("Visite [[Casa"))
        ]), ["Centro"]);
        Assert.True(session.Restore("Centro/Origem.md"));
        var source = session.CurrentDocument!;
        var context = WikiLinkCompletion.Find(source.Content, source.Content.Length);
        Assert.NotNull(context);

        var created = session.CreateLinkedNote(context.Query);
        Assert.NotNull(created);
        Assert.Equal("Centro/Casa.md", created.Path);

        var completion = WikiLinkCompletion.Complete(source.Content, context,
            ArtifactModel.WithoutNoteExtension(created.Path));
        Assert.NotNull(completion);
        Assert.NotNull(session.UpdateSource(completion.Markdown));
        Assert.Contains(created, session.Links());
        Assert.True(session.HasUnsavedChanges);
    }
}
