using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Organizing a vault that grows with images, PDFs and audio: files join a selection with notes ("Selecionar"
/// in a file's menu, then taps), move with them to a folder, and are deleted with a question that says they have no
/// Lixeira.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Notes_FilesAreSelectedMovedAndDeletedWithNotes() => Assert.True(await Session.Dispatch(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-selfiles-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Fotos"));
        File.WriteAllText(Path.Combine(vault, "Viagem.md"), "# Viagem\n\n![[praia.png]]\n");
        File.WriteAllBytes(Path.Combine(vault, "praia.png"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(vault, "mapa.pdf"), [4, 5, 6]);
        File.WriteAllBytes(Path.Combine(vault, "lixo1.png"), [7]);
        File.WriteAllBytes(Path.Combine(vault, "lixo2.png"), [8]);
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "s.json");
        async Task Settled() { for (int i = 0; i < 60; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        Button Bar(MainView v, string tip) => v.Explorer.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == tip);
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.ShowNotes();
            await Settled();

            // a file starts the selection; a note and another file join it with taps
            Assert.True(view.Explorer.SelectFile("praia.png"));
            Assert.True(view.Explorer.TryOpenNote("Viagem.md"));
            Assert.True(view.Explorer.TryOpenFile("mapa.pdf"));
            Assert.False(view.FilePreview.IsVisible);   // a tap while selecting chooses, it does not open
            Assert.Contains(view.Explorer.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "1 nota · 2 arquivos");
            Assert.True(Bar(view, "Mover para pasta").IsEnabled);
            Assert.False(Bar(view, "Renomear").IsEnabled);
            Bar(view, "Mover para pasta").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settled();
            ClickButtonWithText(view.Dialogs, "Fotos");
            await Settled();
            Assert.True(File.Exists(Path.Combine(vault, "Fotos", "Viagem.md")));
            Assert.True(File.Exists(Path.Combine(vault, "Fotos", "praia.png")));
            Assert.True(File.Exists(Path.Combine(vault, "Fotos", "mapa.pdf")));
            Assert.False(File.Exists(Path.Combine(vault, "praia.png")));

            // Selecionar tudo takes the files shown too
            view.ShowNotes();
            await Settled();
            Assert.True(view.Explorer.SelectFile("lixo1.png"));
            view.Explorer.SelectAllShown();
            Assert.Contains("lixo2.png", view.Explorer.SelectedPaths);
            view.Explorer.ClearSelection();

            // two files deleted together: the question says they do not go to the Lixeira
            view.ShowNotes();
            await Settled();
            Assert.True(view.Explorer.SelectFile("lixo1.png"));
            Assert.True(view.Explorer.TryOpenFile("lixo2.png"));
            Bar(view, "Mover para a lixeira").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settled();
            var texts = view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
            Assert.Contains("Excluir 2 arquivos?", texts);
            Assert.Contains(texts, t => t.Contains("não vão para a Lixeira"));
            ClickButtonWithText(view.Dialogs, "Excluir");
            await Settled();
            Assert.False(File.Exists(Path.Combine(vault, "lixo1.png")));
            Assert.False(File.Exists(Path.Combine(vault, "lixo2.png")));
            Assert.Empty(view.Explorer.SelectedPaths);
            return true;
        }
        finally
        {
            window.Close();
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch { }
        }
    }, CancellationToken.None));
}
