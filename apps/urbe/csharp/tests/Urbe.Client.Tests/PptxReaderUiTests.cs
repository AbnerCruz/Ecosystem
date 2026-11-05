using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using SkiaSharp;
using Urbe.Client;
using Urbe.Core;
using Urbe.Tests.Fixtures;
using F = Urbe.Tests.Fixtures.PptxFixture;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-237: PowerPoint decks in the native slide reader: drawn in their proportion, thumbnails, navigation, search,
/// speaker notes, presenting, the position kept privately, and what is not drawn said on each slide.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static PptxFixture LessonDeck(int extraSlides = 0)
    {
        var deck = new PptxFixture();
        deck.Add(F.Placeholder(2, "title", null, F.Paragraph(F.Run("Ciclo da água"))) +
                 F.Placeholder(3, "obj", "1", F.Paragraph(F.Run("Evaporação")), F.Paragraph(F.Run("Condensação"))),
            notes: "Perguntar à turma sobre a chuva.");
        var second = deck.Add(F.Shape(4, F.Xfrm(914400, 914400, 1828800, 914400, rotDegrees: 30), "roundRect", "FF0000", "Girada") +
            F.Picture(5, "rIdImg", F.Xfrm(5486400, 914400, 1828800, 1371600)) +
            F.Table(6, F.Xfrm(914400, 3200400, 3657600, 914400), [["Mês", "Chuva"], ["Março", "120 mm"]], 1828800, 457200) +
            F.Chart(7, F.Xfrm(7315200, 3200400, 2743200, 1828800)));
        second.Rels.Add(("rIdImg", "image", "../media/foto.png"));
        second.After = F.Transition + F.Animation;
        deck.Media["foto.png"] = F.Png(16, 12, 0, 0, 255);
        deck.Add(F.TextBox(8, F.Xfrm(0, 0, 6096000, 1371600), F.Paragraph(F.Run("Atenção à seção", 32, bold: true, color: "123456"), "ctr")), notes: "Fechar com a revisão.");
        deck.Add(F.Shape(9, F.Xfrm(4572000, 2971800, 3048000, 914400, rotDegrees: 90), "rect", "0000FF"));
        for (int i = 0; i < extraSlides; i++) deck.Add(F.TextBox(10, F.Xfrm(914400, 914400, 6096000, 914400), F.Paragraph(F.Run("Slide extra " + (i + 5)))));
        return deck;
    }

    private static SKColor PixelAt(SKBitmap bitmap, double x, double y) => bitmap.GetPixel((int)x, (int)y);
    private static bool Near(SKColor c, byte r, byte g, byte b) => Math.Abs(c.Red - r) < 40 && Math.Abs(c.Green - g) < 40 && Math.Abs(c.Blue - b) < 40;

    [Fact]
    public void PptxRenderer_DrawsInTheDecksProportionWithRotationMasterDecorationPicturesAndTables()
    {
        using var deck = PptxDeck.Open(LessonDeck().Build());
        if (Environment.GetEnvironmentVariable("URBE_PPTX_RENDER_DIR") is { Length: > 0 } output)
        {
            Directory.CreateDirectory(output);
            File.WriteAllBytes(Path.Combine(output, "lesson.pptx"), LessonDeck().Build());
            for (int i = 0; i < deck.SlideCount; i++)
            {
                using var bitmap = PptxSlideRenderer.Render(deck, i, 1280);
                using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(output, "urbe-slide" + (i + 1) + ".png"), data.ToArray());
            }
        }
        using var second = PptxSlideRenderer.Render(deck, 1, 960);
        Assert.Equal((960, 540), (second.Width, second.Height));
        // 1 pt = 1 px at 960: the master's green bar at the bottom, the red rotated shape at its centre, the blue picture
        Assert.True(Near(PixelAt(second, 480, 535), 0x00, 0xB0, 0x50));
        Assert.True(Near(PixelAt(second, 72 + 72 + 43, 72 + 36 + 25), 255, 0, 0));
        Assert.True(Near(PixelAt(second, 432 + 72, 72 + 54), 0, 0, 255));
        // the table's header fill, and the chart marked in place (grey), not left white
        Assert.True(Near(PixelAt(second, 72 + 10, 252 + 5), 0xFF, 0xC0, 0x00));
        Assert.NotEqual(SKColors.White, PixelAt(second, 576 + 30, 252 + 30));

        // a 90° turn: the 240×72 rectangle stands up (72 wide, 240 tall) around its centre (480, 270)
        using var fourth = PptxSlideRenderer.Render(deck, 3, 960);
        Assert.True(Near(PixelAt(fourth, 480, 270 + 100), 0, 0, 255));
        Assert.False(Near(PixelAt(fourth, 480 + 100, 270), 0, 0, 255));

        // text is drawn (dark pixels inside the title's box)
        using var first = PptxSlideRenderer.Render(deck, 0, 960);
        bool dark = false;
        for (int x = 96; x < 600 && !dark; x += 2) for (int y = 27; y < 135 && !dark; y += 2) dark = PixelAt(first, x, y).Red < 120;
        Assert.True(dark);

        // 4:3 keeps 4:3
        var square = new PptxFixture { Width = 9144000, Height = 6858000 };
        square.Add(F.Shape(2, F.Xfrm(0, 0, 9144000, 6858000), "ellipse", "00FF00"));
        using var four = PptxDeck.Open(square.Build());
        using var drawn = PptxSlideRenderer.Render(four, 0, 800);
        Assert.Equal((800, 600), (drawn.Width, drawn.Height));
        Assert.True(Near(PixelAt(drawn, 400, 300), 0, 255, 0));
        Assert.False(Near(PixelAt(drawn, 3, 3), 0, 255, 0));

    }

    [Fact]
    public async Task Pptx_OpensNavigatesSearchesShowsNotesPresentsAndResumesTheSlide() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                File.WriteAllBytes(Path.Combine(vault, "aula.pptx"), LessonDeck().Build());
                await view.OpenVaultAsync();
                var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + ".urbe" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    .Order().Select(f => (f, File.GetLastWriteTimeUtc(f))).ToArray();
                var preview = view.FilePreview;
                await preview.OpenAsync("aula.pptx");
                Assert.Equal("slides", preview.Shown);
                var slides = preview.GetLogicalDescendants().OfType<PptxReaderView>().Single();
                Assert.Equal(4, slides.SlideCount);
                Assert.Equal("Slide 1 de 4", slides.SlideLabel);
                Assert.Contains(ReaderTexts(preview), t => t.StartsWith("Apresentação · ", StringComparison.Ordinal) && t.Contains("4 slides · 16:9", StringComparison.Ordinal));
                await WaitFor(() => slides.ShownPixels is not null);
                var pixels = slides.ShownPixels!.Value;
                Assert.Equal(16.0 / 9, pixels.Width / (double)pixels.Height, 2);
                // the first slide's notes are shown, with where they come from
                Assert.True(slides.NotesVisible);
                Assert.Equal("Perguntar à turma sobre a chuva.", slides.CurrentNotes);
                Assert.Contains(slides.GetLogicalDescendants().OfType<SelectableTextBlock>(), t => t.Text == "Perguntar à turma sobre a chuva.");
                Assert.Contains(ReaderTexts(slides), t => t.Contains("aula.pptx · slide 1", StringComparison.Ordinal));
                Assert.Null(slides.Notice);
                Dispatcher.UIThread.RunJobs();
                Snapshot(window, "native-pptx-reader.png");

                // a tap on the right edge: slide 2 says what it has that is not drawn, and offers another app
                slides.Tap(.9);
                await WaitFor(() => slides.CurrentSlide == 1 && slides.Notice is not null);
                Assert.Contains("gráfico", slides.Notice);
                Assert.Contains("transição", slides.Notice);
                Assert.Contains("animações", slides.Notice);
                Assert.Contains(slides.GetLogicalDescendants().OfType<Button>(), b => b.Content as string == "Abrir em outro app" && b.IsVisible);
                Assert.Equal("Este slide não tem notas do apresentador.", slides.GetLogicalDescendants().OfType<SelectableTextBlock>().Single().Text);
                Assert.All(slides.RenderedSlides, s => Assert.InRange(s, 0, 2));

                // keys: End, Left; the notes can be hidden
                slides.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End });
                await WaitFor(() => slides.CurrentSlide == 3);
                Assert.Equal("Slide 4 de 4", slides.SlideLabel);
                Assert.All(slides.RenderedSlides, s => Assert.InRange(s, 2, 3));
                slides.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Left });
                await WaitFor(() => slides.CurrentSlide == 2);
                ClickButtonWithText(slides, "Notas do apresentador");
                Assert.False(slides.NotesVisible);

                // search across slides and notes, without accents or case; a match in the notes shows them
                await slides.SearchOrNextAsync("CONDENSACAO");
                Assert.Equal(0, slides.CurrentSlide);
                Assert.Contains(ReaderTexts(slides), t => t == "1 resultado em 1 slide");
                await slides.SearchOrNextAsync("revisao");
                Assert.Equal(2, slides.CurrentSlide);
                Assert.True(slides.SearchHits.Single().InNotes);
                Assert.True(slides.NotesVisible);
                await slides.SearchOrNextAsync("marco");
                Assert.Equal(1, slides.CurrentSlide);

                // zoom
                slides.SetZoom(2);
                Assert.Equal(2, slides.Zoom);
                slides.SetZoom(1);

                // presenting: full screen, no bars; Back leaves the presentation before anything else
                slides.SetPresenting(true);
                Assert.True(slides.IsPresenting);
                Assert.True(preview.ReaderMode);
                Assert.True(preview.ChromeHidden);
                Assert.False(slides.Bar.IsVisible);
                Assert.False(slides.NotesVisible);
                slides.Tap(.5);
                await WaitFor(() => slides.CurrentSlide == 2);
                Assert.True(view.HandleSystemBack());
                Assert.False(slides.IsPresenting);
                Assert.False(preview.ChromeHidden);
                Assert.False(preview.ReaderMode);
                Assert.True(preview.IsVisible);

                // the slide is remembered privately, counted out of the slides
                Assert.True(await preview.TryCloseAsync());
                var item = Assert.Single(view.ContinueReading());
                Assert.Equal("aula.pptx", item.Path);
                Assert.Equal("slide 3 de 4 · 75%", item.Progress.Position);
                await preview.OpenAsync("aula.pptx");
                var again = preview.GetLogicalDescendants().OfType<PptxReaderView>().Single();
                Assert.NotSame(slides, again);
                Assert.Equal(2, again.CurrentSlide);
                preview.Close();
                Assert.Equal(before, Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + ".urbe" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    .Order().Select(f => (f, File.GetLastWriteTimeUtc(f))).ToArray());
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Pptx_ThumbnailsAreVirtualizedAndJumpToTheirSlide() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                File.WriteAllBytes(Path.Combine(vault, "longa.pptx"), LessonDeck(extraSlides: 116).Build());
                await view.OpenVaultAsync();
                var preview = view.FilePreview;
                await preview.OpenAsync("longa.pptx");
                var slides = preview.GetLogicalDescendants().OfType<PptxReaderView>().Single();
                Assert.Equal(120, slides.SlideCount);
                await Settle(window, view);
                Assert.True(slides.ThumbnailsVisible);
                await WaitFor(() => slides.RenderedThumbnails.Count > 0);
                // only the thumbnails the list showed are drawn, never the 120
                Assert.InRange(slides.RenderedThumbnails.Count, 1, PptxReaderView.ThumbnailCache);
                Assert.Contains(0, slides.RenderedThumbnails);
                Assert.DoesNotContain(119, slides.RenderedThumbnails);
                var list = slides.GetLogicalDescendants().OfType<ListBox>().First(l => l.ItemCount == 120);
                list.SelectedIndex = 3;
                await WaitFor(() => slides.CurrentSlide == 3);
                await slides.GoToSlideAsync(100);
                Assert.Equal("Slide 101 de 120", slides.SlideLabel);
                Assert.All(slides.RenderedSlides, s => Assert.InRange(s, 99, 101));
                preview.Close();
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Pptx_LegacyEncryptedAndBrokenDecksSayWhyAndOfferAnotherApp() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var ole = new byte[1024];
                new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(ole, 0);
                File.WriteAllBytes(Path.Combine(vault, "antiga.ppt"), ole);
                File.WriteAllBytes(Path.Combine(vault, "protegida.pptx"), ole);
                WriteZip(Path.Combine(vault, "quebrada.pptx"), ("word/document.xml", "<w/>"u8.ToArray()));
                await view.OpenVaultAsync();
                var preview = view.FilePreview;
                bool OpenOffered() => preview.GetLogicalDescendants().OfType<Button>().Any(b => b.Content as string == "Abrir em outro app" && b.IsVisible);

                await preview.OpenAsync("antiga.ppt");
                Assert.Equal("info", preview.Shown);
                Assert.Contains(ReaderTexts(preview), t => t.Contains("PowerPoint 97–2003", StringComparison.Ordinal));
                Assert.Contains(ReaderTexts(preview), t => t.Contains("salve como .pptx", StringComparison.Ordinal));
                Assert.True(OpenOffered());

                await preview.OpenAsync("protegida.pptx");
                Assert.Equal("info", preview.Shown);
                Assert.Contains(ReaderTexts(preview), t => t.Contains("protegida por senha", StringComparison.Ordinal));
                Assert.True(OpenOffered());

                await preview.OpenAsync("quebrada.pptx");
                Assert.Equal("info", preview.Shown);
                Assert.Contains(ReaderTexts(preview), t => t.Contains("Não foi possível abrir o arquivo de leitura", StringComparison.Ordinal));
                Assert.True(OpenOffered());
                preview.Close();
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
