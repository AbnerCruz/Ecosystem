using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// Editing a neighbourhood, as the 1.8.4 does it (openRegionEditDialog → confirmRegion "Salvar alterações": name,
/// description and colour), from the folder's ⋯ in the Explorer and from holding the neighbourhood in the city.
/// A new name renames the real folder in the vault first (IUrbeVaultStorage.RenameFolderAsync, verified file by
/// file); only then the houses, documents, attachments, chosen looks and the map follow. Description and colour are
/// map data only. Nothing changes in memory only.
/// </summary>
public sealed partial class MainView
{
    public async Task EditRegionAsync(LegacyCityRegion region, (string Name, string Description, string Color)? answer = null)
    {
        if (World.City is not { } city || _store is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Editar região", NoVaultMessage);
            return;
        }
        var typed = answer ?? await Dialogs.RegionAsync("Editar região", region.Name, region.Description, region.Color,
            LegacyCity.RegionColors, "Salvar alterações");
        if (typed is not { } v) return;
        if (city.PlanRegionEdit(region, v.Name) is not { } plan)
        {
            await Dialogs.AlertAsync("Não foi possível renomear", "Já existe uma pasta com esse nome aqui.");
            return;
        }
        var moves = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.Equals(plan.Before, plan.After, StringComparison.Ordinal))
        {
            string Moved(string path) => plan.After + path[plan.Before.Length..];
            bool Inside(string path) => path.StartsWith(plan.Before + "/", StringComparison.Ordinal);
            if (Editor.IsVisible && Editor.Current is { } open && Inside(open.Path) && Editor.HasChanges && !await Editor.SaveAsync())
            {
                await Dialogs.AlertAsync("Não foi possível renomear", "A nota aberta tem alterações que não foram salvas no vault.");
                return;
            }
            try { await _vault!.RenameFolderAsync(plan.Before, plan.After); }
            catch (Exception ex)
            {
                await Dialogs.AlertAsync("Não foi possível renomear", ex.Message);
                return;
            }
            foreach (var doc in _store.List().Where(d => Inside(d.Path)).ToList())
            {
                var to = Moved(doc.Path);
                LegacyCity.MoveDocument(_store, doc, to);
                if (_contents.Remove(doc.Path, out var content)) _contents[to] = content;
                moves[doc.Path] = to;
            }
            if (Editor.IsVisible && Editor.Current is { } current && moves.TryGetValue(current.Path, out var renamed)) Editor.Renamed(renamed);
        }
        city.ApplyRegionEdit(region, plan, v.Description, v.Color);
        await CarryLooksAsync(moves);
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
        ShowLifeNotice("Região “" + region.Name + "” atualizada.");
    }
}
