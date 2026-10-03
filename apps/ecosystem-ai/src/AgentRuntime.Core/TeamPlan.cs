namespace AgentRuntime;

public sealed record TeamAssignment(TaskSpec Task, string TeamId, string ProducerId, string ReviewerId, IReadOnlyList<string> DependsOn);

/// <summary>Plano efêmero local. Dependência só libera depois de integração, não depois de texto produzido.</summary>
public sealed class TeamPlan
{
    private readonly Dictionary<string, TeamAssignment> _tasks;
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    public TeamPlan(IEnumerable<TeamAssignment> tasks)
    {
        _tasks = new(StringComparer.Ordinal);
        foreach (var t in tasks)
        {
            if (new[] { t.Task.Id, t.TeamId, t.ProducerId, t.ReviewerId }.Any(string.IsNullOrWhiteSpace)
                || t.ProducerId == t.ReviewerId || t.Task.Acceptance.Count == 0)
                throw new ArgumentException("Identidade, revisão independente e aceite obrigatórios.");
            _tasks.Add(t.Task.Id, t with { Task = t.Task with { Acceptance = Array.AsReadOnly(t.Task.Acceptance.ToArray()) },
                DependsOn = Array.AsReadOnly(t.DependsOn.Distinct(StringComparer.Ordinal).ToArray()) });
        }
        foreach (var t in _tasks.Values)
            if (t.DependsOn.Any(d => !_tasks.ContainsKey(d))) throw new ArgumentException("Dependência ausente.");
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (visited.Contains(id)) return;
            if (!active.Add(id)) throw new ArgumentException("Plano cíclico.");
            foreach (var d in _tasks[id].DependsOn) Visit(d);
            active.Remove(id); visited.Add(id);
        }
        foreach (var id in _tasks.Keys) Visit(id);
    }
    public IReadOnlyList<TeamAssignment> Ready => _tasks.Values
        .Where(t => !_completed.Contains(t.Task.Id) && t.DependsOn.All(_completed.Contains))
        .OrderBy(t => t.Task.Id, StringComparer.Ordinal).ToArray();

    public void Record(IntegrationReceipt receipt)
    {
        if (!_tasks.TryGetValue(receipt.TaskId, out var task) || receipt.Status != IntegrationStatus.Integrated
            || string.IsNullOrWhiteSpace(receipt.Evidence) || receipt.ReviewerId != task.ReviewerId || receipt.ProducerId != task.ProducerId
            || !task.DependsOn.All(_completed.Contains))
            throw new InvalidOperationException("Dependência só conclui com integração verificada e revisão designada.");
        _completed.Add(receipt.TaskId);
    }
}
