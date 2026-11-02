using System.Security.Cryptography;
using System.Text.Json.Nodes;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Writer;

namespace Urbe.Core.Assistant;

/// <summary>Authorized access to PDF bytes from this vault or explicit conversation attachments.</summary>
public interface IAssistantDocumentFiles
{
    Task<byte[]?> ReadAsync(string reference, CancellationToken cancellationToken = default);
}

public sealed record DocumentPage(int Page, int Pages, int Start, int Next, string Text, bool More, bool NeedsOcr, string Sha256, int EmbeddedImages = 0, int VectorPaths = 0, bool VisualReviewRequired = false);
public sealed record DocumentMatch(int Page, int Offset, string Context);
public sealed record DocumentSearch(int Pages, int LastPage, int NextPage, bool Partial, int OcrPages, string Sha256,
    IReadOnlyList<DocumentMatch> Matches, int PagesWithVisuals = 0);

/// <summary>A user-approved provider vision pass over exactly one original PDF page.</summary>
public interface IAssistantDocumentVision
{
    Task<string> ExaminePageAsync(string reference, byte[] singlePagePdf, int page, CancellationToken cancellationToken = default);
}

/// <summary>CST-212 partial: trustworthy PDF text-layer evidence; never claims to OCR scanned pixels.</summary>
public static class AssistantDocumentTools
{
    public const int MaxFileBytes = 60 * 1024 * 1024;
    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    private static void Validate(byte[] data)
    {
        if (data.Length is < 8 or > MaxFileBytes || !data.AsSpan().StartsWith("%PDF-"u8))
            throw new AiToolException("PDF inválido ou superior a 60 MiB.");
    }
    private static string Text(UglyToad.PdfPig.Content.Page p) =>
        (ContentOrderTextExtractor.GetText(p) ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    public static (int Pages, string Sha256) Inspect(byte[] data)
    {
        Validate(data);
        using var pdf = PdfDocument.Open(data);
        return (pdf.NumberOfPages, Hash(data));
    }

    public static DocumentPage Page(byte[] data, int page, int offset = 0, int take = 8000)
    {
        Validate(data);
        if (offset < 0 || take is < 1 or > 16000) throw new AiToolException("Intervalo de caracteres inválido.");
        using var pdf = PdfDocument.Open(data);
        if (page < 1 || page > pdf.NumberOfPages) throw new AiToolException("Página inválida: total " + pdf.NumberOfPages);
        var pdfPage = pdf.GetPage(page);
        var text = Text(pdfPage);
        // Images/figures and vector diagrams can coexist with extractable text.
        int images = pdfPage.NumberOfImages, paths = pdfPage.Paths.Count;
        bool needsVisual = images > 0 || paths > 0;
        if (text.Length == 0) return new(page, pdf.NumberOfPages, 0, 0, "", false, true, Hash(data),
            images, paths, true);
        if (offset > text.Length) throw new AiToolException("Offset fora da página.");
        var next = Math.Min(text.Length, offset + take);
        return new(page, pdf.NumberOfPages, offset, next, text[offset..next], next < text.Length, false, Hash(data), images, paths, needsVisual);
    }

    public static DocumentSearch Search(byte[] data, string query, int start = 1, int count = 150, int limit = 10,
        CancellationToken cancellationToken = default)
    {
        Validate(data);
        if (string.IsNullOrWhiteSpace(query) || query.Length > 240 || count is < 1 or > 500 || limit is < 1 or > 20)
            throw new AiToolException("Busca inválida ou limites excedidos.");
        using var pdf = PdfDocument.Open(data);
        if (start < 1 || start > pdf.NumberOfPages) throw new AiToolException("Página inicial inválida.");
        var matches = new List<DocumentMatch>();
        var last = Math.Min(pdf.NumberOfPages, start + count - 1);
        int scanned = start - 1, ocr = 0, visualPages = 0; bool reachedLimit = false;
        for (int page = start; page <= last; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = pdf.GetPage(page);
            var text = Text(parsed); scanned = page;
            if (parsed.NumberOfImages > 0 || parsed.Paths.Count > 0) visualPages++;
            if (text.Length == 0) { ocr++; continue; }
            int from = 0;
            while (from < text.Length)
            {
                int pos = text.IndexOf(query, from, StringComparison.OrdinalIgnoreCase);
                if (pos < 0) break;
                matches.Add(new(page, pos, text[Math.Max(0, pos - 64)..Math.Min(text.Length, pos + query.Length + 64)]));
                if (matches.Count == limit) { reachedLimit = true; break; }
                from = pos + Math.Max(query.Length, 1);
            }
            if (reachedLimit) break;
        }
        return new(pdf.NumberOfPages, scanned, reachedLimit ? scanned : scanned + 1,
            reachedLimit || scanned < pdf.NumberOfPages, ocr, Hash(data), matches, visualPages);
    }

    /// <summary>Copies one PDF page, retaining embedded images, vector diagrams and formulas for multimodal inspection.</summary>
    public static byte[] SinglePagePdf(byte[] source, int page)
    {
        Validate(source);
        using var pdf = PdfDocument.Open(source);
        if (page < 1 || page > pdf.NumberOfPages)
            throw new AiToolException("Página inexistente neste documento.");
        var output = new PdfDocumentBuilder();
        output.AddPage(pdf, page);
        return output.Build();
    }

    private static string Str(JsonObject i, string key) => (string?)i[key] ?? "";
    private static int Num(JsonObject i, string key, int fallback) => (int?)i[key] ?? fallback;
    private static async Task<AiToolOutput> Execute(JsonObject i, IAssistantHost host, Func<byte[], AiToolOutput> execute)
    {
        var path = Str(i, "path");
        if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || path.Contains("..", StringComparison.Ordinal) ||
            path.StartsWith('/') || path.Contains('\\') || path.Contains(':'))
            throw new AiToolException("Informe um PDF da conversa ou do vault, sem caminhos externos.");
        var reader = host.DocumentFiles ?? throw new AiToolException("Leitura documental indisponível neste host.");
        var data = await reader.ReadAsync(path);
        if (data is null) throw new AiToolException("PDF não encontrado no contexto autorizado ou acima do limite.");
        try { return execute(data); }
        catch (AiToolException) { throw; }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new AiToolException("Falha ao ler PDF (" + e.GetType().Name + "); arquivo preservado.");
        }
    }

    public static IReadOnlyList<AiTool> All { get; } =
    [
        new()
        {
            Name = "inspect_document", Access = "read",
            Description = "Inspeciona PDF: total de páginas e SHA256, sem despejar o livro no contexto.",
            Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""")!,
            Label = i => "Inspecionar PDF " + Str(i, "path"),
            Run = (i, h) => Execute(i, h, data =>
            {
                var info = Inspect(data);
                return new AiToolOutput("Fonte: " + Str(i, "path") + "\nPáginas: " + info.Pages +
                    "\nSHA256: " + info.Sha256 + "\nPara páginas digitalizadas sem texto é necessário OCR separado.");
            })
        },
        new()
        {
            Name = "read_document", Access = "read",
            Description = "Lê texto da página PDF com página, SHA e offset; suporta continuação por start_char. Página sem texto retorna OCR_REQUIRED, sem transcrição inventada.",
            Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"},"page":{"type":"integer","minimum":1},"start_char":{"type":"integer","minimum":0},"max_chars":{"type":"integer","minimum":1,"maximum":16000}},"required":["path","page"],"additionalProperties":false}""")!,
            Label = i => "Ler PDF p. " + Num(i, "page", 1),
            Run = (i, h) => Execute(i, h, data =>
            {
                var p = Page(data, Num(i, "page", 1), Num(i, "start_char", 0), Num(i, "max_chars", 8000));
                var head = "PDF " + Str(i, "path") + " · SHA256 " + p.Sha256 + " · página " + p.Page + "/" + p.Pages + "\n";
                var visual = "\nInventário visual: " + p.EmbeddedImages + " imagem(ns) incorporada(s); " +
                    p.VectorPaths + " caminho(s) vetorial(is)." +
                    (p.VisualReviewRequired
                        ? "\nVISUAL_REVIEW_REQUIRED: fórmulas em imagens, gráficos, desenhos ou legendas não foram transcritos. Exige inspeção visual/OCR."
                        : "\nA inspeção estrutural não substitui inspeção visual da página.");
                return new AiToolOutput(p.NeedsOcr ? head + "OCR_REQUIRED: página sem camada de texto extraível." + visual
                    : head + "Origem: camada textual; caracteres [" + p.Start + "," + p.Next + "):\n" + p.Text +
                      (p.More ? "\nLEITURA PARCIAL: start_char=" + p.Next : "\nFim da página.") + visual);
            })
        },
        new()
        {
            Name = "analyze_document_page", Access = "network",
            Description = "Inspeciona visualmente uma página PDF (texto + imagens + gráficos + fórmulas digitalizadas) enviando SOMENTE aquela página ao provedor multimodal configurado, mediante consentimento específico do usuário. O resultado é reconhecimento por IA, NUNCA transcrição exata verificada; pode falhar em equações. OCR local não é presumido.",
            Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"},"page":{"type":"integer","minimum":1}},"required":["path","page"],"additionalProperties":false}""")!,
            Label = i => "Examinar figuras/fórmulas · página " + Num(i, "page", 1),
            Run = async (i, host) =>
            {
                var path = Str(i, "path");
                if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains("..", StringComparison.Ordinal) || path.Contains('\') ||
                    path.Contains(':') || path.StartsWith('/'))
                    throw new AiToolException("Somente PDF anexado ou do vault é permitido.");
                var reader = host.DocumentFiles ?? throw new AiToolException("PDF inacessível neste host.");
                var vision = host.DocumentVision ?? throw new AiToolException("Modelo visual indisponível neste host.");
                var bytes = await reader.ReadAsync(path);
                if (bytes is null) throw new AiToolException("PDF não encontrado ou muito grande.");
                int page = Num(i, "page", 1);
                var slice = SinglePagePdf(bytes, page);
                var result = await vision.ExaminePageAsync(path, slice, page);
                return new AiToolOutput("Fonte: " + path + "\nSHA256 do documento: " + Hash(bytes) +
                    "\nPágina: " + page + "\nMétodo: interpretação visual por IA (não OCR exato certificado)" +
                    "\nVERIFICAÇÃO VISUAL PENDENTE: fórmulas, gráficos, símbolos e tabelas precisam de conferência." +
                    "\n\n" + result);
            }
        },
        new()
        {
            Name = "search_document", Access = "read",
            Description = "Procura texto exato em PDF por lotes de páginas, retornando página, offset, SHA e aviso OCR; use next_page para continuar.",
            Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{"path":{"type":"string"},"query":{"type":"string"},"start_page":{"type":"integer","minimum":1},"max_pages":{"type":"integer","minimum":1,"maximum":500},"max_results":{"type":"integer","minimum":1,"maximum":20}},"required":["path","query"],"additionalProperties":false}""")!,
            Label = i => "Buscar texto em PDF",
            Run = (i, h) => Execute(i, h, data =>
            {
                var r = Search(data, Str(i, "query"), Num(i, "start_page", 1), Num(i, "max_pages", 150), Num(i, "max_results", 10));
                var output = new System.Text.StringBuilder("PDF " + Str(i, "path") + " · SHA256 " + r.Sha256 +
                    "\nPáginas: " + r.Pages + "; pesquisadas até " + r.LastPage +
                    "; páginas sem texto: " + r.OcrPages + "; páginas com elementos visuais: " + r.PagesWithVisuals + "\n");
                foreach (var m in r.Matches) output.Append("p. ").Append(m.Page).Append(" · offset ")
                    .Append(m.Offset).Append(": ").AppendLine(m.Context);
                if (r.Matches.Count == 0) output.AppendLine("Sem ocorrência no texto pesquisado.");
                if (r.Partial) output.AppendLine("BUSCA PARCIAL; continue em start_page=" + r.NextPage +
                    (r.NextPage == r.LastPage ? " (refine a busca: limite atingido no meio da página)." : "."));
                if (r.OcrPages > 0) output.AppendLine("OCR_REQUIRED: páginas escaneadas não foram pesquisadas.");
                if (r.PagesWithVisuals > 0) output.AppendLine("VISUAL_REVIEW_REQUIRED: texto em figuras, fórmulas, gráficos e imagens não foi pesquisado.");
                return new AiToolOutput(output.ToString());
            })
        }
    ];
}
