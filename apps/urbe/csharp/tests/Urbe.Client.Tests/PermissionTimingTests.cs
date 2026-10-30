using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-073: changing an agent's permission while it works says when the change counts.</summary>
public sealed class PermissionTimingTests
{
    [Theory]
    [InlineData("auto", "readonly", "próxima ação")]
    [InlineData("ask", "readonly", "próxima ação")]
    [InlineData("auto", "ask", "próxima alteração pede a sua aprovação")]
    [InlineData("ask", "auto", "próxima resposta")]
    [InlineData("readonly", "ask", "próxima resposta")]
    public void TheMessageMatchesWhatTheRunDoes(string was, string now, string says) =>
        Assert.Contains(says, AssistantPanel.PermissionTiming(was, now));
}
