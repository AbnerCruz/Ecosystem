using System.IO.Compression;
using Urbe.Core;
using Urbe.Tests.Fixtures;
using F = Urbe.Tests.Fixtures.PptxFixture;

namespace Urbe.Core.Tests;

/// <summary>CST-237: the PowerPoint deck read for the native reader, against packages built the way PowerPoint writes them.</summary>
public sealed class PptxDeckTests
{
    private static PptxFixture Sample()
    {
        var deck = new PptxFixture();
        // 1: title and body placeholders with no position of their own (the layout's), a bullet list, notes
        deck.Add(F.Placeholder(2, "title", null, F.Paragraph(F.Run("Ciclo da água"))) +
                 F.Placeholder(3, "obj", "1", F.Paragraph(F.Run("Evaporação")), F.Paragraph(F.Run("Condensação"))),
            notes: "Lembrar de perguntar à turma.\nSegunda linha das notas.");
        // 2: a rotated shape with text, a picture, a table, a chart, a transition and animations
        var second = deck.Add(F.Shape(4, F.Xfrm(914400, 914400, 1828800, 914400, rotDegrees: 30), "roundRect", "FF0000", "Girada") +
            F.Picture(5, "rIdImg", F.Xfrm(5486400, 914400, 1828800, 1371600)) +
            F.Table(6, F.Xfrm(914400, 3200400, 3657600, 914400), [["Mês", "Chuva"], ["Março", "120 mm"]], 1828800, 457200) +
            F.Chart(7, F.Xfrm(7315200, 3200400, 2743200, 1828800)));
        second.Rels.Add(("rIdImg", "image", "../media/foto.png"));
        second.After = F.Transition + F.Animation;
        deck.Media["foto.png"] = F.Png(8, 6, 0, 0, 255);
        // 3: a text box with explicit size, colour and alignment
        deck.Add(F.TextBox(8, F.Xfrm(0, 0, 6096000, 1371600), F.Paragraph(F.Run("Atenção à seção", 32, bold: true, color: "123456"), "ctr")));
        return deck;
    }

    [Fact]
    public void ReadsSlidesInPresentationOrderWithSizeTextAndNotes()
    {
        using var deck = PptxDeck.Open(Sample().Build());
        Assert.Equal(3, deck.SlideCount);
        Assert.Equal("16:9", deck.AspectLabel);
        Assert.Equal(960, deck.Width, 3);
        Assert.Equal(540, deck.Height, 3);
        Assert.Empty(deck.DeckNotices);

        var first = deck.Slide(0);
        Assert.Contains("Ciclo da água", first.Text);
        Assert.Contains("Condensação", first.Text);
        // the layout's prompts are never shown, nor its or the master's placeholder text
        Assert.DoesNotContain("Clique para editar", first.Text);
        Assert.DoesNotContain(first.Elements.OfType<PptxShape>(), s => s.Text?.PlainText.Contains("mestre", StringComparison.Ordinal) == true);
        Assert.Equal("Lembrar de perguntar à turma.\nSegunda linha das notas.", first.Notes);
        Assert.Empty(first.NotRendered);
    }

    [Fact]
    public void APlaceholderWithoutPositionTakesTheLayoutsAndTheMastersTextStyle()
    {
        using var deck = PptxDeck.Open(Sample().Build());
        var shapes = deck.Slide(0).Elements.OfType<PptxShape>().ToList();
        // the master's green decoration is drawn under the slide, from the theme's accent1
        var bar = shapes.First();
        Assert.Null(bar.Text);
        Assert.Equal(new PptxColor(0x00, 0xB0, 0x50), bar.Fill);
        Assert.Equal(540 - 27, bar.Y, 1);

        var title = shapes.Single(s => s.Text?.PlainText == "Ciclo da água");
        // the layout's position (w/10, h/20, 8w/10, h/5), not the master's
        Assert.Equal(96, title.X, 1); Assert.Equal(27, title.Y, 1); Assert.Equal(768, title.Width, 1); Assert.Equal(108, title.Height, 1);
        var run = title.Text!.Paragraphs[0].Runs[0];
        Assert.Equal(44, run.Size);
        Assert.True(run.Bold);
        Assert.Equal(new PptxColor(0x1F, 0x38, 0x64), run.Color); // tx2 → dk2 of the theme
        Assert.Equal("DejaVu Sans", run.Font);

        var body = shapes.Single(s => s.Text?.PlainText.Contains("Evaporação", StringComparison.Ordinal) == true);
        Assert.Equal(162, body.Y, 1);
        Assert.All(body.Text!.Paragraphs, p => { Assert.Equal("•", p.Bullet); Assert.Equal(18, p.MarginLeft, 1); Assert.Equal(28, p.Runs[0].Size); });
        Assert.Equal(PptxColor.Black, body.Text.Paragraphs[0].Runs[0].Color);
    }

