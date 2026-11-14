using System.Globalization;
using System.Text;

namespace Lunet.Studio;

public enum VectorOp { Move, Line, Quad, Cubic, Close }

/// <summary>Um segmento de caminho. Para Line/Move só (X1,Y1) vale; Quad usa (X1,Y1,X2,Y2); Cubic usa os três pontos.</summary>
public readonly record struct VectorSegment(VectorOp Op, float X1 = 0, float Y1 = 0, float X2 = 0, float Y2 = 0, float X3 = 0, float Y3 = 0);

/// <summary>Um traço (contorno) ou preenchimento de um ícone.</summary>
public sealed record VectorPart(IReadOnlyList<VectorSegment> Segments, bool Filled);

/// <summary>
/// Ícone vetorial em grade 24×24, independente de plataforma: o Android desenha com Path/Canvas e outros hosts podem
/// converter para SVG. Contornos usam traço de 2 unidades com pontas e junções arredondadas.
/// </summary>
public sealed record VectorIcon(string Name, IReadOnlyList<VectorPart> Parts)
{
    public const float Grid = 24f;

    /// <summary>
    /// Lê "M L H V C Q Z" absolutos mais as formas O (círculo: cx cy r) e R (retângulo: x y w h raio). Partes
    /// separadas por '|'; prefixo '~' em uma parte significa preenchida. Números repetidos continuam o último comando.
    /// </summary>
    public static VectorIcon Parse(string name, string definition)
    {
        var parts = new List<VectorPart>();
        foreach (var raw in definition.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool filled = raw.StartsWith('~');
            parts.Add(new VectorPart(ParsePath(name, filled ? raw[1..] : raw), filled));
        }
        if (parts.Count == 0) throw new FormatException($"Ícone '{name}' vazio.");
        return new VectorIcon(name, parts);
    }

    private static List<VectorSegment> ParsePath(string icon, string data)
    {
        var segments = new List<VectorSegment>();
        var tokens = Tokenize(data);
        float cx = 0, cy = 0, sx = 0, sy = 0;
        int i = 0;
        char command = ' ';
        float Next()
        {
            if (i >= tokens.Count || tokens[i].Command is not null) throw new FormatException($"Ícone '{icon}': número esperado em '{data}'.");
            return tokens[i++].Number;
        }
        while (i < tokens.Count)
        {
            if (tokens[i].Command is { } c) { command = c; i++; if (command == 'Z') { Close(); continue; } }
            else if (command is ' ' or 'Z') throw new FormatException($"Ícone '{icon}': comando esperado em '{data}'.");
            switch (command)
            {
                case 'M': { float x = Next(), y = Next(); segments.Add(new(VectorOp.Move, x, y)); cx = sx = x; cy = sy = y; command = 'L'; break; }
                case 'L': { float x = Next(), y = Next(); segments.Add(new(VectorOp.Line, x, y)); cx = x; cy = y; break; }
                case 'H': { float x = Next(); segments.Add(new(VectorOp.Line, x, cy)); cx = x; break; }
                case 'V': { float y = Next(); segments.Add(new(VectorOp.Line, cx, y)); cy = y; break; }
                case 'Q': { float a = Next(), b = Next(), x = Next(), y = Next(); segments.Add(new(VectorOp.Quad, a, b, x, y)); cx = x; cy = y; break; }
                case 'C': { float a = Next(), b = Next(), c2 = Next(), d = Next(), x = Next(), y = Next(); segments.Add(new(VectorOp.Cubic, a, b, c2, d, x, y)); cx = x; cy = y; break; }
                case 'O': { Circle(segments, Next(), Next(), Next()); command = ' '; break; }
                case 'R': { Rect(segments, Next(), Next(), Next(), Next(), Next()); command = ' '; break; }
                default: throw new FormatException($"Ícone '{icon}': comando '{command}' não suportado.");
            }
        }
        return segments;

        void Close() { segments.Add(new(VectorOp.Close)); cx = sx; cy = sy; }
    }

