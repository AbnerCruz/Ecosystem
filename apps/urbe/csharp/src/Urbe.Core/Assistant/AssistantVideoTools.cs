using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>CST-173: a generative video model the person configured and switched on, and the vault that keeps the result.</summary>
public interface IAssistantVideos
{
    /// <summary>True when generative video is switched on and a provider that makes videos is configured.</summary>
    bool CanGenerate { get; }
    /// <summary>Why nothing can be generated now (switched off, no provider), for the agent to tell the person.</summary>
    string Unavailable { get; }
    /// <summary>The model that will be called, said before the person approves.</summary>
    string Model { get; }
    Task<byte[]> GenerateAsync(string prompt, int seconds, string size, CancellationToken cancel);
    /// <summary>A file that does not exist yet, written and read back.</summary>
    Task SaveAsync(string path, byte[] bytes);
    bool Exists(string path);
}

/// <summary>
/// CST-173: generate_video — an optional clip made by an external generative model (paid per second, only the
/// description is sent), saved in the vault as an MP4. It never stands in for the editable motion-graphics video of a
/// Publication, which the Urbe renders itself from notes and scenes.
/// </summary>
public static class AssistantVideoTools
{
    public const string Folder = "Vídeos";
    public static readonly int[] Seconds = [4, 8, 12];
    public static readonly string[] Sizes = ["1280x720", "720x1280"];

    private static string Str(JsonObject i, string k) => i[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : "";
    private static int Duration(JsonObject i) => i["seconds"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 4;

    public static AiTool Tool { get; } = new()
    {
        Name = "generate_video", Access = "write",
        Description = "Gera um clipe curto com um modelo de vídeo generativo externo (pago por segundo; envia só a descrição) e guarda o MP4 em Vídeos/. " +
                      "Para vídeo explicativo editável a partir de notas, use uma Publicação no layout video (motion graphics do Numa), não esta ferramenta.",
        Parameters = JsonNode.Parse("""
            {"type":"object","properties":{
              "prompt":{"type":"string","description":"O que mostrar: assunto, ação, enquadramento, estilo"},
              "seconds":{"type":"integer","enum":[4,8,12]},
              "size":{"type":"string","enum":["1280x720","720x1280"],"description":"Paisagem ou retrato"},
              "path":{"type":"string","description":"Onde guardar. Opcional"}
            },"required":["prompt"],"additionalProperties":false}
            """)!.AsObject(),
        Label = i => "Gerar vídeo: " + (Str(i, "prompt") is { Length: > 60 } p ? p[..60] + "…" : Str(i, "prompt")),
        Preview = (i, host) =>
        {
            var model = host.Videos?.Model ?? "modelo de vídeo";
            return new AiPreview(Str(i, "path") is { Length: > 0 } p ? AssistantTools.Norm(p) : Folder + "/", null, null,
                "Gerar um vídeo de " + Duration(i) + " s (" + (Str(i, "size") is { Length: > 0 } s ? s : Sizes[0]) + ") com " + model +
                ", um modelo generativo externo: o provedor cobra por segundo e recebe só esta descrição. O resultado não é editável cena a cena." +
                "\n\nDescrição: " + Str(i, "prompt"));
        },
        Run = async (i, host) =>
        {
            var videos = host.Videos ?? throw new AiToolException("Vídeo generativo indisponível neste app.");
            if (!videos.CanGenerate) throw new AiToolException(videos.Unavailable);
            var prompt = Str(i, "prompt");
            if (prompt.Length == 0) throw new AiToolException("Descreva o vídeo em prompt.");
            int seconds = Duration(i);
            if (!Seconds.Contains(seconds)) throw new AiToolException("Duração inválida: " + seconds + ". Use 4, 8 ou 12 segundos.");
            var size = Str(i, "size") is { Length: > 0 } s ? s : Sizes[0];
            if (!Sizes.Contains(size)) throw new AiToolException("Tamanho inválido: " + size + ". Use 1280x720 ou 720x1280.");
            byte[] bytes;
            try { bytes = await videos.GenerateAsync(prompt, seconds, size, CancellationToken.None); }
            catch (AiProviderException e) { throw new AiToolException("O vídeo não foi gerado: " + e.Message); }
            var path = AssistantImageTools.PlanPath(Str(i, "path"), prompt, ".mp4", videos.Exists, Folder);
            await videos.SaveAsync(path, bytes);
            return new AiToolOutput("Vídeo guardado em " + path + " (" + seconds + " s, gerado por " + videos.Model + "). Para mostrá-lo numa nota, escreva ![[" + path + "]].");
        }
    };
}
