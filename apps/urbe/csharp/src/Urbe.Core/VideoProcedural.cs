namespace Urbe.Core;

/// <summary>
/// #1004 (motion graphics at the level of code-made ones): procedural elements. An element with <c>repeat</c> N is N
/// instances, and its motions are formulas of the scene time <c>t</c>, the instance <c>i</c> (0 to n−1), the count
/// <c>n</c> and <c>u</c> = i/(n−1) — particles, orbits, grids, waves and staggered entrances are a formula, not a list
/// of hundreds of elements. The formulas are data: arithmetic and pure functions only (rand and noise are
/// deterministic), with no access to anything outside the frame, so the same video comes out on every machine.
/// </summary>
public static class VideoProcedural
{
    /// <summary>
    /// Most instances of one element. Each instance is drawn every frame; 2000 shapes keep a 720p frame within the
    /// budget of the software renderer on a phone (measured in VideoRenderTests), and more are made by more elements.
    /// </summary>
    public const int MaxRepeat = 2000;

    /// <summary>"add" adds the formula to the value of the keyframes (or the base); "set" replaces it.</summary>
    public static readonly string[] Modes = ["add", "set"];

    private sealed record Compiled(FunctionPlot.Expression Expression, FunctionPlot.Parameter I, FunctionPlot.Parameter N, FunctionPlot.Parameter U);

    // the formulas read i, n and u from their parameters; each thread (preview, export) has its own, so two renders
    // of the same video never see each other's instance
    [ThreadStatic] private static Dictionary<string, Compiled>? _compiled;

    private static Compiled Compile(string expression)
    {
        var i = new FunctionPlot.Parameter("i", 0, 0, 1);
        var n = new FunctionPlot.Parameter("n", 1, 0, 1);
        var u = new FunctionPlot.Parameter("u", 0, 0, 1);
        var parameters = new Dictionary<string, FunctionPlot.Parameter>(StringComparer.Ordinal) { ["i"] = i, ["n"] = n, ["u"] = u };
        return new Compiled(FunctionPlot.Expression.Parse(expression, "t", parameters), i, n, u);
    }

    /// <summary>Throws <see cref="FormatException"/> with what is wrong when the formula cannot be read.</summary>
    public static void Check(string expression) => Compile(expression);

    /// <summary>The formula at scene time <paramref name="t"/> for instance <paramref name="index"/> of <paramref name="count"/>; 0 when not a number.</summary>
    public static double Evaluate(string expression, double t, int index = 0, int count = 1)
    {
        var cache = _compiled ??= new(StringComparer.Ordinal);
        if (!cache.TryGetValue(expression, out var c))
        {
            if (cache.Count > 4096) cache.Clear();
            cache[expression] = c = Compile(expression);
        }
        c.I.Value = index;
        c.N.Value = count;
        c.U.Value = count > 1 ? index / (double)(count - 1) : 0;
        var v = c.Expression.Evaluate(t);
        return double.IsFinite(v) ? v : 0;
    }

    /// <summary>#1004 slice 2: "letters" or "words" — a text drawn one unit per instance (kinetic typography).</summary>
    public static readonly string[] Splits = ["letters", "words"];

    /// <summary>
    /// The units of a split text, each with where it starts in the text: letters are every visible character, words
    /// the runs between spaces. Spaces are never a unit (they would be instances that draw nothing).
    /// </summary>
    public static IReadOnlyList<(int Start, int Length)> Units(string text, string? split)
    {
        var units = new List<(int, int)>();
        if (string.IsNullOrEmpty(text) || split is null) return units;
        if (split == "letters")
        {
            var e = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            while (e.MoveNext())
                if (!string.IsNullOrWhiteSpace(e.GetTextElement())) units.Add((e.ElementIndex, e.GetTextElement().Length));
            return units;
        }
        for (int i = 0; i < text.Length;)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
            int start = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i])) i++;
            if (i > start) units.Add((start, i - start));
        }
        return units;
    }
}
