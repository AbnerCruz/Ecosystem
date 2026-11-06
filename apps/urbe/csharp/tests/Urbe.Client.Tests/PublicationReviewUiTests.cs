using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-179/180: "Verificar publicação" lists what to fix and opens the note at the line.</summary>
    [Fact]
    public async Task PublicationReview_ListsWhatToFixAndOpensTheNoteThere() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pubreview-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Cap.md"), "# Capítulo\n\nTexto.\n\n![](figura.png)\n\nVer [[Outra]] e [[Sumiu]].\n");
        File.WriteAllText(Path.Combine(vault, "Outra.md"), "# Outra\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Pump() { for (int j = 0; j < 12; j++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(15); } }
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var create = view.CreatePublicationAsync("Cap.md");
            Dispatcher.UIThread.RunJobs();
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Livro";
            ClickButtonWithText(view.Dialogs, "Criar");
            await create;
            await Pump();

            var review = view.ReviewPublicationAsync();
            for (int i = 0; i < 100 && !view.Dialogs.IsOpen; i++) await Pump();
            var issues = view.LastPublicationReview;
            Assert.Contains(issues, i => i.Message.StartsWith("Imagem não encontrada no vault: figura.png", StringComparison.Ordinal) && i.Line == 5);
            Assert.Contains(issues, i => i.Message.StartsWith("Imagem sem descrição", StringComparison.Ordinal));
            Assert.Contains(issues, i => i.Message.Contains("[[Sumiu]]"));
            Assert.DoesNotContain(issues, i => i.Message.Contains("[[Outra]]"));
            var texts = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains(texts, t => t.StartsWith("1 erro e 2 avisos", StringComparison.Ordinal));
            ClickButtonWithText(view.Dialogs, "Aviso · Link para uma nota que não existe: [[Sumiu]].");
            await review;
            await Pump();
            Assert.Equal("Cap.md", view.Editor.Current?.Path);
            var text = view.Editor.Editor.Text ?? "";
            int lineStart = 0; for (int i = 1; i < 7; i++) lineStart = text.IndexOf('\n', lineStart) + 1;
            Assert.Equal(lineStart, view.Editor.Editor.CaretIndex);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
