using System.Runtime.CompilerServices;

namespace Urbe.Client.Tests;

/// <summary>
/// The app keeps per-device settings (device-settings.json, the private reading state…) in the user's local data
/// folder. A test that turns one on (the touch diagnostics, a reader default) must never leave it on for the next run
/// or for another checkout running at the same time, so this test process gets a data folder of its own.
/// </summary>
internal static class TestIsolation
{
    [ModuleInitializer]
    internal static void OwnDataFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-client-tests-" + Environment.ProcessId);
        Directory.CreateDirectory(folder);
        // Linux and Android read XDG_DATA_HOME for LocalApplicationData; on Windows it is LOCALAPPDATA
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", folder);
        if (OperatingSystem.IsWindows()) Environment.SetEnvironmentVariable("LOCALAPPDATA", folder);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
    }
}
