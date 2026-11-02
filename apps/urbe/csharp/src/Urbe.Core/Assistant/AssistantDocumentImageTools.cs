using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>
/// Native, read-only evidence flow for standalone images containing text, formulae, charts and figures.
/// Analysis is opt-in and provider-dependent: it never claims that recognition equals a verified copy.
/// </summary>
public static class AssistantDocumentImageTools
{
    public const int MaxImageBytes = 16 * 1024 * 1024;

    public static string? Mime(string path, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || bytes.Length > MaxImageBytes) return null;
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var png = bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.StartsWith(new byte[] { 255, 216, 255 });
        var webp = bytes.StartsWith("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8);
        if (png && ext == ".png") return "image/png";
        if (jpeg && (ext == ".jpeg" || ext == ".jpg")) return "image/jpeg";
        if (webp && ext == ".webp") return "image/webp";
        return null;
    }

    public static bool AuthorizedReference(string reference) =>
        !string.IsNullOrWhiteSpace(reference) &&
        (reference.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
         reference.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
         reference.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
         reference.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) &&
        !reference.Contains("..", StringComparison.Ordinal) &&
        !reference.Contains('\\') && !reference.Contains(':') && !reference.StartsWith('/') &&
        !reference.Split('/').Any(s => s.Length == 0 || s.StartsWith('.'));

    public static IReadOnlyList<AiTool> All { get; } =
    [
        new()
        {
            Name = "inspect_document_image",
            Access = "read",
            Description = "Inspeciona uma imagem anexada à conversa ou no vault, conferindo formato e SHA256 sem enviar dados externos. Inclui fotos, scans, fórmulas como imagem, tabelas e gráficos, que exigem OCR/visão posterior.",
            Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""")!,
            Label = i => "Conferir imagem " + ((string?)i["path"] ?? ""),
            Run = async (i, host) =>
            {
                var path = (string?)i["path"] ?? "";
                if (!AuthorizedReference(path)) throw new AiToolException("Informe uma imagem PNG, JPEG ou WebP autorizada do vault/conversa.");
                var reader = host.DocumentFiles ?? throw new AiToolException("Este host não dispõe de leitura documental.");
                var bytes = await reader.ReadAsync(path);
                var mime = bytes is null ? null : Mime(path, bytes);
                if (mime is null) throw new AiToolException("Imagem não encontrada, inválida ou maior que 16 MiB.");
                var sha = Convert.ToHexString(SHA256.HashData(bytes!)).ToLowerInvariant();
                return new AiToolOutput("Fonte: " + path + "\nTipo: " + mime +
                    "\nSHA256: " + sha + "\nMétodo: inspeção dos bytes originais (sem OCR)." +
                    "\nVISUAL_REVIEW_REQUIRED: ainda não foi extraído texto nem reconhecida a fórmula/gráfico desta imagem.");
            }
        },
        new()
        {
            Name = "analyze_document_image",
            Access = "network",
            Description = "Reconhece texto, tabelas, figuras, gráficos e fórmulas de uma imagem PNG/JPEG/WebP autorizada com IA multimodal. Exige consentimento explícito antes de transmitir a imagem a provedor externo. Resultado é OCR visual não verificado: cite incertezas, nunca afirme exatidão absoluta.",
            Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""")!,
            Label = i => "Ler texto e fórmulas da imagem " + ((string?)i["path"] ?? ""),
            Run = async (i, host) =>
            {
                var path = (string?)i["path"] ?? "";
                if (!AuthorizedReference(path)) throw new AiToolException("Referência de imagem inválida ou fora do vault/conversa.");
                var reader = host.DocumentFiles ?? throw new AiToolException("Imagem inacessível neste host.");
                var vision = host.DocumentVision ?? throw new AiToolException("Provedor de visão indisponível neste host.");
                var bytes = await reader.ReadAsync(path);
                var mime = bytes is null ? null : Mime(path, bytes);
                if (mime is null) throw new AiToolException("Arquivo ausente, inválido ou acima de 16 MiB.");
                var result = await vision.ExamineImageAsync(path, bytes!, mime);
                return new AiToolOutput("Fonte: " + path +
                    "\nSHA256 da imagem original: " + Convert.ToHexString(SHA256.HashData(bytes!)).ToLowerInvariant() +
                    "\nMétodo: IA multimodal, não transcrição certificada. Fonte original não modificada." +
                    "\nVISUAL_REVIEW_REQUIRED: verifique símbolos, fórmulas LaTeX, números e estruturas complexas na imagem.\n\n" + result);
            }
        }
    ];
}
