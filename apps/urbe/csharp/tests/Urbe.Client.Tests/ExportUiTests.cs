using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>app.js exportarVault from Configurações and Notas ⋯: "Exportar tudo (.zip)" against a real folder vault.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Export_ZipsTheWholeVaultLikeTheOriginal() =>
        Assert.True(await Session.Dispatch(ExportScenario, CancellationToken.None));

    private static async Task<bool> ExportScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-export-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "Minha Cidade");
        Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
        Directory.CreateDirectory(Path.Combine(vault, ".urbe"));
        File.WriteAllText(Path.Combine(vault, "Casa.md"), "# Casa\n\n[[Bairro/Um]]\n");
        File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n");
        File.WriteAllBytes(Path.Combine(vault, "Bairro", "foto.png"), [137, 80, 78, 71, 1, 2, 3]);
        File.WriteAllText(Path.Combine(vault, ".urbe", "journal.v2.json"), "{}");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            // v23VaultName: Disco.cidade, the vault folder's name
            Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Minha Cidade");
            byte[]? zipped = null;
            view.SaveExport = bytes => { zipped = bytes; return Task.FromResult<bool?>(true); };

            static List<string?> Menu(MainView v) => v.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            _ = view.ShowNotesMenuAsync();
            Dispatcher.UIThread.RunJobs();
            var notes = Menu(view);
            Assert.True(notes.IndexOf("Importar arquivos") < notes.IndexOf("Exportar tudo (.zip)")
                && notes.IndexOf("Exportar tudo (.zip)") < notes.IndexOf("Lixeira")
                && notes.IndexOf("Lixeira") < notes.IndexOf("Pasta do Urbe no dispositivo"), string.Join(" / ", notes));
            window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
            for (int i = 0; i < 20 && view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }

            _ = view.ShowSettingsAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Cópia de segurança de todas as notas", Menu(view));
            window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
            for (int i = 0; i < 20 && view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }

            // an unsaved edit is written before the export, like exportarVault's flush
            view.OpenNoteByPath("Casa.md");
            Dispatcher.UIThread.RunJobs();
            view.Editor.Editor.Text = "# Casa\n\nEditado.\n\n[[Bairro/Um]]\n";
            await view.ExportAsync();
            Assert.Equal("# Casa\n\nEditado.\n\n[[Bairro/Um]]\n", File.ReadAllText(Path.Combine(vault, "Casa.md")));
            Assert.NotNull(zipped);
            using var zip = new ZipArchive(new MemoryStream(zipped!));
            var names = zip.Entries.Select(e => e.FullName).ToHashSet();
            Assert.Contains("Casa.md", names);
            Assert.Contains("Bairro/Um.md", names);
            Assert.Contains("Bairro/foto.png", names);                // attachments too
            Assert.Contains("urbe-export.json", names);               // the manifest
            Assert.DoesNotContain(".urbe/journal.v2.json", names);    // never the journals
            using (var r = new StreamReader(zip.GetEntry("Casa.md")!.Open()))
                Assert.Equal("# Casa\n\nEditado.\n\n[[Bairro/Um]]\n", r.ReadToEnd());
            Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Vault completo exportado.");

            // a failure says so and writes nothing
            view.SaveExport = _ => throw new IOException("disco cheio");
            await view.ExportAsync();
            Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Falhou ao exportar: disco cheio");
            window.Close();
            return true;
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
