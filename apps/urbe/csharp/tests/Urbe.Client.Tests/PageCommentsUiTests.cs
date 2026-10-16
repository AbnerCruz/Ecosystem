using Avalonia.Controls;
using Avalonia.LogicalTree;
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

    [Fact]
    public async Task PageComments_CommentarInThePreviewWritesThePageAndTheNote() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-pcw-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Páginas"));
            var noteFile = Path.Combine(vault, "Comece.md");
            File.WriteAllText(noteFile, "# Comece\n\nUma casa{\"rever\"} na rua.\n");
            const string page = """{"version":1,"kind":"urbe-page","meta":{"title":"Meu site"},"sections":[{"id":"t1","type":"text","props":{"markdown":"Olá mundo{\"saudação\"}."}},{"id":"n1","type":"note","props":{"path":"Comece.md"}}]}""";
            var pageFile = Path.Combine(vault, "Páginas", "Meu site.page.json");
            File.WriteAllText(pageFile, page);
            var factory = ContentViewers.Factory;
            FakeNativePreview? fake = null;
            ContentViewers.Factory = (r, _) => Task.FromResult<Control?>(r.Messages is null ? new Border() : fake = new FakeNativePreview());
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            async Task Answer(string comment)
            {
                for (int i = 0; i < 40 && !view.Dialogs.IsOpen; i++) await Pump(10);
                Assert.True(view.Dialogs.IsOpen);
                var box = view.Dialogs.GetLogicalDescendants().OfType<TextBox>()
                    .First(t => Avalonia.Automation.AutomationProperties.GetName(t) == "Novo comentário");
                box.Text = comment;
                ClickButtonWithText(view.Dialogs, "Adicionar");
                for (int i = 0; i < 40 && view.Dialogs.IsOpen; i++) await Pump(10);
            }
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                await view.OpenStudioAsync("Páginas/Meu site.page.json");
                for (int i = 0; i < 40 && !view.Studio.LastHtml.Contains("urbe-cm", StringComparison.Ordinal); i++) await Pump(20);
                Assert.NotNull(fake);
                Assert.Contains("data-field=\"sections.0.props.markdown\" data-n=\"0\"", view.Studio.LastHtml);
                Assert.Contains("data-note=\"Comece.md\" data-n=\"0\"", view.Studio.LastHtml);

                // the page's own text: the comment goes after the word's, saved in the page file
                view.Studio.PreviewMessage("""{"urbePage":"comment","field":"sections.0.props.markdown","n":0}""");
                await Answer("novo");
                await view.Studio.FlushAsync();
                Assert.Contains("mundo{\\\"saudação\\\"}{\\\"novo\\\"}.", File.ReadAllText(pageFile));
                for (int i = 0; i < 60 && !view.Studio.LastHtml.Contains("💬2", StringComparison.Ordinal); i++) await Pump(20);
                Assert.Contains("💬2", view.Studio.LastHtml);

                // a note the page shows: written into the note by the verified save
                view.Studio.PreviewMessage("""{"urbePage":"comment","note":"Comece.md","n":0}""");
                await Answer("da nota");
                for (int i = 0; i < 60 && !File.ReadAllText(noteFile).Contains("da nota", StringComparison.Ordinal); i++) await Pump(20);
                Assert.Equal("# Comece\n\nUma casa{\"rever\"}{\"da nota\"} na rua.\n", File.ReadAllText(noteFile));
                for (int i = 0; i < 60 && !view.Studio.LastHtml.Contains("rever\nda nota", StringComparison.Ordinal); i++) await Pump(20);
                Assert.Contains("title=\"rever\nda nota\"", view.Studio.LastHtml);

                // a stale 💬 (no such group) writes nothing
                var before = File.ReadAllText(pageFile);
                view.Studio.PreviewMessage("""{"urbePage":"comment","field":"sections.0.props.markdown","n":5}""");
                await Pump(30);
                Assert.False(view.Dialogs.IsOpen);
                await view.Studio.FlushAsync();
                Assert.Equal(before, File.ReadAllText(pageFile));

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
