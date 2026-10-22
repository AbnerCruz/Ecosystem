using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core.Plugins;

namespace Urbe.Core.Assistant;

/// <summary>What the plugin tools need from the app: the plugins of the vault and turning one on (this exact text).</summary>
public interface IAssistantPlugins
{
    IReadOnlyList<(string Path, string Name, string Status)> List();
    void Enable(string path);
}

/// <summary>
/// The Assistente's plugin tools for the native plugin API (owner, 15/10/2026: any idea of plugin must be possible):
/// plugin_guide (the .plugin.json format, items, actions and functions, and the vault's plugins) and write_plugin, which
/// checks the plugin before writing, shows it for approval (always asked, access "plugin") and, approved, writes it to
/// Personalização/plugins and turns it on. The plugin then does only what it declared (deny-by-default, NN-016).
/// </summary>
public static class AssistantPluginTools
{
    private static IAssistantPlugins P(IAssistantHost host) => host.Plugins ?? throw new AiToolException("Plugins indisponíveis neste app.");

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>"Cronômetro" → "Personalização/plugins/Cronômetro.plugin.json".</summary>
    public static string PluginPath(string? raw)
    {
        var p = (raw ?? "").Trim().Replace('\\', '/').Trim('/');
        if (p.Length == 0) throw new AiToolException("Informe path (ex.: \"Cronômetro\" ou \"Personalização/plugins/Cronômetro.plugin.json\").");
        var file = p[(p.LastIndexOf('/') + 1)..];
        if (file.EndsWith(VaultPluginSpec.Extension, StringComparison.OrdinalIgnoreCase)) file = file[..^VaultPluginSpec.Extension.Length];
        else if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) file = file[..file.LastIndexOf('.')];
        file = System.Text.RegularExpressions.Regex.Replace(file, @"[\\/:*?""<>|\u0000-\u001f]", "-").Trim();
        if (file.Length == 0 || file.StartsWith('.')) throw new AiToolException("Nome de plugin inválido: " + raw);
        return LegacyPlugins.Folder + file + VaultPluginSpec.Extension;
    }

    private static (string Path, string Text, VaultPluginSpec Spec) Build(JsonObject i)
    {
        var path = PluginPath(LegacyCustomize.JsToString(i["path"]));
        var node = i["plugin"];
        if (node is JsonValue sv && sv.TryGetValue<string>(out var str))
        {
            try { node = JsonNode.Parse(str); }
            catch (JsonException e) { throw new AiToolException("plugin não é JSON válido: " + e.Message); }
        }
        if (node is not JsonObject o) throw new AiToolException("Envie plugin: o objeto completo do .plugin.json (veja plugin_guide).");
        var text = o.ToJsonString(Pretty) + "\n";
        var (spec, errors) = VaultPluginSpec.Parse(path, text);
        if (spec is null)
            throw new AiToolException("O plugin tem " + errors.Count + " erro(s); corrija e envie de novo:\n" + string.Join("\n", errors.Take(30).Select(e => "- " + e)) + "\n(Chame plugin_guide para ver o formato.)");
        return (path, text, spec);
    }

    public static IReadOnlyList<AiTool> All { get; } =
    [
        new AiTool
        {
            Name = "plugin_guide", Access = "read",
            Description = "Como criar plugins do Urbe (arquivos .plugin.json em Personalização/plugins/): formato completo, itens do painel, ações, funções, permissões e um exemplo; lista também os plugins deste vault. Chame antes de criar ou mudar um plugin; depois use write_plugin.",
            Parameters = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false },
            Label = _ => "Lendo o guia de plugins",
            Run = (_, host) =>
            {
                var list = host.Plugins?.List() ?? [];
                return Task.FromResult(new AiToolOutput(VaultPluginSpec.Guide() + "\n\nPLUGINS NESTE VAULT:\n" +
                    (list.Count > 0 ? string.Join("\n", list.Select(x => "- " + x.Path + " (" + x.Name + ", " + x.Status + ")")) : "nenhum") +
                    "\n\nPara mudar um plugin existente: read_note no arquivo, altere e envie tudo com write_plugin."));
            }
        },
        new AiTool
        {
            Name = "write_plugin", Access = "plugin",
            Description = "Cria ou substitui um plugin do Urbe (Personalização/plugins/<nome>.plugin.json) e o liga depois que o usuário aprovar o código. Valida antes e devolve os erros se houver. Qualquer ideia de plugin cabe aqui: painéis, cronômetros, contadores, listas, botões na cidade ou no editor, comandos, anotações em notas, eventos. Chame plugin_guide antes.",
            Parameters = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject { ["type"] = "string", ["description"] = "Nome ou caminho, ex.: \"Cronômetro\" (vira Personalização/plugins/Cronômetro.plugin.json)" },
                    ["plugin"] = new JsonObject { ["type"] = "object", ["description"] = "O .plugin.json completo no formato de plugin_guide", ["additionalProperties"] = true }
                },
                ["required"] = new JsonArray("path", "plugin"), ["additionalProperties"] = false
            },
            Label = i =>
            {
                var name = i["plugin"] is JsonObject o && o["nome"] is JsonValue n && n.TryGetValue<string>(out var s) ? s : LegacyCustomize.JsToString(i["path"]);
                return "Plugin " + name;
            },
            Preview = (i, host) =>
            {
                var (path, text, spec) = Build(i);
                var d = host.Documents.Get(path);
                var perms = spec.Declared.Count == 0 ? "nenhuma permissão" : "pode: " + string.Join(", ", spec.Declared);
                return new AiPreview(path, d?.Content, text, (d is null ? "Novo plugin" : "Substitui o plugin") + " “" + spec.Name + "” · " + perms + " · liga ao aprovar");
            },
            Run = async (i, host) =>
            {
                var plugins = P(host);
                var (path, text, spec) = Build(i);
                var d = host.Documents.Get(path);
                var before = d is null ? null : new AiNoteSnap(d.Path, d.Content);
                var u = d is null ? await host.CreateNoteAsync(path, text) : await host.SaveNoteAsync(d, text);
                plugins.Enable(path);
                var where = new List<string>();
                if (spec.Panel is not null) where.Add("um painel");
                foreach (var b in spec.Buttons.OfType<JsonObject>())
                    where.Add("o botão “" + VaultPluginSpec.Str(b["titulo"]) + "” " + (VaultPluginSpec.Str(b["onde"]) == "editor" ? "no topo do editor" : "no topo do Explorer"));
                foreach (var c in spec.Commands.OfType<JsonObject>()) where.Add("o comando “" + VaultPluginSpec.Str(c["titulo"]) + "”");
                return new AiToolOutput((d is null ? "Plugin criado e ligado: " : "Plugin atualizado e ligado: ") + path + " (" + spec.Name + ")." +
                    (where.Count > 0 ? " Ele aparece como " + string.Join(", ", where) + "." : "") +
                    " Pode ser desligado em Personalização → Plugins.", [new AiChange(u.Id, before, new AiNoteSnap(u.Path, u.Content))]);
            }
        }
    ];
}
