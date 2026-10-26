using System.Text;
using System.Text.Json;

namespace Urbe.Core.Assistant;

/// <summary>Conservative payload accounting. A provider tokenizer can override the estimate; no billing claims.</summary>
public static class AssistantContextBudget
{
    public sealed record Measurement(long Tokens, bool Estimated, long Window, int OutputReserve);

    public static long ConservativeTokens(AiRequest request, AiProviderConfig? provider = null)
    {
        // Count the actual serialized provider body when the protocol is known (no network call).
        // UTF-8 bytes / 2 + message overhead is a conservative ESTIMATE, not a tokenizer or price.
        byte[] bytes;
        if (provider is not null)
        {
            var resolved = AssistantProviders.Resolve(provider);
            bytes = resolved.Kind switch
            {
                "anthropic" => System.Text.Encoding.UTF8.GetBytes(AssistantProviders.AnthropicWire(request).ToJsonString()),
                "openai" => System.Text.Encoding.UTF8.GetBytes(AssistantProviders.OpenAiWire(request, resolved).ToJsonString()),
                _ => GenericBytes(request)
            };
        }
        else bytes = GenericBytes(request);
        return (bytes.LongLength + 1) / 2 + 32L * (request.Messages.Count + 1);
    }

    private static byte[] GenericBytes(AiRequest request) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        model = request.Model, system = request.System, tools = request.Tools,
        messages = request.Messages, max_tokens = request.MaxTokens, temperature = request.Temperature
    });

    /// <summary>Prepares one bounded request using one joint budget, never a separate memory reservation.</summary>
    public static AiRequest Build(AiAgentRun run, string plainSystem, IReadOnlyList<AiToolSchema> tools,
        IReadOnlyList<AiChatMessage> messages, AiExecutionMetrics? metrics = null,
        Func<AiRequest, long?>? exactCounter = null)
    {
        long window = run.ContextTokens is > 0 ? run.ContextTokens.Value : 128000;
        int requested = run.MaxTokens is > 0 ? run.MaxTokens.Value : 8192;
        int reserved = (int)Math.Min(requested, Math.Max(1, window / 4));
        if (window <= reserved + 128) throw new InvalidOperationException("A janela configurada não comporta instruções e resposta.");
        long usable = window - reserved;
        var settled = AssistantAgent.SettlePast(messages);
        var baseline = new AiRequest(run.Model, [], plainSystem, tools, reserved, run.Temperature);
        long BaseCost() => Count(baseline);
        long Count(AiRequest r) => Math.Max(0, exactCounter?.Invoke(r) ?? ConservativeTokens(r, run.Provider));
        if (BaseCost() >= usable)
            throw new InvalidOperationException("Sistema e ferramentas excedem a janela disponível. Selecione modelo com mais contexto ou reduza ferramentas/instruções.");

        long available = usable - BaseCost();
        // History initially uses at most three quarters of available token space; memory competes for the remainder.
        long historyChars = Math.Min(AssistantAgent.WorkingChars, available * 3 / 2);
        IReadOnlyList<string> memory = run.Memory ?? [];
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var passMetrics = new AiExecutionMetrics();
            var history = AssistantAgent.Prepare(settled, (int)Math.Min(int.MaxValue, Math.Max(0, historyChars)),
                run.OptimizeToolUse, passMetrics);
            var withoutMemory = new AiRequest(run.Model, history, plainSystem, tools, reserved, run.Temperature);
            long remaining = Math.Max(0, usable - Count(withoutMemory));
            // Conservative byte-to-char transport allowance; the final count below is authoritative.
            if (run.RecallMemory is not null)
                memory = run.RecallMemory((int)Math.Min(int.MaxValue, remaining));
            var system = run.BuildSystemWithMemory is not null
                ? run.BuildSystemWithMemory(memory) : plainSystem;
            var final = new AiRequest(run.Model, history, system, tools, reserved, run.Temperature);
            if (Count(final) <= usable)
            {
                if (metrics is not null) metrics.ContextCharsSaved += passMetrics.ContextCharsSaved;
                return final;
            }
            historyChars = historyChars * 2 / 3;
        }
        throw new InvalidOperationException("O conteúdo da requisição excede a janela do modelo. O histórico permanece intacto; recupere as fontes em trechos menores.");
    }
}
