// Ecosystem — checks de consistência da fundação (ADR-0003).
//
// Uso (a partir da raiz do repositório, .NET SDK 10+):
//   dotnet run tests/consistency/Check.cs               executa todos os checks
//   dotnet run tests/consistency/Check.cs -- --self-test prova que cada check falha quando violado
//   dotnet run tests/consistency/Check.cs -- --registry [--file <json com components>] [--discover <capability> [<faixa>]]
//   dotnet run tests/consistency/Check.cs -- --integration <branch> [--base <ref>]   base obsoleta e sobreposição antes de integrar (ADR-0014)
//                                                      indice do Registry (capabilities, providers, consumers) e descoberta
//
// Cada check possui um ID estável (CHK-...) referenciado por docs/governance/enforcement-matrix.json.
// Sem dependências externas: o validador de JSON Schema abaixo implementa apenas o subconjunto usado
// em docs/contracts/schemas e falha explicitamente diante de qualquer keyword não suportada.

#:property Nullable=enable

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

var selfTest = args.Contains("--self-test");
var root = Repo.FindRoot(Directory.GetCurrentDirectory());
if (root is null)
{
    Console.Error.WriteLine("Raiz do repositório não encontrada (procurei MANIFEST.md + ecosystem.json).");
    return 2;
}

if (args.Contains("--registry"))
    return RegistryCli.Run(root, args);

if (args.Contains("--integration"))
    return IntegrationCli.Run(root, args, Console.Out);

if (selfTest)
    return SelfTest.Run(root);

var report = Checks.RunAll(root);
report.Print(Console.Out);
return report.Failed ? 1 : 0;

// ---------------------------------------------------------------------------------------------

static class Checks
{
    public static readonly string[] Ids =
    [
        "CHK-FOUNDATION-FILES",
        "CHK-SCHEMA",
        "CHK-IDS-UNIQUE",
        "CHK-SINGLE-AUTHORITY",
        "CHK-BOUNDARIES",
        "CHK-ARCH-REFS",
        "CHK-REGISTRY",
        "CHK-MIGRATION-HISTORY",
        "CHK-GENERIC-DIRS",
        "CHK-SHARED-DECLARATION",
        "CHK-ENFORCEMENT-MATRIX",
        "CHK-AGENTS-NN",
        "CHK-ADR",
        "CHK-DECISIONS",
        "CHK-HANDOFFS",
        "CHK-VALIDATION",
        "CHK-ROADMAP",
        "CHK-STATE-CONSISTENCY",
        "CHK-SECRETS",
        "CHK-PORTAL",
        "CHK-DECISION-FLOW",
    ];

    static readonly string[] FoundationFiles =
    [
        "MANIFEST.md", "README.md", "AGENTS.md", "ARCHITECTURE.md", "ROADMAP.md", "ecosystem.json",
        "docs/adr/README.md", "docs/adr/TEMPLATE.md", "docs/contracts/README.md", "docs/governance/README.md",
        "docs/migration/README.md", ".github/workflows/consistency.yml",
    ];

    static readonly string[] SharedTypes = ["tool", "service", "library", "workspace", "adapter", "contract"];
    static readonly string[] GenericDirNames =
        ["shared", "common", "commons", "misc", "util", "utils", "utilities", "helper", "helpers", "stuff", "general", "generic"];
    static readonly string[] SharedAreas = ["platform", "workspaces", "tools"];

    const string IdPattern = "^[a-z][a-z0-9]*(-[a-z0-9]+)*$";

    public static Report RunAll(string root)
    {
        var r = new Report();
        var ctx = new Context(root, r);

        Guard(r, "CHK-FOUNDATION-FILES", () => FoundationFiles_(ctx));
        Guard(r, "CHK-SCHEMA", ctx.LoadJson);
        Guard(r, "CHK-SCHEMA", () => Schema(ctx));
        Guard(r, "CHK-IDS-UNIQUE", () => IdsUnique(ctx));
        Guard(r, "CHK-SINGLE-AUTHORITY", () => SingleAuthority(ctx));
        Guard(r, "CHK-BOUNDARIES", () => Boundaries(ctx));
        Guard(r, "CHK-ARCH-REFS", () => ArchRefs(ctx));
        Guard(r, "CHK-REGISTRY", () => RegistryContracts(ctx));
        Guard(r, "CHK-MIGRATION-HISTORY", () => MigrationHistory(ctx));
        Guard(r, "CHK-GENERIC-DIRS", () => GenericDirs(ctx));
        Guard(r, "CHK-SHARED-DECLARATION", () => SharedDeclaration(ctx));
        Guard(r, "CHK-ENFORCEMENT-MATRIX", () => EnforcementMatrix(ctx));
        Guard(r, "CHK-AGENTS-NN", () => AgentsNn(ctx));
        Guard(r, "CHK-ADR", () => Adr(ctx));
        Guard(r, "CHK-DECISIONS", () => Decisions(ctx));
        Guard(r, "CHK-HANDOFFS", () => Handoffs(ctx));
        Guard(r, "CHK-VALIDATION", () => ValidationRecords(ctx));
        Guard(r, "CHK-ROADMAP", () => Roadmap(ctx));
        Guard(r, "CHK-STATE-CONSISTENCY", () => StateConsistency(ctx));
        Guard(r, "CHK-SECRETS", () => Secrets(ctx));
        Guard(r, "CHK-PORTAL", () => Portal(ctx));
        Guard(r, "CHK-DECISION-FLOW", () => DecisionFlow(ctx));
        return r;
    }

    // Um check que quebra conta como falha, mas não como detecção (o self-test distingue os dois).
    static void Guard(Report r, string id, Action check)
    {
        try { check(); }
        catch (Exception e) { r.Crash(id, $"{e.GetType().Name}: {e.Message}"); }
    }

    // --- CHK-FOUNDATION-FILES (MANIFEST §45) ---
    static void FoundationFiles_(Context c)
    {
        c.R.Ran("CHK-FOUNDATION-FILES");
        foreach (var f in FoundationFiles)
            if (!File.Exists(c.P(f)))
                c.R.Fail("CHK-FOUNDATION-FILES", $"arquivo de fundação ausente: {f}");
    }

