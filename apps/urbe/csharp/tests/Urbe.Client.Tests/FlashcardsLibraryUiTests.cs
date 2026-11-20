using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;
using Urbe.Core.Study;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// #1005, first vertical slice: a deck the Assistente makes is written to .urbe/estudo/flashcards.json, adds no note,
    /// folder or plugin to what the Explorer shows, comes back after the app reopens, is offered from Estudo, and the
    /// notes' pergunta::resposta cards are in the same library without being copied.
    /// </summary>
    [Fact]
    public async Task Flashcards_ADeckIsNativeDurableAndLeavesNoResidue() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-flashcards-" + Guid.NewGuid().ToString("N"));
        var vault = WorkspaceVault(dir);
        File.WriteAllText(Path.Combine(vault, "Células.md"), "# Células\n\nMitocôndria::usina da célula\n");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        try
        {
            string[] Visible() => Directory.GetFiles(vault, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(vault, f).Replace('\\', '/')).Where(p => !p.Split('/').Any(s => s.StartsWith('.'))).Order().ToArray();
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            var before = Visible();

            IAssistantStudy study = view;
            var deck = await study.CreateDeckAsync("Animais", [("Maior mamífero?", "Baleia-azul"), ("Animal terrestre mais rápido?", "Guepardo")], null);
            Assert.Equal(2, deck.Cards.Count);
            var file = Path.Combine(vault, ".urbe", "estudo", "flashcards.json");
            Assert.True(File.Exists(file));
            Assert.Single(FlashcardLibrary.Parse(File.ReadAllText(file)).Decks);
            Assert.Equal(before, Visible());   // no note, folder or plugin appeared

            var sets = study.Sets();
            Assert.Equal(["deck:animais", "note:Células.md"], sets.Select(s => s.Set.Id));
            Assert.Equal(2, sets[0].Progress.Due);

            // offered from Estudo, at the top of the source chooser
            await view.StartStudyAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(view.Editor.Pinned.FlashcardsRow);
            window.Close();

            // the app reopens: the deck is still there, and the vault shows nothing new
            var reopened = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var again = new Window { Width = 412, Height = 860, Content = reopened };
            again.Show();
            await reopened.OpenVaultAsync();
            await Settle(again, reopened);
            var back = ((IAssistantStudy)reopened).Sets();
            Assert.Equal(["Maior mamífero?", "Animal terrestre mais rápido?"], back[0].Set.Cards.Select(c => c.Front));
            Assert.Equal(before, Visible());
            again.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }, CancellationToken.None));
}
