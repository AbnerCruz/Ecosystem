using Urbe.Core;

namespace Urbe.Client;

/// <summary>Native-platform vault boundary. The Core remains the canonical parser/writer;
/// clients receive immutable snapshots, never filesystem or Android APIs.</summary>
public interface IUrbeVaultStorage
{
    bool IsConnected { get; }
    Task<VaultSnapshot?> PickAsync(CancellationToken cancellationToken = default);
    Task<VaultSnapshot?> RestoreAsync(CancellationToken cancellationToken = default);
    /// <summary>First safe slice: update one EXISTING Markdown file, with conflict checks,
    /// recovery copy, byte verification and no modifications to other vault entries.</summary>
    Task SaveExistingNoteAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Creates ONE new user text file (a new note, a Personalização JSON) that does not
    /// exist yet, with the same checks; folders on its path are created when missing.</summary>
    Task CreateTextFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Rewrites ONE existing user text file of any editable text type (e.g. tema.json).</summary>
    Task SaveExistingTextFileAsync(string path, string content, CancellationToken cancellationToken = default);

    /// <summary>Creates ONE user folder (a "Nova pasta" / drawn region), and the folders above it, inside
    /// the vault. Never inside .urbe/ or any hidden folder; an existing folder is accepted, a file with
    /// that name is refused. No file of the vault is touched. Hosts must override it.</summary>
    Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Este armazenamento ainda não cria pastas.");

    /// <summary>A folder path Urbe may create: vault-relative, no traversal, drive syntax, or hidden segment.</summary>
    static void EnsureWritableFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Contains(':'))
            throw new InvalidDataException("Caminho inválido no vault: " + path);
        if (path.Split('/').Any(s => s.Length == 0 || s is "." or ".." || s.StartsWith('.') || s.EndsWith(' ') || s.EndsWith('.')))
            throw new InvalidOperationException("Caminho interno ou inseguro no vault: " + path);
    }

    /// <summary>
    /// Writes .urbe/mapa.json, and only it (workspace.js is its single writer in 1.8.4, REQ-040),
    /// with the same safeguards as a note: nothing changed outside Urbe since the open, a map of a
    /// future/unknown format or a read-only vault is never rewritten, the canonical writer
    /// (VaultWriter.Plan with MetadataJson) must produce exactly <paramref name="json"/>, a recovery
    /// copy is kept while replacing, and every file of the vault is verified afterwards.
    /// </summary>
    Task SaveMapAsync(string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// The bytes of .urbe/mapa.json the canonical writer plans for <paramref name="json"/> on these
    /// files, refusing what the 1.8.4 refuses (vault.json of a future format, mapa.json of a future or
    /// unknown version). Returns the existing path of the map (its case on disk) or the default path.
    /// </summary>
    static (string Path, byte[] Bytes) PlanMapWrite(IReadOnlyList<VaultFile> files, string json)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(json);
        var snapshot = VaultReader.Read(files);
        if (snapshot.IsReadOnly)
            throw new InvalidOperationException("Formato futuro de vault: escrita recusada.");
        if (snapshot.IsMapReadOnly || snapshot.FutureFiles.Contains(LegacyCityMap.Path, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("O mapa da cidade é de um formato futuro ou desconhecido: escrita recusada.");
        // The 1.8.4 applies an interrupted operation's journal as a whole (every file) before anything
        // else; writing only the map over it is not that, so it is refused.
        if (snapshot.RecoveredFromJournal)
            throw new InvalidOperationException("Há uma operação interrompida (diário de recuperação) neste vault: escrita do mapa recusada.");
        var documents = snapshot.Documents.Where(d => d.Text is not null)
            .Select(d => new VaultWriteDocument(d.Id, d.Path, d.Text!))
            .ToList();
        var plan = VaultWriter.Plan(new VaultWriteRequest
        {
            Files = files,
            Documents = documents,
            MetadataJson = json,
            AppVersion = "urbe-native-preview"
        });
        var path = files.FirstOrDefault(f => string.Equals(f.Path, LegacyCityMap.Path, StringComparison.OrdinalIgnoreCase))?.Path
                   ?? LegacyCityMap.Path;
        if (!plan.Writable || !plan.Files.TryGetValue(path, out var planned))
            throw new InvalidOperationException("O plano canônico recusou o mapa.");
        var bytes = planned.Bytes.ToArray();
        if (!bytes.AsSpan().SequenceEqual(System.Text.Encoding.UTF8.GetBytes(json)))
            throw new InvalidDataException("O plano canônico modificaria o mapa inesperadamente.");
        return (path, bytes);
    }

    /// <summary>The user files Urbe may write: the 1.8.4 text types, inside the vault, never in
    /// .urbe/ or any hidden folder, never with traversal or drive syntax.</summary>
    static void EnsureWritableTextPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') || path.Contains(':'))
            throw new InvalidDataException("Caminho inválido no vault: " + path);
        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || s is "." or ".." || s.StartsWith('.')))
            throw new InvalidOperationException("Caminho interno ou inseguro no vault: " + path);
        if (!WritableExtensions.Contains(Path.GetExtension(path)))
            throw new NotSupportedException("Tipo de arquivo que o Urbe não grava: " + path);
    }

    static readonly HashSet<string> WritableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".html", ".htm", ".js", ".mjs", ".css", ".json", ".yaml", ".yml", ".csv"
    };
}
