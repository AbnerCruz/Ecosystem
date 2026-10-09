using System.Text;
using AgentRuntime;
using AgentRuntime.Tools.Files;

namespace EcosystemAi.Cli;

/// <summary>
/// Leitura opcional de conteúdo textual de um artefato referenciado pelo Runtime.
/// O Product só lê arquivos atuais dentro do workspace vinculado ao catálogo.
/// Preview não representa conteúdo histórico imutável nem resultado verificado.
/// </summary>
public static class CliArtifactTextPreview
{
    public const int MaxPreviewBytes = 16 * 1024;
    public const int MaxTotalPreviewBytes = 128 * 1024;
    public const int MaxPreviews = 16;

    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".json", ".csv", ".cs", ".html", ".xml"
    };

    public static string? Read(ArtifactRef artifact, string workspaceDirectory, ref int totalBytes, ref int previewCount)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceDirectory);

        if (!string.Equals(artifact.Kind, "file", StringComparison.OrdinalIgnoreCase)
            || !Extensions.Contains(Path.GetExtension(artifact.Location))
            || previewCount >= MaxPreviews || totalBytes >= MaxTotalPreviewBytes)
            return null;

        var root = Path.GetFullPath(workspaceDirectory);
        // Não atravessar links nem no workspace, nem nos ancestrais da raiz.
        if (!Directory.Exists(root) || HasSymlinkAncestor(root, isDirectory: true)) return null;

        string? target;
        try { target = new FileSandbox(root).Resolve(artifact.Location, out _); }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException) { return null; }
        if (target is null || !File.Exists(target) || HasSymlinkAncestor(target, isDirectory: false)) return null;

        try
        {
            var info = new FileInfo(target);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0
                || info.Length == 0 || info.Length > MaxPreviewBytes
                || info.Length > MaxTotalPreviewBytes - totalBytes)
                return null;
            // Cria um arquivo de saída independente; não altera o arquivo original.
            // Reject binary and malformed UTF-8 (no silent replacement).
            using var reader = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (reader.Length == 0 || reader.Length > MaxPreviewBytes
                || reader.Length > MaxTotalPreviewBytes - totalBytes)
                return null;
            var bytes = new byte[checked((int)reader.Length)];
            reader.ReadExactly(bytes);
            if (Array.IndexOf(bytes, (byte)0) >= 0) return null;
            var text = new UTF8Encoding(false, true).GetString(bytes);
            totalBytes += bytes.Length;
            previewCount++;
            return text;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            return null;
        }
    }

    private static bool HasSymlinkAncestor(string fullPath, bool isDirectory)
    {
        FileSystemInfo? current = isDirectory
            ? new DirectoryInfo(fullPath) : new FileInfo(fullPath);
        while (current is not null)
        {
            if (current.LinkTarget is not null) return true;
            current = current switch
            {
                DirectoryInfo directory => directory.Parent,
                FileInfo file => file.Directory,
                _ => null
            };
        }
        return false;
    }
}
