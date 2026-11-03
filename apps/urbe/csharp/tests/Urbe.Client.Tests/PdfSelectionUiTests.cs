using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task StudyPdf_SelectsWordsOnRasterAndReturnsToTheSourceRegion()=>Assert.True(await Session.Dispatch<bool>(async()=>
    {
        var(view,window,vault,dir)=await OpenCreateVault();
        try
        {
            byte[] bytes=StudyPdf();File.WriteAllBytes(Path.Combine(vault,"visual.pdf"),bytes);await view.OpenVaultAsync();view.OpenNoteByPath("Comece.md");
            await view.FilePreview.OpenPdfReferenceAsync("visual.pdf",2);
            var pdf=view.FilePreview.GetLogicalDescendants().OfType<PdfPagesView>().Single();
            await Settle(window,view);await pdf.EnableSelectionAsync();await Settle(window,view);
            var words=DocumentWordGeometry.Read(bytes,2);Assert.Equal("Exact",words[0].Text);Assert.Equal("two.",words[^1].Text);
            var selection=pdf.GetLogicalDescendants().OfType<PdfWordSelection>().Single();
            Point Center(DocumentWord word)=>new((word.Bounds.X+word.Bounds.Width/2)*selection.Bounds.Width,(word.Bounds.Y+word.Bounds.Height/2)*selection.Bounds.Height);
            selection.SelectAt(Center(words[0]),true);selection.SelectAt(Center(words[^1]),false);
            var quote=pdf.SelectedQuote();Assert.NotNull(quote);Assert.Equal("Exact quotation from page two.",quote.Text);Assert.NotNull(quote.Region);
            pdf.CiteSelection(StudyAnnotationKind.Highlight);await ReaderWait(()=>view.Dialogs.IsOpen);ClickButtonWithText(view.Dialogs,"Adicionar em Comece.md");
            await ReaderWait(()=>view.Notices.Any(n=>n.StartsWith("Citação salva em",StringComparison.Ordinal)));
            string saved=File.ReadAllText(Path.Combine(vault,"Comece.md"));Assert.Contains("**Destaque**",saved);Assert.Contains("&region=",saved);
            string target="visual.pdf#"+saved.Split("[[visual.pdf#",StringSplitOptions.None)[1].Split('|')[0];await view.Editor.FollowLinkAsync(target);await Settle(window,view);
            var restored=view.FilePreview.GetLogicalDescendants().OfType<PdfPagesView>().Single();Assert.Equal(2,restored.CurrentPage);
            Assert.NotNull(restored.FocusedRegion);Snapshot(window,"native-study-pdf-region.png");return true;
        }
        finally{window.Close();Directory.Delete(dir,true);}
    },CancellationToken.None));
}
