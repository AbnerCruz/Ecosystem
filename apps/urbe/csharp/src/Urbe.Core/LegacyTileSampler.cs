namespace Urbe.Core;

/// <summary>
/// Urbe 1.8.4-beta terrain.js stage 7: sample the original continuous world
/// in absolute 16px terrain-tile coordinates, including river and lake shapes.
/// No approximated materials, random biome shortcuts or modified palettes.
/// </summary>
public sealed class LegacyTileSampler
{
    public readonly record struct Tile(
        LegacyBiome Biome, double Elevation, double Temperature,
        double Moisture, bool IsLake, double RiverWidth);

    private readonly LegacyHydrologyField _hydrology;
    private readonly LegacyWorldSpawn _spawn;
    private readonly LegacyRiverField _rivers;
    private readonly int _n;
    private readonly float[] _elevation;
    private readonly float[] _temperature;
    private readonly float[] _moisture;
    private readonly double[] _lakeMask;
    private readonly LegacyTerrainMath.GradientNoise _ridge;
    private readonly LegacyTerrainMath.GradientNoise _elevationNoise;
    private readonly LegacyTerrainMath.GradientNoise _moistureNoise;
    private readonly LegacyTerrainMath.GradientNoise _drainageNoise;

    public LegacyTileSampler(
        LegacyHydrologyField hydrology, LegacyWorldSpawn spawn)
    {
        ArgumentNullException.ThrowIfNull(hydrology);
        ArgumentNullException.ThrowIfNull(spawn);
        if (spawn.GridSize != hydrology.GridSize)
            throw new ArgumentException("World grids do not match.", nameof(spawn));
        _hydrology = hydrology;
        _spawn = spawn;
        _n = hydrology.GridSize;
        _elevation = hydrology.Climate.Elevations();
        _temperature = hydrology.Climate.Temperatures();
        _moisture = hydrology.Climate.Moistures();
        _lakeMask = new double[_n * _n];
        for (int y = 0; y < _n; y++)
        for (int x = 0; x < _n; x++)
            _lakeMask[y * _n + x] = hydrology.IsLakeAt(x, y) ? 1 : 0;

        uint seed = LegacyTerrainMath.Hash32(hydrology.Climate.SeedText);
        _elevationNoise = new LegacyTerrainMath.GradientNoise(seed);
        _ridge = new LegacyTerrainMath.GradientNoise(seed ^ 0x9e3779b9u);
        _moistureNoise = new LegacyTerrainMath.GradientNoise(seed ^ 0xc2b2ae35u);
        _drainageNoise = new LegacyTerrainMath.GradientNoise(seed ^ 0x165667b1u);
        _rivers = new LegacyRiverField(hydrology, spawn);
    }

    public LegacyRiverField Rivers => _rivers;
    public LegacyWorldSpawn Spawn => _spawn;

    public Tile Sample(int x, int y)
    {
        double gx = (x + .5 - _spawn.OriginX) / _spawn.CellSize - .5;
        double gy = (y + .5 - _spawn.OriginY) / _spawn.CellSize - .5;
        if (gx < 0 || gy < 0 || gx > _n - 1 || gy > _n - 1)
            return new Tile(LegacyBiome.Deep, .1, .5, .5, false, 0);

        double warpedX = gx +
            LegacyTerrainMath.Fbm(_drainageNoise, x / 46.0, y / 46.0, 3, 2, .5) * .9;
        double warpedY = gy +
            LegacyTerrainMath.Fbm(_drainageNoise,
                x / 46.0 + 23, y / 46.0 - 23, 3, 2, .5) * .9;
        double ec = Bilinear(_elevation, warpedX, warpedY);
        double land = LegacyTerrainMath.Smooth(.43, .52, ec);
        double mountain = LegacyTerrainMath.Smooth(.62, .8, ec);
        double ridge = 1 - Math.Abs(LegacyTerrainMath.Fbm(
            _ridge, x / 18.0, y / 18.0, 3, 2.1, .5) * 1.8);
        ridge = Math.Max(0, ridge);
        double elevation = ec +
            LegacyTerrainMath.Fbm(_elevationNoise,
                x / 34.0, y / 34.0, 3, 2, .5) * .03 * land +
            (ridge * ridge - .35) * .07 * mountain +
            LegacyTerrainMath.Fbm(_elevationNoise,
                x / 9.0 - 70, y / 9.0 + 70, 2, 2, .5) * .01 * land;
        elevation = Math.Clamp(elevation, 0, 1);
        double temperature = Math.Clamp(
            Bilinear(_temperature, gx, gy) - (elevation - ec) * 1.1, 0, 1);
        double moisture = Math.Clamp(
            Bilinear(_moisture, gx, gy) +
            LegacyTerrainMath.Fbm(_moistureNoise,
                x / 24.0, y / 24.0, 2, 2, .5) * .08, 0, 1);
        bool lake = Bilinear(_lakeMask, gx, gy) +
            LegacyTerrainMath.Fbm(_drainageNoise,
                x / 7.0, y / 7.0, 2, 2, .5) * .35 > .5 &&
            elevation >= LegacyBiomeRules.SeaLevel;
        double riverWidth = elevation >= LegacyBiomeRules.SeaLevel && !lake
            ? _rivers.WidthAt(x, y) : 0;
        return new Tile(
            LegacyBiomeRules.Classify(elevation, temperature, moisture,
                lake, riverWidth > 0),
            elevation, temperature, moisture, lake, riverWidth);
    }

    /// <summary>Interpolation must retain original terrain.js clamping order.</summary>
    private double Bilinear(float[] values, double gx, double gy) =>
        BilinearCore(gx, gy, i => values[i]);

    private double Bilinear(double[] values, double gx, double gy) =>
        BilinearCore(gx, gy, i => values[i]);

    private double BilinearCore(
        double gx, double gy, Func<int, double> at)
    {
        int x0 = Math.Clamp((int)Math.Floor(gx), 0, _n - 2);
        int y0 = Math.Clamp((int)Math.Floor(gy), 0, _n - 2);
        double fx = Math.Clamp(gx - x0, 0, 1);
        double fy = Math.Clamp(gy - y0, 0, 1);
        int i0 = y0 * _n + x0;
        return (at(i0) * (1 - fx) + at(i0 + 1) * fx) * (1 - fy) +
            (at(i0 + _n) * (1 - fx) + at(i0 + _n + 1) * fx) * fy;
    }
}
