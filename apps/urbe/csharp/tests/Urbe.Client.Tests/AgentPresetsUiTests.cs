using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-071: an agent made from a preset keeps the preset's tools (Escritor writes) and starts asking before each change;
/// a preset already in use gets a free name instead of an error.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Agents_APresetKeepsItsToolsAndGetsAFreeName() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-presets-" + Guid.NewGuid().ToString("N"));
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
            AiAgentDef MakeWriter()
            {
                panel.ShowSettings();
                ClickNamed(panel, "Configurações: Agentes");
                ClickButtonWithText(panel, "+ Novo agente");
                Dispatcher.UIThread.RunJobs();
                var presets = panel.GetLogicalDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Modelo de agente");
                presets.SelectedIndex = AssistantAgent.Agents.ToList().FindIndex(a => a.Id == "escritor");
                Dispatcher.UIThread.RunJobs();
                var count = panel.Config.CustomAgents.Count;
                ClickButtonWithText(panel, "Salvar");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(count + 1, panel.Config.CustomAgents.Count);
                return panel.Config.CustomAgents[^1];
            }
            var first = MakeWriter();
            Assert.Equal("Escritor", first.Name);
            Assert.Equal("write", panel.Config.ToolScopeFor(first));
            Assert.Equal("ask", panel.Config.PolicyFor(first.Id));
            var second = MakeWriter();
            Assert.Equal("Escritor 2", second.Name);
            Assert.Equal("write", panel.Config.ToolScopeFor(second));
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
