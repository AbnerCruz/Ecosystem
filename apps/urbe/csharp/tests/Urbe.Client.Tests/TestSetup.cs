using System.Runtime.CompilerServices;
using Urbe.Client;

namespace Urbe.Client.Tests;

internal static class TestSetup
{
    /// <summary>The scenarios open small temporary vaults; only the Tutorial's own test lets the city create it.</summary>
    [ModuleInitializer]
    internal static void Init() => MainView.SeedsTutorial = false;
}
