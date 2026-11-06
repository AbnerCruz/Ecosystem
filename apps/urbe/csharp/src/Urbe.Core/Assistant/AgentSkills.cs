using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Assistant;

/// <summary>A skill the agents can use: its catalog entry, where it comes from and what is wrong with it, if anything.</summary>
public sealed record AgentSkill(string Name, string Description, string Folder, string Origin, string Body, IReadOnlyList<string> Files,
    string? License = null, string? Version = null, IReadOnlyList<string>? Problems = null)
{
    public bool Valid => Problems is not { Count: > 0 };
}

/// <summary>
/// CST-177/214: skills in the Agent Skills format — a folder with a <c>SKILL.md</c> whose frontmatter has a
/// <c>name</c> and a <c>description</c>, then instructions, and optional files beside it. Only the catalog (name and
/// description) goes in every conversation; the agent loads a skill's instructions when the task calls for it, and
/// its files one at a time (progressive disclosure). Skills come from the app (built in) and from the person's
/// <c>Personalização/Skills/&lt;pasta&gt;/</c>. A skill never grants permissions (<c>allowed-tools</c> is reported,
/// not applied) and nothing in it is ever executed: scripts are text for the agent to read.
/// </summary>
public static partial class AgentSkills
{
    public const string Folder = "Personalização/Skills/";
    public const string LoadName = "load_skill";
    public const string ReadFileName = "read_skill_file";

    /// <summary>The skills of the vault (person's) and of the app, the person's first when a name repeats.</summary>
    public static IReadOnlyList<AgentSkill> Discover(IEnumerable<(string Path, string Content)> documents)
    {
        var docs = documents.Where(d => d.Path.Replace('\\', '/').StartsWith(Folder, StringComparison.OrdinalIgnoreCase)).ToList();
        var result = new List<AgentSkill>();
        foreach (var (path, content) in docs.Where(d => d.Path.EndsWith("/SKILL.md", StringComparison.OrdinalIgnoreCase)))
        {
            var folder = path[..^"SKILL.md".Length];
            var files = docs.Select(d => d.Path).Where(p => p.StartsWith(folder, StringComparison.OrdinalIgnoreCase) && !p.Equals(path, StringComparison.OrdinalIgnoreCase))
                .Select(p => p[folder.Length..]).Order(StringComparer.Ordinal).ToList();
            result.Add(Parse(content, folder.TrimEnd('/'), "vault", files));
        }
        foreach (var builtIn in BuiltIn)
            if (!result.Any(s => s.Name == builtIn.Name)) result.Add(builtIn);
        return result;
    }

    /// <summary>Reads a SKILL.md and checks it against the Agent Skills format.</summary>
    public static AgentSkill Parse(string markdown, string folder, string origin, IReadOnlyList<string>? files = null)
    {
        var problems = new List<string>();
        var text = markdown.Replace("\r\n", "\n");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var body = text;
        if (text.StartsWith("---\n", StringComparison.Ordinal) && text.IndexOf("\n---", 4, StringComparison.Ordinal) is var end and > 0)
        {
            string? key = null;
            foreach (var line in text[4..end].Split('\n'))
            {
                if (FieldLine().Match(line) is { Success: true } m) { key = m.Groups[1].Value; fields[key] = Unquote(m.Groups[2].Value.Trim()); }
                else if (key is not null && line.StartsWith(' ') && line.Trim().Length > 0) fields[key] = (fields[key] + " " + line.Trim()).Trim();
            }
            var after = text.IndexOf('\n', end + 1);
            body = after < 0 ? "" : text[(after + 1)..];
        }
        else problems.Add("Falta o cabeçalho --- com name e description.");
        var name = fields.GetValueOrDefault("name", "").Trim();
        var description = fields.GetValueOrDefault("description", "").Trim();
        var folderName = folder.Split('/').Last();
        if (name.Length == 0) { problems.Add("Falta name."); name = folderName; }
        else if (!NameRule().IsMatch(name)) problems.Add("name deve ter até 64 letras minúsculas, números e hífens (ex.: revisar-livro).");
        if (description.Length == 0) problems.Add("Falta description: diga o que a skill faz e quando usar.");
        else if (description.Length > 1024) problems.Add("description passa de 1024 caracteres.");
        if (fields.ContainsKey("allowed-tools")) problems.Add("allowed-tools é ignorado: uma skill não concede permissões no Urbe.");
        if (origin == "vault" && name != folderName && name.Length > 0) problems.Add("O nome da pasta (" + folderName + ") difere de name (" + name + ").");
        return new AgentSkill(name, description, folder, origin, body.Trim(), files ?? [], fields.GetValueOrDefault("license"),
            fields.GetValueOrDefault("version"), problems.Count > 0 ? problems : null);
    }

