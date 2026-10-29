using System.Reflection;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// The run's host wraps the app's: every capability the app offers must reach the tools. Graphs were not forwarded, so
/// list_graphs and read_graph found nothing in the app while their own tests (on the app's host) passed.
/// </summary>
public sealed class AssistantRunHostTests
{
    [Fact]
    public void TheRunsHostForwardsEveryCapabilityOfTheAppsHost()
    {
        var runHost = typeof(AssistantAgent).GetNestedType("RunHost", BindingFlags.NonPublic)!;
        Assert.NotNull(runHost);
        var declared = runHost.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToHashSet();
        var missing = typeof(IAssistantHost).GetProperties().Select(p => p.Name).Where(n => !declared.Contains(n)).ToList();
        Assert.True(missing.Count == 0, "RunHost não repassa: " + string.Join(", ", missing));
        var methods = runHost.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(m => m.Name).ToHashSet();
        var missingMethods = typeof(IAssistantHost).GetMethods().Where(m => !m.IsSpecialName).Select(m => m.Name).Where(n => !methods.Contains(n)).ToList();
        Assert.True(missingMethods.Count == 0, "RunHost não repassa: " + string.Join(", ", missingMethods));
    }
}
