using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcosystemAi.ProjectStore;

/// <summary>
/// Evidência de que dois runs de uma equipe ocorreram, mais o parecer
/// EXPLÍCITO do operador local. NÃO é IntegrationReceipt/ChangeSet.
/// </summary>
public sealed record LocalTeamReview(
    string Id, string ProjectId, string SessionId, string TeamId,
    string ProducerId, string ReviewerId, string ProducerRunId, string ReviewerRunId,
    int ProducerTurnIndex, int ReviewerTurnIndex, string ProducerHash, string ReviewerHash,
    string Decision, string? Note, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);

public sealed record TeamReviewBoard(long Revision, IReadOnlyList<LocalTeamReview> Reviews);
internal sealed record ReviewBody(int SchemaVersion, long Revision, IReadOnlyList<LocalTeamReview> Reviews);
internal sealed record ReviewEnvelope(ReviewBody Body, string Sha256);

/// <summary>
/// Arquivo auxiliar do mesmo catálogo do Product. Somente declara
/// owner_accepted/owner_rejected sobre um parecer comprovadamente presente.
/// Não há promoção a TaskRecord.Completed nem a IntegrationReceipt.
/// </summary>
public sealed class LocalTeamReviewStore
{
    public const int MaxNote = 512;
    private const int SchemaVersion = 1;
    private const int MaxItems = 2000;
    private const int MaxBytes = 2 * 1024 * 1024;
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