    // --- CHK-SCHEMA ---
    static void Schema(Context c)
    {
        const string id = "CHK-SCHEMA";
        c.R.Ran(id);
        foreach (var (file, doc) in c.Json)
        {
            var schemaPath = SchemaFor(file);
            if (schemaPath is null) continue;
            var schemaDoc = c.LoadSchema(schemaPath);
            if (schemaDoc is null) { c.R.Fail(id, $"{file}: schema {schemaPath} ausente ou inválido"); continue; }

            // O "$schema" declarado precisa apontar para o schema canônico (evita segunda autoridade de formato).
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("$schema", out var decl))
            {
                var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(c.P(file))!, decl.GetString() ?? ""));
                if (resolved != Path.GetFullPath(c.P(schemaPath)))
                    c.R.Fail(id, $"{file}: \"$schema\" deve apontar para {schemaPath}");
            }

            var errors = new List<string>();
            try { new SchemaValidator(schemaDoc.RootElement).Validate(doc.RootElement, errors); }
            catch (UnsupportedSchemaException e) { c.R.Fail(id, $"{schemaPath}: {e.Message}"); continue; }
            foreach (var e in errors) c.R.Fail(id, $"{file}: {e}");
        }

        if (c.Ecosystem is { } eco && eco.TryGetProperty("ecosystem", out var e2) && e2.TryGetProperty("normative", out var norm)
            && norm.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in norm.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String && !File.Exists(c.P(p.Value.GetString()!)))
                    c.R.Fail(id, $"ecosystem.json: documento normativo '{p.Name}' não existe: {p.Value.GetString()}");
        }

        foreach (var (cid, comp) in c.Components())
        {
            if (!comp.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Object) continue;
            foreach (var p in docs.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.String && !File.Exists(c.P(p.Value.GetString()!)))
                    c.R.Fail(id, $"componente '{cid}': docs.{p.Name} não existe: {p.Value.GetString()}");
        }
    }

    static string? SchemaFor(string file) => file switch
    {
        "ecosystem.json" => "docs/contracts/schemas/ecosystem.schema.json",
        "docs/governance/decisions.json" => "docs/contracts/schemas/decisions.schema.json",
        "docs/governance/enforcement-matrix.json" => "docs/contracts/schemas/enforcement-matrix.schema.json",
        _ when file.StartsWith("docs/governance/handoffs/") => "docs/contracts/schemas/handoff.schema.json",
        _ when file.StartsWith("docs/validation/") => "docs/contracts/schemas/validation-record.schema.json",
        "docs/contracts/permissions.json" => "docs/contracts/schemas/permissions-catalog.schema.json",
        _ when file.StartsWith("docs/contracts/") && file.Contains("/capabilities/") && file.EndsWith(".json") => "docs/contracts/schemas/capability-contract.schema.json",
        _ when file.StartsWith("docs/contracts/examples/context/") => "docs/contracts/schemas/context.schema.json",
        _ when file.StartsWith("docs/contracts/examples/distribution/") || (file.StartsWith("docs/distribution/") && file.EndsWith(".profile.json")) => "docs/contracts/schemas/distribution-profile.schema.json",
        _ => null,
    };

    // --- CHK-IDS-UNIQUE (NN-019) ---
    static void IdsUnique(Context c)
    {
        const string id = "CHK-IDS-UNIQUE";
        c.R.Ran(id);
        foreach (var (file, doc) in c.Json)
            foreach (var dup in Json.DuplicateKeys(doc.RootElement, "$"))
                c.R.Fail(id, $"{file}: chave duplicada {dup}");

        Unique(c, id, "decisions.json", c.DecisionList().Select(d => d.Str("id")));
        Unique(c, id, "enforcement-matrix.json", c.MatrixInvariants().Select(i => i.Str("id")));
        Unique(c, id, "docs/adr", c.AdrFiles().Select(f => Path.GetFileName(f)[..4]));

        var handoffIds = new List<string?>();
        foreach (var (file, doc) in c.Handoffs())
        {
            var mid = doc.RootElement.Str("message_id");
            handoffIds.Add(mid);
            if (mid is not null && Path.GetFileNameWithoutExtension(file) != mid)
                c.R.Fail(id, $"{file}: nome do arquivo deve ser igual ao message_id ({mid}.json)");
        }
        Unique(c, id, "handoffs", handoffIds);
    }

    static void Unique(Context c, string id, string where, IEnumerable<string?> ids)
    {
        foreach (var g in ids.Where(x => x is not null).GroupBy(x => x).Where(g => g.Count() > 1))
            c.R.Fail(id, $"{where}: ID duplicado {g.Key}");
    }

    // --- CHK-SINGLE-AUTHORITY (NN-001, NN-021) ---
    static void SingleAuthority(Context c)
    {
        const string id = "CHK-SINGLE-AUTHORITY";
        c.R.Ran(id);

        foreach (var f in c.RepoFiles().Where(f => Path.GetFileName(f) == "ecosystem.json" && f != "ecosystem.json"))
            c.R.Fail(id, $"segundo manifest raiz encontrado: {f} (a autoridade é /ecosystem.json)");

        var comps = c.Components().ToList();
        foreach (var (a, ca) in comps)
            foreach (var (b, cb) in comps)
            {
                if (string.CompareOrdinal(a, b) >= 0) continue;
                var pa = ca.Str("path"); var pb = cb.Str("path");
                if (pa is null || pb is null) continue;
                if (pa == pb || pa.StartsWith(pb + "/") || pb.StartsWith(pa + "/"))
                    c.R.Fail(id, $"componentes '{a}' e '{b}' reivindicam paths sobrepostos ({pa}, {pb})");
            }

        foreach (var (cid, comp) in comps)
        {
            var status = comp.Str("status");
            var path = comp.Str("path");
            if (path is null || status is null) continue;
            var exists = Directory.Exists(c.P(path));
            if (status is "active" or "deprecated" && !exists)
                c.R.Fail(id, $"componente '{cid}' está '{status}' mas {path}/ não existe");
            if (status is "planned" or "not-migrated" && exists)
                c.R.Fail(id, $"componente '{cid}' está '{status}' mas {path}/ já existe — estado implícito ou autoridade duplicada com a origem");

            if (comp.TryGetProperty("version", out var ver) && ver.ValueKind == JsonValueKind.Object)
            {
                var auth = ver.Str("authority");
                if (auth == "version-file")
                {
                    var vf = ver.Str("file");
                    if (vf is null) c.R.Fail(id, $"componente '{cid}': version.authority=version-file exige version.file");
                    else if (!File.Exists(c.P(vf))) c.R.Fail(id, $"componente '{cid}': arquivo de versão não existe: {vf}");
                }
                if (auth == "source-repository")
                {
                    if (!comp.TryGetProperty("source", out _))
                        c.R.Fail(id, $"componente '{cid}': version.authority=source-repository exige 'source'");
                    if (status is not ("not-migrated" or "migrating"))
                        c.R.Fail(id, $"componente '{cid}': version.authority=source-repository só é válido antes/durante a migração");
                }
            }
            if (status is "not-migrated" or "migrating" && !comp.TryGetProperty("source", out _))
                c.R.Fail(id, $"componente '{cid}' está '{status}' mas não declara 'source'");
        }
    }

    // --- CHK-BOUNDARIES (NN-002, NN-003, NN-007; ARCHITECTURE.md §4) ---
    static void Boundaries(Context c)
    {
        const string id = "CHK-BOUNDARIES";
        c.R.Ran(id);
        var comps = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in c.Components()) comps.TryAdd(k, v); // duplicatas são reportadas por CHK-IDS-UNIQUE
        foreach (var (cid, comp) in comps)
        {
            if (!comp.TryGetProperty("dependencies", out var deps) || deps.ValueKind != JsonValueKind.Array) continue;
            var fromType = comp.Str("type");
            foreach (var d in deps.EnumerateArray())
            {
                var to = d.Str("component"); var kind = d.Str("kind");
                if (to is null) continue;
                if (to == cid) { c.R.Fail(id, $"'{cid}' declara dependência de si mesmo"); continue; }
                if (!comps.TryGetValue(to, out var target)) { c.R.Fail(id, $"'{cid}' depende de componente inexistente '{to}'"); continue; }
                var toType = target.Str("type");

                if ((cid, to) is ("urbe", "lunet2d") or ("lunet2d", "urbe"))
                    c.R.Fail(id, $"dependência proibida {cid} → {to} (NN-002)");
                else if (fromType == "product" && toType == "product")
                    c.R.Fail(id, $"dependência direta Product → Product proibida: {cid} → {to}; use contract/capability/adapter (NN-002, MANIFEST §12)");

                if (to == "hub" && kind == "required")
                    c.R.Fail(id, $"'{cid}' declara o Hub como dependência obrigatória (NN-003, NN-023)");

                if (toType == "portal")
                    c.R.Fail(id, $"'{cid}' depende do portal '{to}': o portal nunca é dependência de nenhum componente (ADD-0001, ADR-0005)");

                if (fromType != "product" && toType == "product")
                    c.R.Fail(id, $"componente não-produto '{cid}' ({fromType}) depende do produto/Host concreto '{to}' (NN-007, MANIFEST §12)");
            }
        }
    }

    // --- CHK-ARCH-REFS (NN-002, NN-003, NN-023; P1-7) ---
    // O grafo de ecosystem.json declara as dependências; este check olha o CÓDIGO REAL dos produtos importados: nenhum Product
    // pode referenciar outro Product nem o Hub (a menos que a dependência esteja declarada e permitida), nem apontar por
    // ProjectReference / file: / link: para fora do próprio diretório.
    static readonly string[] BinaryExt = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".apk", ".aab", ".jar", ".zip", ".gz", ".7z", ".woff", ".woff2", ".ttf", ".otf", ".mp3", ".ogg", ".wav", ".mp4", ".pdf", ".dll", ".exe", ".so", ".keystore", ".jks", ".bin", ".wasm", ".node", ".sqlite", ".db"];
    static readonly Regex ProjectRef = new(@"<(?:ProjectReference|Import|Compile|None|Content)\s[^>]*?(?:Include|Project)\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
    static readonly Regex LocalDep = new(@"""(?:file|link):([^""]+)""");

    static void ArchRefs(Context c)
    {
        const string id = "CHK-ARCH-REFS";
        c.R.Ran(id);
        var products = c.Components().Where(x => x.El.Str("type") == "product" && x.El.Str("status") == "active" && x.El.Str("path") is { } pth && Directory.Exists(c.P(pth))).ToList();
        foreach (var (cid, comp) in products)
        {
            var path = comp.Str("path")!;
            var declared = comp.Arr("dependencies").Select(d => d.Str("component")).Where(x => x is not null).ToHashSet();
            // termos que identificam OUTROS componentes (produtos ativos e o Hub)
            var terms = new List<(string Target, Regex Rx)>();
            foreach (var (oid, other) in c.Components().Where(x => x.Id != cid && x.El.Str("type") == "product"))
            {
                if (declared.Contains(oid)) continue;
                var words = new HashSet<string> { oid, Regex.Replace(oid, @"\d+$", ""), other.Str("name") ?? oid };
                var alt = string.Join("|", words.Where(w => w.Length >= 3).Select(Regex.Escape));
                terms.Add((oid, new Regex($@"\b(?:{alt})\b|apps/{Regex.Escape(oid)}\b", RegexOptions.IgnoreCase)));
            }
            if (!declared.Contains("hub"))
                terms.Add(("hub", new Regex(@"ecosystem[ _.-]?hub|apps/hub\b", RegexOptions.IgnoreCase)));

            // NN-014: pipelines seletivos — todo produto ativo tem um workflow na raiz com filtro de caminho para o próprio diretório.
            var wfDir = c.P(".github/workflows");
            var selective = Directory.Exists(wfDir) && Directory.EnumerateFiles(wfDir, "*.yml").Any(wf =>
            {
                var t = File.ReadAllText(wf);
                return Regex.IsMatch(t, @"(?m)^\s*paths:") && t.Contains($"'{path}/**'");
            });
            if (!selective) c.R.Fail(id, $"produto '{cid}': nenhum workflow em .github/workflows com filtro de caminho '{path}/**' (pipelines seletivos, NN-014)");

            var root = c.P(path);
            foreach (var f in Repo.WalkFiles(root))
            {
                if (BinaryExt.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
                var info = new FileInfo(f);
                if (info.Length > 2_000_000) continue;
                string text;
                try { text = File.ReadAllText(f); } catch { continue; }
                if (text.Contains('\0')) continue;
                var rel = c.Rel(f);
                foreach (var (target, rx) in terms)
                    if (rx.Match(text) is { Success: true } m)
                        c.R.Fail(id, $"{rel}: o produto '{cid}' referencia '{target}' ('{m.Value}'): Product → Product/Hub só por contract/capability declarada (NN-002, NN-003)");

                var ext = Path.GetExtension(f).ToLowerInvariant();
                IEnumerable<string> refs = ext is ".csproj" or ".props" or ".targets" or ".slnx"
                    ? ProjectRef.Matches(text).Select(m => m.Groups[1].Value)
                    : Path.GetFileName(f) == "package.json" ? LocalDep.Matches(text).Select(m => m.Groups[1].Value) : [];
                foreach (var r in refs)
                {
                    if (r.Contains("$(")) continue;
                    var full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, r.Replace('\\', '/')));
                    if (!full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar))
                        c.R.Fail(id, $"{rel}: referência '{r}' aponta para fora de {path}/ (o produto precisa ser autocontido, NN-002, NN-023)");
                }
            }
        }
    }

    // --- CHK-GENERIC-DIRS (NN-004) ---
    static void GenericDirs(Context c)
    {
        const string id = "CHK-GENERIC-DIRS";
        c.R.Ran(id);
        foreach (var d in Directory.EnumerateDirectories(c.Root))
            if (GenericDirNames.Contains(Path.GetFileName(d).ToLowerInvariant()))
                c.R.Fail(id, $"diretório genérico proibido na raiz: {Path.GetFileName(d)}/ (NN-004)");
        foreach (var area in SharedAreas)
        {
            var dir = c.P(area);
            if (!Directory.Exists(dir)) continue;
            foreach (var d in Repo.WalkDirs(dir))
                if (GenericDirNames.Contains(Path.GetFileName(d).ToLowerInvariant()))
                    c.R.Fail(id, $"diretório genérico proibido: {c.Rel(d)}/ (NN-004)");
        }
    }

    // --- CHK-SHARED-DECLARATION (NN-004, NN-022) ---
    static void SharedDeclaration(Context c)
    {
        const string id = "CHK-SHARED-DECLARATION";
        c.R.Ran(id);
        var ids = c.Components().Select(x => x.Id).ToHashSet();
        foreach (var (cid, comp) in c.Components())
        {
            if (!SharedTypes.Contains(comp.Str("type"))) continue;
            foreach (var f in new[] { "responsibility", "contract", "extractionReason", "compatibility" })
                if (string.IsNullOrWhiteSpace(comp.Str(f)))
                    c.R.Fail(id, $"componente compartilhado '{cid}' não declara '{f}' (NN-004)");
            if (!comp.TryGetProperty("consumers", out var cons) || cons.ValueKind != JsonValueKind.Array || cons.GetArrayLength() == 0)
                c.R.Fail(id, $"componente compartilhado '{cid}' não declara consumidores reais (NN-022)");
            else
                foreach (var x in cons.EnumerateArray())
                    if (x.GetString() is { } s && !ids.Contains(s))
                        c.R.Fail(id, $"componente '{cid}': consumidor inexistente '{s}'");
            if (comp.Str("contract") is { } cp && comp.Str("status") == "active" && !File.Exists(c.P(cp)))
                c.R.Fail(id, $"componente '{cid}': contrato não existe: {cp}");
        }
    }

    // --- CHK-ENFORCEMENT-MATRIX (MANIFEST §0.2) ---
    static void EnforcementMatrix(Context c)
    {
        const string id = "CHK-ENFORCEMENT-MATRIX";
        c.R.Ran(id);
        var nn = c.ManifestInvariants();
        if (nn.Count == 0) { c.R.Fail(id, "nenhuma invariante NN-XXX encontrada em MANIFEST.md §0.1"); return; }

        var matrix = c.MatrixInvariants().ToList();
        var inMatrix = matrix.Select(i => i.Str("id")).Where(x => x is not null).ToHashSet();
        foreach (var (nid, _) in nn)
            if (!inMatrix.Contains(nid)) c.R.Fail(id, $"{nid} não está na matriz de enforcement");
        foreach (var x in inMatrix)
            if (!nn.ContainsKey(x!)) c.R.Fail(id, $"{x} está na matriz mas não existe em MANIFEST.md");

        var automated = new[] { "SCHEMA", "TEST", "ARCH", "CI" };
        foreach (var inv in matrix)
        {
            var nid = inv.Str("id") ?? "?";
            if (nn.TryGetValue(nid, out var title) && inv.Str("title") != title)
                c.R.Fail(id, $"{nid}: título na matriz difere do MANIFEST.md (\"{title}\")");
            if (!inv.TryGetProperty("mechanisms", out var mechs) || mechs.ValueKind != JsonValueKind.Array) continue;
            var seen = new HashSet<string>();
            foreach (var m in mechs.EnumerateArray())
            {
                var mech = m.Str("mechanism") ?? "?"; var status = m.Str("status");
                if (!seen.Add(mech)) c.R.Fail(id, $"{nid}: mecanismo {mech} repetido");
                if (status == "implemented" && automated.Contains(mech))
                {
                    var chk = m.Str("check");
                    if (chk is null) c.R.Fail(id, $"{nid}/{mech}: 'implemented' exige referência a um check (CHK-...)");
                    else if (!Ids.Contains(chk)) c.R.Fail(id, $"{nid}/{mech}: check inexistente {chk}");
                }
                if (status == "manual" && (automated.Contains(mech) || mech == "RUNTIME"))
                    c.R.Fail(id, $"{nid}/{mech}: mecanismo automatizável não pode ser 'manual'; use 'implemented' ou 'planned'");
                if (status == "planned" && m.Str("phase") is null)
                    c.R.Fail(id, $"{nid}/{mech}: 'planned' exige 'phase'");
            }
        }
    }

    // --- CHK-AGENTS-NN (NN-010, MANIFEST §25) ---
    static void AgentsNn(Context c)
    {
        const string id = "CHK-AGENTS-NN";
        c.R.Ran(id);
        var path = c.P("AGENTS.md");
        if (!File.Exists(path)) { c.R.Fail(id, "AGENTS.md ausente"); return; }
        var text = File.ReadAllText(path);
        if (!text.Contains("MANIFEST.md")) c.R.Fail(id, "AGENTS.md deve referenciar MANIFEST.md");
        if (!text.Contains("NON-NEGOTIABLES")) c.R.Fail(id, "AGENTS.md deve conter a seção NON-NEGOTIABLES");

        var sections = Regex.Matches(text, @"^### (NN-\d{3}) — (.+?)\s*$", RegexOptions.Multiline);
        var found = new Dictionary<string, (string Title, string Body)>();
        for (var i = 0; i < sections.Count; i++)
        {
            var start = sections[i].Index + sections[i].Length;
            var next = text.IndexOf("\n#", start, StringComparison.Ordinal);
            found[sections[i].Groups[1].Value] = (sections[i].Groups[2].Value, text[start..(next < 0 ? text.Length : next)]);
        }
        foreach (var (nid, title) in c.ManifestInvariants())
        {
            if (!found.TryGetValue(nid, out var s)) { c.R.Fail(id, $"AGENTS.md não reproduz {nid}"); continue; }
            if (s.Title != title) c.R.Fail(id, $"AGENTS.md: título de {nid} difere do MANIFEST.md (\"{title}\")");
            var m = Regex.Match(s.Body, @"\*\*Enforcement obrigatório:\*\*\s*(\S.*)");
            if (!m.Success) c.R.Fail(id, $"AGENTS.md: {nid} sem cláusula 'Enforcement obrigatório'");
        }
        foreach (var nid in found.Keys)
            if (!c.ManifestInvariants().ContainsKey(nid)) c.R.Fail(id, $"AGENTS.md cita {nid}, inexistente no MANIFEST.md");
    }

    // --- CHK-ADR (NN-011, MANIFEST §29) ---
    static readonly string[] AdrSections = ["Status", "Contexto", "Problema", "Opções", "Decisão", "Consequências", "Alternativas rejeitadas"];
    static readonly string[] AdrStatuses = ["Proposto", "Aceito", "Rejeitado", "Substituído", "Obsoleto"];

    static void Adr(Context c)
    {
        const string id = "CHK-ADR";
        c.R.Ran(id);
        var index = File.Exists(c.P("docs/adr/README.md")) ? File.ReadAllText(c.P("docs/adr/README.md")) : "";
        foreach (var dir in Directory.Exists(c.P("docs/adr")) ? Directory.EnumerateFiles(c.P("docs/adr"), "*.md") : [])
        {
            var name = Path.GetFileName(dir);
            if (name is "README.md" or "TEMPLATE.md") continue;
            if (!Regex.IsMatch(name, @"^\d{4}-[a-z0-9]+(-[a-z0-9]+)*\.md$"))
                c.R.Fail(id, $"docs/adr/{name}: nome deve seguir NNNN-titulo-em-kebab.md");
        }
        foreach (var f in c.AdrFiles())
        {
            var name = Path.GetFileName(f);
            var text = File.ReadAllText(f);
            var num = name[..4];
            if (!Regex.IsMatch(text, $@"^# ADR-{num} — \S", RegexOptions.Multiline))
                c.R.Fail(id, $"{name}: primeira linha deve ser '# ADR-{num} — <título>'");
            foreach (var s in AdrSections)
                if (!Regex.IsMatch(text, $@"^## {Regex.Escape(s)}\s*$", RegexOptions.Multiline))
                    c.R.Fail(id, $"{name}: seção obrigatória ausente: '## {s}'");
            var st = Regex.Match(text, @"^## Status\s*\n+\s*(\S+)", RegexOptions.Multiline);
            if (st.Success && !AdrStatuses.Any(s => st.Groups[1].Value.TrimEnd('.', ',', ';').Equals(s)))
                c.R.Fail(id, $"{name}: status '{st.Groups[1].Value}' inválido; use um de: {string.Join(", ", AdrStatuses)}");
            if (!index.Contains(name))
                c.R.Fail(id, $"{name}: não listado no índice docs/adr/README.md");
        }
    }

    // --- CHK-DECISIONS (NN-009, NN-021) ---
    static void Decisions(Context c)
    {
        const string id = "CHK-DECISIONS";
        c.R.Ran(id);
        var comps = c.Components().Select(x => x.Id).Append("ecosystem").ToHashSet();
        foreach (var d in c.DecisionList())
        {
            var did = d.Str("id") ?? "?";
            if (d.Str("component") is { } comp && !comps.Contains(comp))
                c.R.Fail(id, $"{did}: componente inexistente '{comp}'");
            if (d.Str("status") == "pending")
            {
                // DEC-0007: decisão pendente sempre acompanhada do objeto a revisar, para o portal apresentá-lo.
                var related = d.Arr("related").Select(x => x.GetString()).Where(x => x is not null).ToList();
                if (related.Count == 0)
                    c.R.Fail(id, $"{did}: decisão pendente sem objeto ('related'); o portal precisa apresentar o que decidir (DEC-0007)");
                foreach (var r in related)
                    if (!r!.StartsWith("https://") && !File.Exists(c.P(r.Split('#')[0])))
                        c.R.Fail(id, $"{did}: objeto inexistente: {r}");
            }
            if (d.Str("status") == "decided")
            {
                foreach (var f in new[] { "decision", "decidedAt", "record" })
                    if (string.IsNullOrWhiteSpace(d.Str(f))) c.R.Fail(id, $"{did}: decisão tomada exige '{f}' (NN-009)");
                if (d.Str("record") is { } rec && !File.Exists(c.P(rec.Split('#')[0])))
                    c.R.Fail(id, $"{did}: registro persistido não existe: {rec} (NN-009)");
            }
        }
    }

    // --- CHK-HANDOFFS (NN-008, NN-010, NN-017, NN-018) ---
    static void Handoffs(Context c)
    {
        const string id = "CHK-HANDOFFS";
        c.R.Ran(id);
        var comps = c.Components().Select(x => x.Id).Append("ecosystem").ToHashSet();
        var decisions = c.DecisionList().Select(d => d.Str("id")).ToHashSet();
        var adrIds = c.AdrFiles().Select(f => "ADR-" + Path.GetFileName(f)[..4]).ToHashSet();
        var gitHistory = Git(c.Root, "rev-parse", "--is-inside-work-tree").Out == "true" && Git(c.Root, "rev-parse", "--is-shallow-repository").Out == "false";
        // reuse_assessment (ADR-0011): (subject, componente) -> avaliação mais recente
        var assessments = new Dictionary<(string Subject, string Owner), (string Ts, string Status, string File)>();
        var subjectParties = new Dictionary<string, HashSet<string>>();
        foreach (var (file, doc) in c.Handoffs())
        {
            var h = doc.RootElement;
            if (h.ValueKind != JsonValueKind.Object) continue;
            if (h.Str("component") is { } comp && !comps.Contains(comp))
                c.R.Fail(id, $"{file}: componente inexistente '{comp}'");
            foreach (var ra in h.Arr("reuse_assessment"))
            {
                var subject = ra.Str("subject") ?? ""; var st = ra.Str("status") ?? ""; var owner = h.Str("component") ?? "";
                var consumers = ra.Arr("consumers").Select(x => x.GetString() ?? "").ToList();
                var review = ra.Str("extraction_review");
                if (st == "external-consumer-exists")
                {
                    if (consumers.Count == 0) c.R.Fail(id, $"{file}: reuse_assessment '{subject}' é external-consumer-exists sem 'consumers' (precisa de pelo menos um consumidor concreto)");
                    foreach (var cn in consumers)
                    {
                        if (!comps.Contains(cn)) c.R.Fail(id, $"{file}: reuse_assessment '{subject}': consumidor inexistente '{cn}'");
                        if (cn == owner) c.R.Fail(id, $"{file}: reuse_assessment '{subject}': o consumidor '{cn}' é o próprio dono");
                    }
                    if (review is null) c.R.Fail(id, $"{file}: reuse_assessment '{subject}' é external-consumer-exists sem 'extraction_review' (pending, declined ou ADR-NNNN)");
                    else if (review.StartsWith("ADR-") && !adrIds.Contains(review)) c.R.Fail(id, $"{file}: reuse_assessment '{subject}': extraction_review cita {review}, que não existe");
                }
                else if (consumers.Count > 0 || review is not null)
                    c.R.Fail(id, $"{file}: reuse_assessment '{subject}' ({st}) não pode ter 'consumers' nem 'extraction_review': só external-consumer-exists");
                var key = (subject, owner); var ts = h.Str("timestamp") ?? "";
                if (!assessments.TryGetValue(key, out var prev) || string.CompareOrdinal(prev.Ts, ts) < 0) assessments[key] = (ts, st, file);
            }
        }
        // Segundo consumidor (ADR-0011): a mesma necessidade avaliada em 2+ componentes, sem nenhuma avaliação external-consumer-exists, é um
        // candidato esquecido: a Extraction Review precisa ser aberta (e registrada), nunca a extração automática.
        foreach (var grp in assessments.GroupBy(a => a.Key.Subject))
        {
            var owners = grp.Select(g => g.Key.Owner).Distinct().ToList();
            if (owners.Count >= 2 && !grp.Any(g => g.Value.Status == "external-consumer-exists"))
                c.R.Fail(id, $"reuse_assessment '{grp.Key}' aparece em {owners.Count} componentes ({string.Join(", ", owners.Order())}) sem external-consumer-exists: abra a Extraction Review (ADR-0011)");
        }
        foreach (var (file, doc) in c.Handoffs())
        {
            var h = doc.RootElement;
            if (h.ValueKind != JsonValueKind.Object) continue;
            foreach (var d in h.Arr("decisions_required"))
                if (!decisions.Contains(d.GetString())) c.R.Fail(id, $"{file}: decisão inexistente {d.GetString()}");
            foreach (var s in h.Arr("normative_sources"))
                if (s.GetString() is { } p && !File.Exists(c.P(p.Split('#')[0])))
                    c.R.Fail(id, $"{file}: fonte normativa inexistente: {p}");

            var state = h.Str("state");
            var ver = h.Arr("verification").ToList();
            foreach (var v in ver.Where(v => v.Str("kind") == "human" && v.Str("result") == "pending"))
            {
                // DEC-0007: validação humana pendente sempre acompanhada do objeto a validar.
                var obj = v.Str("object");
                if (string.IsNullOrWhiteSpace(obj))
                    c.R.Fail(id, $"{file}: validação humana pendente '{v.Str("check")}' sem 'object'; o portal precisa apresentar o que validar (DEC-0007)");
                else if (!obj.StartsWith("https://") && !File.Exists(c.P(obj.Split('#')[0])))
                    c.R.Fail(id, $"{file}: objeto da validação '{v.Str("check")}' inexistente: {obj}");
            }
            if (state == "done")
            {
                if (ver.Count == 0) c.R.Fail(id, $"{file}: estado 'done' sem verificação (NN-018)");
                foreach (var v in ver)
                {
                    var res = v.Str("result");
                    if (res is not ("passed" or "not-applicable"))
                        c.R.Fail(id, $"{file}: estado 'done' com verificação '{v.Str("check")}' = {res} (NN-017/NN-018)");
                    if (res == "passed" && string.IsNullOrWhiteSpace(v.Str("evidence")))
                        c.R.Fail(id, $"{file}: verificação '{v.Str("check")}' sem evidência (NN-018)");
                }
                if (h.Arr("blockers").Any()) c.R.Fail(id, $"{file}: estado 'done' com bloqueios abertos");
            }
            if (state == "blocked" && !h.Arr("blockers").Any())
                c.R.Fail(id, $"{file}: estado 'blocked' sem bloqueios declarados");

            // Identidades (ADR-0014): BASE (base_commit) ≠ WORK RESULT (commit) ≠ INTEGRATION (merge do pr) ≠ VALIDATION (tested_commit).
            // O handoff não pode mentir: um resultado igual ao ponto de partida, um SHA inexistente ou um resultado que não descende da base.
            var baseSha = h.Str("base_commit"); var resultSha = h.Str("commit");
            if (baseSha is null && string.CompareOrdinal(h.Str("timestamp") ?? "", BaseCommitRequiredFrom) >= 0)
                c.R.Fail(id, $"{file}: handoff a partir de {BaseCommitRequiredFrom[..10]} sem 'base_commit' (de onde o agente partiu; ADR-0014)");
            if (baseSha is not null && resultSha is not null && (baseSha.StartsWith(resultSha) || resultSha.StartsWith(baseSha)))
                c.R.Fail(id, $"{file}: 'commit' (resultado) é o próprio 'base_commit' (ponto de partida); use null se o resultado ainda não é conhecido (ADR-0014)");
            if (gitHistory)
            {
                foreach (var (field, sha) in new[] { ("base_commit", baseSha), ("commit", resultSha) }.Concat(ver.Select(v => ("verification.tested_commit", v.Str("tested_commit")))))
                    if (sha is not null && Git(c.Root, "cat-file", "-e", sha + "^{commit}").Code != 0)
                        c.R.Fail(id, $"{file}: {field} {sha} não existe no histórico (ADR-0014)");
                if (baseSha is not null && resultSha is not null && Git(c.Root, "cat-file", "-e", baseSha + "^{commit}").Code == 0
                    && Git(c.Root, "cat-file", "-e", resultSha + "^{commit}").Code == 0 && Git(c.Root, "merge-base", "--is-ancestor", baseSha, resultSha).Code != 0)
                    c.R.Fail(id, $"{file}: o resultado {resultSha} não descende da base {baseSha} (ADR-0014)");
            }
        }
        if (!gitHistory && c.Handoffs().Any(x => x.Doc.RootElement.ValueKind == JsonValueKind.Object && (x.Doc.RootElement.Str("base_commit") ?? x.Doc.RootElement.Str("commit")) is not null))
            c.R.Note(id, "commits dos handoffs no histórico (sem git completo; use fetch-depth: 0)");
    }

    /// <summary>A partir desta data (timestamp do handoff), 'base_commit' é obrigatório (ADR-0014). Handoffs anteriores ficam como estão.</summary>
    const string BaseCommitRequiredFrom = "2026-10-02T00:00:00Z";

    // --- CHK-VALIDATION (P1-11; NN-001, NN-017, NN-018) ---
    // Registros canônicos de validação por build: o estado precisa ser sustentado pela evidência (CI nunca vira VALIDATED) e a
    // evidência que cita um handoff precisa coincidir com a verificação do handoff (uma só fonte para o mesmo fato).
    static void ValidationRecords(Context c)
    {
        const string id = "CHK-VALIDATION";
        c.R.Ran(id);
        var comps = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (k, v) in c.Components()) comps.TryAdd(k, v); // duplicatas são reportadas por CHK-IDS-UNIQUE
        var handoffs = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (_, d) in c.Handoffs()) handoffs.TryAdd(d.RootElement.Str("message_id") ?? "", d.RootElement);
        var seen = new HashSet<string>();
        foreach (var (file, doc) in c.ValidationRecords())
        {
            var r = doc.RootElement;
            var comp = r.Str("component"); var build = r.Str("build"); var rid = r.Str("record_id");
            if (comp is null || build is null || rid is null) continue; // o schema já reportou
            if (file != $"docs/validation/{comp}/{build}.json") c.R.Fail(id, $"{file}: o caminho deve ser docs/validation/{comp}/{build}.json");
            if (rid != $"VR-{comp}-{build}") c.R.Fail(id, $"{file}: record_id deve ser VR-{comp}-{build}");
            if (!seen.Add(rid)) c.R.Fail(id, $"{file}: record_id duplicado {rid}");
            if (!comps.TryGetValue(comp, out var cc) || cc.Str("type") != "product") { c.R.Fail(id, $"{file}: componente '{comp}' inexistente ou não é um produto"); continue; }

            var ev = r.Arr("evidence").ToList();
            bool Has(string kind, string res) => ev.Any(e => e.Str("kind") == kind && e.Str("result") == res);
            var state = r.Str("state");
            if (ev.Any(e => e.Str("result") == "failed") && state is "VALIDATED" or "AUTOMATED_VERIFIED")
                c.R.Fail(id, $"{file}: estado {state} com evidência reprovada");
            switch (state)
            {
                case "VALIDATED":
                    if (!Has("human", "passed")) c.R.Fail(id, $"{file}: VALIDATED exige evidência humana aprovada (CI nunca basta, NN-017)");
                    if (Has("human", "pending")) c.R.Fail(id, $"{file}: VALIDATED com validação humana ainda pendente");
                    break;
                case "AUTOMATED_VERIFIED":
                    if (!Has("automated", "passed")) c.R.Fail(id, $"{file}: AUTOMATED_VERIFIED exige evidência automática aprovada");
                    break;
                case "HUMAN_VALIDATION_PENDING":
                    if (!Has("human", "pending")) c.R.Fail(id, $"{file}: HUMAN_VALIDATION_PENDING exige uma verificação humana pendente");
                    break;
            }
            foreach (var e in ev.Where(e => e.Str("handoff") is not null))
            {
                var hid = e.Str("handoff")!;
                if (!handoffs.TryGetValue(hid, out var h)) { c.R.Fail(id, $"{file}: handoff {hid} inexistente"); continue; }
                var match = h.Arr("verification").FirstOrDefault(v => v.Str("check") == e.Str("check"));
                if (match.ValueKind != JsonValueKind.Object) c.R.Fail(id, $"{file}: o handoff {hid} não tem a verificação '{e.Str("check")}' (evidência divergente, NN-001)");
                else if (match.Str("result") != e.Str("result") || match.Str("kind") != e.Str("kind"))
                    c.R.Fail(id, $"{file}: a evidência '{e.Str("check")}' diverge do handoff {hid} (registro: {e.Str("kind")}/{e.Str("result")}; handoff: {match.Str("kind")}/{match.Str("result")})");
            }
        }
    }

    // --- CHK-ROADMAP (MANIFEST §46, NN-017) ---
    static void Roadmap(Context c)
    {
        const string id = "CHK-ROADMAP";
        c.R.Ran(id);
        var path = c.P("ROADMAP.md");
        if (!File.Exists(path)) return;
        var text = File.ReadAllText(path);
        for (var i = 0; i <= 7; i++)
            if (!Regex.IsMatch(text, $@"^## Fase {i} — ", RegexOptions.Multiline))
                c.R.Fail(id, $"ROADMAP.md: fase {i} (MANIFEST §46) ausente");
        // Item concluído ([x]) não pode depender de validação humana pendente (NN-017).
        foreach (Match m in Regex.Matches(text, @"^\s*- \[x\].*$", RegexOptions.Multiline))
            if (m.Value.Contains("human validation pending", StringComparison.OrdinalIgnoreCase) || m.Value.Contains("validação humana pendente", StringComparison.OrdinalIgnoreCase))
                c.R.Fail(id, $"ROADMAP.md: item marcado como concluído com validação humana pendente: {m.Value.Trim()}");
    }

    // --- CHK-REGISTRY (ADR-0012; NN-006, NN-007, NN-016, NN-023) ---
    // Contratos da Fase 2 com estrutura confiável: o Registry de produção (ecosystem.json + docs/contracts/capabilities) é consistente e o
    // vertical slice em docs/contracts/examples prova descoberta e compatibilidade (caso positivo) e cada tipo de falha (casos negativos).
    static void RegistryContracts(Context c)
    {
        const string id = "CHK-REGISTRY";
        c.R.Ran(id);
        var perms = RegistryFiles.LoadPermissions(c.P("docs/contracts/permissions.json"));
        if (perms.Count == 0) { c.R.Fail(id, "docs/contracts/permissions.json ausente ou sem permissões"); return; }

        var reg = new Registry(c.Components(), RegistryFiles.LoadContracts(c.P("docs/contracts/capabilities")), perms);
        foreach (var (code, msg) in reg.Validate()) c.R.Fail(id, $"ecosystem.json: {code}: {msg}");

        var schema = c.LoadSchema("docs/contracts/schemas/ecosystem.schema.json");
        var dir = c.P("docs/contracts/examples/registry-slice");
        if (!Directory.Exists(dir) || schema is null) { c.R.Fail(id, "vertical slice ausente: docs/contracts/examples/registry-slice"); return; }
        var exContracts = RegistryFiles.LoadContracts(Path.Combine(dir, "capabilities"));
        var seen = new HashSet<string>();
        foreach (var f in Directory.EnumerateFiles(dir, "*.json").Order())
        {
            var name = Path.GetFileName(f); seen.Add(name);
            using var doc = JsonDocument.Parse(File.ReadAllText(f));
            var comps = doc.RootElement.GetProperty("components");
            var serr = new List<string>();
            new SchemaValidator(schema.RootElement).ValidateAt("#/properties/components", comps, serr);
            foreach (var e in serr) c.R.Fail(id, $"{name}: {e}");
            var rg = new Registry(comps.EnumerateObject().Select(p => (p.Name, p.Value)), exContracts, perms);
            var errors = rg.Validate();
            var expect = doc.RootElement.GetProperty("expect");
            if (expect.Str("error") is { } code)
            {
                if (!errors.Any(e => e.Code == code)) c.R.Fail(id, $"{name}: esperava a falha {code}, obteve [{string.Join(", ", errors.Select(e => e.Code))}]");
            }
            else
            {
                foreach (var (ecode, msg) in errors) c.R.Fail(id, $"{name}: o caso positivo falhou: {ecode}: {msg}");
                if (expect.TryGetProperty("discover", out var disc))
                    foreach (var cap in disc.EnumerateObject())
                    {
                        var want = cap.Value.EnumerateArray().Select(x => x.GetString()!).Order().ToList();
                        var got = rg.Providers(cap.Name).Select(p => $"{p.Component}@{p.Version}").Order().ToList();
                        if (!want.SequenceEqual(got)) c.R.Fail(id, $"{name}: descoberta de {cap.Name}: esperado [{string.Join(", ", want)}], obtido [{string.Join(", ", got)}]");
                    }
            }
        }
        foreach (var required in new[] { "positive.json", "negative-incompatible.json" })
            if (!seen.Contains(required)) c.R.Fail(id, $"vertical slice sem {required}");

        var products = c.Components().Where(x => x.El.Str("type") == "product").Select(x => x.Id).ToHashSet();
        var ids = c.Components().Select(x => x.Id).ToHashSet();
        foreach (var f in (Directory.Exists(c.P("docs/contracts/examples/context")) ? Directory.EnumerateFiles(c.P("docs/contracts/examples/context"), "*.json") : []).Order())
            using (var doc = JsonDocument.Parse(File.ReadAllText(f)))
                foreach (var e in RegistryFiles.ContextErrors(doc.RootElement, products)) c.R.Fail(id, $"{c.Rel(f)}: {e}");
        var compMap = c.Components().GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().El);
        var decisionIds = c.DecisionList().Select(d => d.Str("id") ?? "").ToHashSet();
        var profileFiles = new List<string>();
        if (Directory.Exists(c.P("docs/contracts/examples/distribution"))) profileFiles.AddRange(Directory.EnumerateFiles(c.P("docs/contracts/examples/distribution"), "*.json").Order());
        if (Directory.Exists(c.P("docs/distribution"))) profileFiles.AddRange(Directory.EnumerateFiles(c.P("docs/distribution"), "*.profile.json").Order());
        foreach (var f in profileFiles)
            using (var doc = JsonDocument.Parse(File.ReadAllText(f)))
            {
                var inReal = c.Rel(f).StartsWith("docs/distribution/");
                if (inReal && doc.RootElement.Str("status") is not ("current" or "target")) c.R.Fail(id, $"{c.Rel(f)}: perfil em docs/distribution/ precisa ter status 'current' ou 'target'");
                foreach (var e in RegistryFiles.ProfileErrors(doc.RootElement, compMap, decisionIds)) c.R.Fail(id, $"{c.Rel(f)}: {e}");
            }
        if (!profileFiles.Any(f => c.Rel(f) == "docs/distribution/current.profile.json")) c.R.Fail(id, "docs/distribution/current.profile.json ausente: a distribuição atual dos Products precisa estar descrita como dado (P2-12)");
    }

    // --- CHK-MIGRATION-HISTORY (NN-012; P1-8) ---
    // O histórico importado continua presente: todo commit do commit-map é ancestral do HEAD e toda tag registrada existe no commit
    // registrado. Precisa de histórico completo (CI usa fetch-depth: 0); sem git ou com clone raso é "não verificado", nunca aprovado.
    static (int Code, string Out) Git(string dir, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var proc = Process.Start(psi)!;
            var o = proc.StandardOutput.ReadToEnd(); proc.StandardError.ReadToEnd(); proc.WaitForExit();
            return (proc.ExitCode, o.Trim());
        }
        catch { return (-1, ""); }
    }

    static void MigrationHistory(Context c)
    {
        const string id = "CHK-MIGRATION-HISTORY";
        c.R.Ran(id);
        var products = c.Components().Where(x => x.El.Str("type") == "product" && x.El.Str("status") == "active" && x.El.TryGetProperty("source", out _)).ToList();
        var records = new List<(string Id, JsonElement Rec)>();
        foreach (var (cid, _) in products)
        {
            var f = c.P($"docs/migration/import-{cid}.json");
            if (!File.Exists(f)) { c.R.Fail(id, $"produto importado '{cid}' sem docs/migration/import-{cid}.json (NN-012)"); continue; }
            try { records.Add((cid, JsonDocument.Parse(File.ReadAllText(f)).RootElement)); }
            catch (JsonException e) { c.R.Fail(id, $"import-{cid}.json inválido — {e.Message}"); }
        }
        if (records.Count == 0) return;
        if (Git(c.Root, "rev-parse", "--is-inside-work-tree").Out != "true") { c.R.Note(id, "histórico (não é um repositório git)"); return; }
        if (Git(c.Root, "rev-parse", "--is-shallow-repository").Out != "false") { c.R.Note(id, "histórico (clone raso; use fetch-depth: 0)"); return; }
        var tags = Git(c.Root, "for-each-ref", "--format=%(refname:short) %(*objectname) %(objectname)", "refs/tags").Out
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split(' ')).Where(p => p.Length >= 2)
            .ToDictionary(p => p[0], p => p.Length > 2 && p[1].Length == 40 ? p[1] : p[^1]);
        foreach (var (cid, rec) in records)
        {
            var tip = rec.Str("imported_tip") ?? "";
            if (!Regex.IsMatch(tip, "^[0-9a-f]{40}$")) { c.R.Fail(id, $"{cid}: imported_tip inválido"); continue; }
            if (Git(c.Root, "merge-base", "--is-ancestor", tip, "HEAD").Code != 0) { c.R.Fail(id, $"{cid}: a ponta importada {tip[..8]} não é ancestral do HEAD (NN-012)"); continue; }
            var count = Git(c.Root, "rev-list", "--count", tip).Out;
            if (count != rec.GetProperty("commits").GetInt32().ToString()) c.R.Fail(id, $"{cid}: o histórico importado tem {count} commits, esperado {rec.GetProperty("commits").GetInt32()} (NN-012)");
            var tagFile = c.P($"docs/migration/tags-{cid}.txt");
            if (!File.Exists(tagFile)) { c.R.Fail(id, $"{cid}: docs/migration/tags-{cid}.txt ausente"); continue; }
            var lines = File.ReadAllLines(tagFile).Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(p => p.Length == 2).ToList();
            if (lines.Count != rec.GetProperty("tags").GetInt32()) c.R.Fail(id, $"{cid}: tags-{cid}.txt lista {lines.Count} tags, esperado {rec.GetProperty("tags").GetInt32()}");
            foreach (var l in lines)
                if (!tags.TryGetValue(l[0], out var sha)) c.R.Fail(id, $"{cid}: tag {l[0]} ausente (NN-012)");
                else if (sha != l[1]) c.R.Fail(id, $"{cid}: tag {l[0]} aponta para {sha[..8]}, esperado {l[1][..8]} (NN-012)");
        }
    }

    // --- CHK-STATE-CONSISTENCY (ADR-0010; NN-001, NN-017, NN-021) ---
    // Deriva semântica: duas fontes ESTRUTURADAS afirmando estados incompatíveis. Nunca procura palavras em prosa.
    // Fontes: linhas de formato fixo do ROADMAP (itens `- [x] P1-3 —` e `*Estado do gate:* **aprovado**`), ecosystem.json,
    // decisions.json, matriz de enforcement e (quando existe) o instantâneo das Issues em site/data/issues-snapshot.json
    // (gerado pelo CI com `gh issue list --json number,title,state,labels`; ausente = "não verificado", nunca aprovado em silêncio).
    sealed record RoadmapPhase(int Number, string Gate, List<(string Id, char Mark)> Items);

    static List<RoadmapPhase> ParseRoadmap(string text)
    {
        var phases = new List<RoadmapPhase>();
        var heads = Regex.Matches(text, @"^## (.*)$", RegexOptions.Multiline).ToList();
        for (var i = 0; i < heads.Count; i++)
        {
            var m = Regex.Match(heads[i].Groups[1].Value, @"^Fase (\d+) — ");
            if (!m.Success) continue;
            var end = i + 1 < heads.Count ? heads[i + 1].Index : text.Length;
            var body = text[heads[i].Index..end];
            var gate = Regex.Match(body, @"^\*Estado do gate:\* \*\*(aprovado|aguardando|não iniciado)\*\*", RegexOptions.Multiline) is { Success: true } g ? g.Groups[1].Value : "não iniciado";
            var items = Regex.Matches(body, @"^- \[( |~|x)\] (P\d+-\d+) ", RegexOptions.Multiline).Select(x => (x.Groups[2].Value, x.Groups[1].Value[0])).ToList();
            phases.Add(new RoadmapPhase(int.Parse(m.Groups[1].Value), gate, items));
        }
        return phases;
    }

    static void StateConsistency(Context c)
    {
        const string id = "CHK-STATE-CONSISTENCY";
        c.R.Ran(id);
        if (!File.Exists(c.P("ROADMAP.md"))) return;
        var phases = ParseRoadmap(File.ReadAllText(c.P("ROADMAP.md")));
        var approved = phases.Where(p => p.Gate == "aprovado").Select(p => p.Number).ToHashSet();

        // 1. Gate aprovado não tem itens da fase abertos.
        foreach (var p in phases.Where(p => p.Gate == "aprovado"))
            foreach (var (tid, mark) in p.Items.Where(i => i.Mark != 'x'))
                c.R.Fail(id, $"ROADMAP: o gate da Fase {p.Number} está 'aprovado', mas {tid} continua {(mark == '~' ? "[~]" : "[ ]")}");

        // 2. A fase não é copiada em ecosystem.json (ADR-0010, DEC-0019-A): a autoridade é o ROADMAP. O schema recusa o campo.

        // 3. ROADMAP × Issues (DEC-0003).
        var snap = c.P("site/data/issues-snapshot.json");
        if (!File.Exists(snap)) c.R.Note(id, "ROADMAP × Issues (instantâneo das Issues ausente: gere site/data/issues-snapshot.json com `gh issue list --state all --json number,title,state,labels`)");
        else
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(snap));
            var issues = doc.RootElement.EnumerateArray().Select(i => (
                Number: i.GetProperty("number").GetInt32(),
                Title: i.Str("title") ?? "",
                Open: string.Equals(i.Str("state"), "OPEN", StringComparison.OrdinalIgnoreCase),
                State: i.Arr("labels").Select(l => l.ValueKind == JsonValueKind.String ? l.GetString()! : l.Str("name")!).FirstOrDefault(l => l?.StartsWith("state:") == true)?.Substring(6))).ToList();
            foreach (var (tid, mark) in phases.SelectMany(p => p.Items))
            {
                var mine = issues.Where(i => Regex.IsMatch(i.Title, $@"^{Regex.Escape(tid)}(\s|$)")).ToList();
                if (mark == 'x')
                    foreach (var i in mine)
                    {
                        if (i.Open) c.R.Fail(id, $"{tid} está [x] no ROADMAP, mas a Issue #{i.Number} continua aberta (DEC-0003)");
                        else if (i.State is not (null or "done")) c.R.Fail(id, $"{tid} está [x] no ROADMAP, mas a Issue #{i.Number} tem state:{i.State}");
                    }
                else if (mark == '~')
                {
                    if (!mine.Any(i => i.Open && i.State != "done")) c.R.Fail(id, $"{tid} está [~] (em andamento/aguardando), mas não há Issue aberta com state:<estado> da tarefa (DEC-0003)");
                }
            }
        }

        // 4. "Não decidido" de ARCHITECTURE.md só cita decisões pendentes (ou transitórias).
        if (File.Exists(c.P("ARCHITECTURE.md")))
        {
            var arch = File.ReadAllText(c.P("ARCHITECTURE.md"));
            var sec = Regex.Match(arch, @"^### 8\.2 Não decidido.*?(?=^## |^### |\z)", RegexOptions.Multiline | RegexOptions.Singleline);
            if (sec.Success)
            {
                var decisions = c.DecisionList().ToDictionary(d => d.Str("id")!, d => d);
                foreach (Match m in Regex.Matches(sec.Value, @"DEC-\d{4}"))
                    if (!decisions.TryGetValue(m.Value, out var d)) c.R.Fail(id, $"ARCHITECTURE.md §8.2 cita {m.Value}, que não existe em decisions.json");
                    else if (d.Str("status") != "pending" && !d.TryGetProperty("transitional", out _))
                    {
                        // Registro automático (consequencesApplied=false) é um estado transitório legítimo: o registrador não edita
                        // documentos e nunca pode ser bloqueado por eles. Fica visível como pendência do agente, sem falhar.
                        if (d.TryGetProperty("consequencesApplied", out var ca) && ca.ValueKind == JsonValueKind.False)
                            c.R.Note(id, $"{m.Value} foi decidida e ainda tem consequências a aplicar nos documentos (ARCHITECTURE.md §8.2 a cita como aberta)");
                        else c.R.Fail(id, $"ARCHITECTURE.md §8.2 apresenta {m.Value} como não decidido, mas ela está '{d.Str("status")}'");
                    }
            }
        }

        // 4b. Decisões registradas pela automação com consequências ainda a aplicar: pendência do agente, visível, não bloqueante.
        foreach (var d in c.DecisionList().Where(d => d.Str("status") == "decided" && d.TryGetProperty("consequencesApplied", out var ca) && ca.ValueKind == JsonValueKind.False))
            c.R.Note(id, $"{d.Str("id")}: decisão registrada, consequências nos documentos dependentes ainda por aplicar (AGENTS.md §3)");

        // 5. Matriz de enforcement: mecanismo planejado para uma fase cujo gate já foi aprovado é plano vencido.
        if (File.Exists(c.P("docs/governance/enforcement-matrix.json")))
        {
            using var mx = JsonDocument.Parse(File.ReadAllText(c.P("docs/governance/enforcement-matrix.json")));
            foreach (var inv in mx.RootElement.Arr("invariants"))
                foreach (var mech in inv.Arr("mechanisms").Where(x => x.Str("status") == "planned"))
                    if (mech.Str("phase") is { } mp && Regex.Match(mp, @"^phase-(\d+)$") is { Success: true } mm && approved.Contains(int.Parse(mm.Groups[1].Value)))
                        c.R.Fail(id, $"enforcement-matrix: {inv.Str("id")} {mech.Str("mechanism")} ainda 'planned' para {mp}, cujo gate já foi aprovado (implementar, reclassificar ou re-faseá-lo)");
        }
    }

    // --- CHK-SECRETS (MANIFEST §30.2) ---
    static void Secrets(Context c)
    {
        const string id = "CHK-SECRETS";
        c.R.Ran(id);
        // Padrões montados por concatenação para que este arquivo não case consigo mesmo.
        var patterns = new[]
        {
            "gh" + "[pousr]_[A-Za-z0-9]{36,}",
            "github" + "_pat_[A-Za-z0-9_]{22,}",
            "sk-" + "ant-[A-Za-z0-9_-]{20,}",
            "sk-" + "[A-Za-z0-9]{40,}",
            "AK" + "IA[0-9A-Z]{16}",
            "-----BEGIN " + "([A-Z]+ )?PRIVATE KEY-----",
        }.Select(p => new Regex(p)).ToArray();

        foreach (var rel in c.RepoFiles())
        {
            var full = c.P(rel);
            var info = new FileInfo(full);
            if (info.Length > 2_000_000) continue;
            string text;
            try { text = File.ReadAllText(full); } catch { continue; }
            if (text.Contains('\0')) continue;
            foreach (var p in patterns)
                if (p.IsMatch(text)) { c.R.Fail(id, $"{rel}: possível segredo commitado (padrão {p})"); break; }
        }
    }

    // --- CHK-DECISION-FLOW (DEC-0010, ADR-0007; NN-009, NN-016) ---
    // O workflow que registra decisões escreve na branch padrão a partir de texto de Issue: precisa continuar restrito ao dono,
    // com permissões explícitas e sem interpolar texto não confiável em scripts.
    static readonly Regex EnvLine = new(@"^\s+[A-Z_]+: \$\{\{ github\.event\.[a-z_.]+ \}\}\s*$");

    static void DecisionFlow(Context c)
    {
        const string id = "CHK-DECISION-FLOW";
        c.R.Ran(id);
        const string wf = ".github/workflows/decision.yml", script = ".github/scripts/apply-decision.cs";
        if (!File.Exists(c.P(script))) c.R.Fail(id, $"{script} ausente");
        if (!File.Exists(c.P(wf))) { c.R.Fail(id, $"{wf} ausente"); return; }
        var text = File.ReadAllText(c.P(wf));
        if (!text.Contains("github.event.issue.user.login == github.repository_owner"))
            c.R.Fail(id, $"{wf}: falta a guarda que restringe o workflow ao dono do repositório (NN-016)");
        if (!text.Contains("startsWith(github.event.issue.title, 'Decisão DEC-')"))
            c.R.Fail(id, $"{wf}: falta o filtro de título 'Decisão DEC-' gerado pelo portal");
        if (!text.Contains("startsWith(github.event.issue.title, 'Validação ')"))
            c.R.Fail(id, $"{wf}: falta o filtro de título 'Validação ' gerado pelo portal (ADR-0008)");
        if (!Regex.IsMatch(text, @"(?m)^permissions:\s*$")) c.R.Fail(id, $"{wf}: permissões explícitas ausentes (NN-016)");
        if (!text.Contains(".github/scripts/apply-decision.cs")) c.R.Fail(id, $"{wf}: não chama o aplicador");
        if (!text.Contains("tests/consistency/Check.cs")) c.R.Fail(id, $"{wf}: não roda os checks antes de gravar");
        // Veredito (falso-sucesso do caso DEC-0019): o último passo precisa terminar com o código do veredito, e o veredito
        // vem do script testado, não de texto. Gravar/conferir só quando o aplicador registrou algo agora (outcome=registered).
        const string verdict = ".github/scripts/decision-verdict.sh";
        if (!File.Exists(c.P(verdict))) c.R.Fail(id, $"{verdict} ausente");
        var lastStep = text[text.LastIndexOf("      - name:", StringComparison.Ordinal)..];
        if (!lastStep.Contains("if: always()") || !lastStep.Contains("decision-verdict.sh") || !Regex.IsMatch(lastStep, @"(?m)^\s+exit ""\$verdict_code""\s*$"))
            c.R.Fail(id, $"{wf}: o último passo não termina com o veredito (exit \"$verdict_code\"): uma decisão recusada poderia terminar verde");
        if (Regex.Matches(text, @"steps\.apply\.outputs\.outcome == 'registered'").Count < 2)
            c.R.Fail(id, $"{wf}: conferir e gravar precisam depender de outcome == 'registered' (a repetição idempotente não grava nada)");
        var n = 0;
        foreach (var line in text.Split('\n'))
        {
            n++;
            var unsafeField = line.Contains("github.event.issue.title") || line.Contains("github.event.issue.body")
                || line.Contains("github.event.comment") || line.Contains("github.head_ref");
            if (unsafeField && !line.TrimStart().StartsWith("if:") && !EnvLine.IsMatch(line))
                c.R.Fail(id, $"{wf}:{n}: texto não confiável fora de 'env' (risco de injeção em script): {line.Trim()}");
        }
        var js = File.Exists(c.P("site/app.js")) ? File.ReadAllText(c.P("site/app.js")) : "";
        if (!js.Contains("/issues/new?title=")) c.R.Fail(id, "site/app.js: o portal não monta o link de resposta (/issues/new?title=)");
        if (!js.Contains("p.approve") || !js.Contains("p.reject")) c.R.Fail(id, "site/app.js: o portal não oferece Aprovar/Reprovar nas validações pendentes (ADR-0008)");
    }

    // --- CHK-PORTAL (ADD-0001, ADR-0005; NN-001, NN-017, NN-021) ---
    // O portal é projeção: não pode conter dados canônicos escritos à mão, e a projeção gerada
    // (quando presente) precisa bater com as fontes canônicas.
    static readonly Regex CanonicalLiteral = new(@"[""'`]v?\d+\.\d+\.\d+|\b(DEC-\d{4}|HO-\d{8}|NN-\d{3})\b|phase-\d");

    static void Portal(Context c)
    {
        const string id = "CHK-PORTAL";
        c.R.Ran(id);
        foreach (var (cid, comp) in c.Components().Where(x => x.El.Str("type") == "portal"))
        {
            var path = comp.Str("path");
            if (path is null || comp.Str("status") != "active" || !Directory.Exists(c.P(path))) continue;
            foreach (var f in new[] { "index.html", "app.js", "style.css" })
                if (!File.Exists(c.P($"{path}/{f}"))) c.R.Fail(id, $"portal '{cid}': {path}/{f} ausente");

            var published = Repo.WalkFiles(c.P(path))
                .Select(c.Rel)
                .Where(f => !f.StartsWith($"{path}/generator/") && !f.StartsWith($"{path}/data/") && !f.StartsWith($"{path}/testing/"))
                .Where(f => f.EndsWith(".html") || f.EndsWith(".js") || f.EndsWith(".css"))
                .ToList();
            var referencesProjection = false;
            foreach (var f in published)
            {
                var text = File.ReadAllText(c.P(f));
                referencesProjection |= text.Contains("data/ecosystem-status.json");
                foreach (Match m in CanonicalLiteral.Matches(text))
                    c.R.Fail(id, $"{f}: dado canônico escrito à mão no portal ('{m.Value}'); deve vir da projeção (NN-001)");
            }
            if (!referencesProjection)
                c.R.Fail(id, $"portal '{cid}': nenhum arquivo consome data/ecosystem-status.json");

            var proj = $"{path}/data/ecosystem-status.json";
            if (File.Exists(c.P(proj))) Projection(c, id, proj);
        }
    }

    static void Projection(Context c, string id, string rel)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(File.ReadAllText(c.P(rel))); }
        catch (JsonException e) { c.R.Fail(id, $"{rel}: JSON inválido — {e.Message}"); return; }
        var schema = c.LoadSchema("docs/contracts/schemas/ecosystem-status.schema.json");
        if (schema is null) { c.R.Fail(id, "schema ecosystem-status.schema.json ausente"); return; }
        var errors = new List<string>();
        new SchemaValidator(schema.RootElement).Validate(doc.RootElement, errors);
        foreach (var e in errors) c.R.Fail(id, $"{rel}: {e}");
        if (errors.Count > 0) return;

        var p = doc.RootElement;
        var eco = c.Ecosystem!.Value.GetProperty("ecosystem");
        if (p.GetProperty("source").Str("repository") != eco.Str("repository"))
            c.R.Fail(id, $"{rel}: repositório diverge de ecosystem.json");

        if (File.Exists(c.P("ROADMAP.md")))
        {
            var expectedGates = ParseRoadmap(File.ReadAllText(c.P("ROADMAP.md"))).Select(x => $"{x.Number}:{x.Gate}").Order().ToList();
            var projectedGates = p.GetProperty("ecosystem").Arr("gates").Select(x => $"{x.GetProperty("phase").GetInt32()}:{x.Str("state")}").Order().ToList();
            if (!expectedGates.SequenceEqual(projectedGates))
                c.R.Fail(id, $"{rel}: os gates projetados divergem do ROADMAP (ROADMAP: {string.Join(" ", expectedGates)}; projeção: {string.Join(" ", projectedGates)})");
        }

        var canonical = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in c.Components()) canonical.TryAdd(k, v); // duplicatas são reportadas por CHK-IDS-UNIQUE
        var projected = p.Arr("components").ToList();
        var projectedIds = projected.Select(x => x.Str("id")!).ToHashSet();
        if (!projectedIds.SetEquals(canonical.Keys))
            c.R.Fail(id, $"{rel}: componentes divergem de ecosystem.json (projeção: {string.Join(", ", projectedIds.Order())})");
        foreach (var pc in projected)
        {
            var pid = pc.Str("id")!;
            if (canonical.TryGetValue(pid, out var cc))
                foreach (var f in new[] { "name", "type", "status", "description" })
                    if (pc.Str(f) != cc.Str(f)) c.R.Fail(id, $"{rel}: '{pid}.{f}' diverge de ecosystem.json");
            var v = pc.GetProperty("validation");
            if (v.Str("state") == "VALIDATED" && string.IsNullOrWhiteSpace(v.Str("evidence")))
                c.R.Fail(id, $"{rel}: '{pid}' apresentado como VALIDATED sem evidência (NN-017)");
            var latest = c.ValidationRecords().Select(x => x.Doc.RootElement).Where(x => x.Str("component") == pid)
                .OrderBy(x => x.Str("recorded_at"), StringComparer.Ordinal).ThenBy(x => x.Str("build"), StringComparer.Ordinal).LastOrDefault();
            var expectedState = latest.ValueKind == JsonValueKind.Object ? latest.Str("state") : "UNKNOWN";
            if (v.Str("state") != expectedState)
                c.R.Fail(id, $"{rel}: '{pid}.validation.state' ({v.Str("state")}) diverge do registro canônico de validação ({expectedState})");
        }

        // Validações humanas pendentes: último handoff de cada tarefa ainda não encerrado.
        var expected = c.Handoffs()
            .Select(h => h.Doc.RootElement)
            .GroupBy(h => h.Str("task_id"))
            .Select(g => g.OrderBy(h => h.Str("timestamp"), StringComparer.Ordinal).Last())
            .Where(h => h.Str("state") is not ("done" or "cancelled" or "failed"))
            .SelectMany(h => h.Arr("verification")
                .Where(v => v.Str("kind") == "human" && v.Str("result") == "pending")
                .Select(v => $"{h.Str("task_id")}|{v.Str("check")}"))
            .ToHashSet();
        var actual = p.Arr("pendingValidations").Select(v => $"{v.Str("taskId")}|{v.Str("check")}").ToHashSet();
        if (!actual.SetEquals(expected))
            c.R.Fail(id, $"{rel}: validações pendentes divergem dos handoffs");

        // Decisões pendentes (DEC-0007): o portal não pode omitir nenhuma, nem inventar outra.
        var expectedDecisions = c.DecisionList().Where(d => d.Str("status") == "pending").Select(d => d.Str("id")!).ToHashSet();
        var projectedDecisions = p.Arr("pendingDecisions").Select(d => d.Str("id")!).ToHashSet();
        if (!projectedDecisions.SetEquals(expectedDecisions))
            c.R.Fail(id, $"{rel}: decisões pendentes divergem de decisions.json (esperadas: {string.Join(", ", expectedDecisions.Order())}; projetadas: {string.Join(", ", projectedDecisions.Order())})");

        // Candidatos a reutilização (ADR-0011): a projeção só pode listar o que os handoffs registram, e nada pode faltar.
        var expectedReuse = new Dictionary<(string, string), (string Ts, string Status)>();
        foreach (var (_, hd) in c.Handoffs())
            foreach (var ra in hd.RootElement.Arr("reuse_assessment"))
            {
                var key = (ra.Str("subject") ?? "", hd.RootElement.Str("component") ?? ""); var ts = hd.RootElement.Str("timestamp") ?? "";
                if (!expectedReuse.TryGetValue(key, out var prev) || string.CompareOrdinal(prev.Ts, ts) < 0) expectedReuse[key] = (ts, ra.Str("status") ?? "");
            }
        var expectedReuseSet = expectedReuse.Where(x => x.Value.Status != "product-specific").Select(x => $"{x.Key.Item1}|{x.Key.Item2}|{x.Value.Status}").ToHashSet();
        var projectedReuse = p.Arr("reuseCandidates").Select(x => $"{x.Str("subject")}|{x.Str("component")}|{x.Str("status")}").ToHashSet();
        if (!projectedReuse.SetEquals(expectedReuseSet)) c.R.Fail(id, $"{rel}: candidatos a reutilização divergem dos handoffs");

        // Distribuição atual e capabilities (P2-13): derivadas, conferidas contra as fontes.
        var profPath = c.P("docs/distribution/current.profile.json");
        var expectedChannels = new HashSet<string>();
        if (File.Exists(profPath))
            using (var pd = JsonDocument.Parse(File.ReadAllText(profPath)))
                foreach (var e in pd.RootElement.Arr("entries"))
                    foreach (var ch in e.Arr("channels")) expectedChannels.Add($"{e.Str("component")}|{ch.Str("id")}|{ch.Str("kind")}|{ch.Str("role")}");
        var projectedChannels = (p.TryGetProperty("distribution", out var pdist) && pdist.ValueKind == JsonValueKind.Object ? pdist.Arr("channels") : [])
            .Select(x => $"{x.Str("component")}|{x.Str("channel")}|{x.Str("kind")}|{x.Str("role")}").ToHashSet();
        if (!projectedChannels.SetEquals(expectedChannels)) c.R.Fail(id, $"{rel}: canais de distribuição divergem de docs/distribution/current.profile.json");
        var expectedCaps = new HashSet<string>();
        foreach (var (cid, comp) in c.Components())
            foreach (var key in new[] { "provides", "requires" })
                foreach (var x in comp.Arr(key)) expectedCaps.Add($"{x.Str("capability")}|{key}|{cid}");
        var projectedCaps = new HashSet<string>();
        foreach (var cap in p.Arr("capabilities"))
        {
            foreach (var pr in cap.Arr("providers")) projectedCaps.Add($"{cap.Str("id")}|provides|{pr.GetString()}");
            foreach (var cs in cap.Arr("consumers")) projectedCaps.Add($"{cap.Str("id")}|requires|{cs.GetString()}");
        }
        if (!projectedCaps.SetEquals(expectedCaps)) c.R.Fail(id, $"{rel}: capabilities projetadas divergem de provides/requires em ecosystem.json");

        foreach (var d in p.Arr("docs"))
            if (d.Str("path") is { } dp && !File.Exists(c.P(dp)))
                c.R.Fail(id, $"{rel}: documento inexistente {dp}");
    }
}

