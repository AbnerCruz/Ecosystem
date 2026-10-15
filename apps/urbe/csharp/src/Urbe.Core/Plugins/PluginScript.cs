using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Urbe.Core.Plugins;

/// <summary>A plugin script failed (bad expression, unknown name, missing permission, limit reached): the message is for the person.</summary>
public sealed class PluginScriptException(string message) : Exception(message);

/// <summary>What the expressions of a vault plugin can ask of the app (each one checked against the plugin's permissions).</summary>
public interface IPluginScriptHost
{
    IReadOnlyList<string> Notes(string? folder);
    string? ReadNote(string path);
    DateTime Now { get; }
}

/// <summary>
/// The small expression language of the vault plugins (native API, ADD-0021): numbers, texts, true/false, lists,
/// variables of the plugin's state, arithmetic, comparisons, e/ou/não, the ternary and a fixed set of functions. No
/// loops, no code from outside, no access to anything the host does not give: a plugin can only compute values.
/// Values are double, string, bool, List&lt;object?&gt; or null.
/// </summary>
public static class PluginScript
{
    private const int MaxDepth = 64;
    private const int MaxText = 200_000;

    // ---------------------------------------------------------------- values

    public static string Text(object? v) => v switch
    {
        null => "",
        string s => s,
        bool b => b ? "sim" : "não",
        double d => Number(d),
        List<object?> l => string.Join(", ", l.Select(Text)),
        _ => v.ToString() ?? ""
    };

