using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Urbe.Core.Assistant;

/// <summary>UC-63: image provider independent of the chat; the vault keeps the generated file.</summary>
public interface IAssistantImages
{
    /// <summary>True when the run's provider draws pictures.</summary>
    bool CanDraw { get; }

    /// <summary>The picture drawn by the run's provider (PNG, JPEG or WebP bytes).</summary>
    Task<byte[]> DrawAsync(string prompt, string size, CancellationToken cancel);

    /// <summary>A file that does not exist yet, in the vault: written and read back (never over another file).</summary>
    Task SaveAsync(string path, byte[] bytes);

    /// <summary>True when the vault already has a file at <paramref name="path"/> (a note or any other file).</summary>
    bool Exists(string path);
}

/// <summary>
/// UC-63: generate_image — the Assistente draws with a configured image provider and keeps it in
/// the vault as a new file (Imagens/ by default; never over a file) and, when asked, puts it in a note as
/// <c>![[path]]</c>. Asked for approval like every tool that writes; the provider is the one the person chose.
/// </summary>
public static class AssistantImageTools
{
    public const string Folder = "Imagens";
    public static readonly string[] Sizes = ["1024x1024", "1536x1024", "1024x1536"];

    private static IAssistantImages I(IAssistantHost host) => host.Images ?? throw new AiToolException("Imagens indisponíveis neste app.");

    private static string Str(JsonObject i, string k) => i[k] is { } v ? LegacyCustomize.JsToString(v).Trim() : "";

    /// <summary>Recognize only the successful image-tool result (also from older beta.20 conversations).</summary>
    public static string? SavedImagePath(string? result)
    {
        if (string.IsNullOrEmpty(result) || !result.StartsWith("Imagem guardada em ", StringComparison.Ordinal)) return null;
        var match = Regex.Match(result, @"^Imagem guardada em (.+?\.(?:png|jpg|jpeg|webp))(?=\.\s|\s+e colocada|$)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var path = match.Groups[1].Value;
        return path.Contains("..", StringComparison.Ordinal) || path.StartsWith("/", StringComparison.Ordinal) ||
               path.Contains('\\') ? null : path;
    }

    /// <summary>
    /// Where the picture goes: the path asked (its extension replaced by the picture's), else Imagens/ and a name
    /// from the description; a name taken gets " 2", " 3"…
    /// </summary>
    public static string PlanPath(string? asked, string prompt, string extension, Func<string, bool> exists)
    {
        var p = AssistantTools.Norm(asked);
        string folder, name;
        if (p.Length > 0)
        {
            if (Regex.IsMatch(p, @"(^|/)\.") || Regex.IsMatch(p, "[:*?\"<>|\u0000-\u001f]")) throw new AiToolException("Caminho inválido: " + p);
            int slash = p.LastIndexOf('/');
            folder = slash < 0 ? "" : p[..slash];
            name = Regex.Replace(slash < 0 ? p : p[(slash + 1)..], @"\.[A-Za-z0-9]{2,5}$", "");
        }
        else
        {
            folder = Folder;
            var words = Regex.Matches(prompt, @"[\p{L}\p{N}]+").Select(m => m.Value).Take(6);
            name = string.Join(" ", words);
            if (name.Length > 48) name = name[..48].TrimEnd();
        }
        if (name.Length == 0) name = "Imagem";
        string At(int n) => (folder.Length > 0 ? folder + "/" : "") + name + (n > 1 ? " " + n : "") + extension;
        int k = 1;
        while (exists(At(k))) k++;
        return At(k);
    }

    public static AiTool Tool { get; } = new()
    {
        Name = "generate_image", Access = "write",
        Description = "Gera uma imagem real a partir de uma descrição, usando um provedor visual configurado sem trocar o modelo do chat. Guarda em Imagens/; com note, insere na nota. Para pedidos de desenho ou ilustração, use generate_image, nunca write_page. Descreva assunto, estilo, cores, enquadramento.",
        Parameters = (JsonObject)JsonNode.Parse("""
            {"type":"object","properties":{
              "prompt":{"type":"string","description":"O que desenhar, em detalhe"},
              "path":{"type":"string","description":"Onde guardar, ex.: Imagens/Personagem. Opcional"},
              "size":{"type":"string","enum":["1024x1024","1536x1024","1024x1536"],"description":"Quadrada, paisagem ou retrato. Padrão quadrada"},
              "note":{"type":"string","description":"Nota onde colocar a imagem (no fim). Opcional"}
            },"required":["prompt"],"additionalProperties":false}
            """)!,
        Label = i => "Gerar imagem: " + (Str(i, "prompt") is { Length: > 60 } p ? p[..60] + "…" : Str(i, "prompt")),
        Preview = (i, host) =>
        {
            var note = Str(i, "note");
            var where = Str(i, "path") is { Length: > 0 } p ? AssistantTools.Norm(p) : Folder + "/";
            return new AiPreview(where, null, null,
                "Gerar imagem com um provedor visual configurado, sem trocar o modelo do chat (" + (Str(i, "size") is { Length: > 0 } s ? s : Sizes[0]) + "), e guardá-la em " + where +
                (note.Length > 0 ? " e colocá-la no fim de " + AssistantTools.Norm(note) : "") + ".\n\nDescrição: " + Str(i, "prompt"));
        },
        Run = async (i, host) =>
        {
            var images = I(host);
            if (!images.CanDraw) throw new AiToolException("Nenhum provedor de imagens configurado. Adicione uma chave OpenRouter, OpenAI ou Google Gemini nas configurações do Assistente; não é necessário trocar o modelo do chat.");
            var prompt = Str(i, "prompt");
            if (prompt.Length == 0) throw new AiToolException("Descreva a imagem em prompt.");
            var size = Str(i, "size") is { Length: > 0 } s ? s : Sizes[0];
            if (!Sizes.Contains(size)) throw new AiToolException("Tamanho inválido: " + size + ". Use " + string.Join(", ", Sizes) + ".");
            var note = Str(i, "note") is { Length: > 0 } n ? AssistantTools.ResolveDoc(host, n) : null;
            byte[] bytes;
            try { bytes = await images.DrawAsync(prompt, size, CancellationToken.None); }
            catch (AiProviderException e) { throw new AiToolException("A imagem não foi gerada: " + e.Message); }
            var extension = AssistantProviders.ImageExtension(bytes) ?? throw new AiToolException("O provedor devolveu algo que não é uma imagem.");
            var path = PlanPath(Str(i, "path"), prompt, extension, images.Exists);
            await images.SaveAsync(path, bytes);
            if (note is null) return new AiToolOutput("Imagem guardada em " + path + ". Para mostrá-la numa nota, escreva ![[" + path + "]].");
            var u = await host.SaveNoteAsync(note, AssistantTools.AppendText(note.Content, "![[" + path + "]]", null));
            return new AiToolOutput("Imagem guardada em " + path + " e colocada no fim de " + u.Path + ".",
                [new AiChange(note.Id, new AiNoteSnap(note.Path, note.Content), new AiNoteSnap(u.Path, u.Content))]);
        }
    };
}
