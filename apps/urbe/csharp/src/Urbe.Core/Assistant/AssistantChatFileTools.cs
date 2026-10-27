using System.Text.Json.Nodes;

namespace Urbe.Core.Assistant;

/// <summary>Access is scoped to one conversation. The tool never sees unrelated chat histories or device files.</summary>
public interface IAssistantChatFiles
{
    IReadOnlyList<ChatExtractableAttachment> Available { get; }
    Task<string> ExtractAsync(string attachmentId, string? path);
}

/// <summary>Read-only discovery and permission-gated extraction of user-supplied conversation files.</summary>
public static class AssistantChatFileTools
{
    private static IAssistantChatFiles Files(IAssistantHost h) =>
        h.ChatFiles ?? throw new AiToolException("Anexos da conversa não disponíveis.");
    private static string Str(JsonObject input, string name) =>
        input[name] is { } node ? LegacyCustomize.JsToString(node).Trim() : "";

    public static AiTool List { get; } = new()
    {
        Name = "list_chat_attachments", Access = "read",
        Description = "Lista arquivos enviados pelo usuário nesta conversa e identificadores para extract_chat_attachment. Não lista arquivos de outras conversas.",
        Parameters = (JsonObject)JsonNode.Parse("""{"type":"object","properties":{},"additionalProperties":false}""")!,
        Label = _ => "Consultando anexos da conversa",
        Run = (i, host) =>
        {
            var available = Files(host).Available;
            return Task.FromResult(new AiToolOutput(available.Count == 0
                ? "Nenhum arquivo extraível nesta conversa."
                : string.Join("\n", available.Select(a =>
                    a.Id + " | " + a.Name + " | " + a.Bytes.Length + " bytes | " +
                    (a.Exact ? "original preservado" : "texto legado reconstruível")))));
        }
    };

    public static AiTool Extract { get; } = new()
    {
        Name = "extract_chat_attachment", Access = "write",
        Description = "Copia para o vault um anexo que o usuário enviou nesta conversa; nunca inventa nem reconstitui por geração do modelo. Use list_chat_attachments para obter attachment_id. Aceita pasta/caminho opcional; por padrão Anexos/nome. Não sobrescreve. Para HTML extraído, use uma nota Markdown que aponte ao arquivo, em vez de transformar HTML em Markdown. Arquivos antigos podem ser reconstruções textuais, sem bytes originais.",
        Parameters = (JsonObject)JsonNode.Parse("""
            {"type":"object","properties":{
              "attachment_id":{"type":"string","description":"Identificador exato retornado por list_chat_attachments"},
              "path":{"type":"string","description":"Caminho relativo opcional no vault, ex.: Projetos/Site/index.html"}
            },"required":["attachment_id"],"additionalProperties":false}
            """)!,
        Label = i => "Extraindo anexo " + Str(i, "attachment_id"),
        Preview = (i, host) =>
        {
            var item = Files(host).Available.FirstOrDefault(a => a.Id == Str(i, "attachment_id"))
                ?? throw new AiToolException("Anexo não encontrado nesta conversa.");
            var path = Str(i, "path");
            if (path.Length == 0) path = AssistantChatAttachments.DefaultPath(item.Name);
            AssistantChatAttachments.ValidatePath(path);
            return new AiPreview(path, null, null, "Copiar " + item.Name + " para " + path +
                " sem sobrescrever. " + (item.Exact ? "Bytes originais preservados." :
                "Conversa antiga: texto reconstruído, sem garantia dos bytes originais."));
        },
        Run = async (i, host) =>
        {
            var id = Str(i, "attachment_id");
            if (id.Length == 0) throw new AiToolException("Informe attachment_id de list_chat_attachments.");
            var result = await Files(host).ExtractAsync(id, Str(i, "path"));
            return new AiToolOutput(result);
        }
    };
}
