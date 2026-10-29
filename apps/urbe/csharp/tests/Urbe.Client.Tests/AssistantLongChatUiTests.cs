using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_ALongConversationOpensAtOnce() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            var panel = view.Assistant;
            panel.Open();
            panel.HandleBack();   // the list of agents on the phone: back to the chat
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter"));
            var msgs = panel.Conversation.Messages;
            var answer = "## Resumo\n\nUm parágrafo com **negrito**, *itálico* e um [[Link]] para outra nota, longo o bastante para quebrar em várias linhas no celular.\n\n- primeiro item\n- segundo item com `código`\n- terceiro\n\n```js\nconst x = 1;\nconsole.log(x);\n```\n\nOutro parágrafo para fechar a resposta.";
            for (int i = 0; i < 200; i++)
            {
                msgs.Add(new AiChatMessage { Id = "u" + i, Role = "user", Content = [AiChatBlock.TextBlock("Pergunta número " + i + " sobre as minhas notas")] });
                msgs.Add(new AiChatMessage { Id = "a" + i, Role = "assistant", Content = [AiChatBlock.TextBlock(answer + " (" + i + ")")] });
            }
            var tap = Stopwatch.StartNew();
            panel.RenderAll();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            tap.Stop();
            PerfLog("assistant.open400 " + tap.ElapsedMilliseconds + " ms");
            // alone (URBE_PERF=1): 3 032 ms before (every message made and measured), ~240 ms now
            if (Environment.GetEnvironmentVariable("URBE_PERF") == "1") Assert.True(tap.ElapsedMilliseconds < 800, tap.ElapsedMilliseconds + " ms");
            await Settle(window, view);
            Snapshot(window, "assistant-long-chat");
            var items = panel.Log.Children.OfType<Ui.Deferred>().ToList();
            Assert.Equal(400, items.Count);                        // every message is in the chat
            Assert.InRange(items.Count(d => d.IsMade), 2, 60);     // only those near the screen were made
            Assert.True(items[^1].IsMade);
            var scroll = panel.Log.GetVisualAncestors().OfType<ScrollViewer>().First();
            Assert.True(scroll.Extent.Height - scroll.Offset.Y - scroll.Viewport.Height < 2, "opens at the end");
            Assert.Contains(items[^1].GetLogicalDescendants().OfType<SelectableTextBlock>(), t => t.Text?.Contains("(199)") == true ||
                t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text?.Contains("(199)") == true) == true);

            // a full render (a run that ended, a setting changed) makes again only what was made
            var made = items.Count(d => d.IsMade);
            tap.Restart();
            panel.RenderAll();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            tap.Stop();
            PerfLog("assistant.renderAgain400 " + tap.ElapsedMilliseconds + " ms");
            await Settle(window, view);
            Assert.InRange(panel.Log.Children.OfType<Ui.Deferred>().Count(d => d.IsMade), made - 5, made + 5);
            Assert.True(scroll.Extent.Height - scroll.Offset.Y - scroll.Viewport.Height < 2, "still at the end");

            // the first question is reached by scrolling up
            scroll.Offset = default;
            await Settle(window, view);
            var first = (Ui.Deferred)panel.Log.Children[0];
            Assert.True(first.IsMade);
            Assert.Contains(first.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Pergunta número 0 sobre as minhas notas");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
