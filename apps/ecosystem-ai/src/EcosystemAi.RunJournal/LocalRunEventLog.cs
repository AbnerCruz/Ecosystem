using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentRuntime;

namespace EcosystemAi.RunJournal;

internal sealed record RunJournalBody(int SchemaVersion, string RunId, IReadOnlyList<string> Events);
internal sealed record RunJournalEnvelope(RunJournalBody Body, string Sha256);

/// <summary>
/// Implementação local da porta IEventLog do Agent Runtime para o Product Ecosystem AI.
/// Cada run tem arquivo versionado, verificado por checksum, gravado via temp+rename.
/// O Runtime continua sendo a única autoridade para estados, replay, segurança e autorização.
/// </summary>
public sealed class LocalRunEventLog : IEventLog
{
    public const int SchemaVersion = 1;
    public const int MaxJournalBytes = 8 * 1024 * 1024;
    public const int MaxEventsPerRun = 10000;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private readonly string _root;
    private readonly string _lockPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalRunEventLog(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _root = Path.GetFullPath(rootDirectory);
        _lockPath = Path.Combine(_root, ".writer.lock");
        EnsureRoot();
    }

    public async ValueTask AppendAsync(RuntimeEvent entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateRunId(entry.RunId);
        var contractErrors = EventContract.Validate(entry);
        if (contractErrors.Count != 0) throw new InvalidDataException(
            "Evento fora do contrato: " + string.Join("; ", contractErrors));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            EnsureRoot();
            EnsureNoSymlink(_lockPath);
            FileStream locked;
            try { locked = new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException e) { throw new IOException("RunJournal ocupado por outro escritor.", e); }
            using (locked)
            {
                var old = Load(entry.RunId);
                if (entry.Sequence != old.Count + 1)
                    throw new InvalidDataException($"Evento fora de ordem: esperado {old.Count + 1}.");
                var current = old.Append(entry).ToArray();
                // O Core determina a máquina de estados, inclusive proibição de eventos após terminal.
                try { _ = RunState.Replay(current); }
                catch (InvalidEventLogException e) { throw new InvalidDataException("Sequência de eventos inválida.", e); }

                if (current.Length > MaxEventsPerRun) throw new InvalidOperationException("Limite de eventos do run atingido.");
                var lines = current.Select(EventSerializer.ToLine).ToArray();
                var body = new RunJournalBody(SchemaVersion, entry.RunId, lines);
                await WriteAsync(entry.RunId, body, cancellationToken);
            }
        }
        finally { _gate.Release(); }
    }

    public ValueTask<IReadOnlyList<RuntimeEvent>> ReadRunAsync(string runId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateRunId(runId);
        return ValueTask.FromResult<IReadOnlyList<RuntimeEvent>>(Load(runId).ToArray());
    }

    private IReadOnlyList<RuntimeEvent> Load(string runId)
    {
        var path = FileFor(runId);
        EnsureNoSymlink(path);
        if (!File.Exists(path)) return [];
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length == 0 || stream.Length > MaxJournalBytes)
            throw new InvalidDataException("Journal vazio ou acima do limite.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        RunJournalEnvelope envelope;
        try { envelope = JsonSerializer.Deserialize<RunJournalEnvelope>(bytes, Json)
            ?? throw new JsonException("Envelope vazio."); }
        catch (JsonException e) { throw new InvalidDataException("Envelope de run inválido; nenhum dado alterado.", e); }
        if (envelope.Body is null) throw new InvalidDataException("Journal sem body.");
        if (envelope.Body.SchemaVersion != SchemaVersion)
            throw new InvalidDataException("Versão futura/desconhecida; recusa leitura e sobrescrita.");
        if (envelope.Body.RunId != runId || envelope.Body.Events is null || envelope.Body.Events.Count == 0
            || envelope.Body.Events.Count > MaxEventsPerRun)
            throw new InvalidDataException("Metadados de run incorretos.");
        if (!string.Equals(envelope.Sha256, Hash(envelope.Body), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Checksum de eventos incompatível; não recuperável automaticamente.");

        var events = new List<RuntimeEvent>();
        foreach (var line in envelope.Body.Events)
        {
            if (string.IsNullOrWhiteSpace(line)) throw new InvalidDataException("Evento vazio.");
            RuntimeEvent item;
            try { item = EventSerializer.FromLine(line); }
            catch (JsonException e) { throw new InvalidDataException("Evento JSON inválido.", e); }
            if (item.RunId != runId || item.Sequence != events.Count + 1 || EventContract.Validate(item).Count != 0)
                throw new InvalidDataException("Contrato ou ordem do journal violados.");
            events.Add(item);
        }
        try { _ = RunState.Replay(events); }
        catch (InvalidEventLogException e) { throw new InvalidDataException("Histórico de run inválido.", e); }
        return events;
    }

    private async Task WriteAsync(string runId, RunJournalBody body, CancellationToken token)
    {
        var envelope = new RunJournalEnvelope(body, Hash(body));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, Json);
        if (bytes.Length > MaxJournalBytes) throw new InvalidOperationException("Journal excedeu limite de tamanho.");
        var temp = Path.Combine(_root, ".run-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.WriteAsync(bytes, token);
                stream.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            var destination = FileFor(runId);
            EnsureNoSymlink(destination);
            File.Move(temp, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private string FileFor(string runId)
    {
        ValidateRunId(runId);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(runId))).ToLowerInvariant();
        return Path.Combine(_root, id + ".json");
    }

    private void EnsureRoot()
    {
        Directory.CreateDirectory(_root);
        if (new DirectoryInfo(_root).LinkTarget is not null)
            throw new IOException("RunJournal root não pode ser symlink.");
    }

    private static void EnsureNoSymlink(string path)
    {
        var info = new FileInfo(path);
        if (info.LinkTarget is not null
            || (File.Exists(path) && info.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            throw new IOException("RunJournal recusa symlink no arquivo ou lock.");
    }

    private static string Hash(RunJournalBody body) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(body, Json))).ToLowerInvariant();

    private static void ValidateRunId(string? runId)
    {
        if (string.IsNullOrWhiteSpace(runId) || runId.Length > 120 || runId.Any(char.IsControl))
            throw new ArgumentException("RunId vazio, longo demais ou inválido.", nameof(runId));
    }
}
