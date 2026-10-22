using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core.Tests;

/// <summary>
/// The 1.8.4's texts that spoke of the city, as the native app says them since the city left it (ADD-0028 / DEC-0051).
/// The oracles turn the original's output into the native one with exactly these replacements and compare everything
/// else byte for byte, so nothing else may drift.
/// </summary>
internal static class WithoutTheCity
{
    private static readonly (string Original, string Native)[] Texts =
    [
        ("um workspace local de notas em Markdown em que cada nota é uma casa numa cidade, pastas são bairros e links [[Título]] entre notas viram ruas.",
         "um workspace local de notas em Markdown, organizadas em pastas e ligadas por links [[Título]]."),
        ("Cria uma pasta (que vira um bairro na cidade).", "Cria uma pasta no vault."),
        ("Botão na cidade que abre (ou cria) a nota do dia", "Botão que abre (ou cria) a nota do dia"),
    ];

    /// <summary>The keymap's command the native app no longer has (Mod+Shift+M: "Voltar para a cidade").</summary>
    public const string WorldCommand = "workspace.navigate.world";

    /// <summary>The plugin template that drew over the city's houses ("Desenho no mapa").</summary>
    public const string MapTemplate = "mapa";

    /// <summary>
    /// appearance_schema without the city's part of the format: the "cidade" options, the biomes and the texture packs
    /// (their lines, and their words in the tool's description).
    /// </summary>
    public static string Appearance(string original)
    {
        original = original.Replace("tema.json (tema, cores, fontes, forma, editor, cidade, texturas, estilos), temas salvos e pacotes de texturas.",
            "tema.json (tema, cores, fontes, forma, editor, estilos) e temas salvos.", StringComparison.Ordinal);
        var lines = original.Split('\n').ToList();
        var output = new List<string>();
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            // the schema's "cidade" line and its "texturas"/"paleta" line (the current configuration below is left as it is)
            if (l.StartsWith("  \"cidade\": {", StringComparison.Ordinal) && i + 1 < lines.Count && lines[i + 1].TrimStart().StartsWith("\"texturas\"", StringComparison.Ordinal)) { i++; continue; }
            if (l.StartsWith("Biomas: ", StringComparison.Ordinal) || l.StartsWith("PACOTES DE TEXTURAS: ", StringComparison.Ordinal)) continue;
            if (l.StartsWith("PACOTE DE TEXTURAS", StringComparison.Ordinal))
            {
                while (i + 1 < lines.Count && lines[i + 1].Length > 0) i++;
                i++;   // the blank line after the block
                continue;
            }
            output.Add(l);
        }
        return string.Join("\n", output);
    }

    private static readonly JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Native(string original)
    {
        foreach (var (from, to) in Texts) original = original.Replace(from, to, StringComparison.Ordinal);
        return original;
    }

    public static JsonNode? Native(JsonNode? original) =>
        original is null ? null : JsonNode.Parse(Native(original.ToJsonString(Relaxed)));
}
