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
