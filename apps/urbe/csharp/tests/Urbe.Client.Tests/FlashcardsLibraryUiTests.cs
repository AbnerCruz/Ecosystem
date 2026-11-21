using Avalonia.LogicalTree;
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

            // the library panel: the row opens it, a set shows its cards, the review grades with buttons
            view.Editor.Pinned.FlashcardsRow!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            var panel = view.FlashcardsView;
            Assert.NotNull(panel);
            Assert.Equal("list", panel!.Screen);
            Snapshot(window, "native-flashcards-list.png");
            // a set made from the list opens ready for its first card
            var titleBox = panel.GetLogicalDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Título do novo conjunto");
            titleBox.Text = "Plantas";
            ClickButtonWithText(panel, "Criar conjunto");
            for (int i = 0; i < 200 && panel.Screen != "set"; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal("set", panel.Screen);
            Assert.Contains(panel.GetLogicalDescendants().OfType<TextBox>(), t => Avalonia.Automation.AutomationProperties.GetName(t) == "Frente do novo cartão");
            Assert.Contains(study.Sets(), x => x.Set.Title == "Plantas" && x.Set.Cards.Count == 0);
            panel.ShowSet("deck:animais");
            Assert.Equal("set", panel.Screen);
            // a card typed right in the set, without dialogs; a repeated front is refused there, saying why
            TextBox Named(string name) => panel.GetLogicalDescendants().OfType<TextBox>().Single(t => Avalonia.Automation.AutomationProperties.GetName(t) == name);
            Named("Frente do novo cartão").Text = "Maior ave?";
            Named("Verso do novo cartão").Text = "Avestruz";
            ClickButtonWithText(panel, "Adicionar cartão");
            for (int i = 0; i < 200 && study.Sets()[0].Set.Cards.Count < 3; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal(3, study.Sets()[0].Set.Cards.Count);
            Named("Frente do novo cartão").Text = "Maior ave?";
            Named("Verso do novo cartão").Text = "Outra";
            ClickButtonWithText(panel, "Adicionar cartão");
            for (int i = 0; i < 200 && !panel.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.IsVisible && (t.Text ?? "").Contains("repete")); i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.IsVisible && (t.Text ?? "").Contains("repete"));
            Assert.Equal(3, study.Sets()[0].Set.Cards.Count);
            panel.StartReview(study.Sets()[0].Set, all: false);
            Assert.Equal("review", panel.Screen);
            panel.Reveal();
            Dispatcher.UIThread.RunJobs();
            Snapshot(window, "native-flashcards-review.png");
            panel.GradeCurrent(Recall.Bom);
            panel.GradeCurrent(Recall.Bom);   // not shown yet: ignored
            panel.Reveal();
            panel.GradeCurrent(Recall.Errei);
            panel.Reveal();
            panel.GradeCurrent(Recall.Bom);   // the card added in the set
            Assert.Equal("summary", panel.Screen);
            Assert.Equal(1, study.Sets()[0].Progress.Due);      // the one forgotten is due again today
            Assert.Equal(3, study.Sets()[0].Progress.Reviewed);
            Assert.True(panel.GoBack());
            view.CloseFlashcards();
            Assert.Null(view.FlashcardsView);
            window.Close();

            // the app reopens: the deck is still there, and the vault shows nothing new
            var reopened = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var again = new Window { Width = 412, Height = 860, Content = reopened };
            again.Show();
            await reopened.OpenVaultAsync();
            await Settle(again, reopened);
            var back = ((IAssistantStudy)reopened).Sets();
            Assert.Equal(["Maior mamífero?", "Animal terrestre mais rápido?", "Maior ave?"], back.Single(x => x.Set.Id == "deck:animais").Set.Cards.Select(c => c.Front));
            Assert.Contains(back, x => x.Set.Title == "Plantas");
            Assert.Equal(3, back.Single(x => x.Set.Id == "deck:animais").Progress.Reviewed);   // the review's progress came back too
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
