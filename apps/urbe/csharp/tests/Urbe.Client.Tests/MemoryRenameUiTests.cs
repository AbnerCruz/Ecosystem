using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-134: a note renamed in the app takes its memories along: the source path changes in the saved memory.</summary>
    [Fact]
    public async Task Memory_TheSourceNoteFollowsARename() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-memrename-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Plano.md"), "# Plano\n");
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "assistente");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                await UntilNotesSaved(view);
                view.Assistant.Open();
                view.Assistant.Config.MemoryRecords.Add(new AiMemoryRecord { Id = "m", Text = "O plano tem três fases", Scope = "vault", Vault = view.Assistant.VaultKey, RelatedPaths = ["Plano.md"] });
                await view.RenameNoteAsync("Plano.md", "Plano final");
                for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(File.Exists(Path.Combine(vault, "Plano final.md")));
                Assert.Equal(["Plano final.md"], view.Assistant.Config.MemoryRecords.Single(m => m.Id == "m").RelatedPaths);
                var saved = new AssistantStore(new FolderAssistantStorage(UrbeApp.AssistantFolder)).GetConfig();
                Assert.Equal(["Plano final.md"], saved.MemoryRecords.Single(m => m.Id == "m").RelatedPaths);
                return true;
            }
            finally
            {
                window.Close();
                UrbeApp.AssistantFolder = before;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
