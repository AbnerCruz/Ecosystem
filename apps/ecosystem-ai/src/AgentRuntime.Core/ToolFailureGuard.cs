using System.Security.Cryptography;
using System.Text.Json;

namespace AgentRuntime;

/// <summary>Run-local projection of failed executions; the event log remains the authority.</summary>
internal sealed class ToolFailureGuard
{
    internal const string KeyField = "failedInvocationSha256";
    internal const string RepeatField = "repeatedFailedInvocation";
    internal const string InvalidateField = "invalidatesFailedInvocations";
    internal const string RepeatReason = "chamada idêntica já falhou neste run; corrija os argumentos ou mude de estratégia";
    private readonly HashSet<string> _failed = new(StringComparer.Ordinal);
    public int Repeats { get; private set; }

    public bool Contains(string key) => _failed.Contains(key);
    public void Record(string key) => _failed.Add(key);
    public void Repeated() => Repeats++;
    public void Invalidate() => _failed.Clear();

    public void Replay(IEnumerable<RuntimeEvent> events)
    {
        foreach (var e in events)
        {
            if (e.Kind == EventKind.ToolResult && e.Result == "ok"
                && e.Data?.GetValueOrDefault(InvalidateField) == "true") Invalidate();
            if (e.Kind == EventKind.ToolResult && e.Result == "error"
                && e.Data is { } d && d.TryGetValue(KeyField, out var key)) Record(key);
            if (e.Kind == EventKind.ToolDenied && e.Data is { } denied
                && denied.GetValueOrDefault(RepeatField) == "true") Repeated();
        }
    }

    // Only a digest is added to events: no second copy of private tool arguments.
    // Object order/whitespace/escaped strings do not create a different invocation.
    // Arrays, value types, string case and numeric representations remain distinct.
    public static string Key(ToolDescriptor tool, JsonElement input)
    {
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes))
        {
            writer.WriteStartArray();
            writer.WriteStringValue(tool.CapabilityId);
            writer.WriteStringValue(tool.Version);
            Canonical(writer, input);
            writer.WriteEndArray();
        }
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }

    private static void Canonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    Canonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) Canonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}
