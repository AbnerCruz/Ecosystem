using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#955: a source link of the conversation shows what it is before opening; Copiar endereço copies it.</summary>
    [Fact]
    public async Task Citation_ASourceShowsWhatItIsBeforeOpening() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 800, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync([]);
            var panel = view.Assistant;
            panel.Conversation.WebSources.Add(new AiWebSource("S1", "Fotossíntese — Enciclopédia", "https://example.org/f", "https://example.org/f", "example.org", "2026-05-01", "2026-11-16", "página acessada"));
            var follow = panel.FollowWebLinkAsync("https://example.org/f");
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Dialogs.IsOpen);
            var texts = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("[S1] Fotossíntese — Enciclopédia", texts);
            Assert.Contains(texts, t => t?.Contains("example.org · 2026-05-01 · página acessada", StringComparison.Ordinal) == true);
            ClickButtonWithText(view.Dialogs, "Copiar endereço");
            await follow;
            Assert.False(view.Dialogs.IsOpen);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
