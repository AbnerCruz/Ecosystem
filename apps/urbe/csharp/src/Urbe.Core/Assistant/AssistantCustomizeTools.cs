using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>What the appearance tools need from the app's Personalização (customize.js api).</summary>
public interface IAssistantAppearance
{
    /// <summary>get(): the configuration in use.</summary>
    JsonObject Current { get; }
    /// <summary>normalize(config) with the vault's saved themes.</summary>
    LegacyCustomize.Result Normalize(JsonNode config);
    /// <summary>replace(config) = gravar: tema.json written (the vault first) and applied.</summary>
    Task ReplaceAsync(JsonObject config);
    /// <summary>themes(): the saved themes' ids (paths).</summary>
    IReadOnlyList<string> SavedThemes { get; }
    /// <summary>packs(): the texture packs' paths.</summary>
    IReadOnlyList<string> Packs { get; }
}

/// <summary>
/// The Assistente's appearance tools of the 1.8.4 (src/customize/ai-tools.js), ported: appearance_schema (the tema.json
/// format, ready themes, fonts, saved themes, texture packs and the current configuration) and set_appearance (merge or
/// replace, validated before writing, with the diff for approval and undo). plugin_guide is not: the native app runs no
/// JavaScript plugins (ADD-0021). Proved by csharp/tools/assistant-customize-oracle.mjs.
/// </summary>
public static class AssistantCustomizeTools
{
    private static readonly string[] Biomes =
        ["grama", "campo", "prado", "floresta", "mata", "mata-fechada", "pantano", "pântano", "taiga", "tundra", "neve", "colinas", "montanha", "pico", "deserto", "savana", "estepe", "praia", "mar", "profundo", "oceano", "rio", "lago"];

    private static readonly string[] Tokens =
        ["--ui-bg", "--ui-surface", "--ui-surface-2", "--ui-surface-3", "--ui-line", "--ui-line-2", "--ui-text", "--ui-text-2", "--ui-text-3", "--ui-accent", "--ui-danger", "--ui-ok", "--ui-warn", "--ui-code"];

    /// <summary>customize.js schemaText(): the format of tema.json, saved themes, texture packs, styles and plugins.</summary>
    public static string SchemaText() => string.Join("\n",
    [
        "ARQUIVO: " + LegacyCustomize.FilePath + " (JSON). Só escreva o que quiser mudar; o resto usa o padrão. Salvar o arquivo aplica na hora.",
        "{",
        "  \"tema\": \"" + string.Join("\" | \"", LegacyCustomize.Presets) + "\" | \"Personalização/temas/<nome>.json\",",
        "  \"cores\": { " + string.Join(", ", LegacyCustomize.Colors.Select(k => "\"" + k + "\": \"#rrggbb\"")) + " },   // sobrepõem as do tema; trocar \"fundo\" decide se é claro ou escuro",
        "  \"texto\": { \"fonte\": \"" + string.Join("\" | \"", LegacyCustomize.Fonts) + "\" | \"nome de fonte\", \"fonteEditor\": (igual), \"tamanho\": 12-22, \"tamanhoEditor\": 13-28, \"alturaLinha\": 1.2-2.2 },",
        "  \"forma\": { \"cantos\": \"retos\" | \"suaves\" | \"redondos\", \"densidade\": \"compacta\" | \"normal\" | \"confortavel\", \"vidro\": true|false },",
        "  \"animacoes\": \"sistema\" | \"ligadas\" | \"reduzidas\",",
        "  \"editor\": { \"largura\": \"estreita\" | \"media\" | \"larga\" | \"total\", \"modoInicial\": \"visual\" | \"texto\", \"ortografia\": true|false },",
        "  \"cidade\": { \"moradores\": bool, \"fauna\": bool, \"clima\": bool, \"eventos\": bool, \"nomes\": bool, \"bairros\": bool, \"ambiente\": \"dia\" | \"entardecer\" | \"noite\" | \"auto\" | \"ciclo\",",
        "              \"texturas\": \"Personalização/texturas/<pacote>.json\" | \"\", \"paleta\": { \"<bioma>\": [\"#base\", \"#sombra\", \"#luz\", \"#detalhe\"] } },",
        "  \"estilos\": { \"Personalização/estilos/<arquivo>.css\": true|false },",
        "  \"css\": \"regras CSS extras aplicadas por último\"",
        "}",
        "Biomas: " + string.Join(", ", Biomes) + ", agua (rio+lago+mar).",
        "",
        "TEMA SALVO (Personalização/temas/<nome>.json): { \"nome\": \"...\", \"claro\": bool, \"base\": \"<tema pronto>\", \"cores\": { ...mesmas chaves de \"cores\"... } }",
        "",
        "PACOTE DE TEXTURAS (Personalização/texturas/<nome>.json):",
        "{ \"nome\": \"...\",",
        "  \"paleta\": { \"<bioma>\": [\"#base\", ...] },",
        "  \"chao\": { \"<bioma>\": { \"cores\": { \"a\": \"#rrggbb\", \"b\": \"#rrggbb\" }, \"pixels\": [\"16 letras\", ... 16 linhas] }  |  { \"imagem\": \"data:image/png;base64,...\" }  |  [até 4 variações] },",
        "  \"construcoes\": { \"casa\" | \"salao\" | \"oficina\" | \"tinturaria\" | \"torre\" | \"mercado\" | \"armazem\": { \"imagem\": \"data:...\" } | { \"cores\": {...}, \"pixels\": [...] } } }",
        "Pixels: cada letra é uma cor de \"cores\"; \".\" usa a cor base do bioma. Grades de até 64×64 são reduzidas para 16×16 no chão.",
        "Construções: desenho de 48×56 (3×3,5 tiles) fica igual às originais; outros tamanhos são esticados.",
        "",
        "ESTILOS: arquivos .css em Personalização/estilos/. Variáveis úteis: " + string.Join(", ", Tokens) + ", --ui-font, --ui-editor-font, --ui-editor-size, --ui-r-m.",
        "PLUGINS: arquivos .js em Personalização/plugins/ — veja plugin_guide."
    ]);

