using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>markChanged → scheduleRoadRebuild: a [[link]] typed in the editor becomes a street, before any save.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_ALinkTypedBecomesAStreet() =>
        Assert.True(await Session.Dispatch(RoadsScenario, CancellationToken.None));

    private static async Task<bool> RoadsScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-roads-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "A.md"), "# A\n");
        File.WriteAllText(Path.Combine(vault, "B.md"), "# B\n");
        File.WriteAllText(Path.Combine(vault, "C.md"), "# C\n\n[[A]]\n");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            var city = view.World.City!;
            Assert.Single(city.Links);
            view.OpenNoteByPath("B.md");
            Dispatcher.UIThread.RunJobs();
            view.Editor.SetMode(EditorMode.Fonte);
            Dispatcher.UIThread.RunJobs();
            view.Editor.Editor.Text = "# B\n\nVer [[A]].\n";
            for (int i = 0; i < 40 && city.Links.Count < 2; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal(2, city.Links.Count);                         // a new street, from the text being typed
            Assert.True(city.Roads.Count > 0);
            window.Close();
            return true;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
