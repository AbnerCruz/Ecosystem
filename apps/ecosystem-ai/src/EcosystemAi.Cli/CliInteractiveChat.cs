using System.Globalization;
using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

/// <summary>
/// Interface conversacional local, acionada explicitamente. Cada mensagem usa
/// o MESMO caminho de execução EcosystemAiCli -> AgentWorkspace.WorkspaceSession.
/// Não há runner, provider, ledger, memória ou agenda paralela.
/// </summary>
public static class CliInteractiveChat
{
    private static readonly string[] Required = [
        "--catalog", "--project", "--project-id", "--session-id",
        "--endpoint", "--model", "--budget-cents", "--max-call-cents",
        "--input-usd-per-million", "--output-usd-per-million"
    ];

    private static readonly HashSet<string> Allowed = new(Required, StringComparer.Ordinal)
    {
        "--journal", "--accept-exists"
    };

    public static async Task<int> RunAsync(
        IReadOnlyDictionary<string, string> fields, bool allowCreate,
        TextReader input, TextWriter output, Func<string[], Task<int>> execute)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(execute);
        if (fields.Keys.Except(Allowed, StringComparer.Ordinal).Any())
            throw new ArgumentException("Chat aceita apenas campos da execução e uma sessão existente.");
        foreach (var field in Required)
            if (!fields.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Chat exige " + field + ".");

        var catalog = Path.GetFullPath(fields["--catalog"]);
        var workspace = Path.GetFullPath(fields["--project"]);
        if (!Directory.Exists(workspace))
            throw new DirectoryNotFoundException("Workspace não encontrado.");
        CliRunJournalCommands.RequireOutsideWorkspace(catalog, workspace);
        if (fields.TryGetValue("--journal", out var journal))
            CliRunJournalCommands.RequireOutsideWorkspace(journal, workspace);
        if (!File.Exists(Path.Combine(catalog, "catalog.json")))
            throw new FileNotFoundException("Catálogo não encontrado: crie o projeto e a sessão antes de conversar.");

        // Falhar antes de ler a primeira pergunta em caso de configuração inválida.
        if (!Uri.TryCreate(fields["--endpoint"], UriKind.Absolute, out var endpoint)
            || endpoint.Scheme is not ("https" or "http")
            || (endpoint.Scheme == "http" && !endpoint.IsLoopback))
            throw new ArgumentException("Endpoint deve ser HTTPS ou HTTP somente em loopback.");
        if (!long.TryParse(fields["--budget-cents"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var budget)
            || !long.TryParse(fields["--max-call-cents"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var perCall)
            || budget < 1 || perCall < 1 || perCall > budget)
            throw new ArgumentException("Orçamento por tarefa inválido.");
        if (!decimal.TryParse(fields["--input-usd-per-million"], NumberStyles.Number,
                CultureInfo.InvariantCulture, out var inputPrice) || inputPrice < 0
            || !decimal.TryParse(fields["--output-usd-per-million"], NumberStyles.Number,
                CultureInfo.InvariantCulture, out var outputPrice) || outputPrice < 0)
            throw new ArgumentException("Preços inválidos.");

        var store = new LocalProjectStore(catalog);
        var snapshot = store.Read();
        var projectId = fields["--project-id"];
        var sessionId = fields["--session-id"];
        var project = snapshot.Projects.FirstOrDefault(p => p.Id == projectId)
            ?? throw new KeyNotFoundException("Projeto não encontrado no catálogo.");
        if (!string.Equals(Path.GetFullPath(project.WorkspaceDirectory), workspace,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new ArgumentException("Workspace atual não corresponde à pasta vinculada ao projeto.");
        var session = project.Sessions.FirstOrDefault(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Sessão não pertence ao projeto.");

        output.WriteLine($"Ecosystem AI — {project.Name} / {session.Title}");
        output.WriteLine("Mensagens são executadas individualmente, com orçamento por tarefa.");
        output.WriteLine("Histórico anterior é reenviado com limites; /sair ou EOF encerra. Sem background.");
        if (allowCreate) output.WriteLine("Escrita opt-in: somente criação de arquivos novos.");
        var hadFailure = false;
        while (true)
        {
            output.Write("> ");
            var text = input.ReadLine();
            if (text is null || text.Equals("/sair", StringComparison.OrdinalIgnoreCase))
                return hadFailure ? 1 : 0;
            if (text.Equals("/ajuda", StringComparison.OrdinalIgnoreCase))
            {
                output.WriteLine("/sair encerra, /ajuda mostra opções. Cada envio é uma execução separada.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(text)) continue;
            if (text.Length > 16 * 1024)
            {
                output.WriteLine("Mensagem excede 16 KiB; não enviada.");
                continue;
            }

            // Ler o catálogo canônico antes de cada turno: a primeira pergunta
            // em sessão vazia não deve requisitar histórico que não existe.
            var current = store.Read().Projects
                .First(p => p.Id == projectId).Sessions.First(s => s.Id == sessionId);
            var args = new List<string>();
            foreach (var (key, value) in fields)
            {
                args.Add(key);
                args.Add(value);
            }
            if (allowCreate) args.Add("--allow-create");
            if (current.Turns.Count > 0) args.Add("--use-history");
            args.Add("--goal");
            args.Add(text);

            var result = await execute(args.ToArray());
            if (result == 130) return 130; // Cancelamento nunca vira repetição automática.
            if (result == 2) return 2;     // Parâmetros/credenciais inválidos: não insistir.
            if (result != 0)
            {
                hadFailure = true;
                output.WriteLine("Esta tarefa não foi concluída; o chat não fará retry automático.");
            }
        }
    }
}
