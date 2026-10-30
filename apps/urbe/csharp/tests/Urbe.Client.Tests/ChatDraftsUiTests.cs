using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-122: what the person was writing to each agent comes back after switching agents and after the app is started
/// again (Android may end it in the background), and is gone once sent.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Chat_DraftsComeBackAfterTheAppIsStartedAgain() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-drafts-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(folder, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(folder, "assistente");
        async Task<(MainView View, Window Window)> StartAsync()
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(folder, "sel.json")));
            var window = new Window { Width = 412, Height = 900, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            view.Assistant.Open();
            Dispatcher.UIThread.RunJobs();
            return (view, window);
        }
        async Task Pause() { for (int i = 0; i < 60; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            var (view, window) = await StartAsync();
            var panel = view.Assistant;
            panel.Config.CustomAgents.Add(new("two", "Revisora", "sparkle", "", "read", false, ""));
            panel.Input.Text = "Resuma as notas da semana e";
            await Pause();
            // another agent, with its own draft; then back
            panel.SelectAgent("two");
            Assert.Equal("", panel.Input.Text);
            panel.Input.Text = "Revise a ortografia";
            await Pause();
            panel.SelectAgent("geral");
            Assert.Equal("Resuma as notas da semana e", panel.Input.Text);
            // the app ends without closing anything (Android in the background) and starts again
            window.Close();
            (view, window) = await StartAsync();
            Assert.Equal("Resuma as notas da semana e", view.Assistant.Input.Text);
            view.Assistant.SelectAgent("two");
            Assert.Equal("Revise a ortografia", view.Assistant.Input.Text);
            window.Close();
            return true;
        }
        finally { UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
