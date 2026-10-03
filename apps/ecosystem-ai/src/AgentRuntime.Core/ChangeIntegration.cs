using System.Collections.ObjectModel;

namespace AgentRuntime;

/// <summary>Snapshot imutável local. O adapter escolhe os IDs; o Core não conhece disco nem controle de versão.</summary>
public sealed class WorkspaceSnapshot
{
    public long Revision { get; }
    public IReadOnlyDictionary<string, string> Files { get; }

    public WorkspaceSnapshot(long revision, IEnumerable<KeyValuePair<string, string>> files)
    {
        if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
        Revision = revision;
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, content) in files)
        {
            WorkspacePaths.Validate(path);
            ArgumentNullException.ThrowIfNull(content);
            copy.Add(path, content);
        }
        Files = new ReadOnlyDictionary<string, string>(copy);
    }
}

public static class WorkspacePaths
{
    public static void Validate(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\\') || path.Contains(':') || path.Any(char.IsControl)
            || path.Split('/').Any(s => s is "" or "." or ".."))
            throw new ArgumentException("Caminho relativo portátil obrigatório.", nameof(path));
    }
}

public sealed record FileChange(string Path, string? Before, string? After);

/// <summary>Proposta selada; nunca é escrita no estado canônico pelo produtor.</summary>
public sealed class ChangeSet
{
    public string Id { get; }
    public string TaskId { get; }
    public string ProducerId { get; }
    public string RunId { get; }
    public WorkspaceSnapshot Base { get; }
    public IReadOnlyList<FileChange> Changes { get; }
    internal ChangeSet(string id, string taskId, string producerId, string runId, WorkspaceSnapshot basis, IEnumerable<FileChange> changes)
    {
        if (new[] { id, taskId, producerId, runId }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Proveniência completa obrigatória.");
        Id = id; TaskId = taskId; ProducerId = producerId; RunId = runId; Base = basis;
        Changes = Array.AsReadOnly(changes.ToArray());
    }
}

/// <summary>Overlay local: capabilities do adapter escrevem aqui, nunca no canônico.</summary>
public sealed class IsolatedWorkspace(WorkspaceSnapshot basis)
{
    private readonly Dictionary<string, string?> _overlay = new(StringComparer.Ordinal);
    public WorkspaceSnapshot Base { get; } = basis;
    public string? Read(string path)
    {
        WorkspacePaths.Validate(path);
        return _overlay.TryGetValue(path, out var value) ? value : Base.Files.GetValueOrDefault(path);
    }
    public void Write(string path, string content)
    {
        WorkspacePaths.Validate(path); ArgumentNullException.ThrowIfNull(content); _overlay[path] = content;
    }
    public void Delete(string path) { WorkspacePaths.Validate(path); _overlay[path] = null; }
    public ChangeSet Seal(string id, string taskId, string producerId, string runId) => new(id, taskId, producerId, runId, Base,
        _overlay.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Where(p => p.Value != Base.Files.GetValueOrDefault(p.Key))
            .Select(p => new FileChange(p.Key, Base.Files.GetValueOrDefault(p.Key), p.Value)));
}

public sealed record IntegrationCandidate(string Id, ChangeSet Change, WorkspaceSnapshot Current, WorkspaceSnapshot Combined);
public sealed record ChangeReview(IntegrationCandidate Candidate, string ReviewerId, bool Passed, string Evidence);
public enum ChangeDisposition { Automatic, OwnerAuthorization, Deny }
public sealed record ChangeClassification(ChangeDisposition Disposition, IReadOnlyList<string> Classes);
public sealed record ChangeAuthorization(IntegrationCandidate Candidate, bool Approved, string AuthorizedBy);

/// <summary>Portas confiáveis do Host. Não são argumentos gerados pelo produtor.</summary>
public interface IChangeReviewer
{
    ValueTask<ChangeReview> ReviewAsync(IntegrationCandidate candidate, CancellationToken cancellationToken);
}
public interface ICombinedVerifier
{
    ValueTask<VerificationResult> VerifyAsync(IntegrationCandidate candidate, CancellationToken cancellationToken);
}
public interface IChangePolicy
{
    ChangeClassification Classify(IntegrationCandidate candidate);
}
public interface IChangeAuthorizer
{
    ValueTask<ChangeAuthorization> AuthorizeAsync(IntegrationCandidate candidate, ChangeClassification classification, CancellationToken cancellationToken);
}
public enum IntegrationStatus { Integrated, Conflict, ReviewFailed, VerificationFailed, Escalated, Denied, AlreadyIntegrated }
public sealed class IntegrationReceipt
{
    public string ChangeId { get; }
    public string TaskId { get; }
    public string ProducerId { get; }
    public IntegrationStatus Status { get; }
    public long Revision { get; }
    public string CandidateId { get; }
    public string ReviewerId { get; }
    public string Evidence { get; }
    public IReadOnlyList<string> Conflicts { get; }
    internal IntegrationReceipt(string changeId, string taskId, string producerId, IntegrationStatus status, long revision,
        string candidateId, string reviewerId, string evidence, IReadOnlyList<string> conflicts)
    {
        ChangeId = changeId; TaskId = taskId; ProducerId = producerId; Status = status; Revision = revision;
        CandidateId = candidateId; ReviewerId = reviewerId; Evidence = evidence; Conflicts = Array.AsReadOnly(conflicts.ToArray());
    }
}

/// <summary>
/// Integrador local serial. Testa o combinado exato e só publica depois de revisão independente,
/// verificação e classificação confiáveis. Erro/cancelamento não publica estado parcial.
/// </summary>
public sealed class ChangeIntegrator(WorkspaceSnapshot initial, IChangeReviewer reviewer, ICombinedVerifier verifier,
    IChangePolicy policy, string ownerId, IChangeAuthorizer? authorizer = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _integrated = new(StringComparer.Ordinal);
    private WorkspaceSnapshot _current = initial;
    private long _attempt;
    public WorkspaceSnapshot Current => Volatile.Read(ref _current);

    public async ValueTask<IntegrationReceipt> IntegrateAsync(ChangeSet change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IntegrationReceipt Result(IntegrationStatus status, string candidateId = "", string reviewerId = "", string evidence = "", IReadOnlyList<string>? conflicts = null) =>
                new(change.Id, change.TaskId, change.ProducerId, status, Current.Revision, candidateId, reviewerId, evidence, conflicts ?? []);
            if (_integrated.Contains(change.Id)) return Result(IntegrationStatus.AlreadyIntegrated);
            var current = Current;
            var conflicts = change.Changes.Where(c => current.Files.GetValueOrDefault(c.Path) != c.Before).Select(c => c.Path).ToArray();
            if (conflicts.Length > 0) return Result(IntegrationStatus.Conflict, conflicts: Array.AsReadOnly(conflicts));
            if (change.Changes.Count == 0) return Result(IntegrationStatus.Denied, evidence: "Mudança vazia.");
            var combined = current.Files.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            foreach (var c in change.Changes) { if (c.After is null) combined.Remove(c.Path); else combined[c.Path] = c.After; }
            var candidate = new IntegrationCandidate($"candidate-{checked(++_attempt)}", change, current,
                new WorkspaceSnapshot(checked(current.Revision + 1), combined));
            var review = await reviewer.ReviewAsync(candidate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!review.Passed || !ReferenceEquals(review.Candidate, candidate) || string.IsNullOrWhiteSpace(review.ReviewerId)
                || review.ReviewerId == change.ProducerId || string.IsNullOrWhiteSpace(review.Evidence))
                return Result(IntegrationStatus.ReviewFailed, candidate.Id, review.ReviewerId);
            var verification = await verifier.VerifyAsync(candidate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!verification.Passed || verification.TaskId != change.TaskId || string.IsNullOrWhiteSpace(verification.Evidence))
                return Result(IntegrationStatus.VerificationFailed, candidate.Id, review.ReviewerId);
            var classified = policy.Classify(candidate);
            var classification = classified with { Classes = Array.AsReadOnly(classified.Classes.ToArray()) };
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(classification.Disposition) || classification.Disposition == ChangeDisposition.Deny)
                return Result(IntegrationStatus.Denied, candidate.Id, review.ReviewerId);
            if (classification.Disposition == ChangeDisposition.OwnerAuthorization)
            {
                if (authorizer is null || string.IsNullOrWhiteSpace(ownerId)) return Result(IntegrationStatus.Escalated, candidate.Id, review.ReviewerId);
                var authorization = await authorizer.AuthorizeAsync(candidate, classification, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!authorization.Approved || !ReferenceEquals(authorization.Candidate, candidate) || authorization.AuthorizedBy != ownerId)
                    return Result(IntegrationStatus.Escalated, candidate.Id, review.ReviewerId);
                // A política é lida novamente; uma autorização nunca contorna uma revogação.
                var fresh = policy.Classify(candidate);
                if (fresh.Disposition == ChangeDisposition.Deny || !Enum.IsDefined(fresh.Disposition)
                    || !fresh.Classes.SequenceEqual(classification.Classes, StringComparer.Ordinal))
                    return Result(IntegrationStatus.Denied, candidate.Id, review.ReviewerId);
            }
            cancellationToken.ThrowIfCancellationRequested();
            _integrated.Add(change.Id);
            Volatile.Write(ref _current, candidate.Combined);
            return Result(IntegrationStatus.Integrated, candidate.Id, review.ReviewerId, verification.Evidence);
        }
        finally { _gate.Release(); }
    }
}
