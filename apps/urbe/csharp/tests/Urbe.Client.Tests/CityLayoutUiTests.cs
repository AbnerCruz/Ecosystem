using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>city.reorganize / city.undoReorganize: a backup of the map first, then the layout; the notes never move in the vault.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Reorganize_BacksUpTheMapReorganizesAndUndoes() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-reorg-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Projetos", "Antigos"));
            Directory.CreateDirectory(Path.Combine(vault, "Área"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\nVeja [[Plano]] e [[Ideias]].\n");
            File.WriteAllText(Path.Combine(vault, "Projetos", "Plano.md"), "# Plano\n\n[[Rascunho]]\n");
            File.WriteAllText(Path.Combine(vault, "Projetos", "Antigos", "Rascunho.md"), "# Rascunho\n");
            File.WriteAllText(Path.Combine(vault, "Área", "Ideias.md"), "# Ideias\n");
            var notes = Directory.GetFiles(vault, "*.md", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllText);
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Avalonia.Controls.Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                await view.FlushMapAsync();
                var city = view.World.City!;
                string Layout() => string.Join(";", city.Regions.Select(r => $"{r.Id}:{r.X},{r.Y},{r.W},{r.H}")) + "|" +
                                   string.Join(";", city.Buildings.OrderBy(b => b.Id, StringComparer.Ordinal).Select(b => $"{b.Id}:{b.X},{b.Y},{b.RegionId}"));
                var before = Layout();
                Assert.False(view.CanUndoReorganize);
                Assert.False(view.Search.Execute("city.undoReorganize"));   // nothing to undo yet

                // Configurações lists it, with the 1.8.4's text
                _ = view.ShowSettingsAsync();
                Dispatcher.UIThread.RunJobs();
                var options = view.Dialogs.GetLogicalDescendants().OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Organizar os bairros", options);
                Assert.Contains("Redesenha pastas e subpastas e reagrupa as casas", options);
                window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
                Dispatcher.UIThread.RunJobs();
                Assert.False(view.Dialogs.IsOpen);

                await view.ReorganizeAsync(confirmed: true);
                Assert.True(view.CanUndoReorganize);
                Assert.NotEqual(before, Layout());
                Assert.Empty(city.ValidateDistricts());

                // the backup: .urbe/backup/<stamp>-layout-layout/ with the copy and its manifest (backup.js format)
                var backups = Directory.GetDirectories(Path.Combine(vault, ".urbe", "backup"));
                var backup = Assert.Single(backups);
                Assert.EndsWith("-layout-layout", backup);
                var copy = File.ReadAllBytes(Path.Combine(backup, "files", "urbe", "mapa.json"));
                using (var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(backup, "manifest.json"))))
                {
                    var m = manifest.RootElement;
                    Assert.Equal(1, m.GetProperty("version").GetInt32());
                    Assert.Equal("layout", m.GetProperty("from").GetString());
                    Assert.Equal("reorganizar", m.GetProperty("reason").GetString());
                    var f = Assert.Single(m.GetProperty("files").EnumerateArray().ToList());
                    Assert.Equal(".urbe/mapa.json", f.GetProperty("path").GetString());
                    Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(copy)), f.GetProperty("sha256").GetString());
                }
                using (var saved = JsonDocument.Parse(copy))
                    Assert.True(saved.RootElement.GetProperty("notas").EnumerateObject().Any());

                // the reorganized map is written; the notes are the same files with the same text
                await view.FlushMapAsync();
                Assert.Equal("saved", view.MapSave!.State);
                foreach (var (path, text) in notes) Assert.Equal(text, File.ReadAllText(path));

                // Desfazer reorganização (palette): the layout of before
                Assert.True(view.Search.Execute("city.undoReorganize"));
                Assert.Equal(before, Layout());
                Assert.False(view.CanUndoReorganize);
                await view.FlushMapAsync();
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
