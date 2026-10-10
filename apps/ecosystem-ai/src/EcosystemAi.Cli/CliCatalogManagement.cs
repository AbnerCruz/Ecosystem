using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

/// <summary>
/// Gerenciamento local e explícito da estrutura de projetos/sessões.
/// Usa apenas LocalProjectStore e não inicia modelos, runners, timers ou rede.
/// </summary>
public static class CliCatalogManagement
{
    public static ProjectEntry CreateProject(string catalogDirectory, string workspaceDirectory, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var catalog = Path.GetFullPath(catalogDirectory);
        var workspace = Path.GetFullPath(workspaceDirectory);
        if (!Directory.Exists(workspace))
            throw new DirectoryNotFoundException("Workspace do novo projeto não existe.");
        // O catálogo guarda mensagens e custos potencialmente privados.
        // Não permitir que um agente com acesso ao workspace o leia por acidente.
        EnsureOutsideWorkspace(catalog, workspace);
        return new LocalProjectStore(catalog).CreateProject(name, workspace);
    }

    public static SessionEntry CreateSession(string catalogDirectory, string projectId, string title)
    {
        var store = ReadExisting(catalogDirectory);
        if (!store.Read().Projects.Any(p => p.Id == projectId))
            throw new KeyNotFoundException("Projeto não registrado no catálogo.");
        return store.CreateSession(projectId, title);
    }

    public static int Manage(string catalogDirectory, TextReader input, TextWriter output)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDirectory);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        // Modo interativo local. Só grava após uma escolha inequívoca; EOF sai.
        for (;;)
        {
            output.WriteLine("Ecosystem AI — Projetos e sessões (local, sem provedor)");
            output.WriteLine("1. Listar projetos e sessões");
            output.WriteLine("2. Vincular pasta existente como projeto");
            output.WriteLine("3. Criar sessão em projeto existente");
            output.WriteLine("4. Ler histórico de uma sessão");
            output.WriteLine("0. Sair");
            output.Write("Escolha: ");
            var choice = input.ReadLine();
            if (choice is null or "0") return 0;
            try
            {
                switch (choice)
                {
                    case "1":
                        foreach (var line in CliHistoryCommands.List(catalogDirectory))
                            output.WriteLine(line);
                        break;
                    case "2":
                        var workspace = Ask(input, output, "Pasta existente (caminho absoluto): ");
                        if (workspace is null) return 0;
                        var name = Ask(input, output, "Nome do projeto: ");
                        if (name is null) return 0;
                        var project = CreateProject(catalogDirectory, workspace, name);
                        output.WriteLine($"Projeto criado: {project.Id} — {project.Name}");
                        break;
                    case "3":
                        var projectId = Ask(input, output, "ID do projeto: ");
                        if (projectId is null) return 0;
                        var title = Ask(input, output, "Título da sessão: ");
                        if (title is null) return 0;
                        var session = CreateSession(catalogDirectory, projectId, title);
                        output.WriteLine($"Sessão criada: {session.Id} — {session.Title}");
                        break;
                    case "4":
                        var selectedProject = Ask(input, output, "ID do projeto: ");
                        if (selectedProject is null) return 0;
                        var selectedSession = Ask(input, output, "ID da sessão: ");
                        if (selectedSession is null) return 0;
                        foreach (var line in CliHistoryCommands.Show(catalogDirectory,
                            selectedProject, selectedSession))
                            output.WriteLine(line);
                        break;
                    default:
                        output.WriteLine("Opção inválida.");
                        break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException
                or InvalidDataException or InvalidOperationException or KeyNotFoundException
                or UnauthorizedAccessException)
            {
                output.WriteLine("Operação recusada: " + ex.Message);
            }
        }
    }

    private static string? Ask(TextReader input, TextWriter output, string prompt)
    {
        output.Write(prompt);
        return input.ReadLine();
    }

    private static LocalProjectStore ReadExisting(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!File.Exists(Path.Combine(Path.GetFullPath(directory), "catalog.json")))
            throw new FileNotFoundException("Catálogo inexistente; crie um projeto primeiro.");
        return new LocalProjectStore(directory);
    }

    private static void EnsureOutsideWorkspace(string catalog, string workspace)
    {
        // Recusa caminhos que atravessem links simbólicos inclusive no ancestral.
        for (DirectoryInfo? current = new(catalog); current is not null; current = current.Parent)
            if (current.LinkTarget is not null)
                throw new ArgumentException("O catálogo não pode usar ancestrais simbólicos.");
        for (DirectoryInfo? current = new(workspace); current is not null; current = current.Parent)
            if (current.LinkTarget is not null)
                throw new ArgumentException("O workspace não pode usar ancestrais simbólicos nesta operação.");
        var relation = Path.GetRelativePath(workspace, catalog);
        if (relation == "." || (!Path.IsPathRooted(relation) && relation != ".."
            && !relation.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relation.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)))
            throw new ArgumentException("O catálogo deve ficar fora do workspace acessível ao agente.");
    }
}
