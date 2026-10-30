using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-105: on a phone, every visible button of the Área de trabalho, the Explorer, the editor and the Assistente is at
/// least 40 px each way (the editor's Edição/Leitura was 17 px tall). The scroll bars' own arrows are not the app's.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Touch_EveryButtonOfTheMainScreensIsBigEnough() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-probe-" + Guid.NewGuid().ToString("N"));
        var vault = WorkspaceVault(dir);
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        await view.OpenVaultAsync();
        await Settle(window, view);
        var report = new List<string>();
        void Scan(string screen)
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            foreach (var b in window.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Bounds.Width > 0 && b.Bounds.Height > 0 && b.FindAncestorOfType<Avalonia.Controls.Primitives.ScrollBar>() is null))
                if (b.Bounds.Height < 40 || b.Bounds.Width < 40)
                    report.Add(screen + " | " + (AutomationProperties.GetName(b) ?? ToolTip.GetTip(b) as string ?? (b.Content as string) ?? b.GetType().Name) + " | " + Math.Round(b.Bounds.Width) + "x" + Math.Round(b.Bounds.Height));
        }
        Scan("area");
        view.ShowNotes(); await Settle(window, view); Scan("explorer");
        view.OpenNoteByPath("Comece.md"); await Settle(window, view); Scan("editor");
        Snapshot(window, "native-touch-editor.png");
        view.Assistant.Open(); Dispatcher.UIThread.RunJobs(); Scan("assistente");
        window.Close();
        try { Directory.Delete(dir, true); } catch (IOException) { }
        Assert.True(report.Count == 0, string.Join("\n", report.Distinct()));
        return true;
    }, CancellationToken.None));
}
