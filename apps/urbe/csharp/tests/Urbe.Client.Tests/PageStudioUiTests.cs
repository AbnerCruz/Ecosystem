using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>The page studio through the native UI against a real folder vault: Nova página, blocks, fields, undo, JSON and saving.</summary>
public sealed partial class HeadlessUiTests
{
    private static async Task Pump(int ms = 120)
    {
        for (int i = 0; i < ms / 20; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(20); }
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task Studio_NewPageAddsABlockEditsUndoesAndSavesInTheVault() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                view.ShowPages();
                Dispatcher.UIThread.RunJobs();
                var create = view.Pages.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Nova página");
                create.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                for (int i = 0; i < 100 && view.Studio.Path is null; i++) await Pump(20);
                Assert.Equal("Páginas/Nova página.page.json", view.Studio.Path);
                var file = Path.Combine(vault, "Páginas", "Nova página.page.json");
                Assert.True(File.Exists(file));
                var st = view.Studio.Model!;
                Assert.Single(st.Sections);
                Assert.Equal("free", (string?)st.Sections[0]!["type"]);
                Assert.Contains("Nova página", view.Studio.LastHtml);
                Snapshot(window, "native-studio-new-page.png");

                // the preview's messages (the 1.8.4 preview script): tapping a part selects it; text written in it is kept
                var secId = (string)st.Sections[0]!["id"]!;
                var headId = (string)st.Get("sections.0.props.root.children.0.id")!;
                view.Studio.PreviewMessage("{\"urbePage\":\"select\",\"id\":\"" + secId + "\",\"nid\":\"" + headId + "\"}");
                Assert.Equal(secId, st.Selected);
                Assert.Equal(headId, st.NodeSelected);
                view.Studio.PreviewMessage("{\"urbePage\":\"text\",\"id\":\"" + secId + "\",\"nid\":\"" + headId + "\",\"text\":\"Meu\\ntítulo\"}");
                Assert.Equal("Meu título", (string?)st.Get("sections.0.props.root.children.0.content.text"));
                Assert.Contains("data-sid=", view.Studio.LastHtml);

                // ＋ → Blocos → Capa: a hero after the selected section, then edited in Editar
                view.Studio.OpenLibrary();
                Dispatcher.UIThread.RunJobs();
                var hero = view.Studio.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Capa");
                hero.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Pump();
                Assert.Equal(2, st.Sections.Count);
                Assert.Equal("hero", (string?)st.SelectedSection!["type"]);
                var title = view.Studio.GetLogicalDescendants().OfType<TextBox>().First(t => AutomationProperties.GetName(t) == "Título");
                title.Text = "Bem-vindo ao meu site";
                await Pump();
                Snapshot(window, "native-studio-edit.png");
                await view.Studio.FlushAsync();
                Assert.Contains("Bem-vindo ao meu site", File.ReadAllText(file));
                for (int i = 0; i < 40 && !view.Studio.LastHtml.Contains("Bem-vindo ao meu site", StringComparison.Ordinal); i++) await Pump(20);
                Assert.Contains("Bem-vindo ao meu site", view.Studio.LastHtml);

                // a ready theme, then Desfazer twice brings back the hero's default title
                var papel = view.Studio.GetLogicalDescendants().OfType<Button>().FirstOrDefault(b => AutomationProperties.GetName(b) == "Papel");
                if (papel is null)
                {
                    var tab = view.Studio.GetLogicalDescendants().OfType<Button>().First(b => (b.Tag as string) == "theme");
                    tab.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    Dispatcher.UIThread.RunJobs();
                    papel = view.Studio.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Papel");
                }
                papel.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Pump();
                Assert.Equal("papel", (string?)st.Theme["preset"]);
                ClickButtonWithTip(view.Studio, "Desfazer");
                ClickButtonWithTip(view.Studio, "Desfazer");
                await Pump();
                Assert.Equal("aurora", (string?)st.Theme["preset"]);
                Assert.NotEqual("Bem-vindo ao meu site", (string?)st.Get("sections.1.props.title"));

                // JSON: with errors nothing changes; valid JSON replaces the page and is saved
                view.Studio.OpenJson();
                Dispatcher.UIThread.RunJobs();
                view.Studio.JsonEditor!.Text = "{\"sections\":[{\"type\":\"nao-existe\"}]}";
                ClickButtonWithText(view.Studio, "Aplicar");
                Assert.Equal("Corrija os erros antes de aplicar.", view.Studio.LastToast);
                Assert.Equal(2, st.Sections.Count);
                view.Studio.JsonEditor!.Text = "{\"meta\":{\"title\":\"Citações\"},\"sections\":[{\"type\":\"quote\",\"props\":{\"text\":\"Uma frase\"}}]}";
                ClickButtonWithText(view.Studio, "Aplicar");
                await Pump();
                Assert.Single(st.Sections);
                await view.Studio.FlushAsync();
                Assert.Contains("Uma frase", File.ReadAllText(file));
                Assert.Equal("Salvo", view.Studio.Status);

                // Exportar → Imprimir ou salvar PDF: the export HTML goes to the system's print
                (string Title, string Html)? printed = null;
                var before = ContentViewers.Printer;
                ContentViewers.Printer = (t, h) => { printed = (t, h); return Task.CompletedTask; };
                try { await view.PrintPageAsync("Páginas/Nova página.page.json"); }
                finally { ContentViewers.Printer = before; }
                Assert.Equal("Nova página", printed!.Value.Title);
                Assert.Contains("Uma frase", printed.Value.Html);

                // Voltar: the page is listed with its new title
                await view.Studio.CloseAsync();
                Assert.Null(view.Studio.Path);
                Assert.Contains(view.Pages.Listed, p => p.Path == "Páginas/Nova página.page.json" && p.Title == "Citações");
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Compositions_BecomePagesWithTheSameNotesAndTheFileIsKept() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-cmp-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, ".urbe"));
            File.WriteAllText(Path.Combine(vault, "Capítulo.md"), "# Capítulo\n\nTexto do capítulo.\n");
            File.WriteAllText(Path.Combine(vault, ".urbe", "mapa.json"), "{\"v\":4,\"mundo\":\"placas-1\",\"notas\":{\"Capítulo.md\":{\"id\":\"doc_cap\"}}}");
            var compositions = "{\"version\":2,\"items\":[{\"id\":\"cmp_livro\",\"name\":\"Meu livro\",\"sources\":[\"doc_cap\"],\"theme\":\"clean\",\"customCSS\":\"h1{color:red}\"}]}";
            File.WriteAllText(Path.Combine(vault, ".urbe", "compositions.v2.json"), compositions);
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.ShowPages();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, view.Pages.PendingCompositions);
                var convert = view.Pages.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Converter em páginas");
                var flow = view.ConvertCompositionsAsync();
                await RunWithDialog(flow, async () =>
                {
                    ClickButtonWithText(view.Dialogs, "Converter");
                    await Pump(200);
                });
                var page = Path.Combine(vault, "Páginas", "Composições", "Meu livro.page.json");
                Assert.True(File.Exists(page));
                var text = File.ReadAllText(page);
                var json = System.Text.Json.Nodes.JsonNode.Parse(text)!;
                Assert.Equal("Capítulo.md", (string?)json["sections"]![0]!["props"]!["path"]);
                Assert.Contains("cmp_livro", text);
                Assert.Equal(compositions, File.ReadAllText(Path.Combine(vault, ".urbe", "compositions.v2.json")));
                Assert.Equal(0, view.Pages.PendingCompositions);
                Assert.Contains(view.Pages.Listed, p => p.Title == "Meu livro");
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}

