using Avalonia.Controls;
using Lunet.Client;
using Lunet.Client.Shell;

namespace Lunet.Tests;

/// <summary>
/// H8 parity: the shell's Notas imports images and files into Notes/ (the previous Android screen did), never over
/// another file and within 64 MB, and a tap on one attaches it to the AI's next message.
/// </summary>
public sealed class NotesImportTests
{
    [Fact]
    public void ImportIntoNotes_NeverOverwrites_ListsIt_AndAttachesToTheAi()
    {
        var root = Directory.CreateTempSubdirectory("lunet-notes-import-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var aiDir = Path.Combine(root, ".ai");
                var shell = new LunetShell(root)
                {
                    AiHost = new Lunet.Client.Views.AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(aiDir, "s.json"), new NullVault()), aiDir),
                };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.State.SelectTab(WorkspaceTab.Notes);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var notes = shell.WorkspaceView.Notes;

                var first = notes.ImportNote("Herói Pulando.png", [1, 2, 3]);
                var second = notes.ImportNote("Herói Pulando.png", [4, 5]);
                Assert.StartsWith("Notes/", first);
                Assert.NotEqual(first, second);                                         // never over another file
                var dir = shell.Workspace.Project!.Directory;
                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(dir, first)));
                Assert.Equal(new byte[] { 4, 5 }, File.ReadAllBytes(Path.Combine(dir, second)));
                Assert.Equal(new[] { first, second }.Order(StringComparer.Ordinal), notes.NoteAttachments().Order(StringComparer.Ordinal));
                Assert.Contains(Lunet.Core.ProjectNotes.List(shell.Workspace.Project), n => n.Path == first); // the same list as before

                Assert.Throws<IOException>(() => notes.ImportNote("enorme.bin", new byte[Lunet.Core.ProjectNotes.MaxImportBytes + 1]));

                var attach = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Button>()
                    .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Anexar " + Path.GetFileName(first) + " à IA");
                attach.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(SupportPanel.Ai, shell.State.Panel);
                Assert.Equal([first], shell.WorkspaceView.Support.Ai.Attachments);
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class NullVault : Lunet.Ai.ISecretVault
    {
        public string? Get(string name) => null;
        public void Set(string name, string value) { }
        public void Delete(string name) { }
    }
}
