using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-148: in a long conversation (4 MB of tool results), sending a message never holds the UI thread long: the
    /// request is written and sent off it, and the streamed text comes back below input priority, complete and in
    /// order. The stall is measured as the longest wait of an input-priority tick (URBE_PERF=1 asserts it).
    /// </summary>
    [Fact]
    public async Task Assistant_ALongConversationDoesNotHoldTheUiThread() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-stall-" + Guid.NewGuid().ToString("N"));
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(dir, "assistant");
        var view = new MainView();
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Run(AssistantPanel panel, string text)
        {
            panel.Send(text);
            for (int i = 0; i < 400 && panel.CurrentRun is null; i++) await Task.Delay(2);
            await panel.CurrentRun!;
            for (int i = 0; i < 50; i++) await Task.Delay(5);
        }
        try
        {
            await view.LoadAsync([new CityNote("Comece.md", "# Comece")]);
            var model = new ScriptedModel();
            var panel = view.Assistant;
            panel.Open();
            panel.SelectAgent("geral");
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Config.Providers.Add(new AiProviderConfig("openrouter", "openrouter", ApiKey: "sk-stall"));
            panel.Config.ProviderId = "openrouter"; panel.Config.Model = "org/m";
            panel.Conversation.ProviderId = "openrouter"; panel.Conversation.Model = "org/m";
            var big = new string('x', 20_000);
            for (int i = 0; i < 200; i++)
            {
                panel.Conversation.Messages.Add(new AiChatMessage { Id = "u" + i, Role = "user", Content = [AiChatBlock.TextBlock("pergunta " + i)] });
                panel.Conversation.Messages.Add(new AiChatMessage { Id = "a" + i, Role = "assistant", Content = [new AiChatBlock { Type = "tool_use", Id = "t" + i, Name = "read_note", Input = System.Text.Json.Nodes.JsonNode.Parse("{\"path\":\"Comece\"}") }] });
                panel.Conversation.Messages.Add(new AiChatMessage { Id = "r" + i, Role = "user", Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "t" + i, Content = big }] });
            }
            model.Chats.Enqueue(Sse("""{"choices":[{"delta":{"content":"ok"}}]}"""));
            await Run(panel, "aquecer");   // the release APK is compiled ahead of time: no JIT in what is measured

            var words = Enumerable.Range(0, 300).Select(i => " palavra" + i).ToList();
            model.Chats.Enqueue(Sse([.. words.Select(w => "{\"choices\":[{\"delta\":{\"content\":" + System.Text.Json.JsonSerializer.Serialize(w) + "}}]}")]));
            long worst = 0, last = 0;
            var clock = Stopwatch.StartNew();
            var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(5), DispatcherPriority.Input, (_, _) =>
            { var now = clock.ElapsedMilliseconds; worst = Math.Max(worst, now - last); last = now; });
            timer.Start();
            await Run(panel, "Mais uma");
            timer.Stop();
            var answer = string.Concat(panel.Conversation.Messages.Last().Content.Where(b => b.Type == "text").Select(b => b.Text));
            Assert.Equal(string.Concat(words), answer);   // every piece, in order
            if (Environment.GetEnvironmentVariable("URBE_PERF") == "1") Assert.True(worst < 400, "UI thread held " + worst + " ms");
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
