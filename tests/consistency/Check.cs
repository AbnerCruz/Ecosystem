// Ecosystem — checks de consistência da fundação (ADR-0003).
//
// Uso (a partir da raiz do repositório, .NET SDK 10+):
//   dotnet run tests/consistency/Check.cs               executa todos os checks
//   dotnet run tests/consistency/Check.cs -- --self-test prova que cada check falha quando violado
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
        "CHK-GENERIC-DIRS",
        "CHK-SHARED-DECLARATION",
        "CHK-ENFORCEMENT-MATRIX",
        "CHK-AGENTS-NN",
        "CHK-ADR",
        "CHK-DECISIONS",
        "CHK-HANDOFFS",
        "CHK-ROADMAP",
        "CHK-SECRETS",
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
        Guard(r, "CHK-GENERIC-DIRS", () => GenericDirs(ctx));
        Guard(r, "CHK-SHARED-DECLARATION", () => SharedDeclaration(ctx));
        Guard(r, "CHK-ENFORCEMENT-MATRIX", () => EnforcementMatrix(ctx));
        Guard(r, "CHK-AGENTS-NN", () => AgentsNn(ctx));
        Guard(r, "CHK-ADR", () => Adr(ctx));
        Guard(r, "CHK-DECISIONS", () => Decisions(ctx));
        Guard(r, "CHK-HANDOFFS", () => Handoffs(ctx));
        Guard(r, "CHK-ROADMAP", () => Roadmap(ctx));
        Guard(r, "CHK-SECRETS", () => Secrets(ctx));
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
                    c.R.Fail(id, $"'{cid}' declara o Hub como dependência obrigatória (NN-003)");

                if (fromType != "product" && toType == "product")
                    c.R.Fail(id, $"componente não-produto '{cid}' ({fromType}) depende do produto/Host concreto '{to}' (NN-007, MANIFEST §12)");
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
        foreach (var (file, doc) in c.Handoffs())
        {
            var h = doc.RootElement;
            if (h.ValueKind != JsonValueKind.Object) continue;
            if (h.Str("component") is { } comp && !comps.Contains(comp))
                c.R.Fail(id, $"{file}: componente inexistente '{comp}'");
            foreach (var d in h.Arr("decisions_required"))
                if (!decisions.Contains(d.GetString())) c.R.Fail(id, $"{file}: decisão inexistente {d.GetString()}");
            foreach (var s in h.Arr("normative_sources"))
                if (s.GetString() is { } p && !File.Exists(c.P(p.Split('#')[0])))
                    c.R.Fail(id, $"{file}: fonte normativa inexistente: {p}");

            var state = h.Str("state");
            var ver = h.Arr("verification").ToList();
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

    static readonly Case[] Cases =
    [
        new("arquivo de fundação removido", "CHK-FOUNDATION-FILES", r => File.Delete(Path.Combine(r, "ROADMAP.md"))),
        new("campo desconhecido em ecosystem.json", "CHK-SCHEMA",
            r => Replace(r, "ecosystem.json", "\"schemaVersion\": 1,", "\"schemaVersion\": 1, \"extra\": true,")),
        new("ID de componente duplicado", "CHK-IDS-UNIQUE",
            r => Replace(r, "ecosystem.json", "\"components\": {", "\"components\": { \"urbe\": { },")),
        new("produto não migrado existindo também no monorepo", "CHK-SINGLE-AUTHORITY",
            r => Directory.CreateDirectory(Path.Combine(r, "apps", "urbe"))),
        new("segundo ecosystem.json", "CHK-SINGLE-AUTHORITY",
            r => { Directory.CreateDirectory(Path.Combine(r, "platform")); File.WriteAllText(Path.Combine(r, "platform", "ecosystem.json"), "{}"); }),
        new("Urbe → Lunet2D", "CHK-BOUNDARIES",
            r => Replace(r, "ecosystem.json", "\"https://github.com/AbnerCruz/Urbe\", \"confirmed\": false },\n      \"version\": { \"authority\": \"source-repository\" },\n      \"dependencies\": []",
                "\"https://github.com/AbnerCruz/Urbe\", \"confirmed\": false },\n      \"version\": { \"authority\": \"source-repository\" },\n      \"dependencies\": [{ \"component\": \"lunet2d\", \"kind\": \"optional\", \"reason\": \"x\" }]")),
        new("Hub como dependência obrigatória", "CHK-BOUNDARIES",
            r => Replace(r, "ecosystem.json", "\"https://github.com/AbnerCruz/Lunet2D\", \"confirmed\": false },\n      \"version\": { \"authority\": \"source-repository\" },\n      \"dependencies\": []",
                "\"https://github.com/AbnerCruz/Lunet2D\", \"confirmed\": false },\n      \"version\": { \"authority\": \"source-repository\" },\n      \"dependencies\": [{ \"component\": \"hub\", \"kind\": \"required\", \"reason\": \"x\" }]")),
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
            r => Replace(r, "docs/governance/decisions.json", "\"status\": \"pending\"",
                "\"status\": \"decided\", \"decision\": \"x\", \"decidedAt\": \"2026-01-01\", \"record\": \"docs/adr/9999-nao-existe.md\"")),
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

        foreach (var id in Checks.Ids.Where(i => !covered.Contains(i)))
        {
            Console.WriteLine($"FAIL  {id} não possui caso de self-test");
            failures++;
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "Self-test: todos os checks provaram detectar suas violações." : $"Self-test: {failures} falha(s).");
        return failures == 0 ? 0 : 1;
    }

    static void Copy(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in Repo.WalkFiles(from))
        {
            var rel = Path.GetRelativePath(from, f);
            var dest = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(f, dest);
        }
    }
}
