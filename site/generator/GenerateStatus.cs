// Ecosystem — gerador da projeção de estado do portal (ADR-0005).
//
// Uso (a partir de qualquer diretório do repositório, .NET SDK 10+):
//   dotnet run site/generator/GenerateStatus.cs -- [--out <arquivo>] [--ref <branch>] [--commit <sha>]
//                                                 [--checks passing|failing] [--checks-url <url>]
//
// Lê apenas fontes canônicas (ecosystem.json, handoffs, documentos normativos) e escreve
// site/data/ecosystem-status.json, validado por docs/contracts/schemas/ecosystem-status.schema.json.
// O resultado é projeção, nunca autoridade: todo dado sem fonte automática sai como "not-available".

#:property Nullable=enable

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

var opt = new Dictionary<string, string>();
for (var i = 0; i + 1 < args.Length; i += 2)
{
    if (!args[i].StartsWith("--")) { Console.Error.WriteLine($"argumento inesperado: {args[i]}"); return 2; }
    opt[args[i][2..]] = args[i + 1];
}

string? root = null;
for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
    if (File.Exists(Path.Combine(d.FullName, "MANIFEST.md")) && File.Exists(Path.Combine(d.FullName, "ecosystem.json"))) { root = d.FullName; break; }
if (root is null) { Console.Error.WriteLine("Raiz do repositório não encontrada."); return 2; }

