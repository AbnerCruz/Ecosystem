using Avalonia.Controls;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
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
            var mark = panel.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == "Anotar instante");
            Assert.True(((Control)mark.Parent!).IsVisible);
            browser!.Navigate("https://example.org/article");
            Assert.False(((Control)mark.Parent!).IsVisible);
            panel.Close();
            return Task.FromResult(true);
        }
        finally { StudyWebViews.CreateBrowser = old; }
    }, CancellationToken.None));
}
