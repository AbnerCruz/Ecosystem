using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// JavaScript's <c>JSON.stringify(value, null, indent)</c> and <c>Number.prototype.toString</c>, as the
/// 1.8.4-beta writes .urbe/mapa.json (workspace.js desired: <c>JSON.stringify(metadata,null,1)</c>).
/// Byte-for-byte: property order of a JS object (array-index keys first, ascending; then the other
/// keys in insertion order), numbers in the ECMAScript shortest form (1152, not 1152.0; 1e-7; -0 → 0;
/// NaN/Infinity → null), strings escaped as QuoteJSONString (only ", \, control characters and lone
/// surrogates; everything else raw).
/// </summary>
public static class LegacyJson
{
    /// <summary>JSON.stringify(node, null, indent) (indent ≤ 10 spaces, as JS clamps it).</summary>
    public static string Stringify(JsonNode? node, int indent = 0)
    {
        var sb = new StringBuilder();
        Write(sb, node, new string(' ', Math.Clamp(indent, 0, 10)), "");
        return sb.ToString();
    }

    /// <summary>JSON.parse then JSON.stringify(…, null, indent): the text the 1.8.4 writes for that value.</summary>
    public static string Normalize(string json, int indent) => Stringify(JsonNode.Parse(json), indent);

    private static void Write(StringBuilder sb, JsonNode? node, string gap, string indent)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                return;
            case JsonObject obj:
                WriteObject(sb, obj, gap, indent);
                return;
            case JsonArray arr:
                WriteArray(sb, arr, gap, indent);
                return;
            case JsonValue value:
                WriteValue(sb, value);
                return;
        }
    }

    private static void WriteValue(StringBuilder sb, JsonValue value)
    {
        if (value.TryGetValue<JsonElement>(out var e))
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.String: Quote(sb, e.GetString()!); return;
                case JsonValueKind.Number: sb.Append(NumberToJson(double.Parse(e.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture))); return;
                case JsonValueKind.True: sb.Append("true"); return;
                case JsonValueKind.False: sb.Append("false"); return;
                default: sb.Append("null"); return;
            }
        }
        if (value.TryGetValue<string>(out var s)) { Quote(sb, s); return; }
        if (value.TryGetValue<bool>(out var b)) { sb.Append(b ? "true" : "false"); return; }
        if (value.TryGetValue<double>(out var d)) { sb.Append(NumberToJson(d)); return; }
        if (value.TryGetValue<int>(out var i)) { sb.Append(NumberToJson(i)); return; }
        if (value.TryGetValue<long>(out var l)) { sb.Append(NumberToJson(l)); return; }
        if (value.TryGetValue<float>(out var f)) { sb.Append(NumberToJson(f)); return; }
        if (value.TryGetValue<decimal>(out var m)) { sb.Append(NumberToJson((double)m)); return; }
        throw new NotSupportedException("Valor JSON sem equivalente em JavaScript: " + value);
    }

    private static void WriteObject(StringBuilder sb, JsonObject obj, string gap, string indent)
    {
        var keys = OrderedKeys(obj);
        if (keys.Count == 0) { sb.Append("{}"); return; }
        var inner = indent + gap;
        sb.Append('{');
        for (int i = 0; i < keys.Count; i++)
        {
            if (i > 0) sb.Append(',');
            if (gap.Length > 0) sb.Append('\n').Append(inner);
            Quote(sb, keys[i].Key);
            sb.Append(gap.Length > 0 ? ": " : ":");
            Write(sb, keys[i].Value, gap, inner);
        }
        if (gap.Length > 0) sb.Append('\n').Append(indent);
        sb.Append('}');
    }

    private static void WriteArray(StringBuilder sb, JsonArray arr, string gap, string indent)
    {
        if (arr.Count == 0) { sb.Append("[]"); return; }
        var inner = indent + gap;
        sb.Append('[');
        for (int i = 0; i < arr.Count; i++)
        {
            if (i > 0) sb.Append(',');
            if (gap.Length > 0) sb.Append('\n').Append(inner);
            Write(sb, arr[i], gap, inner);
        }
        if (gap.Length > 0) sb.Append('\n').Append(indent);
        sb.Append(']');
    }

    /// <summary>OrdinaryOwnPropertyKeys: array indices ascending, then strings in creation order.</summary>
    private static List<KeyValuePair<string, JsonNode?>> OrderedKeys(JsonObject obj)
    {
        var indices = new List<(uint Index, KeyValuePair<string, JsonNode?> Pair)>();
        var others = new List<KeyValuePair<string, JsonNode?>>();
        foreach (var pair in obj)
        {
            if (IsArrayIndex(pair.Key, out var index)) indices.Add((index, pair));
            else others.Add(pair);
        }
        if (indices.Count == 0) return others;
        return indices.OrderBy(p => p.Index).Select(p => p.Pair).Concat(others).ToList();
    }

    /// <summary>A canonical numeric string of an integer in [0, 2³² − 2].</summary>
    private static bool IsArrayIndex(string key, out uint index)
    {
        index = 0;
        if (key.Length == 0 || key.Length > 10 || (key.Length > 1 && key[0] == '0')) return false;
        foreach (var c in key) if (c is < '0' or > '9') return false;
        if (!ulong.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var v) || v > 4294967294UL) return false;
        index = (uint)v;
        return true;
    }

    /// <summary>QuoteJSONString (ES2019 well-formed JSON.stringify).</summary>
    public static void Quote(StringBuilder sb, string s)
    {
        sb.Append('"');
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '"': sb.Append("\\\""); continue;
                case '\\': sb.Append("\\\\"); continue;
                case '\b': sb.Append("\\b"); continue;
                case '\f': sb.Append("\\f"); continue;
                case '\n': sb.Append("\\n"); continue;
                case '\r': sb.Append("\\r"); continue;
                case '\t': sb.Append("\\t"); continue;
            }
            if (c < 0x20) { sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); continue; }
            if (char.IsHighSurrogate(c))
            {
                if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { sb.Append(c).Append(s[i + 1]); i++; continue; }
                sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                continue;
            }
            if (char.IsLowSurrogate(c)) { sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); continue; }
            sb.Append(c);
        }
        sb.Append('"');
    }

    /// <summary>A number as JSON.stringify writes it: Number::toString, non-finite → null.</summary>
    public static string NumberToJson(double v) => double.IsFinite(v) ? NumberToString(v) : "null";

    /// <summary>
    /// ECMAScript Number::toString(10): the shortest digits that round-trip (the same digits .NET's
    /// round-trip format chooses), laid out as JS does (plain up to 21 integer digits, 0.000001 down to
    /// 1e-7, exponent "e+21"/"e-7" otherwise).
    /// </summary>
    public static string NumberToString(double v)
    {
        if (double.IsNaN(v)) return "NaN";
        if (double.IsPositiveInfinity(v)) return "Infinity";
        if (double.IsNegativeInfinity(v)) return "-Infinity";
        if (v == 0) return "0"; // also -0
        var r = Math.Abs(v).ToString("R", CultureInfo.InvariantCulture);
        int ePos = r.IndexOfAny(['E', 'e']);
        var mantissa = ePos < 0 ? r : r[..ePos];
        int exp = ePos < 0 ? 0 : int.Parse(r[(ePos + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        int dot = mantissa.IndexOf('.');
        var digits = dot < 0 ? mantissa : mantissa.Remove(dot, 1);
        int intLen = dot < 0 ? mantissa.Length : dot;
        int lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0') lead++;
        digits = digits[lead..];
        intLen -= lead;
        digits = digits.TrimEnd('0');
        if (digits.Length == 0) return "0";
        int k = digits.Length, n = intLen + exp;
        string body;
        if (k <= n && n <= 21) body = digits + new string('0', n - k);
        else if (0 < n && n <= 21) body = digits[..n] + "." + digits[n..];
        else if (-6 < n && n <= 0) body = "0." + new string('0', -n) + digits;
        else
        {
            int e = n - 1;
            var es = (e >= 0 ? "+" : "-") + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
            body = k == 1 ? digits + "e" + es : digits[..1] + "." + digits[1..] + "e" + es;
        }
        return v < 0 ? "-" + body : body;
    }
}