    private static string Unquote(string v) => v.Length >= 2 && (v[0] == '"' && v[^1] == '"' || v[0] == '\'' && v[^1] == '\'') ? v[1..^1] : v;

    /// <summary>The block of the instructions that lists the skills (name and description only).</summary>
    public static string Catalog(IReadOnlyList<AgentSkill> skills)
    {
        var usable = skills.Where(Usable).ToList();
        if (usable.Count == 0) return "";
        var sb = new StringBuilder("Skills disponíveis (instruções especializadas; carregue com " + LoadName + " quando a tarefa pedir, antes de começar — não carregue sem necessidade):\n");
        foreach (var s in usable) sb.Append("- ").Append(s.Name).Append(": ").Append(s.Description).Append('\n');
        return sb.ToString().TrimEnd();
    }

    /// <summary>A skill is offered when it has a valid name and a description (warnings do not hide it).</summary>
    public static bool Usable(AgentSkill s) => s.Description.Length > 0 && NameRule().IsMatch(s.Name);

    public static IReadOnlyList<AiTool> Tools(Func<IAssistantHost, IReadOnlyList<AgentSkill>> skills) =>
    [
        new AiTool
        {
            Name = LoadName, Access = "read",
            Description = "Carrega as instruções completas de uma skill do catálogo (Agent Skills). Use quando a tarefa corresponder à descrição da skill; " +
                          "siga as instruções dentro das suas permissões normais (uma skill não concede permissões nem executa scripts).",
            Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"name":{"type":"string","description":"O name da skill, como aparece no catálogo"}},"required":["name"],"additionalProperties":false}""")!,
            Label = i => "Carregando a skill " + ((string?)i["name"] ?? ""),
            Run = (i, host) => Task.FromResult(new AiToolOutput(Load(skills(host), (string?)i["name"] ?? "")))
        },
        new AiTool
        {
            Name = ReadFileName, Access = "read",
            Description = "Lê um arquivo de apoio de uma skill (listado ao carregá-la): referências, modelos, exemplos. Scripts são lidos como texto, nunca executados.",
            Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"name":{"type":"string"},"file":{"type":"string","description":"Caminho do arquivo dentro da pasta da skill"}},"required":["name","file"],"additionalProperties":false}""")!,
            Label = i => "Lendo " + ((string?)i["file"] ?? "") + " da skill " + ((string?)i["name"] ?? ""),
            Run = (i, host) => Task.FromResult(new AiToolOutput(ReadFile(skills(host), (string?)i["name"] ?? "", (string?)i["file"] ?? "", host)))
        },
    ];

    public static string Load(IReadOnlyList<AgentSkill> skills, string name)
    {
        var skill = skills.FirstOrDefault(s => s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (skill is null || !Usable(skill)) return "Não há skill \"" + name + "\". Catálogo: " + string.Join(", ", skills.Where(Usable).Select(s => s.Name)) + ".";
        var sb = new StringBuilder("# Skill: " + skill.Name + (skill.Origin == "app" ? " (do Urbe)" : " (" + skill.Folder + ")") + "\n\n" + skill.Body);
        if (skill.Files.Count > 0) sb.Append("\n\nArquivos de apoio (" + ReadFileName + "): ").Append(string.Join(", ", skill.Files));
        if (skill.Problems is { Count: > 0 } p) sb.Append("\n\nAvisos: ").Append(string.Join(" ", p));
        return sb.ToString();
    }

    public static string ReadFile(IReadOnlyList<AgentSkill> skills, string name, string file, IAssistantHost host)
    {
        var skill = skills.FirstOrDefault(s => s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (skill is null) return "Não há skill \"" + name + "\".";
        var clean = file.Replace('\\', '/').Trim().TrimStart('/');
        // only the skill's own files, never ../ out of its folder
        if (clean.Split('/').Any(part => part is ".." or ".") || !skill.Files.Contains(clean, StringComparer.Ordinal))
            return "A skill " + skill.Name + " não tem o arquivo \"" + file + "\". Arquivos: " + (skill.Files.Count == 0 ? "nenhum" : string.Join(", ", skill.Files)) + ".";
        var doc = host.Documents.Get(skill.Folder + "/" + clean);
        return doc is null ? "Arquivo indisponível." : AssistantTools.Clip(doc.Content, 24000);
    }

    [GeneratedRegex(@"^([A-Za-z][\w.-]*)\s*:\s*(.*)$")]
    private static partial Regex FieldLine();
    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$")]
    private static partial Regex NameRule();

    /// <summary>Skills that come with the Urbe, about what the app really does (CST-177): slides, books, calculations.</summary>
    public static IReadOnlyList<AgentSkill> BuiltIn { get; } =
    [
        Parse("""
            ---
            name: apresentacao-urbe
            description: Preparar notas para virar apresentação no Urbe (PPTX editável ou apresentação HTML) — divisão em slides, títulos, notas do apresentador e o que cabe num slide.
            ---
            # Apresentação a partir de notas

            No Urbe uma apresentação é uma Publicação com layout **Slides**, feita das notas reais (nunca de cópias).

            1. Cada nota começa um slide; dentro dela, cada título `#` ou `##` começa um slide e é o título dele; uma linha `---` (com linha em branco antes) começa um slide sem título.
            2. Um slide comporta cerca de 8 a 10 linhas de texto de 20 pt. Prefira tópicos curtos. O que não cabe continua num slide marcado "(cont.)" — melhor dividir com `##`.
            3. Notas do apresentador vão numa caixa `:::notas` … `:::` dentro do trecho do slide; nunca aparecem no slide.
            4. Uma imagem ou tabela por slide; com texto, a imagem fica à direita.
            5. Fórmulas saem como TeX no PowerPoint; para um número, use um bloco ```calculo (sai como tabela).
            6. Gráficos de função (```grafico) saem como imagem nítida.

            Edite as notas com as ferramentas de nota e crie/atualize a Publicação com as ferramentas de Publicações. Diga ao usuário que a exportação fica no menu ⋯ do estúdio (PowerPoint, HTML).
            """, "app/apresentacao-urbe", "app"),
        Parse("""
            ---
            name: livro-urbe
            description: Organizar um livro ou apostila no Urbe — capítulos como notas, publicação em layout Livro e exportação EPUB, Word, ODT ou PDF.
            ---
            # Livro a partir de notas

            1. Um capítulo é uma nota; o título da nota vira o título do capítulo (comece com `# Título`).
            2. Crie uma Publicação com layout **Livro** e adicione as notas na ordem dos capítulos; a ordem é da Publicação, não das pastas.
            3. Saídas: EPUB (livro digital), Word (DOCX) e OpenDocument (ODT) com sumário e cada capítulo numa página nova; PDF pela impressão do sistema.
            4. Caixas `:::tipo título` viram caixas destacadas; imagens precisam estar no vault (links relativos); fórmulas vão como TeX no Word/ODT.
            5. Antes de exportar, procure fontes ausentes e imagens fora do vault: elas impedem a exportação ou viram texto alternativo.
            6. Nunca copie o texto das notas para dentro da Publicação: ela só aponta para as notas.
            """, "app/livro-urbe", "app"),
        Parse("""
            ---
            name: calculo-urbe
            description: Fazer contas, conversões de unidade, estatística, matrizes, derivadas, equações e integrais com exatidão, e deixá-las vivas na nota num bloco ```calculo.
            ---
            # Cálculo exato

            - Use a ferramenta `calculate` para qualquer conta, em vez de calcular de cabeça. Resultados com ≈ são aproximados: diga isso.
            - Vírgula decimal (1,5); argumentos separados por `;` (media(3; 5; 8)).
            - Unidades: escreva o número com a unidade e converta com `em` (100 km/h em m/s). Dimensões diferentes dão erro de propósito.
            - Comandos: `derivada de …`, `simplificar …`, `expandir …`, `resolver … = … [para t]`, `integral de … [de a até b]`.
            - Para deixar a conta na nota, escreva as linhas num bloco ```calculo: o Urbe mostra o resultado ao lado de cada linha e recalcula sozinho; não escreva os resultados como texto fixo.
            """, "app/calculo-urbe", "app"),
    ];
}
