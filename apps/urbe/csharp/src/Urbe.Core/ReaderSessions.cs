using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Urbe.Core;

public sealed record ReaderLocation(int Part, int Offset = 0, double Scroll = 0);
public sealed record ReaderPreferences(double FontSize = 18, double LineHeight = 1.6, double Zoom = 1, string Theme = "system", double Margin = 5);
public sealed record ReaderSession(string VaultId, string Path, string Revision, string Format, ReaderLocation Location, ReaderPreferences Preferences, long Updated, bool Deleted = false);

/// <summary>Canonical schema of app-private reading metadata; author text belongs only in vault notes.</summary>
public sealed class ReaderSessionCatalog
{
    private sealed record FileModel(int Version, List<ReaderSession> Entries);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly Dictionary<string, ReaderSession> _entries = new(StringComparer.Ordinal);
    public bool ReadOnly { get; private set; }
    public string? Error { get; private set; }
    public IReadOnlyCollection<ReaderSession> Entries => _entries.Values;
    public static string VaultKey(string identity) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    private static string Key(string vault, string path) => vault + ":" + path;

    public static ReaderSessionCatalog Read(byte[]? bytes)
    {
        var catalog = new ReaderSessionCatalog();
        if (bytes is null) return catalog;
        try
        {
            var file = JsonSerializer.Deserialize<FileModel>(bytes, Json) ?? throw new JsonException();
            if (file.Version != 1) throw new InvalidDataException("Versão de posições de leitura não suportada.");
            foreach (var entry in file.Entries)
            {
                Validate(entry);
                if (!catalog._entries.TryAdd(Key(entry.VaultId, entry.Path), entry)) throw new InvalidDataException("Posições de leitura duplicadas.");
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or NullReferenceException)
        { catalog.ReadOnly = true; catalog.Error = "O arquivo de posições de leitura é incompatível ou inválido e foi preservado."; catalog._entries.Clear(); }
        return catalog;
    }

    public ReaderSession? Restore(string vault, string path, string revision) =>
        _entries.GetValueOrDefault(Key(vault, path)) is { Deleted: false } entry && entry.Revision == revision ? entry : null;

    public bool Record(ReaderSession session)
    {
        if (ReadOnly) return false;
        Validate(session);
        _entries[Key(session.VaultId, session.Path)] = session;
        return true;
    }
    public void Merge(ReaderSessionCatalog incoming, bool preferIncomingTies = true)
    {
        if (ReadOnly || incoming.ReadOnly) throw new InvalidOperationException("Estado incompatível não pode ser mesclado.");
        foreach (var item in incoming.Entries)
            if (!_entries.TryGetValue(Key(item.VaultId, item.Path), out var previous) || (previous.Updated < item.Updated || preferIncomingTies && previous.Updated == item.Updated)) Record(item);
    }
    public void Move(string vault, string from, string to, bool folder = false)
    {
        if (ReadOnly) return;
        foreach (var entry in _entries.Values.Where(e => !e.Deleted && e.VaultId == vault && (e.Path == from || folder && e.Path.StartsWith(from.TrimEnd('/') + "/", StringComparison.Ordinal))).ToArray())
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Record(entry with { Deleted = true, Updated = now });
            Record(entry with { Path = to.TrimEnd('/') + entry.Path[from.Length..], Updated = now });
        }
    }
    public byte[] Serialize()
    {
        if (ReadOnly) throw new InvalidOperationException("O estado original deve ser preservado.");
        return JsonSerializer.SerializeToUtf8Bytes(new FileModel(1, _entries.Values.OrderBy(e => e.VaultId, StringComparer.Ordinal).ThenBy(e => e.Path, StringComparer.Ordinal).ToList()), Json);
    }
    private static void Validate(ReaderSession entry)
    {
        static bool Hash(string s) => s.Length == 64 && s.All(Uri.IsHexDigit);
        if (!Hash(entry.VaultId) || !Hash(entry.Revision) || string.IsNullOrWhiteSpace(entry.Path) || entry.Path.StartsWith('/') || entry.Path.Contains('\\') || entry.Path.Contains(':') ||
            entry.Path.Split('/').Any(p => p is ".." or "." or "") || entry.Location.Part < 1 || entry.Location.Offset < 0 || !double.IsFinite(entry.Location.Scroll) || entry.Location.Scroll is < 0 or > 1 ||
            !double.IsFinite(entry.Preferences.FontSize) || !double.IsFinite(entry.Preferences.LineHeight) || !double.IsFinite(entry.Preferences.Zoom) || entry.Preferences.FontSize <= 0 || entry.Preferences.LineHeight <= 0 || entry.Preferences.Zoom <= 0 || !double.IsFinite(entry.Preferences.Margin) || entry.Preferences.Margin is < 0 or > 25)
            throw new InvalidDataException("Posição de leitura inválida.");
    }
}
