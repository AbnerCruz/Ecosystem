using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.LogicalTree;
using Urbe.Client.Ui;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task ImagePreview_DetachResizeAndReattachDoesNotReadADisposedBitmap() =>
        Assert.True(await Session.Dispatch<bool>(() =>
    {
        var decodes = 0;
        var preview = new ZoomImageView(() => { decodes++; return new RenderTargetBitmap(new PixelSize(80, 60)); });
        var window = new Window { Width = 360, Height = 640, Content = preview };
        window.Show();
        try
        {
            window.UpdateLayout();
            var image = preview.GetLogicalDescendants().OfType<Image>().Single();
            Assert.NotNull(image.Source);
            window.Content = null;
            // Resizing/reparenting native views is not destruction of their logical content.
            preview.SetZoom(2);
            window.Content = preview;
            window.UpdateLayout();
            Assert.Equal(2, preview.Zoom);
            Assert.NotNull(image.Source);
            Assert.Equal(new Size(80, 60), image.Source!.Size);
            Assert.Equal(2, decodes);
            Snapshot(window, "native-image-reattached.png");
            return Task.FromResult(true);
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
