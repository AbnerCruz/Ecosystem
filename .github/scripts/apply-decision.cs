// Ecosystem — registro de decisões do proprietário a partir do portal (ADR-0007, DEC-0010).
//
// Chamado pelo workflow .github/workflows/decision.yml quando o proprietário envia a Issue pré-preenchida pelo
// portal. NUNCA executa texto da Issue: apenas o compara, de forma estrita, com o que está em decisions.json.
//
// Entradas (variáveis de ambiente; o workflow as passa por 'env', nunca por interpolação em script):
//   ISSUE_NUMBER, ISSUE_TITLE, ISSUE_BODY, ISSUE_AUTHOR, AUTHOR_ASSOCIATION, REPO_OWNER, ISSUE_URL, ISSUE_CREATED_AT
//   RESULT_FILE   (opcional) arquivo onde a mensagem de resultado é escrita (padrão: decision-result.md)
//   GITHUB_OUTPUT (opcional) recebe decision=<DEC-xxxx> e option=<letra> em caso de sucesso
// Saída: código 0 = decisão registrada; 3 = recusada (motivo no RESULT_FILE); 2 = erro de ambiente.
//
// Formato da Issue (gerado por site/generator/GenerateStatus.cs, uma única fonte):
//   título:  "Decisão DEC-0008: A"
//   corpo:   linhas "<!-- ecosystem-decision:v1 -->", "decision: DEC-0008", "option: A", "option-hash: <12 hex>"

#:property Nullable=enable

using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";

var resultFile = Env("RESULT_FILE") is { Length: > 0 } rf ? rf : "decision-result.md";

int Reject(string message)
{
    File.WriteAllText(resultFile, "**Decisão não registrada.** " + message + "\n");
    Console.Error.WriteLine("RECUSADA: " + message);
    return 3;
}

string? root = null;
for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
    if (File.Exists(Path.Combine(d.FullName, "MANIFEST.md")) && File.Exists(Path.Combine(d.FullName, "ecosystem.json"))) { root = d.FullName; break; }
if (root is null) { Console.Error.WriteLine("Raiz do repositório não encontrada."); return 2; }

var author = Env("ISSUE_AUTHOR");
var owner = Env("REPO_OWNER");
var title = Env("ISSUE_TITLE");
var body = Env("ISSUE_BODY").Replace("\r\n", "\n");
var number = Env("ISSUE_NUMBER");
var issueUrl = Env("ISSUE_URL");

// 1. Autorização: somente o dono do repositório (NN-016).
if (owner.Length == 0 || !string.Equals(author, owner, StringComparison.OrdinalIgnoreCase) || Env("AUTHOR_ASSOCIATION") != "OWNER")
    return Reject("O autor da Issue não é o proprietário do repositório.");

// 2. Formato estrito do título e do corpo.
var t = Regex.Match(title, @"^Decisão (DEC-\d{4}): ([A-Z])$");
if (!t.Success) return Reject("Título fora do formato 'Decisão DEC-NNNN: X'.");
var decisionId = t.Groups[1].Value; var letter = t.Groups[2].Value[0];

if (!body.Contains("<!-- ecosystem-decision:v1 -->")) return Reject("Corpo sem o marcador 'ecosystem-decision:v1'.");
string? Field(string name) => Regex.Match(body, $@"^{name}: (\S+)\s*$", RegexOptions.Multiline) is { Success: true } m ? m.Groups[1].Value : null;
var bodyDecision = Field("decision"); var bodyOption = Field("option"); var bodyHash = Field("option-hash");
if (bodyDecision != decisionId || bodyOption != letter.ToString())
    return Reject("O título e o corpo da Issue não concordam sobre a decisão e a alternativa.");
if (bodyHash is null || !Regex.IsMatch(bodyHash, "^[0-9a-f]{12}$")) return Reject("'option-hash' ausente ou inválido.");

// 3. A decisão precisa existir e estar pendente.
var decisionsPath = Path.Combine(root, "docs", "governance", "decisions.json");
var doc = JsonNode.Parse(File.ReadAllText(decisionsPath))!;
var decisions = doc["decisions"]!.AsArray();
var decision = decisions.FirstOrDefault(x => x?["id"]?.GetValue<string>() == decisionId)?.AsObject();
if (decision is null) return Reject($"{decisionId} não existe em decisions.json.");
if (decision["status"]?.GetValue<string>() != "pending") return Reject($"{decisionId} não está pendente (estado atual: {decision["status"]}).");

