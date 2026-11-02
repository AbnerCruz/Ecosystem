namespace Urbe.Client.Tests;

/// <summary>
/// Architecture guard for CST-013 / #899. Headless tests cannot run Android's
/// process-fatal foreground-service watchdog, so keep the hazardous entry point
/// out of the visible-Activity send path. DEVICE still verifies the OS lifecycle.
/// </summary>
public sealed class AssistantServiceStartupTests
{
    [Fact]
    public void VisibleActivityDoesNotArmForegroundServiceStartupWatchdog()
    {
        var source = ServiceSource();
        Assert.DoesNotContain("context.StartForegroundService(", source);
        Assert.Contains("else if (MainActivity.IsVisibleToUser)", source);
        Assert.Contains("context.StartService(", source);
    }

    [Fact]
    public void ServicePromotesDuringCreationAndUpdatesExistingInstance()
    {
        var source = ServiceSource();
        var create = source.IndexOf("public override void OnCreate()", StringComparison.Ordinal);
        var command = source.IndexOf("public override StartCommandResult OnStartCommand(", StringComparison.Ordinal);
        Assert.True(create >= 0 && command > create);
        Assert.Contains("Promote(run)", source[create..command]);
        Assert.Contains("if (_instance is { } service)", source);
        Assert.Contains("service.Promote(activity)", source);
        Assert.Contains("_latest = null;", source);
        Assert.Contains("StartCommandResult.NotSticky", source);
    }

    private static string ServiceSource()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Urbe.Portable.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        return File.ReadAllText(Path.Combine(root!.FullName, "src", "Urbe.Android", "AssistantRunService.cs"));
    }
}
