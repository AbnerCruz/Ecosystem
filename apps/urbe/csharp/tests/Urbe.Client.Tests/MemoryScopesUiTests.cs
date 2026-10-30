using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>CST-085: the memory manager says which memories only this agent uses and which every agent shares.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Memory_PrivateAndSharedAreToldApartAndFiltered() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-memory-scopes-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(folder, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(folder, "assistente");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(folder, "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            var panel = view.Assistant;
            panel.Open();
            Dispatcher.UIThread.RunJobs();
            var name = ((IAssistantApp)view).VaultName;
            var key = name.Length > 0 ? name : "Urbe";
            panel.Config.CustomAgents.Add(new("two", "Revisora", "sparkle", "", "read", false, ""));
            panel.Config.MemoryRecords.Add(new AiMemoryRecord { Id = "m1", Text = "Prefere respostas curtas", Kind = "preference", Scope = "agent", AgentId = "geral", Vault = key });
            panel.Config.MemoryRecords.Add(new AiMemoryRecord { Id = "m2", Text = "O projeto se chama Droerni", Kind = "fact", Scope = "vault", Vault = key });
            panel.Config.MemoryRecords.Add(new AiMemoryRecord { Id = "m3", Text = "Revisar só ortografia", Kind = "procedure", Scope = "agent", AgentId = "two", Vault = key });
            panel.ShowSettings();
            ClickNamed(panel, "Configurações: Contexto");
            ClickButtonWithText(panel, "Gerenciar memórias");
            Dispatcher.UIThread.RunJobs();
            List<string?> Texts() => [.. panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text)];
            Assert.Contains(Texts(), t => t?.StartsWith("Preferência · só Assistente (privada) · ", StringComparison.Ordinal) == true);
            Assert.Contains(Texts(), t => t?.StartsWith("Fato · compartilhada · todos os agentes deste vault · ", StringComparison.Ordinal) == true);
            Assert.Contains(Texts(), t => t?.StartsWith("Procedimento · só Revisora (privada) · ", StringComparison.Ordinal) == true);

            ClickButtonWithText(panel, "Privadas de Assistente");
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Prefere respostas curtas", Texts());
            Assert.DoesNotContain("O projeto se chama Droerni", Texts());
            Assert.DoesNotContain("Revisar só ortografia", Texts());

            ClickButtonWithText(panel, "Compartilhadas");
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("O projeto se chama Droerni", Texts());
            Assert.DoesNotContain("Prefere respostas curtas", Texts());
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));
    [Fact]
    public async Task Memory_AnAgentsPrivateMemoriesGoWithItAndSharedOnesStay() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-074: deleting an agent says how many private memories go with it, and removes only those
        var folder = Path.Combine(Path.GetTempPath(), "urbe-memory-delete-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(folder, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(folder, "assistente");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(folder, "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            var panel = view.Assistant;
            panel.Open();
            Dispatcher.UIThread.RunJobs();
            panel.Config.CustomAgents.Add(new("two", "Revisora", "sparkle", "", "read", false, ""));
            panel.Config.MemoryRecords.Add(new AiMemoryRecord { Id = "p1", Text = "Revisar só ortografia", Kind = "procedure", Scope = "agent", AgentId = "two", Vault = "V" });
            panel.Config.MemoryRecords.Add(new AiMemoryRecord { Id = "s1", Text = "O projeto se chama Droerni", Kind = "fact", Scope = "vault", AgentId = "two", Vault = "V" });
            panel.ShowSettings();
            ClickNamed(panel, "Configurações: Agentes");
            Dispatcher.UIThread.RunJobs();
            ClickNamed(panel, "Excluir Revisora");
            for (int i = 0; i < 100 && !view.Dialogs.IsOpen; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("1 lembrança(s) privada(s)", StringComparison.Ordinal));
            ClickButtonWithText(view.Dialogs, "Excluir agente e histórico");
            for (int i = 0; i < 100 && panel.Config.CustomAgents.Any(a => a.Id == "two"); i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
            Assert.DoesNotContain(panel.Config.MemoryRecords, m => m.Id == "p1");
            Assert.Contains(panel.Config.MemoryRecords, m => m.Id == "s1");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
