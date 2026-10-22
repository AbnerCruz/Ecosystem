using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>One note's record in .urbe/notas.json: its stable id and its dates ("yyyy-MM-dd"), never its text.</summary>
public sealed record NoteRecord(string Id, string? Created, string? Modified);

/// <summary>
/// The notes of a vault without the city (ADD-0028 / DEC-0051): the documents, the folders (empty ones too) and what
/// each note keeps besides its file (stable id, created and modified dates). The text of each note is its file; this
/// class never owns it.
/// <para>
/// The record is .urbe/notas.json (<see cref="Serialize"/>), the single authority for ids, dates and folders. A vault
/// that has none yet (opened before) takes them once from the 1.8.4 map (.urbe/mapa.json: notas.{path}.id/criado/modificado
/// and regioes.caminho), which is only read: the city files of the vault are never rewritten or removed.
/// </para>
/// </summary>
public sealed class NoteLibrary
{
    public const string Path = ".urbe/notas.json";
    public const int Version = 1;

    private const string TextAlt = "md|markdown|txt|html?|js|mjs|css|json|ya?ml|csv";
    private static readonly Regex TextRe = new(@"\.(" + TextAlt + ")$", RegexOptions.IgnoreCase);
    private static readonly Regex SplitTextRe = new(@"^(.*?)(\.(?:" + TextAlt + "))$", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private readonly SortedSet<string> _folders = new(StringComparer.Ordinal);

    private NoteLibrary(DocumentStore store) => Store = store;

    /// <summary>The documents of the vault.</summary>
    public DocumentStore Store { get; }

    /// <summary>Every folder of the vault the Explorer shows: the recorded ones (empty folders too) and every folder
    /// that holds a note or a file. Sorted, without the root.</summary>
    public IReadOnlyCollection<string> Folders => _folders;

    /// <summary>
    /// The notes of a vault. <paramref name="notes"/> are its text documents with the id each one has after the opening
    /// (<see cref="Ids"/>). Dates and folders come from .urbe/notas.json when there is one (<paramref name="notasJson"/>),
    /// otherwise from the 1.8.4 map (<paramref name="legacyMapJson"/>, read only). <paramref name="otherFiles"/> are the
    /// vault's other files (attachments): their folders are folders too.
    /// </summary>
    public static NoteLibrary Open(IReadOnlyList<(string Path, string Content, string Id)> notes, string? notasJson,
        string? legacyMapJson, IEnumerable<string>? otherFiles = null)
    {
        ArgumentNullException.ThrowIfNull(notes);
        var record = ParseRecord(notasJson, out _);
        var legacy = record is null ? ParseLegacyMap(legacyMapJson) : null;
        var dates = record?.Notes ?? legacy?.Notes ?? new Dictionary<string, NoteRecord>(StringComparer.Ordinal);
        var store = new DocumentStore();
        foreach (var (path, content, id) in notes)
        {
            dates.TryGetValue(path, out var known);
            store.Upsert(new DocumentInput { Id = id, Path = path, Content = content, Created = known?.Created, Modified = known?.Modified });
        }
        var library = new NoteLibrary(store);
        foreach (var f in record?.Folders ?? legacy?.Folders ?? []) library.AddFolder(f);
        foreach (var d in store.List()) library.AddFolderOf(d.Path);
        foreach (var f in otherFiles ?? []) library.AddFolderOf(f);
        return library;
    }

    /// <summary>
    /// The id each note keeps at the opening: the one recorded in .urbe/notas.json for its path or, in a vault that has
    /// no record yet, the one the 1.8.4 map gave it. Notes without one get null (the caller reconciles them with
    /// .urbe/identity.json, then gives them new ids).
    /// </summary>
    public static Dictionary<string, string?> Ids(IEnumerable<string> paths, string? notasJson, string? legacyMapJson)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var record = ParseRecord(notasJson, out _);
        var known = record?.Notes ?? ParseLegacyMap(legacyMapJson)?.Notes ?? new Dictionary<string, NoteRecord>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        var ids = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var p in paths)
            ids[p] = known.TryGetValue(p, out var r) && r.Id.Length > 0 && used.Add(r.Id) ? r.Id : null;
        return ids;
    }

    /// <summary>True when <paramref name="notasJson"/> is a record of a newer version (it is kept and never written).</summary>
    public static bool IsFuture(string? notasJson)
    {
        ParseRecord(notasJson, out var future);
        return future;
    }

    /// <summary>True when <paramref name="json"/> is a record of this version (what may be written).</summary>
    public static bool IsRecord(string? json) => ParseRecord(json, out _) is not null;

    // ---------------------------------------------------------------- the record

    private sealed record Parsed(Dictionary<string, NoteRecord> Notes, List<string> Folders);

    private static Parsed? ParseRecord(string? json, out bool future)
    {
        future = false;
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json.TrimStart('﻿'));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("versao", out var v) || !v.TryGetInt32(out var version)) return null;
            if (version > Version) { future = true; return null; }
            if (version != Version) return null;
            var notes = new Dictionary<string, NoteRecord>(StringComparer.Ordinal);
            if (root.TryGetProperty("notas", out var notas) && notas.ValueKind == JsonValueKind.Object)
                foreach (var p in notas.EnumerateObject())
                    if (Str(p.Value, "id") is { Length: > 0 } id) notes[p.Name] = new NoteRecord(id, Str(p.Value, "criado"), Str(p.Value, "modificado"));
            var folders = new List<string>();
            if (root.TryGetProperty("pastas", out var pastas) && pastas.ValueKind == JsonValueKind.Array)
                foreach (var f in pastas.EnumerateArray())
                    if (f.ValueKind == JsonValueKind.String && SafeFolder(f.GetString()!) is { } folder) folders.Add(folder);
            return new Parsed(notes, folders);
        }
        catch (JsonException) { return null; }
    }

    /// <summary>The 1.8.4 map, read only: notas.{path}.id/criado/modificado and the folder of each neighbourhood.</summary>
    private static Parsed? ParseLegacyMap(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json.TrimStart('﻿'));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var notes = new Dictionary<string, NoteRecord>(StringComparer.Ordinal);
            if (root.TryGetProperty("notas", out var notas) && notas.ValueKind == JsonValueKind.Object)
                foreach (var p in notas.EnumerateObject())
                    notes[p.Name] = new NoteRecord(Str(p.Value, "id") ?? "", Str(p.Value, "criado"), Str(p.Value, "modificado"));
            var folders = new List<string>();
            if (root.TryGetProperty("regioes", out var regioes) && regioes.ValueKind == JsonValueKind.Array)
                foreach (var r in regioes.EnumerateArray())
                    if (Str(r, "caminho") is { } c && SafeFolder(c) is { } folder) folders.Add(folder);
            return new Parsed(notes, folders);
        }
        catch (JsonException) { return null; }
    }

    private static string? Str(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>A folder path the Explorer may show: relative, no traversal, no hidden segment.</summary>
    private static string? SafeFolder(string path)
    {
        var p = DocumentModel.NormalizePath(path).Trim('/');
        if (p.Length == 0 || p.Contains('\\') || p.Contains(':')) return null;
        return p.Split('/').Any(s => s.Length == 0 || s is "." or ".." || s.StartsWith('.')) ? null : p;
    }

    /// <summary>
    /// .urbe/notas.json as it is now: {"versao":1, "notas":{path:{id, criado, modificado}}, "pastas":[…]}, notes by path
    /// and folders in ordinal order, so the same notes always give the same bytes.
    /// </summary>
    public string Serialize()
    {
        var notas = new JsonObject();
        foreach (var d in Store.List().OrderBy(d => d.Path, StringComparer.Ordinal))
        {
            var o = new JsonObject { ["id"] = d.Id };
            if (!string.IsNullOrEmpty(d.Created)) o["criado"] = d.Created;
            if (!string.IsNullOrEmpty(d.Modified)) o["modificado"] = d.Modified;
            notas[d.Path] = o;
        }
        var root = new JsonObject
        {
            ["versao"] = Version,
            ["notas"] = notas,
            ["pastas"] = new JsonArray(_folders.Select(f => (JsonNode)JsonValue.Create(f)!).ToArray())
        };
        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, IndentSize = 1, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    // ---------------------------------------------------------------- folders

    /// <summary>The folder and every folder above it.</summary>
    public void AddFolder(string folder)
    {
        if (SafeFolder(folder) is not { } f) return;
        for (var p = f; p.Length > 0; p = FolderOf(p)) _folders.Add(p);
    }

    /// <summary>The folder of a file and every folder above it.</summary>
    public void AddFolderOf(string path) => AddFolder(FolderOf(DocumentModel.NormalizePath(path)));

    /// <summary>A folder deleted: it and every folder below it.</summary>
    public void RemoveFolder(string folder)
    {
        var f = DocumentModel.NormalizePath(folder).Trim('/');
        _folders.RemoveWhere(p => p == f || p.StartsWith(f + "/", StringComparison.Ordinal));
    }

    /// <summary>A folder renamed or moved: it and every folder below it take the new path.</summary>
    public void MoveFolder(string from, string to)
    {
        var f = DocumentModel.NormalizePath(from).Trim('/');
        var moved = _folders.Where(p => p == f || p.StartsWith(f + "/", StringComparison.Ordinal)).ToList();
        foreach (var p in moved) _folders.Remove(p);
        foreach (var p in moved) AddFolder(to + p[f.Length..]);
        AddFolder(to);
    }

    /// <summary>The folder of a path ("" for the root).</summary>
    public static string FolderOf(string path)
    {
        var i = path.LastIndexOf('/');
        return i > 0 ? path[..i] : "";
    }

    /// <summary>The file name of a path.</summary>
    public static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];

    // ---------------------------------------------------------------- names (v21UniqueName, separarNomeExtNota)

    /// <summary>
    /// v21UniqueName: the typed name with its text extension (.md when none), with " (2)", " (3)"… when another note of
    /// the same folder (not <paramref name="ignorePath"/>) already has it.
    /// </summary>
    public string UniqueNoteName(string raw, string folder, string? ignorePath = null)
    {
        var txt = Regex.Replace((raw ?? "").Trim(), @"[\\/]", "-");
        if (txt.Length == 0) txt = "Nota.md";
        var m = Regex.Match(txt, @"(\.[A-Za-z0-9]+)$");
        var ext = m.Success && TextRe.IsMatch(m.Groups[1].Value.ToLowerInvariant()) ? m.Groups[1].Value.ToLowerInvariant() : ".md";
        var stemSource = TextRe.Match(txt) is { Success: true } tm ? txt[..^tm.Length] : txt;
        var stem = ArtifactModel.SafeName(stemSource.Length > 0 ? stemSource : "Nota");
        var f = DocumentModel.NormalizePath(folder ?? "").Trim('/');
        var taken = new HashSet<string>(Store.List()
            .Where(d => d.Path != ignorePath && FolderOf(d.Path) == f)
            .Select(d => FileName(d.Path).ToLowerInvariant()), StringComparer.Ordinal);
        var candidate = stem + ext;
        for (int n = 2; taken.Contains(candidate.ToLowerInvariant()); n++) candidate = stem + " (" + n + ")" + ext;
        return candidate;
    }

    /// <summary>separarNomeExtNota(valor, {ext:'.md'}): name and text extension of a file name.</summary>
    public static (string Name, string Ext) SplitNoteName(string full) => SplitNoteName(full, ".md");

    private static (string Name, string Ext) SplitNoteName(string full, string fallbackExt)
    {
        var v = Regex.Replace(full.Trim(), @"[\\/]", "-");
        if (SplitTextRe.Match(v) is { Success: true } sm && sm.Groups[1].Value.Trim().Length > 0)
            return (ArtifactModel.SafeName(sm.Groups[1].Value.Trim()), sm.Groups[2].Value.ToLowerInvariant());
        return (ArtifactModel.SafeName(Regex.Replace(v, @"\.[^.]+$", "")), fallbackExt);
    }

    /// <summary>extNota: the note's text extension (.md when it has none Urbe writes).</summary>
    public static string NoteExtension(string path)
    {
        var m = Regex.Match(FileName(path), @"\.[^.]+$");
        var e = m.Success ? m.Value.ToLowerInvariant() : "";
        return TextRe.IsMatch(e) ? e : ".md";
    }

    /// <summary>nomeCompletoNota: the file name of the note (what the rename dialog shows).</summary>
    public static string FullNoteName(string path) => FileName(DocumentModel.NormalizePath(path)) is { Length: > 0 } f ? f : "arquivo.md";

    /// <summary>
    /// renomearNota: the note's new name, extension and path in the same folder, unique there. Nothing changes; the
    /// file is moved first, then <see cref="MoveDocument"/>.
    /// </summary>
    public (string Name, string Ext, string Path) PlanRename(string path, string typed)
    {
        var p = DocumentModel.NormalizePath(path);
        var folder = FolderOf(p);
        var unique = UniqueNoteName(typed ?? "", folder, p);
        var (name, ext) = SplitNoteName(unique, NoteExtension(p));
        return (name, ext, (folder.Length > 0 ? folder + "/" : "") + name + ext);
    }

    /// <summary>
    /// v21CreateFileInRegion without the lot: the path of a new note typed in "Nova nota", in <paramref name="folder"/>
    /// ("" for the root), unique there, and its first text (v21DefaultContent: "# " for Markdown).
    /// </summary>
    public (string Path, string Content) PlanNewNote(string folder, string typedName)
    {
        var f = DocumentModel.NormalizePath(folder ?? "").Trim('/');
        var (name, ext) = SplitNoteName(UniqueNoteName(typedName, f));
        return ((f.Length > 0 ? f + "/" : "") + name + ext, ext is ".md" or ".markdown" ? "# " : "");
    }

    /// <summary>A new note written to the vault: its document, created and modified today.</summary>
    public UrbeDocument AddNote(string path, string content, long nowMs, string source = "explorer.create")
    {
        var today = NowDate(nowMs);
        var doc = Store.Upsert(new DocumentInput { Path = path, Content = content, Created = today, Modified = today }, source);
        AddFolderOf(doc.Path);
        return doc;
    }

    /// <summary>A note that came back (restored from the trash): the same id and dates, or today's when it has none.</summary>
    public UrbeDocument Restore(UrbeDocument doc, long nowMs)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var today = NowDate(nowMs);
        var back = Store.Upsert(new DocumentInput
        {
            Id = doc.Id, Path = doc.Path, Title = doc.Title, Content = doc.Content, Properties = doc.Properties, Tags = doc.Tags,
            Links = doc.Links, Created = string.IsNullOrEmpty(doc.Created) ? today : doc.Created,
            Modified = string.IsNullOrEmpty(doc.Modified) ? today : doc.Modified
        }, "trash.restore");
        AddFolderOf(back.Path);
        return back;
    }

    /// <summary>The note's text was saved: modified becomes today (editor input: b.modified=nowDate()).</summary>
    public UrbeDocument? Edited(string path, string content, long nowMs)
    {
        if (Store.Get(path) is not { } doc) return null;
        return Store.Upsert(new DocumentInput
        {
            Id = doc.Id, Path = doc.Path, Title = doc.Title, Content = content, Created = doc.Created, Modified = NowDate(nowMs)
        }, "editor.input");
    }

    /// <summary>
    /// renderProps(propertiesFor(b)), the box over the text in Fonte: título (the name), categoria (the folder, or raiz),
    /// the tags, criado and modificado (today when the note has none).
    /// </summary>
    public string? PropertiesText(string path, long nowMs)
    {
        if (Store.Get(path) is not { } doc) return null;
        var today = NowDate(nowMs);
        var folder = FolderOf(doc.Path);
        var name = SplitNoteName(FileName(doc.Path), NoteExtension(doc.Path)).Name;
        return "---\ntítulo: " + name + "\ncategoria: " + (folder.Length > 0 ? folder : "raiz") + "\ntags: [" + string.Join(", ", doc.Tags) +
               "]\ncriado: " + (string.IsNullOrEmpty(doc.Created) ? today : doc.Created) + "\nmodificado: " +
               (string.IsNullOrEmpty(doc.Modified) ? today : doc.Modified) + "\n---";
    }

    // ---------------------------------------------------------------- documents (explorer.js operations)

    /// <summary>app.js nowDate(): the UTC date of the instant, "yyyy-MM-dd".</summary>
    public static string NowDate(long unixMs) =>
        DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A note whose file moved: the same document (id, title, tags, dates) at the new path.</summary>
    public static UrbeDocument MoveDocument(DocumentStore store, UrbeDocument doc, string path)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(doc);
        return store.Upsert(new DocumentInput
        {
            Id = doc.Id, Path = path, Title = doc.Title, Content = doc.Content, Properties = doc.Properties,
            Tags = doc.Tags, Links = doc.Links, Created = doc.Created, Modified = doc.Modified
        }, "explorer.move");
    }

    /// <summary>
    /// explorer.js unique(path, exceptId): the path itself when no other document has it, else "stem (2).ext",
    /// "stem (3).ext"… (the stem and extension of the whole path).
    /// </summary>
    public static string UniqueDocumentPath(DocumentStore store, string path, string? exceptId = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        var basePath = DocumentModel.NormalizePath(path);
        int slash = basePath.LastIndexOf('/'), dot = basePath.LastIndexOf('.');
        string stem = dot > slash ? basePath[..dot] : basePath, ext = dot > slash ? basePath[dot..] : "";
        var candidate = basePath;
        for (int i = 2; store.Get(candidate) is { } other && other.Id != exceptId; i++)
            candidate = stem + " (" + i.ToString(CultureInfo.InvariantCulture) + ")" + ext;
        return candidate;
    }

    /// <summary>explorer.move (Mover para pasta): the document's path in <paramref name="folder"/>, unique.</summary>
    public static string FolderMovePath(DocumentStore store, UrbeDocument doc, string folder)
    {
        ArgumentNullException.ThrowIfNull(doc);
        var f = DocumentModel.NormalizePath(folder);
        return UniqueDocumentPath(store, (f.Length > 0 ? f + "/" : "") + FileName(doc.Path), doc.Id);
    }

    /// <summary>explorer.duplicate: the copy's document ({...doc, id:new, path:unique("stem cópia.ext"), title}),
    /// created and modified today.</summary>
    public static UrbeDocument DuplicateDocument(DocumentStore store, UrbeDocument doc, long nowMs, string? newId = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(doc);
        int dot = doc.Path.LastIndexOf('.');
        string stem = dot >= 0 ? doc.Path[..dot] : doc.Path, ext = dot >= 0 ? doc.Path[dot..] : "";
        var path = UniqueDocumentPath(store, stem + " cópia" + ext);
        var title = Regex.Replace(FileName(path), @"\.(md|markdown)$", "", RegexOptions.IgnoreCase);
        var today = NowDate(nowMs);
        return store.Upsert(new DocumentInput
        {
            Id = newId, Path = path, Title = title, Content = doc.Content, Properties = doc.Properties, Tags = doc.Tags, Links = doc.Links,
            Created = today, Modified = today
        }, "explorer.duplicate");
    }

    /// <summary>operations.js removeFolder: the documents under the folder (path + '/'), in the store's order.</summary>
    public static List<UrbeDocument> FolderDocuments(DocumentStore store, string folder)
    {
        ArgumentNullException.ThrowIfNull(store);
        return store.List().Where(d => d.Path.StartsWith(folder + "/", StringComparison.Ordinal)).ToList();
    }

    /// <summary>urbeNomeDoc: the title without the file extension.</summary>
    public static string NoteName(string title, string path)
    {
        var m = Regex.Match(path, @"\.[^./]+$");
        var ext = m.Success ? m.Value : ".md";
        return title.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? title[..^ext.Length] : title;
    }
}
