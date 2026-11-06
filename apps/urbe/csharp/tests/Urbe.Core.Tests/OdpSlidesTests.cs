using System.IO.Compression;
using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-237: OpenDocument presentations read into the native slide model.</summary>
public sealed class OdpSlidesTests
{
    private const string Ns = "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
        "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
        "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" " +
        "xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:anim=\"urn:oasis:names:tc:opendocument:xmlns:animation:1.0\" xmlns:smil=\"urn:oasis:names:tc:opendocument:xmlns:smil-compatible:1.0\"";

    private const string Styles = "<office:document-styles " + Ns + "><office:styles>" +
        "<style:default-style style:family=\"graphic\"><style:graphic-properties draw:fill=\"solid\" draw:fill-color=\"#729fcf\" draw:stroke=\"solid\" svg:stroke-color=\"#3465a4\"/></style:default-style>" +
        "<style:style style:name=\"Titulo\" style:family=\"presentation\"><style:graphic-properties draw:fill=\"none\" draw:stroke=\"none\"/><style:paragraph-properties fo:text-align=\"center\"/><style:text-properties fo:font-size=\"40pt\" fo:font-weight=\"bold\" fo:color=\"#1f3864\"/></style:style>" +
        "</office:styles><office:automatic-styles><style:page-layout style:name=\"PM1\"><style:page-layout-properties fo:page-width=\"25.4cm\" fo:page-height=\"19.05cm\"/></style:page-layout>" +
        "<style:style style:name=\"Mfaixa\" style:family=\"graphic\"><style:graphic-properties draw:fill=\"solid\" draw:fill-color=\"#c00000\" draw:stroke=\"none\"/></style:style>" +
        "<style:style style:name=\"Mfundo\" style:family=\"drawing-page\"><style:drawing-page-properties draw:fill=\"solid\" draw:fill-color=\"#fff2cc\"/></style:style></office:automatic-styles>" +
        "<office:master-styles><style:master-page style:name=\"Aula\" style:page-layout-name=\"PM1\" draw:style-name=\"Mfundo\">" +
        "<draw:rect draw:style-name=\"Mfaixa\" svg:x=\"0cm\" svg:y=\"18cm\" svg:width=\"25.4cm\" svg:height=\"1.05cm\"/>" +
        "<draw:frame presentation:class=\"title\" presentation:placeholder=\"true\" svg:x=\"1cm\" svg:y=\"1cm\" svg:width=\"23.4cm\" svg:height=\"3cm\"><draw:text-box/></draw:frame>" +
        "</style:master-page></office:master-styles></office:document-styles>";

    private static string Content(string pages, string automatic = "") => "<office:document-content " + Ns + "><office:automatic-styles>" +
        "<style:style style:name=\"elipse\" style:family=\"graphic\"><style:graphic-properties draw:fill=\"solid\" draw:fill-color=\"#00b050\" draw:stroke=\"none\"/></style:style>" +
        "<style:style style:name=\"T1\" style:family=\"text\"><style:text-properties fo:font-style=\"italic\" style:text-underline-style=\"solid\" fo:color=\"#ff0000\"/></style:style>" +
        "<style:style style:name=\"oculta\" style:family=\"drawing-page\"><style:drawing-page-properties presentation:visibility=\"hidden\" presentation:transition-type=\"automatic\"/></style:style>" +
        "<style:style style:name=\"ce1\" style:family=\"table-cell\"><style:graphic-properties draw:fill=\"solid\" draw:fill-color=\"#4f81bd\"/></style:style>" +
        "<text:list-style style:name=\"L1\"><text:list-level-style-number text:level=\"1\"/></text:list-style>" + automatic +
        "</office:automatic-styles><office:body><office:presentation>" + pages + "</office:presentation></office:body></office:document-content>";

