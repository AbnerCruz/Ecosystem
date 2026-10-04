using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

public sealed record VaultGcHistoryItem(string Id, int Revisions, long? Last);
public sealed record VaultGcTrashItem(string Id, string? Path, long? DeletedAt);
public sealed record VaultGcCompositionItem(string Id, string? Name, IReadOnlyList<string> Sources);

public sealed class VaultGcPlan
{
    internal VaultGcPlan(
        IReadOnlyList<VaultGcHistoryItem> history,
        IReadOnlyList<VaultGcTrashItem> trash,
        IReadOnlyList<VaultGcCompositionItem> compositions)
    {
        History = history;
        Trash = trash;
        Compositions = compositions;
    }

    public IReadOnlyList<VaultGcHistoryItem> History { get; }
    public IReadOnlyList<VaultGcTrashItem> Trash { get; }
    public IReadOnlyList<VaultGcCompositionItem> Compositions { get; }
    public int HistoryCount => History.Count;
    public int TrashCount => Trash.Count;
    public int CompositionReferenceCount => Compositions.Sum(item => item.Sources.Count);
}

public sealed class VaultGcResult
{
    internal VaultGcResult(bool dryRun, VaultGcPlan plan, VaultWriteResult? write)
    {
        DryRun = dryRun;
        Plan = plan;
        Write = write;
    }

    public bool DryRun { get; }
    public VaultGcPlan Plan { get; }
    public VaultWriteResult? Write { get; }
}

public static class VaultGarbageCollector
{
    private const long DayMilliseconds = 86_400_000L;

    public static VaultGcPlan Plan(
        IEnumerable<VaultFile> files,
        DateTimeOffset now,
        int orphanDays = 30,
        int? trashDays = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        var current = VaultWriter.CloneFiles(files);
        var snapshot = VaultReader.Read(current.Values);
        var live = new HashSet<string>(snapshot.Documents.Select(document => document.Id), StringComparer.Ordinal);

        var history = ReadSidecar(current, "history");
        var trash = ReadSidecar(current, "trash");
        var compositions = ReadSidecar(current, "compositions");

        var trashedIds = new HashSet<string>(StringComparer.Ordinal);
        if (trash["items"] is JsonArray trashItems)
        {
            foreach (var node in trashItems)
            {
                if (TryTrashId(node, out var id))
                    trashedIds.Add(id);
            }
        }

        bool Known(string id) => live.Contains(id) || trashedIds.Contains(id);

        var historyOut = new List<VaultGcHistoryItem>();
        if (history["documents"] is JsonObject historyDocs)
        {
            foreach (var pair in historyDocs)
            {
                if (Known(pair.Key) || pair.Value is not JsonArray revisions)
                    continue;

                long last = 0;
                foreach (var revision in revisions)
                {
                    if (revision is not JsonObject obj)
                        continue;
                    if (TryLong(obj["timestamp"], out var timestamp))
                        last = Math.Max(last, timestamp);
                }

                if (now.ToUnixTimeMilliseconds() - last >= (long)orphanDays * DayMilliseconds)
                    historyOut.Add(new VaultGcHistoryItem(pair.Key, revisions.Count, last == 0 ? null : last));
            }
        }

        var trashOut = new List<VaultGcTrashItem>();
        if (trashDays is >= 0 && trash["items"] is JsonArray expiringTrash)
        {
            foreach (var node in expiringTrash)
            {
                if (node is not JsonObject item || !TryTrashId(item, out var id))
                    continue;

                var deletedAt = TryLong(item["deletedAt"], out var deleted) ? deleted : 0;
                if (now.ToUnixTimeMilliseconds() - deletedAt < (long)trashDays.Value * DayMilliseconds)
                    continue;

                var path = item["originalPath"]?.GetValue<string>();
                if (path is null &&
                    item["document"] is JsonObject document &&
                    document["path"] is JsonValue pathValue &&
                    pathValue.TryGetValue<string>(out var documentPath))
                    path = documentPath;

                trashOut.Add(new VaultGcTrashItem(id, path, deletedAt == 0 ? null : deletedAt));
            }
        }

        var compositionOut = new List<VaultGcCompositionItem>();
        if (compositions["items"] is JsonArray compositionItems)
        {
            foreach (var node in compositionItems)
            {
                if (node is not JsonObject composition)
                    continue;

                var id = composition["id"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var gone = new List<string>();
                if (composition["sources"] is JsonArray sources)
                {
                    foreach (var source in sources)
                    {
                        var value = source?.GetValue<string>();
                        if (value is not null && !Known(value))
                            gone.Add(value);
                    }
                }

                if (gone.Count > 0)
                    compositionOut.Add(new VaultGcCompositionItem(
                        id,
                        composition["name"]?.GetValue<string>(),
                        gone.AsReadOnly()));
            }
        }

        return new VaultGcPlan(
            historyOut.AsReadOnly(),
            trashOut.AsReadOnly(),
            compositionOut.AsReadOnly());
    }

    public static VaultGcResult Run(
        IEnumerable<VaultFile> files,
        DateTimeOffset now,
        bool dryRun = true,
        int orphanDays = 30,
        int? trashDays = null,
        string appVersion = "?")
    {
        var source = files.ToArray();
        var plan = Plan(source, now, orphanDays, trashDays);
        if (dryRun)
            return new VaultGcResult(true, plan, null);

        var snapshot = VaultReader.Read(source);
        if (snapshot.IsReadOnly)
            throw new InvalidOperationException("Vault somente leitura: nada foi alterado.");

        var current = VaultWriter.CloneFiles(source);
        var history = ReadSidecar(current, "history");
        var trash = ReadSidecar(current, "trash");
        var compositions = ReadSidecar(current, "compositions");

        if (history["documents"] is JsonObject historyDocs)
            foreach (var item in plan.History)
                historyDocs.Remove(item.Id);

        if (trash["items"] is JsonArray trashItems)
        {
            var remove = new HashSet<string>(plan.Trash.Select(item => item.Id), StringComparer.Ordinal);
            for (var i = trashItems.Count - 1; i >= 0; i--)
            {
                if (TryTrashId(trashItems[i], out var id) && remove.Contains(id))
                    trashItems.RemoveAt(i);
            }
        }

        if (compositions["items"] is JsonArray compositionItems)
        {
            var drops = plan.Compositions.ToDictionary(
                item => item.Id,
                item => new HashSet<string>(item.Sources, StringComparer.Ordinal),
                StringComparer.Ordinal);

            foreach (var node in compositionItems)
            {
                if (node is not JsonObject composition)
                    continue;
                var id = composition["id"]?.GetValue<string>();
                if (id is null || !drops.TryGetValue(id, out var drop) ||
                    composition["sources"] is not JsonArray sources)
                    continue;

                for (var i = sources.Count - 1; i >= 0; i--)
                {
                    var sourceId = sources[i]?.GetValue<string>();
                    if (sourceId is not null && drop.Contains(sourceId))
                        sources.RemoveAt(i);
                }
            }
        }

        var documents = snapshot.Documents.Select(document =>
            new VaultWriteDocument(
                document.Id,
                document.Path,
                document.Text ?? Encoding.UTF8.GetString(document.Bytes.Span)))
            .ToArray();

        var write = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = source,
            Documents = documents,
            HistoryJson = history.ToJsonString(),
            TrashJson = trash.ToJsonString(),
            CompositionsJson = compositions.ToJsonString(),
            AppVersion = appVersion,
            Now = now
        });

