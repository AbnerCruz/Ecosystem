using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-066: "conferir referências dos grafos a arquivos e pastas quando esses elementos forem renomeados, movidos ou
/// excluídos". A rename or a move in the Urbe is followed by every graph of the vault (written and verified); a card
/// whose note was deleted says it is missing instead of looking like any other.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Graphs_FollowRenamesAndMovesAndShowWhatIsMissing() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var graph = GraphDocument.Create("Mapa");
        var plan = graph.AddReference(GraphNodeKind.Note, "Plano.md", 0, 0);
        var lore = graph.AddReference(GraphNodeKind.Note, "Droerni/Lore.md", 0, 140);
        var folder = graph.AddReference(GraphNodeKind.Folder, "Droerni", 0, 280);
        var picture = graph.AddReference(GraphNodeKind.File, "mapa.png", 260, 0);
        var (view, window, dir, vault) = await OpenRealVaultAsync(("Plano.md", "# Plano\n"), ("Droerni/Lore.md", "# Lore\n"),
            ("Mundos/.keep.md", "# Mundos\n"), ("mapa.png", "png"), ("Mapa.grafo.json", graph.Serialize()));
        var graphFile = Path.Combine(vault, "Mapa.grafo.json");
        GraphDocument OnDisk() => GraphDocument.Parse(File.ReadAllText(graphFile));
        try
        {
            await view.RenameNoteAsync("Plano.md", "Plano final");
            await Settle(window, view);
            var after = OnDisk().Node(plan.Id)!;
            Assert.Equal(("Plano final.md", "Plano final.md"), (after.Target, after.Label));

            await view.RenameAttachmentAsync("mapa.png", "mapa do mundo.png");
            await Settle(window, view);
            Assert.Equal(("mapa do mundo.png", "mapa do mundo.png"), (OnDisk().Node(picture.Id)!.Target, OnDisk().Node(picture.Id)!.Label));

            Assert.True(await view.MoveFolderAsync("Droerni", "Mundos"));
            await Settle(window, view);
            Assert.Equal("Mundos/Droerni/Lore.md", OnDisk().Node(lore.Id)!.Target);
            Assert.Equal("Mundos/Droerni", OnDisk().Node(folder.Id)!.Target);

            // the graph open: its cards follow too, and a deleted note shows as missing
            await view.OpenGraphAsync("Mapa.grafo.json");
            await Settle(window, view);
            var canvas = view.GraphsPanel.Canvas;
            Assert.False(canvas.IsMissing(OnDisk().Node(plan.Id)!));
            Assert.True(await view.DeleteNoteAsync("Plano final.md", confirmed: true));
            await Settle(window, view);
            Assert.True(canvas.IsMissing(view.OpenGraph!.Node(plan.Id)!));
            Assert.False(canvas.IsMissing(view.OpenGraph!.Node(lore.Id)!));
            Assert.False(canvas.IsMissing(view.OpenGraph!.Node(folder.Id)!));
            Snapshot(window, "native-graph-missing.png");
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
