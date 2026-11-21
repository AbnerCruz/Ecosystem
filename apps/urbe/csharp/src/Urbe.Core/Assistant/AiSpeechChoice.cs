namespace Urbe.Core.Assistant;

/// <summary>References a connected provider and its model without persisting credentials in a publication.</summary>
public sealed record AiSpeechChoice(string ProviderId, string Model, string Voice, string Format = "wav")
{
    public string Reference => "speech:" + string.Join("/", new[] { ProviderId, Model, Voice, Format }.Select(Uri.EscapeDataString));

    public static AiSpeechChoice? Parse(string? value)
    {
        if (value?.StartsWith("speech:", StringComparison.Ordinal) != true) return null;
        var parts = value[7..].Split('/');
        if (parts.Length is not (3 or 4)) return null;
        var decoded = parts.Select(Uri.UnescapeDataString).ToArray();
        if (decoded.Any(string.IsNullOrWhiteSpace)) return null;
        var format = decoded.Length == 3 ? "wav" : decoded[3];
        return format is "wav" or "mp3" or "pcm" ? new(decoded[0], decoded[1], decoded[2], format) : null;
    }
}

public sealed record AiSpeechModel(string Id, string Name, IReadOnlyList<string> Voices, string Format);
