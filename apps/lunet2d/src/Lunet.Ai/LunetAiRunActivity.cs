using AgentRuntime;

namespace Lunet.Ai;

/// <summary>User-visible execution bounds. The shared engine also stops equivalent failures and no-progress loops.</summary>
public static class LunetAiRunOptions
{
    public const int DefaultMaxSteps = 32;
    public const int MaxAllowedSteps = 128;

    public static int ValidateMaxSteps(int value)
    {
        if (value is not (16 or 32 or 64 or 128))
            throw new ArgumentOutOfRangeException(nameof(value),
                "Escolha 16, 32, 64 ou 128 interações de modelo.");
        return value;
    }
}

/// <summary>
/// Safe projection of shared runtime events: only event type, tool ID and generic result.
/// Private file content, tool arguments, model payloads and credentials never reach this view.
/// </summary>
public static class LunetAiRunActivity
{
    public static string? Describe(RuntimeEvent entry) => entry.Kind switch
    {
        EventKind.ModelResponded => "Modelo respondeu",
        EventKind.ToolCalled => "Ferramenta: " + entry.Tool,
        EventKind.ToolResult => (entry.Result == "ok" ? "Concluída: " : "Falhou: ") + entry.Tool,
        EventKind.ToolDenied => "Negada: " + entry.Tool,
        EventKind.ProviderFailed => "Falha no provedor: " + entry.Result,
        EventKind.VerificationFailed => "Verificação não aprovada",
        EventKind.RunBlocked => "Execução bloqueada: " + entry.Result,
        EventKind.RunSucceeded => "Execução concluída e verificada",
        EventKind.RunCancelled => "Execução cancelada",
        EventKind.RunFailed => "Execução falhou",
        _ => null
    };

    public static IReadOnlyList<string> Summarize(IEnumerable<RuntimeEvent> events, int limit = 20)
    {
        if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
        return events.Select(Describe).Where(s => s is not null).Select(s => s!)
            .TakeLast(limit).ToArray();
    }
}