// ---------------------------------------------------------------------------------------------

sealed class Context(string root, Report r)
{
    public string Root { get; } = root;
    public Report R { get; } = r;
    public List<(string File, JsonDocument Doc)> Json { get; } = [];
    readonly Dictionary<string, JsonDocument?> schemas = [];
    Dictionary<string, string>? manifestNn;
    List<string>? repoFiles;

    public string P(string rel) => Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));
    public string Rel(string full) => Path.GetRelativePath(Root, full).Replace(Path.DirectorySeparatorChar, '/');

    public JsonElement? Ecosystem => Json.FirstOrDefault(j => j.File == "ecosystem.json").Doc?.RootElement;

    public void LoadJson()
    {
        var files = new List<string> { "ecosystem.json", "docs/governance/decisions.json", "docs/governance/enforcement-matrix.json" };
        var hdir = P("docs/governance/handoffs");
        if (Directory.Exists(hdir))
            files.AddRange(Directory.EnumerateFiles(hdir, "*.json").Order().Select(Rel));
        if (File.Exists(P("docs/contracts/permissions.json"))) files.Add("docs/contracts/permissions.json");
        foreach (var d in new[] { "docs/contracts/capabilities", "docs/contracts/examples" })
            if (Directory.Exists(P(d)))
                files.AddRange(Directory.EnumerateFiles(P(d), "*.json", SearchOption.AllDirectories).Order().Select(Rel)
                    .Where(f => f.Contains("/capabilities/") || f.Contains("/examples/context/") || f.Contains("/examples/distribution/")));
        if (Directory.Exists(P("docs/distribution")))
            files.AddRange(Directory.EnumerateFiles(P("docs/distribution"), "*.profile.json").Order().Select(Rel));
        var vdir = P("docs/validation");
        if (Directory.Exists(vdir))
            files.AddRange(Directory.EnumerateFiles(vdir, "*.json", SearchOption.AllDirectories).Order().Select(Rel));
        foreach (var f in files)
        {
            if (!File.Exists(P(f))) { R.Fail("CHK-SCHEMA", $"{f} ausente"); continue; }
            try { Json.Add((f, JsonDocument.Parse(File.ReadAllText(P(f))))); }
            catch (JsonException e) { R.Fail("CHK-SCHEMA", $"{f}: JSON inválido — {e.Message}"); }
        }
    }

    public JsonDocument? LoadSchema(string rel)
    {
        if (schemas.TryGetValue(rel, out var d)) return d;
        try { d = File.Exists(P(rel)) ? JsonDocument.Parse(File.ReadAllText(P(rel))) : null; } catch (JsonException) { d = null; }
        return schemas[rel] = d;
    }

    public IEnumerable<(string Id, JsonElement El)> Components()
    {
        if (Ecosystem is { } e && e.ValueKind == JsonValueKind.Object && e.TryGetProperty("components", out var c) && c.ValueKind == JsonValueKind.Object)
            foreach (var p in c.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Object) yield return (p.Name, p.Value);
    }

    public IEnumerable<JsonElement> DecisionList() =>
        Json.FirstOrDefault(j => j.File == "docs/governance/decisions.json").Doc?.RootElement.Arr("decisions") ?? [];

    public IEnumerable<JsonElement> MatrixInvariants() =>
        Json.FirstOrDefault(j => j.File == "docs/governance/enforcement-matrix.json").Doc?.RootElement.Arr("invariants") ?? [];

    public IEnumerable<(string File, JsonDocument Doc)> ValidationRecords() => Json.Where(j => j.File.StartsWith("docs/validation/"));

    public IEnumerable<(string File, JsonDocument Doc)> Handoffs() => Json.Where(j => j.File.StartsWith("docs/governance/handoffs/"));

    public IEnumerable<string> AdrFiles() =>
        Directory.Exists(P("docs/adr"))
            ? Directory.EnumerateFiles(P("docs/adr"), "*.md").Where(f => Regex.IsMatch(Path.GetFileName(f), @"^\d{4}-.*\.md$")).Order()
            : [];

    /// <summary>Invariantes NN-XXX de MANIFEST.md §0.1 (ID → título), extraídas do próprio manifesto.</summary>
    public Dictionary<string, string> ManifestInvariants()
    {
        if (manifestNn is not null) return manifestNn;
        manifestNn = [];
        if (!File.Exists(P("MANIFEST.md"))) return manifestNn;
        foreach (Match m in Regex.Matches(File.ReadAllText(P("MANIFEST.md")), @"^## (NN-\d{3}) — (.+?)\s*$", RegexOptions.Multiline))
            manifestNn[m.Groups[1].Value] = m.Groups[2].Value;
        return manifestNn;
    }

    public List<string> RepoFiles() => repoFiles ??= Repo.WalkFiles(Root).Select(Rel).Order().ToList();
}

