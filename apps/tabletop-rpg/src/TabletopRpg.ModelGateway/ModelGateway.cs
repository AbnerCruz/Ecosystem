namespace TabletopRpg.ModelGateway;

/// <summary>Only explicitly projected, participant-safe text may cross this boundary.</summary>
public enum ModelMessageRole
{
    System,
    User,
    Assistant
}

public sealed record ModelMessage(ModelMessageRole Role, string Content);

/// <summary>
/// Immutable snapshot of one event-triggered model turn. This type deliberately has
/// no reference to Campaign, WorldState, secrets or mutable domain entities.
/// </summary>
public sealed class ModelTurnRequest
{
    public ModelTurnRequest(string turnId, string responseContract, IEnumerable<ModelMessage> messages)
    {
        TurnId = Required(turnId, nameof(turnId), 128);
        ResponseContract = Required(responseContract, nameof(responseContract), 128);
        ArgumentNullException.ThrowIfNull(messages);

        var snapshot = messages.ToArray();
        if (snapshot.Length is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(messages), "A turn requires 1 to 64 messages.");

        var totalCharacters = 0;
        foreach (var message in snapshot)
        {
            if (message is null || !Enum.IsDefined(message.Role) || string.IsNullOrWhiteSpace(message.Content))
                throw new ArgumentException("Messages require valid roles and nonempty content.", nameof(messages));
            if (message.Content.Length > 16_384)
                throw new ArgumentException("Message exceeds the per-message limit.", nameof(messages));
            totalCharacters += message.Content.Length;
            if (totalCharacters > 65_536)
                throw new ArgumentException("Turn exceeds the total content limit.", nameof(messages));
        }

        Messages = Array.AsReadOnly(snapshot);
    }

    public string TurnId { get; }
    public string ResponseContract { get; }
    public IReadOnlyList<ModelMessage> Messages { get; }

    internal static string Required(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength)
            throw new ArgumentException("A nonempty bounded value is required.", name);
        return value;
    }
}

/// <summary>
/// Untrusted output; a later decision adapter must parse it as an intent,
/// and SessionEngine must authorize/execute that intent independently.
/// </summary>
/// <summary>
/// Optional provider-normalized usage. Absence means "unknown", never free.
/// BilledUsd is a measured/estimated amount from an adapter, not a limit.
/// </summary>
public sealed record ModelTurnUsage
{
    public ModelTurnUsage(long? inputTokens = null, long? outputTokens = null,
        decimal? billedUsd = null, bool isEstimate = false)
    {
        if (inputTokens is < 0 || outputTokens is < 0 || billedUsd is < 0)
            throw new ArgumentOutOfRangeException(nameof(inputTokens), "Usage cannot be negative.");
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        BilledUsd = billedUsd;
        IsEstimate = isEstimate;
    }

    public long? InputTokens { get; }
    public long? OutputTokens { get; }
    public decimal? BilledUsd { get; }
    public bool IsEstimate { get; }
}

public sealed class ModelTurnResponse
{
    public ModelTurnResponse(string text, ModelTurnUsage? usage = null)
    {
        Text = ModelTurnRequest.Required(text, nameof(text), 65_536);
        Usage = usage;
    }

    public string Text { get; }
    public ModelTurnUsage? Usage { get; }
}

/// <summary>
/// Product-local gateway port. No provider/network implementation or background loop
/// is constructed by this contract; only an explicit call can request a turn.
/// </summary>
public interface IModelGateway
{
    Task<ModelTurnResponse> GenerateAsync(
        ModelTurnRequest turn,
        CancellationToken cancellationToken = default);
}
