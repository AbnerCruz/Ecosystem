using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// CST-155: "Adaptar" makes a real note beside the source, linked to it, and the publication shows it; the source is
    /// untouched; a later change to the source is said in the studio until the adaptation is marked as reviewed.
    /// </summary>
    [Fact]
    public async Task Publications_AnAdaptationIsARealLinkedNoteThatNoticesItsSourceChange() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-adapt-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Bio"));
        File.WriteAllText(Path.Combine(vault, "Bio", "Fotossíntese.md"), "# Fotossíntese\n\nTexto longo da pesquisa.\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Pump() { for (int j = 0; j < 12; j++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(15); } }
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var origin = view.Documents!.Get("Bio/Fotossíntese.md")!;
            var create = view.CreatePublicationAsync(origin.Path);
            Dispatcher.UIThread.RunJobs();
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Aula";
            ClickButtonWithText(view.Dialogs, "Criar");
            await create;
            await Pump();
            var studio = view.PublicationsStudio;
            var piece = studio.Project!.Sections[0];

            var adapt = view.AdaptPublicationPieceAsync(piece);
            await Pump();
            ClickButtonWithText(view.Dialogs, "Roteiro");
            await adapt;
            await Pump();
            var file = Path.Combine(vault, "Bio", "Fotossíntese — Roteiro.md");
            Assert.True(File.Exists(file));
            Assert.Contains("adaptacao_de: \"[[Fotossíntese]]\"", File.ReadAllText(file));
            Assert.Equal("# Fotossíntese\n\nTexto longo da pesquisa.\n", File.ReadAllText(Path.Combine(vault, "Bio", "Fotossíntese.md")));
            var adaptation = view.Documents.Get("Bio/Fotossíntese — Roteiro.md")!;
            Assert.Equal(adaptation.Id, studio.Project!.Sections[0].Source.DocumentId);
            Assert.Equal(piece.Id, studio.Project.Sections[0].Id);
            Assert.Contains(studio.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Roteiro de Fotossíntese");
            Assert.DoesNotContain(studio.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("A origem mudou", StringComparison.Ordinal) == true);

            // the source changes: the studio says so; marking as reviewed only changes the adaptation's property
            view.Editor.Open(new CityNote(origin.Path, origin.Content));
            view.Editor.SetMode(EditorMode.Fonte);
            view.Editor.Editor.Text = origin.Content + "\nUm parágrafo novo.\n";
            await view.Editor.SaveAsync();
            await Pump();
            studio.Refresh();
            Assert.Contains(studio.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("A origem mudou", StringComparison.Ordinal) == true);
            ClickButtonWithText(studio, "Marcar como revisada");
            for (int j = 0; j < 20; j++) await Pump();
            Assert.DoesNotContain(studio.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("A origem mudou", StringComparison.Ordinal) == true);
            Assert.Contains("Texto longo da pesquisa.", File.ReadAllText(file));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
