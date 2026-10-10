using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcosystemAi.ProjectStore;

/// <summary>Ordem manual de Product; não confundir com um TaskRecord.Completed do Runtime.</summary>
public sealed record PlannedTaskAttempt(string Id, string Outcome, IReadOnlyList<string> RunIds,
    DateTimeOffset At);
public sealed record PlannedTaskEntry(string Id, string ProjectId, string SessionId,
    string Title, string Goal, string Acceptance, string Assignee, DateTimeOffset CreatedAt,
    bool Cancelled, IReadOnlyList<PlannedTaskAttempt> Attempts);
public sealed record PlannedTaskBoard(long Revision, IReadOnlyList<PlannedTaskEntry> Tasks);

internal sealed record PlannedTaskBody(int SchemaVersion, long Revision,
    IReadOnlyList<PlannedTaskEntry> Tasks);
internal sealed record PlannedTaskEnvelope(PlannedTaskBody Body, string Sha256);

/// <summary>
/// Planejamento local deliberado. Nenhum worker, retry, fila automática,
/// grant ou cópia dos receipts do LocalProjectStore. Grava apenas metas,
/// critérios e identificadores de runs já presentes no catálogo canônico.
/// </summary>
public sealed class LocalPlannedTaskStore
{
    public const int MaxGoal = 16 * 1024;
    private const int MaxSize = 3 * 1024 * 1024;
    private const int MaxTasks = 500;
    private const int MaxAttempts = 20;
    private const int SchemaVersion = 1;
    private readonly string _root;
    private readonly string _file;
    private readonly string _lock;
    private readonly Func<DateTimeOffset> _clock;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public LocalPlannedTaskStore(string catalogDirectory, Func<DateTimeOffset>? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDirectory);
        _root = Path.GetFullPath(catalogDirectory);
        _file = Path.Combine(_root, "planned-tasks.json");
        _lock = Path.Combine(_root, ".planned-tasks.writer.lock");
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        CheckRoot();
    }

    public PlannedTaskBoard Read()
    {
        var value = Load();
        return new PlannedTaskBoard(value.Revision, value.Tasks.Select(t => t with
        {
            Attempts = t.Attempts.Select(a => a with { RunIds = a.RunIds.ToArray() }).ToArray()
        }).ToArray());
    }

    public PlannedTaskEntry Create(string projectId, string sessionId, string title,
        string goal, string acceptance, string assignee)
    {
        CheckId(projectId); CheckId(sessionId);
        CheckText(title, 120); CheckText(goal, MaxGoal); CheckText(acceptance, 512);
        ArgumentException.ThrowIfNullOrWhiteSpace(assignee);
        var project = new LocalProjectStore(_root).Read().Projects
            .SingleOrDefault(p => p.Id == projectId)
            ?? throw new KeyNotFoundException("Projeto inexistente.");
        if (!project.Sessions.Any(s => s.Id == sessionId))
            throw new KeyNotFoundException("Sessão inexistente nesse projeto.");

        if (assignee != "default")
        {
            var roster = new LocalAgentRosterStore(_root).Read();
            if (assignee.StartsWith("agent:", StringComparison.Ordinal))
            {
                if (!roster.Agents.Any(a => a.Id == assignee[6..] && a.ProjectId == projectId))
                    throw new ArgumentException("Agente não pertence ao projeto.");
            }
            else if (assignee.StartsWith("team:", StringComparison.Ordinal))
            {
                if (!roster.Teams.Any(t => t.Id == assignee[5..] && t.ProjectId == projectId))
                    throw new ArgumentException("Equipe não pertence ao projeto.");
            }
            else throw new ArgumentException("Atribuição desconhecida.");
        }

        var created = new PlannedTaskEntry(Guid.NewGuid().ToString("N"),
            projectId, sessionId, title.Trim(), goal.Trim(), acceptance.Trim(), assignee,
            _clock(), false, []);
        return Change(state =>
        {
            if (state.Tasks.Count >= MaxTasks)
                throw new InvalidOperationException("Limite de 500 tarefas planejadas.");
            return (state with { Tasks = state.Tasks.Append(created).ToArray() }, created);
        });
    }

    public PlannedTaskEntry Cancel(string taskId)
    {
        CheckId(taskId);
        return Change(state =>
        {
            var task = state.Tasks.SingleOrDefault(t => t.Id == taskId)
                ?? throw new KeyNotFoundException("Tarefa não encontrada.");
            if (task.Cancelled)
                throw new InvalidOperationException("Tarefa já cancelada.");
            var updated = task with { Cancelled = true };
            return (Replace(state, updated), updated);
        });
    }

    /// <summary>
    /// Registra tentativa somente após o comando explícito retornar.
    /// Resposta presente/verificada != critérios do usuário aceitos.
    /// </summary>
    public PlannedTaskEntry RecordAttempt(string taskId, string outcome,
        IReadOnlyList<string> runIds)
    {
        CheckId(taskId);
        if (outcome is not ("response_verified" or "review_recorded" or "failed" or "unverified"))
            throw new ArgumentException("Outcome desconhecido.");
        ArgumentNullException.ThrowIfNull(runIds);
        if (runIds.Count > 2 || runIds.Any(string.IsNullOrWhiteSpace)
            || runIds.Distinct(StringComparer.Ordinal).Count() != runIds.Count)
            throw new ArgumentException("Runs inválidos.");
        return Change(state =>
        {
            var task = state.Tasks.SingleOrDefault(t => t.Id == taskId)
                ?? throw new KeyNotFoundException("Tarefa não encontrada.");
            if (task.Cancelled || task.Attempts.Count >= MaxAttempts)
                throw new InvalidOperationException("Tarefa cancelada ou limite de 20 tentativas.");
            var session = new LocalProjectStore(_root).Read().Projects
                .Single(p => p.Id == task.ProjectId).Sessions.Single(s => s.Id == task.SessionId);
            if (runIds.Any(id => !session.Runs.Any(r => r.RunId == id)))
                throw new InvalidOperationException("Run não pertence à sessão canônica.");
            if (outcome == "response_verified" &&
                (runIds.Count != 1 || !Verified(session, runIds[0])))
                throw new InvalidOperationException("Recibo único verificado exigido.");
            if (outcome == "review_recorded" &&
                (runIds.Count != 2 || runIds.Any(id => !Verified(session, id))
                    || !new LocalTeamReviewStore(_root).Read().Reviews.Any(r =>
                        r.ProjectId == task.ProjectId && r.SessionId == task.SessionId
                        && r.ProducerRunId == runIds[0] && r.ReviewerRunId == runIds[1])))
                throw new InvalidOperationException("Dois recibos e parecer registrado exigidos.");
            var attempt = new PlannedTaskAttempt(Guid.NewGuid().ToString("N"),
                outcome, runIds.ToArray(), _clock());
            var updated = task with { Attempts = task.Attempts.Append(attempt).ToArray() };
            return (Replace(state, updated), updated);
        });
    }

    private static bool Verified(SessionEntry session, string runId) =>
        session.Runs.Any(r => r.RunId == runId && r.Status == "succeeded" && r.Verified);

    private static PlannedTaskBody Replace(PlannedTaskBody state, PlannedTaskEntry task) =>
        state with { Tasks = state.Tasks.Select(t => t.Id == task.Id ? task : t).ToArray() };

    private T Change<T>(Func<PlannedTaskBody, (PlannedTaskBody Next, T Value)> action)
    {
        CheckRoot(); CheckFile(_lock);
        FileStream writer;
        try { writer = new FileStream(_lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException e) { throw new IOException("Planejamento ocupado por outro escritor.", e); }
        using (writer)
        {
            var before = Load();
            var (next, value) = action(before);
            next = next with { Revision = checked(before.Revision + 1) };
            Validate(next);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new PlannedTaskEnvelope(next, Hash(next)), Json);
            if (bytes.Length > MaxSize)
                throw new InvalidOperationException("Planejamento ultrapassou limite do catálogo.");
            CheckFile(_file);
            var temp = Path.Combine(_root, ".planned-tasks-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temp, _file, overwrite: true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            return value;
        }
    }

    private PlannedTaskBody Load()
    {
        CheckFile(_file);
        if (!File.Exists(_file)) return new PlannedTaskBody(SchemaVersion, 0, []);
        using var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 or > MaxSize)
            throw new InvalidDataException("Planejamento vazio ou acima do limite.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        PlannedTaskEnvelope envelope;
        try { envelope = JsonSerializer.Deserialize<PlannedTaskEnvelope>(bytes, Json)
            ?? throw new JsonException("Envelope de tarefas nulo."); }
        catch (JsonException e) { throw new InvalidDataException("Planejamento JSON inválido.", e); }
        if (envelope.Body is null || envelope.Body.SchemaVersion != SchemaVersion)
            throw new InvalidDataException("Schema de planejamento desconhecido.");
        Validate(envelope.Body);
        if (!string.Equals(Hash(envelope.Body), envelope.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Checksum do planejamento inválido.");
        return envelope.Body;
    }

    private static string Hash(PlannedTaskBody state) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state, Json))).ToLowerInvariant();

    private static void Validate(PlannedTaskBody state)
    {
        if (state.Revision < 0 || state.Tasks is null || state.Tasks.Count > MaxTasks)
            throw new InvalidDataException("Cabeçalho de tarefas inválido.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var task in state.Tasks)
        {
            if (task is null || !IsId(task.Id) || !IsId(task.ProjectId) || !IsId(task.SessionId)
                || !ids.Add(task.Id) || !IsText(task.Title, 120) || !IsText(task.Goal, MaxGoal)
                || !IsText(task.Acceptance, 512) || string.IsNullOrWhiteSpace(task.Assignee)
                || task.Assignee.Length > 50 || task.Attempts is null
                || task.Attempts.Count > MaxAttempts)
                throw new InvalidDataException("Tarefa inválida.");
            var attemptIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in task.Attempts)
                if (a is null || !IsId(a.Id) || !attemptIds.Add(a.Id)
                    || a.Outcome is not ("response_verified" or "review_recorded" or "failed" or "unverified")
                    || a.RunIds is null || a.RunIds.Count > 2
                    || a.RunIds.Any(string.IsNullOrWhiteSpace)
                    || a.RunIds.Distinct(StringComparer.Ordinal).Count() != a.RunIds.Count)
                    throw new InvalidDataException("Tentativa inválida.");
        }
    }

    private static bool IsId(string? value) => Guid.TryParseExact(value, "N", out _);
    private static bool IsText(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= max && !value.Contains('\0');
    private static void CheckId(string value)
    {
        if (!IsId(value)) throw new ArgumentException("ID inválido.");
    }
    private static void CheckText(string value, int length)
    {
        if (!IsText(value, length)) throw new ArgumentException("Campo vazio ou acima do limite.");
    }
    private void CheckRoot()
    {
        Directory.CreateDirectory(_root);
        for (DirectoryInfo? item = new(_root); item is not null; item = item.Parent)
            if (item.LinkTarget is not null) throw new IOException("Diretório simbólico não permitido.");
    }
    private static void CheckFile(string path)
    {
        var info = new FileInfo(path);
        if (info.LinkTarget is not null || (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            throw new IOException("Arquivo simbólico de planejamento não permitido.");
    }
}
