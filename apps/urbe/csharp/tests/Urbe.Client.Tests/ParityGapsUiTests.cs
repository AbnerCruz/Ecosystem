using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// The small gaps of the 1.8.4 closed together: the rest of its commands (Avançar, Fechar aba, Restaurar sessão, Abrir
/// tema.json…), Baixar arquivo of the preview, the first-use tips (tips.js) and the Assistente in Configurações.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Parity_CommandsSessionDownloadAndTips() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-parity-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Anexos"));
            File.WriteAllText(Path.Combine(vault, "Comece.md"), "# Comece\n\nTexto.\n");
            File.WriteAllText(Path.Combine(vault, "Plano.md"), "# Plano\n\nPassos.\n");
            File.WriteAllBytes(Path.Combine(vault, "Anexos", "dados.bin"), [1, 2, 3, 4]);
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            view.Customization.Device = new MemoryDeviceStore();
            var window = new Window { Width = 1280, Height = 760, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                foreach (var id in new[] { "editor.back", "editor.forward", "editor.closeTab", "editor.undo", "editor.redo", "editor.contextPanel", "editor.session.restore",
                             "history.restore", "math.insert", "trash.restore", "customize.reset", "customize.edit-file", "ui.plugins", "plugins.reload", "tutorial.menu", "ui.settings", "ui.assistant" })
                    Assert.True(view.Search.Has(id), id);

                // Voltar and Avançar along followed links; Fechar aba (Mod+W)
                view.OpenNoteByPath("Comece.md");
                await view.Editor.NavigateAsync(new CityNote("Plano.md", File.ReadAllText(Path.Combine(vault, "Plano.md"))));
                Assert.Equal("Plano.md", view.Editor.Current!.Path);
                Assert.True(view.Search.Execute("editor.back"));
                for (int i = 0; i < 20 && view.Editor.Current!.Path != "Comece.md"; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("Comece.md", view.Editor.Current!.Path);
                Assert.True(view.Editor.CanForward);
                Assert.True(view.Search.Execute("editor.forward"));
                for (int i = 0; i < 20 && view.Editor.Current!.Path != "Plano.md"; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("Plano.md", view.Editor.Current!.Path);

                // the session: closed editor, Restaurar sessão reopens the tabs and the active note
                Assert.Equal(["Comece.md", "Plano.md"], view.Editor.Tabs);
                view.Editor.IsVisible = false;
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.RestoreSession());
                Assert.True(view.Editor.IsVisible);
                Assert.Equal("Plano.md", view.Editor.Current!.Path);
                Assert.Equal(["Comece.md", "Plano.md"], view.Editor.Tabs);
                Assert.True(view.Search.Execute("editor.closeTab"));
                for (int i = 0; i < 20 && view.Editor.Tabs.Count != 1; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal(["Comece.md"], view.Editor.Tabs);

                // the first-use tip when back in a city with notes and no link; Entendi keeps it away
                view.Editor.IsVisible = false;
                await Task.Delay(400);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("link", view.TipShown);
                ClickButtonWithText(view, "Entendi");
                Assert.Null(view.TipShown);
                view.MaybeLinkTip();
                Assert.Null(view.TipShown);

                // Baixar arquivo: the bytes of the vault file, the vault untouched
                (string Name, byte[] Bytes)? copy = null;
                view.SaveFileCopy = (n, b) => { copy = (n, b); return Task.FromResult<bool?>(true); };
                await view.DownloadFileAsync("Anexos/dados.bin");
                Assert.Equal("dados.bin", copy!.Value.Name);
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, copy.Value.Bytes);

                // Abrir tema.json: written, then in the editor
                Assert.True(view.Search.Execute("customize.edit-file"));
                for (int i = 0; i < 50 && view.Editor.Current?.Path != "Personalização/tema.json"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("Personalização/tema.json", view.Editor.Current?.Path);
                Assert.True(File.Exists(Path.Combine(vault, "Personalização", "tema.json")));
                return true;
            }
            finally
            {
                window.Close();
                try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
