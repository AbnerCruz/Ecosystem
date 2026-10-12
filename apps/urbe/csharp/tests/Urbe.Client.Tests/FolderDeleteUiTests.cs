using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Excluir a pasta in Notas (mobile-ui.js deleteSelected → explorer.deleteFolder), through the real native UI
/// against a real folder vault.</summary>
public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// Holding a folder selects it ("1 pasta", only Mover para a lixeira enabled); confirming "Excluir a pasta “Área”?"
    /// sends its notes and its sub-folder's to the Lixeira, removes the neighbourhoods and the emptied folders, keeps a
    /// file that is not a note (and so its folder), and the map is saved without them. A folder and a note together
    /// ask "Excluir 1 pasta e 1 nota?"; a restored note comes back with its folder.
    /// </summary>
    [Fact]
    public async Task Notes_DeletingAFolderSendsItsNotesToTheTrashAndRemovesTheNeighbourhood() =>
        Assert.True(await Session.Dispatch(DeleteFolderScenario, CancellationToken.None));

    private static async Task<bool> DeleteFolderScenario()
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-folderdel-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Área", "Antigas"));
            Directory.CreateDirectory(Path.Combine(vault, "Projetos", "Fotos"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\n[[Ideias]]\n");
            File.WriteAllText(Path.Combine(vault, "Diário.md"), "# Diário\n");
            File.WriteAllText(Path.Combine(vault, "Área", "Ideias.md"), "# Ideias\n");
            File.WriteAllText(Path.Combine(vault, "Área", "Antigas", "Velha.md"), "# Velha\n");
            File.WriteAllText(Path.Combine(vault, "Projetos", "Plano.md"), "# Plano\n");
            File.WriteAllBytes(Path.Combine(vault, "Projetos", "Fotos", "foto.png"), [1, 2, 3]);
            var mapa = Path.Combine(vault, ".urbe", "mapa.json");
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            async Task Settled() { for (int i = 0; i < 60; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 1280, Height = 760, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                await UntilMapSaved(view);
                var city = view.World.City!;
                Assert.Contains(city.Regions, r => r.Name == "Antigas");
                Button Bar(string tip) => view.Explorer.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == tip);
                string Count() => view.Explorer.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text is "1 pasta" or "1 pasta · 1 nota" or "1 nota").Text!;

                // 1. hold Área → "1 pasta"; the note-only actions are disabled
                view.ShowNotes();
                Assert.True(view.Explorer.HoldFolder("Área"));
                Assert.Equal(new[] { "folder:Área" }, view.Explorer.SelectedPaths);
                Assert.Equal("1 pasta", Count());
                Assert.False(Bar("Abrir").IsEnabled);
                Assert.False(Bar("Mover para pasta").IsEnabled);
                Assert.True(Bar("Mover para a lixeira").IsEnabled);
                Bar("Mover para a lixeira").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Settled();
                var texts = view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Excluir a pasta “Área”?", texts);
                Assert.Contains("2 notas vão para a Lixeira e podem ser restauradas. A pasta também sai da cidade.", texts);
                ClickButtonWithText(view.Dialogs, "Excluir");
                await Settled();
                Assert.False(Directory.Exists(Path.Combine(vault, "Área")));
                Assert.DoesNotContain(city.Regions, r => r.Name is "Área" or "Antigas");
                Assert.DoesNotContain(city.Buildings, b => b.Path.StartsWith("Área/", StringComparison.Ordinal));
                Assert.Null(view.Documents!.Get("Área/Ideias.md"));
                var trash = File.ReadAllText(Path.Combine(vault, ".urbe", "trash.v2.json"));
                Assert.Contains("Área/Antigas/Velha.md", trash);
                Assert.Contains("Área/Ideias.md", trash);
                Assert.Empty(view.Explorer.SelectedPaths);

                // 2. Projetos (holding a picture) + Comece → "Excluir 1 pasta e 1 nota?"; the picture stays with its folder
                view.ShowNotes();
                Assert.True(view.Explorer.HoldFolder("Projetos"));
                Assert.True(view.Explorer.TryOpenNote("Comece.md"));          // a tap while selecting toggles
                Assert.Equal("1 pasta · 1 nota", Count());
                Bar("Mover para a lixeira").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Settled();
                Assert.Contains("Excluir 1 pasta e 1 nota?", view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
                ClickButtonWithText(view.Dialogs, "Excluir");
                await Settled();
                Assert.False(File.Exists(Path.Combine(vault, "Projetos", "Plano.md")));
                Assert.False(File.Exists(Path.Combine(vault, "Comece.md")));
                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(Path.Combine(vault, "Projetos", "Fotos", "foto.png")));
                Assert.Equal("# Diário\n", File.ReadAllText(Path.Combine(vault, "Diário.md")));
                Assert.Empty(city.Regions);

                await UntilMapSaved(view);
                using (var m = System.Text.Json.JsonDocument.Parse(File.ReadAllText(mapa)))
                {
                    Assert.Equal(new[] { "Diário.md" }, m.RootElement.GetProperty("notas").EnumerateObject().Select(p => p.Name));
                    Assert.Equal(0, m.RootElement.GetProperty("regioes").GetArrayLength());
                }

                // 3. Lixeira → Restaurar Velha: the note and its folder come back
                view.ShowNotes();
                view.ShowTrashView();
                Dispatcher.UIThread.RunJobs();
                var row = view.Explorer.GetLogicalDescendants().OfType<Border>()
                    .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "Área/Antigas/Velha.md"));
                ClickButtonWithText(row, "Restaurar");
                await Settled();
                Assert.Equal("# Velha\n", File.ReadAllText(Path.Combine(vault, "Área", "Antigas", "Velha.md")));
                Assert.Contains(city.Buildings, b => b.Path == "Área/Antigas/Velha.md");
                window.Close();
                return true;
            }
            finally
            {
                UrbeApp.LocalStatePath = previous;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }
}
