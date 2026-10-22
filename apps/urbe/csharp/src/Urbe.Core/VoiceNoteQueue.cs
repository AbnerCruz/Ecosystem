using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Urbe.Core;

/// <summary>
/// UC-56/UC-66: one spoken quick note waiting to reach the vault. <see cref="Audio"/> is the recording's file name
/// (in the app's private folder) while it is kept; <see cref="Text"/> is set once transcribed. <see cref="Created"/>
/// is the moment it was said: its local clock time is the "HH:mm" of the line and its day picks the note.
/// </summary>
public sealed record VoiceNoteEntry(string Id, DateTimeOffset Created, string? Audio, string? Text, int Attempts = 0, string? Error = null)
{
    /// <summary>The local time the note was said (the time of its line).</summary>
    public DateTime SaidAt => Created.DateTime;

    /// <summary>The day's note this entry goes to, once transcribed; null while there is no text.</summary>
    public (string Path, string Content)? AppendTo(string? existing) => QuickNotes.Append(existing, Text, SaidAt);
}

/// <summary>
/// UC-66: the queue of voice notes not yet written to the vault, kept as a small JSON file in the app's private
/// folder (never in the vault). An entry leaves it only after the verified write of its line succeeded, so a
/// recording is never lost to a failed transcription, a missing vault or the phone being turned off.
/// </summary>
public static class VoiceNoteQueue
{
    public const int Version = 1;

    /// <summary>The queue file's JSON (stable field order, UTF-8, ISO 8601 times with offset).</summary>
    public static string Serialize(IReadOnlyList<VoiceNoteEntry> entries)
    {
        var notes = new JsonArray();
        foreach (var e in entries)
            notes.Add(new JsonObject
            {
                ["id"] = e.Id,
                ["created"] = e.Created.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
                ["audio"] = e.Audio,
                ["text"] = e.Text,
                ["attempts"] = e.Attempts,
                ["error"] = e.Error
            });
        var root = new JsonObject { ["version"] = Version, ["notes"] = notes };
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    /// <summary>
    /// The entries of a queue file. Empty or missing text is an empty queue; anything unreadable throws
    /// <see cref="InvalidDataException"/> (the caller keeps the file aside, never overwrites it).
    /// </summary>
    public static IReadOnlyList<VoiceNoteEntry> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException ex) { throw new InvalidDataException("Fila de notas de voz ilegível.", ex); }
        if (root is not JsonObject obj || obj["notes"] is not JsonArray notes)
            throw new InvalidDataException("Fila de notas de voz sem a lista \"notes\".");
        if (obj["version"] is JsonValue v && v.TryGetValue<int>(out var version) && version > Version)
            throw new InvalidDataException("Fila de notas de voz de uma versão mais nova do Urbe.");
        var list = new List<VoiceNoteEntry>();
        foreach (var n in notes)
        {
            if (n is not JsonObject o || Str(o["id"]) is not { Length: > 0 } id ||
                !DateTimeOffset.TryParse(Str(o["created"]), CultureInfo.InvariantCulture, DateTimeStyles.None, out var created))
                throw new InvalidDataException("Entrada inválida na fila de notas de voz.");
            int attempts = o["attempts"] is JsonValue a && a.TryGetValue<int>(out var x) ? x : 0;
            list.Add(new VoiceNoteEntry(id, created, Str(o["audio"]), Str(o["text"]), attempts, Str(o["error"])));
        }
        return list;
    }

    /// <summary>
    /// Recordings in the folder that no entry names (the app was killed between saving the audio and the queue)
    /// become entries again, dated by their file time: the audio is never forgotten.
    /// </summary>
    public static IReadOnlyList<VoiceNoteEntry> AdoptOrphans(IReadOnlyList<VoiceNoteEntry> entries, IEnumerable<(string Name, DateTimeOffset Time)> audioFiles)
    {
        var named = entries.Where(e => e.Audio is not null).Select(e => e.Audio!).ToHashSet(StringComparer.Ordinal);
        var list = entries.ToList();
        foreach (var (name, time) in audioFiles.OrderBy(f => f.Time))
            if (!named.Contains(name))
                list.Add(new VoiceNoteEntry(Path.GetFileNameWithoutExtension(name), time, name, null));
        return list;
    }

    private static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}

/// <summary>
/// The queue file on disk (app-private). Writes are atomic (temporary file, then replace); a file that cannot be
/// read is moved aside as "*.ilegivel-*" and never overwritten. Thread-safe within one process.
/// </summary>
public sealed class VoiceNoteQueueFile(string path)
{
    private static readonly object Lock = new();

    public string Path { get; } = path;

    /// <summary>The folder next to the queue file where recordings are kept.</summary>
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? ".";

    public IReadOnlyList<VoiceNoteEntry> Load()
    {
        lock (Lock) return LoadUnlocked();
    }

    /// <summary>Changes the queue under the lock and writes it back; returns the new list.</summary>
    public IReadOnlyList<VoiceNoteEntry> Update(Func<IReadOnlyList<VoiceNoteEntry>, IReadOnlyList<VoiceNoteEntry>> change)
    {
        lock (Lock)
        {
            var next = change(LoadUnlocked());
            Directory.CreateDirectory(Folder);
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, VoiceNoteQueue.Serialize(next), new UTF8Encoding(false));
            File.Move(tmp, Path, overwrite: true);
            return next;
        }
    }

    public void Add(VoiceNoteEntry entry) => Update(list => [.. list.Where(e => e.Id != entry.Id), entry]);

    public void Replace(VoiceNoteEntry entry) => Update(list => [.. list.Select(e => e.Id == entry.Id ? entry : e)]);

    /// <summary>Removes the entry and then its recording (only after its line was written).</summary>
    public void Remove(string id)
    {
        string? audio = null;
        Update(list =>
        {
            audio = list.FirstOrDefault(e => e.Id == id)?.Audio;
            return [.. list.Where(e => e.Id != id)];
        });
        if (audio is not null)
            try { File.Delete(System.IO.Path.Combine(Folder, audio)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The queue with recordings no entry names adopted back (see <see cref="VoiceNoteQueue.AdoptOrphans"/>).</summary>
    public IReadOnlyList<VoiceNoteEntry> LoadAdoptingOrphans(string audioPattern = "*.wav")
    {
        lock (Lock)
        {
            var list = LoadUnlocked();
            if (!Directory.Exists(Folder)) return list;
            var files = Directory.EnumerateFiles(Folder, audioPattern)
                .Select(f => (System.IO.Path.GetFileName(f), new DateTimeOffset(File.GetLastWriteTime(f))));
            var adopted = VoiceNoteQueue.AdoptOrphans(list, files);
            if (adopted.Count == list.Count) return list;
            var tmp = Path + ".tmp";
            File.WriteAllText(tmp, VoiceNoteQueue.Serialize(adopted), new UTF8Encoding(false));
            File.Move(tmp, Path, overwrite: true);
            return adopted;
        }
    }

    private IReadOnlyList<VoiceNoteEntry> LoadUnlocked()
    {
        if (!File.Exists(Path)) return [];
        try { return VoiceNoteQueue.Parse(File.ReadAllText(Path)); }
        catch (InvalidDataException)
        {
            // keep the unreadable file for inspection; the recordings stay and are adopted back
            File.Move(Path, Path + ".ilegivel-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture), overwrite: true);
            return [];
        }
    }
}
