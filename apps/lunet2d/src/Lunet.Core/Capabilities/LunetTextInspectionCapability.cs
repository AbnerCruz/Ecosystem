using System.Text.Json;
using Ecosystem.TextInspection;

namespace Lunet.Core.Capabilities;

/// <summary>Adapter do Host Lunet para text.inspect@1.0.0. O algoritmo vem da projeção canônica.</summary>
public static class LunetTextInspectionCapability
{
    public const string CapabilityId = "text.inspect";
    public static readonly Version Version = new(1, 0, 0);

    public static LunetHostCapability Definition() => new(
        CapabilityId,
        "lunet2d",
        Version,
        new LunetHostContext([new("ecosystem", "ecosystem")]),
        [],
        "stateless",
        ValidInput,
        ValidOutput,
        Inspect);

    private static bool ValidInput(JsonElement value) => value.ValueKind == JsonValueKind.Object
        && value.EnumerateObject().Count() == 1
        && value.TryGetProperty("text", out var text)
        && text.ValueKind == JsonValueKind.String
        && text.GetString()!.Length <= TextInspector.MaximumLength;

    private static bool ValidOutput(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 3) return false;
        foreach (var name in new[] { "characters", "words", "lines" })
            if (!value.TryGetProperty(name, out var number)
                || number.ValueKind != JsonValueKind.Number
                || !number.TryGetInt32(out var count)
                || count < 0)
                return false;
        return true;
    }

    private static Task<JsonElement> Inspect(LunetCapabilityInvocation call, CancellationToken token)
    {
        var result = TextInspector.Inspect(call.Input.GetProperty("text").GetString()!, token);
        return Task.FromResult(JsonSerializer.SerializeToElement(new
        {
            characters = result.Characters,
            words = result.Words,
            lines = result.Lines
        }));
    }
}
