using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class NoteTemplateEngineTests
{
    [Fact]
    public void OnlyOrdinaryMarkdownUnderModelsIsATemplate()
    {
        Assert.True(NoteTemplateEngine.IsTemplatePath("Modelos/Reunião.md"));
        Assert.True(NoteTemplateEngine.IsTemplatePath("modelos/Diário.markdown"));
        Assert.False(NoteTemplateEngine.IsTemplatePath("Projetos/Modelos/a.md"));
        Assert.False(NoteTemplateEngine.IsTemplatePath("Modelos/dados.json"));
        Assert.False(NoteTemplateEngine.IsTemplatePath("Modelos/.private.md"));
        Assert.False(NoteTemplateEngine.IsTemplatePath(".urbe/Modelos/a.md"));
    }

    [Fact]
    public void FieldsAreUniqueAndKeepFirstAppearanceOrder()
    {
        var fields = NoteTemplateEngine.Fields("{{Cliente}}\n{{Data}}\n{{Cliente}}\n{{Preço_1}}");
        Assert.Equal(new[] { "Cliente", "Data", "Preço_1" }, fields);
    }

    [Theory]
    [InlineData("{{}}")]
    [InlineData("{{nome com espaço}}")]
    [InlineData("{{1nome}}")]
    [InlineData("{{nome}")]
    public void InvalidPlaceholdersRemainLiteral(string literal)
    {
        Assert.Empty(NoteTemplateEngine.Fields(literal));
        Assert.True(NoteTemplateEngine.TryRender(literal, new Dictionary<string, string?>(), out var text));
        Assert.Equal(literal, text);
    }

    [Fact]
    public void RendersWithoutNormalizingUnrelatedMarkdownOrCrLf()
    {
        var source = "---\r\ntags: [test]\r\n---\r\n# {{Tema}}\r\n\r\n- {{Pessoa}}\r\n";
        var values = new Dictionary<string, string?>
        {
            ["Tema"] = "Reunião",
            ["Pessoa"] = "Ana"
        };

        Assert.True(NoteTemplateEngine.TryRender(source, values, out var text));
        Assert.Equal("---\r\ntags: [test]\r\n---\r\n# Reunião\r\n\r\n- Ana\r\n", text);
    }

    [Fact]
    public void ValuesAreInsertedLiterallyOnceIncludingBracesDollarAndBackslash()
    {
        const string source = "{{a}} + {{b}} / {{a}}";
        var values = new Dictionary<string, string?>
        {
            ["a"] = "$& {{b}} \\x",
            ["b"] = null
        };

        Assert.True(NoteTemplateEngine.TryRender(source, values, out var text));
        Assert.Equal("$& {{b}} \\x +  / $& {{b}} \\x", text);
    }

    [Fact]
    public void MissingOrExtraFieldFailsClosedWithoutPartialText()
    {
        const string source = "antes {{A}} depois {{B}}";
        Assert.False(NoteTemplateEngine.TryRender(source, new Dictionary<string, string?> { ["A"] = "a" }, out var missing));
        Assert.Equal(string.Empty, missing);
        Assert.False(NoteTemplateEngine.TryRender(source, new Dictionary<string, string?>
        {
            ["A"] = "a", ["B"] = "b", ["C"] = "c"
        }, out var extra));
        Assert.Equal(string.Empty, extra);
        Assert.False(NoteTemplateEngine.TryRender(source, null, out var noValues));
        Assert.Equal(string.Empty, noValues);
    }

    [Fact]
    public void RejectsTooManyFieldsWithoutDroppingOneSilently()
    {
        var source = string.Join("\n", Enumerable.Range(0, NoteTemplateEngine.MaxFields + 1).Select(i => "{{F" + i + "}}"));
        var values = Enumerable.Range(0, NoteTemplateEngine.MaxFields + 1)
            .ToDictionary(i => "F" + i, i => (string?)i.ToString(), StringComparer.Ordinal);

        Assert.False(NoteTemplateEngine.TryRender(source, values, out var rendered));
        Assert.Equal(string.Empty, rendered);
    }
}
