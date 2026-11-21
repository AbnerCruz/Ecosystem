using Avalonia.Threading;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>Runs the UI jobs for about <paramref name="ms"/> milliseconds.</summary>
    private static async Task Pump(int ms = 120)
    {
        for (int i = 0; i < ms / 20; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Dispatcher.UIThread.RunJobs();
    }
}
