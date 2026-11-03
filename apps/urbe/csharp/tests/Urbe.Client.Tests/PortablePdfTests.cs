using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task PortablePdf_RendersRealTextAndGraphicsWithoutAPlatformReader() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        using var document = await PortablePdfDocument.OpenAsync(StudyPdf(), CancellationToken.None);
        Assert.Equal(3, document.PageCount);
        var (rgba, width, height) = document.Render(1, 400);
        Assert.InRange(width, 400, 401); // upstream uses ceil(point width * float scale)
        Assert.True(height > width);
        Assert.Contains(Enumerable.Range(0, rgba.Length / 4), i => rgba[i * 4] < 200);
        var tile=document.RenderRegion(1,10000,new PixelRect(0,1800,1024,1024));
        Assert.Equal(1024*1024*4,tile.Rgba.Length);Assert.Equal(1024,tile.Width);
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task PdfViewport_ThousandPagesHaveOnlyNearbyFramesAndRenderOffTheUiThread() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var doc = new SlowPdf();
        var reader = new PdfPagesView(doc);
        var window = new Window { Width = 390, Height = 720, Content = reader };
        window.Show();
        try
        {
            for (int i = 0; i < 100 && reader.Drawn.Count == 0; i++) { window.UpdateLayout(); await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.InRange(reader.MaterializedPages, 1, 16);
            Assert.NotEmpty(reader.Drawn);
            Assert.False(doc.RenderedOnUiThread);
            reader.GoToPage(800);
            for (int i = 0; i < 100 && !reader.Drawn.Contains(799); i++) { window.UpdateLayout(); await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Contains(799, reader.Drawn);
            Assert.InRange(reader.MaterializedPages, 1, 16);
            Assert.InRange(doc.SizeReads, 0, 20);
            reader.SetZoom(16);
            for(int i=0;i<100&&reader.MaterializedTiles==0;i++){window.UpdateLayout();await Task.Delay(10);Dispatcher.UIThread.RunJobs();}
            Assert.Equal(16,reader.Zoom);Assert.InRange(reader.MaterializedTiles,1,20);
            Assert.InRange(doc.LargestOverview,1000,1600);Assert.True(doc.LargestTilePage>1600);
            reader.SetSinglePage(true);reader.GoToPage(900);Assert.Equal(900,reader.CurrentPage);
        }
        finally { window.Close(); }
        for (int i = 0; i < 100 && !doc.Disposed; i++) await Task.Delay(10);
        Assert.True(doc.Disposed);
        return true;
    }, CancellationToken.None));

    private sealed class SlowPdf : ITiledPdfDocument
    {
        public int PageCount => 1000;
        public int SizeReads,LargestOverview,LargestTilePage;
        public bool RenderedOnUiThread, Disposed;
        public Size PageSize(int index) { Interlocked.Increment(ref SizeReads); return new(600, 800); }
        public (byte[] Rgba, int Width, int Height) Render(int index, int width)
        {
            LargestOverview=Math.Max(LargestOverview,width);
            RenderedOnUiThread |= Dispatcher.UIThread.CheckAccess();
            Thread.Sleep(15);
            int height = width * 4 / 3;
            return (Enumerable.Repeat((byte)255, width * height * 4).ToArray(), width, height);
        }
        public (byte[] Rgba,int Width,int Height) RenderRegion(int index,int pageWidth,PixelRect region)
        {LargestTilePage=Math.Max(LargestTilePage,pageWidth);Assert.InRange(region.Width,1,1024);Assert.InRange(region.Height,1,1024);return(new byte[region.Width*region.Height*4],region.Width,region.Height);}
        public void Dispose() => Disposed = true;
    }
}
