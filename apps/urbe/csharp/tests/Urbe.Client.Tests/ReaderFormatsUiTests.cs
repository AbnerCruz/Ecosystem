using System.IO.Compression;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-238: comics (CBZ) in a native reader that decodes only around the page shown, the plain text family (TXT/LOG,
/// RTF, CSV, JSON) with search, and an honest info panel, never an empty screen, for what the Urbe does not open.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static byte[] PagePng(int width, int height, byte shade)
    {
        var rgba = new byte[width * height * 4];
        for (int i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = (shade, (byte)(255 - shade), 90, 255);
        using var png = new MemoryStream();
        using (var bitmap = Pixels.ToBitmap(rgba, width, height)) bitmap.Save(png, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        return png.ToArray();
    }

    private static void WriteZip(string path, params (string Name, byte[] Bytes)[] entries)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, bytes) in entries)
        {
            using var output = zip.CreateEntry(name).Open();
            output.Write(bytes);
        }
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 300 && !condition(); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
        Assert.True(condition());
    }

    private static IEnumerable<string> ReaderTexts(Control root) => root.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "");

    [Fact]
    public async Task Comics_ReadPageByPageInNaturalOrderAndKeepOnlyTheNeighbourhoodDecoded() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                // ten pages stored out of order, a ComicInfo.xml, and one "page" that is not a picture
                var pages = Enumerable.Range(1, 10).Select(i => ("p" + i + ".png", PagePng(60 + i, 90, (byte)(i * 20)))).Reverse().ToList();
                pages[3] = ("p7.png", "isto não é uma imagem"u8.ToArray());
                WriteZip(Path.Combine(vault, "revista.cbz"), [.. pages, ("ComicInfo.xml", "<ComicInfo/>"u8.ToArray())]);
                var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().Select(f => (f, File.GetLastWriteTimeUtc(f))).ToArray();
                var preview = view.FilePreview;

                await preview.OpenAsync("revista.cbz");
                Assert.Equal("comic", preview.Shown);
                var comic = preview.GetLogicalDescendants().OfType<ComicReaderView>().Single();
                Assert.Equal(10, comic.PageCount);
                Assert.Equal("Página 1 de 10", comic.PageLabel);
                Assert.Null(comic.PageProblem);
                Assert.Contains(ReaderTexts(preview), t => t.StartsWith("Quadrinhos · ", StringComparison.Ordinal) && t.Contains("10 páginas", StringComparison.Ordinal));
                Assert.True(comic.DecodedPages.All(p => p <= 1));
                Dispatcher.UIThread.RunJobs();
                Snapshot(window, "native-comic-reader.png");

                // a tap on the right edge turns forward, on the left edge back
                comic.Tap(.9);
                await WaitFor(() => comic.CurrentPage == 1);
                comic.Tap(.1);
                await WaitFor(() => comic.CurrentPage == 0);

                // far into the book: only the page and its neighbours stay decoded
                await comic.GoToPageAsync(4);
                Assert.Equal("Página 5 de 10", comic.PageLabel);
                Assert.All(comic.DecodedPages, p => Assert.InRange(p, 3, 5));

                // the broken page says so; the book keeps reading
                await comic.GoToPageAsync(6);
                Assert.Contains("página 7", comic.PageProblem, StringComparison.Ordinal);
                comic.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right });
                await WaitFor(() => comic.CurrentPage == 7 && comic.PageProblem is null);
                comic.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.End });
                await WaitFor(() => comic.CurrentPage == 9);
                Assert.Equal("Página 10 de 10", comic.PageLabel);

                // zoom and fit
                comic.SetZoom(2);
                Assert.Equal(2, comic.Zoom);
                ClickButtonWithText(comic, "Ajustar à largura");
                Assert.True(comic.FitWidth);
                Assert.Contains(comic.GetLogicalDescendants().OfType<Button>(), b => Avalonia.Automation.AutomationProperties.GetName(b) == "Ajustar à tela");

                // a tap in the middle hides the bars (and the reader's own), the next one brings them back
                comic.Tap(.5);
                Assert.True(preview.ChromeHidden);
                Assert.False(comic.Bar.IsVisible);
                comic.Tap(.5);
                Assert.False(preview.ChromeHidden);

                // the page is remembered for the next opening
                await comic.GoToPageAsync(2);
                Assert.True(await preview.TryCloseAsync());
                await preview.OpenAsync("revista.cbz");
                var again = preview.GetLogicalDescendants().OfType<ComicReaderView>().Single();
                Assert.NotSame(comic, again);
                Assert.Equal(2, again.CurrentPage);

                preview.Close();
                Assert.Equal(before, Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().Select(f => (f, File.GetLastWriteTimeUtc(f))).ToArray());
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Comics_MaliciousOrUnsupportedArchivesSayWhyAndOfferAnotherApp() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var page = PagePng(20, 30, 40);
                WriteZip(Path.Combine(vault, "fuga.cbz"), ("p1.png", page), ("../../fora.png", page));
                File.WriteAllBytes(Path.Combine(vault, "antiga.cbr"), "Rar!\u001a\u0007\u0001\u0000resto"u8.ToArray());
                File.WriteAllBytes(Path.Combine(vault, "falsa.cbz"), "%PDF-1.7 renomeado"u8.ToArray());
                WriteZip(Path.Combine(vault, "texto.cbz"), ("leia.txt", "sem páginas"u8.ToArray()));
                // a zip bomb: one member far over the decompression budget
                using (var zip = ZipFile.Open(Path.Combine(vault, "bomba.cbz"), ZipArchiveMode.Create))
                {
                    using (var first = zip.CreateEntry("p1.png").Open()) first.Write(page);
                    using var bomb = zip.CreateEntry("p2.png", CompressionLevel.SmallestSize).Open();
                    var zeros = new byte[1024 * 1024];
                    for (int i = 0; i < 130; i++) bomb.Write(zeros);
                }
                var preview = view.FilePreview;
                bool OpenOffered() => preview.GetLogicalDescendants().OfType<Button>().Any(b => b.Content as string == "Abrir em outro app" && b.IsVisible);

                foreach (var (file, says) in new[]
                {
                    ("fuga.cbz", "caminhos inválidos"), ("antiga.cbr", "Formato não suportado"), ("falsa.cbz", "parece PDF"),
                    ("texto.cbz", "não contém imagens"), ("bomba.cbz", "orçamento seguro")
                })
                {
                    await preview.OpenAsync(file);
                    Assert.Equal("info", preview.Shown);
                    Assert.Contains(ReaderTexts(preview), t => t.Contains(says, StringComparison.Ordinal));
                    Assert.Contains(ReaderTexts(preview), t => t.StartsWith("Tamanho: ", StringComparison.Ordinal));
                    Assert.True(OpenOffered(), file);
                    Assert.Empty(preview.GetLogicalDescendants().OfType<ComicReaderView>());
                }
                Assert.Contains(ReaderTexts(preview), t => t.Contains("Abrir: completo", StringComparison.Ordinal));
                await preview.OpenAsync("antiga.cbr");
                Assert.Contains(ReaderTexts(preview), t => t.Contains("Abrir: fallback externo", StringComparison.Ordinal));
                Assert.Contains(ReaderTexts(preview), t => t == "Conteúdo: arquivo RAR");
                Snapshot(window, "native-reader-unsupported-comic.png");
                Assert.False(File.Exists(Path.Combine(dir, "fora.png")));
                Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(dir)!, "fora.png")));
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task PlainText_SearchesRtfCsvAndJsonAreReadHonestly() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                File.WriteAllText(Path.Combine(vault, "diario.txt"), "Primeira linha\nA AÇÃO começa aqui\ne a acao continua\n");
                File.WriteAllText(Path.Combine(vault, "carta.rtf"), @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\b Prezada\b0 ,\par Segue a cota\'e7\'e3o.\par}");
                File.WriteAllText(Path.Combine(vault, "notas.csv"), "aluno;nota\nAna;10\nBia;\"9,5\"\nCaio;8\n");
                File.WriteAllText(Path.Combine(vault, "config.json"), "{\"nome\":\"Urbe\",\"lista\":[1,2]}");
                File.WriteAllText(Path.Combine(vault, "quebrado.json"), "{\"nome\": }");
                File.WriteAllBytes(Path.Combine(vault, "antigo.log"), [(byte)'o', (byte)'l', 0xE1, (byte)'\n']);
                var preview = view.FilePreview;

                await preview.OpenAsync("diario.txt");
                Assert.Equal("text", preview.Shown);
                var text = preview.GetLogicalDescendants().OfType<TextReaderView>().Single();
                Assert.Contains(text.Find.Box, preview.GetLogicalDescendants().OfType<TextBox>());
                Assert.Equal("Buscar no texto", text.Find.Box.PlaceholderText);
                await text.SearchAsync("acao");
                Assert.Equal(2, text.MatchCount);
                Assert.Equal("AÇÃO", text.MarkedText);
                Assert.Equal("1 de 2", text.Find.Count);
                ClickButtonWithText(text, "Próximo resultado");
                Assert.Equal(1, text.CurrentMatch);
                Assert.Equal("acao", text.MarkedText);
                await text.SearchAsync("ausente");
                Assert.Equal("Nenhum", text.Find.Count);
                // the box searches as it is typed
                text.Find.Box.Text = "primeira";
                await WaitFor(() => text.MatchCount == 1);

                await preview.OpenAsync("carta.rtf");
                Assert.Equal("text", preview.Shown);
                Assert.Equal("Prezada,\nSegue a cotação.\n", preview.ShownText);
                Assert.Contains(ReaderTexts(preview), t => t == RtfText.Notice);

                await preview.OpenAsync("notas.csv");
                Assert.Equal("table", preview.Shown);
                var table = preview.GetLogicalDescendants().OfType<CsvTableView>().Single();
                Assert.Equal(3, table.RowCount);
                Assert.Equal(["aluno", "nota"], table.Header);
                Assert.Contains(ReaderTexts(preview), t => t.StartsWith("3 linhas × 2 colunas", StringComparison.Ordinal));
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains(ReaderTexts(table), t => t == "9,5");
                await table.SearchAsync("caio");
                Assert.Equal(2, table.SelectedRow);
                Assert.Equal("1 de 1 linha", table.Find.Count);
                Snapshot(window, "native-reader-csv.png");

                await preview.OpenAsync("config.json");
                Assert.Equal("{\n  \"nome\": \"Urbe\",\n  \"lista\": [\n    1,\n    2\n  ]\n}", preview.ShownText!.ReplaceLineEndings("\n"));
                await preview.OpenAsync("quebrado.json");
                Assert.Equal("{\"nome\": }", preview.ShownText);
                Assert.Contains(ReaderTexts(preview), t => t.StartsWith("JSON inválido", StringComparison.Ordinal));

                await preview.OpenAsync("antigo.log");
                Assert.Equal("olá\n", preview.ShownText);
                Assert.Contains(ReaderTexts(preview), t => t.Contains("Windows-1252", StringComparison.Ordinal));
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task PlainText_AHugeLogIsSearchedWholeAndOnlyAWindowIsLaidOut() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var log = new StringBuilder();
                for (int i = 0; i < 120_000; i++) log.Append("linha ").Append(i).Append(" registro comum do servidor\n");
                log.Append("ERRO FATAL no fim do arquivo\n");
                File.WriteAllText(Path.Combine(vault, "servidor.log"), log.ToString());
                var preview = view.FilePreview;

                await preview.OpenAsync("servidor.log");
                var text = preview.GetLogicalDescendants().OfType<TextReaderView>().Single();
                Assert.Equal(0, text.ShownStart);
                Assert.True(text.ShownEnd <= FilePreviewPanel.TextPart + 1);
                Assert.StartsWith("Mostrando 256 KB de ", text.StatusText, StringComparison.Ordinal);

                await text.SearchAsync("erro fatal");
                Assert.Equal(1, text.MatchCount);
                Assert.Equal("ERRO FATAL", text.MarkedText);
                // the window moved to the match; nothing before it is laid out, and it can be brought back
                Assert.True(text.ShownStart > FilePreviewPanel.TextPart);
                Assert.True(preview.GetLogicalDescendants().OfType<SelectableTextBlock>().Sum(b => b.Text?.Length ?? 0) <= FilePreviewPanel.TextPart + 1);
                int start = text.ShownStart;
                ClickButtonWithText(text, "Mostrar anterior");
                Assert.True(text.ShownStart < start);
                Assert.Equal(log.ToString(), preview.ShownText);
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task UnknownAndRenamedFiles_ShowAnHonestInfoPanelAndNeverCrash() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var png = PagePng(16, 12, 120);
                File.WriteAllBytes(Path.Combine(vault, "dados.xyz"), [0, 1, 2, 3]);
                File.WriteAllBytes(Path.Combine(vault, "foto.png"), "%PDF-1.4 na verdade um PDF"u8.ToArray());
                File.WriteAllBytes(Path.Combine(vault, "falso.jpg"), [0xFF, 0xD8, 0xFF, 0xC0, 0, 17, 8, 0xFF, 0xFF, 0xFF, 0xFF, 3, 1, 2, 3]);
                File.WriteAllBytes(Path.Combine(vault, "imagem.dat"), png);
                File.WriteAllBytes(Path.Combine(vault, "planilha.xlsx"), [0x50, 0x4B, 0x03, 0x04, 0, 0]);
                File.WriteAllBytes(Path.Combine(vault, "programa.txt"), [0x4D, 0x5A, 0x90, 0, 3, 0, 0, 0]);
                File.WriteAllBytes(Path.Combine(vault, "quebrado.zip"), [0x50, 0x4B, 0x03, 0x04, 9, 9]);
                var preview = view.FilePreview;

                await preview.OpenAsync("dados.xyz");
                Assert.Equal("info", preview.Shown);
                Assert.Contains(ReaderTexts(preview), t => t == "Sem visualização nativa para este formato.");
                Assert.Contains(ReaderTexts(preview), t => t == "Tipo: Arquivo · application/octet-stream");
                Assert.Contains(ReaderTexts(preview), t => t == "Tamanho: 4 B");
                Assert.Contains(preview.GetLogicalDescendants().OfType<Button>(), b => b.Content as string == "Abrir em outro app" && b.IsVisible);
                Snapshot(window, "native-reader-unknown-file.png");

                await preview.OpenAsync("foto.png");
                Assert.Equal("info", preview.Shown);
                Assert.Contains(ReaderTexts(preview), t => t == "Conteúdo: PDF");

                // a JPEG header that claims 65535×65535 pixels is never decoded whole; it must not crash
                await preview.OpenAsync("falso.jpg");
                Assert.True(preview.Shown is "info" or "image");

                await preview.OpenAsync("imagem.dat");
                Assert.Equal("image", preview.Shown);
                Assert.Contains(ReaderTexts(preview), t => t.Contains("conteúdo: imagem PNG", StringComparison.Ordinal));

                await preview.OpenAsync("planilha.xlsx");
                Assert.Equal("info", preview.Shown);
                Assert.Contains(ReaderTexts(preview), t => t.StartsWith("Abrir: fallback externo · Busca: fallback externo · Seleção: fallback externo · Offline: sim", StringComparison.Ordinal));

                await preview.OpenAsync("programa.txt");
                Assert.Equal("info", preview.Shown);
                Assert.Contains(ReaderTexts(preview), t => t.Contains("programa executável", StringComparison.Ordinal));

                await preview.OpenAsync("quebrado.zip");
                Assert.Equal("info", preview.Shown);
                Assert.Contains(ReaderTexts(preview), t => t == "O arquivo não pôde ser lido como ZIP.");
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
