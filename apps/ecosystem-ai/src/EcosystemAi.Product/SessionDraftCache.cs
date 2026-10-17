namespace EcosystemAi.Product;

/// <summary>
/// Rascunho privado e volátil, identificado pelo projeto e pela conversa.
/// Nunca entra em catálogo, journal, Bundle Android ou prompt de outro projeto.
/// </summary>
public sealed record SessionDraft(string Goal, string RequiredText)
{
    public static SessionDraft Empty { get; } = new("", "");
}

public sealed class SessionDraftCache
{
    public const int MaxSessions = 32;
    public const int MaxRequiredText = 512;

    private readonly object _gate = new();
    private readonly Dictionary<(string Project, string Session), SessionDraft> _entries = [];
    private readonly LinkedList<(string Project, string Session)> _recent = new();

    public SessionDraft Get(string projectId, string sessionId)
    {
        var key = Key(projectId, sessionId);
        lock (_gate) return _entries.GetValueOrDefault(key) ?? SessionDraft.Empty;
    }

    public void Save(string projectId, string sessionId, string? goal, string? requiredText)
    {
        var key = Key(projectId, sessionId);
        goal ??= "";
        requiredText ??= "";
        if (goal.Length > ProductExecutionHost.MaxText || requiredText.Length > MaxRequiredText)
            throw new ArgumentException("Rascunho maior que o limite de envio autorizado.");

        lock (_gate)
        {
            _recent.Remove(key);
            if (goal.Length == 0 && requiredText.Length == 0)
            {
                _entries.Remove(key);
                return;
            }

            if (!_entries.ContainsKey(key) && _entries.Count == MaxSessions)
            {
                var oldest = _recent.First!.Value;
                _recent.RemoveFirst();
                _entries.Remove(oldest);
            }
            _entries[key] = new SessionDraft(goal, requiredText);
            _recent.AddLast(key);
        }
    }

    public void Clear(string projectId, string sessionId) => Save(projectId, sessionId, "", "");

    private static (string Project, string Session) Key(string projectId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Projeto e conversa precisam ter identidade.");
        return (projectId, sessionId);
    }
}
