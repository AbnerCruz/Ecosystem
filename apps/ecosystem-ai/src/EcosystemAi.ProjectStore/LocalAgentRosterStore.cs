using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcosystemAi.ProjectStore;

/// <summary>Perfis e equipes do Product, sem credenciais, grants ou estado de execução.</summary>
public sealed record LocalAgentProfile(string Id, string ProjectId, string Name, string Instructions, DateTimeOffset CreatedAt);
public sealed record LocalAgentTeam(string Id, string ProjectId, string Name, string ProducerId, string ReviewerId, DateTimeOffset CreatedAt);
public sealed record AgentRoster(long Revision, IReadOnlyList<LocalAgentProfile> Agents, IReadOnlyList<LocalAgentTeam> Teams);

internal sealed record RosterPayload(int SchemaVersion, long Revision,
    IReadOnlyList<LocalAgentProfile> Agents, IReadOnlyList<LocalAgentTeam> Teams);
internal sealed record RosterEnvelope(RosterPayload Body, string Sha256);

/// <summary>
/// Dados auxiliares de Product ao lado do catalog.json, nunca dentro dos
/// workspaces: preserva exatamente o formato v1 existente do catálogo.
/// Não cria Runtime, agenda, grants, ledger ou estado de execução paralelo.
/// </summary>
public sealed class LocalAgentRosterStore
{
    public const int MaxInstructions = 2048;
    private const int SchemaVersion = 1;
    private const int MaxBytes = 1024 * 1024;
    private const int MaxAgents = 20;
    private const int MaxTeams = 10;

