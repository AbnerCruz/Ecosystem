using Avalonia.Media;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The house panel's Visual (owner, DEC-0045, UC-41: "ao clicar em uma construção nós podemos editar o visual dela"):
/// the building's trade among the 1.8.4's sprites, then its variant and flowers, each chosen from pictures of the
/// building itself. Kept in .urbe/mundo.json by the file's path (<see cref="WorldPrefs.Looks"/>), never in mapa.json;
/// "Automático" goes back to what the 1.8.4 chooses (the trade by extension, the rest by name).
/// </summary>
public sealed partial class MainView
{
    /// <summary>The 1.8.4's trades with their names in the picker.</summary>
    public static IReadOnlyList<(string Kind, string Label)> BuildingTrades { get; } =
    [
        ("house", "Casa"), ("tower", "Torre"), ("hall", "Salão"), ("workshop", "Oficina"),
        ("market", "Mercado"), ("store", "Loja"), ("dyer", "Tinturaria")
    ];

    /// <summary>Visual: the trade, then the variant and flowers; written to .urbe/mundo.json.</summary>
    public async Task EditLookAsync(LegacyCityBuilding house)
    {
        if (World.City is null) return;
        if (!VaultWritable || Customization.WorldWriter is null)
        {
            await Dialogs.AlertAsync("Visual", NoVaultMessage);
            return;
        }
        var look = World.LookOf(house);
        IImage? Art(BuildingLook l) => World.ArtOf(house, l);

        var trades = new List<(string, IImage?)> { ("Automático", Art(look with { Kind = null })) };
        trades.AddRange(BuildingTrades.Select(t => (t.Label, Art(look with { Kind = t.Kind }))));
        int currentTrade = look.Kind is { } k ? 1 + BuildingTrades.ToList().FindIndex(t => t.Kind == k) : 0;
        if (await Dialogs.PickArtAsync("Visual · " + house.Name, trades, currentTrade, "Escolha a construção.") is not { } t) return;
        look = look with { Kind = t == 0 ? null : BuildingTrades[t - 1].Kind };

        var details = new List<(string Label, int? Variant, bool? Flowers)> { ("Automático", null, null) };
        for (int v = 0; v < BuildingLook.Variants; v++)
        {
            details.Add(($"Variação {v + 1}", v, false));
            details.Add(($"Variação {v + 1} com flores", v, true));
        }
        int currentDetail = Math.Max(0, details.FindIndex(d => d.Variant == look.Variant && d.Flowers == look.Flowers));
        var pick = await Dialogs.PickArtAsync("Detalhes · " + house.Name,
            details.Select(d => (d.Label, Art(look with { Variant = d.Variant, Flowers = d.Flowers }))).ToList(), currentDetail);
        if (pick is { } d) look = look with { Variant = details[d].Variant, Flowers = details[d].Flowers };
        await SetLookAsync(house, look);
    }

    /// <summary>The look of <paramref name="house"/> written to .urbe/mundo.json and drawn.</summary>
    public async Task SetLookAsync(LegacyCityBuilding house, BuildingLook look)
    {
        try { await Customization.SetWorldAsync(Customization.World.WithLook(house.Path, look)); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException)
        {
            await Dialogs.AlertAsync("Não foi possível mudar o visual", ex.Message);
            return;
        }
        if (ReferenceEquals(World.Selected, house)) ShowHouse(house);
        World.InvalidateVisual();
    }

    /// <summary>A renamed or moved file takes its chosen look with it (old path → new path).</summary>
    private async Task CarryLooksAsync(IReadOnlyDictionary<string, string> moves)
    {
        if (moves.Count == 0 || Customization.WorldWriter is null || Customization.World.WithMoved(moves) is not { } moved) return;
        try { await Customization.SetWorldAsync(moved); }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException)
        {
            ShowLifeNotice("O visual escolhido não acompanhou o arquivo: " + ex.Message);
        }
    }
}
