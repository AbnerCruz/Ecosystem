using System.Globalization;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>CST-077: agents answer in the system's language by default, in the configured one when set, and follow an explicit request for another.</summary>
public sealed class AgentLanguageTests
{
    [Theory]
    [InlineData("", "pt-BR", "pt-BR")]
    [InlineData("", "en-US", "en-US")]
    [InlineData("es", "pt-BR", "es")]
    [InlineData("system", "fr-FR", "fr-FR")]
    [InlineData("", "", "pt-BR")]       // invariant culture (no system language known): the Urbe's own language
    public void TheEffectiveLanguageIsTheConfiguredOneOrTheSystems(string configured, string system, string effective) =>
        Assert.Equal(effective, AgentLanguage.Effective(configured, system.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(system)));

    [Fact]
    public void TheRuleNamesTheLanguageAndLetsAnExplicitRequestWin()
    {
        var rule = AgentLanguage.Rule("en-US");
        Assert.Contains("English", rule);
        Assert.Contains("en-US", rule);
        Assert.Contains("pedir", rule);
        Assert.Contains("Português", AgentLanguage.Rule("pt-BR"));
        Assert.Contains(AgentLanguage.Choices, c => c.Code == "" && c.Name.StartsWith("Idioma do sistema", StringComparison.Ordinal));
    }

    [Fact]
    public void TheConfiguredLanguageIsSavedAndRead()
    {
        var c = new AiConfig { Language = "es" };
        Assert.Equal("es", AssistantStore.ConfigFromJson(AssistantStore.ConfigToJson(c)).Language);
        Assert.Equal("", AssistantStore.ConfigFromJson(AssistantStore.ConfigToJson(new AiConfig())).Language);
    }
}
