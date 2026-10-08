using EcosystemAi.ProjectStore;

namespace EcosystemAi.Cli;

/// <summary>
/// Fronteira do Product entre uma execução CLI e o catálogo persistente.
// Não executa agentes, não armazena eventos do Core nem decide orçamento.
/// </summary>
public sealed class CliSessionPersistence
{
    private readonly LocalProjectStore _store;

    public string ProjectId { get; }
    public string SessionId { get; }

    private CliSessionPersistence(LocalProjectStore store, string projectId, string sessionId)
    {
        _store = store;
        ProjectId = projectId;
        SessionId = sessionId;
    }

    public static CliSessionPersistence Open(string catalogDirectory, string workspaceDirectory,
        string? projectId = null, string? sessionId = null,
        string? projectName = null, string? sessionTitle = null)
    {
        if (sessionId is not null && projectId is null)
            throw new ArgumentException("--session-id exige --project-id.");

        var workspace = Path.GetFullPath(workspaceDirectory);
        if (!Directory.Exists(workspace)) throw new DirectoryNotFoundException("Workspace não existe.");
        var store = new LocalProjectStore(catalogDirectory);
        ProjectEntry project;
        if (projectId is null)
        {
            var defaultName = Path.GetFileName(Path.TrimEndingDirectorySeparator(workspace));
            if (string.IsNullOrWhiteSpace(defaultName)) defaultName = "Projeto";
            project = store.CreateProject(projectName ?? defaultName, workspace);
        }
        else
        {
            project = store.Read().Projects.SingleOrDefault(x => x.Id == projectId)
                ?? throw new KeyNotFoundException("Projeto não existe no catálogo.");
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(project.WorkspaceDirectory, workspace, comparison))
                throw new InvalidOperationException("Projeto vinculado a outro workspace; operação recusada.");
        }

        SessionEntry session;
        if (sessionId is null)
            session = store.CreateSession(project.Id, sessionTitle ?? "Sessão CLI");
        else session = project.Sessions.SingleOrDefault(x => x.Id == sessionId)
            ?? throw new KeyNotFoundException("Sessão não existe no projeto.");

        return new CliSessionPersistence(store, project.Id, session.Id);
    }

    public void Begin(string userText) => _store.AppendTurn(ProjectId, SessionId, "user", userText);

    public void Complete(RunReceipt receipt, string? assistantText) =>
        _store.CompleteRun(ProjectId, SessionId, receipt, assistantText);

    public ProjectCatalog Read() => _store.Read();
}
