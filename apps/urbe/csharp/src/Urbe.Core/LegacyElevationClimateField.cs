namespace Urbe.Core;

/// <summary>
/// Exact algorithmic port of the elevation and climate stages (3 and 4)
/// of Urbe 1.8.4-beta's terrain.js createWorld. Hydrology, biomes at tile
/// resolution and vegetation are deliberately NOT inferred from these fields.
/// </summary>
public sealed class LegacyElevationClimateField
{
    private readonly float[] _elevation;
    private readonly float[] _temperature;
    private readonly float[] _moisture;

    private LegacyElevationClimateField(
        string seedText, LegacyTectonicField tectonics,
        float[] elevation, float[] temperature, float[] moisture)
    {
        SeedText = seedText;
        Tectonics = tectonics;
        _elevation = elevation;
        _temperature = temperature;
        _moisture = moisture;
    }

    public string SeedText { get; }
    public LegacyTectonicField Tectonics { get; }
    public int GridSize => Tectonics.GridSize;
    public float ElevationAt(int x, int y) => _elevation[Index(x, y)];
    public float TemperatureAt(int x, int y) => _temperature[Index(x, y)];
    public float MoistureAt(int x, int y) => _moisture[Index(x, y)];
    internal float[] Elevations() => (float[])_elevation.Clone();
    internal float[] Moistures() => (float[])_moisture.Clone();
    internal float[] Temperatures() => (float[])_temperature.Clone();

