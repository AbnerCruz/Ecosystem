using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcosystemAi.ProjectStore;

/// <summary>Dados de Product explicitamente persistidos. Não são logs internos do agente nem segredos.</summary>
public sealed record ProjectEntry(string Id, string Name, string WorkspaceDirectory,
    DateTimeOffset CreatedAt, IReadOnlyList<SessionEntry> Sessions);

public sealed record SessionEntry(string Id, string Title, DateTimeOffset CreatedAt,
    IReadOnlyList<ConversationTurn> Turns, IReadOnlyList<RunReceipt> Runs);

public sealed record ConversationTurn(string Role, string Text, DateTimeOffset At);

public sealed record RunReceipt(string RunId, string Status, long CostMinor, string Currency,
    bool Verified, string? Verification, DateTimeOffset At);

public sealed record ProjectCatalog(long Revision, IReadOnlyList<ProjectEntry> Projects);

internal sealed record CatalogPayload(int SchemaVersion, long Revision, IReadOnlyList<ProjectEntry> Projects);
internal sealed record CatalogEnvelope(CatalogPayload Body, string Sha256);

/// <summary>
/// Catálogo offline explícito do Product Ecosystem AI: revisões e checksum; escrita em arquivo temporário
/// no mesmo diretório seguida de rename atômico. Sem dependência de AgentRuntime.Core ou provider.
/// Não armazena credenciais. Conteúdo de chat fica em claro no diretório escolhido: o Host decide
/// se habilita a função e como proteger a pasta local.
/// </summary>
public sealed class LocalProjectStore
{
    public const int SchemaVersion = 1;
    public const int MaxCatalogBytes = 4 * 1024 * 1024;
    private const int MaxProjects = 100;
    private const int MaxSessions = 100;
    private const int MaxTurns = 1000;
    private const int MaxText = 16 * 1024;

