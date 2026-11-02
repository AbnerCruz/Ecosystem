namespace AgentRuntime;

/// <summary>Um artefato produzido por uma ferramenta (arquivo, documento, etc.): referência estável, nunca o conteúdo.</summary>
public sealed record ArtifactRef(string Id, string Kind, string Location);

/// <summary>Unidade de trabalho com critérios de aceite verificáveis (plano §5.2). O verificador decide se foram atendidos.</summary>
public sealed record TaskSpec(string Id, string Goal, IReadOnlyList<string> Acceptance);

public enum TaskState { Planned, Running, Verifying, Completed, Blocked, Cancelled, Failed }

public sealed record VerificationResult(string TaskId, bool Passed, string Evidence, IReadOnlyList<string> Checks)
{
    public static VerificationResult Pass(string taskId, string evidence, params string[] checks) => new(taskId, true, evidence, checks);
    public static VerificationResult Fail(string taskId, string evidence, params string[] checks) => new(taskId, false, evidence, checks);
}

public sealed record RunOutput(string FinalText, IReadOnlyList<ArtifactRef> Artifacts);

/// <summary>
/// Verificador independente: olha o resultado real do mundo (não a palavra do modelo). Critério que ele não sabe verificar deve
/// FALHAR (falha fechada), nunca passar.
/// </summary>
public interface IVerifier
{
    ValueTask<VerificationResult> VerifyAsync(TaskSpec task, RunOutput output, CancellationToken cancellationToken);
}

/// <summary>
/// Estado de uma tarefa. <c>output ≠ success</c> (plano §5.2): não existe caminho para <see cref="TaskState.Completed"/> sem uma
/// <see cref="VerificationResult"/> aprovada, desta tarefa e com evidência — o tipo impede, não a convenção.
/// </summary>
public sealed class TaskRecord(TaskSpec spec)
{
    public TaskSpec Spec { get; } = spec;
    public TaskState State { get; private set; } = TaskState.Planned;
    public VerificationResult? Verification { get; private set; }

    public void Start() => Move(TaskState.Running, TaskState.Planned, TaskState.Blocked);
    public void BeginVerification() => Move(TaskState.Verifying, TaskState.Running);
    public void Rework() => Move(TaskState.Running, TaskState.Verifying);
    public void Block() => Move(TaskState.Blocked, TaskState.Planned, TaskState.Running, TaskState.Verifying);
    public void Cancel() => Move(TaskState.Cancelled, TaskState.Planned, TaskState.Running, TaskState.Verifying, TaskState.Blocked);
    public void Fail() => Move(TaskState.Failed, TaskState.Planned, TaskState.Running, TaskState.Verifying, TaskState.Blocked);

    public void Complete(VerificationResult verification)
    {
        if (State != TaskState.Verifying)
            throw new InvalidOperationException($"Tarefa '{Spec.Id}' só pode ser concluída a partir de 'Verifying' (está em '{State}').");
        if (!verification.Passed)
            throw new InvalidOperationException($"Tarefa '{Spec.Id}' exige verificação aprovada para ser concluída.");
        if (!string.Equals(verification.TaskId, Spec.Id, StringComparison.Ordinal))
            throw new InvalidOperationException($"Verificação é da tarefa '{verification.TaskId}', não de '{Spec.Id}'.");
        if (string.IsNullOrWhiteSpace(verification.Evidence))
            throw new InvalidOperationException($"Tarefa '{Spec.Id}' exige evidência verificável.");
        Verification = verification;
        State = TaskState.Completed;
    }

    private void Move(TaskState to, params TaskState[] from)
    {
        if (Array.IndexOf(from, State) < 0)
            throw new InvalidOperationException($"Transição inválida da tarefa '{Spec.Id}': {State} → {to}.");
        State = to;
    }
}
