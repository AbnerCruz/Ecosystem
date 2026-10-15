using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AgentRuntime;
using EcosystemAi.ProjectStore;

namespace EcosystemAi.Product;

/// <summary>Proposta local imutável. Só o Host que a preparou pode publicá-la.</summary>
public sealed class ReviewedProjectFile
{
    internal object Host { get; }
    internal string Workspace { get; }
    internal ChangeSet Change { get; }
    public string ProjectId { get; }
    public string SessionId { get; }
    public int TurnIndex { get; }
    public string FileName { get; }
    public string Content { get; }
    public string Sha256 { get; }

    internal ReviewedProjectFile(object host, string workspace, string projectId, string sessionId,
        int turnIndex, string fileName, string content, ChangeSet change)
    {
        Host = host; Workspace = workspace; ProjectId = projectId; SessionId = sessionId;
        TurnIndex = turnIndex; FileName = fileName; Content = content; Change = change;
        Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
    }
}

/// <summary>Recibo de publicação em disco, emitido somente depois do rename sem sobrescrita.</summary>
public sealed record ProjectFilePublication(string FileName, string Sha256, IntegrationReceipt Integration);

/// <summary>
/// Salvar uma resposta por gesto do operador. Não é tool do agente nem integração de tarefas.
/// Um único arquivo novo, no workspace privado do Host; sem overwrite, diretórios ou replay automático.
/// </summary>
public sealed class ReviewedProjectFileHost(string catalogDirectory, string journalDirectory)
{
    private const string Producer = "host-response-export";
    private const string Owner = "local-operator";
    private readonly object _identity = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _published = new(StringComparer.Ordinal);
    private readonly LocalProjectStore _store = new(catalogDirectory);

    public ReviewedProjectFile Prepare(string projectId, string sessionId, int turnIndex, string fileName)
    {
        ValidateName(fileName);
        var project = FindProject(projectId);
        ValidateRoot(project.WorkspaceDirectory);
        var turn = FindTurn(project, sessionId, turnIndex);
        if (turn.Role != "assistant" || string.IsNullOrWhiteSpace(turn.Text)
            || turn.Text.Length > ProductExecutionHost.MaxText || turn.Text.Contains('\0'))
            throw new ArgumentException("Escolha uma resposta textual da conversa.");
        RequireAbsent(project.WorkspaceDirectory, fileName);
        var operation = "save-response-" + Guid.NewGuid().ToString("N");
        var workspace = new IsolatedWorkspace(new WorkspaceSnapshot(0, []));
        workspace.Write(fileName, turn.Text);
        // Estes IDs identificam a operação de exportação do Host, não um run de inferência.
        var change = workspace.Seal(operation, operation, Producer, operation);
        return new ReviewedProjectFile(_identity, project.WorkspaceDirectory, projectId, sessionId,
            turnIndex, fileName, turn.Text, change);
    }

    /// <summary>O booleano vem exclusivamente da confirmação da UI sobre a proposta exibida.</summary>
    public async Task<ProjectFilePublication> PublishAsync(ReviewedProjectFile proposal, bool approved,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        if (!ReferenceEquals(proposal.Host, _identity)) throw new ArgumentException("Proposta pertence a outro Host.");
        if (!approved) throw new InvalidOperationException("Publicação não aprovada; nenhum arquivo foi criado.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_published.Contains(proposal.Change.Id)) throw new InvalidOperationException("Proposta já publicada.");
            ValidateSource(proposal);
            RequireAbsent(proposal.Workspace, proposal.FileName);
            var integrator = new ChangeIntegrator(new WorkspaceSnapshot(0, []),
                new OperatorReview(proposal.Change), new ExactTextVerifier(proposal), new SavePolicy(),
                Owner, new OperatorAuthorization(proposal.Change));
            var integration = await integrator.IntegrateAsync(proposal.Change, cancellationToken);
            if (integration.Status != IntegrationStatus.Integrated)
                throw new InvalidOperationException("Revisão/verificação da proposta recusada.");

            var temporary = Path.Combine(proposal.Workspace, ".save-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false, true).GetBytes(proposal.Content);
                    await stream.WriteAsync(bytes, cancellationToken);
                    stream.Flush(flushToDisk: true);
                }
                // Revalidar depois de qualquer await; o destino pode ter sido criado enquanto isso.
                ValidateSource(proposal);
                RequireAbsent(proposal.Workspace, proposal.FileName);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, Path.Combine(proposal.Workspace, proposal.FileName), overwrite: false);
                _published.Add(proposal.Change.Id);
                // Não observar cancelamento depois do commit: o arquivo já está publicado.
                return new ProjectFilePublication(proposal.FileName, proposal.Sha256, integration);
            }
            finally
            {
                // Não seguir uma raiz trocada por link para remover temporários fora do projeto.
                ValidateRoot(proposal.Workspace);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        finally { _gate.Release(); }
    }

