using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Urbe.Core.Pages;

namespace Urbe.Core.Assistant;

/// <summary>
/// The Assistente's page tools of the 1.8.4 (src/pages/ai-tools.js), ported: page_schema (the .page.json format, the
/// ready templates and the themes) and write_page (a full spec or a template, checked before writing, with the diff
/// for approval and undo). Proved by csharp/tools/assistant-pages-oracle.mjs.
/// </summary>
public static class AssistantPageTools
{
    /// <summary>The engine and the date the tools use (tests fix the date).</summary>
    public static Func<DateTime> Now { get; set; } = () => DateTime.Now;

    private static string PagePath(string? raw)
    {
        var p = Regex.Replace((raw ?? "").Trim().Replace('\\', '/'), @"^/+|/+$", "");
        if (p.Length == 0) throw new AiToolException("Informe path (ex.: \"Páginas/Portfólio.page.json\").");
        if (!Regex.IsMatch(p, @"\.(page|template)\.json$", RegexOptions.IgnoreCase)) p = Regex.Replace(p, @"\.(json|md|markdown|html?)$", "", RegexOptions.IgnoreCase) + ".page.json";
        if (!p.Contains('/')) p = "Páginas/" + p;
        if (Regex.IsMatch(p, @"(^|/)\.\.?(/|$)") || Regex.IsMatch(p, "[:*?\"<>|\u0000-\u001f]")) throw new AiToolException("Caminho inválido: " + p);
        return p;
    }

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static PageNormalized Build(JsonObject i, IAssistantHost host)
    {
        var engine = new PageEngine(Now().Year);
        JsonNode? spec = i["spec"];
        var template = Str(i["template"]);
        if (spec is null && !string.IsNullOrEmpty(template))
        {
            if (PageTemplates.Get(template) is null)
                throw new AiToolException("Modelo \"" + template + "\" não existe. Modelos: " + string.Join(", ", PageTemplates.List().Select(t => t.Id)));
            var title = Str(i["title"]) ?? "";
            (string, string)? note = null;
            if (Str(i["note"]) is { Length: > 0 } n)
            {
                var d = host.Documents.Get(n) ?? host.Documents.Get(n + ".md") ?? throw new AiToolException("Nota não encontrada: " + n);
                note = (d.Title, d.Path);
            }
            var built = PageTemplates.Build(engine, template, title, Str(i["folder"]) ?? "", note, Now())!;
            if (title.Length > 0)
            {
                ((JsonObject)built["meta"]!)["title"] = title;
                if (built["layout"] is JsonObject layout && Urbe.Core.Pages.PageEngine.Truthy(layout["brand"])) layout["brand"] = title;
            }
            spec = built;
        }
        if (spec is null) throw new AiToolException("Envie spec (o JSON completo da página) ou template.");
        if (spec is JsonValue sv && sv.TryGetValue<string>(out var text))
        {
            try { spec = JsonNode.Parse(text); }
            catch (JsonException e) { throw new AiToolException("spec não é JSON válido: " + e.Message); }
        }
        var normalized = engine.Normalize(spec);
        if (normalized.Errors.Count > 0)
            throw new AiToolException("A página tem " + normalized.Errors.Count + " erro(s); corrija e envie de novo:\n" +
                string.Join("\n", normalized.Errors.Take(20).Select(x => "- " + x.Path + ": " + x.Message)) + "\n(Chame page_schema para ver o formato.)");
        return normalized;
    }

    private static string FileText(JsonObject spec) => LegacyCustomize.Stringify(spec, 2) + "\n";

    private static string Theme(JsonObject spec) => LegacyCustomize.JsToString(spec["theme"]?["preset"]);

    public static IReadOnlyList<AiTool> All { get; } =
    [
        new AiTool
        {
            Name = "page_schema", Access = "read",
            Description = "Formato completo das páginas HTML do Urbe (.page.json): meta, tema, layout, estilos de seção, todos os blocos e modelos prontos. Chame antes de criar ou editar uma página.",
            Parameters = new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false },
            Label = _ => "Lendo o formato de páginas",
            Run = (_, _) => Task.FromResult(new AiToolOutput(PageTemplates.SchemaText(Now().Year) + "\n\nMODELOS PRONTOS (write_page com template):\n" +
                string.Join("\n", PageTemplates.List().Select(t => "- " + t.Id + (t.Needs is { } need ? " (precisa de " + (need == "note" ? "note" : "folder") + ")" : "") + ": " + t.Description)) +
                "\n\nTemas: " + PageTemplates.ThemesList + ".\nPara editar uma página existente: read_note no .page.json, altere o JSON e envie tudo com write_page."))
        },
        new AiTool
        {
            Name = "write_page", Access = "write",
            Description = "Cria ou substitui uma página HTML do vault (arquivo .page.json) a partir do JSON completo (spec) ou de um modelo (template). Valida antes de gravar e devolve os erros se houver. Depois, abra com open_note para o usuário ver no estúdio.",
            Parameters = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject { ["type"] = "string", ["description"] = "Ex.: \"Páginas/Meu site.page.json\" (a extensão é adicionada se faltar)" },
                    ["spec"] = new JsonObject { ["type"] = "object", ["description"] = "JSON completo da página no formato de page_schema", ["additionalProperties"] = true },
                    ["template"] = new JsonObject { ["type"] = "string", ["description"] = "Alternativa a spec: id de um modelo (" + string.Join(", ", PageTemplates.List().Select(t => t.Id)) + ")" },
                    ["title"] = new JsonObject { ["type"] = "string", ["description"] = "Título ao usar template" },
                    ["folder"] = new JsonObject { ["type"] = "string", ["description"] = "Pasta de notas para os modelos vault-site/docs" },
                    ["note"] = new JsonObject { ["type"] = "string", ["description"] = "Caminho da nota para o modelo article" }
                },
                ["required"] = new JsonArray("path"), ["additionalProperties"] = false
            },
            Label = i => "Página " + Regex.Replace(LegacyCustomize.JsToString(i["path"] ?? "").Split('/')[^1], @"\.page\.json$", "", RegexOptions.IgnoreCase),
            Preview = (i, host) =>
            {
                var p = PagePath(Str(i["path"]));
                var n = Build(i, host);
                var d = host.Documents.Get(p);
                return new AiPreview(p, d?.Content, FileText(n.Spec), (d is not null ? "Substitui a página existente" : "Nova página") + " · " + n.Spec["sections"]!.AsArray().Count + " seções · tema " + Theme(n.Spec));
            },
            Run = async (i, host) =>
            {
                var p = PagePath(Str(i["path"]));
                var n = Build(i, host);
                var d = host.Documents.Get(p);
                var before = d is null ? null : new AiNoteSnap(d.Path, d.Content);
                var text = FileText(n.Spec);
                var u = d is null ? await host.CreateNoteAsync(p, text) : await host.SaveNoteAsync(d, text);
                var sections = n.Spec["sections"]!.AsArray();
                return new AiToolOutput((d is not null ? "Página atualizada: " : "Página criada: ") + u.Path + " (" + sections.Count + " seções: " +
                    string.Join(", ", sections.Select(s => LegacyCustomize.JsToString(s!["type"])) ) + ")." +
                    (n.Warnings.Count > 0 ? "\nAvisos: " + string.Join("; ", n.Warnings.Take(8).Select(w => w.Path + " " + w.Message)) : "") +
                    "\nO usuário pode abrir no estúdio (open_note) e exportar como HTML.", [new AiChange(u.Id, before, new AiNoteSnap(u.Path, u.Content))]);
            }
        }
    ];
}
