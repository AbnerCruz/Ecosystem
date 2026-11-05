using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-226: trashing a note others depend on says what depends on it; the list opens each dependent.</summary>
    [Fact]
    public async Task NoteUsage_WarnsBeforeTrash_AndListsDependents() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            var app = (IAssistantApp)view;
            await app.CreateNoteAsync("Fonte.md", "# Fonte\n\nTexto.\n");
            await app.CreateNoteAsync("Usa.md", "Veja ![[Fonte]].\n");
            var fonte = view.Documents!.Get("Fonte.md")!;
            await app.CreateNoteAsync("Publicações/Livro.publication.json", PublicationProjectCodec.Serialize(PublicationProject.Create("Livro", fonte.Id)));

            var trashing = view.SelectionActionAsync("delete", ["Fonte.md"]);
            Dispatcher.UIThread.RunJobs();
            var texts = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains(texts, t => t.Contains("Usada em 1 publicação, 1 incorporação"));
            view.Dialogs.Cancel();
            await trashing;
            Assert.NotNull(view.Documents!.Get("Fonte.md")); // cancelled: nothing happened

            var listing = view.ShowNoteUsageAsync("Fonte.md");
            Dispatcher.UIThread.RunJobs();
            var labels = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains("Livro", labels);
            Assert.Contains("Usa", labels);
            ClickButtonWithText(view.Dialogs, "Livro");
            await listing;
            Assert.Equal("Livro", view.PublicationsStudio.Project?.Title);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
