using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>24/10/2026: <c>![[destino]]</c> shown in place in the Visual editor, and the 💬 of a comment no longer cutting the text.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_ShowsEmbeddedNotesImagesAndFilesInPlace() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-embed-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Alvo.md"), "# Alvo\n\nTexto da nota **incorporada**.\n");
            File.WriteAllText(Path.Combine(vault, "dados.txt"), "linha um\nlinha dois\n");
            File.WriteAllText(Path.Combine(vault, "pagina.html"), "<h1>Olá página</h1>");
            using (var png = File.Create(Path.Combine(vault, "foto.png")))
                Pixels.ToBitmap(Enumerable.Repeat((byte)200, 4 * 4 * 4).ToArray(), 4, 4).Save(png, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            File.WriteAllText(Path.Combine(vault, "Teste.md"),
                "![[Alvo]]\n\n![[pagina.html]]\n\n![[foto.png]]\n\n![[dados.txt]]\n\n![[Nada]]\n\numa palavra{\"um comentário bem comprido que antes cortava o texto ao redor da marca\"} e mais texto depois dela.\n");
            // the platform's viewer (a web view on the phone) is replaced by one that shows what it was given
            var factory = ContentViewers.Factory;
            ContentViewers.Factory = (request, _) => Task.FromResult<Control?>(request.Kind == Urbe.Core.VaultFileKinds.Html
                ? new TextBlock { Text = "PÁGINA:" + System.Text.Encoding.UTF8.GetString(request.Bytes ?? []) } : null);
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.OpenNoteByPath("Teste.md");
                for (int i = 0; i < 100; i++)
                {
                    await Task.Delay(20);
                    Dispatcher.UIThread.RunJobs();
                    var shown = view.Editor.GetVisualDescendants().OfType<Image>().Any(im => im.Source is not null && im.Parent is Border { Child: Image });
                    var page = view.Editor.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.StartsWith("PÁGINA:", StringComparison.Ordinal) == true);
                    var text = view.Editor.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.StartsWith("linha um", StringComparison.Ordinal) == true);
                    if (shown && text && page) break;
                }
                Snapshot(window, "native-embeds.png");
                var all = view.Editor.GetVisualDescendants().ToList();
                Assert.Equal(5, all.OfType<Border>().Count(b => b.Classes.Contains("urbe-embed")) + all.OfType<Border>().Count(b => b.Child is TextBlock { Text: { } t } && t.StartsWith("Não encontrei “Nada”", StringComparison.Ordinal)));
                Assert.Equal(4, all.OfType<Border>().Count(b => b.Classes.Contains("urbe-embed")));
                var pg = all.OfType<TextBlock>().First(t => t.Text?.StartsWith("PÁGINA:", StringComparison.Ordinal) == true);
                Assert.True(pg.Text == "PÁGINA:<h1>Olá página</h1>" && pg.Height == 420, pg.Text + " / " + pg.Height);
                // The embedded HTML must not cover the file's own source sheet on Android.
                await view.FilePreview.OpenAsync("pagina.html");
                Assert.False(pg.IsVisible);
                ClickButtonWithText(view.FilePreview, "Fonte");
                for (int i = 0; i < 30 && view.FilePreview.Shown != "text"; i++)
                {
                    await Task.Delay(10);
                    Dispatcher.UIThread.RunJobs();
                }
                Assert.Equal("<h1>Olá página</h1>", view.FilePreview.ShownText);
                Assert.False(pg.IsVisible);
                view.FilePreview.Close();
                Assert.True(pg.IsVisible);
                var texts = all.OfType<TextBlock>().Select(t => t.Text ?? string.Concat(t.Inlines?.OfType<Run>().Select(r => r.Text) ?? [])).ToList();
                Assert.Contains(texts, t => t.Contains("Texto da nota", StringComparison.Ordinal));
                Assert.Contains(texts, t => t.StartsWith("linha um", StringComparison.Ordinal));
                Assert.Contains(all.OfType<Image>(), im => im.Source is not null && im.Parent is Border);
                // the embedded paragraphs are cards, not the plain "![[…]]" text
                Assert.DoesNotContain(texts, t => t.StartsWith("![[", StringComparison.Ordinal));

                // the comment's mark is a run inside the text: nothing in the text is a control that clips its neighbours
                var line = view.Editor.GetVisualDescendants().OfType<TextBlock>().First(t => (t.Inlines?.OfType<Run>().Any(r => r.Text?.Contains("💬") == true)) == true);
                Assert.Empty(line.Inlines!.OfType<InlineUIContainer>());
                Assert.Contains(line.Inlines!.OfType<Run>(), r => r.Text == "uma palavra");
                Assert.Contains(line.Inlines!.OfType<Run>(), r => r.Text?.Contains("e mais texto depois dela.") == true);
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
