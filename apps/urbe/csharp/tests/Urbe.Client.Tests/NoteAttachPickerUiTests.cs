using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>DEC-0059: "Nota do vault" in the chat reaches every note; it showed only the first 60 matches.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Chat_EveryNoteOfTheVaultCanBeAttached() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var folder = Path.Combine(Path.GetTempPath(), "urbe-attach-note-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(folder, "vault");
        Directory.CreateDirectory(vault);
        for (int i = 1; i <= 70; i++) File.WriteAllText(Path.Combine(vault, $"Nota {i:00}.md"), $"# Nota {i:00}\n");
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(folder, "assistente");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(folder, "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            view.Assistant.Open();
            Dispatcher.UIThread.RunJobs();
            var open = view.Assistant.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Nota do vault");
            open.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            int Listed() => view.Assistant.GetLogicalDescendants().OfType<TextBlock>().Count(t => (t.Text ?? "").StartsWith("Nota ", StringComparison.Ordinal) && t.Text!.Length == 7);
            Assert.Equal(60, Listed());
            ClickButtonWithText(view.Assistant, "Mostrar mais (10 restantes)");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(70, Listed());
            Assert.Contains(view.Assistant.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Nota 70");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(folder, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
