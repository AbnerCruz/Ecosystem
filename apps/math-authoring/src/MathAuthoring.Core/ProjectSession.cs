namespace MathAuthoring.Core;

/// <summary>
/// Single publication boundary for visual commands and valid JSON drafts.
/// History stores validated canonical snapshots; no mutable UI representation is authoritative.
/// </summary>
public sealed class ProjectSession
{
    private const int MaxHistory = 64;
    private readonly object _gate = new();
    private readonly List<string> _undo = new();
    private readonly List<string> _redo = new();
    private string _canonical;
    private long _revision;

    public long Revision { get { lock (_gate) return _revision; } }

    public ProjectDocument Snapshot { get { lock (_gate) return ReadValidated(_canonical); } }

    public string Source { get { lock (_gate) return _canonical; } }

    public bool CanUndo { get { lock (_gate) return _undo.Count > 0; } }

    public bool CanRedo { get { lock (_gate) return _redo.Count > 0; } }

    public ProjectSession(ProjectDocument initial)
    {
        _canonical = ProjectJson.Format(initial);
    }

    public static DocumentReadResult Open(string json) => ProjectJson.Parse(json);

    /// <summary>Visual/editor commands use the same validation and publication path as JSON edits.</summary>
    public DocumentChangeResult Apply(long baseRevision, Func<ProjectDocument, ProjectDocument> command)
    {
        ArgumentNullException.ThrowIfNull(command);
        lock (_gate)
        {
            var conflict = Conflict(baseRevision);
            if (conflict is not null) return conflict;

            // Commands only receive a detached snapshot. A reentrant edit can
            // advance the revision, so recheck after invoking external code.
            var candidate = command(ReadValidated(_canonical));
            conflict = Conflict(baseRevision);
            if (conflict is not null) return conflict;
            if (candidate is null)
                return Reject("transaction.null", "Comando retornou documento nulo.");
            var problems = ProjectValidator.Validate(candidate);
            if (problems.Count != 0)
                return new DocumentChangeResult(false, _revision, problems);
            if (!string.Equals(candidate.ProjectId, ReadValidated(_canonical).ProjectId, StringComparison.Ordinal))
                return Reject("transaction.project-id", "Uma edição não pode mudar a identidade do projeto.");
            try
            {
                return Publish(ProjectJson.Format(candidate));
            }
            catch (ArgumentException)
            {
                return Reject("json.too-large", "Documento excede o limite de serialização.");
            }
        }
    }

    /// <summary>Text view can commit only a fully parsed document at its original base revision.</summary>
    public DocumentChangeResult ApplyText(long baseRevision, string json)
    {
        lock (_gate)
        {
            var conflict = Conflict(baseRevision);
            if (conflict is not null) return conflict;
            var parsed = ProjectJson.Parse(json);
            if (!parsed.Success)
                return new DocumentChangeResult(false, _revision, parsed.Problems);
            if (!string.Equals(parsed.Document!.ProjectId, ReadValidated(_canonical).ProjectId, StringComparison.Ordinal))
                return Reject("transaction.project-id", "Uma edição não pode mudar a identidade do projeto.");
            return Publish(ProjectJson.Format(parsed.Document));
        }
    }

    public DocumentChangeResult Undo(long baseRevision)
    {
        lock (_gate)
        {
            var conflict = Conflict(baseRevision);
            if (conflict is not null) return conflict;
            if (_undo.Count == 0) return Reject("history.empty", "Nada a desfazer.");
            var previous = Pop(_undo);
            Append(_redo, _canonical);
            _canonical = previous;
            _revision++;
            return new DocumentChangeResult(true, _revision, Array.Empty<ValidationProblem>());
        }
    }

    public DocumentChangeResult Redo(long baseRevision)
    {
        lock (_gate)
        {
            var conflict = Conflict(baseRevision);
            if (conflict is not null) return conflict;
            if (_redo.Count == 0) return Reject("history.empty", "Nada a refazer.");
            var next = Pop(_redo);
            Append(_undo, _canonical);
            _canonical = next;
            _revision++;
            return new DocumentChangeResult(true, _revision, Array.Empty<ValidationProblem>());
        }
    }

    private DocumentChangeResult Publish(string canonical)
    {
        if (string.Equals(_canonical, canonical, StringComparison.Ordinal))
            return new DocumentChangeResult(false, _revision, Array.Empty<ValidationProblem>());
        Append(_undo, _canonical);
        _redo.Clear();
        _canonical = canonical;
        _revision++;
        return new DocumentChangeResult(true, _revision, Array.Empty<ValidationProblem>());
    }

    private DocumentChangeResult? Conflict(long revision) => revision == _revision
        ? null
        : Reject("transaction.stale", "Revisão-base obsoleta. Rebase ou descarte explícito é necessário.");

    private DocumentChangeResult Reject(string code, string message) =>
        new(false, _revision, new[] { new ValidationProblem(code, message) });

    private static void Append(List<string> history, string snapshot)
    {
        if (history.Count == MaxHistory) history.RemoveAt(0);
        history.Add(snapshot);
    }

    private static string Pop(List<string> history)
    {
        var last = history.Count - 1;
        var snapshot = history[last];
        history.RemoveAt(last);
        return snapshot;
    }

    private static ProjectDocument ReadValidated(string source)
    {
        var result = ProjectJson.Parse(source);
        return result.Document ?? throw new InvalidOperationException("Snapshot interno inválido.");
    }
}

public sealed record DocumentChangeResult(
    bool Applied,
    long Revision,
    IReadOnlyList<ValidationProblem> Problems)
{
    public bool Accepted => Problems.Count == 0;
}
