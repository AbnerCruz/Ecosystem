using System.Text.Json.Nodes;
namespace Urbe.Core.Assistant;

public interface IAssistantForms
{
    FormSession Request(FormDefinition definition);
}

public static class AssistantFormTools
{
    public static AiTool Request { get; } = new()
    {
        Name = "request_form", Access = "ui",
        Description = "Colete várias respostas em um formulário nativo completo; use ask_user para uma dúvida simples. Campos com initial ficam preenchidos. source_path vincula a versão da nota: releia antes de propor edição. O envio NÃO aprova escrita nem grava memória. Após solicitar, aguarde o usuário. Use apenas dados declarativos, sem código/HTML.",
        Parameters = Schema(),
        Label = i => "Formulário: " + i["title"],
        Run = (input, host) =>
        {
            if (host.Forms is null) throw new AiToolException("Este host não apresenta formulários.");
            try
            {
                var session = host.Forms.Request(FormDefinition.Parse(input));
                return Task.FromResult(new AiToolOutput(new JsonObject { ["form_id"] = session.Id, ["status"] = "pending",
                    ["message"] = "Formulário salvo. Aguarde as respostas do usuário." }.ToJsonString()));
            }
            catch (ArgumentException e) { throw new AiToolException(e.Message); }
        }
    };
    private static JsonObject Schema()
    {
        var schema = (JsonObject)JsonNode.Parse("""
        {"type":"object","properties":{
          "title":{"type":"string"},"description":{"type":"string"},"source_path":{"type":"string"},
          "fields":{"type":"array","minItems":1,"maxItems":128,"items":{"type":"object","properties":{
            "id":{"type":"string"},"label":{"type":"string"},
            "type":{"type":"string","enum":["text","long_text","integer","number","date","time","select","multi_select","checkbox","toggle","scale","list","note","folder","color","calculated","repeat"]},
            "description":{"type":"string"},"group":{"type":"string"},"required":{"type":"boolean"},
            "initial":{},"options":{"type":"array","items":{"type":"string"}},
            "min":{"type":"number"},"max":{"type":"number"},"visible_when":{"type":"string"},"equals_value":{},
            "sum":{"type":"array","items":{"type":"string"}}
          },"required":["id","label","type"],"additionalProperties":false}}
        },"required":["title","fields"],"additionalProperties":false}
        """)!;

        var fields = (JsonObject)schema["properties"]!["fields"]!;
        var field = (JsonObject)fields["items"]!;
        field["properties"]!["fields"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["$ref"] = "#/$defs/field" } };
        schema["$defs"] = new JsonObject { ["field"] = field.DeepClone() };
        fields["items"] = new JsonObject { ["$ref"] = "#/$defs/field" };
        return schema;
    }

}
