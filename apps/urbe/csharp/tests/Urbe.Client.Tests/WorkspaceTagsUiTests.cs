using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>DEC-0059: in the Área de trabalho any tag can filter, not only the six most used.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Workspace_EveryTagCanFilterThroughMoreTags() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-area-tags-" + Guid.NewGuid().ToString("N"));
        var vault = WorkspaceVault(dir);
        // eight tags in Personagens: #t1 on eight notes … #t8 on one
        for (int n = 1; n <= 8; n++)
            File.WriteAllText(Path.Combine(vault, "Personagens", $"P{n}.md"), $"# P{n}\n\n" + string.Join(" ", Enumerable.Range(1, 9 - n).Select(t => "#t" + t)) + "\n");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            var w = view.Workspace;
            w.Open("Personagens");
            Assert.DoesNotContain(w.GetLogicalDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == "#t8");
            Click(Named(w, "+2 tags"));
            for (int i = 0; i < 100 && !view.Dialogs.IsOpen; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
            ClickButtonWithText(view.Dialogs, "#t8");
            for (int i = 0; i < 100 && w.TagFilter is null; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
            Assert.Equal("t8", w.TagFilter);
            // the chosen tag shows as a chip while it filters, and taps off like the others
            Click(Named(w, "#t8"));
            Assert.Null(w.TagFilter);
            window.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }, CancellationToken.None));
}
