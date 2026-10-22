using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>Renomear a pasta from its "⋯" in the Explorer: the folder renamed in the vault first, everything inside it following.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task FolderRename_RenamesTheFolderInTheVaultAndKeepsEverythingInside() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var id = view.Documents!.Get("Bairro/Um.md")!.Id;

                // the "⋯" of the folder → Renomear: its row becomes a box with the name (UC-83, renaming in place)
                view.ShowNotes();
                Assert.True(view.Explorer.OpenFolderMenu("Bairro"));
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Renomear");
                await Task.Delay(30);
                Dispatcher.UIThread.RunJobs();
                var box = view.Explorer.GetVisualDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Novo nome");
                Assert.Equal("Bairro", box.Text);
                Snapshot(window, "native-folder-rename.png");
                box.Text = "Centro";
                box.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
                for (int i = 0; i < 200 && Directory.Exists(Path.Combine(vault, "Bairro")); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }

                // the folder renamed on disk, the note inside it and its document following, with its id
                Assert.False(Directory.Exists(Path.Combine(vault, "Bairro")));
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, "Centro", "Um.md")));
                Assert.Equal(id, view.Documents.Get("Centro/Um.md")!.Id);
                Assert.Contains("Centro", view.Library!.Folders);
                Assert.DoesNotContain("Bairro", view.Library.Folders);
                await UntilNotesSaved(view);
                var record = File.ReadAllText(Path.Combine(vault, NoteLibrary.Path));
                Assert.Contains("\"Centro/Um.md\"", record);
                Assert.DoesNotContain("\"Bairro/Um.md\"", record);

                // a name another folder already has: refused, nothing changes
                Directory.CreateDirectory(Path.Combine(vault, "Outro"));
                await view.CreateFolderAsync("", "Outro2");
                var refused = view.RenameFolderAsync("Centro", "Outro2");
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.Dialogs.IsOpen);
                ClickButtonWithText(view.Dialogs, "Ok");
                Assert.False(await refused);
                Assert.True(Directory.Exists(Path.Combine(vault, "Centro")));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
