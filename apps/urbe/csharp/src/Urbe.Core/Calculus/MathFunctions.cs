namespace Urbe.Core.Calculus;

/// <summary>The calculator's functions, with the names a person types in Portuguese or English.</summary>
public static class MathFunctions
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sin"] = "sen", ["sen"] = "sen", ["cos"] = "cos", ["tan"] = "tan", ["tg"] = "tan",
        ["asin"] = "asen", ["arcsen"] = "asen", ["asen"] = "asen", ["arcsin"] = "asen",
        ["acos"] = "acos", ["arccos"] = "acos", ["atan"] = "atan", ["arctg"] = "atan", ["arctan"] = "atan", ["atan2"] = "atan2",
        ["sinh"] = "senh", ["senh"] = "senh", ["cosh"] = "cosh", ["tanh"] = "tanh", ["tgh"] = "tanh",
        ["exp"] = "exp", ["ln"] = "ln", ["log"] = "log", ["log10"] = "log", ["log2"] = "log2",
        ["sqrt"] = "raiz", ["raiz"] = "raiz", ["cbrt"] = "cbrt", ["abs"] = "abs", ["sign"] = "sinal", ["sinal"] = "sinal",
        ["floor"] = "piso", ["piso"] = "piso", ["ceil"] = "teto", ["teto"] = "teto", ["round"] = "arred", ["arred"] = "arred", ["arredondar"] = "arred",
        ["min"] = "min", ["max"] = "max", ["mod"] = "mod", ["resto"] = "mod",
        ["sum"] = "soma", ["soma"] = "soma", ["prod"] = "produto", ["produto"] = "produto",
        ["mean"] = "media", ["media"] = "media", ["média"] = "media", ["avg"] = "media",
        ["median"] = "mediana", ["mediana"] = "mediana", ["moda"] = "moda", ["mode"] = "moda",
        ["var"] = "variancia", ["variancia"] = "variancia", ["variância"] = "variancia",
        ["stdev"] = "desvio", ["sd"] = "desvio", ["desvio"] = "desvio", ["dp"] = "desvio",
        ["fat"] = "fat", ["fatorial"] = "fat", ["factorial"] = "fat", ["comb"] = "comb", ["ncr"] = "comb", ["perm"] = "perm", ["npr"] = "perm",
        ["mdc"] = "mdc", ["gcd"] = "mdc", ["mmc"] = "mmc", ["lcm"] = "mmc",
        ["det"] = "det", ["inv"] = "inv", ["inversa"] = "inv", ["transposta"] = "transp", ["transpose"] = "transp", ["transp"] = "transp",
        ["tamanho"] = "tamanho", ["len"] = "tamanho", ["contar"] = "tamanho", ["count"] = "tamanho",
        ["graus"] = "graus", ["deg"] = "graus", ["rad"] = "rad", ["radianos"] = "rad",
        ["send"] = "send", ["cosd"] = "cosd", ["tand"] = "tand",
    };

    public static bool IsFunction(string name) => Aliases.ContainsKey(name);
    public static string Canonical(string name) => Aliases.TryGetValue(name, out var c) ? c : name;

    /// <summary>x^y over the reals: a negative base with an odd root (∛-8) keeps its sign; otherwise NaN is reported.</summary>
    public static double RealPow(double x, double y)
    {
        if (x < 0 && y != Math.Round(y))
        {
            var inv = 1 / y;
            if (Math.Abs(inv - Math.Round(inv)) < 1e-9 && ((long)Math.Round(inv)) % 2 != 0) return -Math.Pow(-x, y);
            throw new MathError("Potência fracionária de número negativo não é real.");
        }
        return Math.Pow(x, y);
    }

    public static MathValue Apply(string name, MathValue[] args)
    {
        // functions of a whole list
        switch (name)
        {
            case "soma": case "produto": case "media": case "mediana": case "moda": case "variancia": case "desvio": case "tamanho": case "min": case "max":
                return Aggregate(name, Flatten(args));
            case "det": return Scalar.Of(MathLinear.Determinant(Matrix(args, "det")));
            case "inv": return Vector.FromMatrix(MathLinear.Inverse(Matrix(args, "inv")));
            case "transp": return Vector.FromMatrix(MathLinear.Transpose(Matrix(args, "transposta")));
        }
        // element by element over a list
        if (args.Length == 1 && args[0] is Vector v) return new Vector(v.Items.Select(i => Apply(name, [i])).ToArray());
        var x = args.Select(a => a is Scalar { Pure: true } s ? s.Value : throw new MathError(Label(name) + " recebe números (sem unidade).")).ToArray();
        double One()
        {
            if (x.Length != 1) throw new MathError(Label(name) + " recebe um valor.");
            return x[0];
        }
        double Two()
        {
            if (x.Length != 2) throw new MathError(Label(name) + " recebe dois valores.");
            return 0;
        }
        double r = name switch
        {
            "sen" => Math.Sin(One()), "cos" => Math.Cos(One()), "tan" => Tan(One()),
            "send" => Math.Sin(One() * Math.PI / 180), "cosd" => Math.Cos(One() * Math.PI / 180), "tand" => Tan(One() * Math.PI / 180),
            "asen" => Domain(Math.Asin(One()), "arcsen recebe valores entre -1 e 1."), "acos" => Domain(Math.Acos(One()), "arccos recebe valores entre -1 e 1."),
            "atan" => Math.Atan(One()), "atan2" => Two() + Math.Atan2(x[0], x[1]),
            "senh" => Math.Sinh(One()), "cosh" => Math.Cosh(One()), "tanh" => Math.Tanh(One()),
            "exp" => Math.Exp(One()),
            "ln" => One() > 0 ? Math.Log(x[0]) : throw new MathError("ln recebe números maiores que zero."),
            "log" => x.Length == 2 ? Log(x[0], x[1]) : One() > 0 ? Math.Log10(x[0]) : throw new MathError("log recebe números maiores que zero."),
            "log2" => One() > 0 ? Math.Log2(x[0]) : throw new MathError("log2 recebe números maiores que zero."),
            "raiz" => x.Length == 2 ? RealPow(x[0], 1 / x[1]) : One() >= 0 ? Math.Sqrt(x[0]) : throw new MathError("Raiz quadrada de número negativo não é real."),
            "cbrt" => Math.Cbrt(One()), "abs" => Math.Abs(One()), "sinal" => Math.Sign(One()),
            "piso" => Math.Floor(One()), "teto" => Math.Ceiling(One()),
            "arred" => x.Length == 2 ? Math.Round(x[0], (int)x[1], MidpointRounding.AwayFromZero) : Math.Round(One(), MidpointRounding.AwayFromZero),
            "mod" => Two() + (x[1] == 0 ? throw new MathError("Resto de divisão por zero.") : x[0] - x[1] * Math.Floor(x[0] / x[1])),
            "fat" => Factorial(One()),
            "comb" => Two() + Comb(x[0], x[1]), "perm" => Two() + Factorial(x[0]) / Factorial(x[0] - x[1]),
            "mdc" => Two() + Gcd(x[0], x[1]), "mmc" => Two() + Math.Abs(x[0] * x[1]) / Gcd(x[0], x[1]),
            "graus" => One() * 180 / Math.PI, "rad" => One() * Math.PI / 180,
            _ => throw new MathError("Função desconhecida: " + name)
        };
        if (double.IsNaN(r)) throw new MathError(Label(name) + ": valor fora do domínio.");
        return Scalar.Of(r);
    }

    private static string Label(string name) => name + "(…)";
    private static double Domain(double v, string message) => double.IsNaN(v) ? throw new MathError(message) : v;
    private static double Tan(double x)
    {
        if (Math.Abs(Math.Cos(x)) < 1e-15) throw new MathError("Tangente indefinida neste ângulo.");
        return Math.Tan(x);
    }
    private static double Log(double x, double b) =>
        x > 0 && b > 0 && b != 1 ? Math.Log(x) / Math.Log(b) : throw new MathError("log(x; base) recebe x > 0 e base positiva diferente de 1.");

    public static double Factorial(double n)
    {
        if (n < 0 || n != Math.Round(n)) throw new MathError("Fatorial é de inteiros não negativos.");
        if (n > 170) return double.PositiveInfinity;
        double r = 1;
        for (int i = 2; i <= n; i++) r *= i;
        return r;
    }

    private static double Comb(double n, double k)
    {
        if (n < 0 || k < 0 || n != Math.Round(n) || k != Math.Round(k)) throw new MathError("Combinação recebe inteiros não negativos.");
        if (k > n) return 0;
        double r = 1;
        for (int i = 1; i <= k; i++) r = r * (n - k + i) / i;
        return Math.Round(r);
    }

    private static double Gcd(double a, double b)
    {
        if (a != Math.Round(a) || b != Math.Round(b)) throw new MathError("mdc/mmc recebem inteiros.");
        a = Math.Abs(a); b = Math.Abs(b);
        while (b > 0) (a, b) = (b, a % b);
        return a;
    }

    private static List<Scalar> Flatten(MathValue[] args)
    {
        var list = new List<Scalar>();
        void Add(MathValue v) { if (v is Vector l) foreach (var i in l.Items) Add(i); else list.Add((Scalar)v); }
        foreach (var a in args) Add(a);
        return list;
    }

    private static MathValue Aggregate(string name, List<Scalar> items)
    {
        if (name == "tamanho") return Scalar.Of(items.Count);
        if (items.Count == 0) throw new MathError(name + " precisa de pelo menos um valor.");
        var dim = items[0].Dim;
        if (items.Any(i => i.Dim != dim)) throw new MathError(name + ": os valores têm unidades de dimensões diferentes.");
        var x = items.Select(i => i.Value).ToArray();
        var unit = items.All(i => i.Unit == items[0].Unit) ? items[0].Unit : null;
        Scalar Same(double v) => new(v, dim, unit?.Offset == 0 ? unit : null);
        switch (name)
        {
            case "soma": return Same(x.Sum());
            case "media": return Same(x.Average());
            case "min": return Same(x.Min());
            case "max": return Same(x.Max());
            case "produto": return dim.IsNone ? Scalar.Of(x.Aggregate(1.0, (a, b) => a * b)) : throw new MathError("produto recebe números puros.");
            case "mediana":
                var sorted = x.Order().ToArray();
                return Same(sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2);
            case "moda":
                var groups = x.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).ToList();
                return Same(groups[0].Key);
            case "variancia":
            case "desvio":
                if (x.Length < 2) throw new MathError(name + " precisa de pelo menos dois valores.");
                var mean = x.Average();
                var variance = x.Sum(v => (v - mean) * (v - mean)) / (x.Length - 1); // sample
                return name == "desvio" ? Same(Math.Sqrt(variance)) : new Scalar(variance, dim.Pow(2));
        }
        throw new MathError("Função desconhecida: " + name);
    }

    private static double[,] Matrix(MathValue[] args, string name) =>
        args.Length == 1 && args[0] is Vector v && v.AsMatrix() is { } m ? m : throw new MathError(name + " recebe uma matriz, por exemplo [[1; 2]; [3; 4]].");
}

