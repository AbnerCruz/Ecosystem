using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // CST-129: "demora para salvar e sair do editor"

    [Fact]
    public async Task Leave_ANoteOnlyReadLeavesTheAreaAsItWas() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, _) = await OpenRealVaultAsync(("Casa/Lista.md", "# Lista\n\n- pão\n"), ("Trabalho/Plano.md", "# Plano\n"));
        try
        {
            TextBlock Box(string name = "Casa") => view.Workspace.GetLogicalDescendants().OfType<TextBlock>().First(t => t.Text == name);
            var before = Box();
            var other = Box("Trabalho");
            view.OpenNoteByPath("Casa/Lista.md");
            await Settle(window, view);
            await view.Editor.BackAsync();
            await Settle(window, view);
            Assert.Same(before, Box());   // nothing a card shows changed: the Área de trabalho was not built again

            view.OpenNoteByPath("Casa/Lista.md");
            await Settle(window, view);
            view.Editor.Editor.Text = "# Lista\n\n- café\n- pão\n";
            await view.Editor.BackAsync();
            await Settle(window, view);
            Assert.NotSame(before, Box());   // an edit shows on the cards
            Assert.Same(other, Box("Trabalho"));   // only on its project's box
            Assert.Contains(view.Workspace.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("café") == true);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch { } }
    }, CancellationToken.None));
}
