using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>CST-150/151/158: real vault creation, live editorial sources and real file export.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Publications_FolderCreationSelectsRealNotesAndLegacyNewActionRoutesToPublications() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            var app = (IAssistantApp)view;
            await app.CreateNoteAsync("Bairro/Sub/Dois.md", "# Dois\n\nFonte aninhada");
            await app.CreateNoteAsync("BairroVizinho/Fora.md", "# Fora\n");
            var creating = view.CreatePublicationAsync(sourceFolder: "Bairro");
            Dispatcher.UIThread.RunJobs();
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Do bairro";
            ClickButtonWithText(view.Dialogs, "Criar");
            await creating;
            var project = view.PublicationsStudio.Project!;
            Assert.Equal(new[] { "Bairro/Sub/Dois.md", "Bairro/Um.md" },
                project.Sources.Select(s => view.Documents!.Get(s.DocumentId)!.Path));
            Assert.DoesNotContain("Fonte aninhada", File.ReadAllText(Path.Combine(vault, "Publicações", "Do bairro.publication.json")));
            Assert.False(view.Studio.IsVisible);
            view.PublicationsStudio.Close();

            // Existing shortcut ID stays valid but cannot resurrect the retired editor.
            Assert.True(view.Search.Has("publications.new"));
            Assert.True(view.Search.Execute("pages.new"));
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Dialogs.IsOpen);
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Nova publicação");
            view.Dialogs.Cancel();
            Assert.False(view.Studio.IsVisible);

            view.ShowPages();
            var create = view.Pages.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationProperties.GetName(b) == "Nova publicação");
            create.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Nova publicação");
            view.Dialogs.Cancel();
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task Publications_CreateFromNoteComposeAndExportUsingActualVaultDocuments() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-publication-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Capitulo.md"), "# Capítulo\n\nTexto ORIGINAL\n");
        File.WriteAllText(Path.Combine(vault, "Apendice.md"), "# Apêndice\n\nOutro texto\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Pump()
        {
            for (int j = 0; j < 12; j++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(15); }
        }
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var origin = view.Documents!.Get("Capitulo.md")!;
            var create = view.CreatePublicationAsync(origin.Path);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Dialogs.IsOpen);
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Livro vivo";
            ClickButtonWithText(view.Dialogs, "Criar");
            await create;
            await Pump();

            var target = Path.Combine(vault, "Publicações", "Livro vivo.publication.json");
            Assert.True(File.Exists(target));
            var studio = view.PublicationsStudio;
            Assert.True(studio.IsVisible);
            Assert.Single(studio.Project!.Sources);
            Assert.Equal(origin.Id, studio.Project.Sections[0].Source.DocumentId);
            Assert.DoesNotContain("Texto ORIGINAL", File.ReadAllText(target));

            var appendix = view.Documents.Get("Apendice.md")!;
            studio.AddSource(appendix.Id);
            await Pump();
            Assert.Equal(2, studio.Project!.Sections.Count);
            Assert.Contains(appendix.Id, studio.Project.Sources.Select(s => s.DocumentId));
            for (int j = 0; j < 80 && !File.ReadAllText(target).Contains(appendix.Id); j++) await Pump();
            Assert.Contains(appendix.Id, File.ReadAllText(target));

            (string Name, string Text)? export = null;
            view.SavePageFile = (name, bytes) =>
            {
                export = (name, System.Text.Encoding.UTF8.GetString(bytes));
                return Task.FromResult<bool?>(true);
            };
            var exportButton = studio.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationProperties.GetName(b) == "Exportar HTML");
            exportButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            for (int j = 0; j < 60 && export is null; j++) await Pump();
            Assert.NotNull(export);
            Assert.Equal("Livro vivo.html", export.Value.Name);
            Assert.Contains("Texto ORIGINAL", export.Value.Text);
            Assert.Contains("Outro texto", export.Value.Text);

            var first = studio.GetLogicalDescendants().OfType<Button>()
                .First(b => AutomationProperties.GetName(b) == "Editar nota de origem: Capitulo");
            first.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Pump();
            Assert.False(studio.IsVisible);
            Assert.Equal("Capitulo.md", view.Editor.Current?.Path);
            Assert.True(view.Editor.IsVisible);

            Assert.Equal("# Capítulo\n\nTexto ORIGINAL\n", File.ReadAllText(Path.Combine(vault, "Capitulo.md")));
            view.Editor.SetMode(EditorMode.Fonte);
            view.Editor.Editor.Text = "# Capítulo\n\nTexto REVISADO\n";
            await view.ReturnToPublicationAsync();
            await Pump();
            Assert.True(studio.IsVisible);
            Assert.Equal("# Capítulo\n\nTexto REVISADO\n", File.ReadAllText(Path.Combine(vault, "Capitulo.md")));
            Assert.DoesNotContain("Texto REVISADO", File.ReadAllText(target));
            export = null;
            exportButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            for (int j = 0; j < 60 && export is null; j++) await Pump();
            Assert.NotNull(export);
            Assert.Contains("Texto REVISADO", export.Value.Text);
            Assert.DoesNotContain("Texto ORIGINAL", export.Value.Text);
            Snapshot(window, "native-publications-live-source.png");
            window.Close();
            return true;
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
