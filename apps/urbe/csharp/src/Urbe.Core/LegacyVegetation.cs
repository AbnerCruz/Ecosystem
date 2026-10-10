namespace Urbe.Core;

/// <summary>
/// Exact original Urbe 1.8.4-beta terrain.js tree()/decor() rules.
/// The client chooses only bundled v184 PNGs; no new species, palette or
/// procedural art is introduced by this port.
/// </summary>
public sealed class LegacyVegetation
{
    public sealed record Tree(string Kind, int Variant, bool Snow);

    private readonly LegacyTileSampler _terrain;
    private readonly LegacyTerrainMath.GradientNoise _moistureNoise;

    public LegacyVegetation(LegacyTileSampler terrain)
    {
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        uint seed = LegacyTerrainMath.Hash32(terrain.SeedText);
        _moistureNoise = new LegacyTerrainMath.GradientNoise(seed ^ 0xc2b2ae35u);
    }

    private static double Hash(int x, int y, int salt) =>
        LegacyTerrainMath.TileHash(x, y, salt);

    // JavaScript Math.round() rounds negative halves toward positive infinity;
    // Math.Round() in C# defaults to ties-to-even and is NOT equivalent.
    private static int JsRound(double value) => (int)Math.Floor(value + .5);

    private LegacyBiome BiomeAt(int x, int y) => _terrain.Sample(x, y).Biome;

    private static bool IsForbidden(LegacyBiome biome) =>
        biome is LegacyBiome.Deep or LegacyBiome.Sea or LegacyBiome.River or
            LegacyBiome.Lake or LegacyBiome.Snow or
            LegacyBiome.Mountain or LegacyBiome.Peak;

    private static (double Chance, string Species)? TreeRule(LegacyBiome biome) =>
        biome switch
        {
            LegacyBiome.Grass => (.035, "oak"),
            LegacyBiome.Meadow => (.05, "birch"),
            LegacyBiome.Forest => (.34, "oak"),
            LegacyBiome.Dense => (.55, "oak"),
            LegacyBiome.Taiga => (.36, "pine"),
            LegacyBiome.Tundra => (.03, "deadpine"),
            LegacyBiome.Hills => (.08, "pine"),
            LegacyBiome.Savanna => (.05, "acacia"),
            LegacyBiome.Desert => (.018, "cactus"),
            LegacyBiome.Swamp => (.14, "willow"),
            LegacyBiome.Steppe => (.012, "oak"),
            LegacyBiome.Beach => (.01, "palm"),
            _ => null
        };

    public Tree? TreeAt(int x, int y)
    {
        var local = _terrain.Sample(x, y);
        if (IsForbidden(local.Biome))
            return null;
        int dx = JsRound((Hash(x, y, 41) - .5) * 6);
        int dy = JsRound((Hash(x, y, 43) - .5) * 6);
        var nearby = BiomeAt(x + dx, y + dy);
        var biome = IsForbidden(nearby) ? local.Biome : nearby;
        var rule = TreeRule(biome);
        if (rule is null)
            return null;
        double clump = .55 + LegacyTerrainMath.Fbm(
            _moistureNoise, x / 9.0 + 300, y / 9.0 - 300, 2, 2, .5) * 1.4;
        if (Hash(x, y, 7) > rule.Value.Chance * Math.Max(.2, clump))
            return null;

        string kind = rule.Value.Species;
        if (biome == LegacyBiome.Forest && Hash(x, y, 9) < .28)
            kind = "birch";
        if (biome == LegacyBiome.Dense && Hash(x, y, 9) < .35)
            kind = "pine";
        if (biome == LegacyBiome.Taiga &&
            local.Biome == LegacyBiome.Taiga && Hash(x, y, 3) < .1)
            kind = "deadpine";
        bool snow = biome == LegacyBiome.Tundra ||
            (biome == LegacyBiome.Taiga && local.Elevation > .62);
        return new Tree(kind, (int)Math.Floor(Hash(x, y, 11) * 3), snow);
    }

