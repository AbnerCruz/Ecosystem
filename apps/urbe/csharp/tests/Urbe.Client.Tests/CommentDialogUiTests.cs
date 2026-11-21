using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-022: in the comments window each comment is edited in the box (Salvar) or removed.</summary>
    [Fact]
    public async Task Comments_TheWindowEditsOrRemovesAComment() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dialogs = new UrbeDialogs();
        var window = new Window { Width = 360, Height = 700, Content = dialogs };
        window.Show();
        try
        {
            Button Named(string name) => dialogs.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == name);
            var edit = dialogs.CommentsEditAsync("Comentários", ["um", "dois"]);
            Dispatcher.UIThread.RunJobs();
            Named("Editar comentário 2").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            var box = dialogs.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Novo comentário");
            Assert.Equal("dois", box.Text);
            box.Text = "dois, revisto";
            ClickButtonWithText(dialogs, "Salvar");
            Assert.Equal(new CommentChange(1, "dois, revisto"), await edit);

            var remove = dialogs.CommentsEditAsync("Comentários", ["um", "dois"]);
            Dispatcher.UIThread.RunJobs();
            Named("Excluir comentário 1").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new CommentChange(0, null), await remove);

            // the read-only window (old pages) still only adds
            var add = dialogs.CommentsAsync("Comentários", ["um"]);
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain(dialogs.GetLogicalDescendants().OfType<Button>(), b => AutomationProperties.GetName(b)?.StartsWith("Editar", StringComparison.Ordinal) == true);
            dialogs.GetLogicalDescendants().OfType<TextBox>().Single().Text = "três";
            ClickButtonWithText(dialogs, "Adicionar");
            Assert.Equal("três", await add);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
