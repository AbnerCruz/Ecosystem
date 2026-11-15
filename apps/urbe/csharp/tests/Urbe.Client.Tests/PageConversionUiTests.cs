using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-157: an old page becomes a publication on command; its text is real notes, the page file is untouched.</summary>
    [Fact]
    public async Task Pages_AnOldPageBecomesAPublicationWithRealNotesAndStaysAsItWas() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pageconv-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Páginas"));
        File.WriteAllText(Path.Combine(vault, "Casa.md"), "# Casa\n\nProjeto da casa.\n");
        const string page = """{"kind":"page","version":1,"meta":{"title":"Portfólio"},"sections":[{"id":"a","type":"text","props":{"markdown":"Sobre mim."}},{"id":"b","type":"note","props":{"path":"Casa"}},{"id":"c","type":"contact","props":{"email":"a@b.c"}}]}""";
        var pageFile = Path.Combine(vault, "Páginas", "Portfólio.page.json");
        File.WriteAllText(pageFile, page);
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var convert = view.ConvertPageToPublicationAsync("Páginas/Portfólio.page.json");
            for (int i = 0; i < 100 && !view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("“contact”", StringComparison.Ordinal) == true);
            ClickButtonWithText(view.Dialogs, "Ok");
            await convert;
            Assert.Equal(page, File.ReadAllText(pageFile));
            Assert.Equal("Sobre mim.\n", File.ReadAllText(Path.Combine(vault, "Publicações", "Portfólio", "Portfólio.md")));
            var project = view.PublicationsStudio.Project!;
            Assert.Equal(2, project.Sections.Count);
            Assert.Equal(view.Documents!.Get("Casa.md")!.Id, project.Sections[1].Source.DocumentId);
            Assert.True(File.Exists(Path.Combine(vault, "Publicações", "Portfólio.publication.json")));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
