using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>UC-49 in pages: the studio preview shows a word's comments as a 💬; the exported HTML leaves them out.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task PageComments_ShowInTheStudioPreviewButNotInTheExport() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-pcm-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Páginas"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\nUma casa{\"rever\"}{\"cor?\"} na rua.\n");
            const string page = """{"version":1,"kind":"urbe-page","meta":{"title":"Meu site"},"sections":[{"id":"t1","type":"text","props":{"markdown":"Olá mundo{\"saudação\"}."}},{"id":"n1","type":"note","props":{"path":"Comece.md"}}]}""";
            File.WriteAllText(Path.Combine(vault, "Páginas", "Meu site.page.json"), page);
            var factory = ContentViewers.Factory;
            ContentViewers.Factory = (r, _) => Task.FromResult<Control?>(new Border { Tag = "viewer:" + r.Kind });
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                await view.OpenStudioAsync("Páginas/Meu site.page.json");
                for (int i = 0; i < 40 && !view.Studio.LastHtml.Contains("urbe-cm", StringComparison.Ordinal); i++) await Pump(20);
                var preview = view.Studio.LastHtml;
                Assert.Contains("mundo<sup class=\"urbe-cm\"", preview);
                Assert.Contains("title=\"saudação\"", preview);
                Assert.Contains("casa<sup class=\"urbe-cm\"", preview);
                Assert.Contains("💬2</sup>", preview);

                (string Name, byte[] Bytes)? downloaded = null;
                view.SavePageFile = (n, b) => { downloaded = (n, b); return Task.FromResult<bool?>(true); };
                await view.DownloadPageHtmlAsync("Páginas/Meu site.page.json");
                var html = System.Text.Encoding.UTF8.GetString(downloaded!.Value.Bytes);
                Assert.Contains("Olá mundo.", html);
                Assert.Contains("Uma casa na rua.", html);
                Assert.DoesNotContain("urbe-cm", html);
                Assert.DoesNotContain("saudação", html);
                Assert.DoesNotContain("rever", html);

                await view.Studio.CloseAsync();
                window.Close();
                return true;
            }
            finally
            {
                ContentViewers.Factory = factory;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