    [Fact]
    public void ShapesPicturesTablesAndWhatIsNotDrawnAreRead()
    {
        using var deck = PptxDeck.Open(Sample().Build());
        var slide = deck.Slide(1);
        var shape = slide.Elements.OfType<PptxShape>().Single(s => s.Text is not null);
        Assert.Equal("roundRect", shape.Geometry);
        Assert.Equal(30, shape.Rotation, 3);
        Assert.Equal(new PptxColor(255, 0, 0), shape.Fill);
        Assert.Equal(72, shape.X, 1); Assert.Equal(144, shape.Width, 1);
        Assert.Equal(PptxTextAlign.Center, shape.Text!.Paragraphs[0].Align);
        Assert.Equal(PptxAnchor.Middle, shape.Text.Anchor);

        var picture = Assert.Single(slide.Elements.OfType<PptxPicture>());
        Assert.Equal("ppt/media/foto.png", picture.Media);
        Assert.NotNull(deck.Picture(picture.Media));

        var table = Assert.Single(slide.Elements.OfType<PptxTable>());
        Assert.Equal(2, table.Columns.Count);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(4, table.Cells.Count);
        Assert.Equal("Março", table.Cells.Single(c => c.Row == 1 && c.Column == 0).Text.PlainText);
        Assert.Equal(new PptxColor(0xFF, 0xC0, 0x00), table.Cells.Single(c => c.Row == 0 && c.Column == 1).Fill);
        Assert.Contains("120 mm", slide.Text);

        var chart = Assert.Single(slide.Elements.OfType<PptxMissing>());
        Assert.Equal("Gráfico", chart.Label);
        Assert.Contains("gráfico", slide.NotRendered);
        Assert.Contains("transição", slide.NotRendered);
        Assert.Contains("animações (2 efeitos)", slide.NotRendered);
        Assert.Equal("", slide.Notes);

        var box = deck.Slide(2).Elements.OfType<PptxShape>().Single(s => s.Text is not null);
        var run = box.Text!.Paragraphs[0].Runs[0];
        Assert.Equal(32, run.Size);
        Assert.Equal(new PptxColor(0x12, 0x34, 0x56), run.Color);
    }

    [Fact]
    public void FourByThreeAndPortraitDecksKeepTheirProportion()
    {
        var deck = new PptxFixture { Width = 9144000, Height = 6858000 };
        deck.Add(F.TextBox(2, F.Xfrm(0, 0, 9144000, 914400), F.Paragraph(F.Run("Quatro por três"))));
        using var four = PptxDeck.Open(deck.Build());
        Assert.Equal("4:3", four.AspectLabel);
        Assert.Equal(720, four.Width, 3);
        Assert.Equal(540, four.Height, 3);
        Assert.Equal("retrato", PptxDeck.Aspect(6858000, 9144000));
        Assert.Equal("16:10", PptxDeck.Aspect(16, 10));
        // a size PowerPoint never writes is not believed
        var hostile = new PptxFixture { Width = 999_999_999_999, Height = 10 };
        hostile.Add();
        using var odd = PptxDeck.Open(hostile.Build());
        Assert.Equal("16:9", odd.AspectLabel);
    }

