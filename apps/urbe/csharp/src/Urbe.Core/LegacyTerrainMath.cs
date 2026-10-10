namespace Urbe.Core;

/// <summary>
/// Mathematical primitives lifted from Urbe 1.8.4-beta terrain.js. All visual
/// decisions are defined by the legacy code; this is a faithful C# port,
/// not a new random map generator.
///
/// IMPORTANT: JS uses unsigned wrapping Math.imul, >>> and Float32Array.
/// Keep the casting steps and PRNG call order unchanged when porting the
/// remaining continental/climate/hydrology stages.
/// </summary>
public static class LegacyTerrainMath
{
    public static uint Hash32(string? input)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (char character in input ?? string.Empty)
            {
                h ^= character; // exactly one JS UTF-16 code unit
                h *= 16777619;
            }
            return h;
        }
    }

    /// <summary>Same unsigned JS Math.imul-based mulberry32 generator.</summary>
    public sealed class Mulberry32
    {
        private uint _state;
        public Mulberry32(uint seed) => _state = seed;

        public double Next()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                uint t = (_state ^ (_state >> 15)) * (1u | _state);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }
    }

    public sealed class GradientNoise
    {
        private readonly byte[] _permutation = new byte[512];
        private readonly float[] _gx = new float[256];
        private readonly float[] _gy = new float[256];

        public GradientNoise(uint seed)
        {
            var random = new Mulberry32(seed);
            var perm = Enumerable.Range(0, 256).ToArray();
            for (int i = 255; i > 0; i--)
            {
                int j = (int)Math.Floor(random.Next() * (i + 1));
                (perm[i], perm[j]) = (perm[j], perm[i]);
            }

            // JS initializes the 512-wide permutation and writes angles
            // into a Float32Array BEFORE passing them to Math.cos/sin.
            for (int i = 0; i < 512; i++)
            {
                _permutation[i] = (byte)perm[i & 255];
                float angle = (float)(random.Next() * Math.PI * 2);
                if (i < 256)
                {
                    _gx[i] = (float)Math.Cos(angle);
                    _gy[i] = (float)Math.Sin(angle);
                }
            }
        }

        public double Sample(double x, double y)
        {
            int cellX = checked((int)Math.Floor(x));
            int cellY = checked((int)Math.Floor(y));
            double fx = x - cellX, fy = y - cellY;
            int xi = cellX & 255, yi = cellY & 255;
            int aa = _permutation[_permutation[xi] + yi];
            int ab = _permutation[_permutation[xi] + yi + 1];
            int ba = _permutation[_permutation[xi + 1] + yi];
            int bb = _permutation[_permutation[xi + 1] + yi + 1];
            double n00 = _gx[aa] * fx + _gy[aa] * fy;
            double n10 = _gx[ba] * (fx - 1) + _gy[ba] * fy;
            double n01 = _gx[ab] * fx + _gy[ab] * (fy - 1);
            double n11 = _gx[bb] * (fx - 1) + _gy[bb] * (fy - 1);
            double u = Fade(fx), v = Fade(fy);
            double a1 = n00 + (n10 - n00) * u;
            double a2 = n01 + (n11 - n01) * u;
            return a1 + (a2 - a1) * v;
        }
    }

    public static double Fbm(
        GradientNoise noise, double x, double y,
        int octaves, double lacunarity, double gain)
    {
        ArgumentNullException.ThrowIfNull(noise);
        if (octaves < 1) throw new ArgumentOutOfRangeException(nameof(octaves));
        double sum = 0, amplitude = 1, frequency = 1, normalizer = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += amplitude * noise.Sample(x * frequency, y * frequency);
            normalizer += amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }
        return sum / normalizer;
    }

    public static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    public static double Smooth(double a, double b, double x)
    {
        double t = Math.Max(0, Math.Min(1, (x - a) / (b - a)));
        return t * t * (3 - 2 * t);
    }
    public static double Mix(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Original h2() from inside terrain.js createWorld.</summary>
    public static double TileHash(int x, int y, int salt)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + salt * unchecked((int)2246822519u));
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / 4294967296.0;
        }
    }
}

