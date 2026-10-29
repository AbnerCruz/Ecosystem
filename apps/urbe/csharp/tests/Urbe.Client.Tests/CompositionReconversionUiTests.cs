using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // CST-062: the conversion keeps the notes in their order, deletes nothing and, repeated after the vault is opened
    // again, makes no second page
    [Fact]
    public async Task Compositions_KeepTheOrderAndAreNotConvertedTwiceAfterReopening() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-cmp2-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, ".urbe"));
            File.WriteAllText(Path.Combine(vault, "A.md"), "# A\n\nPrimeira.\n");
            File.WriteAllText(Path.Combine(vault, "B.md"), "# B\n\nSegunda.\n");
            File.WriteAllText(Path.Combine(vault, ".urbe", "mapa.json"), "{\"v\":4,\"mundo\":\"placas-1\",\"notas\":{\"A.md\":{\"id\":\"doc_a\"},\"B.md\":{\"id\":\"doc_b\"}}}");
            var compositions = "{\"version\":2,\"items\":[{\"id\":\"cmp_um\",\"name\":\"Livro\",\"sources\":[\"doc_b\",\"doc_sumiu\",\"doc_a\"]}]}";
            File.WriteAllText(Path.Combine(vault, ".urbe", "compositions.v2.json"), compositions);
            var notesBefore = Directory.EnumerateFiles(vault, "*.md", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllText);

            async Task<(MainView View, Window Window)> Open()
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.ShowPages();
                Dispatcher.UIThread.RunJobs();
                return (view, window);
            }
            try
            {
                var (view, window) = await Open();
                Assert.Equal(1, view.Pages.PendingCompositions);
                await RunWithDialog(view.ConvertCompositionsAsync(), async () => { ClickButtonWithText(view.Dialogs, "Converter"); await Pump(200); });
                window.Close();
                var page = Path.Combine(vault, "Páginas", "Composições", "Livro.page.json");
                var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(page))!;
                var paths = json["sections"]!.AsArray().Select(s => (string?)s!["props"]?["path"]).ToList();
                Assert.Equal("B.md", paths[0]);   // the composition's order
                Assert.Equal("A.md", paths[^1]);
                Assert.Equal(3, paths.Count);     // the note that no longer exists stays as a section

                var (again, window2) = await Open();
                try
                {
                    Assert.Equal(0, again.Pages.PendingCompositions);
                    await again.ConvertCompositionsAsync();   // "As composições já viraram páginas.": nothing asked, nothing made
                    Assert.False(again.Dialogs.IsOpen);
                    Assert.Single(Directory.EnumerateFiles(Path.Combine(vault, "Páginas"), "*.page.json", SearchOption.AllDirectories));
                    Assert.Equal(compositions, File.ReadAllText(Path.Combine(vault, ".urbe", "compositions.v2.json")));
                    foreach (var (file, text) in notesBefore) Assert.Equal(text, File.ReadAllText(file));
                }
                finally { window2.Close(); }
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
