using Avalonia.Headless;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-159: on a phone, a note of the book becomes its cover with one tap; it goes first, stays there and is saved.</summary>
    [Fact]
    public async Task PublicationCover_ANoteBecomesTheCoverWithOneTap() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-cover-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Um.md"), "# A chuva\n\nCai devagar.\n");
            File.WriteAllText(Path.Combine(vault, "Capa.md"), "# Águas\n\nContos de Lia Vale\n");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 360, Height = 800, Content = view };
            window.Show();
            Button Named(string name) => view.PublicationsStudio.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == name);
            bool Has(string name) => view.PublicationsStudio.GetLogicalDescendants().OfType<Button>().Any(b => AutomationProperties.GetName(b) == name);
            void Click(Button b) => b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                var create = view.CreatePublicationAsync("Um.md");
                Dispatcher.UIThread.RunJobs();
                view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Águas";
                ClickButtonWithText(view.Dialogs, "Criar");
                await create;
                await Pump();
                var studio = view.PublicationsStudio;
                var capa = view.Documents!.Get("Capa.md")!;
                studio.UpdateProject(studio.Project!.AddSource(capa.Id) with { Design = studio.Project.Design with { Layout = "book" } });
                await Pump();
                var target = Path.Combine(vault, "Publicações", "Águas.publication.json");
                Click(view.PublicationsStudio.GetLogicalDescendants().OfType<Button>().Where(b => AutomationProperties.GetName(b) == "Usar como capa").Last());
                await Pump();
                Assert.Equal(capa.Id, studio.Project!.Cover!.Source.DocumentId);
                Assert.Equal(capa.Id, studio.Project.Sections[0].Source.DocumentId);
                Assert.True(Has("Editar nota de origem: Capa · Capa"));
                Assert.True(Has("Deixar de ser a capa"));
                if (Environment.GetEnvironmentVariable("URBE_CAPTURE") is { Length: > 0 } outDir)
                { for (int k = 0; k < 30; k++) { Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(5); } window.CaptureRenderedFrame()!.Save(Path.Combine(outDir, "cover-phone.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); }
                // the cover has no move button, and the piece after it moves down only if there is somewhere to go
                Assert.False(Has("Mover fonte para baixo") || Has("Mover fonte para cima"));
                for (int j = 0; j < 80 && !File.ReadAllText(target).Contains("\"cover\""); j++) await Pump();
                Assert.Contains("\"cover\"", File.ReadAllText(target));
                // the notes are untouched
                Assert.Equal("# Águas\n\nContos de Lia Vale\n", File.ReadAllText(Path.Combine(vault, "Capa.md")));
                Click(Named("Deixar de ser a capa"));
                await Pump();
                Assert.Null(studio.Project!.Cover);
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