    private static byte[] Package(string content, string? styles = Styles, Action<ZipArchive>? more = null, string? manifest = null)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            void Add(string name, string text) { using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); w.Write(text); }
            Add("mimetype", "application/vnd.oasis.opendocument.presentation");
            Add("content.xml", content);
            if (styles is not null) Add("styles.xml", styles);
            if (manifest is not null) Add("META-INF/manifest.xml", manifest);
            more?.Invoke(zip);
        }
        return stream.ToArray();
    }

    private static readonly string Lesson = Content(
        "<draw:page draw:name=\"p1\" draw:master-page-name=\"Aula\">" +
        "<draw:frame presentation:class=\"title\" presentation:style-name=\"Titulo\"><draw:text-box><text:p>Ciclo da água</text:p></draw:text-box></draw:frame>" +
        "<draw:frame svg:x=\"2cm\" svg:y=\"5cm\" svg:width=\"10cm\" svg:height=\"6cm\"><draw:text-box><text:list text:style-name=\"L1\"><text:list-item><text:p>Evaporação</text:p></text:list-item>" +
        "<text:list-item><text:p>Condensação <text:span text:style-name=\"T1\">rápida</text:span></text:p></text:list-item></text:list></draw:text-box></draw:frame>" +
        "<draw:ellipse draw:style-name=\"elipse\" svg:x=\"14cm\" svg:y=\"5cm\" svg:width=\"4cm\" svg:height=\"4cm\"/>" +
        "<draw:custom-shape svg:width=\"4cm\" svg:height=\"2cm\" draw:transform=\"rotate (1.5707963267949) translate (14cm 14cm)\"><text:p>Girada</text:p><draw:enhanced-geometry draw:type=\"ooxml-roundRect\"/></draw:custom-shape>" +
        "<draw:line svg:x1=\"1cm\" svg:y1=\"12cm\" svg:x2=\"10cm\" svg:y2=\"12cm\"/>" +
        "<presentation:notes><draw:frame presentation:class=\"notes\"><draw:text-box><text:p>Perguntar sobre a chuva.</text:p></draw:text-box></draw:frame></presentation:notes></draw:page>" +
        "<draw:page draw:name=\"p2\" draw:master-page-name=\"Aula\" draw:style-name=\"oculta\">" +
        "<draw:frame svg:x=\"1cm\" svg:y=\"1cm\" svg:width=\"8cm\" svg:height=\"6cm\"><draw:image xlink:href=\"Pictures/foto.png\"/></draw:frame>" +
        "<draw:frame svg:x=\"10cm\" svg:y=\"1cm\" svg:width=\"8cm\" svg:height=\"4cm\"><table:table><table:table-column table:number-columns-repeated=\"2\"/>" +
        "<table:table-row table:default-cell-style-name=\"ce1\"><table:table-cell table:number-columns-spanned=\"2\"><text:p>Mês e chuva</text:p></table:table-cell><table:covered-table-cell/></table:table-row>" +
        "<table:table-row><table:table-cell><text:p>Março</text:p></table:table-cell><table:table-cell><text:p>120 mm</text:p></table:table-cell></table:table-row></table:table></draw:frame>" +
        "<draw:frame svg:x=\"1cm\" svg:y=\"9cm\" svg:width=\"8cm\" svg:height=\"6cm\"><draw:object xlink:href=\"./Object 1\"/></draw:frame>" +
        "<draw:frame svg:x=\"10cm\" svg:y=\"9cm\" svg:width=\"8cm\" svg:height=\"6cm\"><draw:object xlink:href=\"./Object 2\"/><draw:image xlink:href=\"Pictures/foto.png\"/></draw:frame>" +
        "<draw:frame svg:x=\"1cm\" svg:y=\"16cm\" svg:width=\"4cm\" svg:height=\"1cm\"><draw:image xlink:href=\"Pictures/falsa.png\"/></draw:frame>" +
        "<anim:par presentation:node-type=\"timing-root\"><anim:seq/></anim:par></draw:page>");

    private static void LessonMedia(ZipArchive zip)
    {
        using (var s = zip.CreateEntry("Pictures/foto.png").Open()) s.Write(Png());
        using (var s = zip.CreateEntry("Pictures/falsa.png").Open()) s.Write("%PDF-1.4 não é imagem"u8);
        using (var w = new StreamWriter(zip.CreateEntry("Object 1/content.xml").Open())) w.Write("<office:document-content " + Ns + "><office:body><office:chart><chart:chart xmlns:chart=\"urn:oasis:names:tc:opendocument:xmlns:chart:1.0\"/></office:chart></office:body></office:document-content>");
        using (var w = new StreamWriter(zip.CreateEntry("Object 2/content.xml").Open())) w.Write("<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mi>x</mi></math>");
    }

    private static byte[] Png() => Urbe.Tests.Fixtures.PptxFixture.Png(4, 3, 0, 0, 255);

    [Fact]
    public void Deck_ReadsSizeMasterPlaceholdersTextShapesAndNotes()
    {
        using var deck = PptxDeck.OpenOdp(Package(Lesson, more: LessonMedia));
        Assert.Equal(2, deck.SlideCount);
        Assert.Equal("4:3", deck.AspectLabel);
        Assert.Equal(25.4 * 72 / 2.54, deck.Width, 3);
        var first = deck.Slide(0);
        Assert.Equal(new PptxColor(0xFF, 0xF2, 0xCC), first.Background);
        // the master's band comes first, under everything
        var band = Assert.IsType<PptxShape>(first.Elements[0]);
        Assert.Equal(new PptxColor(0xC0, 0, 0), band.Fill);
        // the title takes the master placeholder's place and its presentation style
        var title = first.Elements.OfType<PptxShape>().Single(s => s.Text?.PlainText == "Ciclo da água");
        Assert.Equal((1 * 72 / 2.54, 23.4 * 72 / 2.54), (title.X, title.Width), new ToleranceComparer());
        var run = title.Text!.Paragraphs[0].Runs[0];
        Assert.Equal((40.0, true, new PptxColor(0x1F, 0x38, 0x64)), (run.Size, run.Bold, run.Color));
        Assert.Equal(PptxTextAlign.Center, title.Text.Paragraphs[0].Align);
        Assert.Null(title.Fill);
        var list = first.Elements.OfType<PptxShape>().Single(s => s.Text?.PlainText.StartsWith("Evaporação", StringComparison.Ordinal) == true).Text!;
        Assert.Equal(["1.", "2."], list.Paragraphs.Select(p => p.Bullet));
        var styled = list.Paragraphs[1].Runs.Single(r => r.Text == "rápida");
        Assert.True(styled.Italic && styled.Underline);
        Assert.Equal(new PptxColor(255, 0, 0), styled.Color);
        var ellipse = first.Elements.OfType<PptxShape>().Single(s => s.Geometry == "ellipse");
        Assert.Equal(new PptxColor(0, 0xB0, 0x50), ellipse.Fill);
        var turned = first.Elements.OfType<PptxShape>().Single(s => s.Text?.PlainText == "Girada");
        Assert.Equal("roundRect", turned.Geometry);
        Assert.Equal(270, turned.Rotation, 3);
        Assert.Equal(new PptxColor(0x72, 0x9F, 0xCF), turned.Fill); // the default graphic style of the file
        Assert.Contains(first.Elements.OfType<PptxShape>(), s => s.Geometry == "line");
        Assert.Equal("Perguntar sobre a chuva.", first.Notes);
        Assert.Contains("Condensação rápida", first.Text);
        Assert.False(first.Hidden);
        Assert.Empty(first.NotRendered);
    }

    [Fact]
    public void Deck_PicturesTablesAndWhatIsNotDrawnAreSaid()
    {
        using var deck = PptxDeck.OpenOdp(Package(Lesson, more: LessonMedia));
        var second = deck.Slide(1);
        Assert.True(second.Hidden);
        var picture = second.Elements.OfType<PptxPicture>().First();
        Assert.NotNull(deck.Picture(picture.Media));
        var table = Assert.Single(second.Elements.OfType<PptxTable>());
        Assert.Equal(2, table.Columns.Count);
        var header = table.Cells.Single(c => c.Row == 0);
        Assert.Equal((2, new PptxColor(0x4F, 0x81, 0xBD)), (header.ColumnSpan, header.Fill!.Value));
        Assert.Contains(table.Cells, c => c is { Row: 1, Column: 1 } && c.Text.PlainText == "120 mm");
        Assert.Contains(second.Elements.OfType<PptxMissing>(), m => m.Label.StartsWith("Gráfico", StringComparison.Ordinal));
        // an equation with a fallback picture shows the picture
        Assert.Equal(2, second.Elements.OfType<PptxPicture>().Count());
        Assert.Contains(second.Elements.OfType<PptxMissing>(), m => m.Label.StartsWith("Imagem", StringComparison.Ordinal));
        Assert.Contains("gráficos", second.NotRendered);
        Assert.Contains("equações", second.NotRendered);
        Assert.Contains("animações", second.NotRendered);
        Assert.Contains("transição de slide", second.NotRendered);
        Assert.Contains("imagens em formato não suportado ou vinculadas", second.NotRendered);
        var hit = Assert.Single(deck.Search("marco", TestContext.Current.CancellationToken));
        Assert.Equal((1, false), (hit.Slide, hit.InNotes));
        Assert.Contains("Março", hit.Excerpt);
    }

    [Fact]
    public void Open_RefusesWhatIsNotAReadablePresentation()
    {
        Assert.Throws<InvalidDataException>(() => PptxDeck.OpenOdp("%PDF-1.7"u8.ToArray()));
        Assert.Throws<InvalidDataException>(() => PptxDeck.OpenOdp(Package(Content(""), null)));
        Assert.Throws<NotSupportedException>(() => PptxDeck.OpenOdp(Package(Lesson, manifest: "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\"><manifest:file-entry><manifest:encryption-data/></manifest:file-entry></manifest:manifest>")));
        Assert.ThrowsAny<Exception>(() => PptxDeck.OpenOdp(Package("<!DOCTYPE x [<!ENTITY e SYSTEM \"file:///etc/passwd\">]><x>&e;</x>")));
        Assert.Throws<InvalidDataException>(() => PptxDeck.OpenOdp(Package("<office:document-content " + Ns + "><office:body><office:text/></office:body></office:document-content>")));
    }

    [Fact]
    public void Lengths_AreReadInEveryOdfUnit()
    {
        Assert.Equal(72, OdpSlides.Length("2.54cm"), 6);
        Assert.Equal(72, OdpSlides.Length("25.4mm"), 6);
        Assert.Equal(72, OdpSlides.Length("1in"), 6);
        Assert.Equal(12, OdpSlides.Length("12pt"), 6);
        Assert.Equal(0, OdpSlides.Length("abc"));
        Assert.Equal(0, OdpSlides.Length("NaNcm"));
    }

    private sealed class ToleranceComparer : IEqualityComparer<(double, double)>
    {
        public bool Equals((double, double) a, (double, double) b) => Math.Abs(a.Item1 - b.Item1) < .01 && Math.Abs(a.Item2 - b.Item2) < .01;
        public int GetHashCode((double, double) obj) => 0;
    }
}