sealed class Report
{
    readonly List<string> ran = [];
    readonly List<(string Check, string Message, bool Crash)> failures = [];
    public bool Failed => failures.Count > 0;
    public void Ran(string id) { if (!ran.Contains(id)) ran.Add(id); }
    public void Fail(string id, string msg) { Ran(id); failures.Add((id, msg, false)); }
    readonly List<(string Check, string Message)> notes = [];
    public void Note(string id, string msg) { Ran(id); notes.Add((id, msg)); }
    public void Crash(string id, string msg) { Ran(id); failures.Add((id, "erro interno do check — " + msg, true)); }
    public bool FailedCheck(string id) => failures.Any(f => f.Check == id && !f.Crash);
    public bool Crashed => failures.Any(f => f.Crash);

    public void Print(TextWriter w)
    {
        foreach (var id in ran)
        {
            var mine = failures.Where(f => f.Check == id).ToList();
            w.WriteLine($"{(mine.Count == 0 ? "PASS" : "FAIL")}  {id}");
            foreach (var f in mine) w.WriteLine($"      - {f.Message}");
            foreach (var n in notes.Where(n => n.Check == id)) w.WriteLine($"      ~ {(n.Message.StartsWith("DEC-") || n.Message.Contains("ainda tem consequências") ? "pendência" : "não verificado")}: {n.Message}");
        }
        w.WriteLine();
        w.WriteLine(Failed
            ? $"{failures.Count} falha(s) em {failures.Select(f => f.Check).Distinct().Count()} check(s)."
            : $"Todos os {ran.Count} checks passaram.");
    }
}

