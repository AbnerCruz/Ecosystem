using System.Text.Json;

namespace Hub.Core.Capabilities;

/// <summary>Tool candidata local, sem acesso a arquivo/rede ou conhecimento do Host concreto.</summary>
public static class TextInspectionTool
{
    public const string CapabilityId = "text.inspect";
    public const int MaximumLength = 100_000;

    public static LocalCapability Definition(LocalContext scope) => new(CapabilityId, "local-text-tool",
        new Version(1, 0, 0), "inspect", scope, ["ui.display"], "stateless", ValidInput, ValidOutput, Inspect);

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
        var text = call.Input.GetProperty("text").GetString()!;
        var words = 0; var inside = false; var characters = 0; var lines = text.Length == 0 ? 0 : 1;
        foreach (var rune in text.EnumerateRunes())
        {
            token.ThrowIfCancellationRequested();
            characters++;
            if (rune.Value == '\n') lines++;
            var white = System.Text.Rune.IsWhiteSpace(rune);
            if (!white && !inside) words++;
            inside = !white;
        }
        call.Progress(100);
        return Task.FromResult(JsonSerializer.SerializeToElement(new { characters, words, lines }));
    }
}