/// <summary>Small dense linear algebra (Gaussian elimination with partial pivoting).</summary>
public static class MathLinear
{
    public static double[,] Multiply(double[,] a, double[,] b)
    {
        if (a.GetLength(1) != b.GetLength(0)) throw new MathError($"Matrizes {a.GetLength(0)}×{a.GetLength(1)} e {b.GetLength(0)}×{b.GetLength(1)} não se multiplicam.");
        var r = new double[a.GetLength(0), b.GetLength(1)];
        for (int i = 0; i < r.GetLength(0); i++) for (int j = 0; j < r.GetLength(1); j++) for (int k = 0; k < a.GetLength(1); k++) r[i, j] += a[i, k] * b[k, j];
        return r;
    }

    public static double[] Apply(double[,] a, double[] v)
    {
        if (a.GetLength(1) != v.Length) throw new MathError("A matriz e o vetor não têm tamanhos compatíveis.");
        var r = new double[a.GetLength(0)];
        for (int i = 0; i < r.Length; i++) for (int k = 0; k < v.Length; k++) r[i] += a[i, k] * v[k];
        return r;
    }

    public static double[,] Transpose(double[,] a)
    {
        var r = new double[a.GetLength(1), a.GetLength(0)];
        for (int i = 0; i < a.GetLength(0); i++) for (int j = 0; j < a.GetLength(1); j++) r[j, i] = a[i, j];
        return r;
    }

