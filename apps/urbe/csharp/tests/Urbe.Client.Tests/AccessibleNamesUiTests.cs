using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    /// <summary>CST-105: every visible button of the main screens has a name a screen reader can say (the Personalização tabs had none).</summary>
    public async Task Accessibility_EveryButtonHasAName() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-names-" + Guid.NewGuid().ToString("N"));
        var vault = WorkspaceVault(dir);
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(dir, "assistente");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        await view.OpenVaultAsync();
        await Settle(window, view);
        var report = new List<string>();
        void Scan(string screen, Control root)
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            foreach (var b in root.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible && b.Bounds.Width > 0 && b.FindAncestorOfType<Avalonia.Controls.Primitives.ScrollBar>() is null && b.FindAncestorOfType<Slider>() is null))
            {
                var name = ControlAutomationPeer.CreatePeerForElement(b).GetName();
                if (string.IsNullOrWhiteSpace(name))
                {
                    var texts = string.Join(" ", b.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t)));
                    report.Add(screen + " | sem nome | texto: " + texts + " | " + Math.Round(b.Bounds.Width) + "x" + Math.Round(b.Bounds.Height));
                }
            }
        }
        Scan("area", view.Workspace);
        view.ShowNotes(); await Settle(window, view); Scan("explorer", view);
        view.OpenNoteByPath("Comece.md"); await Settle(window, view); Scan("editor", view.Editor);
        foreach (var section in new[] { "aparencia", "texto", "editor", "tags" }) { view.Customize.Open(section); Scan("personalizacao/" + section, view.Customize); }
        view.Customize.Close();
        view.Assistant.Open(); Scan("assistente", view.Assistant);
        window.Close();
        UrbeApp.AssistantFolder = before;
        try { Directory.Delete(dir, true); } catch (IOException) { }
        Assert.True(report.Count == 0, string.Join("\n", report.Distinct()));
        return true;
    }, CancellationToken.None));
}
