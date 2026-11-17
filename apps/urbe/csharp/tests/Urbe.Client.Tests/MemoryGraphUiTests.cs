using Avalonia.Headless;
using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>UC-87: the agent's memory opens as a graph, nothing is written to the vault, and moving it saves nothing.</summary>
    [Fact]
    public async Task MemoryGraph_OpensAsAProjectionAndWritesNothing() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-memgraph-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Receitas.md"), "# Receitas\n");
        var previous = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(dir, "ai");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var panel = view.Assistant;
            panel.Open();
            panel.SelectAgent("geral");
            var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Length;
            AssistantMemoryEngine.CaptureUserStatements(panel.Config, panel.VaultKey, "geral", "Gosto de strogonoff.", "conversation:a");
            AssistantMemoryEngine.CaptureUserStatements(panel.Config, panel.VaultKey, "geral", "Não gosto de strogonoff.", "conversation:b");
            await panel.ShowMemoryGraphAsync();
            await Settle(window, view);
            Assert.True(view.GraphOpen);
            Assert.Contains(view.OpenGraph!.Edges, e => e.Relation == "substitui");
            if (Environment.GetEnvironmentVariable("URBE_CAPTURE") is { Length: > 0 } capture)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.CaptureRenderedFrame()!.Save(capture, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            }
            view.OpenGraph.MoveNode(view.OpenGraph.Nodes[0].Id, 500, 500);
            await view.CloseGraphAsync();
            Assert.Equal(before, Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Length);
            return true;
        }
        finally
        {
            window.Close();
            UrbeApp.AssistantFolder = previous;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }, CancellationToken.None));
}
