using System.Text.Json;

namespace AgentRuntime;

/// <summary>O que um modelo declara saber fazer (plano §5.2): o agente referencia um perfil, nunca um fornecedor.</summary>
public sealed record ModelCapabilities(bool Text, bool Vision, bool Tools, bool StructuredOutput, bool Streaming, int ContextWindow);

/// <param name="ProviderId">Id do provedor no <see cref="ProviderRegistry"/>; desconhecido falha.</param>
/// <param name="MaxCostPerCall">Teto de custo de uma chamada, reservado no ledger antes de chamar.</param>
public sealed record ModelProfile(string ProviderId, string Model, ModelCapabilities Capabilities, Money MaxCostPerCall);

/// <summary>Formato normalizado de conteúdo: o agente só conhece isto, nunca o formato de um fornecedor.</summary>
public abstract record ContentBlock;

public sealed record TextBlock(string Text) : ContentBlock;

public sealed record ToolUseBlock(string Id, string Name, JsonElement Input) : ContentBlock;

public sealed record ToolResultBlock(string ToolUseId, string Content, bool IsError) : ContentBlock;

/// <summary>
/// Raciocínio privado que um provedor eventualmente devolva. O runtime o DESCARTA na entrada: nunca é registrado, devolvido ao
/// modelo, nem exigido (plano §8.3). Existe como tipo só para a descarga ser explícita e testável.
/// </summary>
public sealed record ReasoningBlock(string Text) : ContentBlock;

public enum MessageRole { User, Assistant }

public sealed record Message(MessageRole Role, IReadOnlyList<ContentBlock> Content)
{
    public static Message User(string text) => new(MessageRole.User, [new TextBlock(text)]);
}

public sealed record ToolSpec(string Name, string Description, string InputSchemaJson);

public sealed record ModelRequest(ModelProfile Profile, string System, IReadOnlyList<Message> Messages, IReadOnlyList<ToolSpec> Tools);

public enum StopReason { EndTurn, ToolUse, MaxTokens }

public sealed record Usage(long InputTokens, long OutputTokens, Money Cost);

public sealed record ModelResponse(IReadOnlyList<ContentBlock> Content, StopReason Stop, Usage Usage);

/// <summary>
/// Porta de provedor de modelo (ADR-0017 D1). Implementações concretas (comerciais, locais) vivem FORA do Core, como adapters
/// substituíveis. Contrato: devolve <see cref="ModelResponse"/> ou lança <see cref="ProviderException"/> — nunca uma exceção
/// de fornecedor — e respeita o <see cref="CancellationToken"/>.
/// </summary>
public interface IModelProvider
{
    string ProviderId { get; }

    ValueTask<ModelResponse> CompleteAsync(ModelRequest request, CancellationToken cancellationToken);
}

/// <summary>Falha de provedor, com tipo estável (entra na impressão digital de falhas equivalentes) e se vale tentar de novo.</summary>
public sealed class ProviderException(string kind, string message, bool isTransient) : Exception(message)
{
    public string Kind { get; } = kind;
    public bool IsTransient { get; } = isTransient;
}

public sealed class UnknownProviderException(string providerId) : Exception($"Provedor desconhecido: '{providerId}'.")
{
    public string ProviderId { get; } = providerId;
}

/// <summary>Resolve provedores por id. Falha fechada: provedor não registrado é erro, nunca um padrão silencioso.</summary>
public sealed class ProviderRegistry
{
    private readonly Dictionary<string, IModelProvider> _providers = new(StringComparer.Ordinal);

    public ProviderRegistry Register(IModelProvider provider)
    {
        if (string.IsNullOrWhiteSpace(provider.ProviderId)) throw new ArgumentException("Provedor sem id.", nameof(provider));
        if (!_providers.TryAdd(provider.ProviderId, provider))
            throw new InvalidOperationException($"Provedor '{provider.ProviderId}' já registrado.");
        return this;
    }

    public IModelProvider Resolve(string providerId) =>
        _providers.TryGetValue(providerId, out var p) ? p : throw new UnknownProviderException(providerId);
}
