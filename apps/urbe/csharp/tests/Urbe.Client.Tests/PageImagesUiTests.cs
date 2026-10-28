using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Pages_AVaultImageIsInsideTheShownAndDownloadedPage() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // CST-059 (owner: an image added to a page did not show): the viewer reaches nothing outside the page
            var dir = Path.Combine(Path.GetTempPath(), "urbe-page-img-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Páginas"));
            Directory.CreateDirectory(Path.Combine(vault, "Imagens"));
            byte[] png;
            using (var mem = new MemoryStream())
            {
                Pixels.ToBitmap(Enumerable.Repeat((byte)120, 4 * 4 * 4).ToArray(), 4, 4).Save(mem, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                png = mem.ToArray();
            }
            File.WriteAllBytes(Path.Combine(vault, "Imagens", "capa.png"), png);
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n![foto](Imagens/capa.png)\n");
            const string page = """{"version":1,"kind":"urbe-page","meta":{"title":"Meu site"},"sections":[{"id":"i1","type":"image","props":{"src":"Imagens/capa.png","alt":"Capa"}},{"id":"n1","type":"note","props":{"path":"Comece.md"}}]}""";
            File.WriteAllText(Path.Combine(vault, "Páginas", "Meu site.page.json"), page);
            var requests = new List<ContentRequest>();
            var factory = ContentViewers.Factory;
            ContentViewers.Factory = (r, _) => { requests.Add(r); return Task.FromResult<Control?>(new Border()); };
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                var data = "data:image/png;base64," + Convert.ToBase64String(png);
                await view.ShowPageAsync("Páginas/Meu site.page.json");
                var html = FilePreviewPanel.Text(Assert.Single(requests).Bytes!)!;
                // the image block and the note's image are both the picture itself
                Assert.Equal(2, html.Split(data).Length - 1);
                Assert.DoesNotContain("src=\"Imagens/capa.png\"", html);
                view.FilePreview.Close();

                (string Name, byte[] Bytes)? downloaded = null;
                view.SavePageFile = (n, b) => { downloaded = (n, b); return Task.FromResult<bool?>(true); };
                await view.DownloadPageHtmlAsync("Páginas/Meu site.page.json");
                Assert.Contains(data, System.Text.Encoding.UTF8.GetString(downloaded!.Value.Bytes));
                return true;
            }
            finally
            {
                ContentViewers.Factory = factory;
                window.Close();
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