/// <summary>UC-35: a section dragged by its grip goes where it is let go (the others make room); a removed one comes back with Desfazer.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Studio_DragsASectionAndUndoesARemoval() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                view.ShowPages();
                Dispatcher.UIThread.RunJobs();
                var create = view.Pages.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Nova página");
                create.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                for (int i = 0; i < 100 && view.Studio.Path is null; i++) await Pump(20);
                var st = view.Studio.Model!;
                view.Studio.OpenJson();
                Dispatcher.UIThread.RunJobs();
                view.Studio.JsonEditor!.Text = "{\"sections\":[{\"type\":\"quote\",\"props\":{\"text\":\"A\"}},{\"type\":\"quote\",\"props\":{\"text\":\"B\"}},{\"type\":\"quote\",\"props\":{\"text\":\"C\"}}]}";
                ClickButtonWithText(view.Studio, "Aplicar");
                await Pump();
                string Order() => string.Concat(st.Sections.Select(s => (string?)s!["props"]!["text"]));
                Assert.Equal("ABC", Order());
                await Pump();
                window.UpdateLayout();

                // the grip of A, two rows down: B and C move up while dragging, then A is last
                var grips = view.Studio.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == "⋮⋮").ToList();
                Assert.Equal(3, grips.Count);
                var cards = grips.Select(g => g.FindAncestorOfType<Border>(true)!).Select(b => b.Tag as string == "section" ? b : b.FindAncestorOfType<Border>()!).ToList();
                var from = grips[0].TranslatePoint(new Avalonia.Point(grips[0].Bounds.Width / 2, grips[0].Bounds.Height / 2), window)!.Value;
                double rowStep = cards[1].TranslatePoint(default, window)!.Value.Y - cards[0].TranslatePoint(default, window)!.Value.Y;
                Assert.True(rowStep > 10);
                window.MouseDown(from, Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
                for (int k = 1; k <= 6; k++)
                    window.MouseMove(from + new Avalonia.Point(0, rowStep * 2.1 * k / 6), Avalonia.Input.RawInputModifiers.LeftMouseButton);
                Dispatcher.UIThread.RunJobs();
                Assert.All(cards.Skip(1), c => Assert.IsType<Avalonia.Media.TranslateTransform>(c.RenderTransform));
                window.MouseUp(from + new Avalonia.Point(0, rowStep * 2.1), Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.None);
                await Pump();
                Assert.Equal("BCA", Order());

                // Delete on the selected section: removed, with Desfazer in the notice
                var a = (string)st.Sections[2]!["id"]!;
                st.Select(a);
                typeof(PageStudioView).GetMethod("Remove", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(view.Studio, [a]);
                await Pump();
                Assert.Equal("BC", Order());
                Assert.EndsWith("removida.", view.Studio.LastToast);
                var undo = view.Studio.GetLogicalDescendants().OfType<Border>().Single(b => b.Tag as string == "toast")
                    .GetLogicalDescendants().OfType<Button>().Single();
                undo.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Pump();
                Assert.Equal("BCA", Order());
                await view.Studio.FlushAsync();
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}

/// <summary>UC-62 slice 2: "Criar página daqui" — a page from a folder follows its notes; a page from a note shows it.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Studio_CreatesAPageFromAFolderAndFromANote() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                await view.CreatePageFromAsync(folder: "Bairro");
                for (int i = 0; i < 100 && view.Studio.Path is null; i++) await Pump(20);
                Assert.Equal("Páginas/Bairro.page.json", view.Studio.Path);
                var json = File.ReadAllText(Path.Combine(vault, "Páginas", "Bairro.page.json"));
                Assert.Contains("\"type\": \"notes\"", json);   // "source": "folder" is the default, left out of the file
                Assert.Contains("\"folder\": \"Bairro\"", json);
                for (int i = 0; i < 60 && !view.Studio.LastHtml.Contains("<h3>Um</h3>", StringComparison.Ordinal); i++) await Pump(20);
                Assert.Contains("<h3>Um</h3>", view.Studio.LastHtml);
                // slice 4: the cover's title written in place in the preview; a path that is not a plain text is ignored
                Assert.Contains("data-path=\"sections.0.props.title\"", view.Studio.LastHtml);
                view.Studio.PreviewMessage("{\"urbePage\":\"field\",\"path\":\"sections.0.props.title\",\"text\":\"Meu  bairro\"}");
                Assert.Equal("Meu bairro", (string?)view.Studio.Model!.Get("sections.0.props.title"));
                view.Studio.PreviewMessage("{\"urbePage\":\"field\",\"path\":\"meta.title\",\"text\":\"X\"}");
                view.Studio.PreviewMessage("{\"urbePage\":\"field\",\"path\":\"sections.1.props.source\",\"text\":\"tag\"}");
                Assert.Equal("Bairro", (string?)view.Studio.Model!.Meta["title"]);
                await view.Studio.FlushAsync();
                Assert.Contains("Meu bairro", File.ReadAllText(Path.Combine(vault, "Páginas", "Bairro.page.json")));
                await view.Studio.CloseAsync();

                await view.CreatePageFromAsync(notePath: "Comece.md");
                for (int i = 0; i < 100 && view.Studio.Path != "Páginas/Comece.page.json"; i++) await Pump(20);
                Assert.Equal("Páginas/Comece.page.json", view.Studio.Path);
                Assert.Contains("\"path\": \"Comece.md\"", File.ReadAllText(Path.Combine(vault, "Páginas", "Comece.page.json")));
                await view.Studio.CloseAsync();

                // a second page from the same folder never writes over the first
                await view.CreatePageFromAsync(folder: "Bairro");
                for (int i = 0; i < 100 && view.Studio.Path is null; i++) await Pump(20);
                Assert.NotEqual("Páginas/Bairro.page.json", view.Studio.Path);
                Assert.True(File.Exists(Path.Combine(vault, "Páginas", "Bairro.page.json")));
                await view.Studio.CloseAsync();
                return true;
            }
            finally
            {
                window.Close();
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