    private int Index(int x, int y)
    {
        if (x < 0 || x >= GridSize) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= GridSize) throw new ArgumentOutOfRangeException(nameof(y));
        return y * GridSize + x;
    }

    public static LegacyElevationClimateField Generate(
        string? seedText, int gridSize = 512,
        int? plateCount = null, double landFraction = .4)
    {
        if (!double.IsFinite(landFraction) ||
            landFraction <= 0 || landFraction >= 1)
            throw new ArgumentOutOfRangeException(nameof(landFraction));

        string seedName = string.IsNullOrEmpty(seedText) ? "urbe" : seedText;
        uint seed = LegacyTerrainMath.Hash32(seedName);
        var tectonics = LegacyTectonicField.Generate(
            seedName, gridSize, plateCount);
        int n = tectonics.GridSize, size = checked(n * n);
        var nElevation = new LegacyTerrainMath.GradientNoise(seed);
        var nRidge = new LegacyTerrainMath.GradientNoise(seed ^ 0x9e3779b9u);
        var nTemperature = new LegacyTerrainMath.GradientNoise(seed ^ 0x85ebca6bu);
        var nMoisture = new LegacyTerrainMath.GradientNoise(seed ^ 0xc2b2ae35u);
        var nWarp = new LegacyTerrainMath.GradientNoise(seed ^ 0x27d4eb2fu);
        var baseElevation = new float[size];
        for (int i = 0; i < size; i++)
        {
            int x = i % n, y = i / n;
            var plate = tectonics.Plates[tectonics.PlateIndexAt(x, y)];
            baseElevation[i] = (float)(
                plate.Base +
                LegacyTerrainMath.Fbm(nElevation, x / 130.0, y / 130.0, 4, 2, .5) * .34 +
                LegacyTerrainMath.Fbm(nElevation, x / 38.0 - 50, y / 38.0 + 50, 3, 2, .5) * .08);
        }
        Blur(baseElevation, n, 6, 3);

        var uplift = new float[size];
        for (int i = 0; i < size; i++)
        {
            int x = i % n, y = i / n;
            float source = tectonics.NearestBoundaryUpliftAt(x, y);
            int distance = tectonics.DistanceToBoundaryAt(x, y);
            double radius = source > 0 ? 10 : 5;
            uplift[i] = (float)(source * Math.Exp(
                -(distance * (double)distance) / (radius * radius)));
        }
        Blur(uplift, n, 4, 3);

        var elevation = new float[size];
        for (int i = 0; i < size; i++)
        {
            int x = i % n, y = i / n;
            double u = uplift[i];
            double ridge = Math.Max(0,
                1 - Math.Abs(LegacyTerrainMath.Fbm(
                    nRidge, x / 26.0, y / 26.0, 4, 2.1, .5) * 1.7));
            double up = u > 0 ? u * (.3 + ridge * ridge * 1.05) : u;
            double raw = baseElevation[i] + up +
                LegacyTerrainMath.Fbm(
                    nElevation, x / 22.0 + 300, y / 22.0 - 300,
                    4, 2, .5) * .07;
            double dx = x / (double)n - .5, dy = y / (double)n - .5;
            double rr = Math.Max(Math.Abs(dx), Math.Abs(dy)) * .55 +
                Math.Sqrt(dx * dx + dy * dy) * .45 +
                LegacyTerrainMath.Fbm(
                    nWarp, x / 60.0 - 200, y / 60.0 + 200,
                    3, 2, .5) * .07;
            double edge = 1 - LegacyTerrainMath.Smooth(.34, .49, rr);
            elevation[i] = (float)(raw * edge + (-.05) * (1 - edge));
        }

        // Stable Float32Array sorting of the original: sea level is a rank,
        // not a fixed-height threshold on the original tectonic elevations.
        var sorted = (float[])elevation.Clone();
        Array.Sort(sorted);
        float seaQuantile = sorted[(int)Math.Floor(size * (1 - landFraction))];
        float lowest = sorted[0];
        var land = new bool[size];
        for (int i = 0; i < size; i++)
            land[i] = elevation[i] >= seaQuantile;

        for (int i = 0; i < size; i++)
        {
            if (!land[i])
                elevation[i] = (float)(
                    (elevation[i] - lowest) / (double)(seaQuantile - lowest) *
                    LegacyBiomeRules.SeaLevel);
        }
        RankLand(elevation, land, r =>
        {
            double curve = r < .8 ? r * .45 :
                .36 + .64 * Math.Pow((r - .8) / .2, 1.3);
            return LegacyBiomeRules.SeaLevel + .0005 +
                curve * (1 - LegacyBiomeRules.SeaLevel - .0005);
        });

        // Original Hadley circulation and rain shadow stage.
        var rainfall = new float[size];
        var air = new float[size];
        double sea = LegacyBiomeRules.SeaLevel;
        for (int y = 0; y < n; y++)
        {
            double latitude = (y / (double)(n - 1)) * 2 - 1;
            double absLatitude = Math.Abs(latitude);
            int direction = absLatitude < .33 ? -1 :
                (absLatitude < .66 ? 1 : -1);
            double humidity = .8;
            for (int sample = 0; sample < n; sample++)
            {
                int x = direction > 0 ? sample : n - 1 - sample;
                int i = y * n + x;
                if (elevation[i] < sea)
                {
                    humidity = Math.Min(1, humidity + .12);
                    rainfall[i] = (float)(humidity * .05);
                    air[i] = (float)humidity;
                    continue;
                }
                float previous = direction > 0
                    ? (x > 0 ? elevation[i - 1] : elevation[i])
                    : (x < n - 1 ? elevation[i + 1] : elevation[i]);
                double rise = Math.Max(0, elevation[i] - previous);
                double rain = Math.Min(humidity,
                    humidity * (.006 + rise * 7));
                humidity -= rain * .72;
                humidity = Math.Max(0, humidity - .0004);
                rainfall[i] = (float)rain;
                air[i] = (float)humidity;
            }
        }
        Blur(rainfall, n, 3, 2);
        Blur(air, n, 6, 2);
        var sortedRain = (float[])rainfall.Clone();
        Array.Sort(sortedRain);
        double rain95 = sortedRain[(int)Math.Floor(size * .96)];
        if (rain95 == 0) rain95 = 1;

        var temperature = new float[size];
        var moisture = new float[size];
        for (int i = 0; i < size; i++)
        {
            int x = i % n, y = i / n;
            double latitude = (y / (double)(n - 1)) * 2 - 1;
            double absLatitude = Math.Abs(latitude);
            double hadley = .5 + .5 * Math.Cos(absLatitude * Math.PI * 3);
            moisture[i] = (float)Math.Clamp(
                .5 * air[i] + .3 * Math.Min(1, rainfall[i] / rain95) +
                .38 * hadley - .12 +
                LegacyTerrainMath.Fbm(
                    nMoisture, x / 60.0, y / 60.0, 3, 2, .5) * .25, 0, 1);
            temperature[i] = (float)Math.Clamp(
                1.06 - Math.Pow(absLatitude, 1.6) -
                Math.Max(0, elevation[i] - sea) * 1.15 +
                LegacyTerrainMath.Fbm(
                    nTemperature, x / 80.0, y / 80.0, 3, 2, .5) * .12,
                0, 1);
        }
        var aboveSea = new bool[size];
        for (int i = 0; i < size; i++)
            aboveSea[i] = elevation[i] >= sea;
        RankLand(moisture, aboveSea, r => Math.Pow(r, .95));

        return new LegacyElevationClimateField(
            seedName, tectonics, elevation, temperature, moisture);
    }

    private static void RankLand(
        float[] target, bool[] mask, Func<double, double> curve)
    {
        int[] indices = Enumerable.Range(0, target.Length)
            .Where(i => mask[i])
            // JS Array.sort is stable for equal Float32 values.
            .OrderBy(i => target[i])
            .ThenBy(i => i)
            .ToArray();
        double denominator = Math.Max(1, indices.Length - 1);
        for (int rank = 0; rank < indices.Length; rank++)
            target[indices[rank]] = (float)curve(rank / denominator);
    }

    private static void Blur(float[] field, int n, int radius, int passes)
    {
        int denominator = radius * 2 + 1;
        var temp = new float[field.Length];
        for (int pass = 0; pass < passes; pass++)
        {
            for (int y = 0; y < n; y++)
            {
                double sum = 0;
                int row = y * n;
                for (int xx = -radius; xx <= radius; xx++)
                    sum += field[row + Math.Clamp(xx, 0, n - 1)];
                for (int x = 0; x < n; x++)
                {
                    temp[row + x] = (float)(sum / denominator);
                    sum += field[row + Math.Min(n - 1, x + radius + 1)] -
                           field[row + Math.Max(0, x - radius)];
                }
            }
            for (int x = 0; x < n; x++)
            {
                double sum = 0;
                for (int yy = -radius; yy <= radius; yy++)
                    sum += temp[Math.Clamp(yy, 0, n - 1) * n + x];
                for (int y = 0; y < n; y++)
                {
                    field[y * n + x] = (float)(sum / denominator);
                    sum += temp[Math.Min(n - 1, y + radius + 1) * n + x] -
                           temp[Math.Max(0, y - radius) * n + x];
                }
            }
        }
    }
}
