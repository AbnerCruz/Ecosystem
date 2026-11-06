namespace Urbe.Core.Calculus;

/// <summary>A physical dimension: exponents of metre, kilogram, second, ampere, kelvin, mole (SI base units).</summary>
public readonly record struct Dim(int L, int M, int T, int I, int K, int N)
{
    public static readonly Dim None = default;
    public bool IsNone => this == None;
    public static Dim operator *(Dim a, Dim b) => new(a.L + b.L, a.M + b.M, a.T + b.T, a.I + b.I, a.K + b.K, a.N + b.N);
    public static Dim operator /(Dim a, Dim b) => new(a.L - b.L, a.M - b.M, a.T - b.T, a.I - b.I, a.K - b.K, a.N - b.N);
    public Dim Pow(int n) => new(L * n, M * n, T * n, I * n, K * n, N * n);

    /// <summary>The dimension in SI symbols (m·kg/s²), for results with no nicer unit.</summary>
    public string Symbol()
    {
        var up = new List<string>();
        var down = new List<string>();
        void Part(string s, int e) { if (e > 0) up.Add(s + Exp(e)); else if (e < 0) down.Add(s + Exp(-e)); }
        static string Exp(int e) => e == 1 ? "" : e == 2 ? "²" : e == 3 ? "³" : "^" + e;
        Part("kg", M); Part("m", L); Part("s", T); Part("A", I); Part("K", K); Part("mol", N);
        var text = up.Count == 0 ? "1" : string.Join("·", up);
        return down.Count == 0 ? text : text + "/" + string.Join("·", down);
    }
}

/// <summary>A unit: how many SI base units it is (and an offset for temperature scales).</summary>
public sealed record MathUnit(string Symbol, double Factor, Dim Dim, double Offset = 0, string? Name = null);

/// <summary>
/// The units the calculator knows (CST-206): SI with its prefixes for the common ones, and the everyday ones of
/// Brazil and abroad. A conversion checks the dimension: metres never become seconds.
/// </summary>
public static class MathUnits
{
    private static readonly Dim Length = new(1, 0, 0, 0, 0, 0), Mass = new(0, 1, 0, 0, 0, 0), Time = new(0, 0, 1, 0, 0, 0),
        Current = new(0, 0, 0, 1, 0, 0), Temp = new(0, 0, 0, 0, 1, 0), Amount = new(0, 0, 0, 0, 0, 1);
    private static readonly Dim Area = Length.Pow(2), Volume = Length.Pow(3), Speed = Length / Time, Accel = Length / Time.Pow(2),
        Force = Mass * Accel, Energy = Force * Length, Power = Energy / Time, Pressure = Force / Area, Frequency = Dim.None / Time,
        Charge = Current * Time, Voltage = Power / Current, Resistance = Voltage / Current;

    private static readonly Dictionary<string, MathUnit> Table = Build();

