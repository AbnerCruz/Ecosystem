using System.Globalization;
using System.Text;

namespace Urbe.Core.Calculus;

/// <summary>
/// Reads what a person types in a note: <c>2x^2 + 3x - 1</c>, <c>sen(π/6)</c>, <c>√2</c>, <c>x² + y²</c>,
/// <c>media(1; 2; 3)</c>, <c>[[1; 2]; [3; 4]]</c>, <c>5!</c>, <c>20%</c>, <c>3 km</c>. A comma between digits is the
/// decimal comma (<c>1,5</c>); arguments and items are separated by <c>;</c> or by a comma followed by a space.
/// A product can be implicit (<c>2x</c>, <c>2(x+1)</c>, <c>(a)(b)</c>, <c>3 km</c>).
/// </summary>
public static class MathParser
{
    public static MathExpr Parse(string text, IReadOnlySet<string>? userFunctions = null)
    {
        var p = new Reader(Normalize(text), userFunctions ?? new HashSet<string>());
        var e = p.Equation();
        p.SkipSpaces();
        if (!p.End) throw new MathError("Não entendi a partir de “" + p.Rest + "”.");
        return e;
    }

    /// <summary>The Unicode a phone keyboard or a pasted formula brings, in the calculator's own spelling.</summary>
    internal static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length + 8);
        foreach (var c in text)
            sb.Append(c switch
            {
                '×' or '·' or '⋅' or '∙' => "*",
                '÷' or '∕' => "/",
                '−' or '–' => "-",
                '√' => "sqrt ",
                'π' => "pi",
                '²' => "^2",
                '³' => "^3",
                '∞' => "inf",
                '≠' => "!=",
                _ => c.ToString()
            });
        return sb.ToString().Trim();
    }

    private sealed class Reader(string s, IReadOnlySet<string> userFunctions)
    {
        private int _i;
        public bool End => _i >= s.Length;
        public string Rest => s[_i..];
        private char C => _i < s.Length ? s[_i] : '\0';

        public void SkipSpaces() { while (_i < s.Length && char.IsWhiteSpace(s[_i])) _i++; }

        private bool Eat(char c)
        {
            SkipSpaces();
            if (C != c) return false;
            _i++;
            return true;
        }

        public MathExpr Equation()
        {
            var left = Sum();
            SkipSpaces();
            if (C == '=' && (_i + 1 >= s.Length || s[_i + 1] != '='))
            {
                _i++;
                return new Equation(left, Sum());
            }
            return left;
        }

        private MathExpr Sum()
        {
            var e = Product();
            while (true)
            {
                if (Eat('+')) e = new Add(e, Product());
                else if (Eat('-')) e = new Sub(e, Product());
                else return e;
            }
        }

        private MathExpr Product()
        {
            var e = Unary();
            while (true)
            {
                SkipSpaces();
                if (Eat('*')) e = new Mul(e, Unary());
                else if (Eat('/')) e = new Div(e, Unary());
                // implicit product: 2x, 2(x+1), (a)(b), 3 km, x y
                else if (StartsFactor()) e = new Mul(e, Power());
                else return e;
            }
        }

        private bool StartsFactor()
        {
            SkipSpaces();
            if (End) return false;
            var c = C;
            if (c is '(' or '[') return true;
            // a name continues the product (2x, 3 km) unless it is a word of the sentence around it (5 km em m)
            if (char.IsLetter(c) || c is '°' or 'µ')
                return !(Word("em") || Word("to") || Word("para") || Word("de") || Word("ate") || Word("até") || Word("in"));
            return false;
        }

        /// <summary>The word <paramref name="w"/> stands here on its own (not the start of a longer name).</summary>
        private bool Word(string w) =>
            string.CompareOrdinal(s, _i, w, 0, w.Length) == 0 && (_i + w.Length >= s.Length || !char.IsLetterOrDigit(s[_i + w.Length]));

        private MathExpr Unary()
        {
            if (Eat('-')) return new Neg(Unary());
            if (Eat('+')) return Unary();
            return Power();
        }

        private MathExpr Power()
        {
            var b = Postfix();
            if (Eat('^')) return new Pow(b, Unary()); // right-associative; 2^-1 allowed
            return b;
        }

        private MathExpr Postfix()
        {
            var e = Atom();
            while (true)
            {
                SkipSpaces();
                if (C == '!' && (_i + 1 >= s.Length || s[_i + 1] != '=')) { _i++; e = new Call("fat", [e]); }
                else if (C == '%') { _i++; e = new Div(e, MathExpr.N(100)); }
                else return e;
            }
        }

        private MathExpr Atom()
        {
            SkipSpaces();
            if (End) throw new MathError("A expressão terminou antes do esperado.");
            var c = C;
            if (char.IsDigit(c) || c == '.' && _i + 1 < s.Length && char.IsDigit(s[_i + 1])) return Number();
            if (c == '(')
            {
                _i++;
                var e = Sum();
                if (!Eat(')')) throw new MathError("Falta fechar um parêntese.");
                return e;
            }
            if (c == '[')
            {
                _i++;
                var items = new List<MathExpr>();
                if (!Eat(']'))
                {
                    do items.Add(Sum()); while (Separator());
                    if (!Eat(']')) throw new MathError("Falta fechar um colchete.");
                }
                return new ListExpr(items);
            }
            if (c == '|')
            {
                _i++;
                var e = Sum();
                if (!Eat('|')) throw new MathError("Falta fechar o valor absoluto |…|.");
                return new Call("abs", [e]);
            }
            if (char.IsLetter(c) || c == '°' || c == 'µ')
            {
                int start = _i;
                while (_i < s.Length && (char.IsLetterOrDigit(s[_i]) || s[_i] is '_' or '°' or 'µ' or '²' or '³')) _i++;
                var name = s[start.._i];
                SkipSpaces();
                if (userFunctions.Contains(name) && C == '(')
                {
                    _i++;
                    var userArgs = new List<MathExpr>();
                    do userArgs.Add(Sum()); while (Separator());
                    if (!Eat(')')) throw new MathError("Falta fechar os parênteses de " + name + "(…).");
                    return new Call(name, userArgs);
                }
                // min, h… without parentheses after a number are units (30 min), not functions
                if (MathFunctions.IsFunction(name) && !(C != '(' && MathUnits.TryGet(name, out _)))
                {
                    if (C == '(')
                    {
                        _i++;
                        var args = new List<MathExpr>();
                        if (!Eat(')'))
                        {
                            do args.Add(Sum()); while (Separator());
                            if (!Eat(')')) throw new MathError("Falta fechar os parênteses de " + name + "(…).");
                        }
                        return new Call(MathFunctions.Canonical(name), args);
                    }
                    // sqrt 2, sen x: one factor without parentheses
                    return new Call(MathFunctions.Canonical(name), [Power()]);
                }
                return new Var(name);
            }
            throw new MathError("Não entendi o símbolo “" + c + "”.");
        }

        /// <summary>; always separates; a comma separates when a space follows it (1, 2) — 1,5 is a number.</summary>
        private bool Separator()
        {
            SkipSpaces();
            if (C == ';') { _i++; return true; }
            if (C == ',') { _i++; return true; }
            return false;
        }

        private MathExpr Number()
        {
            int start = _i;
            while (_i < s.Length && char.IsDigit(s[_i])) _i++;
            // decimal point, or decimal comma straight between digits
            if (_i < s.Length && (s[_i] == '.' || s[_i] == ',') && _i + 1 < s.Length && char.IsDigit(s[_i + 1]))
            {
                _i++;
                while (_i < s.Length && char.IsDigit(s[_i])) _i++;
            }
            if (_i < s.Length && (s[_i] is 'e' or 'E') && _i + 1 < s.Length && (char.IsDigit(s[_i + 1]) || (s[_i + 1] is '-' or '+') && _i + 2 < s.Length && char.IsDigit(s[_i + 2])))
            {
                _i += 2;
                while (_i < s.Length && char.IsDigit(s[_i])) _i++;
            }
            var raw = s[start.._i].Replace(',', '.');
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) throw new MathError("Número inválido: " + raw);
            return new Num(v);
        }
    }
}