/// <summary>Exact biome IDs and thresholds of Urbe 1.8.4-beta terrain.js.</summary>
public enum LegacyBiome : byte
{
    Deep = 0, Sea = 1, River = 2, Lake = 3, Beach = 4,
    Grass = 5, Meadow = 6, Forest = 7, Dense = 8, Swamp = 9,
    Taiga = 10, Tundra = 11, Snow = 12, Hills = 13,
    Mountain = 14, Peak = 15, Desert = 16, Savanna = 17, Steppe = 18
}

public static class LegacyBiomeRules
{
    public const double SeaLevel = 0.42;

    public static LegacyBiome Classify(
        double elevation, double temperature, double moisture,
        bool isLake = false, bool isRiver = false)
    {
        const double sea = SeaLevel;
        if (elevation < sea - .07) return LegacyBiome.Deep;
        if (elevation < sea) return LegacyBiome.Sea;
        if (isLake) return LegacyBiome.Lake;
        if (isRiver) return LegacyBiome.River;
        if (elevation < sea + .006)
            return temperature < .22 ? LegacyBiome.Tundra :
                moisture > .82 ? LegacyBiome.Swamp : LegacyBiome.Beach;
        if (elevation > .9)
            return temperature < .45 ? LegacyBiome.Snow : LegacyBiome.Peak;
        if (elevation > .78)
            return temperature < .28 ? LegacyBiome.Snow : LegacyBiome.Mountain;
        if (temperature < .12) return LegacyBiome.Snow;
        if (elevation > .68)
            return temperature < .3 ? LegacyBiome.Tundra : LegacyBiome.Hills;
        if (temperature < .28)
            return moisture > .45 ? LegacyBiome.Taiga : LegacyBiome.Tundra;
        if (temperature > .7)
        {
            if (moisture < .28) return LegacyBiome.Desert;
            if (moisture < .5) return LegacyBiome.Savanna;
            return moisture > .8 ? LegacyBiome.Dense : LegacyBiome.Forest;
        }
        if (moisture < .22) return temperature > .5 ? LegacyBiome.Desert : LegacyBiome.Steppe;
        if (moisture < .34) return LegacyBiome.Steppe;
        if (moisture > .9 && elevation < sea + .04) return LegacyBiome.Swamp;
        if (moisture > .78) return LegacyBiome.Dense;
        if (moisture > .56) return LegacyBiome.Forest;
        if (moisture > .45) return LegacyBiome.Meadow;
        return LegacyBiome.Grass;
    }

    public static string Id(LegacyBiome biome) => biome switch
    {
        LegacyBiome.Deep => "deep",
        LegacyBiome.Sea => "sea",
        LegacyBiome.River => "river",
        LegacyBiome.Lake => "lake",
        LegacyBiome.Beach => "beach",
        LegacyBiome.Grass => "grass",
        LegacyBiome.Meadow => "meadow",
        LegacyBiome.Forest => "forest",
        LegacyBiome.Dense => "dense",
        LegacyBiome.Swamp => "swamp",
        LegacyBiome.Taiga => "taiga",
        LegacyBiome.Tundra => "tundra",
        LegacyBiome.Snow => "snow",
        LegacyBiome.Hills => "hills",
        LegacyBiome.Mountain => "mountain",
        LegacyBiome.Peak => "peak",
        LegacyBiome.Desert => "desert",
        LegacyBiome.Savanna => "savanna",
        LegacyBiome.Steppe => "steppe",
        _ => throw new ArgumentOutOfRangeException(nameof(biome))
    };

    public static bool Buildable(LegacyBiome biome) => biome switch
    {
        LegacyBiome.Deep or LegacyBiome.Sea or LegacyBiome.River or
        LegacyBiome.Lake or LegacyBiome.Swamp or LegacyBiome.Snow or
        LegacyBiome.Mountain or LegacyBiome.Peak => false,
        _ => true
    };

    public static bool Roadable(LegacyBiome biome) => biome switch
    {
        LegacyBiome.Deep or LegacyBiome.Sea or LegacyBiome.Snow or
        LegacyBiome.Mountain or LegacyBiome.Peak => false,
        _ => true
    };
}
