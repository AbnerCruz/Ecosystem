using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// Moving a house in the city, as the 1.8.4 does it: holding it 430 ms and dragging it to another lot
/// (v21MoveHold → moveBuilding → finalizarMovimentoMundo), and the house panel's Mover (moveBtn →
/// escolherDestinoEMover → moverArquivosParaRegiao). When the house changes neighbourhood its note changes folder:
/// the file is moved in the vault first, and only then the house, its document and the map follow. Nothing moves
/// in memory only.
/// </summary>
public sealed partial class MainView
{
    /// <summary>The house panel's Mover: the folder, then a free lot in it (vagaNaRegiao), the file moved with it.</summary>
    public async Task MoveHouseToFolderAsync(LegacyCityBuilding house, string? folder = null)
    {
        if (World.City is not { } city || _store is null || !house.IsNote) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Mover", NoVaultMessage);
            return;
        }
        folder ??= await ChooseFolderAsync();
        if (folder is null) return;
        var region = folder.Length > 0 ? city.RegionByPath(folder) : null;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (city.PlanMove(house, region, now, house.Path) is not { } plan)
        {
            ShowLifeNotice("Sem espaço construível no destino.");
            return;
        }
        if (!await MoveFileOfAsync(house, plan.Path)) return;
        city.ApplyMove(house, region, plan, now);
        AfterHouseMoved(house, region);
        World.CenterOn(house);
    }

    /// <summary>finalizarMovimentoMundo: the carried house let go at (x, y).</summary>
    public async Task<bool> MoveHouseAsync(LegacyCityBuilding house, int x, int y)
    {
        if (World.City is not { } city || _store is null) return false;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Mover", NoVaultMessage);
            return false;
        }
        var (ok, region) = city.ValidMove(house, x, y);
        if (!ok)
        {
            ShowLifeNotice("Lote inválido para mover a construção.");
            return false;
        }
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (region?.Id != house.RegionId)
        {
            if (!house.IsNote)
            {
                ShowLifeNotice("Mover anexos para outra pasta ainda não está no app nativo.");
                return false;
            }
            var folder = region is not null ? city.RegionPaths().GetValueOrDefault(region.Id, region.Name) : "";
            var target = _store.Get(house.Path) is { } doc ? LegacyCity.FolderMovePath(_store, doc, folder) : house.Path;
            if (!await MoveFileOfAsync(house, target)) return false;
            city.ApplyMove(house, region, (x, y, target), now);
        }
        else city.MoveBuildingTo(house, x, y, now);
        AfterHouseMoved(house, region);
        return true;
    }

    /// <summary>The note's file moved to <paramref name="to"/> in the vault, then its text, document and look follow.</summary>
    private async Task<bool> MoveFileOfAsync(LegacyCityBuilding house, string to)
    {
        var from = house.Path;
        if (string.Equals(from, to, StringComparison.Ordinal)) return true;
        if (Editor.IsVisible && Editor.Current?.Path == from && Editor.HasChanges && !await Editor.SaveAsync())
        {
            await Dialogs.AlertAsync("Não foi possível mover", "A nota tem alterações que não foram salvas no vault.");
            return false;
        }
        try { await _vault!.MoveTextFileAsync(from, to); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível mover", ex.Message);
            return false;
        }
        if (_contents.Remove(from, out var content)) _contents[to] = content;
        if (_store!.Get(from) is { } doc) LegacyCity.MoveDocument(_store, doc, to);
        if (Editor.IsVisible && Editor.Current?.Path == from) Editor.Renamed(to);
        await CarryLooksAsync(new Dictionary<string, string>(StringComparer.Ordinal) { [from] = to });
        return true;
    }

    /// <summary>finalizarMovimentoMundo's tail: scheduleRoadRebuild, buildTree, agendarSalvar + marcarSinc, the toast.</summary>
    private void AfterHouseMoved(LegacyCityBuilding house, LegacyCityRegion? region)
    {
        ScheduleRoadRebuild();
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
        if (ReferenceEquals(World.Selected, house)) ShowHouse(house);
        ShowLifeNotice(region is not null ? "Movido para " + region.Name + "." : "Movido para a raiz.");
    }

    /// <summary>
    /// v20AgruparSelecao: at least two notes; the folder's name; a new neighbourhood sized for them inside the folder
    /// shown in Notas (criarRegiaoOrganica), its folder created in the vault; then each note moves into it, house and
    /// file (moverEntradaPara).
    /// </summary>
    public async Task GroupIntoFolderAsync(IReadOnlyList<string> paths, string? typedName = null)
    {
        if (World.City is not { } city || _store is null) return;
        if (paths.Count < 2) { ShowLifeNotice("Selecione pelo menos dois arquivos."); return; }
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Agrupar em nova pasta", NoVaultMessage);
            return;
        }
        var name = (typedName ?? await Dialogs.PromptAsync(new PromptOptions("Agrupar em nova pasta", Label: "Nome da pasta", Value: "Nova pasta", Confirm: "Criar pasta")) ?? "").Trim();
        if (name.Length == 0) return;
        var parent = city.RegionByPath(Explorer.CurrentFolder ?? "");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var region = city.CreateRegion(city.UniqueRegionName(name), paths.Count, LegacyJsMath.Seed(name + now.ToString(System.Globalization.CultureInfo.InvariantCulture)), parent?.Id, null);
        if (region is null) { ShowLifeNotice("Não foi possível criar a pasta."); return; }
        if (!await WriteFolderAsync(city, region, undo: () => city.Regions.Remove(region))) return;
        var folder = city.RegionPaths()[region.Id];
        int moved = 0;
        foreach (var path in paths)
            if (city.Buildings.Find(b => b.IsNote && b.Path == path) is { } house)
            {
                await MoveHouseToFolderAsync(house, folder);
                if (house.RegionId == region.Id) moved++;
            }
        Explorer.ClearSelection();
        RefreshNotes();
        World.InvalidateVisual();
        ShowLifeNotice("Pasta criada com " + moved + " itens.");
    }
}
