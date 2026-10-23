using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>UC-63: importing documents brings their attachments (pictures, PDFs…) along, created and verified in the vault.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Import_BringsTheAttachmentsAlongAndKeepsTheOnesAlreadyThere() =>
        Assert.True(await Session.Dispatch(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                File.WriteAllBytes(Path.Combine(vault, "Bairro", "velha.png"), [7, 7]);
                await view.OpenVaultAsync();   // read again, with the picture
                await Settle(window, view);
                byte[] png = [0x89, 0x50, 0x4e, 0x47, 1, 2, 3];
                ImportSource Source(string path, byte[] bytes) => new(path, _ => ValueTask.FromResult<Stream>(new MemoryStream(bytes)));
                var selection = new ImportSelection([
                    Source("Viagem/Diário.md", "# Diário\n\n![[Viagem/foto.png]]\n"u8.ToArray()),
                    Source("Viagem/foto.png", png),
                    Source("Bairro/velha.png", [1]),
                    Source("Viagem/programa.exe", [0x4d, 0x5a])
                ]);
                var import = view.ImportSelectionAsync(selection);
                for (int i = 0; i < 200 && !view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                await Settle(window, view);
                var texts = view.Dialogs.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
                Assert.Contains(texts, t => t.Contains("1 anexo(s) (imagens, PDFs, áudio, vídeo) vêm junto.", StringComparison.Ordinal));
                ClickButtonWithText(view.Dialogs, "Continuar");
                await import;
                Assert.Equal(png, File.ReadAllBytes(Path.Combine(vault, "Viagem", "foto.png")));
                Assert.Equal("# Diário\n\n![[Viagem/foto.png]]\n", File.ReadAllText(Path.Combine(vault, "Viagem", "Diário.md")));
                Assert.Equal(new byte[] { 7, 7 }, File.ReadAllBytes(Path.Combine(vault, "Bairro", "velha.png")));   // never over a file
                Assert.False(File.Exists(Path.Combine(vault, "Viagem", "programa.exe")));
                // the Explorer lists it among the vault's files
                var files = (HashSet<string>)typeof(MainView).GetField("_attachments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(view)!;
                Assert.Contains("Viagem/foto.png", files);
                return true;
            }
            finally
            {
                window.Close();
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