    private readonly string _root;
    private readonly string _catalog;
    private readonly string _lock;
    private readonly Func<DateTimeOffset> _clock;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false
    };

    public LocalProjectStore(string directory, Func<DateTimeOffset>? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _root = Path.GetFullPath(directory);
        _catalog = Path.Combine(_root, "catalog.json");
        _lock = Path.Combine(_root, ".writer.lock");
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        EnsureRoot();
    }

    public ProjectCatalog Read()
    {
        var current = Load();
        // Não entregar mutabilidade interna ao cliente: snapshots sempre são reconstruídos ao ler.
        return new ProjectCatalog(current.Revision, current.Projects.Select(p => p with
        {
            Sessions = p.Sessions.Select(s => s with { Turns = s.Turns.ToArray(), Runs = s.Runs.ToArray() }).ToArray()
        }).ToArray());
    }

    public ProjectEntry CreateProject(string name, string workspaceDirectory)
    {
        CheckText(name, nameof(name), 120);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);
        var workspace = Path.GetFullPath(workspaceDirectory);
        if (!Directory.Exists(workspace)) throw new DirectoryNotFoundException("O workspace vinculado precisa existir.");
        return Change(state =>
        {
            if (state.Projects.Count >= MaxProjects) throw new InvalidOperationException("Limite de projetos atingido.");
            var project = new ProjectEntry(NewId(), name.Trim(), workspace, _clock(), []);
            return (state with { Projects = state.Projects.Append(project).ToArray() }, project);
        });
    }

    public SessionEntry CreateSession(string projectId, string title)
    {
        CheckId(projectId);
        CheckText(title, nameof(title), 120);
        return Change(state =>
        {
            var project = FindProject(state, projectId);
            if (project.Sessions.Count >= MaxSessions) throw new InvalidOperationException("Limite de sessões atingido.");
            var session = new SessionEntry(NewId(), title.Trim(), _clock(), [], []);
            var updated = project with { Sessions = project.Sessions.Append(session).ToArray() };
            return (ReplaceProject(state, updated), session);
        });
    }

    public void AppendTurn(string projectId, string sessionId, string role, string text)
    {
        CheckId(projectId); CheckId(sessionId);
        if (role is not ("user" or "assistant")) throw new ArgumentException("Papel da conversa inválido.", nameof(role));
        CheckText(text, nameof(text), MaxText);
        Change(state =>
        {
            var project = FindProject(state, projectId);
            var session = FindSession(project, sessionId);
            if (session.Turns.Count >= MaxTurns) throw new InvalidOperationException("Limite de mensagens atingido.");
            var changed = session with { Turns = session.Turns.Append(new ConversationTurn(role, text, _clock())).ToArray() };
            return (ReplaceProject(state, ReplaceSession(project, changed)), true);
        });
    }

    public void AppendRun(string projectId, string sessionId, RunReceipt receipt)
    {
        CheckId(projectId); CheckId(sessionId);
        ArgumentNullException.ThrowIfNull(receipt);
        CheckReceipt(receipt);
        Change(state =>
        {
            var project = FindProject(state, projectId);
            var session = FindSession(project, sessionId);
            if (session.Runs.Any(r => r.RunId == receipt.RunId))
                throw new InvalidOperationException("RunId duplicado na sessão: não registrar duas vezes.");
            if (session.Runs.Count >= MaxTurns) throw new InvalidOperationException("Limite de runs atingido.");
            var changed = session with { Runs = session.Runs.Append(receipt).ToArray() };
            return (ReplaceProject(state, ReplaceSession(project, changed)), true);
        });
    }

    private T Change<T>(Func<CatalogPayload, (CatalogPayload Next, T Value)> operation)
    {
        EnsureRoot();
        EnsureNoLink(_lock);
        FileStream writer;
        try { writer = new FileStream(_lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException e) { throw new IOException("Catálogo ocupado por outro escritor; operação recusada.", e); }
        using (writer)
        {
            var before = Load();
            var (candidate, output) = operation(before);
            candidate = candidate with { Revision = checked(before.Revision + 1) };
            Validate(candidate);
            Persist(candidate);
            return output;
        }
    }

    private CatalogPayload Load()
    {
        EnsureNoLink(_catalog);
        if (!File.Exists(_catalog)) return new CatalogPayload(SchemaVersion, 0, []);
        using var stream = new FileStream(_catalog, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaxCatalogBytes || stream.Length == 0)
            throw new InvalidDataException("Catálogo ausente de conteúdo ou acima do limite.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        CatalogEnvelope envelope;
        try { envelope = JsonSerializer.Deserialize<CatalogEnvelope>(bytes, Json)
            ?? throw new JsonException("Envelope vazio."); }
        catch (JsonException e) { throw new InvalidDataException("Catálogo JSON inválido; nenhum dado foi sobrescrito.", e); }
        if (envelope.Body is null) throw new InvalidDataException("Catálogo sem conteúdo.");
        if (envelope.Body.SchemaVersion != SchemaVersion)
            throw new InvalidDataException("Versão de catálogo desconhecida; não é permitido modificar os dados.");
        Validate(envelope.Body);
        var actual = ComputeHash(envelope.Body);
        if (!string.Equals(actual, envelope.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Integridade do catálogo inválida; nenhum dado foi sobrescrito.");
        return envelope.Body;
    }

    private void Persist(CatalogPayload candidate)
    {
        EnsureNoLink(_catalog);
        var envelope = new CatalogEnvelope(candidate, ComputeHash(candidate));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, Json);
        if (bytes.Length > MaxCatalogBytes)
            throw new InvalidOperationException("Limite de tamanho do catálogo atingido; operação não persistida.");
        var temporary = Path.Combine(_root, ".catalog-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var writer = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                writer.Write(bytes);
                writer.Flush(flushToDisk: true);
            }
            // Renomeação dentro do MESMO volume. Um crash não deixa o JSON canônico parcialmente escrito.
            File.Move(temporary, _catalog, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private void EnsureRoot()
    {
        Directory.CreateDirectory(_root);
        if (new DirectoryInfo(_root).LinkTarget is not null)
            throw new IOException("Diretório do catálogo não pode ser symlink.");
    }

    private static void EnsureNoLink(string path)
    {
        var info = new FileInfo(path);
        if (info.LinkTarget is not null || (File.Exists(path) && info.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            throw new IOException("Arquivo de catálogo/lock não pode ser symlink.");
    }

    private static CatalogPayload ReplaceProject(CatalogPayload state, ProjectEntry project) => state with
    {
        Projects = state.Projects.Select(x => x.Id == project.Id ? project : x).ToArray()
    };

    private static ProjectEntry ReplaceSession(ProjectEntry project, SessionEntry session) => project with
    {
        Sessions = project.Sessions.Select(s => s.Id == session.Id ? session : s).ToArray()
    };

    private static ProjectEntry FindProject(CatalogPayload state, string id) =>
        state.Projects.FirstOrDefault(p => p.Id == id)
        ?? throw new KeyNotFoundException("Projeto não encontrado no catálogo.");

    private static SessionEntry FindSession(ProjectEntry project, string id) =>
        project.Sessions.FirstOrDefault(s => s.Id == id)
        ?? throw new KeyNotFoundException("Sessão não encontrada no projeto.");

    private static string ComputeHash(CatalogPayload body) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(body, Json))).ToLowerInvariant();

    private static void CheckText(string? value, string name, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.IndexOf('\0') >= 0)
            throw new ArgumentException($"Campo {name} vazio ou acima de {max} caracteres.", name);
    }

    private static string NewId() => Guid.NewGuid().ToString("N");

    private static void CheckId(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("ID inválido.", nameof(id));
    }

    private static void CheckReceipt(RunReceipt r)
    {
        CheckText(r.RunId, nameof(r.RunId), 120);
        if (r.Status is not ("succeeded" or "failed" or "blocked" or "cancelled"))
            throw new ArgumentException("Status de run inválido.", nameof(r));
        if (r.CostMinor < 0 || r.Currency.Length != 3 || r.Currency.Any(c => c is < 'A' or > 'Z'))
            throw new ArgumentException("Custo ou moeda inválidos.", nameof(r));
        if (r.Verified && r.Status != "succeeded") throw new ArgumentException("Run não concluído não pode ser verificado.", nameof(r));
        if (r.Verification is { Length: > MaxText }) throw new ArgumentException("Evidência longa demais.", nameof(r));
    }

    private static void Validate(CatalogPayload state)
    {
        if (state.Revision < 0 || state.Projects is null || state.Projects.Count > MaxProjects)
            throw new InvalidDataException("Cabeçalho de catálogo inválido.");
        var projectIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in state.Projects)
        {
            if (!Guid.TryParseExact(project.Id, "N", out _) || !projectIds.Add(project.Id)
                || string.IsNullOrWhiteSpace(project.Name) || project.Name.Length > 120
                || string.IsNullOrWhiteSpace(project.WorkspaceDirectory) || !Path.IsPathFullyQualified(project.WorkspaceDirectory)
                || project.Sessions is null || project.Sessions.Count > MaxSessions)
                throw new InvalidDataException("Projeto inválido ou duplicado.");
            var sessionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var session in project.Sessions)
            {
                if (!Guid.TryParseExact(session.Id, "N", out _) || !sessionIds.Add(session.Id)
                    || string.IsNullOrWhiteSpace(session.Title) || session.Title.Length > 120
                    || session.Turns is null || session.Runs is null || session.Turns.Count > MaxTurns || session.Runs.Count > MaxTurns)
                    throw new InvalidDataException("Sessão inválida ou duplicada.");
                foreach (var turn in session.Turns)
                    if (turn is null || turn.Role is not ("user" or "assistant") ||
                        string.IsNullOrWhiteSpace(turn.Text) || turn.Text.Length > MaxText)
                        throw new InvalidDataException("Mensagem inválida.");
                var runs = new HashSet<string>(StringComparer.Ordinal);
                foreach (var run in session.Runs)
                {
                    try { CheckReceipt(run); }
                    catch (ArgumentException e) { throw new InvalidDataException("Registro de run inválido.", e); }
                    if (!runs.Add(run.RunId)) throw new InvalidDataException("Run duplicado.");
                }
            }
        }
    }
}
