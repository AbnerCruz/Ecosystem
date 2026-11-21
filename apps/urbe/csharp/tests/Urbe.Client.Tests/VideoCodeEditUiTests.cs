using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#1004: a code element's program is edited by hand; a broken one is refused with its reason, a good one replaces it.</summary>
    [Fact]
    public async Task VideoCode_AProgramIsEditedByHandAndCheckedBeforeSaving() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var store = new DocumentStore();
        var doc = store.Upsert(new DocumentInput { Path = "Cena.md", Content = "# Cena\n\nTexto.\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = VideoComposition.Synced(PublicationProject.Create("V", doc.Id) with { Design = new PublicationDesign { Layout = "video" } });
        var element = new VideoElement { Id = "prog", Kind = "code", Text = "function draw(ctx){ ctx.circle(1,1,1); }", Width = 1, Height = 1 };
        project = project with { Video = project.Video!.WithScene(project.Video.Scenes[0] with { Elements = [element] }) };
        var studio = new PublicationStudioView();
        studio.Open("Publicações/V.publication.json", project, catalog);
        var view = new MainView();
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        var editing = view.EditVideoCodeAsync(studio, studio.Project!.Video!.Scenes[0], element);
        for (int i = 0; i < 200 && !view.Dialogs.IsOpen; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
        var box = view.Dialogs.GetLogicalDescendants().OfType<TextBox>().Single();
        Assert.True(box.AcceptsReturn);
        box.Text = "function draw( {";
        Dispatcher.UIThread.RunJobs();   // typing settles before the button is pressed
        ClickButtonWithText(view.Dialogs, "Salvar");
        Dispatcher.UIThread.RunJobs();
        Assert.True(view.Dialogs.IsOpen);   // refused: the dialog stays, saying why
        var shown = string.Join(" | ", view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text));
        Assert.True(shown.Contains("JavaScript válido"), shown);
        box.Text = "function draw(ctx, t){ ctx.fill('#f00'); ctx.rect(0, 0, 10, 10); }";
        Dispatcher.UIThread.RunJobs();
        ClickButtonWithText(view.Dialogs, "Salvar");
        await editing;
        Assert.Equal("function draw(ctx, t){ ctx.fill('#f00'); ctx.rect(0, 0, 10, 10); }", studio.Project!.Video!.Scenes[0].Elements.Single().Text);
        window.Close();
        return true;
    }, CancellationToken.None));
}
