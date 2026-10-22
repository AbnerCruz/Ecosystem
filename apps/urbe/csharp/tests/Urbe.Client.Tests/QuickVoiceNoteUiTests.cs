using Avalonia.Controls;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>UC-56: a quick note by voice (a fake transcriber) written to the real folder vault.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task QuickVoiceNote_AppendsToTheDaysNoteInTheVault() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var said = new Queue<string?>(["comprar pão", "ligar para a Ana", null]);
                view.Voice = () => Task.FromResult(said.Dequeue());
                var path = QuickNotes.PathFor(DateTime.Now);
                var file = Path.Combine(vault, path.Replace('/', Path.DirectorySeparatorChar));

                // from the Explorer's "Nova" menu: the note and its folder are made in the vault
                await PickFromNewMenu(view, "Nota rápida", () => Task.CompletedTask);
                Assert.True(File.Exists(file));
                var first = File.ReadAllText(file);
                Assert.StartsWith(QuickNotes.Header(DateTime.Now) + "\n\n" + QuickNotes.DayHeading(DateTime.Now) + "\n\n- ", first);
                Assert.EndsWith(" — comprar pão\n", first);
                Assert.NotNull(view.Documents!.Get(path));
                Assert.Contains(QuickNotes.Folder, view.Library!.Folders);

                // the next one goes into the same day of the month's note
                await view.QuickVoiceNoteAsync();
                var second = File.ReadAllText(file);
                Assert.StartsWith(first, second);
                Assert.EndsWith(" — ligar para a Ana\n", second);
                Assert.Single(view.Documents.List(), d => d.Path == path);

                // cancelled (null): nothing changes
                await view.QuickVoiceNoteAsync();
                Assert.Equal(second, File.ReadAllText(file));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task QuickVoiceNote_WithoutAWritableVaultKeepsTheWordsToCopy() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            int notes = view.Documents!.List().Count;
            view.Voice = () => Task.FromResult<string?>("ideia para o projeto");
            await RunWithDialog(view.QuickVoiceNoteAsync(), () =>
            {
                Assert.Equal("ideia para o projeto", view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text);
                ClickButtonWithText(view.Dialogs, "Fechar");
                return Task.CompletedTask;
            });
            Assert.Equal(notes, view.Documents.List().Count);
            window.Close();
            return true;
        }, CancellationToken.None));
}
