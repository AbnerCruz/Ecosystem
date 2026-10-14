using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>ADD-0023: HTML, PDF, audio and video are seen inside the Urbe, through the platform's content viewers.</summary>
public sealed partial class HeadlessUiTests
{
    private sealed class FakePdf(int pages) : IPdfDocument
    {
        public List<int> Rendered { get; } = [];
        public bool Disposed { get; private set; }
        public int PageCount => pages;
        public Size PageSize(int index) => new(600, 800);
        public (byte[] Rgba, int Width, int Height) Render(int index, int width)
        {
            Rendered.Add(index);
            int h = width * 4 / 3;
            var rgba = new byte[width * h * 4];
            Array.Fill(rgba, (byte)255);
            return (rgba, width, h);
        }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public async Task ContentViewer_HtmlAsAPageOrItsSourceAndPdfPagesInsideThePanel() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            var requests = new List<ContentRequest>();
            var pdf = new FakePdf(30);
            ContentViewers.Factory = (r, _) =>
            {
                requests.Add(r);
                return Task.FromResult<Control?>(new Border { Tag = "viewer:" + r.Kind });
            };
            ContentViewers.PdfReader = (_, _) => Task.FromResult<IPdfDocument?>(pdf);
            try
            {
                File.WriteAllText(Path.Combine(vault, "site.html"), "<h1>Olá</h1>");
                File.WriteAllBytes(Path.Combine(vault, "livro.pdf"), [0x25, 0x50, 0x44, 0x46]);
                File.WriteAllBytes(Path.Combine(vault, "som.mp3"), new byte[2048]);
                var preview = view.FilePreview;

                // HTML: the page, by the platform's viewer, with its text
                await preview.OpenAsync("site.html");
                Assert.Equal("viewer", preview.Shown);
                var html = Assert.Single(requests);
                Assert.Equal(("site.html", VaultFileKinds.Html), (html.Path, html.Kind));
                Assert.Equal("<h1>Olá</h1>", FilePreviewPanel.Text(html.Bytes!));
                // Fonte: its source as text, without asking the viewer again
                ClickButtonWithText(preview, "Fonte");
                for (int i = 0; i < 20 && preview.Shown != "text"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("<h1>Olá</h1>", preview.ShownText);
                Assert.Single(requests);
                ClickButtonWithText(preview, "Página");
                for (int i = 0; i < 20 && preview.Shown != "viewer"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("viewer", preview.Shown);

                // audio: played from the vault (only its size read), "Abrir" still there as an extra
                await preview.OpenAsync("som.mp3");
                Assert.Equal("viewer", preview.Shown);
                var audio = requests[^1];
                Assert.Equal(VaultFileKinds.Audio, audio.Kind);
                Assert.Null(audio.Bytes);
                Assert.Contains(preview.GetLogicalDescendants().OfType<TextBlock>(), t => (t.Text ?? "").StartsWith("Áudio · 2.0 KB", StringComparison.Ordinal));

                // PDF: pages drawn as images near what is on screen, the document closed with the panel
                await preview.OpenAsync("livro.pdf");
                Assert.Equal("viewer", preview.Shown);
                var pages = preview.GetLogicalDescendants().OfType<PdfPagesView>().Single();
                Assert.Equal(30, pages.PageCount);
                for (int i = 0; i < 20; i++) { window.UpdateLayout(); await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(pages.Drawn.Contains(0), $"bounds={pages.Bounds} viewport={pages.Viewport} extent={pages.Extent} rendered={string.Join(",", pdf.Rendered)} visible={pages.IsEffectivelyVisible} h={pages.Height}");
                Assert.True(pages.Drawn.Count < 10, "only the pages near the screen are drawn: " + string.Join(",", pages.Drawn));
                Snapshot(window, "native-content-pdf.png");
                Assert.True(pages.Extent.Height > pages.Viewport.Height * 5, $"extent={pages.Extent} viewport={pages.Viewport} bounds={pages.Bounds} vis={pages.IsEffectivelyVisible} arr={pages.IsArrangeValid} pres={pages.Presenter} prev={preview.Bounds} attached={TopLevel.GetTopLevel(pages) is not null}");
                pages.Offset = new Vector(0, pages.Extent.Height - pages.Viewport.Height);
                for (int i = 0; i < 20; i++) { window.UpdateLayout(); await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(pages.Drawn.Contains(29), $"offset={pages.Offset} viewport={pages.Viewport} extent={pages.Extent} drawn={string.Join(",", pages.Drawn)} rendered={string.Join(",", pdf.Rendered)}");
                Assert.DoesNotContain(0, pages.Drawn);
                preview.Close();
                Dispatcher.UIThread.RunJobs();
                Assert.True(pdf.Disposed);
                window.Close();
                return true;
            }
            finally
            {
                ContentViewers.Factory = null;
                ContentViewers.PdfReader = null;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
