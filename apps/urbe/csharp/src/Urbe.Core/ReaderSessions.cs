using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Urbe.Core;

/// <summary>Where a source was left. <paramref name="Total"/> is the count of its parts (PDF pages, EPUB spine items,
/// slides) when the reader knew it; files written before CST-241 have none.</summary>
public sealed record ReaderLocation(int Part, int Offset = 0, double Scroll = 0, int? Total = null);
public sealed record ReaderPreferences(double FontSize = 18, double LineHeight = 1.6, double Zoom = 1, string Theme = "system", double Margin = 5);
/// <summary>The last look chosen in one kind of reader (<see cref="ReaderKinds"/>), used for sources opened for the
/// first time; never the editor's preferences.</summary>
public sealed record ReaderDefault(string Reader, ReaderPreferences Preferences, long Updated);
public sealed record ReaderSession(string VaultId, string Path, string Revision, string Format, ReaderLocation Location, ReaderPreferences Preferences, long Updated, bool Deleted = false);

/// <summary>Canonical schema of app-private reading metadata; author text belongs only in vault notes.</summary>
public sealed class ReaderSessionCatalog
{
    // Version 1 grew additively (CST-241: location totals, reader defaults); older files read as before and older
    // readers ignore the new members.
    private sealed record FileModel(int Version, List<ReaderSession> Entries, List<ReaderDefault>? Defaults = null);
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    private readonly Dictionary<string, ReaderSession> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReaderDefault> _defaults = new(StringComparer.Ordinal);
    public bool ReadOnly { get; private set; }
    public string? Error { get; private set; }
    public IReadOnlyCollection<ReaderSession> Entries => _entries.Values;
    public IReadOnlyCollection<ReaderDefault> Defaults => _defaults.Values;
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
            foreach (var item in file.Defaults ?? [])
            {
                Validate(item);
                if (!catalog._defaults.TryAdd(item.Reader, item)) throw new InvalidDataException("Preferências de leitor duplicadas.");
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or ArgumentException or NullReferenceException)
        { catalog.ReadOnly = true; catalog.Error = "O arquivo de posições de leitura é incompatível ou inválido e foi preservado."; catalog._entries.Clear(); catalog._defaults.Clear(); }
        return catalog;
    }

    public ReaderSession? Restore(string vault, string path, string revision) =>
        _entries.GetValueOrDefault(Key(vault, path)) is { Deleted: false } entry && entry.Revision == revision ? entry : null;

    /// <summary>The session of a path whatever its revision: tells "the file changed since" apart from "never read".</summary>
    public ReaderSession? Latest(string vault, string path) => _entries.GetValueOrDefault(Key(vault, path)) is { Deleted: false } entry ? entry : null;

    /// <summary>"Continuar lendo": the vault's sources with a position worth resuming, the most recently read first.</summary>
    public IReadOnlyList<ReaderSession> Recent(string vault, int max) => _entries.Values
        .Where(e => !e.Deleted && e.VaultId == vault && ReaderProgress.Resumable(e.Format, e.Path))
        .OrderByDescending(e => e.Updated).ThenBy(e => e.Path, StringComparer.Ordinal).Take(Math.Max(0, max)).ToArray();

    public ReaderPreferences? DefaultFor(string reader) => _defaults.GetValueOrDefault(reader)?.Preferences;

    /// <summary>The look a source opened for the first time starts with: the last one chosen in its kind of reader.</summary>
    public ReaderPreferences PreferencesFor(string format)
    {
        var preferences = new ReaderPreferences();
        if (ReaderKinds.IsBook(format) && DefaultFor(ReaderKinds.Book) is { } book) preferences = book with { Zoom = preferences.Zoom };
        if (format == VaultFileKinds.Pdf && DefaultFor(ReaderKinds.Pdf) is { } pdf) preferences = preferences with { Zoom = pdf.Zoom };
        return preferences;
    }

    public bool RecordDefault(ReaderDefault item)
    {
        if (ReadOnly) return false;
        Validate(item);
        if (_defaults.TryGetValue(item.Reader, out var previous) && previous.Preferences == item.Preferences) return false;
        _defaults[item.Reader] = item;
        return true;
    }

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
        foreach (var item in incoming.Defaults)
            if (!_defaults.TryGetValue(item.Reader, out var previous) || previous.Updated < item.Updated || preferIncomingTies && previous.Updated == item.Updated) { Validate(item); _defaults[item.Reader] = item; }
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
        return JsonSerializer.SerializeToUtf8Bytes(new FileModel(1, _entries.Values.OrderBy(e => e.VaultId, StringComparer.Ordinal).ThenBy(e => e.Path, StringComparer.Ordinal).ToList(),
            _defaults.Values.OrderBy(d => d.Reader, StringComparer.Ordinal).ToList()), Json);
    }
    private static void Validate(ReaderSession entry)
    {
        static bool Hash(string s) => s.Length == 64 && s.All(Uri.IsHexDigit);
        if (!Hash(entry.VaultId) || !Hash(entry.Revision) || string.IsNullOrWhiteSpace(entry.Path) || entry.Path.StartsWith('/') || entry.Path.Contains('\\') || entry.Path.Contains(':') ||
            entry.Path.Split('/').Any(p => p is ".." or "." or "") || entry.Location.Part < 1 || entry.Location.Offset < 0 || !double.IsFinite(entry.Location.Scroll) || entry.Location.Scroll is < 0 or > 1 ||
            entry.Location.Total is < 1 || !Valid(entry.Preferences))
            throw new InvalidDataException("Posição de leitura inválida.");
    }
    private static void Validate(ReaderDefault item)
    {
        if (string.IsNullOrEmpty(item.Reader) || item.Reader.Length > 32 || !item.Reader.All(c => c is >= 'a' and <= 'z') || !Valid(item.Preferences))
            throw new InvalidDataException("Preferências de leitor inválidas.");
    }
    private static bool Valid(ReaderPreferences p) =>
        double.IsFinite(p.FontSize) && double.IsFinite(p.LineHeight) && double.IsFinite(p.Zoom) && p.FontSize > 0 && p.LineHeight > 0 && p.Zoom > 0 &&
        double.IsFinite(p.Margin) && p.Margin is >= 0 and <= 25 && p.Theme is not null;
}

