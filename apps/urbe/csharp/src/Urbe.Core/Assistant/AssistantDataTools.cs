using System.Text;
using System.Text.Json.Nodes;
using Urbe.Core.Data;

namespace Urbe.Core.Assistant;

/// <summary>
/// CST-207: the agents analyse the vault's data (CSV, JSON, Markdown tables of notes) with reproducible steps instead
/// of reading a whole file into the conversation and guessing. Read-only: the source is never changed; a result is
/// written to a note only through the note tools (with their approvals), ideally as a ```dados block that stays live.
/// </summary>
public static class AssistantDataTools
{
    public const string Name = "query_data";

    public static AiTool Query { get; } = new()
    {
        Name = Name, Access = "read",
        Description = "Consulta dados do vault — CSV, JSON ou a tabela de uma nota (Nota.md, ou Nota.md#Título para a tabela sob um título) — com etapas reproduzíveis, uma por linha: " +
                      "'filtrar valor > 100 e categoria = \"Mercado\"' (= != > < >= <=, contém, começa com; e/ou), 'colunas a, b', 'ordenar por valor desc', " +
                      "'agrupar por categoria: soma(valor) como total, media(valor), contar', 'calcular total = preco * qtd', 'limitar 10', 'distintos coluna', 'resumo'. " +
                      "Sem etapas, mostra colunas, tipos e as primeiras linhas. Use em vez de ler planilhas inteiras. Cite a fonte e as linhas. " +
                      "Para deixar a análise viva numa nota, escreva um bloco ```dados com 'fonte: caminho' e as mesmas etapas.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"source":{"type":"string","description":"Caminho no vault (Planilhas/gastos.csv, dados.json, Livros.md#Lidos)"},"steps":{"type":"string","description":"Etapas, uma por linha (opcional)"},"format":{"type":"string","enum":["markdown","csv","json"],"description":"Formato do resultado (padrão markdown)"},"rows":{"type":"integer","description":"Quantas linhas mostrar (padrão 50; o total sempre é informado)"}},"required":["source"],"additionalProperties":false}""")!,
        Label = i => "Consultando dados: " + ((string?)i["source"] ?? ""),
        Run = async (i, host) => new AiToolOutput(await AnswerAsync(host, (string?)i["source"] ?? "", (string?)i["steps"], (string?)i["format"], (int?)i["rows"]))
    };

    public static async Task<string> AnswerAsync(IAssistantHost host, string source, string? steps, string? format, int? rows)
    {
        if (string.IsNullOrWhiteSpace(source)) return "Informe a fonte (source).";
        var path = source.Trim();
        var hash = path.IndexOf('#');
        var file = hash > 0 ? path[..hash] : path;
        var text = await host.ReadDataAsync(file) ?? (!Path.HasExtension(file) ? await host.ReadDataAsync(file + ".md") : null);
        if (text is null) return "Fonte não encontrada no vault: " + file + ". Use list_notes ou procure o caminho exato.";
        DataResult result;
        try
        {
            var block = "fonte: " + path + "\n" + (steps ?? "");
            result = DataBlock.Run(block, p => p.Equals(file, StringComparison.OrdinalIgnoreCase) || p.Equals(file + ".md", StringComparison.OrdinalIgnoreCase) ? text : null);
        }
        catch (InvalidDataException ex) { return "ERRO: " + ex.Message; }
        var t = result.Table;
        var limit = Math.Clamp(rows ?? 50, 1, 2000);
        var sb = new StringBuilder();
        sb.Append("Fonte: ").Append(t.Source).Append(" · ").Append(t.Rows.Count.ToString("N0", DataTable.PtBr)).Append(" linha(s) · colunas: ")
          .Append(string.Join(", ", t.Columns.Select((c, k) => c + " (" + Type(t.Types[k]) + ")"))).Append('\n');
        if (result.Steps.Count > 0) sb.Append("Etapas: ").Append(string.Join(" → ", result.Steps)).Append('\n');
        foreach (var n in result.Notes) sb.Append("Aviso: ").Append(n).Append('\n');
        var shown = new DataTable(t.Source, t.Columns, t.Types, t.Rows.Take(limit).ToList(), t.SourceRows.Take(limit).ToList());
        sb.Append('\n').Append(format switch { "csv" => shown.ToCsv(), "json" => shown.ToJson(), _ => shown.ToMarkdown() });
        if (t.Rows.Count > limit) sb.Append("\n(").Append(limit).Append(" de ").Append(t.Rows.Count).Append(" linhas mostradas; use etapas para resumir ou rows para ver mais.)");
        if (result.Steps.All(s => !s.StartsWith("agrupar", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("resumo", StringComparison.OrdinalIgnoreCase)))
            sb.Append("\nLinhas de origem: ").Append(string.Join(", ", shown.SourceRows.Take(30))).Append(shown.SourceRows.Count > 30 ? "…" : "");
        return sb.ToString();

        static string Type(DataType d) => d switch { DataType.Number => "número", DataType.Date => "data", DataType.Boolean => "sim/não", _ => "texto" };
    }
}
