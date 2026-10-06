using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace Urbe.Core;

public enum ImportKind { Documents, HistoricalVault, CurrentVault, Export, Backup }
public enum ImportMode { Incorporate, Restore, RestoreSubset }
public enum ImportConflictChoice { KeepCurrent, Replace }

/// <summary>Host-owned document access. The Core never interprets a URI as a path.</summary>
public sealed record ImportSource(string RelativePath, Func<CancellationToken, ValueTask<Stream>> OpenRead);
public sealed record ImportLimits(long TotalBytes = 256L * 1024 * 1024, long FileBytes = 64L * 1024 * 1024, int FileCount = 10000);
public sealed record ImportSelection(IReadOnlyList<ImportSource> Sources, bool IsDirectory = false);
public sealed record ImportConflict(string Path);
public sealed record ImportSourceConflict(string Path, IReadOnlyList<VaultFile> Candidates);
public sealed record ImportInspection(ImportKind Kind, IReadOnlyDictionary<string, VaultFile> Files,
    VaultSnapshot Snapshot, int DocumentCount, int AssetCount, VaultExportManifest? Manifest, IReadOnlyList<string>? AbsentPaths = null, IReadOnlyList<ImportSourceConflict>? SourceConflicts = null);
public sealed record ImportPlan(ImportInspection Inspection, ImportMode Mode,
    IReadOnlyList<VaultOperation> Operations, IReadOnlyList<ImportConflict> Conflicts, string ExpectedRevision, int Unchanged);
public sealed record ImportResult(int Written, int Removed, int Unchanged);

/// <summary>
/// A host must supply an atomic, durable transaction, with crash recovery and a backup before replacement.
/// Commit checks the revision again while holding its write lock; partial changes must never become visible.
/// No mock/no-op implementation is supplied by the Core.
/// </summary>
public interface IImportTransactionStore
{
    ValueTask<ImportResult> CommitAsync(ImportPlan plan, CancellationToken cancellationToken);
}

