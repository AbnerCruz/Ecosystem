using Urbe.Core;

namespace Urbe.Client.World;

/// <summary>
/// app.js 'world.life.host' over the native world: terrain from LegacyWorld (biome -1 while
/// the chunk is not ready, as MUNDO.has), city from LegacyCity; also the town the villagers
/// walk (app.js v25). Read-only.
/// </summary>
internal sealed class CityLifeHost(WorldView view) : ILegacyLifeHost, ILegacyTownMap
{
    private LegacyWorld? World => view.World;
    private LegacyCity? City => view.City;

    public bool Water(int x, int y) => World is { } w && (int)w.At(x, y).Biome <= (int)LegacyBiome.Lake;

    public int Biome(int x, int y) => World is { } w && view.ChunkReady(x, y) ? (int)w.At(x, y).Biome : -1;

    public bool Road(int x, int y) => City?.HasRoad(x, y) ?? false;

    public bool House(int x, int y) => City?.BuildingAt(x, y) is not null;

    public bool District(int x, int y) => City?.RegionAt(x, y) is not null;

    public IReadOnlyList<LegacyCityBuilding> Buildings => City?.Buildings ?? (IReadOnlyList<LegacyCityBuilding>)[];

    public IReadOnlyCollection<(int X, int Y)> Roads => City?.Roads ?? [];

    /// <summary>world.regions with w and urbeCentroide (cell average + .5).</summary>
    public IEnumerable<LegacyLifeDistrict> Districts
    {
        get
        {
            if (City is null) yield break;
            foreach (var r in City.Regions)
            {
                if (r.W == 0) continue;
                double cx, cy;
                if (r.Cells.Count == 0) { cx = r.X + r.W / 2.0; cy = r.Y + r.H / 2.0; }
                else
                {
                    double sx = 0, sy = 0;
                    foreach (var (x, y) in r.Cells) { sx += x; sy += y; }
                    cx = sx / r.Cells.Count + .5;
                    cy = sy / r.Cells.Count + .5;
                }
                yield return new LegacyLifeDistrict(r.Name, r.ParentId is not null, cx, cy);
            }
        }
    }

    /// <summary>v25Povo.</summary>
    public IReadOnlyList<ILegacyLifePerson> People => view.Villagers.People;

    public IReadOnlyList<(string From, string To)> Links => City?.Links ?? (IReadOnlyList<(string, string)>)[];

    public LegacyCityBuilding? BuildingAt(int x, int y) => City?.BuildingAt(x, y);
}