        if (!write.Writable)
            throw new InvalidOperationException("Vault somente leitura: nada foi alterado.");

        var final = write.Files.ToDictionary(
            pair => pair.Key,
            pair => new VaultFile(pair.Key, pair.Value.Bytes.ToArray()),
            StringComparer.OrdinalIgnoreCase);
        var operations = write.Operations.ToList();

        final.TryGetValue(".urbe/vault.json", out var vaultFile);
        var maintenance = new JsonObject
        {
            ["kind"] = "gc",
            ["at"] = now.UtcDateTime.ToString("O"),
            ["removed"] = new JsonObject
            {
                ["history"] = plan.HistoryCount,
                ["trash"] = plan.TrashCount,
                ["compositionRefs"] = plan.CompositionReferenceCount
            },
            ["orphanDays"] = orphanDays,
            ["trashDays"] = trashDays is null ? null : JsonValue.Create(trashDays.Value)
        };

        var vaultBytes = VaultWriter.SetVaultMaintenance(
            vaultFile?.Bytes,
            appVersion,
            now,
            maintenance);
        final[".urbe/vault.json"] = new VaultFile(".urbe/vault.json", vaultBytes);
        operations.Add(CreateWriteOperation(".urbe/vault.json", vaultBytes));

        var result = new VaultWriteResult(
            true,
            operations.AsReadOnly(),
            new ReadOnlyDictionary<string, VaultFile>(final),
            write.BackupDirectory,
            write.Migrated,
            write.JournalUsed);

        return new VaultGcResult(false, plan, result);
    }

    private static VaultOperation CreateWriteOperation(string path, byte[] bytes)
    {
        var constructor = typeof(VaultOperation).GetConstructor(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            [typeof(VaultOperationKind), typeof(string), typeof(byte[])],
            modifiers: null);
        return (VaultOperation)constructor!.Invoke([VaultOperationKind.Write, path, bytes]);
    }

    private static JsonObject ReadSidecar(
        IReadOnlyDictionary<string, VaultFile> files,
        string key)
    {
        var v2 = $".urbe/{key}.v2.json";
        var v1 = $".urbe/{key}.json";

        if (files.TryGetValue(v2, out var v2File))
        {
            var parsed = Parse(v2File.Bytes);
            if (parsed is not null && parsed["version"]?.GetValue<int>() == 2)
            {
                parsed["version"] = 1;
                return parsed;
            }

            return Empty(key);
        }

        if (files.TryGetValue(v1, out var v1File))
        {
            var parsed = Parse(v1File.Bytes);
            var versionAccepted = parsed is not null &&
                                  (parsed["version"]?.GetValue<int?>() == 1 ||
                                   (key == "trash" && parsed["version"] is null));
            if (versionAccepted)
                return parsed!;
        }

        return Empty(key);
    }

    private static JsonObject Empty(string key) =>
        key == "history"
            ? new JsonObject { ["version"] = 1, ["documents"] = new JsonObject() }
            : new JsonObject { ["version"] = 1, ["items"] = new JsonArray() };

    private static JsonObject? Parse(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return JsonNode.Parse(bytes.Span)?.AsObject();
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static bool TryTrashId(JsonNode? node, out string id)
    {
        id = string.Empty;
        if (node is not JsonObject item ||
            item["document"] is not JsonObject document ||
            document["id"] is not JsonValue value ||
            !value.TryGetValue<string>(out var candidate) ||
            string.IsNullOrWhiteSpace(candidate))
            return false;

        id = candidate;
        return true;
    }

    private static bool TryLong(JsonNode? node, out long value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;
        if (jsonValue.TryGetValue<long>(out value))
            return true;
        if (jsonValue.TryGetValue<double>(out var number))
        {
            value = (long)number;
            return true;
        }

        return false;
    }
}
