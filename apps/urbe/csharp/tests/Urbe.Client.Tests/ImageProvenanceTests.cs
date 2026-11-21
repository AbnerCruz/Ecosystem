using SkiaSharp;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>CST-082: a generated image carries, inside the file, that it was made by a model, which, from what and when.</summary>
public sealed class ImageProvenanceTests
{
    private static readonly ImageProvenance Made = new("gpt-image-test", "OpenAI", "Um farol ao entardecer, aquarela — “cores quentes” & <traço leve>", new DateTimeOffset(2026, 11, 17, 9, 30, 0, TimeSpan.FromHours(-3)));

    private static byte[] Encode(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(64, 32);
        bitmap.Erase(SKColors.Orange);
        using var data = SKImage.FromBitmap(bitmap).Encode(format, 90);
        return data.ToArray();
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    public void TheImageStillOpensAndSaysWhatMadeIt(SKEncodedImageFormat format)
    {
        var original = Encode(format);
        Assert.Null(ImageProvenance.Read(original));
        var marked = Made.Embed(original);
        Assert.True(marked.Length > original.Length);
        using (var decoded = SKBitmap.Decode(marked))
        {
            Assert.NotNull(decoded);
            Assert.Equal((64, 32), (decoded.Width, decoded.Height));
        }
        Assert.Equal(Made, ImageProvenance.Read(marked));
        Assert.Contains(ImageProvenance.TrainedAlgorithmicMedia, System.Text.Encoding.UTF8.GetString(marked));
    }

    [Fact]
    public void OtherFormatsAreLeftAsTheyAre()
    {
        var webp = Encode(SKEncodedImageFormat.Webp);
        Assert.Same(webp, Made.Embed(webp));
    }

    [Fact]
    public void ThePngChunkIsValid()
    {
        var marked = Made.Embed(Encode(SKEncodedImageFormat.Png));
        // the chunk after IHDR is iTXt with the XMP keyword, (its CRC: ImageProvenanceCrcTests)
        Assert.Equal("iTXt", System.Text.Encoding.ASCII.GetString(marked, 37, 4));
        Assert.Equal("XML:com.adobe.xmp", System.Text.Encoding.ASCII.GetString(marked, 41, 17));
    }
}

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-082: the preview of a generated picture says it was made by AI and by which model; its description is the alternative text.</summary>
    [Fact]
    public async Task GeneratedImage_ThePreviewSaysWhatMadeIt() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-provenance-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Imagens"));
            using (var bitmap = new SKBitmap(16, 16))
            {
                bitmap.Erase(SKColors.Teal);
                using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(vault, "Imagens", "Farol.png"),
                    new ImageProvenance("modelo-x", "OpenAI", "Um farol ao entardecer", new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero)).Embed(data.ToArray()));
            }
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Avalonia.Controls.Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                await view.FilePreview.OpenAsync("Imagens/Farol.png");
                await Settle(window, view);
                Assert.Equal("image", view.FilePreview.Shown);
                Assert.Contains(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view.FilePreview).OfType<Avalonia.Controls.TextBlock>(),
                    t => t.Text?.Contains("gerada por IA (modelo-x, 17/11/2026)") == true);
                Assert.Contains(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view.FilePreview).OfType<Avalonia.Controls.Control>(),
                    c => Avalonia.Automation.AutomationProperties.GetName(c) == "Um farol ao entardecer");
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