/// <summary>Portable selection → inspection → plan. No UI, permissions or filesystem paths.</summary>
public static class ImportPipeline
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static async ValueTask<ImportInspection> InspectAsync(ImportSelection selection,
        ImportLimits? limits = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        limits ??= new ImportLimits();
        if (limits.FileCount <= 0 || limits.FileBytes <= 0 || limits.TotalBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(limits));
        if (selection.Sources.Count == 0) throw new InvalidDataException("Nenhum arquivo selecionado.");
        if (selection.Sources.Count > limits.FileCount) throw new InvalidDataException("Há arquivos demais nesta seleção.");
        var files = new Dictionary<string, VaultFile>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        var sourceConflicts = new Dictionary<string, List<VaultFile>>(StringComparer.OrdinalIgnoreCase);
        VaultExportManifest? manifest = null;
        foreach (var source in selection.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = SafePath(source.RelativePath);
            await using var stream = await source.OpenRead(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[64 * 1024];
            int length;
            while ((length = await stream.ReadAsync(chunk, cancellationToken)) != 0)
            {
                if (buffer.Length + length > limits.FileBytes || total + length > limits.TotalBytes)
                    throw new InvalidDataException("A seleção excede o limite seguro de importação.");
                await buffer.WriteAsync(chunk.AsMemory(0, length), cancellationToken);
                total += length;
            }
            var bytes = buffer.ToArray();
            // Signature, not extension; a .zip without ZIP content is still an invalid selection.
            bool zip = bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4b &&
                ((bytes[2] == 3 && bytes[3] == 4) || (bytes[2] == 5 && bytes[3] == 6));
            if (zip && !selection.IsDirectory)
            {
                if (selection.Sources.Count != 1)
                    throw new InvalidDataException("Selecione o arquivo compactado separadamente dos demais documentos.");
                var archive = VaultArchive.Import(bytes, limits);
                if (archive.ManifestState == VaultExportManifestState.Corrupt || archive.Manifest is { FormatVersion: not 1 } || archive.Verification is { Ok: false })
                    throw new InvalidDataException("O backup não confere com seu manifesto.");
                manifest = archive.Manifest;
                foreach (var file in archive.Files.Values) Add(file);
            }
            else
            {
                if (!selection.IsDirectory && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("O arquivo ZIP é inválido.");
                Add(new VaultFile(path, bytes));
            }
        }
        if (files.Remove(VaultExportManifest.FileName, out var manifestFile))
        {
            var parsed = VaultExportManifestCodec.Parse(manifestFile.Bytes);
            if (parsed.State != VaultExportManifestState.Current || parsed.Manifest is null || parsed.Manifest.FormatVersion != 1)
                throw new InvalidDataException("Este manifesto não é reconhecido. Atualize o Urbe se o backup for mais recente.");
            manifest = parsed.Manifest;
            foreach (var listed in manifest.Files)
                if (SafePath(listed.Path) != listed.Path) throw new InvalidDataException("Caminho inválido no manifesto.");
            if (!VaultExportManifestCodec.Verify(manifest, files.Values).Ok)
                throw new InvalidDataException("O backup não confere com seu manifesto.");
        }
        var backup = UnpackBackup(ref files, out var absent);
        if (files.Count == 0 && absent.Count == 0) throw new InvalidDataException("Nenhum documento encontrado.");
        foreach (var file in files.Values.Concat(sourceConflicts.Values.SelectMany(c=>c)).Where(f => IsText(f.Path)))
        {
            try { _ = StrictUtf8.GetString(file.Bytes.Span); }
            catch (DecoderFallbackException e) { throw new InvalidDataException("Texto com codificação inválida: " + file.Path, e); }
        }
        var snapshot = VaultReader.Read(files.Values);
        if (snapshot.FormatState is VaultFormatState.Future or VaultFormatState.Corrupt || snapshot.VaultFormatVersion < 1 ||
            snapshot.IsMapReadOnly || snapshot.FutureFiles.Count > 0)
            throw new InvalidDataException("Este vault contém dados mais recentes ou não reconhecidos. Ele foi preservado.");
        foreach (var candidate in files.Values.Concat(sourceConflicts.Values.SelectMany(c => c)))
        {
            if (VaultReader.Read([candidate]).FutureFiles.Count > 0)
                throw new InvalidDataException("Documento de origem contém dados de versão mais recente.");
            if (!new[] { ".page.json", ".block.json", ".theme.json" }.Any(suffix => candidate.Path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))) continue;
            try
            {
                using var json = JsonDocument.Parse(candidate.Bytes);
                if (json.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException("Documento estruturado inválido: " + candidate.Path);
                if (json.RootElement.TryGetProperty("version", out var version) &&
                    (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number > 1))
                    throw new InvalidDataException("Documento de versão mais recente ou não reconhecida: " + candidate.Path);
            }
            catch (JsonException e) { throw new InvalidDataException("Documento estruturado inválido: " + candidate.Path, e); }
        }
        var hasMetadata = files.Keys.Any(p => p.StartsWith(".urbe/", StringComparison.OrdinalIgnoreCase));
        if (hasMetadata && !backup && !files.ContainsKey(".urbe/vault.json") && !files.ContainsKey(".urbe/mapa.json"))
            throw new InvalidDataException("Os metadados selecionados não identificam um vault completo. Selecione a pasta inteira ou seu backup.");
        var kind = backup ? ImportKind.Backup : hasMetadata ? snapshot.FormatState == VaultFormatState.Absent || snapshot.VaultFormatVersion < 2 ? ImportKind.HistoricalVault : ImportKind.CurrentVault
            : manifest is not null ? ImportKind.Export : ImportKind.Documents;
        if (kind != ImportKind.Documents && sourceConflicts.Count > 0) throw new InvalidDataException("A seleção contém versões diferentes de um vault. Selecione uma única cópia para restaurar.");
        var documents = files.Keys.Count(p => !p.StartsWith(".urbe/", StringComparison.OrdinalIgnoreCase) && IsText(p));
        return new ImportInspection(kind, new ReadOnlyDictionary<string, VaultFile>(files), snapshot,
            documents, files.Keys.Count(p => !p.StartsWith(".urbe/", StringComparison.OrdinalIgnoreCase) && !IsText(p)), manifest, absent, sourceConflicts.Select(c => new ImportSourceConflict(c.Key, c.Value.AsReadOnly())).ToArray());

        void Add(VaultFile file)
        {
            var normalized = SafePath(file.Path);
            if (string.Equals(normalized, ImportTransaction.JournalPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("O vault de origem contém uma importação interrompida. Recupere-o antes de importar.");
            if (files.TryGetValue(normalized, out var previous))
            {
                if (!previous.Bytes.Span.SequenceEqual(file.Bytes.Span))
                {
                    if (!sourceConflicts.TryGetValue(normalized, out var candidates)) sourceConflicts[previous.Path] = candidates = [previous];
                    if (!candidates.Any(c => c.Bytes.Span.SequenceEqual(file.Bytes.Span))) candidates.Add(new VaultFile(normalized, file.Bytes.ToArray()));
                }
                return;
            }
            if (files.Count >= limits.FileCount || files.Values.Sum(f => (long)f.Bytes.Length) + file.Bytes.Length > limits.TotalBytes || file.Bytes.Length > limits.FileBytes)
                throw new InvalidDataException("A seleção excede o limite seguro de importação.");
            files.Add(normalized, new VaultFile(normalized, file.Bytes.ToArray()));
        }
    }

    public static ImportInspection ResolveSources(ImportInspection inspection, IReadOnlyDictionary<string, int> choices)
    {
        var files = new Dictionary<string, VaultFile>(inspection.Files, StringComparer.OrdinalIgnoreCase);
        foreach (var conflict in inspection.SourceConflicts ?? [])
        {
            if (!choices.TryGetValue(conflict.Path, out var index) || index < 0 || index >= conflict.Candidates.Count)
                throw new InvalidOperationException("Escolha qual arquivo importar: " + conflict.Path);
            var chosen = conflict.Candidates[index];
            files.Remove(conflict.Path); files.Add(chosen.Path, chosen);
        }
        return inspection with { Files = new ReadOnlyDictionary<string, VaultFile>(files), Snapshot = VaultReader.Read(files.Values), SourceConflicts = [] };
    }

    public static ImportPlan Plan(ImportInspection inspection, VaultSnapshot current, string revision,
        IReadOnlyDictionary<string, ImportConflictChoice>? choices = null)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        ArgumentNullException.ThrowIfNull(current);
        if (inspection.SourceConflicts is { Count: > 0 }) throw new InvalidOperationException("Escolha os arquivos de origem antes de planejar a importação.");
        if (current.IsReadOnly || current.IsMapReadOnly || current.FutureFiles.Count > 0 || current.Files.ContainsKey(ImportTransaction.JournalPath))
            throw new InvalidDataException("O vault atual está protegido. Nenhum dado será alterado.");
        var mode = inspection.Kind == ImportKind.Backup ? ImportMode.RestoreSubset : inspection.Kind is ImportKind.CurrentVault or ImportKind.HistoricalVault or ImportKind.Export
            ? ImportMode.Restore : ImportMode.Incorporate;
        var conflicts = new List<ImportConflict>();
        var operations = new List<VaultOperation>();
        int unchanged = 0;
        foreach (var file in inspection.Files.Values)
        {
            if (current.Files.TryGetValue(file.Path, out var existing))
            {
                if (file.Bytes.Span.SequenceEqual(existing.Bytes.Span)) { unchanged++; continue; }
                if (choices is null || !choices.TryGetValue(file.Path, out var choice)) { conflicts.Add(new(file.Path)); continue; }
                if (mode != ImportMode.Incorporate && choice != ImportConflictChoice.Replace)
                    throw new InvalidDataException("Uma restauração precisa preservar o conjunto completo do vault de origem.");
                if (choice == ImportConflictChoice.KeepCurrent) { unchanged++; continue; }
            }
            operations.Add(new VaultOperation(VaultOperationKind.Write, file.Path, file.Bytes.ToArray()));
        }
        if (mode == ImportMode.Restore)
            foreach (var path in current.Files.Keys.Where(p => !inspection.Files.ContainsKey(p) && !p.StartsWith(".urbe/backup/", StringComparison.OrdinalIgnoreCase)))
            {
                if (choices is null || !choices.TryGetValue(path, out var choice)) conflicts.Add(new(path));
                else if (choice != ImportConflictChoice.Replace) throw new InvalidDataException("Restauração cancelada para preservar o vault atual.");
                else operations.Add(new VaultOperation(VaultOperationKind.Remove, path, null));
            }
        foreach (var path in inspection.AbsentPaths ?? [])
        {
            if (inspection.Files.ContainsKey(path)) throw new InvalidDataException("Backup contraditório.");
            if (!current.Files.ContainsKey(path)) continue;
            if (choices is null || !choices.TryGetValue(path, out var choice)) conflicts.Add(new(path));
            else if (choice != ImportConflictChoice.Replace) throw new InvalidDataException("Restauração cancelada para preservar o vault atual.");
            else operations.Add(new VaultOperation(VaultOperationKind.Remove, path, null));
        }
        return new ImportPlan(inspection, mode, operations.AsReadOnly(), conflicts.AsReadOnly(), revision, unchanged);
    }

    public static ValueTask<ImportResult> ExecuteAsync(ImportPlan plan, IImportTransactionStore store,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(store);
        cancellationToken.ThrowIfCancellationRequested();
        if (plan.Conflicts.Count != 0) throw new InvalidOperationException("Escolha como resolver os conflitos antes de importar.");
        return store.CommitAsync(plan, cancellationToken);
    }

    private static bool UnpackBackup(ref Dictionary<string, VaultFile> files, out IReadOnlyList<string> absent)
    {
        absent = Array.Empty<string>();
        if (!files.TryGetValue("manifest.json", out var file)) return false;
        JsonDocument document;
        try { document = JsonDocument.Parse(file.Bytes); }
        catch (JsonException) { return false; }
        using (document)
        {
            var json = document.RootElement;
            if (json.ValueKind != JsonValueKind.Object) return false;
            bool legacy = json.TryGetProperty("from", out _) && json.TryGetProperty("to", out _) && json.TryGetProperty("files", out var rows) && rows.ValueKind == JsonValueKind.Array && json.TryGetProperty("absent", out var missing) && missing.ValueKind == JsonValueKind.Array;
            bool binary = json.TryGetProperty("kind", out var kind) && kind.GetString() == "urbe-import-backup";
            if (!legacy && !binary) return false;
            var restored = new Dictionary<string, VaultFile>(StringComparer.OrdinalIgnoreCase);
            var removals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (legacy)
                {
                    if (json.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("Backup não reconhecido.");
                    foreach (var entry in json.GetProperty("absent").EnumerateArray()) removals.Add(SafePath(entry.GetString()!));
                    foreach (var entry in json.GetProperty("files").EnumerateArray())
                    {
                        var path = SafePath(entry.GetProperty("path").GetString()!);
                        var mapped = path.StartsWith(".urbe/", StringComparison.Ordinal) ? "urbe/" + path[6..] : path;
                        if (!files.TryGetValue("files/" + mapped, out var content) || StrictUtf8.GetString(content.Bytes.Span).Length != entry.GetProperty("size").GetInt32()) throw new InvalidDataException("Backup incompleto.");
                        if (entry.TryGetProperty("sha256", out var digest) && digest.ValueKind != JsonValueKind.Null && Digest(content.Bytes.Span) != digest.GetString()) throw new InvalidDataException("Backup corrompido.");
                        if (!restored.TryAdd(path, new VaultFile(path, content.Bytes.ToArray()))) throw new InvalidDataException("Caminho duplicado no backup.");
                    }
                }
                else
                {
                    var journal = json.GetProperty("journal");
                    if (json.GetProperty("version").GetInt32() != 2 || journal.GetProperty("version").GetInt32() != 1) throw new InvalidDataException("Backup não reconhecido.");
                    foreach (var entry in journal.GetProperty("entries").EnumerateArray())
                    {
                        var path = SafePath(entry.GetProperty("path").GetString()!);
                        if (!entry.TryGetProperty("backup", out var source) || source.ValueKind == JsonValueKind.Null) { removals.Add(path); continue; }
                        if (!files.TryGetValue("files/" + path, out var content) || content.Bytes.Length != entry.GetProperty("size").GetInt32() || Digest(content.Bytes.Span) != entry.GetProperty("sha256").GetString()) throw new InvalidDataException("Backup incompleto ou corrompido.");
                        if (!restored.TryAdd(path, new VaultFile(path, content.Bytes.ToArray()))) throw new InvalidDataException("Caminho duplicado no backup.");
                    }
                }
            }
            catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or DecoderFallbackException)
            { throw new InvalidDataException("Backup não reconhecido. Nenhum dado foi alterado.", e); }
            if (removals.Any(restored.ContainsKey) || removals.Contains(ImportTransaction.JournalPath) || restored.ContainsKey(ImportTransaction.JournalPath)) throw new InvalidDataException("Backup contraditório ou com importação pendente.");
            files = restored;
            absent = Array.AsReadOnly(removals.ToArray());
            return true;
        }
    }
    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string SafePath(string path)
    {
        if (path.Contains('\\') || path.Contains(':') || path.Any(c => c < 32 || c == 127))
            throw new InvalidDataException("Nome de arquivo inválido.");
        return VaultArchive.NormalizeArchivePath(path);
    }
    private static bool IsText(string path) => ArtifactModel.IsText(path);
}
