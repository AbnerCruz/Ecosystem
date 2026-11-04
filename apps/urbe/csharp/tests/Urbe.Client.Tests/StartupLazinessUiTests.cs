using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// The Assistente (conversations, agents, its storage) is built the first time it opens. Beta.38–46 built it at every
    /// start, through a visibility subscription in MainView's constructor; opening a note or a file must not build it either.
    /// </summary>
    [Fact]
    public async Task Startup_TheAssistenteIsBuiltOnlyWhenItOpens() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            Assert.False(view.AssistantCreated);
            view.OpenNoteByPath("Tutorial/Comece aqui.md");
            await Settle(window, view);
            await view.Editor.BackAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.AssistantCreated);
            view.Navigation.Items.First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Assistente")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.AssistantCreated);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
