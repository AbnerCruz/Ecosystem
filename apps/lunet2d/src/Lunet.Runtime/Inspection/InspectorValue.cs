using System.Globalization;
using System.Numerics;
using Lunet.Graphics;

namespace Lunet.Runtime.Inspection;

/// <summary>Conversão entre valores do jogo e o texto mostrado/editado no Inspector.</summary>
public static class InspectorValue
{
    /// <summary>Texto que representa o valor (números com ponto decimal, cores em hexadecimal).</summary>
    public static string Format(Type type, object? value)
    {
        if (value is null) return "";
        type = Nullable.GetUnderlyingType(type) ?? type;
        return value switch
        {
            float f => f.ToString("0.###", CultureInfo.InvariantCulture),
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            Vector2 v => $"{v.X.ToString("0.###", CultureInfo.InvariantCulture)}, {v.Y.ToString("0.###", CultureInfo.InvariantCulture)}",
            Color c => c.A == 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}",
            Enum e => e.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    /// <summary>Interpreta o texto digitado. Devolve falso (e não altera nada) se o texto não é válido para o tipo.</summary>
    public static bool TryParse(Type type, string text, out object? value)
    {
        value = null;
        type = Nullable.GetUnderlyingType(type) ?? type;
        text = text.Trim();
        var culture = CultureInfo.InvariantCulture;
        try
        {
            if (type == typeof(string)) { value = text; return true; }
            if (type == typeof(bool))
            {
                if (bool.TryParse(text, out var b)) { value = b; return true; }
                return false;
            }
            if (type.IsEnum)
            {
                if (Enum.TryParse(type, text, ignoreCase: true, out var e) && Enum.IsDefined(type, e!)) { value = e; return true; }
                return false;
            }
            if (type == typeof(Vector2))
            {
                var parts = text.Trim('(', ')').Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && float.TryParse(parts[0], NumberStyles.Float, culture, out var x) && float.TryParse(parts[1], NumberStyles.Float, culture, out var y))
                {
                    value = new Vector2(x, y);
                    return true;
                }
                return false;
            }
            if (type == typeof(Color)) return TryParseColor(text, out value);
            if (type == typeof(float)) return Set(float.TryParse(text.Replace(',', '.'), NumberStyles.Float, culture, out var f), f, out value);
            if (type == typeof(double)) return Set(double.TryParse(text.Replace(',', '.'), NumberStyles.Float, culture, out var d), d, out value);
            if (type == typeof(decimal)) return Set(decimal.TryParse(text.Replace(',', '.'), NumberStyles.Float, culture, out var m), m, out value);
            if (type == typeof(int)) return Set(int.TryParse(text, NumberStyles.Integer, culture, out var i), i, out value);
            if (type == typeof(long)) return Set(long.TryParse(text, NumberStyles.Integer, culture, out var l), l, out value);
            if (type == typeof(short)) return Set(short.TryParse(text, NumberStyles.Integer, culture, out var s), s, out value);
            if (type == typeof(byte)) return Set(byte.TryParse(text, NumberStyles.Integer, culture, out var by), by, out value);
            if (type == typeof(uint)) return Set(uint.TryParse(text, NumberStyles.Integer, culture, out var ui), ui, out value);
            if (type == typeof(ulong)) return Set(ulong.TryParse(text, NumberStyles.Integer, culture, out var ul), ul, out value);
            if (type == typeof(ushort)) return Set(ushort.TryParse(text, NumberStyles.Integer, culture, out var us), us, out value);
            if (type == typeof(sbyte)) return Set(sbyte.TryParse(text, NumberStyles.Integer, culture, out var sb), sb, out value);
        }
        catch (FormatException) { }
        catch (OverflowException) { }
        return false;

        static bool Set(bool ok, object parsed, out object? result)
        {
            result = ok ? parsed : null;
            return ok;
        }
    }

    /// <summary>Aceita <c>#RGB</c>, <c>#RRGGBB</c> e <c>#RRGGBBAA</c> (o <c>#</c> é opcional).</summary>
    public static bool TryParseColor(string text, out object? value)
    {
        value = null;
        text = text.Trim().TrimStart('#');
        if (text.Length == 3) text = string.Concat(text.Select(c => new string(c, 2)));
        if (text.Length is not (6 or 8) || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed)) return false;
        if (text.Length == 6) packed = packed << 8 | 0xFF;
        value = new Color((byte)(packed >> 24), (byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
        return true;
    }

    /// <summary>Converte o valor para o tipo exato do campo (por exemplo, um número decimal do controle deslizante para <c>int</c>).</summary>
    public static object? Coerce(Type type, object? value)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (value is null || type.IsInstanceOfType(value)) return value;
        if (type.IsEnum) return Enum.ToObject(type, value);
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }
}
