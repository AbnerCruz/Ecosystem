using System.Text.Json;
using Ecosystem.TextInspection;

namespace Hub.Core.Capabilities;

/// <summary>Adapter do Host Hub para text.inspect@1.0.0. O algoritmo vive na Library host-neutra.</summary>
public static class TextInspectionTool
{
    public const string CapabilityId = "text.inspect";
    public const int MaximumLength = TextInspector.MaximumLength;

    public static LocalCapability Definition(LocalContext scope) => new(CapabilityId, "local-text-tool",
        new Version(1, 0, 0), "inspect", scope, [], "stateless", ValidInput, ValidOutput, Inspect);

    private static bool ValidInput(JsonElement value) => value.ValueKind == JsonValueKind.Object
        && value.EnumerateObject().Count() == 1
        && value.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
        && text.GetString()!.Length <= MaximumLength;

    private static bool ValidOutput(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != 3) return false;
        foreach (var name in new[] { "characters", "words", "lines" })
            if (!value.TryGetProperty(name, out var number) || number.ValueKind != JsonValueKind.Number
                || !number.TryGetInt32(out var count) || count < 0)
                return false;
        return true;
    }

    private static Task<JsonElement> Inspect(LocalInvocation call, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        call.Progress(0);
        var result = TextInspector.Inspect(call.Input.GetProperty("text").GetString()!, token);
        call.Progress(100);
        return Task.FromResult(JsonSerializer.SerializeToElement(new
        {
            characters = result.Characters,
            words = result.Words,
            lines = result.Lines
        }));
    }
}
