using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
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