    [Fact]
    public void SearchIsAccentAndCaseInsensitiveOverSlidesAndNotes()
    {
        using var deck = PptxDeck.Open(Sample().Build());
        var hits = deck.Search("CONDENSACAO", TestContext.Current.CancellationToken);
        var hit = Assert.Single(hits);
        Assert.Equal(0, hit.Slide);
        Assert.False(hit.InNotes);
        Assert.Contains("Condensação", hit.Excerpt);
        var notes = Assert.Single(deck.Search("perguntar a turma", TestContext.Current.CancellationToken));
        Assert.True(notes.InNotes);
        Assert.Equal(1, Assert.Single(deck.Search("marco", TestContext.Current.CancellationToken)).Slide);
        Assert.Empty(deck.Search("inexistente", TestContext.Current.CancellationToken));
        Assert.Empty(deck.Search("  ", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GroupsMapChildCoordinatesAndFlipsAndSlideNumbersAreRead()
    {
        var deck = new PptxFixture { MasterDecoration = false };
        // a group drawn at twice its child size, holding one rectangle; a slide number field
        deck.Add("<p:grpSp><p:nvGrpSpPr><p:cNvPr id=\"20\" name=\"Grupo\"/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr><p:grpSpPr><a:xfrm><a:off x=\"1270000\" y=\"1270000\"/><a:ext cx=\"2540000\" cy=\"2540000\"/><a:chOff x=\"0\" y=\"0\"/><a:chExt cx=\"1270000\" cy=\"1270000\"/></a:xfrm></p:grpSpPr>" +
                 F.Shape(21, F.Xfrm(635000, 0, 635000, 635000, flipH: true), "rect", "00FF00") + "</p:grpSp>" +
                 F.TextBox(22, F.Xfrm(0, 0, 1270000, 400000), "<a:p><a:fld id=\"{1}\" type=\"slidenum\"><a:t>‹nº›</a:t></a:fld></a:p>"));
        deck.Add();
        using var read = PptxDeck.Open(deck.Build());
        var slide = read.Slide(0);
        var rect = slide.Elements.OfType<PptxShape>().Single(s => s.Fill == new PptxColor(0, 255, 0));
        Assert.Equal(100 + 100, rect.X, 1);
        Assert.Equal(100, rect.Y, 1);
        Assert.Equal(100, rect.Width, 1);
        Assert.True(rect.FlipH);
        Assert.Equal("1", slide.Elements.OfType<PptxShape>().Single(s => s.Text is not null).Text!.PlainText);
        Assert.Empty(read.Slide(1).Elements);
    }

    [Fact]
    public void MacrosEmbeddedFontsHiddenSlidesAndExternalPicturesAreSaid()
    {
        var deck = new PptxFixture { Macros = true, EmbeddedFonts = true };
        var slide = deck.Add(F.Picture(2, "rIdLink", F.Xfrm(0, 0, 914400, 914400)) + F.Picture(3, "rIdEmf", F.Xfrm(914400, 0, 914400, 914400)));
        slide.Attributes = " show=\"0\"";
        slide.Rels.Add(("rIdEmf", "image", "../media/logo.emf"));
        deck.Media["logo.emf"] = [1, 0, 0, 0, 0x6C, 0, 0, 0];
        using var read = PptxDeck.Open(deck.Build());
        Assert.Contains("macros (nunca são executadas)", read.DeckNotices);
        Assert.Contains(read.DeckNotices, n => n.StartsWith("fontes incorporadas", StringComparison.Ordinal));
        var s = read.Slide(0);
        Assert.True(s.Hidden);
        Assert.Contains("imagem EMF", s.NotRendered);
        Assert.Contains("imagem ausente no pacote", s.NotRendered);
        Assert.Equal(2, s.Elements.OfType<PptxMissing>().Count());
    }

    [Fact]
    public void APictureThatIsNotAPictureNeverReachesTheDecoder()
    {
        var deck = new PptxFixture();
        var slide = deck.Add(F.Picture(2, "rIdImg", F.Xfrm(0, 0, 914400, 914400)));
        slide.Rels.Add(("rIdImg", "image", "../media/falsa.png"));
        deck.Media["falsa.png"] = "%PDF-1.7 não é imagem"u8.ToArray();
        using var read = PptxDeck.Open(deck.Build());
        var picture = Assert.Single(read.Slide(0).Elements.OfType<PptxPicture>());
        Assert.Null(read.Picture(picture.Media));
        Assert.Null(read.Picture("ppt/media/nada.png"));
    }

    [Fact]
    public void LegacyBrokenAndMaliciousPackagesAreRefusedWithAReason()
    {
        // the binary .ppt (and an encrypted .pptx, the same OLE container)
        var ole = new byte[512];
        new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(ole, 0);
        var legacy = Assert.Throws<NotSupportedException>(() => PptxDeck.Open(ole));
        Assert.Contains(".ppt", legacy.Message);
        Assert.Contains("outro app", legacy.Message);

        Assert.Contains("PDF", Assert.Throws<InvalidDataException>(() => PptxDeck.Open("%PDF-1.4 renomeado"u8.ToArray())).Message);

        static byte[] Zip(params (string Name, byte[] Bytes)[] entries)
        {
            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                foreach (var (name, bytes) in entries) { using var s = zip.CreateEntry(name).Open(); s.Write(bytes); }
            return output.ToArray();
        }
        // a zip that is not a presentation; one that escapes its folder; one without slides
        Assert.Contains("presentation.xml", Assert.Throws<InvalidDataException>(() => PptxDeck.Open(Zip(("word/document.xml", "<w/>"u8.ToArray())))).Message);
        Assert.Throws<InvalidDataException>(() => PptxDeck.Open(Zip(("ppt/presentation.xml", "<p/>"u8.ToArray()), ("../../fora.xml", "x"u8.ToArray()))));
        Assert.Contains("não tem slides", Assert.Throws<InvalidDataException>(() => PptxDeck.Open(Zip(("ppt/presentation.xml", "<p:presentation xmlns:p=\"p\"/>"u8.ToArray())))).Message);
        // an XML bomb with a DTD is refused by the XML reader
        Assert.ThrowsAny<System.Xml.XmlException>(() => PptxDeck.Open(Zip(("ppt/presentation.xml", "<!DOCTYPE x [<!ENTITY a \"aaaa\">]><x>&a;</x>"u8.ToArray()))));

        // a zip bomb: one member far over the decompression budget
        using var bomb = new MemoryStream();
        using (var zip = new ZipArchive(bomb, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var p = zip.CreateEntry("ppt/presentation.xml").Open()) p.Write("<p/>"u8);
            using var big = zip.CreateEntry("ppt/media/huge.png", CompressionLevel.SmallestSize).Open();
            var zeros = new byte[1024 * 1024];
            for (int i = 0; i < 130; i++) big.Write(zeros);
        }
        Assert.Contains("orçamento", Assert.Throws<InvalidDataException>(() => PptxDeck.Open(bomb.ToArray())).Message);
    }

    [Fact]
    public void ABrokenSlideIsSaidOnItsOwnAndTheRestReads()
    {
        var bytes = Sample().Build();
        using var output = new MemoryStream();
        output.Write(bytes);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, leaveOpen: true))
        {
            zip.GetEntry("ppt/slides/slide3.xml")!.Delete();
            using var s = zip.CreateEntry("ppt/slides/slide3.xml").Open();
            s.Write("<p:sld xmlns:p=\"x\"><não fecha"u8);
        }
        using var deck = PptxDeck.Open(output.ToArray());
        Assert.Equal(3, deck.SlideCount);
        Assert.Contains(deck.Slide(2).NotRendered, n => n.StartsWith("o slide inteiro", StringComparison.Ordinal));
        Assert.Contains("Ciclo da água", deck.Slide(0).Text);
    }

    [Fact]
    public void TheDeckTheUrbeExportsReadsBack()
    {
        var doc = new OfficeDocument("Aula", "pagina", [new OfficeChapter("Aula", "aula.md",
            [new OfficeHeading(1, [new OfficeRun("Fotossíntese")]), new OfficeParagraph([new OfficeRun("A luz vira açúcar.")])])], []);
        using var deck = PptxDeck.Open(PublicationPptx.Write(doc).Bytes);
        Assert.True(deck.SlideCount >= 1);
        Assert.Contains(Enumerable.Range(0, deck.SlideCount), i => deck.Slide(i).Text.Contains("Fotossíntese", StringComparison.Ordinal));
        Assert.Contains(Enumerable.Range(0, deck.SlideCount), i => deck.Slide(i).Text.Contains("A luz vira açúcar.", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSupportMatrixSaysWhatTheSlideReaderDoesAndDoesNot()
    {
        var pptx = FormatSupportMatrix.For("aula.pptx")!;
        Assert.Equal(SupportLevel.Partial, pptx.Open);
        Assert.Contains("Animações", pptx.Note);
        Assert.Contains("notas", pptx.Note);
        var ppt = FormatSupportMatrix.For("antiga.ppt")!;
        Assert.Equal(SupportLevel.External, ppt.Open);
        Assert.Contains(".pptx", ppt.Note);
        Assert.Equal(SupportLevel.Partial, FormatSupportMatrix.For("aula.odp")!.Open);
    }
}
