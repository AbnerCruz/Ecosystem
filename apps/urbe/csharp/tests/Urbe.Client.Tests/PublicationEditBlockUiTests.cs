using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>CST-156: editing a long source from a publication opens the note at the block the person picks.</summary>
    [Fact]
    public async Task Publication_EditingASourceOpensAtTheChosenBlock() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-editblock-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Longa.md"), "# Título\n\nPrimeiro.\n\nSegundo parágrafo.\n\nTerceiro alvo.\n");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                var id = view.Documents!.Get("Longa.md")!.Id;
                var edit = view.EditPublicationReferenceAsync(new PublicationSourceReference(id));
                for (int i = 0; i < 100 && !view.Dialogs.IsOpen; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
                ClickButtonWithText(view.Dialogs, "Terceiro alvo.");
                await edit;
                await Settle(window, view);
                Assert.Equal("Longa.md", view.Editor.Current?.Path);
                var text = view.Editor.Editor.Text ?? "";
                Assert.Equal(text.IndexOf("Terceiro alvo.", StringComparison.Ordinal), view.Editor.Editor.CaretIndex);
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
