using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-238: the plain text family (TXT/LOG, RTF, CSV, JSON), its search and the honest support matrix.</summary>
public sealed class PlainTextFormatsTests
{
    [Fact]
    public void DecodesUtf8Utf16AndWindows1252AndRefusesBinaries()
    {
        Assert.Equal(new DecodedText("ação", null), TextDecoding.Decode([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("ação")]));
        Assert.Equal("ação", TextDecoding.Decode([0xFF, 0xFE, .. Encoding.Unicode.GetBytes("ação")])!.Text);
        Assert.Equal("ação", TextDecoding.Decode([0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes("ação")])!.Text);
        var latin = TextDecoding.Decode([(byte)'a', 0xE7, 0xE3, (byte)'o', 0x20, 0x93, (byte)'x', 0x94])!;
        Assert.Equal("ação “x”", latin.Text);
        Assert.Contains("Windows-1252", latin.Notice, StringComparison.Ordinal);
        Assert.Null(TextDecoding.Decode([(byte)'a', 0, (byte)'b']));
        Assert.Null(TextDecoding.Decode([1, 2, 3, 4, 5, 6, 7, 8, 0x80, 0x81]));
        Assert.Equal("", TextDecoding.Decode([])!.Text);
    }

    [Fact]
    public void RtfBecomesItsTextWithoutHeadersOrPictures()
    {
        const string rtf = @"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0 Times;}}{\colortbl;\red255\green0\blue0;}{\*\generator Teste;}" +
                           @"{\info{\title Oculto}}\pard\b T\'edtulo\b0\par Ol\u225?\u225 ?mundo\tab fim\line linha {\pict\pngblip 89504e47}{\*\unknown x}" +
                           @"{\field{\*\fldinst HYPERLINK ""x""}{\fldrslt link}}\{chaves\}\\\emdash\par}";
        var text = RtfText.Convert(Encoding.ASCII.GetBytes(rtf));
        Assert.Equal("Título\nOláámundo\tfim\nlinha link{chaves}\\—\n", text);
        Assert.DoesNotContain("Times", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Oculto", text, StringComparison.Ordinal);
        Assert.DoesNotContain("89504e47", text, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => RtfText.Convert("não é rtf"u8));
    }

    [Fact]
    public void RtfSurvivesDeepNestingAndTruncation()
    {
        var deep = "{\\rtf1 " + new string('{', 200_000) + "fundo" + new string('}', 200_000) + "}";
        Assert.Equal("fundo", RtfText.Convert(Encoding.ASCII.GetBytes(deep)));
        Assert.Equal("abc", RtfText.Convert("{\\rtf1 abc\\'"u8));
        Assert.Equal("", RtfText.Convert("{\\rtf1 \\bin999999999 x"u8));
    }

    [Fact]
    public void CsvIndexesRowsAndParsesQuotedCellsOnDemand()
    {
        var table = new CsvTable("nome;cidade;nota\r\n\"Ana; Maria\";\"São\nPaulo\";10\r\n\r\nBia;\"Rio \"\"de\"\" Janeiro\";9\n");
        Assert.Equal(';', table.Delimiter);
        Assert.Equal(3, table.RowCount);
        Assert.Equal(["nome", "cidade", "nota"], table.Row(0));
        Assert.Equal(["Ana; Maria", "São\nPaulo", "10"], table.Row(1));
        Assert.Equal(["Bia", "Rio \"de\" Janeiro", "9"], table.Row(2));
        Assert.Equal(1, table.RowAt(table.RowStart(1) + 5));
        Assert.Equal(2, table.RowAt(table.RowStart(2)));
        Assert.Equal(',', new CsvTable("a,b\n1,2").Delimiter);
        Assert.Equal('\t', new CsvTable("a\tb,c\n").Delimiter);
        Assert.Equal(["x"], new CsvTable("x").Row(0));
    }

    [Fact]
    public void JsonIsFormattedAndAnInvalidOneIsShownWithItsError()
    {
        var pretty = JsonPretty.Format("{\"nome\":\"ação\",\"itens\":[1,2,],/* nota */\"ok\":true}");
        Assert.Equal("{\n  \"nome\": \"ação\",\n  \"itens\": [\n    1,\n    2\n  ],\n  \"ok\": true\n}", pretty.Text.ReplaceLineEndings("\n"));
        Assert.Contains("comentários", pretty.Notice, StringComparison.Ordinal);
        var invalid = JsonPretty.Format("{\n\"a\": }");
        Assert.Equal("{\n\"a\": }", invalid.Text);
        Assert.Contains("JSON inválido (linha 2)", invalid.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchIgnoresCaseAndAccentsAndKeepsOffsets()
    {
        const string text = "Ação e ACAO e acão; nada mais";
        var folded = TextSearch.Fold(text);
        Assert.Equal(text.Length, folded.Length);
        Assert.Equal([0, 7, 14], TextSearch.FindAll(folded, "AÇÃO", TestContext.Current.CancellationToken));
        Assert.Empty(TextSearch.FindAll(folded, "", TestContext.Current.CancellationToken));
        Assert.Empty(TextSearch.FindAll(folded, "ausente", TestContext.Current.CancellationToken));
        var many = TextSearch.Fold(new string('a', TextSearch.MaxMatches + 50));
        Assert.Equal(TextSearch.MaxMatches, TextSearch.FindAll(many, "a", TestContext.Current.CancellationToken).Count);
    }

    [Theory]
    [InlineData("leia.txt", "Texto simples", SupportLevel.Complete)]
    [InlineData("registro.LOG", "Texto simples", SupportLevel.Complete)]
    [InlineData("carta.rtf", "RTF", SupportLevel.Partial)]
    [InlineData("dados.csv", "Tabela CSV", SupportLevel.Complete)]
    [InlineData("revista.cbz", "Quadrinhos CBZ", SupportLevel.Complete)]
    [InlineData("revista.cbr", "Quadrinhos CBR/CB7", SupportLevel.External)]
    [InlineData("antigo.doc", "Documento legado do Office", SupportLevel.External)]
    public void TheSupportMatrixIsHonest(string file, string name, SupportLevel open)
    {
        var row = FormatSupportMatrix.For(file)!;
        Assert.Equal(name, row.Name);
        Assert.Equal(open, row.Open);
        Assert.False(string.IsNullOrWhiteSpace(row.Note));
        Assert.Null(FormatSupportMatrix.For("dados.xyz"));
        Assert.Equal("fallback externo", FormatSupportMatrix.Label(SupportLevel.External));
        Assert.Equal("parcial", FormatSupportMatrix.Label(SupportLevel.Partial));
        Assert.Equal("completo", FormatSupportMatrix.Label(SupportLevel.Complete));
    }

    [Fact]
    public void EveryExtensionHasOneRowAndUnsupportedFormatsPromiseNothing()
    {
        var extensions = FormatSupportMatrix.All.SelectMany(f => f.Extensions).ToArray();
        Assert.Equal(extensions.Length, extensions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(FormatSupportMatrix.All.Where(f => f.Open == SupportLevel.External), f =>
        {
            Assert.Equal(SupportLevel.External, f.Search);
            Assert.Equal(SupportLevel.External, f.Selection);
        });
    }

    [Theory]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A }, FileSignature.Png)]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, FileSignature.Jpeg)]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }, FileSignature.Pdf)]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, FileSignature.Zip)]
    [InlineData(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x00 }, FileSignature.Rar)]
    [InlineData(new byte[] { 0x7B, 0x5C, 0x72, 0x74, 0x66, 0x31 }, FileSignature.Rtf)]
    [InlineData(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, FileSignature.Executable)]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x61, 0x00 }, FileSignature.Text)]
    [InlineData(new byte[] { 0x6F, 0x6C, 0xC3, 0xA1, 0x0A }, FileSignature.Text)]
    [InlineData(new byte[] { 0x01, 0x02, 0x00, 0x03 }, FileSignature.Unknown)]
    public void SniffsWhatTheBytesAre(byte[] bytes, FileSignature signature) => Assert.Equal(signature, FileSignatures.Detect(bytes));
}
