using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private static string ReaderFixture(string file)=>Path.Combine(AppContext.BaseDirectory,"fixtures",file);
    private static async Task ReaderWait(Func<bool> ready)
    {for(int i=0;i<300&&!ready();i++){await Task.Delay(10);Dispatcher.UIThread.RunJobs();}Assert.True(ready());}
    [Fact]
    public async Task StudyBook_QuotesToCanonicalNoteAndReturnsToTheVerifiedChapter()=>Assert.True(await Session.Dispatch<bool>(async()=>
    {
        var(view,window,vault,dir)=await OpenCreateVault();
        try
        {
            File.Copy(ReaderFixture("reflow.epub"),Path.Combine(vault,"book.epub"));await view.OpenVaultAsync();view.OpenNoteByPath("Comece.md");
            await view.FilePreview.OpenAsync("book.epub");
            var book=view.FilePreview.GetLogicalDescendants().OfType<EpubStudyView>().Single();await book.GoToChapterAsync(1);
            var text=book.GetLogicalDescendants().OfType<TextBox>().Single(t=>t.IsReadOnly);Assert.Contains("Texto da nota de rodapé.",text.Text);
            text.SelectionStart=0;text.SelectionEnd=text.Text!.Length;ClickButtonWithText(book,"Adicionar seleção à nota");
            await ReaderWait(()=>view.Dialogs.IsOpen);ClickButtonWithText(view.Dialogs,"Adicionar em Comece.md");
            await ReaderWait(()=>view.Notices.Any(n=>n.StartsWith("Citação salva em",StringComparison.Ordinal)));
            string saved=File.ReadAllText(Path.Combine(vault,"Comece.md"));Assert.Contains("Texto da nota de rodapé.",saved);
            string target="book.epub#"+saved.Split("[[book.epub#",StringSplitOptions.None)[1].Split('|')[0];
            await view.Editor.FollowLinkAsync(target);
            Assert.Equal(1,view.FilePreview.GetLogicalDescendants().OfType<EpubStudyView>().Single().CurrentChapter);
            File.AppendAllText(Path.Combine(vault,"book.epub"),"changed");await view.Editor.FollowLinkAsync(target);
            Assert.Contains(view.FilePreview.GetLogicalDescendants().OfType<TextBlock>(),t=>t.Text=="Fonte alterada");
            return true;
        }
        finally{window.Close();Directory.Delete(dir,true);}
    },CancellationToken.None));

    private sealed class CompletedOcr:Border,IReaderComputation
    {public Task<string> ReadComputationAsync()=>Task.FromResult("{\"status\":\"done\",\"text\":\"Texto reconhecido para revisar.\",\"confidence\":87,\"words\":[]}");}
    [Fact]
    public async Task StudyOcr_RequiresExplicitRunAllowsReviewAndSavesUncertaintyWithSourceRegion()=>Assert.True(await Session.Dispatch<bool>(async()=>
    {
        var(view,window,vault,dir)=await OpenCreateVault();var factory=ContentViewers.Factory;
        try
        {
            ContentViewers.Factory=(r,t)=>r.ReaderEngine==ReaderContentEngine.Ocr?Task.FromResult<Control?>(new CompletedOcr()):factory?.Invoke(r,t)??Task.FromResult<Control?>(null);
            File.Copy(ReaderFixture("scan.png"),Path.Combine(vault,"scan.png"));await view.OpenVaultAsync();view.OpenNoteByPath("Comece.md");
            await view.FilePreview.OpenAsync("scan.png");ClickButtonWithText(view.FilePreview,"Reconhecer região (OCR)");
            await ReaderWait(()=>view.FilePreview.GetLogicalDescendants().OfType<OcrStudyView>().Any());
            var ocr=view.FilePreview.GetLogicalDescendants().OfType<OcrStudyView>().Single();
            Assert.DoesNotContain(ocr.GetLogicalDescendants(),c=>c is CompletedOcr);
            ClickButtonWithText(ocr,"Reconhecer região");
            await ReaderWait(()=>ocr.GetLogicalDescendants().OfType<TextBox>().Any(t=>t.Text=="Texto reconhecido para revisar."));
            var text=ocr.GetLogicalDescendants().OfType<TextBox>().Single(t=>t.AcceptsReturn);text.Text="Texto corrigido pelo usuário.";
            ClickButtonWithText(ocr,"Adicionar à nota");await ReaderWait(()=>view.Dialogs.IsOpen);ClickButtonWithText(view.Dialogs,"Adicionar em Comece.md");
            await ReaderWait(()=>view.Notices.Any(n=>n.StartsWith("Citação salva em",StringComparison.Ordinal)));
            string saved=File.ReadAllText(Path.Combine(vault,"Comece.md"));Assert.Contains("Texto corrigido pelo usuário.",saved);
            Assert.Contains("OCR estimado, ainda não revisado",saved);Assert.Contains("&region=0,0,1,1",saved);Assert.Contains("scan.png#image=1&sha256=",saved);
            window.Width=390;window.Height=820;await Settle(window,view);Snapshot(window,"native-study-ocr-review.png");
            return true;
        }
        finally{ContentViewers.Factory=factory;window.Close();Directory.Delete(dir,true);}
    },CancellationToken.None));

    private sealed class StudyMedia:Border,IReaderMediaPosition
    {
        public readonly List<double> Seeks=[];
        public Task<double?> ReadMediaSecondAsync()=>Task.FromResult<double?>(42);
        public Task SeekMediaAsync(double second){Seeks.Add(second);return Task.CompletedTask;}
    }
    [Fact]
    public async Task StudyMedia_MarksVerifiedTimeAndReturnsThroughTheSameFileViewer()=>Assert.True(await Session.Dispatch<bool>(async()=>
    {
        var(view,window,vault,dir)=await OpenCreateVault();var factory=ContentViewers.Factory;StudyMedia? latest=null;
        try
        {
            ContentViewers.Factory=(r,t)=>r.Kind==VaultFileKinds.Audio?Task.FromResult<Control?>(latest=new StudyMedia()):factory?.Invoke(r,t)??Task.FromResult<Control?>(null);
            File.WriteAllBytes(Path.Combine(vault,"audio.mp3"),Enumerable.Range(0,200).Select(i=>(byte)i).ToArray());await view.OpenVaultAsync();view.OpenNoteByPath("Comece.md");
            await view.FilePreview.OpenAsync("audio.mp3");ClickButtonWithText(view.FilePreview,"Anotar instante");await ReaderWait(()=>view.Dialogs.IsOpen);ClickButtonWithText(view.Dialogs,"Adicionar em Comece.md");
            await ReaderWait(()=>view.Notices.Any(n=>n.StartsWith("Citação salva em",StringComparison.Ordinal)));
            string saved=File.ReadAllText(Path.Combine(vault,"Comece.md"));Assert.Contains("audio.mp3#time=42&sha256=",saved);
            string target="audio.mp3#"+saved.Split("[[audio.mp3#",StringSplitOptions.None)[1].Split('|')[0];
            await view.Editor.FollowLinkAsync(target);await ReaderWait(()=>latest?.Seeks.Contains(42)==true);
            await view.Editor.FollowLinkAsync("https://example.org/study");Assert.True(view.StudyBrowser.IsVisible);
            return true;
        }
        finally{ContentViewers.Factory=factory;window.Close();Directory.Delete(dir,true);}
    },CancellationToken.None));
}
