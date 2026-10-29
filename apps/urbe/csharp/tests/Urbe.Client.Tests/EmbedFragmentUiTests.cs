using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_AnEmbedOfASectionOrBlockShowsOnlyThatPart() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // CST-064: ![[Nota#Título]] and ![[Nota#^id]] showed the whole note
            var dir = Path.Combine(Path.GetTempPath(), "urbe-embed-part-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Reino.md"), "# Reino\n\nAbertura do reino.\n\n## Economia\n\nTrigo e sal. ^renda\n\n## Guerras\n\nA guerra do norte.\n");
            File.WriteAllText(Path.Combine(vault, "Teste.md"), "![[Reino#Economia]]\n\n![[Reino#^renda]]\n\n![[Reino#Fantasia]]\n");
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.OpenNoteByPath("Teste.md");
                List<string> texts = [];
                for (int i = 0; i < 60; i++)
                {
                    await Task.Delay(20);
                    Dispatcher.UIThread.RunJobs();
                    texts = view.Editor.GetVisualDescendants().OfType<TextBlock>()
                        .Select(t => t.Text ?? string.Concat(t.Inlines?.OfType<Run>().Select(r => r.Text) ?? [])).ToList();
                    if (texts.Any(t => t.Contains("Trigo e sal"))) break;
                }
                Assert.True(texts.Count(t => t.Contains("Trigo e sal")) == 2, string.Join(" | ", texts));
                Assert.DoesNotContain(texts, t => t.Contains("Abertura do reino") || t.Contains("guerra do norte"));
                Assert.DoesNotContain(texts, t => t.Contains("sal. ^renda"));
                Assert.Contains(texts, t => t == "Reino › Economia");
                Assert.Contains(texts, t => t.Contains("O título \"Fantasia\" não existe"));
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch { } }
        }, CancellationToken.None));
}