    public IReadOnlyList<string> DecorAt(int x, int y)
    {
        var biome = BiomeAt(x, y);
        double r = Hash(x, y, 21), q = Hash(x, y, 22);
        double patch = LegacyTerrainMath.Fbm(
            _moistureNoise, x / 6.0 - 900, y / 6.0 + 900, 2, 2, .5);
        var decor = new List<string>(2);
        if (biome <= LegacyBiome.Lake)
        {
            if ((biome == LegacyBiome.Lake || biome == LegacyBiome.River ||
                biome == LegacyBiome.Sea) &&
                r < (biome == LegacyBiome.Sea ? .02 : .16))
            {
                bool neighborIsLand = BiomeAt(x + 1, y) > LegacyBiome.Lake ||
                    BiomeAt(x - 1, y) > LegacyBiome.Lake ||
                    BiomeAt(x, y + 1) > LegacyBiome.Lake ||
                    BiomeAt(x, y - 1) > LegacyBiome.Lake;
                if (neighborIsLand)
                    decor.Add(biome == LegacyBiome.Sea ? "shorerock" : "lily");
            }
            return decor;
        }
        switch (biome)
        {
            case LegacyBiome.Grass:
                if (patch > .12 ? r < .55 : r < .07) decor.Add("flowers");
                if (q < .13) decor.Add("bush");
                else if (q < .36) decor.Add("tallgrass");
                else if (q < .42) decor.Add("pebbles");
                break;
            case LegacyBiome.Meadow:
                if (patch > 0 ? r < .7 : r < .22) decor.Add("flowers");
                if (q < .32) decor.Add("tallgrass");
                else if (q > .96) decor.Add("bush");
                break;
            case LegacyBiome.Forest:
            case LegacyBiome.Dense:
                if (r < .1) decor.Add("mushroom");
                else if (r < .26) decor.Add("fern");
                if (q < .05) decor.Add("log");
                else if (q < .09) decor.Add("stump");
                else if (q < .2) decor.Add("bush");
                break;
            case LegacyBiome.Taiga:
                if (r < .18) decor.Add("fern");
                else if (r < .24) decor.Add("mushroom");
                if (q < .08) decor.Add("rock");
                else if (q < .12) decor.Add("stump");
                break;
            case LegacyBiome.Swamp:
                if (r < .4) decor.Add("reeds");
                if (q < .22) decor.Add("puddle");
                else if (q < .3) decor.Add("lily");
                break;
            case LegacyBiome.Beach:
                if (r < .05) decor.Add("shell");
                if (q < .03) decor.Add("driftwood");
                else if (q < .1) decor.Add("pebbles");
                break;
            case LegacyBiome.Desert:
                if (r < .05) decor.Add("rock");
                if (q < .07) decor.Add("drybush");
                else if (q < .1) decor.Add("pebbles");
                break;
            case LegacyBiome.Savanna:
                if (r < .4) decor.Add("drygrass");
                if (q < .08) decor.Add("drybush");
                else if (q < .11) decor.Add("rock");
                break;
            case LegacyBiome.Steppe:
                if (r < .18) decor.Add("tuft");
                if (q < .25) decor.Add("drygrass");
                else if (q < .3) decor.Add("pebbles");
                break;
            case LegacyBiome.Hills:
                if (r < .14) decor.Add("rock");
                else if (r < .2) decor.Add("flowers");
                if (q < .16) decor.Add("pebbles");
                else if (q < .4) decor.Add("tallgrass");
                break;
            case LegacyBiome.Mountain:
            case LegacyBiome.Peak:
                if (r < .22) decor.Add("rock");
                if (q < .07) decor.Add("boulder");
                break;
            case LegacyBiome.Tundra:
                if (r < .22) decor.Add("snowpatch");
                if (q < .1) decor.Add("rock");
                else if (q < .2) decor.Add("tuft");
                break;
            case LegacyBiome.Snow:
                if (r < .06) decor.Add("rock");
                break;
        }
        return decor;
    }
}
