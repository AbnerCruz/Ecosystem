using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>DEC-0059: every changed line of a proposal can be seen before approving; the card stopped at 160.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Approval_EveryChangedLineCanBeSeen() => Assert.True(await Session.Dispatch<bool>(() =>
    {
        var before = string.Join("\n", Enumerable.Range(1, 400).Select(i => "linha " + i));
        var after = string.Join("\n", Enumerable.Range(1, 400).Select(i => "linha " + i + " revisada"));
        var diff = AssistantPanel.Diff(before, after);
        var window = new Window { Width = 412, Height = 900, Content = diff };
        window.Show();
        try
        {
            int Shown() => diff.GetLogicalDescendants().OfType<TextBlock>().Count(t => (t.Text ?? "").StartsWith("+ linha", StringComparison.Ordinal));
            Assert.True(Shown() < 400);
            for (int i = 0; i < 10 && diff.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => b.Content is string c && c.StartsWith("Mostrar mais", StringComparison.Ordinal)) is { } more; i++)
            {
                more.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
            }
            Assert.Equal(400, Shown());
            Assert.Contains(diff.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "+ linha 400 revisada");
            return Task.FromResult(true);
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