    private ProjectEntry FindProject(string id) => _store.Read().Projects.SingleOrDefault(p => p.Id == id)
        ?? throw new KeyNotFoundException("Projeto inexistente.");

    private static ConversationTurn FindTurn(ProjectEntry project, string sessionId, int turnIndex)
    {
        var session = project.Sessions.SingleOrDefault(s => s.Id == sessionId)
            ?? throw new KeyNotFoundException("Conversa não pertence ao projeto.");
        if (turnIndex < 0 || turnIndex >= session.Turns.Count) throw new ArgumentException("Resposta inexistente.");
        return session.Turns[turnIndex];
    }

    private void ValidateSource(ReviewedProjectFile proposal)
    {
        var project = FindProject(proposal.ProjectId);
        if (project.WorkspaceDirectory != proposal.Workspace
            || FindTurn(project, proposal.SessionId, proposal.TurnIndex) is not { Role: "assistant" } turn
            || turn.Text != proposal.Content)
            throw new InvalidOperationException("A origem mudou; prepare e revise novamente.");
        ValidateRoot(project.WorkspaceDirectory);
    }

    private void ValidateRoot(string root)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Workspace inexistente.");
        ProductExecutionHost.RequireOutside(catalogDirectory, root);
        ProductExecutionHost.RequireOutside(journalDirectory, root);
        ProductExecutionHost.RequireOutside(root, catalogDirectory);
        ProductExecutionHost.RequireOutside(root, journalDirectory);
    }

    private static void RequireAbsent(string root, string name)
    {
        // Case-insensitive para que uma exportação não crie colisão ao migrar de plataforma.
        if (Directory.EnumerateFileSystemEntries(root).Any(p =>
            string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Nome já existe no projeto; escolha outro. Nenhum arquivo foi sobrescrito.");
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 96
            || !Regex.IsMatch(name, @"\A[a-zA-Z0-9][a-zA-Z0-9._-]*\.(md|txt)\z", RegexOptions.IgnoreCase)
            || Regex.IsMatch(name.Split('.')[0], @"\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])\z", RegexOptions.IgnoreCase))
            throw new ArgumentException("Use nome .md ou .txt com letras sem acento, números, ponto, hífen ou sublinhado (até 96 caracteres).");
    }

    private sealed class OperatorReview(ChangeSet expected) : IChangeReviewer
    {
        public ValueTask<ChangeReview> ReviewAsync(IntegrationCandidate candidate, CancellationToken token) =>
            ValueTask.FromResult(new ChangeReview(candidate, Owner, ReferenceEquals(candidate.Change, expected),
                "Operador confirmou o nome e o conteúdo completos desta proposta imutável."));
    }

    private sealed class ExactTextVerifier(ReviewedProjectFile expected) : ICombinedVerifier
    {
        public ValueTask<VerificationResult> VerifyAsync(IntegrationCandidate candidate, CancellationToken token)
        {
            var passed = candidate.Combined.Files.Count == 1 && candidate.Change.Changes.Count == 1
                && candidate.Change.Changes[0].Before is null
                && candidate.Combined.Files.GetValueOrDefault(expected.FileName) == expected.Content;
            return ValueTask.FromResult(passed
                ? VerificationResult.Pass(candidate.Change.TaskId, "Arquivo novo contém exatamente a resposta revisada; não verifica qualidade.")
                : VerificationResult.Fail(candidate.Change.TaskId, "Conteúdo diverge da proposta revisada."));
        }
    }

    private sealed class SavePolicy : IChangePolicy
    {
        public ChangeClassification Classify(IntegrationCandidate candidate) =>
            new(ChangeDisposition.OwnerAuthorization, ["create-reviewed-text-file"]);
    }

    private sealed class OperatorAuthorization(ChangeSet expected) : IChangeAuthorizer
    {
        public ValueTask<ChangeAuthorization> AuthorizeAsync(IntegrationCandidate candidate,
            ChangeClassification classification, CancellationToken token) =>
            ValueTask.FromResult(new ChangeAuthorization(candidate, ReferenceEquals(candidate.Change, expected), Owner));
    }
}