    private static IAssistantAppearance C(IAssistantHost host) => host.Appearance ?? throw new AiToolException("Personalização indisponível.");

    private static JsonObject Merge(JsonObject a, JsonObject b)
    {
        var o = (JsonObject)a.DeepClone();
        foreach (var (k, v) in b)
            o[k] = v is JsonObject bo && o[k] is JsonObject ao ? Merge(ao, bo) : v?.DeepClone();
        return o;
    }

    private static LegacyCustomize.Result Build(JsonObject i, IAssistantHost host)
    {
        var c = C(host);
        var replace = Urbe.Core.Pages.PageEngine.Truthy(i["replace"]);
        var base0 = replace ? new JsonObject() : c.Current;
        var alvo = i["config"];
        if (alvo is JsonValue sv && sv.TryGetValue<string>(out var text))
        {
            try { alvo = JsonNode.Parse(text); }
            catch (JsonException e) { throw new AiToolException("config não é JSON válido: " + e.Message); }
        }
        if (alvo is not JsonObject target) throw new AiToolException("Envie config: objeto com as partes do tema.json a mudar.");
        var n = c.Normalize(Merge(base0, target));
        if (n.Errors.Count > 0)
            throw new AiToolException("O tema tem " + n.Errors.Count + " erro(s); corrija e envie de novo:\n" +
                string.Join("\n", n.Errors.Select(x => "- " + (x.Path.Length > 0 ? x.Path : "(arquivo)") + ": " + x.Message)) + "\n(Chame appearance_schema para ver o formato.)");
        return n;
    }

    private static JsonObject Schema(JsonObject properties, params string[] required)
    {
        var o = new JsonObject { ["type"] = "object", ["properties"] = properties };
        if (required.Length > 0) o["required"] = new JsonArray(required.Select(r => (JsonNode)JsonValue.Create(r)).ToArray());
        o["additionalProperties"] = false;
        return o;
    }

    public static IReadOnlyList<AiTool> All { get; } =
    [
        new AiTool
        {
            Name = "appearance_schema", Access = "read",
            Description = "Formato completo da personalização do Urbe: tema.json (tema, cores, fontes, forma, editor, cidade, texturas, estilos), temas salvos e pacotes de texturas. Mostra também a configuração atual. Chame antes de mudar a aparência.",
            Parameters = Schema(new JsonObject()),
            Label = _ => "Lendo o formato da personalização",
            Run = (_, host) =>
            {
                var c = C(host);
                var presets = string.Join(", ", LegacyCustomizeTheme.Presets.Select(p => p.Id + " (" + p.Name + (p.Light ? ", claro" : ", escuro") + ")"));
                var themes = c.SavedThemes.Count > 0 ? string.Join(", ", c.SavedThemes) : "nenhum";
                var packs = c.Packs.Count > 0 ? string.Join(", ", c.Packs) : "nenhum";
                return Task.FromResult(new AiToolOutput(SchemaText() + "\n\nTEMAS PRONTOS: " + presets + "\nFONTES: " + string.Join(", ", LegacyCustomize.Fonts) +
                    "\nTEMAS SALVOS: " + themes + "\nPACOTES DE TEXTURAS: " + packs + "\n\nCONFIGURAÇÃO ATUAL:\n" + LegacyCustomize.Stringify(c.Current, 2)));
            }
        },
        new AiTool
        {
            Name = "set_appearance", Access = "write",
            Description = "Muda a aparência do Urbe gravando o tema.json (validado). Envie só o que muda em config, ex.: {\"tema\":\"sepia\",\"texto\":{\"fonte\":\"serifada\"}}. Use replace=true para substituir tudo em vez de mesclar. Aplica na hora; o usuário pode desfazer.",
            Parameters = Schema(new JsonObject
            {
                ["config"] = new JsonObject { ["type"] = "object", ["description"] = "Partes do tema.json no formato de appearance_schema", ["additionalProperties"] = true },
                ["replace"] = new JsonObject { ["type"] = "boolean", ["description"] = "true = substitui a configuração inteira" }
            }, "config"),
            Label = _ => "Mudando a aparência",
            Preview = (i, host) =>
            {
                var n = Build(i, host);
                var d = host.Documents.Get(LegacyCustomize.FilePath);
                return new AiPreview(LegacyCustomize.FilePath, d?.Content, LegacyCustomize.Stringify(n.Config, 2) + "\n", "Aparência · tema " + LegacyCustomize.JsToString(n.Config["tema"]));
            },
            Run = async (i, host) =>
            {
                var c = C(host);
                var n = Build(i, host);
                var d = host.Documents.Get(LegacyCustomize.FilePath);
                var before = d is null ? null : new AiNoteSnap(d.Path, d.Content);
                await c.ReplaceAsync(n.Config);
                var u = host.Documents.Get(LegacyCustomize.FilePath);
                var content = "Aparência aplicada (tema " + LegacyCustomize.JsToString(n.Config["tema"]) + ")." +
                              (n.Warnings.Count > 0 ? "\nAvisos: " + string.Join("; ", n.Warnings.Select(w => w.Path + " " + w.Message)) : "");
                return new AiToolOutput(content, u is null ? [] : [new AiChange(u.Id, before, new AiNoteSnap(u.Path, u.Content))]);
            }
        }
    ];
}