    public LocalTeamReviewStore(string catalogDirectory, Func<DateTimeOffset>? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDirectory);
        _root = Path.GetFullPath(catalogDirectory);
        _file = Path.Combine(_root, "team-reviews.json");
        _lock = Path.Combine(_root, ".team-reviews.writer.lock");
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        CheckRoot();
    }

    public TeamReviewBoard Read()
    {
        var state = Load();
        return new TeamReviewBoard(state.Revision, state.Reviews.ToArray());
    }

    public LocalTeamReview Record(string projectId, string sessionId, string teamId,
        string producerRunId, string reviewerRunId, int producerTurnIndex, int reviewerTurnIndex)
    {
        var (project, session, team) = Context(projectId, sessionId, teamId);
        if (string.IsNullOrWhiteSpace(producerRunId)
            || string.IsNullOrWhiteSpace(reviewerRunId)
            || producerRunId == reviewerRunId
            || producerTurnIndex < 0 || reviewerTurnIndex <= producerTurnIndex
            || reviewerTurnIndex >= session.Turns.Count)
            throw new ArgumentException("Par de runs/turnos inválido.");
        if (!PairPresent(session, producerRunId, reviewerRunId, producerTurnIndex, reviewerTurnIndex))
            throw new InvalidOperationException("Parecer não comprovado por dois recibos verificados e duas respostas.");

        var entry = new LocalTeamReview(Guid.NewGuid().ToString("N"), project.Id, session.Id,
            team.Id, team.ProducerId, team.ReviewerId, producerRunId, reviewerRunId,
            producerTurnIndex, reviewerTurnIndex,
            Digest(session.Turns[producerTurnIndex].Text), Digest(session.Turns[reviewerTurnIndex].Text),
            "pending_owner", null, _clock(), null);
        return Change(current =>
        {
            if (current.Reviews.Count >= MaxItems)
                throw new InvalidOperationException("Limite de 2000 pareceres.");
            if (current.Reviews.Any(r => r.ProjectId == projectId
                && r.SessionId == sessionId && r.ReviewerRunId == reviewerRunId))
                throw new InvalidOperationException("Run do revisor já registrado.");
            return (current with { Reviews = current.Reviews.Append(entry).ToArray() }, entry);
        });
    }

    /// <summary>
    /// Somente anotação do operador que controla o servidor localhost;
    /// nenhuma autorização de merge, gravação de arquivo ou gasto.
    /// </summary>
    public LocalTeamReview Decide(string reviewId, string decision, string note)
    {
        if (!Guid.TryParseExact(reviewId, "N", out _))
            throw new ArgumentException("ID do parecer inválido.");
        if (decision is not ("owner_accepted" or "owner_rejected"))
            throw new ArgumentException("Decisão inválida.");
        if (string.IsNullOrWhiteSpace(note) || note.Length > MaxNote || note.Contains('\0'))
            throw new ArgumentException("Justificativa é obrigatória (até 512 caracteres).");

        return Change(current =>
        {
            var entry = current.Reviews.SingleOrDefault(r => r.Id == reviewId)
                ?? throw new KeyNotFoundException("Parecer inexistente.");
            if (entry.Decision != "pending_owner")
                throw new InvalidOperationException("Decisão final não pode ser repetida ou revertida.");
            var (_, session, team) = Context(entry.ProjectId, entry.SessionId, entry.TeamId);
            if (team.ProducerId != entry.ProducerId || team.ReviewerId != entry.ReviewerId
                || !PairPresent(session, entry.ProducerRunId, entry.ReviewerRunId,
                    entry.ProducerTurnIndex, entry.ReviewerTurnIndex)
                || !string.Equals(entry.ProducerHash, Digest(session.Turns[entry.ProducerTurnIndex].Text),
                    StringComparison.Ordinal)
                || !string.Equals(entry.ReviewerHash, Digest(session.Turns[entry.ReviewerTurnIndex].Text),
                    StringComparison.Ordinal))
                throw new InvalidDataException("Evidência mudou; parecer não pode ser decidido.");
            var updated = entry with { Decision = decision, Note = note.Trim(), DecidedAt = _clock() };
            return (current with { Reviews = current.Reviews.Select(r =>
                r.Id == reviewId ? updated : r).ToArray() }, updated);
        });
    }

    private (ProjectEntry Project, SessionEntry Session, LocalAgentTeam Team) Context(
        string projectId, string sessionId, string teamId)
    {
        var project = new LocalProjectStore(_root).Read().Projects
            .SingleOrDefault(x => x.Id == projectId)
            ?? throw new KeyNotFoundException("Projeto inexistente.");
        var session = project.Sessions.SingleOrDefault(x => x.Id == sessionId)
            ?? throw new KeyNotFoundException("Sessão inexistente.");
        var roster = new LocalAgentRosterStore(_root).Read();
        var team = roster.Teams.SingleOrDefault(x => x.Id == teamId && x.ProjectId == projectId)
            ?? throw new KeyNotFoundException("Equipe não pertence ao projeto.");
        if (team.ProducerId == team.ReviewerId
            || !roster.Agents.Any(a => a.Id == team.ProducerId && a.ProjectId == projectId)
            || !roster.Agents.Any(a => a.Id == team.ReviewerId && a.ProjectId == projectId))
            throw new InvalidDataException("Equipe sem agentes independentes.");
        return (project, session, team);
    }

    private static bool PairPresent(SessionEntry session, string producerRunId,
        string reviewerRunId, int producerTurnIndex, int reviewerTurnIndex)
    {
        if (producerTurnIndex < 0 || reviewerTurnIndex <= producerTurnIndex
            || reviewerTurnIndex >= session.Turns.Count) return false;
        var first = session.Runs.SingleOrDefault(r => r.RunId == producerRunId);
        var second = session.Runs.SingleOrDefault(r => r.RunId == reviewerRunId);
        return first is { Status: "succeeded", Verified: true }
            && second is { Status: "succeeded", Verified: true }
            && session.Runs.IndexOf(first) < session.Runs.IndexOf(second)
            && session.Turns[producerTurnIndex].Role == "assistant"
            && session.Turns[reviewerTurnIndex].Role == "assistant"
            && !string.IsNullOrWhiteSpace(session.Turns[producerTurnIndex].Text)
            && !string.IsNullOrWhiteSpace(session.Turns[reviewerTurnIndex].Text);
    }

    private T Change<T>(Func<ReviewBody, (ReviewBody Next, T Value)> action)
    {
        CheckRoot(); CheckFile(_lock);
        FileStream writer;
        try { writer = new FileStream(_lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException e) { throw new IOException("Pareceres ocupados por outro escritor.", e); }
        using (writer)
        {
            var before = Load();
            var (next, result) = action(before);
            next = next with { Revision = checked(before.Revision + 1) };
            Validate(next);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new ReviewEnvelope(next, Hash(next)), Json);
            if (bytes.Length > MaxBytes)
                throw new InvalidOperationException("Pareceres acima do limite do Product.");
            CheckFile(_file);
            var temp = Path.Combine(_root, ".team-review-" + Guid.NewGuid().ToString("N") + ".tmp");
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
            return result;
        }
    }

    private ReviewBody Load()
    {
        CheckFile(_file);
        if (!File.Exists(_file)) return new ReviewBody(SchemaVersion, 0, []);
        using var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 or > MaxBytes)
            throw new InvalidDataException("Pareceres vazios ou grandes demais.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        ReviewEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<ReviewEnvelope>(bytes, Json)
                ?? throw new JsonException("Envelope de pareceres vazio.");
        }
        catch (JsonException e) { throw new InvalidDataException("Pareceres JSON inválidos.", e); }
        if (envelope.Body is null || envelope.Body.SchemaVersion != SchemaVersion)
            throw new InvalidDataException("Versão desconhecida dos pareceres.");
        Validate(envelope.Body);
        if (!string.Equals(envelope.Sha256, Hash(envelope.Body), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Checksum dos pareceres inválido.");
        return envelope.Body;
    }

    private static void Validate(ReviewBody state)
    {
        if (state.Revision < 0 || state.Reviews is null || state.Reviews.Count > MaxItems)
            throw new InvalidDataException("Cabeçalho dos pareceres inválido.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var runIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var x in state.Reviews)
        {
            if (x is null || !Guid.TryParseExact(x.Id, "N", out _)
                || !Guid.TryParseExact(x.ProjectId, "N", out _)
                || !Guid.TryParseExact(x.SessionId, "N", out _)
                || !Guid.TryParseExact(x.TeamId, "N", out _)
                || !Guid.TryParseExact(x.ProducerId, "N", out _)
                || !Guid.TryParseExact(x.ReviewerId, "N", out _)
                || x.ProducerId == x.ReviewerId || !ids.Add(x.Id)
                || string.IsNullOrWhiteSpace(x.ProducerRunId)
                || string.IsNullOrWhiteSpace(x.ReviewerRunId)
                || !runIds.Add(x.ReviewerRunId)
                || x.ProducerRunId == x.ReviewerRunId
                || x.ProducerTurnIndex < 0 || x.ReviewerTurnIndex <= x.ProducerTurnIndex
                || x.ProducerHash.Length != 64 || x.ReviewerHash.Length != 64
                || x.Decision is not ("pending_owner" or "owner_accepted" or "owner_rejected")
                || (x.Decision == "pending_owner" && (x.Note is not null || x.DecidedAt is not null))
                || (x.Decision != "pending_owner"
                    && (string.IsNullOrWhiteSpace(x.Note) || x.Note.Length > MaxNote || x.DecidedAt is null)))
                throw new InvalidDataException("Parecer inválido ou duplicado.");
        }
    }

    private static string Digest(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

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
            throw new IOException("Arquivo de pareceres não pode ser simbólico.");
    }
}
