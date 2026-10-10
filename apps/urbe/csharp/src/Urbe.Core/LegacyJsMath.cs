namespace Urbe.Core;

/// <summary>
/// JavaScript number semantics needed to reproduce Urbe 1.8.4-beta layout code exactly.
/// Sorting of lots and neighbourhood growth depends on the last bit of these results.
/// </summary>
public static class LegacyJsMath
{
    /// <summary>
    /// V8 Math.hypot (builtins/math.tq): scale by the largest magnitude and Kahan-sum the
    /// squares. It differs from Math.Sqrt(x*x+y*y) in the last bit for some inputs.
    /// </summary>
    public static double Hypot(double a, double b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        if (double.IsInfinity(a) || double.IsInfinity(b)) return double.PositiveInfinity;
        if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
        double max = Math.Max(a, b);
        if (max == 0) return 0;
        double sum = 0, compensation = 0;
        foreach (double value in (ReadOnlySpan<double>)[a, b])
        {
            double n = value / max;
            double summand = n * n - compensation;
            double preliminary = sum + summand;
            compensation = preliminary - sum - summand;
            sum = preliminary;
        }
        return Math.Sqrt(sum) * max;
    }

    /// <summary>JS Math.round: rounds half toward +∞.</summary>
    public static double Round(double x)
    {
        double r = Math.Floor(x);
        return x - r >= .5 ? r + 1 : r;
    }

    /// <summary>JS ToInt32 for the bitwise operators.</summary>
    public static int ToInt32(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return 0;
        double t = Math.Truncate(x);
        double m = t % 4294967296.0;
        if (m < 0) m += 4294967296.0;
        return unchecked((int)(uint)m);
    }

    /// <summary>JS x &gt;&gt;&gt; 0.</summary>
    public static uint ToUint32(double x) => unchecked((uint)ToInt32(x));

    /// <summary>app.js rng(s): the seeded generator used by the city layout.</summary>
    public static Func<double> Rng(double seed)
    {
        uint a = ToUint32(seed);
        return () =>
        {
            unchecked
            {
                a += 0x6D2B79F5;
                uint t = (a ^ (a >> 15)) * (1 | a);
                t = (t + (t ^ (t >> 7)) * (61 | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        };
    }

    /// <summary>app.js semente(txt): FNV-1a over UTF-16 code units.</summary>
    public static uint Seed(string text) => LegacyTerrainMath.Hash32(text);

    /// <summary>app.js slug(s).</summary>
    public static string Slug(string s)
    {
        var decomposed = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(decomposed.Length);
        bool dash = false;
        foreach (char c in decomposed)
        {
            if (c >= '̀' && c <= 'ͯ') continue;
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') { sb.Append(c); dash = false; }
            else if (!dash) { sb.Append('-'); dash = true; }
        }
        var result = sb.ToString().Trim('-');
        // JS: replace(/^-|-$/g,"") removes only one dash at each end; runs were already collapsed.
        return result.Length == 0 ? "item" : result;
    }
}
