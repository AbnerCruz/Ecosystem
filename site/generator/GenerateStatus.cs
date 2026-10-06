// Ecosystem — gerador da projeção de estado do portal (ADR-0005).
//
// Uso (a partir de qualquer diretório do repositório, .NET SDK 10+):
//   dotnet run site/generator/GenerateStatus.cs -- [--out <arquivo>] [--ref <branch>] [--commit <sha>]
//                                                 [--checks passing|failing] [--checks-url <url>] [--releases online|offline]
//
// Lê apenas fontes canônicas (ecosystem.json, handoffs, documentos normativos) e escreve
// site/data/ecosystem-status.json, validado por docs/contracts/schemas/ecosystem-status.schema.json.
// O resultado é projeção, nunca autoridade: todo dado sem fonte automática sai como "not-available".
// --releases online consulta a API pública do GitHub (canais do perfil current); o padrão é offline, para que
// verificações locais e testes não dependam de rede. Falha de rede nunca derruba o gerador: o dado sai "not-available".
// --release-fixtures <arquivo> (somente offline) injeta respostas gravadas por URL de repositório nos self-tests.

#:property Nullable=enable

using System.Net.Http;
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
var currentProfile = File.Exists(P("docs/distribution/current.profile.json")) ? Load("docs/distribution/current.profile.json") : null;
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

// --- releases dos repositórios de origem (P1-9): derivadas da API pública do GitHub, nunca digitadas ---
var online = opt.GetValueOrDefault("releases", "offline") == "online";
if (opt.GetValueOrDefault("releases", "offline") is not ("online" or "offline")) { Console.Error.WriteLine("--releases deve ser online|offline"); return 2; }
HttpClient? http = null;
var fixturePath = opt.GetValueOrDefault("release-fixtures");
if (fixturePath is not null && online) { Console.Error.WriteLine("--release-fixtures só pode ser usado em geração offline."); return 2; }
var releaseFixtures = fixturePath is null ? null : JsonNode.Parse(File.ReadAllText(fixturePath))!.AsObject();
string? Fetch(string url)
{
    try
    {
        http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("ecosystem-portal-generator");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        if (Environment.GetEnvironmentVariable("GITHUB_TOKEN") is { Length: > 0 } tk) req.Headers.Authorization = new("Bearer", tk);
        using var res = http.Send(req);
        return res.IsSuccessStatusCode ? new StreamReader(res.Content.ReadAsStream()).ReadToEnd() : null;
    }
    catch { return null; }
}

// Retorna a release mais recente publicada (não rascunho) e seus artefatos instaláveis, ou (null, []) se indisponível.
(JsonObject? Rel, JsonArray Artifacts, string Note) LatestRelease(string sourceRepo, string? tagPrefix)
{
    var artifacts = new JsonArray();
    var m = System.Text.RegularExpressions.Regex.Match(sourceRepo, @"^https://github\.com/([^/]+)/([^/]+?)/?$");
    if (!online && releaseFixtures is null) return (null, artifacts, "Releases não consultadas (geração offline).");
    if (!m.Success) return (null, artifacts, "Repositório de origem não é um repositório GitHub reconhecido.");
    var json = releaseFixtures is null
        ? Fetch($"https://api.github.com/repos/{m.Groups[1].Value}/{m.Groups[2].Value}/releases?per_page=100")
        : releaseFixtures[sourceRepo]?.ToJsonString();
    if (json is null) return (null, artifacts, "API de releases do GitHub indisponível na geração; use o link de releases.");
    try
    {
    var list = JsonNode.Parse(json)?.AsArray();
    var r = list?.Where(x => x?["draft"]?.GetValue<bool>() == false
            && (tagPrefix is null || S(x?["tag_name"])?.StartsWith(tagPrefix, StringComparison.Ordinal) == true))
        .OrderByDescending(x => S(x?["published_at"]), StringComparer.Ordinal).FirstOrDefault()?.AsObject();
    if (r is null) return (null, artifacts, "Nenhuma release publicada deste componente entre as últimas 100 releases do canal.");

    string? sums = null;
    foreach (var a in r["assets"]?.AsArray() ?? [])
        if (S(a?["name"]) == "SHA256SUMS.txt" && S(a?["browser_download_url"]) is { } su) sums = Fetch(su);
    foreach (var a in r["assets"]?.AsArray() ?? [])
    {
        var name = S(a?["name"]); var url = S(a?["browser_download_url"]);
        if (name is null || url is null) continue;
        var kind = name.EndsWith(".apk") ? "apk" : name.EndsWith(".exe") ? "windows-installer" : name.EndsWith(".AppImage") ? "appimage" : null;
        if (kind is null) continue;
        string? sha = S(a?["digest"]) is { } dg && dg.StartsWith("sha256:") ? dg[7..] : null;
        if (sha is null && sums is not null)
            foreach (var line in sums.Split('\n'))
            {
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && parts[1].TrimStart('*') == name && parts[0].Length == 64) sha = parts[0].ToLowerInvariant();
            }
        artifacts.Add(new JsonObject { ["kind"] = kind, ["name"] = name, ["url"] = url, ["sizeBytes"] = a?["size"]?.GetValue<long>(), ["sha256"] = sha });
    }
    return (r, artifacts, releaseFixtures is null ? "API de releases do GitHub (lida na geração)." : "Resposta gravada de releases (self-test offline).");
    }
    catch
    {
        return (null, new JsonArray(), "Resposta de releases inválida; use o link de releases.");
    }
}

