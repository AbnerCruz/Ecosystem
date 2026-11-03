using Avalonia;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Rendering.Skia;

namespace Urbe.Client;

/// <summary>Visual PDF fallback for Windows and hosts without PdfRenderer, using the same PdfPig parser as extraction.</summary>
public sealed class PortablePdfDocument : ITiledPdfDocument
{
    private readonly PdfDocument _document;
    private PortablePdfDocument(byte[] bytes)
    {
        _document = PdfDocument.Open(bytes, SkiaRenderingParsingOptions.Instance);
        _document.AddSkiaPageFactory();
    }
    public static Task<PortablePdfDocument> OpenAsync(byte[] bytes, CancellationToken cancellationToken) =>
        Task.Run(() => new PortablePdfDocument(bytes), cancellationToken);
    public int PageCount => _document.NumberOfPages;
    public Size PageSize(int index)
    {
        var page = _document.GetPageSize(index + 1);
        return new(page.Width, page.Height);
    }
    public (byte[] Rgba, int Width, int Height) Render(int index, int width)
    {
        var size = PageSize(index);
        using var original = _document.GetPageAsSKBitmap(index + 1, (float)(width / size.Width), SKColors.White);
        using var rgba = new SKBitmap(new SKImageInfo(original.Width, original.Height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(rgba)) canvas.DrawBitmap(original, 0, 0);
        return (rgba.Bytes, rgba.Width, rgba.Height);
    }
    public (byte[] Rgba, int Width, int Height) RenderRegion(int index, int pageWidth, PixelRect region)
    {
        var size = PageSize(index);
        using var picture = _document.GetPageAsSKPicture(index + 1);
        using var bitmap = new SKBitmap(new SKImageInfo(region.Width,region.Height,SKColorType.Rgba8888,SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White); canvas.Translate(-region.X,-region.Y);
            canvas.Scale((float)(pageWidth / size.Width)); canvas.DrawPicture(picture);
        }
        return (bitmap.Bytes,bitmap.Width,bitmap.Height);
    }
    public void Dispose() => _document.Dispose();
}