static class Repo
{
    static readonly string[] Skip = [".git", "bin", "obj", "node_modules", ".vs", ".idea"];

    public static string? FindRoot(string start)
    {
        for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "MANIFEST.md")) && File.Exists(Path.Combine(d.FullName, "ecosystem.json")))
                return d.FullName;
        return null;
    }

    public static IEnumerable<string> WalkDirs(string dir)
    {
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            if (Skip.Contains(Path.GetFileName(d))) continue;
            yield return d;
            foreach (var x in WalkDirs(d)) yield return x;
        }
    }

    public static IEnumerable<string> WalkFiles(string dir) =>
        Directory.EnumerateFiles(dir).Concat(WalkDirs(dir).SelectMany(Directory.EnumerateFiles));
}

// =============================================================================================================================
// Registry inicial e validador de compatibilidade (Fase 2, ADR-0012). Local-first (ADR-0011): nasce DENTRO dos checks do Ecosystem,
// onde estão seus consumidores reais (CHK-REGISTRY, `--registry`); só será promovido a componente próprio quando o Hub for consumidor.
// =============================================================================================================================

readonly record struct SemVer(int Major, int Minor, int Patch) : IComparable<SemVer>
{
    public static bool TryParse(string? s, out SemVer v)
    {
        v = default;
        var m = Regex.Match(s ?? "", @"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$");
        if (!m.Success) return false;
        v = new SemVer(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
        return true;
    }
    public int CompareTo(SemVer o) => Major != o.Major ? Major.CompareTo(o.Major) : Minor != o.Minor ? Minor.CompareTo(o.Minor) : Patch.CompareTo(o.Patch);
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

static class VersionRange
{
    /// <summary>Exata (1.2.0), caret (^1.2.0), tilde (~1.2.0) ou comparadores separados por espaço (&gt;=1.0.0 &lt;2.0.0). Inválida: null.</summary>
    public static bool? Satisfies(SemVer v, string range)
    {
        range = range.Trim();
        if (range.Length == 0) return null;
        if (range[0] is '^' or '~')
        {
            if (!SemVer.TryParse(range[1..], out var b)) return null;
            SemVer upper = range[0] == '~' ? new(b.Major, b.Minor + 1, 0)
                : b.Major > 0 ? new(b.Major + 1, 0, 0) : b.Minor > 0 ? new(0, b.Minor + 1, 0) : new(0, 0, b.Patch + 1);
            return v.CompareTo(b) >= 0 && v.CompareTo(upper) < 0;
        }
        if (SemVer.TryParse(range, out var exact)) return v.CompareTo(exact) == 0;
        var ok = true;
        foreach (var part in range.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Regex.Match(part, @"^(>=|>|<=|<)(.+)$");
            if (!m.Success || !SemVer.TryParse(m.Groups[2].Value, out var bound)) return null;
            var cmp = v.CompareTo(bound);
            ok &= m.Groups[1].Value switch { ">=" => cmp >= 0, ">" => cmp > 0, "<=" => cmp <= 0, _ => cmp < 0 };
        }
        return ok;
    }
}

sealed class Registry(IEnumerable<(string Id, JsonElement El)> components, Dictionary<string, Dictionary<string, JsonElement>> contracts, ISet<string> permissions)
{
    readonly List<(string Id, JsonElement El)> comps = components.ToList();

    public IEnumerable<string> Capabilities => comps.SelectMany(c => c.El.Arr("provides").Select(p => p.Str("capability")!))
        .Concat(comps.SelectMany(c => c.El.Arr("requires").Select(r => r.Str("capability")!))).Distinct().Order();

    /// <summary>Todos os providers declarados de uma capability, com a versão fornecida.</summary>
    public IEnumerable<(string Component, SemVer Version)> Providers(string capability) =>
        comps.SelectMany(c => c.El.Arr("provides").Where(p => p.Str("capability") == capability)
            .Where(p => SemVer.TryParse(p.Str("version"), out _)).Select(p => { SemVer.TryParse(p.Str("version"), out var v); return (c.Id, v); }));

    /// <summary>Descoberta: providers cuja versão satisfaz a faixa.</summary>
    public IEnumerable<(string Component, SemVer Version)> Discover(string capability, string range) =>
        Providers(capability).Where(p => VersionRange.Satisfies(p.Version, range) == true);

    public List<(string Code, string Message)> Validate()
    {
        var errors = new List<(string, string)>();
        var types = new Dictionary<string, string?>();
        foreach (var (cid, cel) in comps) types.TryAdd(cid, cel.Str("type")); // duplicatas são reportadas por CHK-IDS-UNIQUE
        foreach (var (id, el) in comps)
        {
            foreach (var p in el.Arr("provides"))
            {
                var cap = p.Str("capability")!; var ver = p.Str("version")!;
                if (!contracts.TryGetValue(cap, out var versions)) errors.Add(("CAP_UNKNOWN", $"{id} fornece '{cap}', que não tem contrato"));
                else if (!versions.ContainsKey(ver)) errors.Add(("CAP_VERSION_UNKNOWN", $"{id} fornece {cap}@{ver}, versão sem contrato"));
            }
            var requested = el.TryGetProperty("permissions", out var perms) ? perms.Arr("requests").Select(x => x.GetString()!).ToHashSet() : [];
            foreach (var rq in requested.Where(x => !permissions.Contains(x)))
                errors.Add(("PERMISSION_UNKNOWN", $"{id} solicita a permissão '{rq}', que não existe em docs/contracts/permissions.json"));
            foreach (var r in el.Arr("requires"))
            {
                var cap = r.Str("capability")!; var range = r.Str("range")!;
                var optional = r.TryGetProperty("optional", out var o) && o.ValueKind == JsonValueKind.True;
                if (!contracts.TryGetValue(cap, out var versions)) { errors.Add(("CAP_UNKNOWN", $"{id} exige '{cap}', que não tem contrato")); continue; }
                if (VersionRange.Satisfies(new SemVer(0, 0, 0), range) is null) { errors.Add(("RANGE_INVALID", $"{id} exige {cap} com faixa inválida '{range}'")); continue; }
                var providers = Providers(cap).ToList();
                if (providers.Count == 0) { if (!optional) errors.Add(("CAP_NO_PROVIDER", $"{id} exige {cap} {range}, mas nenhum componente a fornece")); continue; }
                var matching = Discover(cap, range).ToList();
                if (matching.Count == 0)
                {
                    if (!optional) errors.Add(("CAP_INCOMPATIBLE", $"{id} exige {cap} {range}; providers disponíveis: {string.Join(", ", providers.Select(p => $"{p.Component}@{p.Version}"))}"));
                    continue;
                }
                // deny-by-default (NN-016): o consumidor precisa ter solicitado as permissões que o contrato da versão fornecida exige.
                foreach (var (_, ver) in matching)
                    if (versions.TryGetValue(ver.ToString(), out var vc))
                        foreach (var need in vc.Arr("requiredPermissions").Select(x => x.GetString()!).Where(x => !requested.Contains(x)))
                            errors.Add(("PERMISSION_MISSING", $"{id} consome {cap}@{ver}, que exige a permissão '{need}', não solicitada"));
            }
            // boundaries também no Registry: Tool/Service/Library/Workspace/Adapter não conhecem Host concreto (NN-007); Product → Product direto é proibido (NN-002).
            foreach (var d in el.Arr("dependencies").Select(x => x.Str("component")).Where(x => x is not null && types.TryGetValue(x, out var t) && t == "product"))
                if (types[id] == "product") errors.Add(("PRODUCT_DEPENDS_ON_PRODUCT", $"{id} depende diretamente do Product {d}"));
                else if (types[id] is "tool" or "service" or "library" or "workspace" or "adapter")
                    errors.Add(("TOOL_KNOWS_HOST", $"{id} ({types[id]}) depende do Product/Host concreto {d}"));
        }
        return errors.Distinct().ToList();
    }
}

static class RegistryFiles
{
    public static Dictionary<string, Dictionary<string, JsonElement>> LoadContracts(string dir)
    {
        var result = new Dictionary<string, Dictionary<string, JsonElement>>();
        if (!Directory.Exists(dir)) return result;
        foreach (var f in Directory.EnumerateFiles(dir, "*.json").Order())
        {
            var doc = JsonDocument.Parse(File.ReadAllText(f)).RootElement.Clone();
            if (doc.Str("capability") is not { } cap) continue;
            result[cap] = doc.Arr("versions").Where(v => v.Str("version") is not null).ToDictionary(v => v.Str("version")!, v => v);
        }
        return result;
    }

    public static HashSet<string> LoadPermissions(string file) =>
        File.Exists(file) ? JsonDocument.Parse(File.ReadAllText(file)).RootElement.Arr("permissions").Select(p => p.Str("id")!).ToHashSet() : [];

    /// <summary>Regras de Context além do schema: começa em ecosystem, níveis em ordem estrita e o produto existe.</summary>
    public static List<string> ContextErrors(JsonElement ctx, ISet<string> products)
    {
        var errors = new List<string>(); var order = new[] { "ecosystem", "product", "project", "workspace", "tool" };
        var path = ctx.Arr("path").ToList();
        if (path.Count == 0) return ["path vazio"];
        if (path[0].Str("level") != "ecosystem") errors.Add("o Context precisa começar no nível 'ecosystem'");
        var last = -1;
        foreach (var seg in path)
        {
            var i = Array.IndexOf(order, seg.Str("level"));
            if (i <= last) errors.Add($"nível '{seg.Str("level")}' fora de ordem (ecosystem → product → project → workspace → tool, sem repetir)");
            last = Math.Max(last, i);
            if (seg.Str("level") == "product" && !products.Contains(seg.Str("id") ?? "")) errors.Add($"product '{seg.Str("id")}' não existe em ecosystem.json");
        }
        return errors;
    }

    /// <summary>Regras de Distribution Profile além do schema: componentes existem, sem duplicata, NN-023 (Hub nunca bundled com Product público) e,
    /// em perfis `current`, canais resolvíveis a partir do próprio componente (sem URL copiada), todo componente com canal e decisões existentes.</summary>
    public static List<string> ProfileErrors(JsonElement prof, IReadOnlyDictionary<string, JsonElement> components, ISet<string> decisionIds)
    {
        var errors = new List<string>(); var entries = prof.Arr("entries").ToList();
        foreach (var e in entries.Where(e => !components.ContainsKey(e.Str("component") ?? ""))) errors.Add($"componente '{e.Str("component")}' não existe em ecosystem.json");
        foreach (var g in entries.GroupBy(e => e.Str("component")).Where(g => g.Count() > 1)) errors.Add($"componente '{g.Key}' repetido");
        var publicProduct = entries.Any(e => e.Str("component") is { } cid && cid != "hub" && e.Str("visibility") == "public" && e.Str("availability") is "bundled" or "optional" or "marketplace");
        if (publicProduct && entries.Any(e => e.Str("component") == "hub" && e.Str("availability") == "bundled"))
            errors.Add("o Hub não pode ser 'bundled' em um perfil que inclui um componente público (NN-023)");
        foreach (var d in prof.Arr("decisions").Select(x => x.GetString() ?? "").Where(d => !decisionIds.Contains(d))) errors.Add($"a decisão {d} citada não existe em decisions.json");
        var status = prof.Str("status");
        if (status == "target")
        {
            if (!prof.Arr("decisions").Any()) errors.Add("perfil 'target' precisa citar a decisão do proprietário que o sustenta");
            foreach (var e in entries)
                foreach (var ch in e.Arr("channels"))
                {
                    var from = ch.Str("locationFrom");
                    if (ch.Str("kind") != "first-party-platform" && from is null) errors.Add($"canal '{ch.Str("id")}': canal existente sem 'locationFrom'");
                    if (from is not null && components.TryGetValue(e.Str("component") ?? "", out var tc)
                        && string.IsNullOrEmpty(from == "source.repository" ? (tc.TryGetProperty("source", out var ts) ? ts.Str("repository") : null) : tc.Str(from)))
                        errors.Add($"canal '{ch.Str("id")}': '{e.Str("component")}' não declara '{from}' em ecosystem.json");
                }
            return errors;
        }
        if (status != "current") return errors;
        foreach (var e in entries)
        {
            var cid = e.Str("component") ?? "";
            var channels = e.Arr("channels").ToList();
            if (channels.Count == 0) errors.Add($"perfil 'current': '{cid}' não descreve nenhum canal de distribuição");
            if (!components.TryGetValue(cid, out var comp)) continue;
            if (comp.Str("type") == "product" && !comp.TryGetProperty("path", out _)) errors.Add($"'{cid}' não declara o path de desenvolvimento (SOURCE)");
            foreach (var ch in channels)
            {
                var from = ch.Str("locationFrom") ?? "";
                if (from.Length == 0) { errors.Add($"canal '{ch.Str("id")}': perfil 'current' exige 'locationFrom'"); continue; }
                var resolved = from == "source.repository" ? (comp.TryGetProperty("source", out var s) ? s.Str("repository") : null) : comp.Str(from);
                if (string.IsNullOrEmpty(resolved)) errors.Add($"canal '{ch.Str("id")}': '{cid}' não declara '{from}' em ecosystem.json (a localização não pode ser inventada no perfil)");
                if (ch.Str("kind") is "first-party-platform" or "external-store") errors.Add($"canal '{ch.Str("id")}': o tipo '{ch.Str("kind")}' não existe hoje; um perfil 'current' só descreve canais reais");
            }
        }
        return errors;
    }
}

/// <summary>
/// Pré-integração (ADR-0014): o mundo pode ter mudado enquanto o agente trabalhava. Compara a branch com a base atual (padrão origin/main):
/// FRESH (código 0) = a base atual é ancestral da branch; STALE (3) = a base andou e a branch precisa ser reconciliada; STALE com
/// sobreposição (4) = os dois lados mudaram os mesmos arquivos e a reconciliação precisa ser semântica. Só usa dados estruturados do git.
/// </summary>
static class IntegrationCli
{
    public static readonly string[] HighRisk =
    [
        "ROADMAP.md", "ARCHITECTURE.md", "AGENTS.md", "MANIFEST.md", "ecosystem.json", "docs/governance/decisions.json",
        "docs/governance/enforcement-matrix.json", "docs/adr/README.md", "tests/consistency/Check.cs",
    ];
    static readonly string[] HighRiskPrefixes = ["docs/contracts/schemas/", "site/generator/", ".github/workflows/"];

    public static bool IsHighRisk(string path) => HighRisk.Contains(path) || HighRiskPrefixes.Any(path.StartsWith) || path.EndsWith("/AGENTS.md");

    static (int Code, string Out) Git(string dir, params string[] a)
    {
        var psi = new ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var x in a) psi.ArgumentList.Add(x);
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit();
        return (p.ExitCode, o.Trim());
    }

    public static int Run(string root, string[] args, TextWriter w)
    {
        string Opt(string name, string def) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
        var branch = Opt("--integration", ""); var baseRef = Opt("--base", "origin/main");
        if (branch.Length == 0 || branch.StartsWith("--")) { w.WriteLine("uso: --integration <branch> [--base <ref>]"); return 2; }
        foreach (var r in new[] { branch, baseRef })
            if (Git(root, "rev-parse", "--verify", "-q", r + "^{commit}").Code != 0) { w.WriteLine($"ERRO: referência inexistente: {r} (faça git fetch)"); return 2; }
        var mb = Git(root, "merge-base", baseRef, branch).Out;
        var baseTip = Git(root, "rev-parse", baseRef).Out;
        w.WriteLine($"branch: {branch} ({Git(root, "rev-parse", "--short", branch).Out}) · base: {baseRef} ({baseTip[..Math.Min(7, baseTip.Length)]}) · merge-base: {mb[..Math.Min(7, mb.Length)]}");
        List<string> Files(string from, string to) => Git(root, "diff", "--name-only", from, to).Out.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        var mine = Files(mb, branch);
        if (mb == baseTip)
        {
            w.WriteLine($"FRESH: {baseRef} é ancestral de {branch}; {mine.Count} arquivo(s) alterado(s) pela branch. Ainda assim: rerodar os checks no estado a integrar.");
            return 0;
        }
        var ahead = Git(root, "rev-list", "--count", $"{mb}..{baseRef}").Out;
        var theirs = Files(mb, baseRef);
        var overlap = mine.Intersect(theirs).Order().ToList();
        w.WriteLine($"STALE: {baseRef} avançou {ahead} commit(s) desde a base desta branch; {theirs.Count} arquivo(s) mudaram na base.");
        if (overlap.Count == 0)
        {
            w.WriteLine("Sem sobreposição de arquivos. Reconcilie (merge da base na branch), rerode os checks no estado combinado e só então integre.");
            return 3;
        }
        w.WriteLine($"SOBREPOSIÇÃO em {overlap.Count} arquivo(s) — reconciliação semântica obrigatória (nunca --ours/--theirs às cegas):");
        foreach (var f in overlap) w.WriteLine($"  {(IsHighRisk(f) ? "[ALTO RISCO] " : "")}{f}");
        return 4;
    }
}

static class RegistryCli
{
    public static int Run(string root, string[] args)
    {
        string Opt(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : ""; }
        var file = Opt("--file") is { Length: > 0 } f ? Path.GetFullPath(f) : Path.Combine(root, "ecosystem.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var comps = doc.RootElement.GetProperty("components").EnumerateObject().Select(p => (p.Name, p.Value)).ToList();
        var contractsDir = Opt("--file").Length > 0 ? Path.Combine(Path.GetDirectoryName(file)!, "capabilities") : Path.Combine(root, "docs", "contracts", "capabilities");
        var reg = new Registry(comps, RegistryFiles.LoadContracts(contractsDir), RegistryFiles.LoadPermissions(Path.Combine(root, "docs", "contracts", "permissions.json")));
        var disc = Array.IndexOf(args, "--discover");
        if (disc >= 0 && disc + 1 < args.Length)
        {
            var cap = args[disc + 1]; var range = disc + 2 < args.Length && !args[disc + 2].StartsWith("--") ? args[disc + 2] : ">=0.0.0";
            var found = reg.Discover(cap, range).ToList();
            Console.WriteLine($"{cap} {range}: " + (found.Count == 0 ? "nenhum provider compativel" : string.Join(", ", found.Select(p => $"{p.Component}@{p.Version}"))));
            return found.Count == 0 ? 1 : 0;
        }
        foreach (var cap in reg.Capabilities)
        {
            Console.WriteLine(cap);
            foreach (var p in reg.Providers(cap)) Console.WriteLine($"  provider: {p.Component}@{p.Version}");
            foreach (var (id, el) in comps) foreach (var r in el.Arr("requires").Where(r => r.Str("capability") == cap)) Console.WriteLine($"  consumer: {id} ({r.Str("range")})");
        }
        var errors = reg.Validate();
        foreach (var (code, msg) in errors) Console.WriteLine($"ERRO {code}: {msg}");
        if (!reg.Capabilities.Any()) Console.WriteLine("(nenhuma capability declarada)");
        return errors.Count == 0 ? 0 : 1;
    }
}

static class Json
{
    public static string? Str(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static IEnumerable<JsonElement> Arr(this JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray() : [];

    public static IEnumerable<string> DuplicateKeys(JsonElement e, string path)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>();
            foreach (var p in e.EnumerateObject())
            {
                if (!seen.Add(p.Name)) yield return $"{path}.{p.Name}";
                foreach (var x in DuplicateKeys(p.Value, $"{path}.{p.Name}")) yield return x;
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var item in e.EnumerateArray())
                foreach (var x in DuplicateKeys(item, $"{path}[{i++}]")) yield return x;
        }
    }
}

sealed class UnsupportedSchemaException(string msg) : Exception(msg);

/// <summary>Validador do subconjunto de JSON Schema 2020-12 usado pelos contratos da fundação.</summary>
sealed class SchemaValidator(JsonElement rootSchema)
{
    static readonly HashSet<string> Annotations = ["$schema", "$id", "title", "description", "$defs"];
    static readonly HashSet<string> Supported =
        ["type", "const", "enum", "pattern", "minLength", "minItems", "required", "properties", "additionalProperties", "propertyNames", "items", "$ref"];

    public void Validate(JsonElement instance, List<string> errors) => Validate(instance, rootSchema, "$", errors);

    /// <summary>Valida contra uma sub-definição do mesmo arquivo (ex.: "#/properties/components").</summary>
    public void ValidateAt(string reference, JsonElement instance, List<string> errors) => Validate(instance, Resolve(reference), "$", errors);

    void Validate(JsonElement inst, JsonElement schema, string path, List<string> errors)
    {
        foreach (var kw in schema.EnumerateObject())
            if (!Annotations.Contains(kw.Name) && !Supported.Contains(kw.Name))
                throw new UnsupportedSchemaException($"keyword não suportada pelo validador: '{kw.Name}'");

        if (schema.TryGetProperty("$ref", out var r))
        {
            var target = Resolve(r.GetString()!);
            Validate(inst, target, path, errors);
        }

        if (schema.TryGetProperty("type", out var type))
        {
            var types = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(t => t.GetString()!).ToArray() : [type.GetString()!];
            if (!types.Any(t => IsType(inst, t))) { errors.Add($"{path}: tipo esperado {string.Join("|", types)}"); return; }
        }
        if (schema.TryGetProperty("const", out var cst) && !JsonElement.DeepEquals(inst, cst))
            errors.Add($"{path}: valor deve ser {cst.GetRawText()}");
        if (schema.TryGetProperty("enum", out var en) && !en.EnumerateArray().Any(v => JsonElement.DeepEquals(inst, v)))
            errors.Add($"{path}: valor {inst.GetRawText()} fora de [{string.Join(", ", en.EnumerateArray().Select(v => v.GetRawText()))}]");

        if (inst.ValueKind == JsonValueKind.String)
        {
            var s = inst.GetString()!;
            if (schema.TryGetProperty("pattern", out var pat) && !Regex.IsMatch(s, pat.GetString()!))
                errors.Add($"{path}: \"{s}\" não casa com {pat.GetString()}");
            if (schema.TryGetProperty("minLength", out var ml) && s.Length < ml.GetInt32())
                errors.Add($"{path}: comprimento mínimo {ml.GetInt32()}");
        }

        if (inst.ValueKind == JsonValueKind.Array)
        {
            if (schema.TryGetProperty("minItems", out var mi) && inst.GetArrayLength() < mi.GetInt32())
                errors.Add($"{path}: mínimo de {mi.GetInt32()} item(ns)");
            if (schema.TryGetProperty("items", out var items))
            {
                var i = 0;
                foreach (var it in inst.EnumerateArray()) Validate(it, items, $"{path}[{i++}]", errors);
            }
        }

        if (inst.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var req))
                foreach (var q in req.EnumerateArray())
                    if (!inst.TryGetProperty(q.GetString()!, out _)) errors.Add($"{path}: campo obrigatório ausente '{q.GetString()}'");
            schema.TryGetProperty("properties", out var props);
            foreach (var p in inst.EnumerateObject())
            {
                if (schema.TryGetProperty("propertyNames", out var pn))
                    Validate(JsonDocument.Parse($"\"{JsonEncodedText.Encode(p.Name)}\"").RootElement, pn, $"{path}.{p.Name} (nome)", errors);
                if (props.ValueKind == JsonValueKind.Object && props.TryGetProperty(p.Name, out var ps))
                    Validate(p.Value, ps, $"{path}.{p.Name}", errors);
                else if (schema.TryGetProperty("additionalProperties", out var ap))
                {
                    if (ap.ValueKind == JsonValueKind.False) errors.Add($"{path}: campo não permitido '{p.Name}'");
                    else if (ap.ValueKind == JsonValueKind.Object) Validate(p.Value, ap, $"{path}.{p.Name}", errors);
                }
            }
        }
    }

    JsonElement Resolve(string reference)
    {
        if (!reference.StartsWith("#/")) throw new UnsupportedSchemaException($"$ref não suportado: {reference}");
        var e = rootSchema;
        foreach (var part in reference[2..].Split('/'))
            if (!e.TryGetProperty(part, out e)) throw new UnsupportedSchemaException($"$ref não resolvido: {reference}");
        return e;
    }

    static bool IsType(JsonElement e, string t) => t switch
    {
        "object" => e.ValueKind == JsonValueKind.Object,
        "array" => e.ValueKind == JsonValueKind.Array,
        "string" => e.ValueKind == JsonValueKind.String,
        "boolean" => e.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => e.ValueKind == JsonValueKind.Null,
        "number" => e.ValueKind == JsonValueKind.Number,
        "integer" => e.ValueKind == JsonValueKind.Number && e.TryGetInt64(out _),
        _ => throw new UnsupportedSchemaException($"tipo não suportado: {t}"),
    };
}

