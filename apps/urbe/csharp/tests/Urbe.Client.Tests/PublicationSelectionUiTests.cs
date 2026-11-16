using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-135: the selected paragraph becomes a publication's passage, through a ^id marked in the note.</summary>
    [Fact]
    public async Task Publication_FromTheSelectedParagraph() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pubsel-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Livro.md"), "# Livro\n\nPrimeiro parágrafo.\n\nO trecho escolhido fica aqui.\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Wait(Func<bool> ready) { for (int i = 0; i < 300 && !ready(); i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.OpenNoteByPath("Livro.md");
            await Settle(window, view);
            view.Editor.SetMode(EditorMode.Fonte);
            var text = view.Editor.Editor.Text!;
            view.Editor.Editor.SelectionStart = text.IndexOf("trecho escolhido", StringComparison.Ordinal);
            view.Editor.Editor.SelectionEnd = view.Editor.Editor.SelectionStart + "trecho escolhido".Length;
            var run = view.CreatePublicationFromSectionAsync("Livro.md");
            await Wait(() => view.Dialogs.IsOpen);
            ClickButtonWithText(view.Dialogs, "O parágrafo selecionado");
            await Wait(() => view.Dialogs.GetLogicalDescendants().OfType<TextBox>().Any());
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Trecho";
            ClickButtonWithText(view.Dialogs, "Criar");
            await run;
            await Settle(window, view);
            var note = File.ReadAllText(Path.Combine(vault, "Livro.md"));
            var id = System.Text.RegularExpressions.Regex.Match(note, @"fica aqui\. \^(pub-[0-9a-f]{6})").Groups[1].Value;
            Assert.NotEmpty(id);
            Assert.Contains("Primeiro parágrafo.\n", note);
            Assert.Contains("\"^" + id + "\"", File.ReadAllText(Path.Combine(vault, "Publicações", "Trecho.publication.json")));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
