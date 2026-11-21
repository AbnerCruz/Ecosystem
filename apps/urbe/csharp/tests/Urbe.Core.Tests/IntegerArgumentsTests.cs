using System.Text.Json.Nodes;
using Urbe.Core.Assistant;

namespace Urbe.Core.Tests;

/// <summary>
/// Owner's report (beta.54): recall_memory with max_chars/offset and list_conversations with limit failed with
/// NodeUnableToConvert. Every integer parameter of every tool, after the runtime's validation, reads as an int.
/// </summary>
public sealed class IntegerArgumentsTests
{
    [Fact]
    public void EveryIntegerParameterReadsAsAnIntAfterValidation()
    {
        var tools = AssistantTools.All.Concat(AssistantWebTools.All).Concat(AssistantMemoryTools.All)
            .Append(AssistantConversationTools.List).Append(AssistantConversationTools.Read).DistinctBy(t => t.Name).ToList();
        Assert.Contains(tools, t => t.Name == "recall_memory");
        Assert.Contains(tools, t => t.Name == "list_conversations");
        int checkedParams = 0;
        foreach (var tool in tools)
        {
            if (tool.Parameters["properties"] is not JsonObject props) continue;
            foreach (var (name, schema) in props)
            {
                if ((string?)schema?["type"] != "integer") continue;
                foreach (var raw in new JsonNode[] { JsonValue.Create(12000.0), JsonValue.Create(1), JsonValue.Create(0), JsonValue.Create(30L) })
                {
                    var input = new JsonObject { [name] = raw.DeepClone() };
                    AssistantTools.Validate(tool, input);
                    if (input[name] is null) continue;
                    var asInt = (int?)input[name];   // what the tools do; threw on a double before
                    Assert.NotNull(asInt);
                    Assert.Equal((long)asInt!, (long?)input[name]);
                }
                checkedParams++;
            }
        }
        Assert.True(checkedParams >= 8, "integer parameters checked: " + checkedParams);
    }

    [Fact]
    public void ANonIntegerIsStillRefused()
    {
        var tool = AssistantTools.All.First(t => t.Parameters["properties"]?["limit"]?["type"]?.ToString() == "integer");
        Assert.Contains(AssistantTools.Validate(tool, new JsonObject { ["limit"] = 1.5 }), e => e.Contains("inteiro"));
    }
}