    private static Dictionary<string, MathUnit> Build()
    {
        var t = new Dictionary<string, MathUnit>(StringComparer.Ordinal);
        void U(string symbol, double factor, Dim dim, double offset = 0, params string[] aliases)
        {
            var unit = new MathUnit(symbol, factor, dim, offset);
            t[symbol] = unit;
            foreach (var a in aliases) t[a] = unit;
        }
        void Prefixed(string symbol, double factor, Dim dim, params (string P, double F)[] prefixes)
        {
            U(symbol, factor, dim);
            foreach (var (p, f) in prefixes) U(p + symbol, factor * f, dim);
        }
        (string, double)[] common = [("k", 1e3), ("c", 1e-2), ("m", 1e-3), ("µ", 1e-6), ("u", 1e-6), ("n", 1e-9)];
        (string, double)[] big = [("k", 1e3), ("M", 1e6), ("G", 1e9), ("m", 1e-3), ("µ", 1e-6)];
        Prefixed("m", 1, Length, common);
        U("km²", 1e6, Area); U("m²", 1, Area); U("cm²", 1e-4, Area); U("mm²", 1e-6, Area); U("ha", 1e4, Area);
        U("m³", 1, Volume); U("cm³", 1e-6, Volume); U("mm³", 1e-9, Volume);
        U("L", 1e-3, Volume, 0, "l", "litro", "litros"); U("mL", 1e-6, Volume, 0, "ml");
        U("in", 0.0254, Length, 0, "pol"); U("ft", 0.3048, Length, 0, "pé", "pés"); U("yd", 0.9144, Length); U("mi", 1609.344, Length, 0, "milha", "milhas"); U("nmi", 1852, Length);
        Prefixed("g", 1e-3, Mass, ("k", 1e3), ("m", 1e-3), ("µ", 1e-6));
        U("t", 1000, Mass, 0, "ton"); U("lb", 0.45359237, Mass); U("oz", 0.028349523125, Mass);
        U("s", 1, Time, 0, "seg"); U("ms", 1e-3, Time); U("µs", 1e-6, Time); U("ns", 1e-9, Time);
        U("min", 60, Time); U("h", 3600, Time, 0, "hora", "horas"); U("dia", 86400, Time, 0, "dias", "d"); U("semana", 604800, Time, 0, "semanas"); U("ano", 31557600, Time, 0, "anos");
        U("km/h", 1000 / 3600.0, Speed, 0, "kmh"); U("m/s", 1, Speed); U("mph", 1609.344 / 3600, Speed); U("nó", 1852 / 3600.0, Speed, 0, "nós");
        U("m/s²", 1, Accel);
        Prefixed("N", 1, Force, ("k", 1e3));
        Prefixed("J", 1, Energy, ("k", 1e3), ("M", 1e6));
        U("cal", 4.184, Energy); U("kcal", 4184, Energy, 0, "Cal"); U("Wh", 3600, Energy); U("kWh", 3.6e6, Energy); U("eV", 1.602176634e-19, Energy);
        Prefixed("W", 1, Power, big); U("cv", 735.49875, Power); U("hp", 745.699872, Power);
        Prefixed("Pa", 1, Pressure, ("k", 1e3), ("M", 1e6), ("h", 100)); U("bar", 1e5, Pressure); U("atm", 101325, Pressure); U("mmHg", 133.322387415, Pressure); U("psi", 6894.757293168, Pressure);
        Prefixed("Hz", 1, Frequency, ("k", 1e3), ("M", 1e6), ("G", 1e9));
        Prefixed("A", 1, Current, ("m", 1e-3)); Prefixed("V", 1, Voltage, ("k", 1e3), ("m", 1e-3)); U("Ω", 1, Resistance, 0, "ohm"); U("kΩ", 1e3, Resistance); U("C", 1, Charge); U("mAh", 3.6, Charge);
        U("K", 1, Temp); U("°C", 1, Temp, 273.15, "ºC", "celsius"); U("°F", 5 / 9.0, Temp, 459.67 * 5 / 9.0, "ºF", "fahrenheit");
        U("mol", 1, Amount);
        return t;
    }

    public static bool TryGet(string symbol, out MathUnit unit) => Table.TryGetValue(symbol, out unit!);

    /// <summary>Reads a unit as typed after a value or after “em”: <c>km</c>, <c>km/h</c>, <c>m/s²</c>, <c>kg*m/s^2</c>.</summary>
    public static MathUnit? Parse(string text)
    {
        text = text.Trim().Replace(" ", "").Replace("^2", "²").Replace("^3", "³").Replace("*", "·");
        if (text.Length == 0) return null;
        if (Table.TryGetValue(text, out var direct)) return direct;
        // a quotient and products of known units
        double factor = 1; var dim = Dim.None;
        var parts = text.Split('/');
        for (int i = 0; i < parts.Length; i++)
            foreach (var piece in parts[i].Split('·'))
            {
                var name = piece; int exp = 1;
                if (name.EndsWith('²')) { name = name[..^1]; exp = 2; }
                else if (name.EndsWith('³')) { name = name[..^1]; exp = 3; }
                if (!Table.TryGetValue(name, out var u) || u.Offset != 0) return null;
                var signed = i == 0 ? exp : -exp;
                factor *= Math.Pow(u.Factor, signed);
                dim *= u.Dim.Pow(signed);
            }
        return new MathUnit(text, factor, dim);
    }

    /// <summary>The usual name of a dimension's SI unit, for results that combined units (N, J, W, Pa…).</summary>
    public static string Named(Dim dim) =>
        dim == Force ? "N" : dim == Energy ? "J" : dim == Power ? "W" : dim == Pressure ? "Pa" : dim == Speed ? "m/s" : dim == Accel ? "m/s²"
        : dim == Area ? "m²" : dim == Volume ? "m³" : dim == Frequency ? "Hz" : dim == Voltage ? "V" : dim == Resistance ? "Ω" : dim == Charge ? "C" : dim.Symbol();
}
