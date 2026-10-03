using System.Net;
using Hub.Core;

namespace Hub.Tests;

public sealed class ReleaseNotesTests
{
    [Theory]
    [InlineData("## Added\n- Editor", ReleaseNoteKind.Highlights)]
    [InlineData("## Novidades\n- Editor", ReleaseNoteKind.Highlights)]
    [InlineData("## Changed\n- Mais rápido", ReleaseNoteKind.Improvements)]
    [InlineData("## Melhorias\n- Mais rápido", ReleaseNoteKind.Improvements)]
    [InlineData("## Fixed\n- Salvar", ReleaseNoteKind.Fixes)]
    [InlineData("## Correções\n- Salvar", ReleaseNoteKind.Fixes)]
    [InlineData("## Breaking changes\n- Formato", ReleaseNoteKind.BreakingChanges)]
    [InlineData("## Detalhes técnicos\n- Testes", ReleaseNoteKind.Technical)]
    public void RecognizesCategoriesWithoutChangingSource(string markdown, ReleaseNoteKind expected)
    {
        var parsed = ReleaseNotesParser.Parse(markdown);
        Assert.True(parsed.Structured); Assert.Equal(markdown, parsed.Markdown);
        Assert.Equal(expected, Assert.Single(parsed.Sections).Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n\t")]
    public void MissingBodyDoesNotFabricateNotes(string? body)
    { var parsed = ReleaseNotesParser.Parse(body); Assert.False(parsed.Structured); Assert.Empty(parsed.Sections); }

    [Theory]
    [InlineData("Texto **livre**, com [link](https://example.invalid/release).")]
    [InlineData("## Experimentação\nUma observação sem template.")]
    [InlineData("```markdown\n## Fixed\nExemplo literal\n```")]
    public void FreeTextAndFencedHeadingsArePreserved(string markdown)
    { var parsed = ReleaseNotesParser.Parse(markdown); Assert.False(parsed.Structured); Assert.Equal(markdown, Assert.Single(parsed.Sections).Markdown); }

    [Fact]
    public void MixedKnownUnknownNestedAndRepeatedSectionsLoseNoContent()
    {
        var body = "Introdução\n## Added\n- Editor\n### Canvas\nExplicação\n## Observações\nNão categorizado\n## Fixed\n- Salvar\n## Fixed\n- Selecionar\n## Technical\ncommit abc";
        var parsed = ReleaseNotesParser.Parse(body);
        Assert.Equal([ReleaseNoteKind.FreeText, ReleaseNoteKind.Highlights, ReleaseNoteKind.FreeText,
            ReleaseNoteKind.Fixes, ReleaseNoteKind.Fixes, ReleaseNoteKind.Technical], parsed.Sections.Select(s => s.Kind));
        Assert.Contains("### Canvas", parsed.Sections[1].Markdown);
        Assert.Equal("Observações", parsed.Sections[2].Heading);
        Assert.Contains("Selecionar", parsed.Sections[4].Markdown);
        Assert.Equal(body, parsed.Markdown);
    }

    [Fact]
    public void NativeMarkdownPreservesFormattingListsCodeEntitiesTablesAndSafeLinks()
    {
        var blocks = MarkdownPresentation.Build("# Título\n\nTexto **forte** e *leve* &amp; `código` [site](https://example.invalid/a).\n\n- Um\n- Dois\n\n3. Três\n4. Quatro\n\n```cs\nvar x = 1;\n```\n\n> Citação\n\n| Nome | Valor |\n| --- | --- |\n| A | B |\n");
        Assert.Equal("Título", blocks[0].Text);
        Assert.Contains(blocks.SelectMany(b => b.Runs), r => r.Text == "forte" && r.Bold);
        Assert.Contains(blocks.SelectMany(b => b.Runs), r => r.Text == "leve" && r.Italic);
        Assert.Contains(blocks.SelectMany(b => b.Runs), r => r.Text == "código" && r.Code);
        Assert.Contains(blocks.SelectMany(b => b.Runs), r => r.Text == "site" && r.Url == "https://example.invalid/a");
        Assert.Contains("&", string.Concat(blocks.Select(b => b.Text)));
        Assert.Contains(blocks, b => b.Text == "• Um");
        Assert.Contains(blocks, b => b.Text == "3. Três");
        Assert.Contains(blocks, b => b.Style == "code" && b.Text.Contains("var x = 1;"));
        Assert.Contains(blocks, b => b.Style == "quote" && b.Text == "Citação");
        Assert.Contains(blocks, b => b.Text == "A · B");
    }

    [Fact]
    public void HtmlAndImagesNeverBecomeExecutableOrRemoteRequests()
    {
        var blocks = MarkdownPresentation.Build("<script>alert(1)</script>\n\n[perigo](javascript:alert) ![desenho](https://example.invalid/p.png)");
        Assert.Contains(blocks, b => b.Text.Contains("<script>"));
        Assert.Contains("[imagem: desenho]", string.Concat(blocks.Select(b => b.Text)));
        Assert.All(blocks.SelectMany(b => b.Runs), r => Assert.Null(r.Url));
        Assert.Null(MarkdownPresentation.SafeLink("https://name:pass@example.invalid"));
        Assert.Null(MarkdownPresentation.SafeLink("file:///tmp/a"));
        Assert.Null(MarkdownPresentation.SafeLink("./relative.md"));
    }

    sealed class Handler(string body, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }); }
    }
    static async Task<Datum<IReadOnlyList<ReleaseInfo>>> Read(string json, HttpStatusCode status = HttpStatusCode.OK)
        => await new GitHubReader(new HttpClient(new Handler(json, status)), new("acme", "alpha"))
            .ReadReleasesAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task ReaderPreservesMarkdownNoBodyPrereleaseDateAndExcludesDraft()
    {
        var data = await Read("""[{"tag_name":"v1.0.0","published_at":"2026-10-01T00:00:00Z","body":"## Fixed\n- Save"},{"tag_name":"v2.0.0-beta","prerelease":true,"published_at":"2026-10-02T00:00:00Z","body":null},{"tag_name":"v3.0.0","draft":true,"body":"secret"}]""");
        Assert.Equal(Availability.Derived, data.Availability);
        Assert.Equal(["v2.0.0-beta", "v1.0.0"], data.Value!.Select(r => r.Tag));
        Assert.True(data.Value![0].Prerelease); Assert.Null(data.Value![0].BodyMarkdown);
        Assert.Equal("## Fixed\n- Save", data.Value![1].BodyMarkdown);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("{}")]
    [InlineData("[{}]")]
    [InlineData("[null]")]
    public async Task InvalidProviderResponseIsUnavailableInsteadOfEmpty(string json)
    { var data = await Read(json); Assert.Equal(Availability.NotAvailable, data.Availability); Assert.Null(data.Value); }

    [Fact]
    public async Task EmptyAndProviderUnavailableRemainDistinct()
    {
        Assert.Empty((await Read("[]")).Value!);
        Assert.Equal(Availability.NotAvailable, (await Read("[]", HttpStatusCode.ServiceUnavailable)).Availability);
    }

    [Fact]
    public async Task ReaderNeverFetchesUnboundedHistory()
    {
        var json = "[" + string.Join(',', Enumerable.Range(1, 105).Select(i => "{\"tag_name\":\"v1.0." + i + "\"}")) + "]";
        var data = await Read(json); Assert.Equal(100, data.Value!.Count);
    }
}
