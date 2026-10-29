using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Assistant_AGeneratedImageCardReusedAfterLeavingTheScreenNeverDrawsAFreedPicture() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        // CST-111 (owner: the app closed near the end of a carousel run with up to ten generated images): the preview
        // freed its picture when it left the screen but kept it as its content; a cached card put back on screen drew it
        var dir = Path.Combine(Path.GetTempPath(), "urbe-image-reuse-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Imagens"));
        using (var png = File.Create(Path.Combine(vault, "Imagens", "slide1.png")))
            Pixels.ToBitmap(Enumerable.Repeat((byte)180, 8 * 8 * 4).ToArray(), 8, 8).Save(png, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = Path.Combine(dir, "ai");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var panel = view.Assistant;
            panel.Open();
            panel.Config.Providers.Add(new AiProviderConfig("p", "openrouter", ApiKey: "test"));
            panel.Config.ProviderId = "p";
            panel.Config.Model = "test-model";
            panel.Conversation.Messages.Add(new AiChatMessage { Id = "u", Role = "user", Content = [AiChatBlock.TextBlock("Faça o carrossel")] });
            panel.Conversation.Messages.Add(new AiChatMessage { Id = "a", Role = "assistant", Content = [new AiChatBlock { Type = "tool_use", Id = "img", Name = "generate_image", Input = new System.Text.Json.Nodes.JsonObject { ["prompt"] = "capa" } }] });
            panel.Conversation.Messages.Add(new AiChatMessage { Id = "r", Role = "user", Meta = new AiChatMeta { ToolResults = true }, Content = [new AiChatBlock { Type = "tool_result", ToolUseId = "img", Content = "Imagem guardada em Imagens/slide1.png." }] });
            panel.Conversation.Messages.Add(new AiChatMessage { Id = "f", Role = "assistant", Content = [AiChatBlock.TextBlock("Pronto.")] });
            panel.Conversation.Runs.Add(new AiRunRecord { Stopped = "end" });
            panel.RenderAll();
            Image? Shown() => panel.GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Source is Bitmap);
            for (int i = 0; i < 60 && Shown() is null; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            var picture = Shown();
            Assert.NotNull(picture);

            // the card leaves the visual tree and comes back, as the log does with the views it keeps when it is rebuilt
            Control card = picture!;
            while (card.Parent is Control up && up is not Panel { Children.Count: > 1 }) card = up;
            var holder = (Panel)card.Parent!;
            int at = holder.Children.IndexOf(card);
            holder.Children.RemoveAt(at);
            holder.Children.Insert(at, card);
            // whatever is on screen now, no image may hold a picture that was freed
            foreach (var image in panel.GetVisualDescendants().OfType<Image>())
                if (image.Source is Bitmap bitmap)
                {
                    bool freed;
                    try { freed = bitmap.PixelSize.Width <= 0; } catch (Exception) { freed = true; }
                    Assert.False(freed, "a freed picture is still shown");
                }
            Assert.NotNull(window.CaptureRenderedFrame());   // drawing it must not throw
            for (int i = 0; i < 60 && Shown() is null; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.NotNull(Shown());   // and the preview comes back
            return true;
        }
        finally { window.Close(); UrbeApp.AssistantFolder = before; try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
