using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Urbe.Core;

/// <summary>Host IO and exclusive writer coordination; paths are relative to its already selected vault.</summary>
public interface IImportStorage
{
    ValueTask<IAsyncDisposable> AcquireExclusiveAsync(CancellationToken cancellationToken);
    ValueTask<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);
    ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string path, CancellationToken cancellationToken);
    ValueTask WriteAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken);
    ValueTask RemoveAsync(string path, CancellationToken cancellationToken);
}

/// <summary>Durable, binary-safe import transaction and restart recovery shared by future C# hosts.</summary>
public sealed class ImportTransaction(IImportStorage storage) : IImportTransactionStore
{
    public const string JournalPath = ".urbe/import.v1.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    public static string Revision(IEnumerable<VaultFile> files) => Hash(Encoding.UTF8.GetBytes(string.Join('\n', files.OrderBy(f => f.Path, StringComparer.Ordinal).Select(f => f.Path + "\0" + Hash(f.Bytes.Span)))));

    public async ValueTask<ImportResult> CommitAsync(ImportPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Conflicts.Count > 0) throw new InvalidOperationException("Resolva os conflitos antes de importar.");
        await using var held = await storage.AcquireExclusiveAsync(cancellationToken);
        await RecoverLockedAsync(cancellationToken);
        var current = new Dictionary<string, VaultFile>(StringComparer.OrdinalIgnoreCase);
        var limits = new ImportLimits(); long total = 0;
        foreach (var path in await storage.ListAsync(cancellationToken))
        {
            Safe(path);
            var bytes = await storage.ReadAsync(path, cancellationToken) ?? throw new IOException("Não foi possível ler " + path);
            total += bytes.Length;
            if (bytes.Length > limits.FileBytes || total > limits.TotalBytes || current.Count >= limits.FileCount)
                throw new InvalidDataException("O vault atual excede o limite seguro desta operação. Nenhum dado foi alterado.");
            current.Add(path, new VaultFile(path, bytes.ToArray()));
        }
        if (Revision(current.Values) != plan.ExpectedRevision) throw new InvalidOperationException("O vault mudou. Confira a importação novamente.");
        if (plan.Operations.Count == 0) return new(0, 0, plan.Unchanged);
        var root = ".urbe/backup/import-" + Guid.NewGuid().ToString("D");
        var entries = new List<RecoveryEntry>();
        foreach (var operation in plan.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Safe(operation.Path);
            if (operation.Path == JournalPath || (operation.Path.StartsWith(".urbe/backup/", StringComparison.OrdinalIgnoreCase) && current.ContainsKey(operation.Path)))
                throw new InvalidDataException("Registro de recuperação e backups existentes estão protegidos.");
            var entry = new RecoveryEntry { Path = operation.Path };
            if (current.TryGetValue(operation.Path, out var original))
            {
                entry.Backup = root + "/files/" + operation.Path;
                entry.Size = original.Bytes.Length;
                entry.Sha256 = Hash(original.Bytes.Span);
                await storage.WriteAsync(entry.Backup, original.Bytes, cancellationToken);
                await VerifyAsync(entry.Backup, original.Bytes, cancellationToken);
            }
            entries.Add(entry);
        }
        var journal = new RecoveryJournal { Version = 1, Root = root, Entries = entries };
        var journalBytes = JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions);
        var backup = JsonSerializer.SerializeToUtf8Bytes(new { version = 2, kind = "urbe-import-backup", journal }, JsonOptions);
        await storage.WriteAsync(root + "/manifest.json", backup, cancellationToken);
        await storage.WriteAsync(JournalPath, journalBytes, cancellationToken);
        await VerifyAsync(JournalPath, journalBytes, cancellationToken);
        try
        {
            int written = 0, removed = 0;
            foreach (var operation in plan.Operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (operation.Kind == VaultOperationKind.Remove) { await storage.RemoveAsync(operation.Path, cancellationToken); removed++; }
                else { await storage.WriteAsync(operation.Path, operation.Bytes!.Value, cancellationToken); await VerifyAsync(operation.Path, operation.Bytes.Value, cancellationToken); written++; }
            }
            // Removal is the commit point. Before it, a reopening deterministically rolls back.
            await storage.RemoveAsync(JournalPath, cancellationToken);
            return new(written, removed, plan.Unchanged);
        }
        catch
        {
            // Cancellation does not cancel rollback. If IO still fails, keep the journal for reopening.
            await RecoverLockedAsync(CancellationToken.None);
            throw;
        }
    }

    public async ValueTask<bool> RecoverAsync(CancellationToken cancellationToken = default)
    {
        await using var held = await storage.AcquireExclusiveAsync(cancellationToken);
        return await RecoverLockedAsync(cancellationToken);
    }
    private async ValueTask<bool> RecoverLockedAsync(CancellationToken token)
    {
        var bytes = await storage.ReadAsync(JournalPath, token);
        if (bytes is null) return false;
        RecoveryJournal journal;
        try { journal = JsonSerializer.Deserialize<RecoveryJournal>(bytes.Value.Span, JsonOptions) ?? throw new InvalidDataException(); }
        catch (JsonException e) { throw new InvalidDataException("Recuperação ilegível. O vault permanece protegido.", e); }
        if (journal.Version != 1 || journal.Entries is null || journal.Root is null ||
            !journal.Root.StartsWith(".urbe/backup/import-", StringComparison.Ordinal) || !Guid.TryParseExact(journal.Root[20..], "D", out _))
            throw new InvalidDataException("Recuperação não reconhecida. O vault permanece protegido.");
        var recovered = new List<(string Path, ReadOnlyMemory<byte>? Bytes)>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Validate every backup before the first recovery write.
        foreach (var entry in journal.Entries)
        {
            Safe(entry.Path);
            if (entry.Path == JournalPath || entry.Path.StartsWith(journal.Root + "/", StringComparison.OrdinalIgnoreCase) || !names.Add(entry.Path))
                throw new InvalidDataException("Caminho inválido na recuperação.");
            ReadOnlyMemory<byte>? original = null;
            if (entry.Backup is not null)
            {
                if (entry.Backup != journal.Root + "/files/" + entry.Path) throw new InvalidDataException("Caminho de backup inválido.");
                original = await storage.ReadAsync(entry.Backup, token) ?? throw new IOException("Backup ausente. O vault permanece protegido.");
                if (original.Value.Length != entry.Size || Hash(original.Value.Span) != entry.Sha256) throw new InvalidDataException("Backup incompleto. O vault permanece protegido.");
            }
            recovered.Add((entry.Path, original));
        }
        foreach (var entry in recovered)
        {
            if (entry.Bytes is null) await storage.RemoveAsync(entry.Path, token);
            else { await storage.WriteAsync(entry.Path, entry.Bytes.Value, token); await VerifyAsync(entry.Path, entry.Bytes.Value, token); }
        }
        await storage.RemoveAsync(JournalPath, token);
        return true;
    }
    private async ValueTask VerifyAsync(string path, ReadOnlyMemory<byte> expected, CancellationToken token)
    {
        var actual = await storage.ReadAsync(path, token);
        if (actual is null || !actual.Value.Span.SequenceEqual(expected.Span)) throw new IOException("Não foi possível conferir a gravação de " + path);
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Safe(string path)
    {
        if (path.Contains('\\') || path.Contains(':') || path.Any(c => c < 32 || c == 127)) throw new InvalidDataException("Nome de arquivo inválido.");
        _ = VaultArchive.NormalizeArchivePath(path);
    }
    private sealed class RecoveryJournal { public int Version { get; set; } public string? Root { get; set; } public List<RecoveryEntry>? Entries { get; set; } }
    private sealed class RecoveryEntry { public string Path { get; set; } = ""; public string? Backup { get; set; } public int Size { get; set; } public string? Sha256 { get; set; } }
}