string P(string rel) => Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
var sources = new List<string>();
JsonNode Load(string rel) { sources.Add(rel); return JsonNode.Parse(File.ReadAllText(P(rel)))!; }
string? S(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

var eco = Load("ecosystem.json");
var repo = S(eco["ecosystem"]?["repository"]) ?? throw new InvalidOperationException("ecosystem.repository ausente");
var gitRef = opt.GetValueOrDefault("ref", "HEAD");
var commit = opt.GetValueOrDefault("commit");
string Blob(string path) => $"{repo}/blob/{gitRef}/{path}";
string Tree(string path) => $"{repo}/tree/{gitRef}/{path}";

JsonObject Datum(string? value, string source, string? url = null) => new()
{
    ["value"] = value,
    ["availability"] = value is null ? "not-available" : "derived",
    ["source"] = source,
    ["url"] = url,
};

// --- checks de consistência do commit projetado (informados pelo CI que acabou de executá-los) ---
var checks = opt.GetValueOrDefault("checks");
if (checks is not (null or "passing" or "failing")) { Console.Error.WriteLine("--checks deve ser passing|failing"); return 2; }
var checksDatum = Datum(checks,
    checks is null ? "Checks de consistência não informados a este gerador (execução local)." : "tests/consistency/Check.cs executado no CI deste commit.",
    opt.GetValueOrDefault("checks-url"));

// --- componentes ---
var components = new JsonArray();
foreach (var (id, c) in eco["components"]!.AsObject())
{
    var status = S(c!["status"])!;
    var path = S(c["path"])!;
    var sourceRepo = S(c["source"]?["repository"]);
    var inMonorepo = status is "active" or "migrating" or "deprecated";

    var repoLink = sourceRepo ?? (inMonorepo ? Tree(path) : null);
    var releases = sourceRepo is not null ? $"{sourceRepo}/releases" : null;

    var authority = S(c["version"]?["authority"]);
    var versionFile = S(c["version"]?["file"]);
    var version = authority switch
    {
        "version-file" when versionFile is not null && File.Exists(P(versionFile)) =>
            Datum(File.ReadAllText(P(versionFile)).Trim(), $"Arquivo de versão {versionFile}.", Blob(versionFile)),
        "source-repository" => Datum(null, "Autoridade da versão: repositório de origem (produto ainda não migrado). Não projetada na Fase 0 (P1-9)."),
        "undecided" => Datum(null, "Autoridade da versão ainda não decidida."),
        _ => Datum(null, "Componente não declara versão em ecosystem.json."),
    };
    if (authority == "version-file" && versionFile is not null) sources.Add(versionFile);

    var release = Datum(null, sourceRepo is not null
        ? "Releases do repositório de origem ainda não projetadas (P1-9); use o link de releases."
        : "Sem releases projetadas.");
    var ci = Datum(null,
        inMonorepo ? "CI por componente ainda não projetado; veja os checks do Ecosystem."
        : sourceRepo is not null ? "CI do produto vive no repositório de origem enquanto não migrado."
        : "Componente ainda não existe.");

    components.Add(new JsonObject
    {
        ["id"] = id,
        ["name"] = S(c["name"]),
        ["type"] = S(c["type"]),
        ["status"] = status,
        ["description"] = S(c["description"]),
        ["links"] = new JsonObject { ["repository"] = repoLink, ["releases"] = releases, ["web"] = S(c["publicUrl"]) },
        ["version"] = version,
        ["release"] = release,
        ["ci"] = ci,
        ["validation"] = new JsonObject
        {
            ["state"] = "UNKNOWN",
            ["evidence"] = null,
            ["source"] = "Ainda não existem registros canônicos de validação por build (P1-11).",
        },
    });
}

// --- validações humanas pendentes: último handoff de cada tarefa ---
var pending = new JsonArray();
var hdir = P("docs/governance/handoffs");
if (Directory.Exists(hdir))
{
    var latestPerTask = Directory.EnumerateFiles(hdir, "*.json").Order()
        .Select(f => (Rel: "docs/governance/handoffs/" + Path.GetFileName(f), Node: Load("docs/governance/handoffs/" + Path.GetFileName(f))))
        .GroupBy(h => S(h.Node["task_id"]))
        .Select(g => g.OrderBy(h => S(h.Node["timestamp"]), StringComparer.Ordinal).Last());
    foreach (var (rel, h) in latestPerTask)
    {
        if (S(h["state"]) is "done" or "cancelled" or "failed") continue;
        foreach (var v in h["verification"]!.AsArray())
            if (S(v!["kind"]) == "human" && S(v["result"]) == "pending")
                pending.Add(new JsonObject
                {
                    ["component"] = S(h["component"]),
                    ["taskId"] = S(h["task_id"]),
                    ["check"] = S(v["check"]),
                    ["state"] = "HUMAN_VALIDATION_PENDING",
                    ["record"] = Blob(rel),
                });
    }
}

// --- documentação canônica ---
var titles = new Dictionary<string, string>
{
    ["manifest"] = "MANIFEST", ["agents"] = "AGENTS", ["architecture"] = "Architecture", ["roadmap"] = "Roadmap",
    ["enforcementMatrix"] = "Matriz de enforcement", ["decisions"] = "Decisões do proprietário",
};
var docs = new JsonArray();
void Doc(string title, string path) { if (File.Exists(P(path))) docs.Add(new JsonObject { ["title"] = title, ["path"] = path, ["url"] = Blob(path) }); }
foreach (var (key, v) in eco["ecosystem"]!["normative"]!.AsObject())
    Doc(titles.GetValueOrDefault(key, key), S(v)!);
Doc("ADRs", "docs/adr/README.md");
Doc("Definition of Done e estados de validação", "docs/governance/definition-of-done.md");
Doc("Estratégia de migração", "docs/migration/README.md");
Doc("Portal (arquitetura)", "docs/architecture/portal.md");

var result = new JsonObject
{
    ["schema"] = "ecosystem/contracts/ecosystem-status/1",
    ["schemaVersion"] = 1,
    ["kind"] = "projection",
    ["authority"] = false,
    ["generatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
    ["generator"] = "site/generator/GenerateStatus.cs",
    ["source"] = new JsonObject
    {
        ["repository"] = repo,
        ["ref"] = gitRef,
        ["commit"] = commit,
        ["files"] = new JsonArray(sources.Distinct().Select(s => (JsonNode?)JsonValue.Create(s)).ToArray()),
    },
    ["ecosystem"] = new JsonObject
    {
        ["name"] = S(eco["ecosystem"]!["name"]),
        ["phase"] = S(eco["ecosystem"]!["phase"]),
        ["checks"] = checksDatum,
    },
    ["components"] = components,
    ["pendingValidations"] = pending,
    ["docs"] = docs,
};

var outPath = Path.GetFullPath(opt.GetValueOrDefault("out", P("site/data/ecosystem-status.json")));
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
Console.WriteLine($"Projeção escrita em {Path.GetRelativePath(root, outPath)} ({components.Count} componentes, {pending.Count} validação(ões) pendente(s)).");
return 0;
