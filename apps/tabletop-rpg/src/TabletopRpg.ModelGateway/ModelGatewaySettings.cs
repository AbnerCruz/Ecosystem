namespace TabletopRpg.ModelGateway;

/// <summary>
/// Nonsecret provider configuration, outside the campaign serialization graph.
/// Only an environment VARIABLE NAME is retained, never its value.
/// </summary>
public sealed class ModelGatewaySettings
{
    public const string ProviderVariable = "TABLETOP_RPG_MODEL_PROVIDER";
    public const string ModelVariable = "TABLETOP_RPG_MODEL_ID";
    public const string EndpointVariable = "TABLETOP_RPG_MODEL_ENDPOINT";
    public const string CredentialNameVariable = "TABLETOP_RPG_MODEL_CREDENTIAL_ENV";

    public ModelGatewaySettings(
        string providerId,
        string modelId,
        Uri endpoint,
        string? credentialEnvironmentVariable = null)
    {
        ProviderId = ModelTurnRequest.Required(providerId, nameof(providerId), 128);
        ModelId = ModelTurnRequest.Required(modelId, nameof(modelId), 128);
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri ||
            (endpoint.Scheme != Uri.UriSchemeHttps &&
             !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)) ||
            endpoint.UserInfo.Length != 0 ||
            endpoint.Query.Length != 0 ||
            endpoint.Fragment.Length != 0)
            throw new ArgumentException(
                "Model endpoint must be HTTPS or loopback HTTP, without credentials, query or fragment.",
                nameof(endpoint));

        if (credentialEnvironmentVariable is not null &&
            (credentialEnvironmentVariable.Length is < 1 or > 128 ||
             !IsEnvironmentVariableName(credentialEnvironmentVariable)))
            throw new ArgumentException("Invalid credential variable name.", nameof(credentialEnvironmentVariable));

        Endpoint = endpoint;
        CredentialEnvironmentVariable = credentialEnvironmentVariable;
    }

    public string ProviderId { get; }
    public string ModelId { get; }
    public Uri Endpoint { get; }
    public string? CredentialEnvironmentVariable { get; }

    /// <summary>Read only nonsecret configuration; never read the credential here.</summary>
    public static ModelGatewaySettings FromEnvironment(Func<string, string?>? read = null)
    {
        read ??= Environment.GetEnvironmentVariable;
        var provider = read(ProviderVariable);
        var model = read(ModelVariable);
        var endpointText = read(EndpointVariable);
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("Model endpoint is missing or invalid.");
        return new ModelGatewaySettings(
            ModelTurnRequest.Required(provider, ProviderVariable, 128),
            ModelTurnRequest.Required(model, ModelVariable, 128),
            endpoint,
            read(CredentialNameVariable) is { Length: > 0 } credentialName ? credentialName : null);
    }

    private static bool IsEnvironmentVariableName(string name)
    {
        if (!(name[0] is >= 'A' and <= 'Z' or '_')) return false;
        return name.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
    }
}

/// <summary>Resolves ephemeral credentials at use-time; does not write them anywhere.</summary>
public interface IModelCredentialSource
{
    string? Resolve(ModelGatewaySettings settings);
}

public sealed class EnvironmentModelCredentialSource : IModelCredentialSource
{
    private readonly Func<string, string?> _read;

    public EnvironmentModelCredentialSource(Func<string, string?>? read = null)
    {
        _read = read ?? Environment.GetEnvironmentVariable;
    }

    public string? Resolve(ModelGatewaySettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.CredentialEnvironmentVariable is null) return null;

        var credential = _read(settings.CredentialEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(credential))
            throw new InvalidOperationException("Configured model credential is unavailable.");

        return credential;
    }
}
