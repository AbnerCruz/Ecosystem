using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// "Organizar os bairros" (city.reorganize) and "Desfazer reorganização" (city.undoReorganize), as app.js and
/// world/layout-guard.js: after the person confirms, a copy of the map goes to .urbe/backup (REQ-043, written and
/// verified before anything moves), the layout is photographed for the undo and the city is reorganized
/// (LegacyCity.Reorganize); the streets, the Explorer and the map follow. Notes, folders and links do not change.
/// </summary>
public sealed partial class MainView
{
    private LegacyCityLayoutSnapshot? _lastLayout;

    /// <summary>urbeLayout.last(): there is a reorganization to undo.</summary>
    public bool CanUndoReorganize => _lastLayout is not null;

    /// <summary>city.reorganize.</summary>
    public async Task ReorganizeAsync(bool confirmed = false)
    {
        if (World.City is not { } city || _store is null) return;
        if (city.Regions.Count == 0 && city.Buildings.Count == 0) { ShowLifeNotice("A cidade ainda está vazia."); return; }
        if (!confirmed && !await Dialogs.ConfirmAsync("Organizar os bairros?",
                "Os bairros são redesenhados (cada subpasta dentro da sua pasta) e as casas se agrupam perto do centro de cada bairro. Notas, pastas e ligações não mudam; só o lugar de cada coisa no mapa.",
                "Organizar")) return;
        if (!ReferenceEquals(World.City, city)) return;
        // layout-guard.js before(): the backup of the map as it is now (only where the map is written at all)
        if (_mapSave is not null && _vault is not null && _mapWriter is not null)
        {
            var text = _mapWriter.Produce(city, _store, World.Camera.X, World.Camera.Y, World.Camera.Zoom,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), LegacyCityMap.ReferenceVersion, out var refusal);
            try
            {
                if (text is null) throw new InvalidOperationException(refusal ?? "O mapa não pôde ser lido.");
                await _vault.SaveLayoutBackupAsync(text, DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                await Dialogs.AlertAsync("Não foi possível organizar", "A cópia de segurança do mapa não foi gravada (" + ex.Message + "). Nada foi mudado.");
                return;
            }
        }
        _lastLayout = city.SnapshotLayout();
        var r = city.Reorganize();
        AfterLayoutChanged(city);
        World.FitNotes();   // urbeEnquadrarNotas(true)
        ShowLifeNotice((r.Failures != 0 ? "Cidade organizada (" + r.Failures + " bairro(s) sem espaço)." : "Cidade organizada.") + " Para voltar: Ctrl+K → Desfazer reorganização.");
    }

    /// <summary>city.undoReorganize: the photo taken before the last reorganization.</summary>
    public void UndoReorganize()
    {
        if (_lastLayout is not { } last || World.City is not { } city) return;
        _lastLayout = null;
        city.RestoreLayout(last);
        AfterLayoutChanged(city);   // onRestored: indexar, rebuildRoadNetwork, counts, buildTree, marcarSinc
        ShowLifeNotice("Reorganização desfeita.");
    }

    private void AfterLayoutChanged(LegacyCity city)
    {
        if (_store is not null && city.RebuildRoadsAfterChange(_store)) World.RoadsChanged();
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
        if (World.Selected is { } house) ShowHouse(house);
    }
}
