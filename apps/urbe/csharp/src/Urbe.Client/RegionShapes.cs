using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// Reshaping a neighbourhood, as the 1.8.4 does it: a tap on the neighbourhood (not on a house) shows its rectangle
/// with eight handles (v20EntrarShape); dragging them changes the draft (v20ResizeDraft); Aplicar applies the owner's
/// priority mask (v20SairShape → v20MascaraPrioritaria → v20AplicarMascaraRegiao). The root houses the new shape
/// covers join the folder (absorverConstrucoesDaRaiz): their files are moved into it in the vault.
/// </summary>
public sealed partial class MainView
{
    /// <summary>v20EntrarShape: the neighbourhood's rectangle and handles over the city.</summary>
    public void BeginRegionShape(LegacyCityRegion region)
    {
        if (World.City is null || region.W <= 0 || region.H <= 0)
        {
            ShowLifeNotice("Esta pasta não possui área desenhada.");
            return;
        }
        RegionShapeTool? tool = null;
        tool = new RegionShapeTool(region, (region.X, region.Y, region.W, region.H), () => LegacyWorld.Tile * World.Camera.Zoom, () =>
        {
            _toolOk.IsEnabled = tool!.Changed;
            var d = tool.Draft;
            _toolText.Text = $"Forma de “{region.Name}” · {d.W}×{d.H}";
        });
        BeginTool(tool, $"Forma de “{region.Name}” · arraste as alças", okVisible: true);
        _toolOk.Content = "Aplicar";
    }

    /// <summary>v20SairShape(true): the new shape, or nothing when the handles did not move.</summary>
    private async Task ApplyRegionShapeAsync(RegionShapeTool tool)
    {
        if (World.City is not { } city || _store is null) return;
        if (!tool.Changed) { EndTool(); return; }
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Forma da região", NoVaultMessage);
            return;
        }
        var d = tool.Draft;
        var mask = city.ResizeRegionMask(tool.Region, d.X, d.Y, d.W, d.H);
        EndTool();
        if (mask.Cells.Count == 0)
        {
            ShowLifeNotice("A alteração foi descartada: não restou terreno válido.");
            World.InvalidateVisual();
            return;
        }
        var adopted = city.ApplyRegionMask(tool.Region, mask).Where(b => b.IsNote).Select(b => b.Path).ToList();
        ScheduleRoadRebuild();
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        World.InvalidateVisual();
        if (adopted.Count > 0) await MoveNotesToAsync(adopted, city.RegionPaths()[tool.Region.Id]);
        RefreshNotes();
        ShowLifeNotice("Formato da região atualizado.");
    }
}
