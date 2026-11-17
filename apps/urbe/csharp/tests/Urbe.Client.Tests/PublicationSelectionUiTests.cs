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

    /// <summary>CST-135: a search's notes, shown and confirmed, become a publication's sources.</summary>
    [Fact]
    public async Task Publication_FromASearch() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pubsearch-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Folhas.md"), "# Folhas\n\nA fotossíntese acontece nas folhas.\n");
        File.WriteAllText(Path.Combine(vault, "Luz.md"), "# Luz\n\nFotossíntese precisa de luz.\n");
        File.WriteAllText(Path.Combine(vault, "Pedras.md"), "# Pedras\n\nNada a ver.\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Wait(Func<bool> ready) { for (int i = 0; i < 300 && !ready(); i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); } }
        async Task Type(string text, string confirm)
        {
            await Wait(() => view.Dialogs.GetLogicalDescendants().OfType<TextBox>().Any());
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = text;
            ClickButtonWithText(view.Dialogs, confirm);
            for (int i = 0; i < 5; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
        }
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var run = view.CreatePublicationFromSearchAsync();
            await Type("fotossíntese", "Buscar");
            await Wait(() => view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Criar publicação"));
            ClickButtonWithText(view.Dialogs, "Criar publicação");
            await Type("Fotossíntese", "Criar");
            await run;
            await Settle(window, view);
            var json = File.ReadAllText(Path.Combine(vault, "Publicações", "Fotossíntese.publication.json"));
            Assert.Contains(view.Documents!.Get("Folhas.md")!.Id, json);
            Assert.Contains(view.Documents!.Get("Luz.md")!.Id, json);
            Assert.DoesNotContain(view.Documents!.Get("Pedras.md")!.Id, json);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    /// <summary>CST-160: a publication from a tag offers the notes that get the tag later.</summary>
    [Fact]
    public async Task Publication_FromATagOffersNewNotesWithIt() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-pubquery-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Bolo.md"), "# Bolo\n\n#receita\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Wait(Func<bool> ready) { for (int i = 0; i < 300 && !ready(); i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var create = view.CreatePublicationAsync(tag: "receita");
            await Wait(() => view.Dialogs.GetLogicalDescendants().OfType<TextBox>().Any());
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = "Receitas";
            ClickButtonWithText(view.Dialogs, "Criar");
            await create;
            await Settle(window, view);
            await view.FlushPendingAsync();
            File.WriteAllText(Path.Combine(vault, "Pão.md"), "# Pão\n\n#receita\n");
            await view.OpenVaultAsync();
            await Settle(window, view);
            var open = view.OpenPublicationAsync("Publicações/Receitas.publication.json");
            await Wait(() => view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Acrescentar"));
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Pão") == true);
            ClickButtonWithText(view.Dialogs, "Acrescentar");
            await open;
            await Settle(window, view);
            var json = File.ReadAllText(Path.Combine(vault, "Publicações", "Receitas.publication.json"));
            Assert.Contains(view.Documents!.Get("Pão.md")!.Id, json);
            Assert.Contains("\"kind\": \"tag\"", json);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
