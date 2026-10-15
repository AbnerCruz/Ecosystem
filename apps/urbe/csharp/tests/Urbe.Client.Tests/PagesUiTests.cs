using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>pages.home: the vault's .page.json listed in Páginas and each shown inside the Urbe as the 1.8.4 renders it.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Pages_AreListedAndShownInsideTheUrbeAsTheEngineRendersThem() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-pages-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Páginas"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\nTexto da nota com **força**.\n");
            const string page = """{"version":1,"kind":"urbe-page","meta":{"title":"Meu site","icon":"🚀"},"sections":[{"id":"h1","type":"hero","props":{"title":"Olá"}},{"id":"n1","type":"note","props":{"path":"Comece.md"}}]}""";
            File.WriteAllText(Path.Combine(vault, "Páginas", "Meu site.page.json"), page);
            var requests = new List<ContentRequest>();
            ContentViewers.Factory = (r, _) => { requests.Add(r); return Task.FromResult<Control?>(new Border { Tag = "viewer:" + r.Kind }); };
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);

                // Configurações → Páginas
                _ = view.ShowSettingsAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("Sites e páginas HTML do vault", view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
                ClickButtonWithText(view.Dialogs, "Páginas");
                for (int i = 0; i < 20 && !view.Pages.IsVisible; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var card = Assert.Single(view.Pages.Listed);
                Assert.Equal(new PageCard("Páginas/Meu site.page.json", "Meu site", "🚀", 2), card);
                Snapshot(window, "native-pages-home.png");

                // the page inside the Urbe: the HTML of the page engine, with the note's text
                // (the card opens the studio; "⋯" → Ver página shows it as it is published)
                ClickButtonWithTip(view.Pages, "Opções");
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Ver página");
                for (int i = 0; i < 40 && view.FilePreview.Shown != "viewer"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("viewer", view.FilePreview.Shown);
                Assert.Equal("Páginas/Meu site.page.json", view.FilePreview.GeneratedFrom);
                var request = Assert.Single(requests);
                Assert.Equal(VaultFileKinds.Html, request.Kind);
                var html = FilePreviewPanel.Text(request.Bytes!)!;
                Assert.Contains("<title>Meu site</title>", html);
                Assert.Contains("Texto da nota com <strong>força</strong>.", html);
                Assert.Contains("<h1>Olá</h1>", html);

                // Fonte: the HTML as text
                ClickButtonWithText(view.FilePreview, "Fonte");
                for (int i = 0; i < 20 && view.FilePreview.Shown != "text"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal(html, view.FilePreview.ShownText);
                view.FilePreview.Close();

                // the exports: Baixar HTML (a file where the person chooses), Salvar HTML no vault (next to the page), Copiar HTML
                (string Name, byte[] Bytes)? downloaded = null;
                view.SavePageFile = (n, b) => { downloaded = (n, b); return Task.FromResult<bool?>(true); };
                await view.DownloadPageHtmlAsync("Páginas/Meu site.page.json");
                Assert.Equal("Meu site.html", downloaded!.Value.Name);
                Assert.Equal(html, System.Text.Encoding.UTF8.GetString(downloaded.Value.Bytes));
                await view.SavePageHtmlInVaultAsync("Páginas/Meu site.page.json");
                Assert.Equal(html, File.ReadAllText(Path.Combine(vault, "Páginas", "Meu site.html")));
                Assert.Contains(view.World.City!.Buildings, b => b.Path == "Páginas/Meu site.html");
                await view.SavePageHtmlInVaultAsync("Páginas/Meu site.page.json");   // again: replaced, not duplicated
                Assert.Single(view.World.City!.Buildings, b => b.Path == "Páginas/Meu site.html");
                await view.CopyPageHtmlAsync("Páginas/Meu site.page.json");
                if (TopLevel.GetTopLevel(view)?.Clipboard is not null) Assert.Equal(html, view.CopiedText);

                // the palette has it too
                view.Pages.Close();
                Assert.True(view.Search.Execute("pages.home"));
                Assert.True(view.Pages.IsVisible);

                // "⋯" → Mover para a lixeira: the file leaves the vault and the list
                ClickButtonWithTip(view.Pages, "Opções");
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Mover para a lixeira");
                for (int i = 0; i < 40 && !view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Mover"); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                ClickButtonWithText(view.Dialogs, "Mover");
                for (int i = 0; i < 100 && File.Exists(Path.Combine(vault, "Páginas", "Meu site.page.json")); i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.False(File.Exists(Path.Combine(vault, "Páginas", "Meu site.page.json")));
                for (int i = 0; i < 40 && view.Pages.Listed.Count > 0; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Empty(view.Pages.Listed);
                window.Close();
                return true;
            }
            finally
            {
                ContentViewers.Factory = null;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    private static void ClickButtonWithTip(Control root, string tip)
    {
        var button = root.GetLogicalDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == tip);
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }
}