    public static double[,] Power(double[,] a, int n)
    {
        Square(a);
        if (n < 0) { a = Inverse(a); n = -n; }
        var r = Identity(a.GetLength(0));
        for (int i = 0; i < n; i++) r = Multiply(r, a);
        return r;
    }

    public static double Determinant(double[,] a)
    {
        Square(a);
        var m = (double[,])a.Clone();
        int n = m.GetLength(0);
        double det = 1;
        for (int c = 0; c < n; c++)
        {
            int p = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(m[r, c]) > Math.Abs(m[p, c])) p = r;
            if (Math.Abs(m[p, c]) < 1e-14) return 0;
            if (p != c) { Swap(m, p, c); det = -det; }
            det *= m[c, c];
            for (int r = c + 1; r < n; r++)
            {
                var f = m[r, c] / m[c, c];
                for (int k = c; k < n; k++) m[r, k] -= f * m[c, k];
            }
        }
        return det;
    }

    public static double[,] Inverse(double[,] a)
    {
        Square(a);
        int n = a.GetLength(0);
        var m = new double[n, 2 * n];
        for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) m[i, j] = a[i, j]; m[i, n + i] = 1; }
        for (int c = 0; c < n; c++)
        {
            int p = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(m[r, c]) > Math.Abs(m[p, c])) p = r;
            if (Math.Abs(m[p, c]) < 1e-12) throw new MathError("A matriz não tem inversa (determinante zero).");
            Swap(m, p, c);
            var d = m[c, c];
            for (int k = 0; k < 2 * n; k++) m[c, k] /= d;
            for (int r = 0; r < n; r++)
            {
                if (r == c) continue;
                var f = m[r, c];
                for (int k = 0; k < 2 * n; k++) m[r, k] -= f * m[c, k];
            }
        }
        var inv = new double[n, n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = m[i, n + j];
        return inv;
    }

    /// <summary>The solution of A·x = b for a square, non-singular A.</summary>
    public static double[] Solve(double[,] a, double[] b) => Apply(Inverse(a), b);

    private static double[,] Identity(int n)
    {
        var r = new double[n, n];
        for (int i = 0; i < n; i++) r[i, i] = 1;
        return r;
    }

    private static void Square(double[,] a)
    {
        if (a.GetLength(0) != a.GetLength(1)) throw new MathError("A operação precisa de uma matriz quadrada.");
    }

    private static void Swap(double[,] m, int a, int b)
    {
        if (a == b) return;
        for (int k = 0; k < m.GetLength(1); k++) (m[a, k], m[b, k]) = (m[b, k], m[a, k]);
    }
}
