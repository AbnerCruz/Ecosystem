namespace Urbe.Core.Tests;

public sealed class PublicationPrivacyTests
{
    [Fact]
    public void PrivateLookingDataIsFoundAndYearsAreNot()
    {
        var html = "<p>Fale com <a href=\"mailto:ana@exemplo.com\">ana@exemplo.com</a> ou (11) 98765-4321.</p>" +
                   "<p>CPF 123.456.789-09. senha: abc123. Em 2026, 1500 pessoas.</p><p>sk-abcdefghijklmnop</p>";
        var found = PublicationPrivacy.Scan(html);
        Assert.Equal(["e-mail", "telefone", "CPF", "chave ou senha", "chave ou senha"], found.Select(f => f.Kind));
        Assert.Equal("ana@exemplo.com", found[0].Sample);
        Assert.Empty(PublicationPrivacy.Scan("<p>Em 2026, 1500 pessoas leram 12 capítulos.</p>"));
    }
}
