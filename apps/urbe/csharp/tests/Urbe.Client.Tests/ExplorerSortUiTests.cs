using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Organizing a growing vault: Ordenar notas (Explorer ⋯) puts the notes of each folder by name, the ones
/// changed last first or the ones made last first; folders stay first; the choice is kept on the device.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Notes_SortByNameOrByTheLastChanged() => Assert.True(await Session.Dispatch(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-sort-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Pasta"));
        Directory.CreateDirectory(Path.Combine(vault, ".urbe"));
        foreach (var n in new[] { "Alfa", "Beta", "Gama" }) File.WriteAllText(Path.Combine(vault, n + ".md"), "# " + n + "\n");
        File.WriteAllText(Path.Combine(vault, ".urbe", "notas.json"),
            "{\"versao\":1,\"notas\":{" +
            "\"Alfa.md\":{\"id\":\"a\",\"criado\":\"2026-11-03\",\"modificado\":\"2026-11-04\"}," +
            "\"Beta.md\":{\"id\":\"b\",\"criado\":\"2026-11-01\",\"modificado\":\"2026-11-14\"}," +
            "\"Gama.md\":{\"id\":\"c\",\"criado\":\"2026-11-10\",\"modificado\":\"2026-11-10\"}},\"pastas\":[\"Pasta\"]}");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "s.json");
        async Task Settled() { for (int i = 0; i < 40; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 1280, Height = 800, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.ShowNotes();
            await Settled();
            Assert.Equal(["Alfa.md", "Beta.md", "Gama.md"], view.Explorer.VisibleNotePaths);

            var choosing = view.ChooseSortAsync();
            await Settled();
            ClickButtonWithText(view.Dialogs, "Modificadas por último");
            await choosing;
            await Settled();
            Assert.Equal(["Beta.md", "Gama.md", "Alfa.md"], view.Explorer.VisibleNotePaths);
            Assert.Equal("modificadas", view.ExplorerState.Sort);

            choosing = view.ChooseSortAsync();
            await Settled();
            ClickButtonWithText(view.Dialogs, "Criadas por último");
            await choosing;
            await Settled();
            Assert.Equal(["Gama.md", "Alfa.md", "Beta.md"], view.Explorer.VisibleNotePaths);
            // kept on the device
            Assert.Equal("criadas", new ExplorerState(UrbeApp.LocalStatePath).Sort);
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