    private readonly string _root;
    private readonly string _file;
    private readonly string _lock;
    private readonly Func<DateTimeOffset> _clock;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false
    };

    public LocalAgentRosterStore(string catalogDirectory, Func<DateTimeOffset>? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogDirectory);
        _root = Path.GetFullPath(catalogDirectory);
        _file = Path.Combine(_root, "roster.json");
        _lock = Path.Combine(_root, ".roster.writer.lock");
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        CheckRoot();
    }

    public AgentRoster Read()
    {
        var data = Load();
        return new AgentRoster(data.Revision, data.Agents.ToArray(), data.Teams.ToArray());
    }

    public LocalAgentProfile CreateAgent(string projectId, string name, string instructions)
    {
        CheckId(projectId);
        CheckText(name, 80);
        CheckText(instructions, MaxInstructions);
        EnsureProject(projectId);
        return Change(state =>
        {
            if (state.Agents.Count(a => a.ProjectId == projectId) >= MaxAgents)
                throw new InvalidOperationException("Limite de 20 agentes por projeto.");
            if (state.Agents.Any(a => a.ProjectId == projectId &&
                string.Equals(a.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Já existe agente com este nome no projeto.");
            var created = new LocalAgentProfile(Guid.NewGuid().ToString("N"), projectId,
                name.Trim(), instructions.Trim(), _clock());
            return (state with { Agents = state.Agents.Append(created).ToArray() }, created);
        });
    }

    public LocalAgentTeam CreateTeam(string projectId, string name, string producerId, string reviewerId)
    {
        CheckId(projectId); CheckId(producerId); CheckId(reviewerId); CheckText(name, 80);
        if (producerId == reviewerId)
            throw new ArgumentException("Uma equipe exige produtor e revisor independentes.");
        EnsureProject(projectId);
        return Change(state =>
        {
            if (state.Teams.Count(t => t.ProjectId == projectId) >= MaxTeams)
                throw new InvalidOperationException("Limite de 10 equipes por projeto.");
            if (state.Teams.Any(t => t.ProjectId == projectId &&
                string.Equals(t.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Já existe equipe com este nome no projeto.");
            if (!state.Agents.Any(a => a.ProjectId == projectId && a.Id == producerId)
                || !state.Agents.Any(a => a.ProjectId == projectId && a.Id == reviewerId))
                throw new ArgumentException("Produtor e revisor devem pertencer ao mesmo projeto.");
            var created = new LocalAgentTeam(Guid.NewGuid().ToString("N"), projectId,
                name.Trim(), producerId, reviewerId, _clock());
            return (state with { Teams = state.Teams.Append(created).ToArray() }, created);
        });
    }

    private void EnsureProject(string id)
    {
        // Sem catálogo/projeto canônico: não criar roster órfão.
        if (!File.Exists(Path.Combine(_root, "catalog.json"))
            || !new LocalProjectStore(_root).Read().Projects.Any(p => p.Id == id))
            throw new KeyNotFoundException("Projeto não registrado no catálogo.");
    }

    private T Change<T>(Func<RosterPayload, (RosterPayload Next, T Value)> action)
    {
        CheckRoot(); CheckFile(_lock);
        FileStream handle;
        try { handle = new FileStream(_lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException e) { throw new IOException("Roster ocupado por outro escritor.", e); }
        using (handle)
        {
            var before = Load();
            var (next, value) = action(before);
            next = next with { Revision = checked(before.Revision + 1) };
            Validate(next);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                new RosterEnvelope(next, Hash(next)), Json);
            if (bytes.Length > MaxBytes)
                throw new InvalidOperationException("Roster excedeu 1 MiB.");
            CheckFile(_file);
            var temp = Path.Combine(_root, ".roster-" + Guid.NewGuid().ToString("N") + ".tmp");
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

    private RosterPayload Load()
    {
        CheckFile(_file);
        if (!File.Exists(_file)) return new RosterPayload(SchemaVersion, 0, [], []);
        using var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 or > MaxBytes)
            throw new InvalidDataException("Roster vazio ou acima do limite.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        RosterEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<RosterEnvelope>(bytes, Json)
                ?? throw new JsonException("Roster nulo.");
        }
        catch (JsonException e) { throw new InvalidDataException("Roster JSON inválido.", e); }
        if (envelope.Body is null || envelope.Body.SchemaVersion != SchemaVersion)
            throw new InvalidDataException("Versão do roster desconhecida.");
        Validate(envelope.Body);
        if (!string.Equals(Hash(envelope.Body), envelope.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Checksum do roster inválido.");
        return envelope.Body;
    }

    private static string Hash(RosterPayload x) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(x, Json))).ToLowerInvariant();

    private static void Validate(RosterPayload s)
    {
        if (s.Revision < 0 || s.Agents is null || s.Teams is null)
            throw new InvalidDataException("Roster sem cabeçalho.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var perProject = s.Agents.GroupBy(a => a.ProjectId, StringComparer.Ordinal);
        if (perProject.Any(g => g.Count() > MaxAgents)
            || s.Teams.GroupBy(t => t.ProjectId, StringComparer.Ordinal).Any(g => g.Count() > MaxTeams))
            throw new InvalidDataException("Limite de agentes/equipes inválido.");
        foreach (var a in s.Agents)
        {
            if (a is null || !IsId(a.Id) || !IsId(a.ProjectId) || !ids.Add(a.Id)
                || !IsText(a.Name, 80) || !IsText(a.Instructions, MaxInstructions))
                throw new InvalidDataException("Perfil de agente inválido.");
        }
        foreach (var t in s.Teams)
        {
            if (t is null || !IsId(t.Id) || !IsId(t.ProjectId) || !ids.Add(t.Id)
                || !IsText(t.Name, 80) || t.ProducerId == t.ReviewerId
                || !s.Agents.Any(a => a.Id == t.ProducerId && a.ProjectId == t.ProjectId)
                || !s.Agents.Any(a => a.Id == t.ReviewerId && a.ProjectId == t.ProjectId))
                throw new InvalidDataException("Equipe inválida: revisor independente obrigatório.");
        }
    }

    private void CheckRoot()
    {
        Directory.CreateDirectory(_root);
        for (DirectoryInfo? p = new(_root); p is not null; p = p.Parent)
            if (p.LinkTarget is not null) throw new IOException("Roster não aceita diretórios simbólicos.");
    }

    private static void CheckFile(string name)
    {
        var file = new FileInfo(name);
        if (file.LinkTarget is not null || (file.Exists && file.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            throw new IOException("Roster não aceita arquivo simbólico.");
    }

    private static bool IsId(string value) => Guid.TryParseExact(value, "N", out _);
    private static bool IsText(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= max && value.IndexOf('\0') < 0;
    private static void CheckId(string value) { if (!IsId(value)) throw new ArgumentException("ID inválido."); }
    private static void CheckText(string value, int max)
    {
        if (!IsText(value, max)) throw new ArgumentException("Texto vazio ou acima do limite " + max + ".");
    }
}