// --- registros canônicos de validação por build (P1-11): estado de validação por componente e páginas /testing/ ---
var records = new List<(string Rel, JsonNode Node)>();
var vdir = P("docs/validation");
if (Directory.Exists(vdir))
    foreach (var f in Directory.EnumerateFiles(vdir, "*.json", SearchOption.AllDirectories).Order())
        records.Add((Path.GetRelativePath(root, f).Replace('\\', '/'), Load(Path.GetRelativePath(root, f).Replace('\\', '/'))));
(string Rel, JsonNode Node)? LatestRecord(string componentId) => records
    .Where(r => S(r.Node["component"]) == componentId)
    .OrderBy(r => S(r.Node["recorded_at"]), StringComparer.Ordinal).ThenBy(r => S(r.Node["build"]), StringComparer.Ordinal)
    .Select(r => ((string, JsonNode)?)r).LastOrDefault();
string TestingPath(JsonNode n) => $"testing/{S(n["component"])}/{S(n["build"])}/";
string? pagesBase = repo.StartsWith("https://github.com/") ? $"https://{repo.Split('/')[3].ToLowerInvariant()}.github.io/{repo.Split('/')[4]}/" : null;

JsonObject ValidationOf(string componentId)
{
    if (LatestRecord(componentId) is not { } lr)
        return new JsonObject { ["state"] = "UNKNOWN", ["evidence"] = null, ["source"] = "Nenhum registro canônico de validação para este componente (docs/validation/).", ["build"] = null, ["page"] = null };
    var state = S(lr.Node["state"])!;
    var human = (lr.Node["evidence"]!.AsArray()).Where(e => S(e!["kind"]) == "human" && S(e["result"]) == "passed").Select(e => S(e!["evidence"])).FirstOrDefault();
    return new JsonObject
    {
        ["state"] = state,
        ["evidence"] = state == "VALIDATED" ? human : null,
        ["source"] = $"Registro canônico {lr.Rel} (build {S(lr.Node["build"])}).",
        ["build"] = S(lr.Node["build"]),
        ["page"] = pagesBase is null ? null : pagesBase + TestingPath(lr.Node),
    };
}

// Hash da árvore de um caminho no HEAD (funciona em checkout raso); nulo se git não estiver disponível.
string? GitTree(string rel)
{
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "rev-parse", "HEAD:" + rel }) psi.ArgumentList.Add(a);
        using var proc = System.Diagnostics.Process.Start(psi)!;
        var o = proc.StandardOutput.ReadToEnd().Trim(); proc.StandardError.ReadToEnd(); proc.WaitForExit();
        return proc.ExitCode == 0 && System.Text.RegularExpressions.Regex.IsMatch(o, "^[0-9a-f]{40}$") ? o : null;
    }
    catch { return null; }
}

