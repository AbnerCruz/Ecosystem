using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Urbe.Core;

/// <summary>A note's text typed and not yet saved to the vault, with the saved text it started from.</summary>
public sealed record NoteDraft(string Vault, string Path, string Saved, string Text, DateTimeOffset At);

/// <summary>
/// CST-144: what was typed in a note and not yet saved is kept here, in the app's private folder (never the vault), a
/// small file per note written atomically, so a crash or a killed process between a key and the autosave loses
/// nothing: the next opening of the note offers it back. A draft leaves when the note is saved or the person
/// discards it. Thread-safe within one process (writes run off the UI thread).
/// </summary>
public sealed class NoteDraftStore(string folder)
{
    private readonly object _gate = new();

    public string Folder { get; } = folder;

    private string FileOf(string vault, string path) =>
        System.IO.Path.Combine(Folder, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(vault + "\n" + path)))[..24].ToLowerInvariant() + ".json");

    public void Save(NoteDraft draft)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["vault"] = draft.Vault, ["path"] = draft.Path, ["saved"] = draft.Saved, ["text"] = draft.Text, ["at"] = draft.At.ToString("O")
        });
        lock (_gate)
        {
            Directory.CreateDirectory(Folder);
            var file = FileOf(draft.Vault, draft.Path);
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, file, overwrite: true);
        }
    }

    public void Clear(string vault, string path)
    {
        lock (_gate)
        {
            var file = FileOf(vault, path);
            if (File.Exists(file)) File.Delete(file);
        }
    }

    /// <summary>The draft of a note, or null (none, or unreadable: an unreadable one is left on disk, never overwritten blindly).</summary>
    public NoteDraft? Get(string vault, string path)
    {
        lock (_gate)
        {
            var file = FileOf(vault, path);
            if (!File.Exists(file)) return null;
            try
            {
                var d = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                if (d is null || d.GetValueOrDefault("vault") != vault || d.GetValueOrDefault("path") != path) return null;
                return new NoteDraft(vault, path, d.GetValueOrDefault("saved") ?? "", d.GetValueOrDefault("text") ?? "",
                    DateTimeOffset.TryParse(d.GetValueOrDefault("at"), out var at) ? at : DateTimeOffset.MinValue);
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException) { return null; }
        }
    }

    /// <summary>
    /// Whether a draft is worth offering for a note whose text is now <paramref name="current"/>: it differs from it.
    /// <see cref="Changed"/> tells whether the note itself changed since the draft started (another device, another app).
    /// </summary>
    public static bool Offers(NoteDraft draft, string current) => !string.Equals(draft.Text, current, StringComparison.Ordinal);

    public static bool Changed(NoteDraft draft, string current) => !string.Equals(draft.Saved, current, StringComparison.Ordinal);
}
