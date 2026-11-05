using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    private static int ShownPdfPage(FilePreviewPanel preview) =>
        preview.GetLogicalDescendants().OfType<PdfStudyView>().FirstOrDefault()?.CurrentPage ??
        preview.GetLogicalDescendants().OfType<PdfPagesView>().FirstOrDefault()?.CurrentPage ?? 0;

    /// <summary>
    /// CST-241: a PDF read to page 2 is listed in "Continuar lendo" as "página 2 de 3", reopens there from the Estudo
    /// pane, and a changed file says its saved position was not applied. The text readers' look (PDF text layer, plain
    /// text) is one preference that the next text file starts with. Nothing is written to the vault for reading.
    /// </summary>
    [Fact]
    public async Task ContinueReading_ListsPdfProgressReopensAtThePageAndKeepsTheTextReadersLook() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            File.WriteAllBytes(Path.Combine(vault, "study.pdf"), StudyPdf());
            File.WriteAllText(Path.Combine(vault, "notas.txt"), "linha um\nlinha dois\n");
            await view.OpenVaultAsync();
            (string, DateTime)[] VaultFiles() => Directory.GetFiles(vault, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + ".urbe" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal).Select(f => (f, File.GetLastWriteTimeUtc(f))).ToArray();
            var vaultFiles = VaultFiles();
            Assert.Empty(view.ContinueReading());

            await view.FilePreview.OpenAsync("study.pdf");
            if (!view.FilePreview.GetLogicalDescendants().OfType<PdfStudyView>().Any()) ClickButtonWithText(view.FilePreview, "Texto selecionável");
            await ReaderWait(() => view.FilePreview.GetLogicalDescendants().OfType<PdfStudyView>().Any());
            var reader = view.FilePreview.GetLogicalDescendants().OfType<PdfStudyView>().Single();
            await ReaderWait(() => reader.PageCount == 3);
            await reader.GoToPageAsync(2);
            // the text layer's "Aa": a bigger letter and sepia, here at once
            var look = view.FilePreview.CurrentTextLook!;
            Assert.Equal(PdfStudyView.DefaultFontSize, look.FontSize);
            Assert.Contains(view.FilePreview.GetLogicalDescendants().OfType<Button>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Aparência do texto" && b.IsVisible);
            view.FilePreview.SetTextLook(look.Choices().Single(c => c.Label == "Letra maior").Next);
            view.FilePreview.SetTextLook(view.FilePreview.CurrentTextLook!.Choices().Single(c => c.Label == "Tema: claro").Next);
            view.FilePreview.SetTextLook(view.FilePreview.CurrentTextLook!.Choices().Single(c => c.Label == "Tema: sépia").Next);
            var text = reader.GetLogicalDescendants().OfType<TextBox>().Single(t => t.IsReadOnly);
            Assert.Equal(19, text.FontSize);
            Assert.True(await view.FilePreview.TryCloseAsync());

            // "Continuar lendo": the page out of the PDF's pages, never an invented total
            var item = Assert.Single(view.ContinueReading());
            Assert.Equal("study.pdf", item.Path);
            Assert.Equal("página 2 de 3 · 67%", item.Progress.Position);
            Assert.Equal("agora há pouco", item.LastRead);

            // a plain text file starts with the text readers' look; it is not listed as a reading to continue
            await view.FilePreview.OpenAsync("notas.txt");
            Assert.Equal("text", view.FilePreview.Shown);
            var lines = view.FilePreview.GetLogicalDescendants().OfType<SelectableTextBlock>().First(t => (t.Text ?? "").Contains("linha um", StringComparison.Ordinal));
            Assert.Equal(19, lines.FontSize);
            Assert.Equal("sepia", view.FilePreview.CurrentTextLook!.Theme);
            view.FilePreview.SetTextLook(view.FilePreview.CurrentTextLook!.Choices().Single(c => c.Label == "Letra menor").Next);
            Assert.Equal(17, lines.FontSize);
            Assert.True(await view.FilePreview.TryCloseAsync());
            Assert.Single(view.ContinueReading());
            Assert.Equal(17, view.ReadingState.DefaultFor(ReaderKinds.Text)!.FontSize);

            // the command palette has it; the Estudo pane lists it and opens it where it was left
            Assert.True(view.Search.Has("reader.continue"));
            view.OpenNoteByPath("Comece.md");
            var pane = view.Editor.Pinned;
            pane.Open();
            await Settle(window, view);
            var row = Assert.Single(pane.ContinueRows);
            Assert.Contains("página 2 de 3", Avalonia.Automation.AutomationProperties.GetName(row));
            Assert.Contains(row.GetLogicalDescendants().OfType<ProgressBar>(), b => Math.Abs(b.Value - 200.0 / 3) < .01);
            row.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await ReaderWait(() => pane.Preview.Current == "study.pdf" && ShownPdfPage(pane.Preview) == 2);
            Assert.True(await pane.TryUnpinAsync());

            // reading wrote nothing to the vault
            Assert.Equal(vaultFiles, VaultFiles());

            // another version of the file: the saved page is not applied, and the reader says so
            File.AppendAllText(Path.Combine(vault, "study.pdf"), "\n% changed revision\n");
            await view.FilePreview.OpenAsync("study.pdf");
            await ReaderWait(() => ShownPdfPage(view.FilePreview) == 1);
            Assert.Contains(view.FilePreview.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").Contains("o arquivo mudou desde a última leitura", StringComparison.Ordinal));
            Assert.True(await view.FilePreview.TryCloseAsync());
            return true;
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }, CancellationToken.None));

    /// <summary>CST-241: an EPUB's progress is its chapter out of the spine; a look chosen in a book is where the next new book starts.</summary>
    [Fact]
    public async Task ContinueReading_EpubCountsChaptersAndANewBookStartsWithTheLastBookLook() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            File.Copy(ReaderFixture("reflow.epub"), Path.Combine(vault, "book.epub"));
            File.Copy(ReaderFixture("reflow.epub"), Path.Combine(vault, "outro.epub"));
            await view.OpenVaultAsync();
            await view.FilePreview.OpenAsync("book.epub");
            var book = view.FilePreview.GetLogicalDescendants().OfType<EpubStudyView>().Single();
            Assert.Equal(18, book.FontSizePreference);
            await book.GoToChapterAsync(1);
            await book.SetTypographyAsync(24);
            Assert.True(await view.FilePreview.TryCloseAsync());

            var item = Assert.Single(view.ContinueReading());
            Assert.StartsWith("capítulo 2 de " + book.PartCount + " · ", item.Progress.Position);
            Assert.EndsWith("% dos capítulos", item.Progress.Position);
            Assert.DoesNotContain("página", item.Progress.Position);
            Assert.Equal(24, view.ReadingState.DefaultFor(ReaderKinds.Book)!.FontSize);

            // a book never opened starts with the last book look; the one read before keeps its own place
            await view.FilePreview.OpenAsync("outro.epub");
            var other = view.FilePreview.GetLogicalDescendants().OfType<EpubStudyView>().Single();
            Assert.Equal(24, other.FontSizePreference);
            Assert.Equal(0, other.CurrentChapter);
            Assert.True(await view.FilePreview.TryCloseAsync());
            await view.FilePreview.OpenAsync("book.epub");
            Assert.Equal(1, view.FilePreview.GetLogicalDescendants().OfType<EpubStudyView>().Single().CurrentChapter);
            Assert.True(await view.FilePreview.TryCloseAsync());
            Assert.Equal(["book.epub", "outro.epub"], view.ContinueReading().Select(i => i.Path).Order(StringComparer.Ordinal));
            return true;
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }, CancellationToken.None));
}
