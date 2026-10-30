using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Owner (UC-50): Personalização/AGENTS.md with instructions per model ("Claude 5: {…}", "Geral: {…}").</summary>
public sealed class UserAgentsFileTests
{
    private const string File = """
        Geral: { Escreva em português do Brasil. }
        Claude 5: {
          Seja breve.
          Cite as notas { com links }.
        }
        GPT: { Use listas. }

        ## Gemini
        Explique passo a passo.
        """;

    [Fact]
    public void SectionsAreReadInBothForms()
    {
        var s = UserAgentsFile.Parse(File);
        Assert.Equal(["Geral", "Claude 5", "GPT", "Gemini"], s.Select(x => x.Name));
        Assert.Contains("Cite as notas { com links }.", s[1].Text);
        Assert.Equal("Explique passo a passo.", s[3].Text);
    }

    [Fact]
    public void TheGeneralPartAndTheModelsPartApply()
    {
        var claude = UserAgentsFile.For(File, "anthropic/claude-opus-5-5");
        Assert.Contains("português", claude);
        Assert.Contains("Seja breve.", claude);
        Assert.DoesNotContain("listas", claude);
        var gemini = UserAgentsFile.For(File, "google/gemini-3-pro");
        Assert.Contains("passo a passo", gemini);
        Assert.DoesNotContain("breve", gemini);
        Assert.Equal("Só texto solto.", UserAgentsFile.For("Só texto solto.", "qualquer"));
        Assert.Equal("", UserAgentsFile.For(null, "x"));
    }
    [Fact]
    public void WithAPartForTheModelEachPartSaysWhatItIs()
    {
        var claude = UserAgentsFile.For(File, "anthropic/claude-opus-5-5");
        Assert.StartsWith("Geral:\n", claude);
        Assert.Contains("Para este modelo (Claude 5), vale sobre a parte geral onde divergirem:\n", claude);
        Assert.True(claude.IndexOf("português", StringComparison.Ordinal) < claude.IndexOf("Seja breve.", StringComparison.Ordinal));
        // only the general part: as written
        Assert.Equal("Escreva em português do Brasil.", UserAgentsFile.For(File, "mistral/large"));
    }
}
