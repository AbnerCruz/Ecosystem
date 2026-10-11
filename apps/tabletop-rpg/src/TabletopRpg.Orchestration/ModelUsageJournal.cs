using TabletopRpg.ModelGateway;

namespace TabletopRpg.Orchestration;

/// <summary>
/// Model- and provider-neutral usage. Nothing here predicts provider bills;
/// no hourly, daily, monthly or total spending ceiling is applied.
/// </summary>
public sealed record ModelUsageReceipt(
    string TurnId,
    string ProviderId,
    string ModelId,
    long? InputTokens,
    long? OutputTokens,
    decimal? BilledUsd,
    bool IsEstimate,
    bool Completed);

/// <summary>
/// Decorate the IModelGateway chosen by the host. No second provider registry,
/// no transport or storage, and no secrets/prompt/response text in receipts.
/// </summary>
public sealed class ObservableModelGateway : IModelGateway
{
    private readonly IModelGateway _inner;
    private readonly string _provider;
    private readonly string _model;
    private readonly object _gate = new();
    private readonly List<ModelUsageReceipt> _receipts = [];

    public ObservableModelGateway(IModelGateway inner, string providerId, string modelId)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (string.IsNullOrWhiteSpace(providerId) || providerId.Length > 128 ||
            string.IsNullOrWhiteSpace(modelId) || modelId.Length > 128)
            throw new ArgumentException("Host must provide bounded provider and model identifiers.");
        _provider = providerId.Trim();
        _model = modelId.Trim();
    }

    public IReadOnlyList<ModelUsageReceipt> Receipts
    {
        get { lock (_gate) return Array.AsReadOnly(_receipts.ToArray()); }
    }

    public async Task<ModelTurnResponse> GenerateAsync(ModelTurnRequest turn,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(turn);
        try
        {
            var response = await _inner.GenerateAsync(turn, cancellationToken)
                .ConfigureAwait(false);
            var usage = response.Usage;
            Append(new ModelUsageReceipt(turn.TurnId, _provider, _model,
                usage?.InputTokens, usage?.OutputTokens, usage?.BilledUsd,
                usage?.IsEstimate ?? false, true));
            return response;
        }
        catch
        {
            // A provider may have billed an unsuccessful call. Never claim zero
            // tokens or cost when no authoritative receipt was received.
            Append(new ModelUsageReceipt(turn.TurnId, _provider, _model,
                null, null, null, false, false));
            throw;
        }
    }

    private void Append(ModelUsageReceipt receipt)
    {
        lock (_gate) _receipts.Add(receipt);
    }
}