// ---------------------------------------------------------------------------------------------

/// <summary>
/// Prova que cada check detecta a violação que fiscaliza: copia o repositório para um diretório temporário,
/// injeta uma violação e exige que o check esperado falhe. Também exige que o repositório real passe.
/// </summary>
static class SelfTest
{
    record Case(string Name, string ExpectedCheck, Action<string> Mutate);

    static void Replace(string root, string rel, string from, string to)
    {
        var p = Path.Combine(root, rel);
        var t = File.ReadAllText(p);
        if (!t.Contains(from)) throw new InvalidOperationException($"self-test desatualizado: '{from}' não encontrado em {rel}");
        File.WriteAllText(p, t.Replace(from, to));
    }

    static string PendingDecision(bool withObject) => """
        "decisions": [ { "id": "DEC-9999", "status": "pending", "component": "ecosystem", "title": "t", "context": "c", "question": "q",
          "alternatives": [ { "option": "a", "consequences": "x" }, { "option": "b", "consequences": "y" } ],
          "consequences": "c", "manifestCompatibility": "m",
        """ + (withObject ? " \"related\": [\"MANIFEST.md\"]," : "") + """
          "raisedBy": "self-test", "raisedAt": "2026-01-01" },
        """;

    static void RunGenerator(string root)
    {
        var psi = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "run", "site/generator/GenerateStatus.cs" }) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi)!;
        proc.StandardOutput.ReadToEnd(); proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0) throw new InvalidOperationException("self-test: o gerador da projeção falhou na cópia do repositório");
    }

    /// <summary>Cria, na cópia, um handoff 'done' baseado em um real, com reuse_assessment (self-test de ADR-0011).</summary>
    static void AddReuseHandoff(string root, string suffix, string component, string timestamp, string reuseJson, string? baseCommit = "a4ad875", string? commit = null)
    {
        var dir = Path.Combine(root, "docs", "governance", "handoffs");
        var n = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "HO-20261001-p0-15-aprovado.json")))!.AsObject();
        n["message_id"] = "HO-99990101-" + suffix; n["task_id"] = "P9-" + suffix.Length; n["component"] = component; n["timestamp"] = timestamp;
        if (reuseJson.Length > 0) n["reuse_assessment"] = System.Text.Json.Nodes.JsonNode.Parse(reuseJson);
        if (baseCommit is not null) n["base_commit"] = baseCommit;
        n["commit"] = commit;
        File.WriteAllText(Path.Combine(dir, "HO-99990101-" + suffix + ".json"), n.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
    }

    /// <summary>Acrescenta um item citando a decisão na seção "Não decidido" de ARCHITECTURE.md (self-test).</summary>
    static void CiteInArchitecture(string root, string decisionId) =>
        Replace(root, "ARCHITECTURE.md", "### 8.2 Não decidido (nenhum agente deve tratar como decidido)\n\n", $"### 8.2 Não decidido (nenhum agente deve tratar como decidido)\n\n- tema de teste ({decisionId});\n");

    static string Reuse(string subject, string status, string extra = "") =>
        $"[{{\"subject\":\"{subject}\",\"status\":\"{status}\",\"rationale\":\"teste\"{extra}}}]";

    static readonly Case[] Cases =
    [
        new("arquivo de fundação removido", "CHK-FOUNDATION-FILES", r => File.Delete(Path.Combine(r, "ROADMAP.md"))),
        new("campo desconhecido em ecosystem.json", "CHK-SCHEMA",
            r => Replace(r, "ecosystem.json", "\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"extra\": true,")),
        new("ID de componente duplicado", "CHK-IDS-UNIQUE",
            r => Replace(r, "ecosystem.json", "\"components\": {", "\"components\": { \"urbe\": { },")),
        new("produto não migrado existindo também no monorepo", "CHK-SINGLE-AUTHORITY",
            r => Replace(r, "ecosystem.json", "\"path\": \"apps/urbe\",\n      \"status\": \"active\"", "\"path\": \"apps/urbe\",\n      \"status\": \"not-migrated\"")),
        new("segundo ecosystem.json", "CHK-SINGLE-AUTHORITY",
            r => { Directory.CreateDirectory(Path.Combine(r, "platform")); File.WriteAllText(Path.Combine(r, "platform", "ecosystem.json"), "{}"); }),
        new("Urbe → Lunet2D", "CHK-BOUNDARIES",
            r => Replace(r, "ecosystem.json", "\"https://github.com/AbnerCruz/Urbe\", \"confirmed\": true },\n      \"version\": { \"authority\": \"version-file\", \"file\": \"apps/urbe/package.json\" },\n      \"dependencies\": []",
                "\"https://github.com/AbnerCruz/Urbe\", \"confirmed\": true },\n      \"version\": { \"authority\": \"version-file\", \"file\": \"apps/urbe/package.json\" },\n      \"dependencies\": [{ \"component\": \"lunet2d\", \"kind\": \"optional\", \"reason\": \"x\" }]")),
        new("Urbe referencia o Lunet2D no código", "CHK-ARCH-REFS",
            r => File.AppendAllText(Path.Combine(r, "apps", "urbe", "package.json"), "\n// usa lunet2d\n")),
        new("Lunet2D referencia o Hub no código", "CHK-ARCH-REFS",
            r => File.AppendAllText(Path.Combine(r, "apps", "lunet2d", "VERSION"), "\nEcosystem Hub\n")),
        new("ProjectReference para fora do produto", "CHK-ARCH-REFS",
            r => File.WriteAllText(Path.Combine(r, "apps", "lunet2d", "src", "Fora.csproj"), "<Project><ItemGroup><ProjectReference Include=\"../../../../platform/X/X.csproj\" /></ItemGroup></Project>")),
        new("build VALIDATED sem evidência humana", "CHK-VALIDATION",
            r => Replace(r, "docs/validation/lunet2d/v0.0.1-dev.107.json", "\"kind\": \"human\"", "\"kind\": \"automated\"")),
        new("evidência do registro diverge do handoff", "CHK-VALIDATION",
            r => { var f = Path.Combine(r, "docs", "validation", "urbe", "1.8.2-beta-web.json"); File.WriteAllText(f, File.ReadAllText(f).Replace("\"result\": \"passed\"", "\"result\": \"failed\"")); }),
        new("registro de validação fora do caminho", "CHK-VALIDATION",
            r => { var d = Path.Combine(r, "docs", "validation", "urbe"); File.Move(Path.Combine(d, "1.8.2-beta-web.json"), Path.Combine(d, "outro.json")); }),
        new("Hub como dependência obrigatória", "CHK-BOUNDARIES",
            r => Replace(r, "ecosystem.json", "\"https://github.com/AbnerCruz/Lunet2D\", \"confirmed\": true },\n      \"version\": { \"authority\": \"version-file\", \"file\": \"apps/lunet2d/VERSION\" },\n      \"dependencies\": []",
                "\"https://github.com/AbnerCruz/Lunet2D\", \"confirmed\": true },\n      \"version\": { \"authority\": \"version-file\", \"file\": \"apps/lunet2d/VERSION\" },\n      \"dependencies\": [{ \"component\": \"hub\", \"kind\": \"required\", \"reason\": \"x\" }]")),
        new("diretório shared genérico", "CHK-GENERIC-DIRS",
            r => Directory.CreateDirectory(Path.Combine(r, "platform", "shared"))),
        new("componente compartilhado sem declaração", "CHK-SHARED-DECLARATION",
            r => Replace(r, "ecosystem.json", "\"components\": {",
                "\"components\": { \"sprite-studio\": { \"name\": \"Sprite Studio\", \"type\": \"tool\", \"language\": \"csharp\", \"path\": \"tools/sprite-studio\", \"status\": \"planned\", \"description\": \"x\", \"owners\": [\"x\"], \"dependencies\": [] },")),
        new("invariante ausente da matriz", "CHK-ENFORCEMENT-MATRIX",
            r => Replace(r, "docs/governance/enforcement-matrix.json", "\"id\": \"NN-015\"", "\"id\": \"NN-099\"")),
        new("invariante ausente do AGENTS.md", "CHK-AGENTS-NN",
            r => Replace(r, "AGENTS.md", "### NN-007 — ", "### Removida — ")),
        new("enforcement suprimido no AGENTS.md", "CHK-AGENTS-NN",
            r => Replace(r, "AGENTS.md", "**Enforcement obrigatório:** dependency tests;", "dependency tests;")),
        new("ADR sem seção obrigatória", "CHK-ADR",
            r => Replace(r, "docs/adr/0001-registro-de-decisoes-arquiteturais.md", "## Alternativas rejeitadas", "## Outras")),
        new("decisão tomada sem registro persistido", "CHK-DECISIONS",
            r => Replace(r, "docs/governance/decisions.json",
                "docs/governance/addenda/ADD-0003-aprovacao-de-decisoes-e-superficie-de-decisoes-no-portal.md", "docs/adr/9999-nao-existe.md")),
        new("handoff 'done' com validação humana pendente", "CHK-HANDOFFS",
            r => File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(r, "docs/governance/handoffs")).FullName, "HO-20260101-self-test.json"), """
                {
                  "$schema": "../../contracts/schemas/handoff.schema.json",
                  "schemaVersion": 1, "message_id": "HO-20260101-self-test", "category": "RESULT",
                  "timestamp": "2026-01-01T00:00:00Z", "agent": { "id": "self-test", "kind": "ai-agent" },
                  "task_id": "P0-1", "component": "ecosystem", "state": "done", "branch": "x", "commit": null, "pr": null,
                  "files_changed": [], "work_completed": ["x"],
                  "verification": [{ "check": "aparelho", "kind": "human", "result": "pending" }],
                  "known_issues": [], "blockers": [], "next_actions": [], "decisions_required": [],
                  "normative_sources": ["MANIFEST.md"], "invariants": []
                }
                """)),
        new("fase ausente do ROADMAP", "CHK-ROADMAP", r => Replace(r, "ROADMAP.md", "## Fase 7 — ", "## Fase sete — ")),
        new("dependência de um componente no portal", "CHK-BOUNDARIES",
            r => Replace(r, "ecosystem.json", "\"commands\": { \"test\": \"dotnet run tests/consistency/Check.cs\" },\n      \"dependencies\": []",
                "\"commands\": { \"test\": \"dotnet run tests/consistency/Check.cs\" },\n      \"dependencies\": [{ \"component\": \"portal\", \"kind\": \"optional\", \"reason\": \"x\" }]")),
        new("versão escrita à mão no portal", "CHK-PORTAL",
            r => File.AppendAllText(Path.Combine(r, "site", "app.js"), "\nconst lunetVersion = \"0.4.2\";\n")),
        new("projeção divergente de ecosystem.json", "CHK-PORTAL",
            r => File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(r, "site", "data")).FullName, "ecosystem-status.json"), """
                {
                  "schema": "ecosystem/contracts/ecosystem-status/1", "schemaVersion": 1, "kind": "projection", "authority": false,
                  "generatedAt": "2026-01-01T00:00:00Z", "generator": "self-test",
                  "source": { "repository": "https://github.com/AbnerCruz/Ecosystem", "ref": "HEAD", "commit": null, "files": ["ecosystem.json"] },
                  "ecosystem": { "name": "Ecosystem",
                    "checks": { "value": null, "availability": "not-available", "source": "x", "url": null } },
                  "components": [], "pendingDecisions": [], "pendingValidations": [], "docs": []
                }
                """)),
        new("decisão pendente sem objeto", "CHK-DECISIONS",
            r => Replace(r, "docs/governance/decisions.json", "\"decisions\": [", PendingDecision(withObject: false))),
        new("validação humana pendente sem objeto", "CHK-HANDOFFS",
            r => File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(r, "docs/governance/handoffs")).FullName, "HO-20260101-self-test-obj.json"), """
                {
                  "$schema": "../../contracts/schemas/handoff.schema.json",
                  "schemaVersion": 1, "message_id": "HO-20260101-self-test-obj", "category": "HANDOFF",
                  "timestamp": "2026-01-01T00:00:00Z", "agent": { "id": "self-test", "kind": "ai-agent" },
                  "task_id": "P0-1", "component": "ecosystem", "state": "review", "branch": "x", "commit": null, "pr": null,
                  "files_changed": [], "work_completed": ["x"],
                  "verification": [{ "check": "aparelho", "kind": "human", "result": "pending" }],
                  "known_issues": [], "blockers": [], "next_actions": [], "decisions_required": [],
                  "normative_sources": ["MANIFEST.md"], "invariants": []
                }
                """)),
        new("decisão pendente ausente do portal", "CHK-PORTAL",
            r =>
            {
                // Projeção real e coerente, gerada ANTES de a decisão pendente existir: só a decisão diverge.
                RunGenerator(r);
                Replace(r, "docs/governance/decisions.json", "\"decisions\": [", PendingDecision(withObject: true));
            }),
        // --- deriva semântica (ADR-0010): duas fontes estruturadas com estados incompatíveis ---
        new("external-consumer-exists sem consumidor concreto", "CHK-HANDOFFS",
            r => AddReuseHandoff(r, "a", "urbe", "2099-01-01T00:00:00Z", Reuse("account-login", "external-consumer-exists", ",\"extraction_review\":\"pending\""))),
        new("segundo consumidor sem Extraction Review", "CHK-HANDOFFS",
            r => { AddReuseHandoff(r, "b", "urbe", "2099-01-01T00:00:00Z", Reuse("account-login", "possible-candidate")); AddReuseHandoff(r, "bb", "lunet2d", "2099-01-02T00:00:00Z", Reuse("account-login", "possible-candidate")); }),
        new("possible-candidate com extraction_review", "CHK-HANDOFFS",
            r => AddReuseHandoff(r, "c", "urbe", "2099-01-01T00:00:00Z", Reuse("account-login", "possible-candidate", ",\"extraction_review\":\"pending\""))),
        new("vertical slice: positivo deixa de ser compatível", "CHK-REGISTRY",
            r => Replace(r, "docs/contracts/examples/registry-slice/positive.json", "\"range\": \"^1.0.0\"", "\"range\": \"^2.0.0\"")),
        new("vertical slice: negativo não falha como esperado", "CHK-REGISTRY",
            r => Replace(r, "docs/contracts/examples/registry-slice/negative-incompatible.json", "\"error\": \"CAP_INCOMPATIBLE\"", "\"error\": \"CAP_NO_PROVIDER\"")),
        new("componente real exige capability sem contrato", "CHK-REGISTRY",
            r => Replace(r, "ecosystem.json", "\"commands\": { \"test\": \"dotnet run tests/consistency/Check.cs\" },", "\"commands\": { \"test\": \"dotnet run tests/consistency/Check.cs\" },\n      \"requires\": [{ \"capability\": \"nao.existe\", \"range\": \"^1.0.0\" }],")),
        new("Context fora de ordem", "CHK-REGISTRY",
            r => Replace(r, "docs/contracts/examples/context/lunet-editor.json", "\"level\": \"tool\"", "\"level\": \"project\"")),
        new("Distribution Profile com o Hub bundled (NN-023)", "CHK-REGISTRY",
            r => Replace(r, "docs/contracts/examples/distribution/lunet-public.example.json", "\"availability\": \"optional\"", "\"availability\": \"bundled\"")),
        new("perfil de distribuição atual com canal que o componente não declara", "CHK-REGISTRY",
            r => Replace(r, "ecosystem.json", "\"publicUrl\": \"https://abnercruz.github.io/Urbe/\",", "")),
        new("perfil de distribuição atual descreve plataforma first-party inexistente", "CHK-REGISTRY",
            r => Replace(r, "docs/distribution/current.profile.json", "\"kind\": \"github-release\"", "\"kind\": \"first-party-platform\"")),
        new("perfil de distribuição atual cita decisão inexistente", "CHK-REGISTRY",
            r => Replace(r, "docs/distribution/current.profile.json", "\"DEC-0008\"", "\"DEC-8888\"")),
        new("perfil de distribuição alvo sem a decisão que o sustenta", "CHK-REGISTRY",
            r => Replace(r, "docs/distribution/target.profile.json", "\"decisions\": [\n    \"DEC-0021\"\n  ],", "\"decisions\": [],")),
        new("perfil de distribuição alvo com canal existente sem localização", "CHK-REGISTRY",
            r => Replace(r, "docs/distribution/target.profile.json", "\"locationFrom\": \"publicUrl\",", "")),
        new("perfil de distribuição atual removido", "CHK-REGISTRY",
            r => File.Delete(Path.Combine(r, "docs", "distribution", "current.profile.json"))),
        new("Caso A: ROADMAP [x] com Issue em state:review", "CHK-STATE-CONSISTENCY",
            r => { Directory.CreateDirectory(Path.Combine(r, "site", "data")); File.WriteAllText(Path.Combine(r, "site", "data", "issues-snapshot.json"), "[{\"number\":6,\"title\":\"P1-3 — Plano\",\"state\":\"OPEN\",\"labels\":[{\"name\":\"state:review\"}]}]"); }),
        new("tarefa [~] sem Issue aberta", "CHK-STATE-CONSISTENCY",
            r => { Directory.CreateDirectory(Path.Combine(r, "site", "data")); File.WriteAllText(Path.Combine(r, "site", "data", "issues-snapshot.json"), "[]"); Replace(r, "ROADMAP.md", "- [x] P1-12 —", "- [~] P1-12 —"); }),
        new("gate aprovado com item da fase aberto", "CHK-STATE-CONSISTENCY",
            r => Replace(r, "ROADMAP.md", "- [x] P0-1 —", "- [ ] P0-1 —")),
        new("fase copiada de volta em ecosystem.json", "CHK-SCHEMA",
            r => Replace(r, "ecosystem.json", "\"id\": \"ecosystem\",", "\"id\": \"ecosystem\",\n    \"phase\": \"phase-1\",")),
        new("ARCHITECTURE apresenta decisão já decidida como não decidida", "CHK-STATE-CONSISTENCY",
            r => { Replace(r, "docs/governance/decisions.json", "\"decisions\": [", PendingDecision(withObject: true).Replace("\"status\": \"pending\"", "\"status\": \"decided\"")); CiteInArchitecture(r, "DEC-9999"); }),
        new("matriz com mecanismo planned de fase já aprovada", "CHK-STATE-CONSISTENCY",
            r => Replace(r, "docs/governance/enforcement-matrix.json", "\"phase\": \"phase-3\"", "\"phase\": \"phase-1\"")),
        new("produto importado sem registro de importação", "CHK-MIGRATION-HISTORY",
            r => File.Delete(Path.Combine(r, "docs", "migration", "import-urbe.json"))),
        new("portal mostra gate não aprovado como aprovado", "CHK-PORTAL",
            r => { RunGenerator(r); var f = Path.Combine(r, "site", "data", "ecosystem-status.json"); File.WriteAllText(f, File.ReadAllText(f).Replace("\"phase\": 3,\n        \"state\": \"não iniciado\"", "\"phase\": 3,\n        \"state\": \"aprovado\"")); }),
        new("portal mostra canal de distribuição que o perfil não declara", "CHK-PORTAL",
            r => { RunGenerator(r); Replace(r, "site/data/ecosystem-status.json", "\"channel\": \"urbe-github-pages\"", "\"channel\": \"urbe-inventado\""); }),
        new("portal mostra capability que nenhum manifest declara", "CHK-PORTAL",
            r => { RunGenerator(r); Replace(r, "site/data/ecosystem-status.json", "\"capabilities\": []", "\"capabilities\": [ { \"id\": \"inventada.cap\", \"providers\": [\"urbe\"], \"consumers\": [] } ]"); }),
        new("Caso B: produto active no ecosystem.json, projeção diz not-migrated", "CHK-PORTAL",
            r => { RunGenerator(r); Replace(r, "site/data/ecosystem-status.json", "\"name\": \"Urbe\",\n      \"type\": \"product\",\n      \"status\": \"active\"", "\"name\": \"Urbe\",\n      \"type\": \"product\",\n      \"status\": \"not-migrated\""); }),
        new("Caso C: decisão decided, projeção ainda a mostra pendente", "CHK-PORTAL",
            r => { Replace(r, "docs/governance/decisions.json", "\"decisions\": [", PendingDecision(withObject: true)); RunGenerator(r); Replace(r, "docs/governance/decisions.json", "\"status\": \"pending\"", "\"status\": \"decided\""); }),
        new("Caso D: build VALIDATED no registro, projeção diz HUMAN_VALIDATION_PENDING", "CHK-PORTAL",
            r => { RunGenerator(r); var f = Path.Combine(r, "site", "data", "ecosystem-status.json"); File.WriteAllText(f, File.ReadAllText(f).Replace("\"state\": \"VALIDATED\"", "\"state\": \"HUMAN_VALIDATION_PENDING\"")); }),
        new("handoff afirma como resultado o próprio commit de partida", "CHK-HANDOFFS",
            r => AddReuseHandoff(r, "x", "urbe", "2099-01-01T00:00:00Z", "", "a4ad875", "a4ad875")),
        new("handoff novo sem base_commit", "CHK-HANDOFFS",
            r => AddReuseHandoff(r, "y", "urbe", "2099-01-01T00:00:00Z", "", null, null)),
        new("workflow de decisão sem a guarda do dono", "CHK-DECISION-FLOW",
            r => Replace(r, ".github/workflows/decision.yml", "github.event.issue.user.login == github.repository_owner && ", "")),
        new("texto da Issue interpolado em script", "CHK-DECISION-FLOW",
            r => Replace(r, ".github/workflows/decision.yml", "          dotnet run .github/scripts/apply-decision.cs\n",
                "          echo \"${{ github.event.issue.body }}\"\n          dotnet run .github/scripts/apply-decision.cs\n")),
        new("workflow de decisão termina verde mesmo sem registrar (sem o veredito)", "CHK-DECISION-FLOW",
            r => Replace(r, ".github/workflows/decision.yml", "          exit \"$verdict_code\"\n", "")),
        new("workflow de decisão grava sem exigir outcome=registered", "CHK-DECISION-FLOW",
            r => Replace(r, ".github/workflows/decision.yml", " && steps.apply.outputs.outcome == 'registered' && steps.verify.outputs.code == '0'", " && steps.verify.outputs.code == '0'")),
        new("segredo commitado", "CHK-SECRETS",
            r => File.WriteAllText(Path.Combine(r, "leak.txt"), "token=" + "gh" + "p_" + new string('a', 36))),
    ];

    public static int Run(string repoRoot)
    {
        var failures = 0;
        var baseline = Checks.RunAll(repoRoot);
        if (baseline.Failed)
        {
            Console.WriteLine("FAIL  baseline: o repositório real não passa nos checks");
            baseline.Print(Console.Out);
            failures++;
        }
        else Console.WriteLine("PASS  baseline: repositório real passa em todos os checks");

        var covered = new HashSet<string>();
        foreach (var c in Cases)
        {
            var tmp = Path.Combine(Path.GetTempPath(), "ecosystem-selftest-" + Guid.NewGuid().ToString("N"));
            try
            {
                Copy(repoRoot, tmp);
                c.Mutate(tmp);
                var rep = Checks.RunAll(tmp);
                var ok = rep.FailedCheck(c.ExpectedCheck) && !rep.Crashed;
                if (!ok) rep.Print(Console.Out);
                Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {c.ExpectedCheck} detecta: {c.Name}");
                if (ok) covered.Add(c.ExpectedCheck); else failures++;
            }
            catch (Exception e) { Console.WriteLine($"FAIL  {c.ExpectedCheck} ({c.Name}): {e.Message}"); failures++; }
            finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
        }

        failures += ApplierTests(repoRoot);
        failures += ValidationApplierTests(repoRoot);
        failures += DecisionVerdictTests(repoRoot);
        failures += OriginSyncTests(repoRoot);
        failures += RegistryUnitTests();
        failures += MigrationHistoryTests(repoRoot);
        failures += IntegrationTests();

        foreach (var id in Checks.Ids.Where(i => !covered.Contains(i)))
        {
            Console.WriteLine($"FAIL  {id} não possui caso de self-test");
            failures++;
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "Self-test: todos os checks provaram detectar suas violações." : $"Self-test: {failures} falha(s).");
        return failures == 0 ? 0 : 1;
    }

    static (int Code, string Result, string Outcome) RunApplier(string root, string author, string association, string title, string body)
    {
        var resultFile = Path.GetTempFileName();
        var outputFile = Path.GetTempFileName();
        var psi = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "run", ".github/scripts/apply-decision.cs" }) psi.ArgumentList.Add(a);
        psi.Environment["ISSUE_NUMBER"] = "1"; psi.Environment["ISSUE_TITLE"] = title; psi.Environment["ISSUE_BODY"] = body;
        psi.Environment["ISSUE_AUTHOR"] = author; psi.Environment["AUTHOR_ASSOCIATION"] = association; psi.Environment["REPO_OWNER"] = "AbnerCruz";
        psi.Environment["ISSUE_URL"] = "https://github.com/AbnerCruz/Ecosystem/issues/1"; psi.Environment["ISSUE_CREATED_AT"] = "2026-10-01T12:00:00Z";
        psi.Environment["RESULT_FILE"] = resultFile; psi.Environment["GITHUB_OUTPUT"] = outputFile;
        using var proc = Process.Start(psi)!;
        proc.StandardOutput.ReadToEnd(); proc.StandardError.ReadToEnd(); proc.WaitForExit();
        var result = File.Exists(resultFile) ? File.ReadAllText(resultFile) : "";
        var outcome = File.ReadAllLines(outputFile).Where(l => l.StartsWith("outcome=")).Select(l => l["outcome=".Length..]).LastOrDefault() ?? "";
        File.Delete(resultFile); File.Delete(outputFile);
        return (proc.ExitCode, result, outcome);
    }

    /// <summary>Prova o aplicador de decisões: o caminho feliz registra e deixa o repositório consistente; cada recusa de segurança recusa.</summary>
    static int ApplierTests(string repoRoot)
    {
        var failures = 0;
        void Report(bool ok, string name) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  aplicador de decisões: {name}"); if (!ok) failures++; }
        var tmp = Path.Combine(Path.GetTempPath(), "ecosystem-applier-" + Guid.NewGuid().ToString("N"));
        try
        {
            Copy(repoRoot, tmp);
            Replace(tmp, "docs/governance/decisions.json", "\"decisions\": [", PendingDecision(withObject: true));
            RunGenerator(tmp); // o gerador define título e corpo da Issue; o aplicador os valida (mesmo contrato)
            using var proj = JsonDocument.Parse(File.ReadAllText(Path.Combine(tmp, "site", "data", "ecosystem-status.json")));
            var dec = proj.RootElement.Arr("pendingDecisions").First(d => d.Str("id") == "DEC-9999");
            var alt = dec.Arr("alternatives").ElementAt(1);
            string title = alt.Str("issueTitle")!, body = alt.Str("issueBody")!;
            var decisionsFile = Path.Combine(tmp, "docs", "governance", "decisions.json");
            var before = File.ReadAllText(decisionsFile);
            string title0 = dec.Arr("alternatives").ElementAt(0).Str("issueTitle")!, body0 = dec.Arr("alternatives").ElementAt(0).Str("issueBody")!;
            bool Rejected((int Code, string Result, string Outcome) r) => r.Code == 3 && r.Outcome == "rejected" && r.Result.StartsWith("**Registro não efetuado.**");

            // Caso C — tentativa inválida: recusa (código ≠ 0) e não grava.
            Report(Rejected(RunApplier(tmp, "intruso", "NONE", title, body)), "recusa autor que não é o proprietário");
            Report(Rejected(RunApplier(tmp, "AbnerCruz", "OWNER", "Decisão DEC-9999", body)), "recusa título fora do formato");
            Report(Rejected(RunApplier(tmp, "AbnerCruz", "OWNER", title, body.Replace("option-hash: ", "option-hash: 0"))), "recusa hash adulterado");
            Report(Rejected(RunApplier(tmp, "AbnerCruz", "OWNER", "Decisão DEC-9999: Z", body)), "recusa título e corpo que discordam");
            Report(File.ReadAllText(decisionsFile) == before, "recusas não alteram decisions.json");

            // Caso D — estado inconsistente: o aplicador não grava parcialmente e sai com erro (≠ 0).
            var staleRecord = Path.Combine(tmp, "docs", "governance", "responses", "DEC-9999.md");
            File.WriteAllText(staleRecord, "resto de uma tentativa anterior\n");
            var stale = RunApplier(tmp, "AbnerCruz", "OWNER", title, body);
            Report(stale.Code == 2 && stale.Outcome == "error" && File.ReadAllText(decisionsFile) == before, "registro pré-existente com a decisão pendente: erro, nada gravado");
            File.Delete(staleRecord);
            File.WriteAllText(decisionsFile, "{ isto não é JSON");
            var broken = RunApplier(tmp, "AbnerCruz", "OWNER", title, body);
            Report(broken.Code == 2 && broken.Outcome == "error" && broken.Result.StartsWith("**Registro não efetuado.**") && !File.Exists(staleRecord), "decisions.json ilegível: erro estruturado, nenhum arquivo criado");
            File.WriteAllText(decisionsFile, before);

            // Caso A — decisão válida: pending → decided, código 0 e outcome=registered.
            var ok = RunApplier(tmp, "AbnerCruz", "OWNER", title, body);
            Report(ok.Code == 0 && ok.Outcome == "registered", "registra a decisão do proprietário (código 0, outcome=registered)");
            using var after = JsonDocument.Parse(File.ReadAllText(decisionsFile));
            var d9999 = after.RootElement.Arr("decisions").First(d => d.Str("id") == "DEC-9999");
            Report(d9999.Str("status") == "decided" && d9999.Str("decidedAt") == "2026-10-01" && d9999.Str("record") == "docs/governance/responses/DEC-9999.md"
                && File.Exists(staleRecord), "decisão fica decidida, datada e com registro persistido");
            RunGenerator(tmp);
            var consistent = Checks.RunAll(tmp);
            if (consistent.Failed) consistent.Print(Console.Out);
            Report(!consistent.Failed, "o repositório continua consistente depois do registro");

            // Caso E — decisão registrada com consequências ainda a aplicar é um estado LEGÍTIMO (sucesso do registrador), não "não registrada".
            Report(File.ReadAllText(decisionsFile).Contains("\"consequencesApplied\": false") && !consistent.Failed, "decidida + consequencesApplied=false é estado válido e consistente");

            // Caso B — idempotência: reprocessar o mesmo evento não duplica nada e é sucesso; outra alternativa é recusa.
            var registered = File.ReadAllText(decisionsFile); var recordText = File.ReadAllText(staleRecord);
            var again = RunApplier(tmp, "AbnerCruz", "OWNER", title, body);
            Report(again.Code == 0 && again.Outcome == "already-registered" && File.ReadAllText(decisionsFile) == registered && File.ReadAllText(staleRecord) == recordText,
                "reprocessar o mesmo evento é idempotente (código 0, already-registered, nada alterado)");
            Report(Rejected(RunApplier(tmp, "AbnerCruz", "OWNER", title0, body0)) && File.ReadAllText(decisionsFile) == registered, "outra alternativa para decisão já decidida: recusa e não sobrescreve");
            Report(RunApplier(tmp, "intruso", "NONE", title, body).Code == 3, "reprocessar com autor inválido continua recusando (a autorização vem antes da idempotência)");
            Report(File.ReadAllText(decisionsFile).Contains("\"consequencesApplied\": false"), "registro automático marca as consequências como a aplicar");
            // Decisão registrada pela automação e citada em ARCHITECTURE §8.2 como aberta NÃO pode bloquear o registro (caso real DEC-0019)...
            CiteInArchitecture(tmp, "DEC-9999");
            RunGenerator(tmp);
            Report(!Checks.RunAll(tmp).Failed, "decisão recém-registrada e ainda citada como aberta não bloqueia a automação");
            // ...mas, depois que o agente declara as consequências aplicadas, citá-la como aberta é deriva.
            Replace(tmp, "docs/governance/decisions.json", "\"consequencesApplied\": false", "\"consequencesApplied\": true");
            Report(Checks.RunAll(tmp).FailedCheck("CHK-STATE-CONSISTENCY"), "consequências declaradas aplicadas + citada como aberta é deriva");
        }
        catch (Exception e) { Report(false, "execução: " + e.Message); }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
        return failures;
    }

    /// <summary>Prova a resposta a validações humanas (ADR-0008): aprova/reprova gravando na verificação do handoff; cada recusa de segurança recusa.</summary>
    static int ValidationApplierTests(string repoRoot)
    {
        var failures = 0;
        void Report(bool ok, string name) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  aplicador de validações: {name}"); if (!ok) failures++; }
        var tmp = Path.Combine(Path.GetTempPath(), "ecosystem-applier-val-" + Guid.NewGuid().ToString("N"));
        try
        {
            Copy(repoRoot, tmp);
            var hdir = Path.Combine(tmp, "docs", "governance", "handoffs");
            var template = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(hdir, "HO-20261001-p0-15-aprovado.json")))!.AsObject();
            System.Text.Json.Nodes.JsonObject Make(string id, string task, string timestamp, string state)
            {
                var n = System.Text.Json.Nodes.JsonNode.Parse(template.ToJsonString())!.AsObject();
                n["message_id"] = id; n["task_id"] = task; n["timestamp"] = timestamp; n["state"] = state;
                var v = n["verification"]!.AsArray(); v.Clear();
                n["base_commit"] = "a4ad875";
                v.Add(new System.Text.Json.Nodes.JsonObject { ["check"] = "Validação de teste", ["kind"] = "human", ["result"] = "pending", ["object"] = "docs/governance/handoffs/" + id + ".json", ["evidence"] = "x" });
                return n;
            }
            void Save(System.Text.Json.Nodes.JsonObject n) =>
                File.WriteAllText(Path.Combine(hdir, n["message_id"]!.GetValue<string>() + ".json"), n.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
            Save(Make("HO-99990101-teste-validacao", "P9-9", "2099-01-01T00:00:00Z", "review"));
            Save(Make("HO-99990102-teste-validacao-2", "P9-8", "2099-01-02T00:00:00Z", "review"));
            RunGenerator(tmp);
            using var proj = JsonDocument.Parse(File.ReadAllText(Path.Combine(tmp, "site", "data", "ecosystem-status.json")));
            var val = proj.RootElement.Arr("pendingValidations").First(v => v.Str("taskId") == "P9-9");
            var approve = val.GetProperty("approve"); var reject = val.GetProperty("reject");
            string title = approve.Str("issueTitle")!, body = approve.Str("issueBody")!;
            var path = Path.Combine(hdir, "HO-99990101-teste-validacao.json");
            var before = File.ReadAllText(path);

            Report(RunApplier(tmp, "intruso", "NONE", title, body).Code == 3, "recusa autor que não é o proprietário");
            Report(RunApplier(tmp, "AbnerCruz", "OWNER", "Validação P9-9", body).Code == 3, "recusa título fora do formato");
            Report(RunApplier(tmp, "AbnerCruz", "OWNER", title, body.Replace("check-hash: ", "check-hash: 0")).Code == 3, "recusa hash adulterado");
            Report(RunApplier(tmp, "AbnerCruz", "OWNER", title, body.Replace("result: passed", "result: failed")).Code == 3, "recusa título e corpo que discordam");
            Report(RunApplier(tmp, "AbnerCruz", "OWNER", "Validação P9-8: aprovada", body.Replace("validation: P9-9", "validation: P9-8")).Code == 3, "recusa handoff que não é da tarefa informada");
            Report(File.ReadAllText(path) == before, "recusas não alteram o handoff");

            var ok = RunApplier(tmp, "AbnerCruz", "OWNER", title, body + "ficou ótimo `x`\n");
            Report(ok.Code == 0 && ok.Outcome == "registered", "registra a aprovação do proprietário (outcome=registered)");
            using var after = JsonDocument.Parse(File.ReadAllText(path));
            var entry = after.RootElement.Arr("verification").First();
            Report(entry.Str("result") == "passed" && (entry.Str("evidence") ?? "").Contains("Aprovada pelo proprietário")
                && after.RootElement.Str("state") == "review"
                && Directory.EnumerateFiles(Path.Combine(tmp, "docs", "governance", "responses"), "VAL-HO-99990101-teste-validacao-*.md").Any(),
                "grava o resultado na verificação, não muda o estado e persiste o registro");
            RunGenerator(tmp);
            var consistent = Checks.RunAll(tmp);
            if (consistent.Failed) consistent.Print(Console.Out);
            Report(!consistent.Failed, "o repositório continua consistente depois do registro");
            var handoffRegistered = File.ReadAllText(path);
            var replayVal = RunApplier(tmp, "AbnerCruz", "OWNER", title, body);
            Report(replayVal.Code == 0 && replayVal.Outcome == "already-registered" && File.ReadAllText(path) == handoffRegistered, "reprocessar a mesma validação é idempotente (nada alterado)");
            var opposite = RunApplier(tmp, "AbnerCruz", "OWNER", reject.Str("issueTitle")!, reject.Str("issueBody")!);
            Report(opposite.Code == 3 && opposite.Outcome == "rejected" && File.ReadAllText(path) == handoffRegistered, "resposta oposta a uma validação já respondida é recusada e não sobrescreve");

            // Reprovação e handoff substituído por um mais recente da mesma tarefa.
            using var proj2 = JsonDocument.Parse(File.ReadAllText(Path.Combine(tmp, "site", "data", "ecosystem-status.json")));
            var other = proj2.RootElement.Arr("pendingValidations").First(v => v.Str("taskId") == "P9-8");
            Save(Make("HO-99990103-teste-validacao-3", "P9-8", "2099-01-03T00:00:00Z", "review"));
            Report(RunApplier(tmp, "AbnerCruz", "OWNER", other.GetProperty("reject").Str("issueTitle")!, other.GetProperty("reject").Str("issueBody")!).Code == 3,
                "recusa handoff que não é mais o mais recente da tarefa");
        }
        catch (Exception e) { Report(false, "execução: " + e.Message); }
        finally { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); }
        return failures;
    }

    /// <summary>Prova o veredito do workflow de decisão: só "registrada agora" ou "já registrada" terminam verdes; todo o resto falha.</summary>
    static int DecisionVerdictTests(string repoRoot)
    {
        var failures = 0;
        void Report(bool ok, string name) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  veredito do workflow de decisão: {name}"); if (!ok) failures++; }
        int Verdict(string apply, string outcome, string verify, string pushed)
        {
            var (code, _) = Sh(repoRoot, "bash", [".github/scripts/decision-verdict.sh"],
                new() { ["APPLY_CODE"] = apply, ["OUTCOME"] = outcome, ["VERIFY_CODE"] = verify, ["PUSHED"] = pushed });
            return code;
        }
        Report(Verdict("0", "registered", "0", "true") == 0, "registrada, consistente e enviada: sucesso");
        Report(Verdict("0", "already-registered", "", "") == 0, "já registrada com a mesma escolha: sucesso idempotente");
        Report(Verdict("3", "rejected", "", "") != 0, "recusada pelo aplicador: falha");
        Report(Verdict("2", "error", "", "") != 0, "erro/estado inconsistente: falha");
        Report(Verdict("0", "registered", "1", "") != 0, "registrada no arquivo, mas a consistência recusou o repositório: falha");
        Report(Verdict("0", "registered", "0", "false") != 0, "registrada, mas o push falhou: falha");
        Report(Verdict("0", "registered", "", "") != 0, "registrada sem conferência nem push: falha");
        Report(Verdict("0", "", "", "") != 0, "aplicador sem outcome: falha");
        Report(Verdict("", "", "", "") != 0, "aplicador nem chegou a rodar (checkout/.NET falhou): falha");
        Report(Verdict("1", "registered", "0", "true") != 0, "código do aplicador diferente de 0 nunca é sucesso, mesmo com o resto verde");
        return failures;
    }

    static (int Code, string Output) Sh(string dir, string exe, IEnumerable<string> args, Dictionary<string, string>? env = null)
    {
        var psi = new ProcessStartInfo(exe) { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (env is not null) foreach (var (k, v) in env) psi.Environment[k] = v;
        using var proc = Process.Start(psi)!;
        var o = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd(); proc.WaitForExit();
        return (proc.ExitCode, o.Trim());
    }

    /// <summary>Prova o espelho de distribuição das origens (plano §7): âncora na primeira execução, idempotência, propagação de mudança e remoção, deriva.</summary>
    static int OriginSyncTests(string repoRoot)
    {
        var failures = 0;
        void Report(bool ok, string name) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  espelho das origens: {name}"); if (!ok) failures++; }
        var script = Path.Combine(repoRoot, ".github", "origin-sync", "sync-from-ecosystem.sh");
        if (!File.Exists(script)) { Report(false, "script ausente"); return 1; }
        var tmp = Path.Combine(Path.GetTempPath(), "ecosystem-sync-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tmp);
            var g = new Dictionary<string, string> { ["GIT_AUTHOR_NAME"] = "t", ["GIT_AUTHOR_EMAIL"] = "t@t", ["GIT_COMMITTER_NAME"] = "t", ["GIT_COMMITTER_EMAIL"] = "t@t" };
            (int Code, string Output) Git(string dir, params string[] a) => Sh(dir, "git", a, g);
            string eco = Path.Combine(tmp, "eco"), originBare = Path.Combine(tmp, "origin.git"), work = Path.Combine(tmp, "work"), bin = Path.Combine(tmp, "bin");
            Directory.CreateDirectory(Path.Combine(eco, "apps", "demo", "sub")); Directory.CreateDirectory(bin);
            File.WriteAllText(Path.Combine(eco, "apps", "demo", "a.txt"), "a\n"); File.WriteAllText(Path.Combine(eco, "apps", "demo", "sub", "b.txt"), "b\n");
            File.WriteAllText(Path.Combine(bin, "gh"), "#!/usr/bin/env bash\nexit 0\n"); Sh(tmp, "chmod", ["+x", Path.Combine(bin, "gh")]);
            Git(eco, "init", "-q", "-b", "main"); Git(eco, "add", "-A"); Git(eco, "commit", "-qm", "inicial");
            // origem: mesmo conteúdo + .github próprio
            Git(tmp, "clone", "-q", "--bare", eco, originBare);
            Git(tmp, "clone", "-q", originBare, work);
            // a origem tem a árvore na raiz (sem apps/demo): reconstruir
            foreach (var f in Directory.GetFileSystemEntries(work).Where(x => !x.EndsWith(".git"))) { if (Directory.Exists(f)) Directory.Delete(f, true); else File.Delete(f); }
            Directory.CreateDirectory(Path.Combine(work, "sub")); Directory.CreateDirectory(Path.Combine(work, ".github", "workflows"));
            File.WriteAllText(Path.Combine(work, "a.txt"), "a\n"); File.WriteAllText(Path.Combine(work, "sub", "b.txt"), "b\n");
            File.WriteAllText(Path.Combine(work, ".github", "workflows", "ci.yml"), "name: ci\n");
            Git(work, "add", "-A"); Git(work, "commit", "-qm", "origem"); Git(work, "push", "-q", "origin", "HEAD:main");
            var env = new Dictionary<string, string> { ["COMPONENT"] = "demo", ["ECOSYSTEM_REPO"] = eco, ["PATH"] = bin + ":" + Environment.GetEnvironmentVariable("PATH") };
            (int Code, string Output) Sync() => Sh(work, "bash", [script], env);

            var r1 = Sync();
            Report(r1.Code == 0 && Git(work, "log", "-1", "--format=%an").Output == "github-actions[bot]" && Git(work, "log", "-1", "--format=%B").Output.Contains("Ecosystem-Tree: "), "primeira execução registra a âncora (espelho idêntico)");
            var n1 = Git(work, "rev-list", "--count", "HEAD").Output;
            Sync();
            Report(Git(work, "rev-list", "--count", "HEAD").Output == n1, "segunda execução não muda nada (idempotente)");

            File.AppendAllText(Path.Combine(eco, "apps", "demo", "a.txt"), "mais\n"); File.Delete(Path.Combine(eco, "apps", "demo", "sub", "b.txt")); File.WriteAllText(Path.Combine(eco, "apps", "demo", "c.txt"), "c\n");
            Git(eco, "add", "-A"); Git(eco, "commit", "-qm", "mudança");
            var r2 = Sync();
            Report(r2.Code == 0 && File.ReadAllText(Path.Combine(work, "a.txt")) == "a\nmais\n" && !File.Exists(Path.Combine(work, "sub", "b.txt")) && File.Exists(Path.Combine(work, "c.txt")), "propaga mudança, remoção e arquivo novo");
            Report(File.ReadAllText(Path.Combine(work, ".github", "workflows", "ci.yml")) == "name: ci\n", ".github da origem fica intacto");
            Report(Git(originBare, "rev-parse", "main").Output == Git(work, "rev-parse", "HEAD").Output, "empurra para a origem");

            File.AppendAllText(Path.Combine(work, "a.txt"), "humano\n"); Git(work, "add", "-A"); Git(work, "commit", "-qm", "edição humana"); Git(work, "push", "-q", "origin", "HEAD:main");
            var before = Git(originBare, "rev-parse", "main").Output;
            Report(Sync().Code == 1 && Git(originBare, "rev-parse", "main").Output == before, "detecta deriva e não empurra nada");

            // primeira execução com espelho diferente da origem é recusada
            var work2 = Path.Combine(tmp, "work2"); Git(tmp, "clone", "-q", originBare, work2);
            Git(work2, "reset", "-q", "--hard", Git(work2, "rev-list", "--max-parents=0", "HEAD").Output); // histórico sem âncora
            File.WriteAllText(Path.Combine(work2, "a.txt"), "diferente\n"); Git(work2, "add", "-A"); Git(work2, "commit", "-qm", "divergente");
            Report(Sh(work2, "bash", [script], env).Code == 1, "primeira sincronização recusada se o espelho difere da origem");
        }
        catch (Exception e) { Report(false, "execução: " + e.Message); }
        finally { try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } }
        return failures;
    }

    /// <summary>Prova CHK-MIGRATION-HISTORY num repositório git sintético: ponta fora do histórico, contagem errada, tag ausente/errada e o caso íntegro.</summary>
    static int MigrationHistoryTests(string repoRoot)
    {
        var failures = 0;
        void Report(bool ok, string name) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  histórico migrado: {name}"); if (!ok) failures++; }
        var tmp = Path.Combine(Path.GetTempPath(), "ecosystem-history-" + Guid.NewGuid().ToString("N"));
        try
        {
            Copy(repoRoot, tmp);
            var g = new Dictionary<string, string> { ["GIT_AUTHOR_NAME"] = "t", ["GIT_AUTHOR_EMAIL"] = "t@t", ["GIT_COMMITTER_NAME"] = "t", ["GIT_COMMITTER_EMAIL"] = "t@t" };
            void G(params string[] a) => Sh(tmp, "git", a, g);
            G("init", "-q", "-b", "main"); G("add", "-A"); G("commit", "-qm", "base");
            var head = Sh(tmp, "git", ["rev-parse", "HEAD"], g).Output;
            bool Fails() => Checks.RunAll(tmp).FailedCheck("CHK-MIGRATION-HISTORY");
            Report(Fails(), "ponta importada que não está no histórico é detectada");

            foreach (var cid in new[] { "lunet2d", "urbe" })
            {
                var f = Path.Combine(tmp, "docs", "migration", $"import-{cid}.json");
                var text = File.ReadAllText(f);
                text = Regex.Replace(text, "\"imported_tip\": \"[0-9a-f]{40}\"", $"\"imported_tip\": \"{head}\"");
                text = Regex.Replace(text, "\"commits\": \\d+", "\"commits\": 1");
                File.WriteAllText(f, text);
                foreach (var l in File.ReadAllLines(Path.Combine(tmp, "docs", "migration", $"tags-{cid}.txt")).Select(x => x.Split(' ')).Where(p => p.Length == 2))
                    G("tag", l[0], head);
                File.WriteAllText(Path.Combine(tmp, "docs", "migration", $"tags-{cid}.txt"), string.Join("\n", File.ReadAllLines(Path.Combine(tmp, "docs", "migration", $"tags-{cid}.txt")).Select(x => x.Split(' ')[0] + " " + head)) + "\n");
            }
            Report(!Fails(), "histórico, contagem e tags íntegros passam");
            G("tag", "-d", "urbe/v1.8.2-beta");
            Report(Fails(), "tag ausente é detectada");
            G("tag", "urbe/v1.8.2-beta", Sh(tmp, "git", ["commit-tree", "HEAD^{tree}", "-m", "x"], g).Output);
            Report(Fails(), "tag apontando para outro commit é detectada");
        }
        catch (Exception e) { Report(false, "execução: " + e.Message); }
        finally { try { if (Directory.Exists(tmp)) { foreach (var f in Directory.EnumerateFiles(tmp, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(tmp, true); } } catch { } }
        return failures;
    }

    /// <summary>Pré-integração (ADR-0014): base atual ancestral = FRESH; base que andou = STALE; os dois lados no mesmo arquivo = sobreposição.</summary>
    static int IntegrationTests()
    {
        var failures = 0;
        void Report(bool ok, string name) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  integração multiagente: {name}"); if (!ok) failures++; }
        var tmp = Path.Combine(Path.GetTempPath(), "ecosystem-integration-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tmp);
            var g = new Dictionary<string, string> { ["GIT_AUTHOR_NAME"] = "t", ["GIT_AUTHOR_EMAIL"] = "t@t", ["GIT_COMMITTER_NAME"] = "t", ["GIT_COMMITTER_EMAIL"] = "t@t" };
            void G(params string[] a) => Sh(tmp, "git", a, g);
            void W(string f, string text) { Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(tmp, f))!); File.WriteAllText(Path.Combine(tmp, f), text); G("add", "-A"); G("commit", "-qm", f); }
            (int Code, string Out) Run(string branch) { var sw = new StringWriter(); var c = IntegrationCli.Run(tmp, ["--integration", branch, "--base", "main"], sw); return (c, sw.ToString()); }
            G("init", "-q", "-b", "main"); W("ROADMAP.md", "r\n"); W("apps/urbe/a.js", "a\n");
            G("checkout", "-qb", "agent-a"); W("docs/adr/0013.md", "a\n"); W("ROADMAP.md", "r\nfase 3\n");
            G("checkout", "-q", "main"); G("checkout", "-qb", "agent-b"); W("apps/urbe/a.js", "b\n");
            G("checkout", "-q", "main"); G("checkout", "-qb", "agent-c"); W("ROADMAP.md", "r\nfase 1\n");
            Report(Run("agent-a").Code == 0 && Run("agent-b").Code == 0, "duas branches do mesmo HEAD, main parada: FRESH");
            G("checkout", "-q", "main"); G("merge", "-q", "--no-ff", "agent-a", "-m", "integra A");
            var b = Run("agent-b");
            Report(b.Code == 3 && b.Out.Contains("STALE"), "main avançou (A integrada): B fica STALE sem sobreposição");
            var c = Run("agent-c");
            Report(c.Code == 4 && c.Out.Contains("[ALTO RISCO] ROADMAP.md"), "C mudou o mesmo ROADMAP.md que A: sobreposição de alto risco");
            G("checkout", "-q", "agent-b"); G("merge", "-q", "--no-edit", "main");
            Report(Run("agent-b").Code == 0 && File.ReadAllText(Path.Combine(tmp, "apps/urbe/a.js")) == "b\n" && File.Exists(Path.Combine(tmp, "docs/adr/0013.md")),
                "B reconciliada com a main: FRESH e os dois trabalhos preservados");
            Report(Run("nao-existe").Code == 2, "referência inexistente é erro, nunca FRESH");
            // Regressão do incidente: a cópia usada pelos self-tests nunca leva o `.git` (arquivo de worktree ou diretório).
            var wt = Path.Combine(tmp, "wt"); Directory.CreateDirectory(wt); File.WriteAllText(Path.Combine(wt, ".git"), "gitdir: /repositorio/real\n"); File.WriteAllText(Path.Combine(wt, "x.txt"), "x");
            var copy = Path.Combine(tmp, "copia"); Copy(wt, copy);
            Report(!File.Exists(Path.Combine(copy, ".git")) && File.Exists(Path.Combine(copy, "x.txt")), "cópia do self-test não leva o .git de um worktree (não escreve no repositório real)");
        }
        catch (Exception e) { Report(false, "execução: " + e.Message); }
        finally { try { if (Directory.Exists(tmp)) { foreach (var f in Directory.EnumerateFiles(tmp, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(tmp, true); } } catch { } }
        return failures;
    }

    /// <summary>Semântica de versões e faixas (ADR-0012): exata, caret, tilde e comparadores; faixas inválidas são recusadas.</summary>
    static int RegistryUnitTests()
    {
        var failures = 0;
        void Report(bool ok, string name) { Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  registry: {name}"); if (!ok) failures++; }
        bool? S(string v, string r) { SemVer.TryParse(v, out var sv); return VersionRange.Satisfies(sv, r); }
        Report(S("1.2.3", "1.2.3") == true && S("1.2.4", "1.2.3") == false, "versão exata");
        Report(S("1.9.0", "^1.2.0") == true && S("2.0.0", "^1.2.0") == false && S("1.1.9", "^1.2.0") == false, "caret: compatível dentro do mesmo MAJOR");
        Report(S("0.2.9", "^0.2.1") == true && S("0.3.0", "^0.2.1") == false, "caret em 0.x: MINOR é quebra");
        Report(S("1.2.9", "~1.2.0") == true && S("1.3.0", "~1.2.0") == false, "tilde: só PATCH");
        Report(S("1.5.0", ">=1.0.0 <2.0.0") == true && S("2.0.0", ">=1.0.0 <2.0.0") == false && S("0.9.0", ">=1.0.0 <2.0.0") == false, "comparadores combinados");
        Report(S("1.0.0", "latest") is null && S("1.0.0", "^1.x") is null && S("1.0.0", "") is null, "faixa inválida é recusada, não aceita");
        Report(!SemVer.TryParse("1.0", out _) && !SemVer.TryParse("01.0.0", out _) && SemVer.TryParse("10.20.30", out var v) && v.CompareTo(new SemVer(9, 99, 99)) > 0, "versão: formato estrito e ordenação numérica");
        var perms = new HashSet<string> { "fs.read" };
        using var doc = JsonDocument.Parse("""{"a":{"type":"tool","provides":[{"capability":"x.y","version":"1.0.0"},{"capability":"x.y","version":"1.4.0"}]},"b":{"type":"tool","provides":[{"capability":"x.y","version":"2.0.0"}]},"c":{"type":"workspace","requires":[{"capability":"x.y","range":"^1.0.0"}]}}""");
        using var contract = JsonDocument.Parse("""{"capability":"x.y","versions":[{"version":"1.0.0","requiredPermissions":[]},{"version":"1.4.0","requiredPermissions":[]},{"version":"2.0.0","requiredPermissions":[]}]}""");
        var contracts = new Dictionary<string, Dictionary<string, JsonElement>> { ["x.y"] = contract.RootElement.Arr("versions").ToDictionary(v2 => v2.Str("version")!, v2 => v2) };
        var reg = new Registry(doc.RootElement.EnumerateObject().Select(p => (p.Name, p.Value)), contracts, perms);
        var found = reg.Discover("x.y", "^1.0.0").Select(p => $"{p.Component}@{p.Version}").Order().ToList();
        Report(found.SequenceEqual(["a@1.0.0", "a@1.4.0"]) && reg.Validate().Count == 0, "descoberta devolve só providers compatíveis; consumidor satisfeito valida");
        return failures;
    }

    static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Repo.WalkFiles(from))
        {
            var rel = Path.GetRelativePath(from, f);
            if (rel.Replace('\\', '/') == "site/data/issues-snapshot.json") continue; // o instantâneo é de ambiente; as fixtures criam o seu
            // Num git worktree, `.git` é um ARQUIVO que aponta para o repositório real: copiá-lo faria o self-test (git init/commit/tag)
            // escrever no repositório real (incidente de 2026-10-01, ADR-0014). A cópia nunca leva metadados do git.
            if (rel == ".git" || rel.StartsWith(".git" + Path.DirectorySeparatorChar)) continue;
            var dest = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(f, dest);
        }
    }
}
