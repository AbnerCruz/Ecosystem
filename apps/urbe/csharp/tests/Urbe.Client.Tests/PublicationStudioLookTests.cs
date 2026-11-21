using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-158 (capture at 412 px, 16/11/2026): no Explorer tip over the studio, each note offers only the move it can
    /// make, and the preview does not say a note's name twice. URBE_CAPTURE=file saves the picture.
    /// </summary>
    [Fact]
    public async Task PublicationStudio_PhoneLookHasNoStrayTipNoUselessMoveNoRepeatedTitle() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-studiolook-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Capítulo 1.md"), "# Capítulo 1\n\nUm começo.\n");
        File.WriteAllText(Path.Combine(vault, "Capítulo 2.md"), "# Capítulo 2\n\nOutro capítulo.\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var create = view.CreatePublicationAsync("Capítulo 1.md");
            Dispatcher.UIThread.RunJobs();
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Livro";
            ClickButtonWithText(view.Dialogs, "Criar");
            await create;
            var studio = view.PublicationsStudio;
            studio.AddSource(view.Documents!.Get("Capítulo 2.md")!.Id);
            view.MaybeLinkTip();   // the Explorer's tip, asked while the studio is open
            for (int i = 0; i < 30; i++) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(10); }
            Assert.DoesNotContain(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Ligue suas notas");
            var names = studio.GetLogicalDescendants().OfType<Button>().Select(b => AutomationProperties.GetName(b)).ToList();
            Assert.Equal(1, names.Count(n => n == "Mover fonte para baixo"));
            Assert.Equal(1, names.Count(n => n == "Mover fonte para cima"));
            Assert.DoesNotContain(studio.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Capítulo 1" && t.FontSize == 12 && t.IsVisible);
            // the Assistente from the studio, with the publication already named in the message being typed
            view.AskAssistantAboutPublication();
            Assert.Equal("Na publicação “Livro” (Publicações/Livro.publication.json): ", view.Assistant.Input.Text);
            if (Environment.GetEnvironmentVariable("URBE_CAPTURE") is { Length: > 0 } capture)
                window.CaptureRenderedFrame()!.Save(capture, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
