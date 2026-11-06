using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client.Ui;

/// <summary>
/// CST-174: a ```quiz block in the note — each question answered by touch: choose, Conferir, see right/wrong and the
/// explanation. Answers live only in this view (never written into the note); reopening the note starts fresh.
/// </summary>
public sealed class QuizBlockView : Border
{
    public IReadOnlyList<QuizQuestion> Questions { get; }
    public int Right { get; private set; }

    public QuizBlockView(string code, double size)
    {
        var problems = new List<string>();
        Questions = QuizBlock.Parse(code, problems);
        var panel = new StackPanel { Spacing = 14 };
        for (int q = 0; q < Questions.Count; q++) panel.Children.Add(Question(q, Questions[q], size));
        foreach (var p in problems)
            panel.Children.Add(new TextBlock { Text = p, Foreground = UrbeTheme.Brush(UrbeTheme.Danger), FontSize = size * .8, TextWrapping = TextWrapping.Wrap });
        Child = panel;
        Padding = new Thickness(12, 10);
        BorderThickness = new Thickness(1);
        BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        CornerRadius = new CornerRadius(10);
        Avalonia.Automation.AutomationProperties.SetName(this, "Questionário com " + Questions.Count + " questão(ões)");
    }

    private Control Question(int index, QuizQuestion question, double size)
    {
        var box = new StackPanel { Spacing = 4 };
        box.Children.Add(new TextBlock { Text = (index + 1) + ". " + question.Prompt, FontSize = size, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text) });
        var choices = new List<Avalonia.Controls.Primitives.ToggleButton>();
        foreach (var (text, _) in question.Options)
        {
            var choice = new Avalonia.Controls.Primitives.ToggleButton
            {
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size * .95 },
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, MinHeight = 44, Padding = new Thickness(10, 6),
            };
            Avalonia.Automation.AutomationProperties.SetName(choice, "Alternativa: " + text);
            choice.IsCheckedChanged += (_, _) =>
            {
                // one right answer: choosing one lets the others go
                if (!question.Multiple && choice.IsChecked == true) foreach (var other in choices) if (!ReferenceEquals(other, choice)) other.IsChecked = false;
            };
            choices.Add(choice);
            box.Children.Add(choice);
        }
        var feedback = new TextBlock { IsVisible = false, FontWeight = FontWeight.SemiBold, FontSize = size * .9, TextWrapping = TextWrapping.Wrap };
        var why = new TextBlock { IsVisible = false, Text = question.Explanation, FontSize = size * .85, TextWrapping = TextWrapping.Wrap, Foreground = UrbeTheme.Brush(UrbeTheme.Text2) };
        var check = new Button { Content = "Conferir", MinHeight = 40, HorizontalAlignment = HorizontalAlignment.Left };
        bool counted = false;
        check.Click += (_, e) =>
        {
            e.Handled = true;
            if (choices.All(c => c.IsChecked != true)) { feedback.Text = "Escolha uma alternativa."; feedback.IsVisible = true; return; }
            var ok = choices.Select((c, i) => (c.IsChecked == true) == question.Options[i].Correct).All(x => x);
            feedback.Text = ok ? "Certo!" : "Ainda não. A certa está destacada.";
            feedback.Foreground = UrbeTheme.Brush(ok ? UrbeTheme.Ok : UrbeTheme.Danger);
            feedback.IsVisible = true;
            why.IsVisible = question.Explanation is not null;
            for (int i = 0; i < choices.Count; i++)
                choices[i].BorderBrush = question.Options[i].Correct ? UrbeTheme.Brush(UrbeTheme.Ok) : choices[i].IsChecked == true ? UrbeTheme.Brush(UrbeTheme.Danger) : null;
            if (ok && !counted) { Right++; counted = true; }
        };
        box.Children.Add(check);
        box.Children.Add(feedback);
        box.Children.Add(why);
        return box;
    }
}
