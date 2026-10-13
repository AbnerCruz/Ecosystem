using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>markChanged → scheduleRoadRebuild: a [[link]] typed in the editor becomes a street, before any save.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_ALinkTypedBecomesAStreet() =>
        Assert.True(await Session.Dispatch(RoadsScenario, CancellationToken.None));

    private static async Task<bool> RoadsScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-roads-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "A.md"), "# A\n");
        File.WriteAllText(Path.Combine(vault, "B.md"), "# B\n");
        File.WriteAllText(Path.Combine(vault, "C.md"), "# C\n\n[[A]]\n");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            var city = view.World.City!;
            Assert.Single(city.Links);
            view.OpenNoteByPath("B.md");
            Dispatcher.UIThread.RunJobs();
            view.Editor.SetMode(EditorMode.Fonte);
            Dispatcher.UIThread.RunJobs();
            view.Editor.Editor.Text = "# B\n\nVer [[A]].\n";
            for (int i = 0; i < 40 && city.Links.Count < 2; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal(2, city.Links.Count);                         // a new street, from the text being typed
            Assert.True(city.Roads.Count > 0);

            // Mais ações → Estrutura e backlinks: the outline of the text and the notes that link here; a backlink opens
            view.OpenNoteByPath("A.md");
            Dispatcher.UIThread.RunJobs();
            view.Editor.Editor.Text = "# A\n\n## Parte\n\ntexto\n";
            view.ShowEditorContext();
            Dispatcher.UIThread.RunJobs();
            var panel = view.Editor.Context;
            Assert.True(panel.IsVisible);
            Assert.Equal(new[] { "A", "Parte", "C" }, panel.Items.ToArray());
            panel.GetLogicalDescendants().OfType<Button>().First(b => (b.Content as TextBlock)?.Text == "Parte")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.False(panel.IsVisible);
            Assert.Equal("# A\n\n".Length, view.Editor.Editor.CaretIndex);
            view.ShowEditorContext();
            panel.GetLogicalDescendants().OfType<Button>().First(b => (b.Content as TextBlock)?.Text == "C")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            // a followed backlink saves the open note first (DEC-0045, UC-37), then opens the other
            for (int i = 0; i < 200 && view.Editor.Current?.Path != "C.md"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal("C.md", view.Editor.Current!.Path);
            Assert.Equal(new[] { "C", "Sem backlinks." }.Take(1), panel.Items.Take(1));

            // Mostrar nas notas: Notas over the open editor
            view.ShowNotesOverEditor();
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Explorer.IsVisible && view.Editor.IsVisible && view.Explorer.ZIndex > view.Editor.ZIndex);
            window.Close();
            return true;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
