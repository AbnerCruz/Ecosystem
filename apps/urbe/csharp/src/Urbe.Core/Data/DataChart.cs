using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Urbe.Core.Calculus;

namespace Urbe.Core.Data;

/// <summary>What a chart step asked for: the kind, the label column and the value columns.</summary>
public sealed record ChartSpec(string Kind, string? Label, IReadOnlyList<string> Values, string? Title);

/// <summary>
/// CST-208: charts drawn from data, as a deterministic SVG — the last step of a <c>```dados</c> block:
/// <c>gráfico barras categoria total</c>, <c>gráfico linhas mês receita despesa</c>, <c>gráfico pizza categoria total</c>,
/// <c>gráfico dispersão altura peso</c>, optionally <c>título: …</c> after it. The chart is the block's text: editing
/// the steps edits the chart. Long labels are shortened (with the full text kept for screen readers), values get
/// their own axis with round ticks, every series a colour that reads on white and dark.
/// </summary>
public static partial class DataChart
{
    public static readonly string[] Colors = ["#4f8cff", "#e5484d", "#30a46c", "#f5a524", "#8e4ec6", "#12a594", "#d6409f", "#5b5bd6"];

    public static ChartSpec? Parse(string step)
    {
        var m = StepRe().Match(step.Trim());
        if (!m.Success) return null;
        var kind = DataTable.Fold(m.Groups["kind"].Value) switch
        {
            "barras" or "barra" or "bar" or "bars" or "colunas" => "barras",
            "linhas" or "linha" or "line" or "lines" => "linhas",
            "pizza" or "pie" or "setores" => "pizza",
            "dispersao" or "scatter" or "pontos" => "dispersao",
            var other => throw new InvalidDataException("Gráfico desconhecido: " + other + ". Use barras, linhas, pizza ou dispersão.")
        };
        var rest = m.Groups["rest"].Value;
        string? title = null;
        var t = TitleRe().Match(rest);
        if (t.Success) { title = t.Groups[1].Value.Trim().Trim('"'); rest = rest[..t.Index]; }
        var cols = Regex.Matches(rest, @"""([^""]+)""|[^\s,]+").Select(x => x.Groups[1].Success ? x.Groups[1].Value : x.Value).ToList();
        if (cols.Count == 0) throw new InvalidDataException("Diga as colunas do gráfico: gráfico barras categoria total");
        return cols.Count == 1 ? new ChartSpec(kind, null, cols, title) : new ChartSpec(kind, cols[0], cols.Skip(1).ToList(), title);
    }

    public static bool IsChartStep(string step) => StepRe().IsMatch(step.Trim());

    public static string ToSvg(DataTable t, ChartSpec spec, int width = 640, int height = 400)
    {
        int Col(string name)
        {
            var i = t.ColumnIndex(name);
            return i >= 0 ? i : throw new InvalidDataException("Não há coluna “" + name + "” para o gráfico. Colunas: " + string.Join(", ", t.Columns) + ".");
        }
        var labelCol = spec.Label is null ? -1 : Col(spec.Label);
        var valueCols = spec.Values.Select(Col).ToList();
        foreach (var c in valueCols)
            if (t.Types[c] != DataType.Number) throw new InvalidDataException("A coluna “" + t.Columns[c] + "” não é numérica para o gráfico.");
        if (t.Rows.Count == 0) throw new InvalidDataException("Não há linhas para desenhar.");
        if (t.Rows.Count > 2000) throw new InvalidDataException("Linhas demais para um gráfico (" + t.Rows.Count + "); agrupe ou limite antes.");
        var labels = t.Rows.Select((r, i) => labelCol < 0 ? (i + 1).ToString(CultureInfo.InvariantCulture) : r[labelCol].Text).ToList();
        var series = valueCols.Select(c => t.Rows.Select(r => r[c].Number ?? double.NaN).ToArray()).ToList();
        var names = valueCols.Select(c => t.Columns[c]).ToList();
        var sb = new StringBuilder();
        var aria = (spec.Title ?? "Gráfico de " + string.Join(", ", names)) + ": " + string.Join("; ", labels.Take(30).Select((l, i) => l + " " + string.Join("/", series.Select(s => Num(s[i])))));
        sb.Append("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 ").Append(width).Append(' ').Append(height).Append("' width='").Append(width).Append("' height='").Append(height)
          .Append("' role='img' aria-label='").Append(Esc(aria)).Append("' font-family='sans-serif' font-size='12'>");
        sb.Append("<rect x='0' y='0' width='").Append(width).Append("' height='").Append(height).Append("' fill='#ffffff'/>");
        double top = spec.Title is null ? 14 : 34;
        if (spec.Title is { } title) sb.Append("<text x='").Append(width / 2).Append("' y='22' text-anchor='middle' font-size='15' font-weight='600' fill='#25272c'>").Append(Esc(title)).Append("</text>");
        switch (spec.Kind)
        {
            case "pizza": Pie(sb, labels, series[0], width, height, top); break;
            case "dispersao":
                if (labelCol < 0 || t.Types[labelCol] != DataType.Number) throw new InvalidDataException("Na dispersão, a primeira coluna é o eixo x e precisa ser numérica.");
                Axes(sb, t.Rows.Select(r => r[labelCol].Number ?? double.NaN).ToArray(), labels, series, names, width, height, top, "dispersao");
                break;
            default: Axes(sb, null, labels, series, names, width, height, top, spec.Kind); break;
        }
        return sb.Append("</svg>").ToString();
    }

    private static void Axes(StringBuilder sb, double[]? xs, List<string> labels, List<double[]> series, List<string> names, int width, int height, double top, string kind)
    {
        var all = series.SelectMany(s => s).Where(double.IsFinite).ToList();
        if (all.Count == 0) throw new InvalidDataException("Nenhum valor numérico para desenhar.");
        double min = Math.Min(0, all.Min()), max = Math.Max(0, all.Max());
        if (kind == "dispersao" || kind == "linhas")
        {
            // 5% of room above and below: no point sits on the frame
            min = all.Min(); max = all.Max();
            var pad = (max - min) * .05;
            min -= pad; max += pad;
        }
        if (max == min) { max += 1; min -= kind == "barras" ? 0 : 1; }
        var ticks = FunctionPlot.Ticks(min, max, 5).Where(v => v >= min - 1e-9 && v <= max + 1e-9).ToList();
        if (ticks.Count == 0) ticks = [min, max];
        double legend = series.Count > 1 ? 22 : 0;
        var rotate = kind != "dispersao" && labels.Count > 6 || labels.Any(l => l.Length > 10);
        // room for the widest tick label (≈ .62 em per digit at 12 px) and for the last x label
        double left = 16 + 7.5 * ticks.Max(v => Num(v).Length), right = 18, bottom = (rotate ? 70 : 30) + legend;
        double w = width - left - right, h = height - top - bottom;
        string Y(double v) => F(top + (max - v) / (max - min) * h);
        sb.Append("<g stroke='#25272c' stroke-opacity='.12'>");
        foreach (var tk in ticks) sb.Append("<line x1='").Append(F(left)).Append("' x2='").Append(F(left + w)).Append("' y1='").Append(Y(tk)).Append("' y2='").Append(Y(tk)).Append("'/>");
        sb.Append("</g><g fill='#25272c' fill-opacity='.7' text-anchor='end'>");
        foreach (var tk in ticks) sb.Append("<text x='").Append(F(left - 6)).Append("' y='").Append(Y(tk)).Append("' dy='4'>").Append(Esc(Num(tk))).Append("</text>");
        sb.Append("</g><line x1='").Append(F(left)).Append("' x2='").Append(F(left + w)).Append("' y1='").Append(Y(Math.Max(min, Math.Min(0, max)))).Append("' y2='").Append(Y(Math.Max(min, Math.Min(0, max)))).Append("' stroke='#25272c' stroke-opacity='.5'/>");
        int n = labels.Count;
        if (kind == "dispersao")
        {
            var xv = xs!.Where(double.IsFinite).ToList();
            double xmin = xv.Min(), xmax = xv.Max();
            if (xmax == xmin) { xmax += 1; xmin -= 1; }
            var xpad = (xmax - xmin) * .05;
            xmin -= xpad; xmax += xpad;
            var xt = FunctionPlot.Ticks(xmin, xmax, 6);
            string X(double v) => F(left + (v - xmin) / (xmax - xmin) * w);
            sb.Append("<g fill='#25272c' fill-opacity='.7' text-anchor='middle'>");
            foreach (var tk in xt.Where(v => v >= xmin && v <= xmax)) sb.Append("<text x='").Append(X(tk)).Append("' y='").Append(F(top + h + 16)).Append("'>").Append(Esc(Num(tk))).Append("</text>");
            sb.Append("</g>");
            for (int s = 0; s < series.Count; s++)
                for (int i = 0; i < n; i++)
                    if (double.IsFinite(xs![i]) && double.IsFinite(series[s][i]))
                        sb.Append("<circle cx='").Append(X(xs[i])).Append("' cy='").Append(Y(series[s][i])).Append("' r='4' fill='").Append(Colors[s % Colors.Length]).Append("' fill-opacity='.8'/>");
        }
        else
        {
            double slot = w / n;
            for (int i = 0; i < n; i++)
            {
                var cx = left + slot * (i + .5);
                var label = Short(labels[i], rotate ? 16 : Math.Max(4, (int)(slot / 7)));
                if (rotate) sb.Append("<text transform='translate(").Append(F(cx)).Append(' ').Append(F(top + h + 12)).Append(") rotate(-40)' text-anchor='end' fill='#25272c' fill-opacity='.8'>");
                else sb.Append("<text x='").Append(F(cx)).Append("' y='").Append(F(top + h + 16)).Append("' text-anchor='middle' fill='#25272c' fill-opacity='.8'>");
                sb.Append("<title>").Append(Esc(labels[i])).Append("</title>").Append(Esc(label)).Append("</text>");
            }
            if (kind == "barras")
            {
                double group = slot * .8, bar = group / series.Count;
                for (int s = 0; s < series.Count; s++)
                    for (int i = 0; i < n; i++)
                    {
                        var v = series[s][i];
                        if (!double.IsFinite(v)) continue;
                        double x = left + slot * i + slot * .1 + bar * s, y0 = Math.Max(min, Math.Min(0, max));
                        double ya = (top + (max - Math.Max(v, y0)) / (max - min) * h), yb = (top + (max - Math.Min(v, y0)) / (max - min) * h);
                        sb.Append("<rect x='").Append(F(x)).Append("' y='").Append(F(ya)).Append("' width='").Append(F(Math.Max(1, bar - 2))).Append("' height='").Append(F(Math.Max(0.5, yb - ya)))
                          .Append("' fill='").Append(Colors[s % Colors.Length]).Append("'><title>").Append(Esc(labels[i] + ": " + Num(v))).Append("</title></rect>");
                    }
            }
            else
                for (int s = 0; s < series.Count; s++)
                {
                    var points = Enumerable.Range(0, n).Where(i => double.IsFinite(series[s][i])).Select(i => F(left + slot * (i + .5)) + " " + Y(series[s][i])).ToList();
                    sb.Append("<polyline fill='none' stroke-width='2.5' stroke-linejoin='round' stroke='").Append(Colors[s % Colors.Length]).Append("' points='").Append(string.Join(" ", points)).Append("'/>");
                    foreach (var p in points) { var xy = p.Split(' '); sb.Append("<circle cx='").Append(xy[0]).Append("' cy='").Append(xy[1]).Append("' r='3' fill='").Append(Colors[s % Colors.Length]).Append("'/>"); }
                }
        }
        if (series.Count > 1)
        {
            double x = left;
            for (int s = 0; s < series.Count; s++)
            {
                sb.Append("<rect x='").Append(F(x)).Append("' y='").Append(F(height - 16)).Append("' width='10' height='10' fill='").Append(Colors[s % Colors.Length]).Append("'/>")
                  .Append("<text x='").Append(F(x + 14)).Append("' y='").Append(F(height - 7)).Append("' fill='#25272c'>").Append(Esc(names[s])).Append("</text>");
                x += 24 + 7 * names[s].Length;
            }
        }
    }

    private static void Pie(StringBuilder sb, List<string> labels, double[] values, int width, int height, double top)
    {
        if (values.Any(v => v < 0)) throw new InvalidDataException("Um gráfico de pizza não tem valores negativos.");
        var total = values.Where(double.IsFinite).Sum();
        if (total <= 0) throw new InvalidDataException("A soma dos valores é zero: não há pizza para desenhar.");
        double r = Math.Min(width * .24, (height - top - 16) / 2), cx = width * .27, cy = top + (height - top) / 2;
        double angle = -Math.PI / 2;
        for (int i = 0; i < values.Length; i++)
        {
            if (!double.IsFinite(values[i]) || values[i] == 0) continue;
            var sweep = values[i] / total * 2 * Math.PI;
            var color = Colors[i % Colors.Length];
            if (sweep >= 2 * Math.PI - 1e-9) sb.Append("<circle cx='").Append(F(cx)).Append("' cy='").Append(F(cy)).Append("' r='").Append(F(r)).Append("' fill='").Append(color).Append("'/>");
            else
            {
                double x1 = cx + r * Math.Cos(angle), y1 = cy + r * Math.Sin(angle), x2 = cx + r * Math.Cos(angle + sweep), y2 = cy + r * Math.Sin(angle + sweep);
                sb.Append("<path d='M").Append(F(cx)).Append(' ').Append(F(cy)).Append(" L").Append(F(x1)).Append(' ').Append(F(y1)).Append(" A").Append(F(r)).Append(' ').Append(F(r))
                  .Append(" 0 ").Append(sweep > Math.PI ? 1 : 0).Append(" 1 ").Append(F(x2)).Append(' ').Append(F(y2)).Append(" Z' fill='").Append(color).Append("' stroke='#ffffff' stroke-width='1.5'><title>")
                  .Append(Esc(labels[i] + ": " + Num(values[i]))).Append("</title></path>");
            }
            angle += sweep;
        }
        // the legend says the values and the share (colour is never the only cue)
        double ly = top + 12;
        for (int i = 0; i < values.Length && ly < top + (height - top) - 4; i++, ly += 20)
        {
            var share = " — " + Num(values[i]) + " (" + (values[i] / total * 100).ToString("0.#", DataTable.PtBr) + "%)";
            // the label gives way so the value and the share always fit (≈ 9 px per character at 12 px, conservative across fonts)
            var room = (int)((width - (width * .54 + 18) - 8) / 9.0) - share.Length;
            sb.Append("<rect x='").Append(F(width * .54)).Append("' y='").Append(F(ly - 10)).Append("' width='12' height='12' fill='").Append(Colors[i % Colors.Length]).Append("'/>")
              .Append("<text x='").Append(F(width * .54 + 18)).Append("' y='").Append(F(ly)).Append("' fill='#25272c'><title>").Append(Esc(labels[i] + share)).Append("</title>")
              .Append(Esc(Short(labels[i], Math.Max(3, room)) + share)).Append("</text>");
        }
    }

    private static string Short(string s, int max) => s.Length <= max ? s : s[..Math.Max(1, max - 1)] + "…";
    private static string Num(double v) => double.IsFinite(v) ? MathFormat.Number(Math.Round(v, 6)) : "";
    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("'", "&#39;");

    [GeneratedRegex(@"^(?:gr[aá]fico|chart)\s+(?<kind>\S+)\s*(?<rest>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex StepRe();
    [GeneratedRegex(@"\b(?:t[ií]tulo|title)\s*:\s*(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TitleRe();
}