    private const float Kappa = 0.5522847f;

    private static void Circle(List<VectorSegment> s, float cx, float cy, float r)
    {
        float k = r * Kappa;
        s.Add(new(VectorOp.Move, cx + r, cy));
        s.Add(new(VectorOp.Cubic, cx + r, cy + k, cx + k, cy + r, cx, cy + r));
        s.Add(new(VectorOp.Cubic, cx - k, cy + r, cx - r, cy + k, cx - r, cy));
        s.Add(new(VectorOp.Cubic, cx - r, cy - k, cx - k, cy - r, cx, cy - r));
        s.Add(new(VectorOp.Cubic, cx + k, cy - r, cx + r, cy - k, cx + r, cy));
        s.Add(new(VectorOp.Close));
    }

    private static void Rect(List<VectorSegment> s, float x, float y, float w, float h, float r)
    {
        r = Math.Min(r, Math.Min(w, h) / 2);
        float k = r * Kappa, right = x + w, bottom = y + h;
        s.Add(new(VectorOp.Move, x + r, y));
        s.Add(new(VectorOp.Line, right - r, y));
        if (r > 0) s.Add(new(VectorOp.Cubic, right - r + k, y, right, y + r - k, right, y + r));
        s.Add(new(VectorOp.Line, right, bottom - r));
        if (r > 0) s.Add(new(VectorOp.Cubic, right, bottom - r + k, right - r + k, bottom, right - r, bottom));
        s.Add(new(VectorOp.Line, x + r, bottom));
        if (r > 0) s.Add(new(VectorOp.Cubic, x + r - k, bottom, x, bottom - r + k, x, bottom - r));
        s.Add(new(VectorOp.Line, x, y + r));
        if (r > 0) s.Add(new(VectorOp.Cubic, x, y + r - k, x + r - k, y, x + r, y));
        s.Add(new(VectorOp.Close));
    }

    private readonly record struct Token(char? Command, float Number);

    private static List<Token> Tokenize(string data)
    {
        var tokens = new List<Token>();
        for (int i = 0; i < data.Length;)
        {
            char c = data[i];
            if (char.IsWhiteSpace(c) || c == ',') { i++; continue; }
            if (char.IsLetter(c)) { tokens.Add(new Token(char.ToUpperInvariant(c), 0)); i++; continue; }
            int start = i++;
            while (i < data.Length && (char.IsDigit(data[i]) || data[i] == '.')) i++;
            if (!float.TryParse(data.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                throw new FormatException($"Número inválido em '{data}'.");
            tokens.Add(new Token(null, value));
        }
        return tokens;
    }

    /// <summary>SVG equivalente (24×24), útil para pré-visualizar o catálogo e exportar para outros hosts.</summary>
    public string ToSvg(string color = "#e6ecf5", int size = 24)
    {
        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"{color}\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");
        foreach (var part in Parts)
        {
            svg.Append(part.Filled ? $"<path fill=\"{color}\" stroke=\"none\" d=\"" : "<path d=\"");
            foreach (var s in part.Segments)
            {
                svg.Append(s.Op switch
                {
                    VectorOp.Move => string.Create(CultureInfo.InvariantCulture, $"M{s.X1} {s.Y1}"),
                    VectorOp.Line => string.Create(CultureInfo.InvariantCulture, $"L{s.X1} {s.Y1}"),
                    VectorOp.Quad => string.Create(CultureInfo.InvariantCulture, $"Q{s.X1} {s.Y1} {s.X2} {s.Y2}"),
                    VectorOp.Cubic => string.Create(CultureInfo.InvariantCulture, $"C{s.X1} {s.Y1} {s.X2} {s.Y2} {s.X3} {s.Y3}"),
                    _ => "Z"
                });
            }
            svg.Append("\"/>");
        }
        return svg.Append("</svg>").ToString();
    }
}