/// <summary>The kinds of reader whose look is remembered apart from each other and from the editor.</summary>
public static class ReaderKinds
{
    /// <summary>PDF pages: zoom.</summary>
    public const string Pdf = "pdf";
    /// <summary>Reflowed books and documents (EPUB, DOCX/ODT, slides, CBZ): size, line spacing, theme, margins.</summary>
    public const string Book = "book";
    /// <summary>Plain text and a PDF's text layer: size, line spacing, theme (one look for every such file).</summary>
    public const string Text = "text";
    public static bool IsBook(string format) => format is VaultFileKinds.Epub or VaultFileKinds.Comic or VaultFileKinds.Slides or VaultFileKinds.Document;
}

/// <summary>A reading position said the way its format allows: pages for a PDF, spine items (never invented pages) for
/// a reflowed EPUB, time for media. A fraction only when the total is known.</summary>
public sealed record ReaderProgressInfo(string Position, double? Fraction)
{
    public string? Percent => Fraction is { } f ? Math.Round(f * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "%" : null;
}

/// <summary>CST-241: reading progress by source; separate from the progress of a note's tasks (<see cref="NoteProgress"/>).</summary>
public static class ReaderProgress
{
    /// <summary>Formats whose position can be resumed in the Urbe's readers.</summary>
    public static bool Resumable(string format, string path) => format switch
    {
        VaultFileKinds.Pdf or VaultFileKinds.Epub or VaultFileKinds.Comic or VaultFileKinds.Audio or VaultFileKinds.Video => true,
        VaultFileKinds.Slides or VaultFileKinds.Document => System.IO.Path.GetExtension(path).ToLowerInvariant() is ".pptx" or ".odp" or ".docx" or ".odt",
        _ => false
    };

    public static ReaderProgressInfo Describe(ReaderSession session)
    {
        var at = session.Location;
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        string extension = System.IO.Path.GetExtension(session.Path).ToLowerInvariant();
        if (session.Format is VaultFileKinds.Audio or VaultFileKinds.Video)
        {
            var time = TimeSpan.FromSeconds(at.Part - 1);
            return new("em " + time.ToString(time.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss", invariant), null);
        }
        // reflowed text has no stable physical page: the position is the spine item and how far into it
        bool reflow = session.Format == VaultFileKinds.Epub || extension is ".docx" or ".odt";
        string unit = session.Format switch
        {
            VaultFileKinds.Pdf => "página",
            VaultFileKinds.Epub => "capítulo",
            VaultFileKinds.Comic => "imagem",
            _ => extension is ".pptx" or ".odp" ? "slide" : "seção"
        };
        if (at.Total is not { } total) return new(unit + " " + at.Part, null);
        int part = Math.Min(at.Part, total);
        double fraction = Math.Clamp(reflow ? (part - 1 + at.Scroll) / total : (double)part / total, 0, 1);
        var info = new ReaderProgressInfo(unit + " " + part + " de " + total, fraction);
        return info with { Position = info.Position + " · " + info.Percent + (reflow ? unit == "capítulo" ? " dos capítulos" : " das seções" : "") };
    }

    /// <summary>"última leitura" as a short relative time.</summary>
    public static string LastRead(long updated, long now)
    {
        var age = TimeSpan.FromMilliseconds(Math.Max(0, now - updated));
        if (age.TotalMinutes < 1) return "agora há pouco";
        if (age.TotalHours < 1) return "há " + (int)age.TotalMinutes + " min";
        if (age.TotalDays < 1) return "há " + (int)age.TotalHours + " h";
        if (age.TotalDays < 2) return "ontem";
        if (age.TotalDays < 7) return "há " + (int)age.TotalDays + " dias";
        return DateTimeOffset.FromUnixTimeMilliseconds(updated).ToLocalTime().ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }
}