// --- componentes ---
var components = new JsonArray();
foreach (var (id, c) in eco["components"]!.AsObject())
{
    var status = S(c!["status"])!;
    var path = S(c["path"])!;
    var sourceRepo = S(c["source"]?["repository"]);
    var inMonorepo = status is "active" or "migrating" or "deprecated";

    // Depois da migração, a autoridade do código é o caminho canônico no Ecosystem; source.repository é apenas proveniência/canal quando declarado.
    var repoLink = inMonorepo ? Tree(path) : sourceRepo;
    var channels = currentProfile?["entries"]?.AsArray().FirstOrDefault(e => S(e?["component"]) == id)?["channels"]?.AsArray();
    var releaseChannel = channels?.FirstOrDefault(ch => S(ch?["kind"]) == "github-release");
    var locationFrom = S(releaseChannel?["locationFrom"]);
    var releaseLocation = locationFrom switch
    {
        "source.repository" => sourceRepo,
        "ecosystem.repository" => repo,
        not null => S(c[locationFrom]),
        _ => null,
    };
    var releaseRepo = releaseLocation?.TrimEnd('/');
    if (releaseRepo?.EndsWith("/releases", StringComparison.Ordinal) == true) releaseRepo = releaseRepo[..^9];
    // As origens legadas continuam disponíveis quando não há perfil; um canal explícito prevalece sobre a origem.
    if (releaseChannel is null) releaseRepo = sourceRepo;
    var releases = releaseRepo is not null ? $"{releaseRepo}/releases" : null;
    // Tags de releases no próprio monorepo são <component-id>-v..., conforme o canal de desenvolvimento do Hub.
    var tagPrefix = releaseRepo == repo.TrimEnd('/') ? id + "-v" : null;
    var publicUrl = S(c["publicUrl"]);
    // publicUrl pode ser a URL de Web/PWA ou o próprio canal de Releases. Não projetar um canal de release como versão Web.
    var web = publicUrl is not null && releases is not null
        && publicUrl.TrimEnd('/') == releases.TrimEnd('/') ? null : publicUrl;

    var authority = S(c["version"]?["authority"]);
    var versionFile = S(c["version"]?["file"]);
    var version = authority switch
    {
        "version-file" when versionFile is not null && File.Exists(P(versionFile)) =>
            Datum(versionFile.EndsWith(".json")
                    ? S(JsonNode.Parse(File.ReadAllText(P(versionFile)))?["version"]) ?? throw new InvalidOperationException($"{versionFile}: campo 'version' ausente")
                    : File.ReadAllText(P(versionFile)).Trim(),
                $"Arquivo de versão {versionFile}.", Blob(versionFile)),
        "source-repository" => Datum(null, "Autoridade da versão: repositório de origem (produto ainda não migrado). Não projetada na Fase 0 (P1-9)."),
        "undecided" => Datum(null, "Autoridade da versão ainda não decidida."),
        _ => Datum(null, "Componente não declara versão em ecosystem.json."),
    };
    if (authority == "version-file" && versionFile is not null) sources.Add(versionFile);

    // Só projetar espelho quando o perfil current realmente declarar o repositório de origem como canal secundário.
    // source.repository sozinho é proveniência de migração e não deve ressuscitar um espelho abandonado (DEC-0039).
    var sourceChannel = channels?.FirstOrDefault(ch =>
        S(ch?["locationFrom"]) == "source.repository" &&
        S(ch?["role"]) is "mirror" or "legacy" or "alternative");
    JsonObject? mirror = null;
    if (status == "active" && sourceChannel is not null && sourceRepo is not null && GitTree(path) is { } tree
        && System.Text.RegularExpressions.Regex.Match(sourceRepo, @"^https://github\.com/([^/]+/[^/]+?)/?$") is { Success: true } om)
        mirror = new JsonObject
        {
            ["origin"] = om.Groups[1].Value,
            ["expectedTree"] = tree,
            ["workflowUrl"] = $"{sourceRepo}/actions/workflows/sync-from-ecosystem.yml",
            ["source"] = $"Árvore de {path} neste commit do Ecosystem; a origem é conferida ao vivo pelo navegador (último commit com Ecosystem-Tree).",
        };

    var (latest, artifacts, relNote) = releaseRepo is not null ? LatestRelease(releaseRepo, tagPrefix) : (null, new JsonArray(), "Sem releases projetadas.");
    var release = latest is null
        ? Datum(null, relNote)
        : Datum($"{S(latest["tag_name"])} ({(S(latest["published_at"]) ?? "")[..Math.Min(10, (S(latest["published_at"]) ?? "").Length)]}{(latest["prerelease"]?.GetValue<bool>() == true ? ", pré-lançamento" : "")})",
            $"{relNote} Canal: {releases}; localização derivada de ecosystem.json e docs/distribution/current.profile.json.", S(latest["html_url"]));
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
        ["links"] = new JsonObject { ["repository"] = repoLink, ["releases"] = releases, ["web"] = web },
        ["version"] = version,
        ["release"] = release,
        ["artifacts"] = artifacts,
        ["mirror"] = mirror,
        ["ci"] = ci,
        ["validation"] = ValidationOf(id),
    });
}