    public static string Number(double d) =>
        double.IsFinite(d) ? (Math.Abs(d - Math.Round(d)) < 1e-9 && Math.Abs(d) < 1e15 ? ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture) : d.ToString("0.##########", CultureInfo.InvariantCulture)) : "0";

    public static double Num(object? v) => v switch
    {
        null => 0,
        double d => d,
        bool b => b ? 1 : 0,
        string s => double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0,
        List<object?> l => l.Count,
        _ => 0
    };

    public static bool Truthy(object? v) => v switch
    {
        null => false,
        bool b => b,
        double d => d != 0 && !double.IsNaN(d),
        string s => s.Length > 0,
        List<object?> l => l.Count > 0,
        _ => true
    };

    public static object? FromJson(JsonNode? n) => n switch
    {
        null => null,
        JsonArray a => a.Select(FromJson).ToList(),
        JsonObject => null,
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        _ => LegacyCustomize.JsToNumber(n),
    };

    public static JsonNode? ToJson(object? v) => v switch
    {
        null => null,
        bool b => JsonValue.Create(b),
        double d => JsonValue.Create(double.IsFinite(d) ? d : 0),
        string s => JsonValue.Create(s),
        List<object?> l => new JsonArray(l.Select(ToJson).ToArray()),
        _ => JsonValue.Create(v.ToString())
    };

    private static bool Same(object? a, object? b) => (a, b) switch
    {
        (null, null) => true,
        (double x, double y) => Math.Abs(x - y) < 1e-12,
        (string x, string y) => x == y,
        (bool x, bool y) => x == y,
        (List<object?> x, List<object?> y) => x.Count == y.Count && x.Zip(y).All(p => Same(p.First, p.Second)),
        (double or bool, string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && Math.Abs(Num(a) - d) < 1e-12,
        (string, double or bool) => Same(b, a),
        _ => false
    };

    // ---------------------------------------------------------------- tokens

    private enum T { Num, Str, Id, Op, End }
    private readonly record struct Tok(T Kind, string Text, double Number, int At);

    private static List<Tok> Lex(string src)
    {
        var list = new List<Tok>();
        int i = 0;
        while (i < src.Length)
        {
            char c = src[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (char.IsDigit(c) || (c == '.' && i + 1 < src.Length && char.IsDigit(src[i + 1])))
            {
                int s = i;
                while (i < src.Length && (char.IsDigit(src[i]) || src[i] == '.')) i++;
                if (!double.TryParse(src[s..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) throw new PluginScriptException("Número inválido: " + src[s..i]);
                list.Add(new Tok(T.Num, src[s..i], d, s));
                continue;
            }
            if (c is '"' or '\'')
            {
                int s = i++;
                var sb = new StringBuilder();
                while (i < src.Length && src[i] != c)
                {
                    if (src[i] == '\\' && i + 1 < src.Length)
                    {
                        i++;
                        sb.Append(src[i] switch { 'n' => '\n', 't' => '\t', _ => src[i] });
                    }
                    else sb.Append(src[i]);
                    i++;
                }
                if (i >= src.Length) throw new PluginScriptException("Texto sem fechar aspas: " + src[s..]);
                i++;
                list.Add(new Tok(T.Str, sb.ToString(), 0, s));
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                int s = i;
                while (i < src.Length && (char.IsLetterOrDigit(src[i]) || src[i] == '_')) i++;
                list.Add(new Tok(T.Id, src[s..i], 0, s));
                continue;
            }
            var two = i + 1 < src.Length ? src.Substring(i, 2) : "";
            if (two is "==" or "!=" or "<=" or ">=" or "&&" or "||") { list.Add(new Tok(T.Op, two, 0, i)); i += 2; continue; }
            if ("+-*/%<>!?:(),[]".Contains(c)) { list.Add(new Tok(T.Op, c.ToString(), 0, i)); i++; continue; }
            throw new PluginScriptException("Caractere inesperado “" + c + "” em: " + src);
        }
        list.Add(new Tok(T.End, "", 0, src.Length));
        return list;
    }

    // ---------------------------------------------------------------- syntax tree

    public abstract record Node;
    private sealed record Lit(object? Value) : Node;
    private sealed record Var(string Name) : Node;
    private sealed record Un(string Op, Node A) : Node;
    private sealed record Bin(string Op, Node A, Node B) : Node;
    private sealed record Cond(Node Test, Node Then, Node Else) : Node;
    private sealed record Call(string Name, List<Node> Args) : Node;
    private sealed record ListLit(List<Node> Items) : Node;
    private sealed record Index(Node Target, Node At) : Node;

    private sealed class Parser(List<Tok> toks, string src)
    {
        private int _i;
        private int _depth;
        private Tok Peek => toks[_i];

        private bool IsOp(string op) => Peek.Kind == T.Op && Peek.Text == op;
        private bool IsWord(string w) => Peek.Kind == T.Id && Peek.Text == w;

        private void Expect(string op)
        {
            if (!IsOp(op)) throw new PluginScriptException("Esperava “" + op + "” em: " + src);
            _i++;
        }

        public Node Parse()
        {
            var n = Ternary();
            if (Peek.Kind != T.End) throw new PluginScriptException("Sobrou “" + src[Peek.At..] + "” em: " + src);
            return n;
        }

        private Node Ternary()
        {
            if (++_depth > MaxDepth) throw new PluginScriptException("Expressão aninhada demais.");
            try
            {
                var test = Or();
                if (!IsOp("?")) return test;
                _i++;
                var a = Ternary();
                Expect(":");
                var b = Ternary();
                return new Cond(test, a, b);
            }
            finally { _depth--; }
        }

        private Node Or()
        {
            var a = And();
            while (IsOp("||") || IsWord("ou")) { _i++; a = new Bin("||", a, And()); }
            return a;
        }

        private Node And()
        {
            var a = Equality();
            while (IsOp("&&") || IsWord("e")) { _i++; a = new Bin("&&", a, Equality()); }
            return a;
        }

        private Node Equality()
        {
            var a = Compare();
            while (IsOp("==") || IsOp("!=")) { var op = toks[_i++].Text; a = new Bin(op, a, Compare()); }
            return a;
        }

        private Node Compare()
        {
            var a = Sum();
            while (IsOp("<") || IsOp(">") || IsOp("<=") || IsOp(">=")) { var op = toks[_i++].Text; a = new Bin(op, a, Sum()); }
            return a;
        }

        private Node Sum()
        {
            var a = Product();
            while (IsOp("+") || IsOp("-")) { var op = toks[_i++].Text; a = new Bin(op, a, Product()); }
            return a;
        }

        private Node Product()
        {
            var a = Unary();
            while (IsOp("*") || IsOp("/") || IsOp("%")) { var op = toks[_i++].Text; a = new Bin(op, a, Unary()); }
            return a;
        }

        private Node Unary()
        {
            if (IsOp("-") || IsOp("!") || IsWord("nao") || IsWord("não"))
            {
                var op = toks[_i++].Text;
                if (++_depth > MaxDepth) throw new PluginScriptException("Expressão aninhada demais.");
                try { return new Un(op == "-" ? "-" : "!", Unary()); }
                finally { _depth--; }
            }
            return Postfix();
        }

        private Node Postfix()
        {
            var n = Primary();
            while (IsOp("[")) { _i++; var at = Ternary(); Expect("]"); n = new Index(n, at); }
            return n;
        }

        private Node Primary()
        {
            var t = Peek;
            switch (t.Kind)
            {
                case T.Num: _i++; return new Lit(t.Number);
                case T.Str: _i++; return new Lit(t.Text);
                case T.Id:
                    _i++;
                    switch (t.Text)
                    {
                        case "verdadeiro" or "true" or "sim": return new Lit(true);
                        case "falso" or "false": return new Lit(false);
                        case "nulo" or "null" or "nada": return new Lit(null);
                    }
                    if (IsOp("("))
                    {
                        _i++;
                        var args = new List<Node>();
                        if (!IsOp(")"))
                        {
                            args.Add(Ternary());
                            while (IsOp(",")) { _i++; args.Add(Ternary()); }
                        }
                        Expect(")");
                        if (!Functions.ContainsKey(t.Text)) throw new PluginScriptException("Função desconhecida: " + t.Text + ". Funções: " + string.Join(", ", Functions.Keys) + ".");
                        return new Call(t.Text, args);
                    }
                    return new Var(t.Text);
                case T.Op when t.Text == "(":
                {
                    _i++;
                    var n = Ternary();
                    Expect(")");
                    return n;
                }
                case T.Op when t.Text == "[":
                {
                    _i++;
                    var items = new List<Node>();
                    if (!IsOp("]"))
                    {
                        items.Add(Ternary());
                        while (IsOp(",")) { _i++; items.Add(Ternary()); }
                    }
                    Expect("]");
                    return new ListLit(items);
                }
                default:
                    throw new PluginScriptException(t.Kind == T.End ? "Expressão incompleta: " + src : "Inesperado “" + t.Text + "” em: " + src);
            }
        }
    }

    private static readonly Dictionary<string, Node> Cache = new(StringComparer.Ordinal);

    /// <summary>Parses an expression (cached); throws <see cref="PluginScriptException"/> with a readable message.</summary>
    public static Node Parse(string src)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(src, out var n)) return n;
            n = new Parser(Lex(src), src).Parse();
            if (Cache.Count > 4096) Cache.Clear();
            Cache[src] = n;
            return n;
        }
    }

    // ---------------------------------------------------------------- evaluation

    /// <summary>Where an expression is evaluated: the variables, the host and a step budget shared by a whole run.</summary>
    public sealed class Scope(IDictionary<string, object?> vars, IPluginScriptHost host, Func<string, bool> allowed)
    {
        public IDictionary<string, object?> Vars { get; } = vars;
        public IPluginScriptHost Host { get; } = host;
        public Func<string, bool> Allowed { get; } = allowed;
        public int Steps { get; set; }
        public int MaxSteps { get; init; } = 200_000;

        public void Step()
        {
            if (++Steps > MaxSteps) throw new PluginScriptException("O plugin passou do limite de operações (" + MaxSteps + ").");
        }
    }

    public static object? Eval(string src, Scope scope) => Eval(Parse(src), scope);

    private static object? Eval(Node n, Scope s)
    {
        s.Step();
        switch (n)
        {
            case Lit l: return l.Value is List<object?> lv ? new List<object?>(lv) : l.Value;
            case Var v:
                if (s.Vars.TryGetValue(v.Name, out var val)) return val;
                throw new PluginScriptException("Variável desconhecida: " + v.Name + ". Declare-a em \"estado\".");
            case ListLit ll: return ll.Items.Select(i => Eval(i, s)).ToList();
            case Index ix:
            {
                var target = Eval(ix.Target, s);
                var at = (int)Math.Floor(Num(Eval(ix.At, s)));
                return target switch
                {
                    List<object?> list => at >= 0 && at < list.Count ? list[at] : null,
                    string str => at >= 0 && at < str.Length ? str[at].ToString() : null,
                    _ => null
                };
            }
            case Un u:
            {
                var a = Eval(u.A, s);
                return u.Op == "-" ? -Num(a) : !Truthy(a);
            }
            case Cond c: return Truthy(Eval(c.Test, s)) ? Eval(c.Then, s) : Eval(c.Else, s);
            case Bin b:
            {
                if (b.Op == "&&") { var l = Eval(b.A, s); return Truthy(l) ? Eval(b.B, s) : l; }
                if (b.Op == "||") { var l = Eval(b.A, s); return Truthy(l) ? l : Eval(b.B, s); }
                var x = Eval(b.A, s);
                var y = Eval(b.B, s);
                switch (b.Op)
                {
                    case "+":
                        if (x is List<object?> lx) return y is List<object?> ly ? new List<object?>([.. lx, .. ly]) : new List<object?>([.. lx, y]);
                        if (x is string || y is string)
                        {
                            var t = Text(x) + Text(y);
                            if (t.Length > MaxText) throw new PluginScriptException("Texto grande demais.");
                            return t;
                        }
                        return Num(x) + Num(y);
                    case "-": return Num(x) - Num(y);
                    case "*": return Num(x) * Num(y);
                    case "/": return Num(y) == 0 ? 0d : Num(x) / Num(y);
                    case "%": return Num(y) == 0 ? 0d : Num(x) - Num(y) * Math.Floor(Num(x) / Num(y));
                    case "==": return Same(x, y);
                    case "!=": return !Same(x, y);
                    case "<" or ">" or "<=" or ">=":
                        int cmp = x is string sx && y is string sy ? string.Compare(sx, sy, StringComparison.CurrentCulture) : Num(x).CompareTo(Num(y));
                        return b.Op switch { "<" => cmp < 0, ">" => cmp > 0, "<=" => cmp <= 0, _ => cmp >= 0 };
                }
                throw new PluginScriptException("Operador desconhecido: " + b.Op);
            }
            case Call call:
                return Functions[call.Name].Run(call.Args.Select(a => Eval(a, s)).ToList(), s);
        }
        throw new PluginScriptException("Expressão inválida.");
    }

    // ---------------------------------------------------------------- templates: "Tempo: {formatarTempo(segundos)}"

    /// <summary>A text with {expressões}; "{{" and "}}" are literal braces.</summary>
    public static string Template(string template, Scope scope)
    {
        if (template.IndexOf('{') < 0 && template.IndexOf('}') < 0) return template;
        var sb = new StringBuilder();
        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];
            if (c == '{' && i + 1 < template.Length && template[i + 1] == '{') { sb.Append('{'); i += 2; continue; }
            if (c == '}' && i + 1 < template.Length && template[i + 1] == '}') { sb.Append('}'); i += 2; continue; }
            if (c == '{')
            {
                int end = FindClose(template, i + 1);
                if (end < 0) throw new PluginScriptException("Falta “}” em: " + template);
                sb.Append(Text(Eval(template[(i + 1)..end], scope)));
                if (sb.Length > MaxText) throw new PluginScriptException("Texto grande demais.");
                i = end + 1;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>Checks a template's expressions without running them.</summary>
    public static void CheckTemplate(string template)
    {
        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];
            if ((c == '{' || c == '}') && i + 1 < template.Length && template[i + 1] == c) { i += 2; continue; }
            if (c == '{')
            {
                int end = FindClose(template, i + 1);
                if (end < 0) throw new PluginScriptException("Falta “}” em: " + template);
                Parse(template[(i + 1)..end]);
                i = end + 1;
                continue;
            }
            i++;
        }
    }

    private static int FindClose(string t, int from)
    {
        char? quote = null;
        for (int i = from; i < t.Length; i++)
        {
            char c = t[i];
            if (quote is { } q) { if (c == '\\') i++; else if (c == q) quote = null; continue; }
            if (c is '"' or '\'') quote = c;
            else if (c == '}') return i;
        }
        return -1;
    }

    // ---------------------------------------------------------------- functions

    public sealed record Function(string Signature, string Help, Func<List<object?>, Scope, object?> Run);

    private static object? Arg(List<object?> a, int i) => i < a.Count ? a[i] : null;
    private static string S(List<object?> a, int i) => Text(Arg(a, i));
    private static double N(List<object?> a, int i) => Num(Arg(a, i));
    private static List<object?> L(List<object?> a, int i) => Arg(a, i) as List<object?> ?? [];

    private static string Clock(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, Math.Floor(seconds)));
        return (t.TotalHours >= 1 ? ((int)t.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" : "") + t.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
    }

    private static void NeedRead(Scope s)
    {
        if (!s.Allowed("notas.ler")) throw new PluginScriptException("O plugin não declarou a permissão “notas.ler”.");
    }

    public static IReadOnlyDictionary<string, Function> Functions { get; } = new Dictionary<string, Function>(StringComparer.Ordinal)
    {
        ["agora"] = new("agora()", "milissegundos desde 1970 (para medir tempo)", (_, s) => (double)new DateTimeOffset(s.Host.Now).ToUnixTimeMilliseconds()),
        ["hora"] = new("hora()", "\"HH:mm:ss\" agora", (_, s) => s.Host.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
        ["data"] = new("data()", "\"aaaa-mm-dd\" de hoje", (_, s) => s.Host.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        ["diaSemana"] = new("diaSemana()", "nome do dia da semana", (_, s) => Weekday(s.Host.Now)),
        ["formatarTempo"] = new("formatarTempo(segundos)", "\"mm:ss\" ou \"h:mm:ss\"", (a, _) => Clock(N(a, 0))),
        ["arredondar"] = new("arredondar(x, casas?)", "arredonda", (a, _) => Math.Round(N(a, 0), (int)Math.Clamp(N(a, 1), 0, 10), MidpointRounding.AwayFromZero)),
        ["piso"] = new("piso(x)", "arredonda para baixo", (a, _) => Math.Floor(N(a, 0))),
        ["teto"] = new("teto(x)", "arredonda para cima", (a, _) => Math.Ceiling(N(a, 0))),
        ["abs"] = new("abs(x)", "valor absoluto", (a, _) => Math.Abs(N(a, 0))),
        ["min"] = new("min(a, b, ...)", "menor", (a, _) => a.Count == 0 ? 0d : a.Select(Num).Min()),
        ["max"] = new("max(a, b, ...)", "maior", (a, _) => a.Count == 0 ? 0d : a.Select(Num).Max()),
        ["aleatorio"] = new("aleatorio(a, b)", "inteiro entre a e b", (a, _) => (double)Random.Shared.NextInt64((long)Math.Min(N(a, 0), N(a, 1)), (long)Math.Max(N(a, 0), N(a, 1)) + 1)),
        ["texto"] = new("texto(x)", "converte em texto", (a, _) => S(a, 0)),
        ["numero"] = new("numero(x)", "converte em número", (a, _) => N(a, 0)),
        ["tamanho"] = new("tamanho(x)", "letras de um texto ou itens de uma lista", (a, _) => Arg(a, 0) switch { string t => (double)t.Length, List<object?> l => (double)l.Count, _ => 0d }),
        ["maiusculas"] = new("maiusculas(t)", "", (a, _) => S(a, 0).ToUpperInvariant()),
        ["minusculas"] = new("minusculas(t)", "", (a, _) => S(a, 0).ToLowerInvariant()),
        ["aparar"] = new("aparar(t)", "tira espaços das pontas", (a, _) => S(a, 0).Trim()),
        ["contem"] = new("contem(texto ou lista, x)", "", (a, _) => Arg(a, 0) is List<object?> l ? l.Any(i => Same(i, Arg(a, 1))) : S(a, 0).Contains(S(a, 1), StringComparison.OrdinalIgnoreCase)),
        ["comecaCom"] = new("comecaCom(t, x)", "", (a, _) => S(a, 0).StartsWith(S(a, 1), StringComparison.OrdinalIgnoreCase)),
        ["substituir"] = new("substituir(t, de, para)", "troca todas as ocorrências", (a, _) => S(a, 1).Length == 0 ? S(a, 0) : S(a, 0).Replace(S(a, 1), S(a, 2), StringComparison.Ordinal)),
        ["trecho"] = new("trecho(t, inicio, quantos?)", "parte de um texto", (a, _) =>
        {
            var t = S(a, 0);
            int st = (int)Math.Clamp(N(a, 1), 0, t.Length);
            int len = a.Count > 2 ? (int)Math.Clamp(N(a, 2), 0, t.Length - st) : t.Length - st;
            return t.Substring(st, len);
        }),
        ["repetirTexto"] = new("repetirTexto(t, n)", "", (a, _) =>
        {
            var n = (int)Math.Clamp(N(a, 1), 0, 10_000);
            if ((long)S(a, 0).Length * n > MaxText) throw new PluginScriptException("Texto grande demais.");
            return string.Concat(Enumerable.Repeat(S(a, 0), n));
        }),
        ["linhas"] = new("linhas(t)", "lista com as linhas", (a, _) => S(a, 0).Split('\n').Select(x => (object?)x.TrimEnd('\r')).ToList()),
        ["dividir"] = new("dividir(t, sep)", "lista com as partes", (a, _) => (S(a, 1).Length == 0 ? S(a, 0).Select(c => c.ToString()) : S(a, 0).Split(S(a, 1))).Select(x => (object?)x).ToList()),
        ["juntar"] = new("juntar(lista, sep)", "texto com os itens", (a, _) => string.Join(a.Count > 1 ? S(a, 1) : ", ", L(a, 0).Select(Text))),
        ["palavras"] = new("palavras(t)", "quantas palavras", (a, _) => (double)S(a, 0).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length),
        ["adicionar"] = new("adicionar(lista, x)", "nova lista com x no fim", (a, _) => (object?)new List<object?>([.. L(a, 0), Arg(a, 1)])),
        ["remover"] = new("remover(lista, indice)", "nova lista sem o item", (a, _) =>
        {
            var l = new List<object?>(L(a, 0));
            var i = (int)N(a, 1);
            if (i >= 0 && i < l.Count) l.RemoveAt(i);
            return l;
        }),
        ["trocar"] = new("trocar(lista, indice, x)", "nova lista com o item trocado", (a, _) =>
        {
            var l = new List<object?>(L(a, 0));
            var i = (int)N(a, 1);
            if (i >= 0 && i < l.Count) l[i] = Arg(a, 2);
            return l;
        }),
        ["indice"] = new("indice(lista, x)", "posição de x, ou -1", (a, _) => (double)L(a, 0).FindIndex(i => Same(i, Arg(a, 1)))),
        ["ordenar"] = new("ordenar(lista)", "nova lista em ordem", (a, _) => L(a, 0).OrderBy(i => i is string ? 1 : 0).ThenBy(Num).ThenBy(Text, StringComparer.CurrentCulture).ToList()),
        ["inverter"] = new("inverter(lista ou texto)", "", (a, _) => Arg(a, 0) is List<object?> l ? Enumerable.Reverse(l).ToList() : new string(S(a, 0).Reverse().ToArray())),
        ["soma"] = new("soma(lista)", "", (a, _) => L(a, 0).Sum(Num)),
        ["sequencia"] = new("sequencia(n)", "[0, 1, ..., n-1]", (a, _) => Enumerable.Range(0, (int)Math.Clamp(N(a, 0), 0, 10_000)).Select(i => (object?)(double)i).ToList()),
        ["notas"] = new("notas(pasta?)", "caminhos das notas (precisa de notas.ler)", (a, s) => { NeedRead(s); return s.Host.Notes(a.Count > 0 ? S(a, 0) : null).Select(p => (object?)p).ToList(); }),
        ["lerNota"] = new("lerNota(caminho)", "texto da nota, ou nulo (precisa de notas.ler)", (a, s) => { NeedRead(s); return s.Host.ReadNote(S(a, 0)); }),
    };

    private static string Weekday(DateTime d) =>
        new[] { "domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado" }[(int)d.DayOfWeek];
}