// 4. A alternativa precisa existir e ser exatamente a que o portal mostrou (impede aplicar uma escolha desatualizada).
var alternatives = decision["alternatives"]!.AsArray();
var index = letter - 'A';
if (index < 0 || index >= alternatives.Count) return Reject($"A alternativa {letter} não existe em {decisionId}.");
var option = alternatives[index]!["option"]!.GetValue<string>();
var consequences = alternatives[index]!["consequences"]!.GetValue<string>();
var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(option + "\n" + consequences))).ToLowerInvariant()[..12];
if (bodyHash != expectedHash)
    return Reject($"O texto da alternativa {letter} mudou desde que o portal a exibiu (hash esperado {expectedHash}). Recarregue o portal e escolha de novo.");

// 5. Registro persistido (NN-009): arquivo de resposta + atualização de decisions.json.
var created = Env("ISSUE_CREATED_AT");
var decidedAt = Regex.IsMatch(created, @"^\d{4}-\d{2}-\d{2}") ? created[..10] : DateTime.UtcNow.ToString("yyyy-MM-dd");
var recordRel = $"docs/governance/responses/{decisionId}.md";
var recordPath = Path.Combine(root, recordRel.Replace('/', Path.DirectorySeparatorChar));
Directory.CreateDirectory(Path.GetDirectoryName(recordPath)!);

var sb = new StringBuilder();
sb.AppendLine($"# Resposta à {decisionId} — alternativa {letter}");
sb.AppendLine();
sb.AppendLine($"> Registro gerado automaticamente por `.github/scripts/apply-decision.cs` (ADR-0007) a partir de uma Issue enviada pelo proprietário pelo portal.");
sb.AppendLine();
sb.AppendLine($"- **Decisão:** {decisionId} — {decision["title"]!.GetValue<string>()}");
sb.AppendLine($"- **Alternativa escolhida:** {letter} — {option}");
sb.AppendLine($"- **Autor:** {author} (proprietário do repositório)");
sb.AppendLine($"- **Enviada em:** {created}");
sb.AppendLine($"- **Issue:** {(issueUrl.Length > 0 ? issueUrl : "#" + number)}");
sb.AppendLine($"- **Hash da alternativa (conferido):** `{expectedHash}`");
sb.AppendLine();
sb.AppendLine("## Pergunta");
sb.AppendLine();
sb.AppendLine(decision["question"]!.GetValue<string>());
sb.AppendLine();
sb.AppendLine("## Alternativas no momento da escolha");
sb.AppendLine();
for (var i = 0; i < alternatives.Count; i++)
{
    sb.AppendLine($"{(char)('A' + i)}. **{alternatives[i]!["option"]!.GetValue<string>()}**{(i == index ? " ← escolhida" : "")}");
    sb.AppendLine($"   {alternatives[i]!["consequences"]!.GetValue<string>()}");
    sb.AppendLine();
}
sb.AppendLine("## Próximo passo");
sb.AppendLine();
sb.AppendLine("A decisão está registrada em `docs/governance/decisions.json`. As consequências nos documentos dependentes (ADR, arquitetura, roadmap, migração) **ainda não foram aplicadas por este registro**: um agente deve aplicá-las e registrar o handoff (AGENTS.md §3). Uma decisão estrutural continua exigindo ADR (NN-011).");
File.WriteAllText(recordPath, sb.ToString());

decision["status"] = "decided";
decision["decision"] = $"Alternativa {letter} — {option} (escolhida pelo proprietário pelo portal; Issue {(issueUrl.Length > 0 ? issueUrl : "#" + number)}).";
decision["decidedAt"] = decidedAt;
decision["record"] = recordRel;
File.WriteAllText(decisionsPath, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");

File.WriteAllText(resultFile,
    $"**Decisão registrada.** {decisionId}: alternativa **{letter}** — {option}\n\n" +
    $"Registro: `{recordRel}` e `docs/governance/decisions.json`. O portal é republicado em seguida. " +
    "As consequências nos documentos dependentes serão aplicadas por um agente.\n");
if (Env("GITHUB_OUTPUT") is { Length: > 0 } outFile)
    File.AppendAllText(outFile, $"decision={decisionId}\noption={letter}\n");
Console.WriteLine($"REGISTRADA: {decisionId} alternativa {letter}");
return 0;
