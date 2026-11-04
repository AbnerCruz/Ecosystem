using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private sealed class DelayedBookSelection : Border, IReaderTextSelection
    {
        public TaskCompletionSource<string> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> SelectedTextAsync() => Result.Task;
    }

    [Fact]
    public async Task BookSelection_DoesNotAttributeAnOldSelectionToANewChapter() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var factory = ContentViewers.Factory;
        try
        {
            DelayedBookSelection? selected = null;
            ContentViewers.Factory = (_, _) => Task.FromResult<Control?>(selected = new DelayedBookSelection());
            using var source = new EpubReadingDocument(File.ReadAllBytes(ReaderFixture("reflow.epub")));
            using var reader = new EpubStudyView("book.epub", source);
            await reader.GoToChapterAsync(0);
            BookReaderQuote? quote = null;
            reader.QuoteRequested += value => quote = value;
            var previous = selected!;
            ClickButtonWithText(reader, "Adicionar seleção à nota");
            await reader.GoToChapterAsync(1);
            previous.Result.SetResult("Trecho do capítulo anterior");
            for (int i = 0; i < 10; i++) { await Task.Delay(10); Avalonia.Threading.Dispatcher.UIThread.RunJobs(); }
            Assert.Null(quote);
            return true;
        }
        finally { ContentViewers.Factory = factory; }
    }, CancellationToken.None));

    [Fact]
    public async Task MediaSelection_RejectsAFileChangedAfterThePlayerWasOpened() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        var factory = ContentViewers.Factory;
        try
        {
            ContentViewers.Factory = (r, _) => Task.FromResult<Control?>(r.Kind == VaultFileKinds.Audio ? new StudyMedia() : null);
            File.WriteAllBytes(Path.Combine(vault, "audio.mp3"), [1, 2, 3, 4]);
            await view.OpenVaultAsync();
            await view.FilePreview.OpenAsync("audio.mp3");
            File.WriteAllBytes(Path.Combine(vault, "audio.mp3"), [9, 8, 7, 6]);
            ClickButtonWithText(view.FilePreview, "Anotar instante");
            await ReaderWait(() => view.Dialogs.IsOpen || view.FilePreview.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("mudou", StringComparison.Ordinal) == true));
            Assert.False(view.Dialogs.IsOpen);
            return true;
        }
        finally { ContentViewers.Factory = factory; window.Close(); Directory.Delete(dir, true); }
    }, CancellationToken.None));

    [Fact]
    public async Task BookSearch_OpensAndSelectsTheExactOccurrenceInTextMode() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var source = new EpubReadingDocument(File.ReadAllBytes(ReaderFixture("reflow.epub")));
        using var reader = new EpubStudyView("book.epub", source);
        await reader.GoToChapterAsync(0);
        await reader.SearchAsync("Parágrafo 20.");
        var results = reader.GetLogicalDescendants().OfType<ListBox>().Single();
        results.SelectedIndex = 0;
        await ReaderWait(() => reader.CurrentOffset > 0);
        var text = reader.GetLogicalDescendants().OfType<TextBox>().Single(t => t.IsReadOnly);
        await ReaderWait(() => text.SelectedText == "Parágrafo 20.");
        Assert.Equal("Parágrafo 20.", text.SelectedText);
        BookReaderQuote? quote = null;
        reader.QuoteRequested += value => quote = value;
        ClickButtonWithText(reader, "Adicionar seleção à nota");
        await ReaderWait(() => quote is not null);
        Assert.Equal(text.SelectionStart, quote!.TextOffset);
        Assert.Equal(text.SelectedText.Length, quote.TextLength);
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Browser_LeavingVideoClearsItsTimestampActions() => Assert.True(await Session.Dispatch<bool>(() =>
    {
        var old = StudyWebViews.CreateBrowser;
        try
        {
            FakeStudyWeb? browser = null;
            StudyWebViews.CreateBrowser = address => { browser = new FakeStudyWeb(); browser.Navigate(address); return browser; };
            var panel = new StudyBrowserPanel();
            Assert.True(panel.Open("https://www.youtube.com/watch?v=abcdefghijk&t=42"));
            var mark = panel.GetLogicalDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Anotar instante");
            Assert.True(((Control)mark.Parent!).IsVisible);
            browser!.Navigate("https://example.org/article");
            Assert.False(((Control)mark.Parent!).IsVisible);
            panel.Close();
            return Task.FromResult(true);
        }
        finally { StudyWebViews.CreateBrowser = old; }
    }, CancellationToken.None));
}
