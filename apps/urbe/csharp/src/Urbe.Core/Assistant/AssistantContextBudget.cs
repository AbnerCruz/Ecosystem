using System.Text;
using System.Text.Json;

namespace Urbe.Core.Assistant;

/// <summary>Conservative payload accounting. A provider tokenizer can override the estimate; no billing claims.</summary>
public static class AssistantContextBudget
{
    public sealed record Measurement(long Tokens, bool Estimated, long Window, int OutputReserve);

    public static long ConservativeTokens(AiRequest request)
    {
        // Full payload including system, tools, attachments and serialized tool arguments/results.
        // Counting one token per two UTF-8 bytes deliberately overestimates ordinary Latin text.
        var data = new
        {
            model = request.Model, system = request.System, tools = request.Tools,
            messages = request.Messages, max_tokens = request.MaxTokens, temperature = request.Temperature
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data);
        return (bytes.LongLength + 1) / 2 + 32L * (request.Messages.Count + 1);
    }

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
        long Count(AiRequest r) => Math.Max(0, exactCounter?.Invoke(r) ?? ConservativeTokens(r));
        if (BaseCost() >= usable)
            throw new InvalidOperationException("Sistema e ferramentas excedem a janela disponível. Selecione modelo com mais contexto ou reduza ferramentas/instruções.");

        long available = usable - BaseCost();
        // History initially uses at most three quarters of available token space; memory competes for the remainder.
        long historyChars = Math.Min(AssistantAgent.WorkingChars, available * 3 / 2);
        IReadOnlyList<string> memory = run.Memory ?? [];
        for (int attempt = 0; attempt < 12; attempt++)
        {
            var history = AssistantAgent.Prepare(settled, (int)Math.Min(int.MaxValue, Math.Max(0, historyChars)),
                run.OptimizeToolUse, metrics);
            var withoutMemory = new AiRequest(run.Model, history, plainSystem, tools, reserved, run.Temperature);
            long remaining = Math.Max(0, usable - Count(withoutMemory));
            // Conservative byte-to-char transport allowance; the final count below is authoritative.
            if (run.RecallMemory is not null)
                memory = run.RecallMemory((int)Math.Min(int.MaxValue, remaining));
            var system = run.BuildSystemWithMemory is not null
                ? run.BuildSystemWithMemory(memory) : plainSystem;
            var final = new AiRequest(run.Model, history, system, tools, reserved, run.Temperature);
            if (Count(final) <= usable) return final;
            historyChars = historyChars * 2 / 3;
        }
        throw new InvalidOperationException("O conteúdo da requisição excede a janela do modelo. O histórico permanece intacto; recupere as fontes em trechos menores.");
    }
}