JsonObject Link(string target) => new()
{
    ["title"] = target,
    ["url"] = target.StartsWith("https://") ? target : Blob(target.Split('#')[0]),
};

// Alternativa clicável (DEC-0010, ADR-0007). O título e o corpo da Issue são definidos AQUI e validados por
// .github/scripts/apply-decision.cs; o hash impede aplicar uma escolha cujo texto mudou desde que o portal a exibiu.
JsonObject Alternative(string decisionId, int index, string option, string consequences)
{
    var letter = (char)('A' + index);
    var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(option + "\n" + consequences))).ToLowerInvariant()[..12];
    var issueBody =
        "<!-- ecosystem-decision:v1 -->\n" +
        $"decision: {decisionId}\n" +
        $"option: {letter}\n" +
        $"option-hash: {hash}\n\n" +
        $"Escolha da alternativa **{letter}** para a decisão {decisionId}, feita pelo portal do Ecosystem.\n\n" +
        "Enviar esta Issue confirma a escolha: uma automação a registra em `docs/governance/decisions.json`. " +
        "Não edite as quatro primeiras linhas.\n";
    return new JsonObject
    {
        ["id"] = letter.ToString(),
        ["option"] = option,
        ["consequences"] = consequences,
        ["hash"] = hash,
        ["issueTitle"] = $"Decisão {decisionId}: {letter}",
        ["issueBody"] = issueBody,
    };
}

// Resposta a uma validação humana (ADD-0005, ADR-0008). Mesmo princípio de Alternative(): título e corpo são definidos AQUI e
// validados por .github/scripts/apply-decision.cs; o hash impede aplicar uma resposta a uma validação que mudou.
string ValidationHash(string check, string obj) =>
    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(check + "\n" + obj))).ToLowerInvariant()[..12];

JsonObject ValidationAnswer(string taskId, string handoffId, string hash, bool approve)
{
    var result = approve ? "passed" : "failed";
    var verb = approve ? "aprovada" : "reprovada";
    return new JsonObject
    {
        ["result"] = result,
        ["issueTitle"] = $"Validação {taskId}: {verb}",
        ["issueBody"] =
            "<!-- ecosystem-validation:v1 -->\n" +
            $"validation: {taskId}\n" +
            $"handoff: {handoffId}\n" +
            $"check-hash: {hash}\n" +
            $"result: {result}\n\n" +
            $"Validação humana da tarefa {taskId} **{verb}**, feita pelo portal do Ecosystem.\n\n" +
            "Enviar esta Issue confirma o resultado: uma automação o registra na verificação do handoff. " +
            "Não edite as cinco primeiras linhas. Se quiser, escreva um comentário depois da linha `comment:`.\n\n" +
            "comment:\n",
    };
}

