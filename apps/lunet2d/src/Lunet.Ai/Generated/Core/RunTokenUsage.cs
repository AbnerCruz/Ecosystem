using System.Globalization;

namespace AgentRuntime;

/// <summary>Contagens reportadas nas respostas do modelo; ausência/erro nunca vira zero ou total parcial.</summary>
public sealed record RunTokenUsage(long InputTokens, long OutputTokens)
{
    public static RunTokenUsage? Read(IEnumerable<RuntimeEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        long input = 0, output = 0;
        var seen = false;
        foreach (var entry in events.Where(e => e.Kind == EventKind.ModelResponded))
        {
            if (entry.Data is not { } data || !data.TryGetValue("inputTokens", out var inputText)
                || !data.TryGetValue("outputTokens", out var outputText)
                || !long.TryParse(inputText, NumberStyles.None, CultureInfo.InvariantCulture, out var i)
                || !long.TryParse(outputText, NumberStyles.None, CultureInfo.InvariantCulture, out var o)) return null;
            try { input = checked(input + i); output = checked(output + o); }
            catch (OverflowException) { return null; }
            seen = true;
        }
        return seen ? new(input, output) : null;
    }
}
