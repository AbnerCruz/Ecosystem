using Avalonia.Input;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-243: a question sent while a PDF is open carries the page, revision and exact link as hidden context.</summary>
    [Fact]
    public async Task Assistant_AQuestionWithAPdfOpenCarriesItsPageAndExactLink() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var before = UrbeApp.AssistantFolder;
        UrbeApp.AssistantFolder = null;
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            byte[] bytes = StudyPdf(); File.WriteAllBytes(Path.Combine(vault, "visual.pdf"), bytes);
            await view.OpenVaultAsync(); view.OpenNoteByPath("Comece.md");
            await view.FilePreview.OpenPdfReferenceAsync("visual.pdf", 2);
            await Settle(window, view);
            string sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.Equal(("visual.pdf", sha, 2), (view.FilePreview.CurrentReading!.Path, view.FilePreview.CurrentReading.Revision, view.FilePreview.CurrentReading.Location.Part));

            var panel = view.Assistant;
            panel.Open();
            var model = new OneAnswer();
            panel.Providers = new AssistantProviders(model, (_, _) => Task.CompletedTask);
            panel.Config.Providers.Add(new AiProviderConfig("or", "openrouter", "OpenRouter", "sk-or-test"));
            panel.Config.ProviderId = "or";
            panel.Config.Model = "acme/text";
            panel.Input.Text = "O que diz esta página?";
            panel.Input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            for (int i = 0; i < 300 && (panel.CurrentRun is null || !panel.CurrentRun.IsCompleted); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            var body = Assert.Single(model.Bodies);
            Assert.Contains("fonte aberta no leitor = visual.pdf", body);
            Assert.Contains("read_document (path \\\"visual.pdf\\\", page 2)", body);
            Assert.Contains("[[visual.pdf#page=2&sha256=" + sha, body);
            return true;
        }
        finally { window.Close(); Directory.Delete(dir, true); UrbeApp.AssistantFolder = before; }
    }, CancellationToken.None));
}
