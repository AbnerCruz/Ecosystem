using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-174: a ```quiz block is answered in the note, and the note keeps only its text.</summary>
    [Fact]
    public async Task QuizBlock_AnswersByTouch_WithoutWritingTheNote() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        await view.LoadAsync(TutorialNotes.Load());
        const string note = "# Revisão\n\n```quiz\n? Qual a derivada de x²?\n- [ ] x\n- [x] 2x\n> Regra da potência.\n```\n";
        view.Editor.Open(new CityNote("Tutorial/Revisao.md", note));
        view.Editor.SetMode(EditorMode.Visual);
        await BenchPump(window);
        var quiz = view.Editor.GetVisualDescendants().OfType<Urbe.Client.Ui.QuizBlockView>().Single();
        var options = quiz.GetVisualDescendants().OfType<ToggleButton>().ToList();
        options[0].IsChecked = true;
        options[1].IsChecked = true; // a single-answer question lets the other go
        Assert.False(options[0].IsChecked);
        quiz.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Conferir").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await BenchPump(window);
        var texts = quiz.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text).ToList();
        Assert.Contains("Certo!", texts);
        Assert.Contains("Regra da potência.", texts);
        Assert.Equal(1, quiz.Right);
        Assert.Equal(note, view.Editor.Editor.Text);
        Snapshot(window, "quiz-block.png");
        window.Close();
        return true;
    }, CancellationToken.None));
}
