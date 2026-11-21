using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-140: Configurações → Atalhos says what in atalhos.json would fail silently (two entries for one key, a command
    /// that does not exist) and, for a shortcut of yours over a default, which default it replaced.
    /// </summary>
    [Fact]
    public async Task Shortcuts_HiddenConflictsAreToldInTheList() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-keyconf-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Personalização"));
        File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n");
        File.WriteAllText(Path.Combine(vault, "Personalização", "atalhos.json"),
            "{\"versao\": 1, \"atalhos\": {\"Ctrl+K\": \"ui.workspace\", \"Mod+K\": \"ui.constellation\", \"Mod+Shift+J\": \"ui.cosntellation\"}}\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var showing = view.ShowShortcutsAsync();
            Dispatcher.UIThread.RunJobs();
            var texts = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains(texts, t => t.Contains("“Ctrl+K” e “Mod+K” são a mesma tecla"));
            Assert.Contains(texts, t => t.Contains("ui.cosntellation") && t.Contains("não existe"));
            Assert.Contains(texts, t => t.Contains("no lugar de Abrir comandos"));   // Ctrl+K was the command palette
            ClickButtonWithText(view.Dialogs, "Editar meus atalhos");
            await showing;
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
