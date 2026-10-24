using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>openFilePreview natively: an image is drawn, text and a .zip listing are shown, a PDF offers Abrir.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task FilePreview_ShowsImagesTextAndZipAndNothingIsWritten() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                // a 48×56 picture (any PNG of the vault)
                var rgba = new byte[48 * 56 * 4];
                for (int i = 0; i < rgba.Length; i += 4) (rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]) = ((byte)(i % 251), 90, 160, 255);
                Directory.CreateDirectory(Path.Combine(vault, "Anexos"));
                using (var png = new MemoryStream())
                {
                    Pixels.ToBitmap(rgba, 48, 56).Save(png, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    File.WriteAllBytes(Path.Combine(vault, "Anexos", "torre.png"), png.ToArray());
                }
                File.WriteAllText(Path.Combine(vault, "registro.log"), "linha 1\nlinha 2\n");
                using (var zip = ZipFile.Open(Path.Combine(vault, "pacote.zip"), ZipArchiveMode.Create))
                {
                    zip.CreateEntry("a.md");
                    zip.CreateEntry("pasta/b.png");
                }
                File.WriteAllBytes(Path.Combine(vault, "livro.pdf"), [0x25, 0x50, 0x44, 0x46, 0, 1, 2]);
                var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().Select(f => (f, File.GetLastWriteTimeUtc(f))).ToArray();
                var preview = view.FilePreview;

                await preview.OpenAsync("Anexos/torre.png");
                Assert.True(preview.IsVisible);
                Assert.Equal("image", preview.Shown);
                Dispatcher.UIThread.RunJobs();
                Snapshot(window, "native-file-preview-image.png");
                var meta = preview.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains(meta, t => t is not null && t.StartsWith("Imagem · ", StringComparison.Ordinal) && t.EndsWith(" · Anexos/torre.png", StringComparison.Ordinal));

                await preview.OpenAsync("registro.log");
                Assert.Equal("text", preview.Shown);
                Assert.Equal("linha 1\nlinha 2\n", preview.ShownText);

                await preview.OpenAsync("pacote.zip");
                Assert.Equal("zip", preview.Shown);
                Assert.Equal("a.md\npasta/b.png", preview.ShownText);

                await preview.OpenAsync("livro.pdf");
                Assert.Equal("none", preview.Shown);
                Assert.Contains(preview.GetLogicalDescendants().OfType<Button>(), b => b.Content as string == "Abrir em outro app" && b.IsVisible);

                await preview.OpenAsync("sumiu.png");
                Assert.Contains(preview.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Arquivo não encontrado");
                Assert.DoesNotContain(preview.GetLogicalDescendants().OfType<Button>(), b => b.Content as string == "Abrir" && b.IsVisible);

                preview.Close();
                Assert.False(preview.IsVisible);
                Assert.Equal(before, Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order().Select(f => (f, File.GetLastWriteTimeUtc(f))).ToArray());
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