// --- decisões pendentes do proprietário (DEC-0007): sempre com o objeto a revisar ---
var pendingDecisions = new JsonArray();
var decisionsDoc = Load("docs/governance/decisions.json");
foreach (var dn in decisionsDoc["decisions"]!.AsArray())
{
    if (S(dn!["status"]) != "pending") continue;
    var related = dn["related"]?.AsArray().Select(x => S(x)!).ToList() ?? [];
    if (related.Count == 0)
        throw new InvalidOperationException($"{S(dn["id"])}: decisão pendente sem objeto ('related'); o portal não pode apresentá-la (DEC-0007).");
    pendingDecisions.Add(new JsonObject
    {
        ["id"] = S(dn["id"]),
        ["component"] = S(dn["component"]),
        ["title"] = S(dn["title"]),
        ["blocking"] = dn["blocking"]?.GetValue<bool>() ?? false,
        ["question"] = S(dn["question"]),
        ["alternatives"] = new JsonArray(dn["alternatives"]!.AsArray().Select((a, i) => (JsonNode?)Alternative(S(dn["id"])!, i, S(a!["option"])!, S(a["consequences"])!)).ToArray()),
        ["recommendation"] = S(dn["recommendation"]),
        ["objects"] = new JsonArray(related.Select(r => (JsonNode?)Link(r)).ToArray()),
        ["record"] = Blob("docs/governance/decisions.json"),
        ["raisedAt"] = S(dn["raisedAt"]),
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
            {
                var obj = S(v["object"]) ?? throw new InvalidOperationException($"{rel}: validação humana pendente '{S(v["check"])}' sem 'object'; o portal não pode apresentá-la (DEC-0007).");
                pending.Add(new JsonObject
                {
                    ["component"] = S(h["component"]),
                    ["taskId"] = S(h["task_id"]),
                    ["check"] = S(v["check"]),
                    ["state"] = "HUMAN_VALIDATION_PENDING",
                    ["record"] = Blob(rel),
                    ["object"] = Link(obj),
                    // ADD-0012: só a validação crítica interrompe o proprietário; handoffs antigos sem a marcação contam como críticos.
                    ["critical"] = !(v["critical"] is JsonValue cv && cv.TryGetValue<bool>(out var isCritical) && !isCritical),
                    ["handoff"] = S(h["message_id"]),
                    ["checkHash"] = ValidationHash(S(v["check"])!, obj),
                    ["approve"] = ValidationAnswer(S(h["task_id"])!, S(h["message_id"])!, ValidationHash(S(v["check"])!, obj), true),
                    ["reject"] = ValidationAnswer(S(h["task_id"])!, S(h["message_id"])!, ValidationHash(S(v["check"])!, obj), false),
                });
            }
    }
}

// --- candidatos a reutilização (ADR-0011): derivados dos reuse_assessment dos handoffs, nunca digitados ---
var reuseLatest = new Dictionary<(string Subject, string Component), (string Ts, JsonNode Node, string Handoff, string Component)>();
if (Directory.Exists(hdir))
    foreach (var f in Directory.EnumerateFiles(hdir, "*.json").Order())
    {
        var hn = JsonNode.Parse(File.ReadAllText(f))!;
        if (hn["reuse_assessment"] is not JsonArray ra) continue;
        foreach (var a in ra)
        {
            var key = (S(a!["subject"])!, S(hn["component"])!);
            var ts = S(hn["timestamp"]) ?? "";
            if (!reuseLatest.TryGetValue(key, out var prev) || string.CompareOrdinal(prev.Ts, ts) < 0)
                reuseLatest[key] = (ts, a, S(hn["message_id"])!, key.Item2);
        }
    }
// Distribuição atual (P2-12/P2-13): derivada do perfil `current`; as localizações vêm do próprio componente (nada digitado).
JsonObject? distribution = null;
if (currentProfile is not null)
{
    var prof = currentProfile;
    var chRows = new JsonArray();
    foreach (var e in prof["entries"]!.AsArray())
    {
        var cid = S(e!["component"])!; var comp = eco["components"]![cid]!;
        foreach (var ch in e["channels"]?.AsArray() ?? [])
        {
            var from = S(ch!["locationFrom"]);
            chRows.Add(new JsonObject
            {
                ["component"] = cid,
                ["componentName"] = S(comp["name"]),
                ["sourcePath"] = S(comp["path"]),
                ["channel"] = S(ch["id"]),
                ["kind"] = S(ch["kind"]),
                ["role"] = S(ch["role"]),
                ["location"] = from switch
                {
                    "source.repository" => S(comp["source"]?["repository"]),
                    "ecosystem.repository" => repo,
                    "publicUrl" => S(comp["publicUrl"]),
                    _ => null,
                },
                ["artifacts"] = new JsonArray((ch["artifacts"]?.AsArray() ?? []).Select(x => (JsonNode?)JsonValue.Create(S(x))).ToArray()),
                ["updateMechanism"] = S(ch["updateMechanism"]),
            });
        }
    }
    distribution = new JsonObject
    {
        ["profile"] = S(prof["id"]), ["name"] = S(prof["name"]), ["status"] = S(prof["status"]),
        ["decisions"] = new JsonArray((prof["decisions"]?.AsArray() ?? []).Select(x => (JsonNode?)JsonValue.Create(S(x))).ToArray()),
        ["channels"] = chRows,
        ["source"] = "docs/distribution/current.profile.json (SOURCE = path do componente no Ecosystem; localizações derivadas de ecosystem.json)",
    };
}
// Capabilities (P2-13): derivadas de provides/requires dos manifests; nenhuma é inventada. Vazio hoje, e o portal não mostra a seção.
var capabilityIndex = new SortedDictionary<string, (SortedSet<string> Providers, SortedSet<string> Consumers)>(StringComparer.Ordinal);
foreach (var (cid, comp) in eco["components"]!.AsObject())
{
    foreach (var pv in comp!["provides"]?.AsArray() ?? [])
    {
        var k = S(pv!["capability"])!; if (!capabilityIndex.ContainsKey(k)) capabilityIndex[k] = ([], []);
        capabilityIndex[k].Providers.Add(cid);
    }
    foreach (var rq in comp["requires"]?.AsArray() ?? [])
    {
        var k = S(rq!["capability"])!; if (!capabilityIndex.ContainsKey(k)) capabilityIndex[k] = ([], []);
        capabilityIndex[k].Consumers.Add(cid);
    }
}
var capabilities = new JsonArray(capabilityIndex.Select(kv => (JsonNode?)new JsonObject
{
    ["id"] = kv.Key,
    ["providers"] = new JsonArray(kv.Value.Providers.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
    ["consumers"] = new JsonArray(kv.Value.Consumers.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()),
}).ToArray());

var reuseCandidates = new JsonArray(reuseLatest.Values.Where(v => S(v.Node["status"]) != "product-specific")
    .OrderBy(v => S(v.Node["subject"]), StringComparer.Ordinal).ThenBy(v => v.Component, StringComparer.Ordinal)
    .Select(v => (JsonNode?)new JsonObject
    {
        ["subject"] = S(v.Node["subject"]),
        ["component"] = v.Component,
        ["status"] = S(v.Node["status"]),
        ["rationale"] = S(v.Node["rationale"]),
        ["consumers"] = new JsonArray((v.Node["consumers"]?.AsArray() ?? []).Select(x => (JsonNode?)JsonValue.Create(S(x))).ToArray()),
        ["extractionReview"] = S(v.Node["extraction_review"]),
        ["handoff"] = v.Handoff,
        ["record"] = Blob("docs/governance/handoffs/" + v.Handoff + ".json"),
    }).ToArray());

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
Doc("Modelo de produto (Product Shell, Context)", "docs/architecture/product-model.md");
Doc("Distribuição e plataforma própria", "docs/architecture/distribution.md");
Doc("Perguntas-chave", "docs/architecture/faq.md");
Doc("Portal (arquitetura)", "docs/architecture/portal.md");

// --- páginas de validação /testing/<componente>/<build>/ geradas dos registros (P1-11): só leitura, sem script ---
string H(string? t) => System.Net.WebUtility.HtmlEncode(t ?? "");
string Items(JsonNode? arr, string tag) => string.Join("", (arr?.AsArray() ?? []).Select(x => $"<li>{H(S(x))}</li>"));
var stateLabels = new Dictionary<string, string> { ["IMPLEMENTED"] = "implementado", ["AUTOMATED_VERIFIED"] = "verificado automaticamente", ["HUMAN_VALIDATION_PENDING"] = "validação humana pendente", ["VALIDATED"] = "validado por humano" };
var testingRoot = P("site/testing");
if (Directory.Exists(testingRoot)) Directory.Delete(testingRoot, true);
foreach (var (rel, n) in records)
{
    var dir = P("site/" + TestingPath(n));
    Directory.CreateDirectory(dir);
    var art = n["artifact"]!; var tr = n["traceability"]!;
    var ev = string.Join("", n["evidence"]!.AsArray().Select(e =>
        $"<li><strong>{(S(e!["kind"]) == "human" ? "Humana" : "Automática")} · {H(S(e["result"]))}</strong>: {H(S(e["check"]))}<br><small>{H(S(e["evidence"]))}</small></li>"));
    var html = $$"""
<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Validação — {{H(S(n["component"]))}} {{H(S(n["build"]))}}</title>
  <link rel="stylesheet" href="../../../style.css">
</head>
<body>
  <header class="top"><h1>Validação do build {{H(S(n["build"]))}}</h1>
    <p class="meta">{{H(S(n["component"]))}} · <span class="badge v-{{H(S(n["state"]))}}">{{H(stateLabels.GetValueOrDefault(S(n["state"]) ?? "", S(n["state"])))}}</span> · <a href="../../../">portal</a></p></header>
  <main>
    <section><h2>Objetivo</h2><p>{{H(S(n["objective"]))}}</p></section>
    <section><h2>Pré-condições</h2><ul>{{Items(n["preconditions"], "li")}}</ul></section>
    <section><h2>Passos</h2><ol>{{Items(n["steps"], "li")}}</ol></section>
    <section><h2>Resultado esperado</h2><p>{{H(S(n["expected"]))}}</p></section>
    <section><h2>Problemas conhecidos</h2><ul>{{Items(n["known_issues"], "li")}}</ul></section>
    <section><h2>Artefato</h2><p><a href="{{H(S(art["url"]))}}">{{H(S(art["name"]))}}</a>{{(S(art["sha256"]) is { } sh ? $"<br><small>SHA-256 {H(sh)}</small>" : "")}}</p></section>
    <section><h2>Evidência</h2><ul>{{ev}}</ul></section>
    <section><h2>Rastreabilidade</h2><p>Tarefa {{H(S(tr["task"]))}} · commit {{H(S(tr["commit"])?[..Math.Min(8, (S(tr["commit"]) ?? "").Length)])}}{{(S(tr["pr"]) is { } pr ? $" · <a href=\"{H(pr)}\">PR</a>" : "")}} · <a href="{{Blob(rel)}}">registro canônico</a></p>
      <p class="hint">Esta página é gerada do registro canônico; não é fonte de verdade.</p></section>
  </main>
</body>
</html>
""";
    File.WriteAllText(Path.Combine(dir, "index.html"), html);
}
if (records.Count > 0)
{
    var rows = string.Join("", records.OrderBy(r => S(r.Node["component"]), StringComparer.Ordinal).ThenByDescending(r => S(r.Node["recorded_at"]), StringComparer.Ordinal)
        .Select(r => $"<li><a href=\"{H(S(r.Node["component"]))}/{H(S(r.Node["build"]))}/\">{H(S(r.Node["component"]))} · {H(S(r.Node["build"]))}</a> — {H(stateLabels.GetValueOrDefault(S(r.Node["state"]) ?? "", S(r.Node["state"])))}</li>"));
    File.WriteAllText(P("site/testing/index.html"), "<!doctype html>\n<html lang=\"pt-BR\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Validações por build</title><link rel=\"stylesheet\" href=\"../style.css\"></head><body><header class=\"top\"><h1>Validações por build</h1><p class=\"meta\"><a href=\"../\">portal</a></p></header><main><section><ul>" + rows + "</ul></section></main></body></html>\n");
}

// --- fases e gates: derivados do ROADMAP (a autoridade), nunca digitados ---
var roadmapGates = new JsonArray();
if (File.Exists(P("ROADMAP.md")))
{
    sources.Add("ROADMAP.md");
    var rtext = File.ReadAllText(P("ROADMAP.md"));
    var heads = System.Text.RegularExpressions.Regex.Matches(rtext, @"^## (.*)$", System.Text.RegularExpressions.RegexOptions.Multiline).ToList();
    for (var hi = 0; hi < heads.Count; hi++)
    {
        var hm = System.Text.RegularExpressions.Regex.Match(heads[hi].Groups[1].Value, @"^Fase (\d+) — ");
        if (!hm.Success) continue;
        var body = rtext[heads[hi].Index..(hi + 1 < heads.Count ? heads[hi + 1].Index : rtext.Length)];
        var gm = System.Text.RegularExpressions.Regex.Match(body, @"^\*Estado do gate:\* \*\*(aprovado|aguardando|não iniciado)\*\*", System.Text.RegularExpressions.RegexOptions.Multiline);
        // Nome e progresso da fase: o título "## Fase N — <nome>" e as caixas dos itens "- [x|~| ] P<n>-<m> " (mesmas linhas
        // de formato fixo que CHK-STATE-CONSISTENCY lê); CHK-PORTAL compara.
        var marks = System.Text.RegularExpressions.Regex.Matches(body, @"^- \[( |~|x)\] P\d+-\d+ ", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(x => x.Groups[1].Value).ToList();
        roadmapGates.Add(new JsonObject
        {
            ["phase"] = int.Parse(hm.Groups[1].Value),
            ["state"] = gm.Success ? gm.Groups[1].Value : "não iniciado",
            ["name"] = heads[hi].Groups[1].Value[hm.Length..].Trim(),
            ["done"] = marks.Count(x => x == "x"),
            ["inProgress"] = marks.Count(x => x == "~"),
            ["todo"] = marks.Count(x => x == " "),
        });
    }
}

// --- aprovações críticas (ADD-0012): PRs que o integrador marcou como críticos e prontos; instantâneo do GitHub (pages.yml) ---
// Rotina verde nunca aparece aqui: entra sozinha. Sem instantâneo, o dado é "not-available" (nunca inventado).
var approvalItems = new JsonArray();
var approvalsAvailable = File.Exists(P("site/data/approvals-snapshot.json"));
if (approvalsAvailable)
    foreach (var pr in JsonNode.Parse(File.ReadAllText(P("site/data/approvals-snapshot.json")))!.AsArray().OrderBy(x => x!["number"]!.GetValue<int>()))
        approvalItems.Add(new JsonObject
        {
            ["number"] = pr!["number"]!.GetValue<int>(),
            ["title"] = S(pr["title"]),
            ["url"] = S(pr["url"]),
        });
var pendingApprovals = new JsonObject { ["availability"] = approvalsAvailable ? "derived" : "not-available", ["items"] = approvalItems };

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
        ["gates"] = roadmapGates,
        ["gatesSource"] = "ROADMAP.md (linha 'Estado do gate' de cada fase)",
        ["checks"] = checksDatum,
    },
    ["components"] = components,
    ["pendingDecisions"] = pendingDecisions,
    ["pendingValidations"] = pending,
    ["pendingApprovals"] = pendingApprovals,
    ["reuseCandidates"] = reuseCandidates,
    ["distribution"] = distribution,
    ["capabilities"] = capabilities,
    ["docs"] = docs,
};

var outPath = Path.GetFullPath(opt.GetValueOrDefault("out", P("site/data/ecosystem-status.json")));
Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
File.WriteAllText(outPath, result.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
Console.WriteLine($"Projeção escrita em {Path.GetRelativePath(root, outPath)} ({components.Count} componentes, {pendingDecisions.Count} decisão(ões) e {pending.Count} validação(ões) pendente(s)).");
return 0;
