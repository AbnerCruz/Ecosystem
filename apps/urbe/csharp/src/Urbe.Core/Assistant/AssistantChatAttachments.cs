using System.Text;

namespace Urbe.Core.Assistant;

/// <summary>One file supplied by the person in this conversation. Old text-only context is explicitly inexact.</summary>
public sealed record ChatExtractableAttachment(string Id, string Name, byte[] Bytes, bool Exact);

/// <summary>
/// Recover files only from the currently selected conversation. Never treat model text or tool output as an upload.
/// Filenames and requested destinations are untrusted; the vault's own writer performs a second path check.
/// </summary>
public static class AssistantChatAttachments
{
    private const int MaxBytes = 8 * 1024 * 1024;
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt", ".html", ".htm", ".js", ".mjs", ".css", ".json",
        ".yaml", ".yml", ".csv", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp",
        ".pdf", ".mp3", ".m4a", ".ogg", ".oga", ".wav", ".flac", ".mp4", ".webm", ".mov"
    };

    public static IReadOnlyList<ChatExtractableAttachment> List(AiConversation conversation)
    {
        var files = new List<ChatExtractableAttachment>();
        foreach (var message in conversation.Messages.Where(m => m.Role == "user" && m.Meta?.ToolResults != true))
            for (int index = 0; index < message.Content.Count; index++)
            {
                var block = message.Content[index];
                var id = message.Id + ":" + index;
                if (block.Type == "attachment" && !string.IsNullOrEmpty(block.FileName))
                {
                    if (TryBase64(block.Content, out var bytes))
                        files.Add(new ChatExtractableAttachment(id, block.FileName, bytes, true));
                    continue;
                }
                if (block.Type != "text" || !block.Hidden ||
                    block.Text?.StartsWith("[Arquivo anexado pelo usuário: ", StringComparison.Ordinal) != true)
                    continue;
                // Fresh conversations contain the original bytes alongside the inert model-facing text.
                if (!string.IsNullOrWhiteSpace(block.FileName) && TryBase64(block.Content, out var original))
                {
                    files.Add(new ChatExtractableAttachment(id, block.FileName, original, true));
                    continue;
                }
                // Legacy conversations stored only a decoded, model-facing string: recover it honestly.
                var lineEnd = block.Text.IndexOf('\n');
                if (lineEnd <= 0) continue;
                var semicolon = block.Text.IndexOf(';', "[Arquivo anexado pelo usuário: ".Length);
                if (semicolon <= 0 || semicolon > lineEnd) continue;
                var name = block.Text["[Arquivo anexado pelo usuário: ".Length..semicolon].Trim();
                var recovered = Encoding.UTF8.GetBytes(block.Text[(lineEnd + 1)..]);
                if (recovered.Length > 0 && recovered.Length <= MaxBytes)
                    files.Add(new ChatExtractableAttachment(id, name, recovered, false));
            }
        return files;
    }

    private static bool TryBase64(string content, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(content) || content.Length > (MaxBytes + 2) / 3 * 4 + 8) return false;
        try { bytes = Convert.FromBase64String(content); return bytes.Length > 0 && bytes.Length <= MaxBytes; }
        catch (FormatException) { return false; }
    }

    public static string DefaultPath(string filename)
    {
        var name = filename.Replace('\\', '/').Split('/').LastOrDefault() ?? "";
        // Path.GetFileName is OS-dependent; do not trust Android/Windows separators from another device.
        ValidatePath("Anexos/" + name);
        return "Anexos/" + name;
    }

    public static void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > 240 || path.StartsWith('/') ||
            path.Contains('\\') || path.Contains(':') ||
            path.Any(char.IsControl))
            throw new AiToolException("Nome ou destino inseguro para extrair o arquivo.");
        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || s.Length > 120 || s is "." or ".." ||
            s.StartsWith('.') || s.EndsWith('.') || s.EndsWith(' ') || s.StartsWith(' ')))
            throw new AiToolException("Destino fora do vault ou caminho interno não permitido.");
        if (!Supported.Contains(Path.GetExtension(path)))
            throw new AiToolException("Este tipo de arquivo não pode ser salvo no vault.");
        var filename = segments[^1];
        var stem = Path.GetFileNameWithoutExtension(filename).TrimEnd(' ', '.');
        if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5",
                    "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4",
                    "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
            .Contains(stem, StringComparer.OrdinalIgnoreCase))
            throw new AiToolException("Nome reservado do sistema operacional.");
    }
}
