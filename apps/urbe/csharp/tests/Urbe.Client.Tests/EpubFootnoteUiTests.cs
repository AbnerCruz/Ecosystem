using System.IO.Compression;
using System.Text;
using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private static byte[] FootnoteBook()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Put(string name, string text) { using var w = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8); w.Write(text); }
            Put("mimetype", "application/epub+zip");
            Put("META-INF/container.xml", "<container xmlns='urn:oasis:names:tc:opendocument:xmlns:container' version='1.0'><rootfiles><rootfile full-path='b.opf' media-type='application/oebps-package+xml'/></rootfiles></container>");
            Put("b.opf", "<package xmlns='http://www.idpf.org/2007/opf' version='3.0' unique-identifier='id'><metadata xmlns:dc='http://purl.org/dc/elements/1.1/'><dc:identifier id='id'>n</dc:identifier><dc:title>Notas</dc:title><dc:language>pt</dc:language></metadata><manifest><item id='nav' href='nav.xhtml' media-type='application/xhtml+xml' properties='nav'/><item id='a' href='a.xhtml' media-type='application/xhtml+xml'/><item id='b' href='b.xhtml' media-type='application/xhtml+xml'/></manifest><spine><itemref idref='a'/><itemref idref='b'/></spine></package>");
            Put("nav.xhtml", "<html xmlns='http://www.w3.org/1999/xhtml' xmlns:epub='http://www.idpf.org/2007/ops'><head><title>S</title></head><body><nav epub:type='toc'><ol><li><a href='a.xhtml'>A</a></li><li><a href='b.xhtml'>B</a></li></ol></nav></body></html>");
            Put("a.xhtml", "<html xmlns='http://www.w3.org/1999/xhtml'><head><title>A</title></head><body><p>Fato<a role='doc-noteref' href='b.xhtml#n1'>1</a>.</p></body></html>");
            Put("b.xhtml", "<html xmlns='http://www.w3.org/1999/xhtml'><head><title>B</title></head><body><h1 id='t'>Notas</h1><p id='n1' role='doc-endnote'>A fonte do fato.</p></body></html>");
        }
        return stream.ToArray();
    }

    [Fact]
    public async Task Book_AFootnoteOpensUnderThePageAndTheChapterStays() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var source = new EpubReadingDocument(FootnoteBook());
        using var reader = new EpubStudyView("notas.epub", source);
        await reader.GoToChapterAsync(0);
        await reader.FollowBookLinkAsync(1, "n1");
        Assert.Equal("A fonte do fato.", reader.ShownNote);
        Assert.Equal(0, reader.CurrentChapter);
        // Ir até a nota: the notes' chapter, and the card goes away
        ClickButtonWithText(reader, "Ir até a nota");
        await ReaderWait(() => reader.CurrentChapter == 1);
        Assert.Null(reader.ShownNote);
        // a link to a heading is not a note: it goes there
        await reader.GoToChapterAsync(0);
        await reader.FollowBookLinkAsync(1, "t");
        Assert.Null(reader.ShownNote);
        Assert.Equal(1, reader.CurrentChapter);
        return true;
    }, CancellationToken.None));

    private sealed class FakePages : Border, IReaderTextLocation
    {
        public int Page, Pages = 3;
        public bool Paged;
        public (int Offset, double Scroll)? Restored;
        public Task<ReaderTextPosition?> ReadTextPositionAsync(bool selection) => Task.FromResult<ReaderTextPosition?>(null);
        public Task RestoreTextPositionAsync(int offset, int length, double scroll) { Restored = (offset, scroll); Page = scroll >= 1 ? Pages - 1 : 0; return Task.CompletedTask; }
        public Task GoToReaderAnchorAsync(string id) => Task.CompletedTask;
        public Task ApplyReaderAppearanceAsync(string theme, double margin, bool reflow, bool paged = false) { Paged = paged; return Task.CompletedTask; }
        public Task<bool> TurnPageAsync(int direction)
        {
            int to = direction == int.MaxValue ? Pages - 1 : Page + direction;
            if (!Paged || to < 0 || to >= Pages || to == Page) return Task.FromResult(false);
            Page = to;
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task Book_ByPagesTheArrowsTurnPagesThenChaptersAndBackOpensTheLastPage() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var factory = ContentViewers.Factory;
        try
        {
            var views = new List<FakePages>();
            ContentViewers.Factory = (_, _) => { var v = new FakePages(); views.Add(v); return Task.FromResult<Control?>(v); };
            using var source = new EpubReadingDocument(FootnoteBook());
            using var reader = new EpubStudyView("notas.epub", source);
            await reader.GoToChapterAsync(0);
            // scrolling (the default): the arrows go by chapters
            await reader.TurnAsync(1);
            Assert.Equal(1, reader.CurrentChapter);
            await reader.GoToChapterAsync(0);
            await reader.SetPagedAsync(true);
            var view = views[^1];
            Assert.True(view.Paged);
            await reader.TurnAsync(1); await reader.TurnAsync(1);
            Assert.Equal((0, 2), (reader.CurrentChapter, view.Page));
            await reader.TurnAsync(1);   // the chapter's last page: the next chapter
            Assert.Equal(1, reader.CurrentChapter);
            await reader.TurnAsync(-1);  // its first page: the previous chapter, at its last page
            Assert.Equal(0, reader.CurrentChapter);
            Assert.Equal(1d, views[^1].Restored!.Value.Scroll);
            Assert.Equal(2, views[^1].Page);
            return true;
        }
        finally { ContentViewers.Factory = factory; }
    }, CancellationToken.None));
}
