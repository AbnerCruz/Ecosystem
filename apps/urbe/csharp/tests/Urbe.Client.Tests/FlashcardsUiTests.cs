using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#956: a note's cards are reviewed; what was known waits, what was missed comes back today; progress is kept.</summary>
    [Fact]
    public async Task Flashcards_ReviewKeepsWhatWasKnownAndBringsBackWhatWasMissed() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var state = Path.Combine(Path.GetTempPath(), "urbe-study-" + Guid.NewGuid().ToString("N") + ".json");
        var view = new MainView { StudyStatePath = state };
        var window = new Window { Width = 412, Height = 800, Content = view };
        window.Show();
        try
        {
            var note = new CityNote("Bio.md", "# Bio\n\nFotossíntese::luz vira energia\nClorofila::pigmento verde\n");
            await view.LoadAsync([note]);
            Assert.Equal(2, view.DueCards(note).Count);
            async Task Pick(string label)
            {
                bool Has() => view.Dialogs.IsOpen && Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view.Dialogs).OfType<TextBlock>().Any(t => t.Text == label);
                for (int i = 0; i < 100 && !Has(); i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                ClickButtonWithText(view.Dialogs, label);
                for (int i = 0; i < 5; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            }
            var review = view.ReviewCardsAsync(note);
            await Pick("Mostrar resposta"); await Pick("Bom");
            await Pick("Mostrar resposta"); await Pick("Errei");
            await review;
            Assert.Equal(["Clorofila"], view.DueCards(note).Select(c => c.Front));
            Assert.True(File.Exists(state));
            // what a new app reads after the process died: both reviews, the missed one due today
            var saved = Urbe.Core.Study.Flashcards.Parse(File.ReadAllText(state));
            Assert.Equal(2, saved.Count);
            Assert.Single(saved.Values, s => s.Lapses == 1 && s.Due == DateOnly.FromDateTime(DateTime.Now));
            return true;
        }
        finally { window.Close(); File.Delete(state); }
    }, CancellationToken.None));
}
