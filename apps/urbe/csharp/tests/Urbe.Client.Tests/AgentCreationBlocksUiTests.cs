using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_NewAgentHasInstructionBlocksFromTheStart() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // CST-070: the blocks (optional title + text) are on "Novo agente", and the agent's settings reopen the same ones
            var dir = Path.Combine(Path.GetTempPath(), "urbe-assistant-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var before = UrbeApp.AssistantFolder;
            UrbeApp.AssistantFolder = Path.Combine(dir, "app", "assistant");
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.AssistantLauncher.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                var panel = view.Assistant;
                panel.ShowSettings();
                ClickNamed(panel, "Configurações: Agentes");
                ClickButtonWithText(panel, "+ Novo agente");
                TextBox Field(string placeholder) => panel.GetLogicalDescendants().OfType<TextBox>().Last(t => (t.PlaceholderText ?? "").StartsWith(placeholder, StringComparison.Ordinal));
                Field("Ex.: Revisor").Text = "Revisor";
                ClickButtonWithText(panel, "+ Adicionar bloco");
                Field("Nome do bloco").Text = "Estilo";
                Field("Instruções detalhadas").Text = "Frases curtas e diretas.";
                ClickButtonWithText(panel, "+ Adicionar bloco");
                Field("Instruções detalhadas").Text = "Sem título também vale.";
                ClickButtonWithText(panel, "Salvar");
                var agent = Assert.Single(panel.Config.CustomAgents, a => a.Name == "Revisor");
                Assert.Equal([new AiInstructionBlock("Estilo", "Frases curtas e diretas."), new AiInstructionBlock("", "Sem título também vale.")],
                    panel.Config.AgentInstructionBlocks[agent.Id]);
                // the agent's settings (opened after saving) show the same blocks
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBox>(), t => t.Text == "Frases curtas e diretas.");
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBox>(), t => t.Text == "Sem título também vale.");
                return true;
            }
            finally
            {
                UrbeApp.AssistantFolder = before;
                window.Close();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
