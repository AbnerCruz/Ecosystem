using System.Collections.Frozen;
using System.Text.Json;

namespace Lunet.Core.Capabilities;

public sealed record LunetContextStep(string Level, string Id);

/// <summary>Context canônico: mesmo vocabulário/ordem de context.schema.json.</summary>
public sealed class LunetHostContext
{
    private static readonly string[] Levels = ["ecosystem", "product", "project", "workspace", "tool"];
    public IReadOnlyList<LunetContextStep> Path { get; }

    public LunetHostContext(IEnumerable<LunetContextStep> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var steps = path.ToArray();
        if (steps.Length == 0 || steps.Any(s => s is null) || steps[0].Level != "ecosystem")
            throw new ArgumentException("Context precisa iniciar em ecosystem.", nameof(path));

        var previous = -1;
        foreach (var step in steps)
        {
            var order = Array.IndexOf(Levels, step.Level);
            if (order <= previous || string.IsNullOrWhiteSpace(step.Id))
                throw new ArgumentException("Context inválido.", nameof(path));
            previous = order;
        }

        Path = Array.AsReadOnly(steps);
    }

    public static LunetHostContext ForProject(LunetProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(project.Manifest.GameId))
            throw new ArgumentException("Projeto sem GameId estável.", nameof(project));

        return new LunetHostContext([
            new("ecosystem", "ecosystem"),
            new("product", "lunet2d"),
            new("project", project.Manifest.GameId)
        ]);
    }

    public bool Contains(LunetHostContext child) => Path.Count <= child.Path.Count
        && Path.SequenceEqual(child.Path.Take(Path.Count));
}

public sealed record LunetCapabilityDescriptor(
    string Capability,
    string Provider,
    Version Version,
    string Lifecycle,
    IReadOnlySet<string> RequiredPermissions);

public sealed record LunetHostResponse(JsonElement? Output, string? HostError, string? CapabilityError)
{
    public bool Succeeded => HostError is null && CapabilityError is null;
    public string? Error => HostError ?? CapabilityError;

    public static LunetHostResponse Success(JsonElement output) => new(output, null, null);
    public static LunetHostResponse HostFailure(string code) => new(null, code, null);
    public static LunetHostResponse CapabilityFailure(string code) => new(null, null, code);
}

public sealed class LunetHostException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class LunetCapabilityInvocation
{
    public LunetHostContext Context { get; }
    public JsonElement Input { get; }

    internal LunetCapabilityInvocation(LunetHostContext context, JsonElement input)
        => (Context, Input) = (context, input);
}

/// <summary>Definição confiável de uma capability dentro do Host do Lunet.</summary>
public sealed class LunetHostCapability
{
    public string Id { get; }
    public string Provider { get; }
    public Version Version { get; }
    public LunetHostContext Scope { get; }
    public IReadOnlySet<string> RequiredPermissions { get; }
    public string Lifecycle { get; }
    internal Func<JsonElement, bool> ValidateInput { get; }
    internal Func<JsonElement, bool> ValidateOutput { get; }
    internal Func<LunetCapabilityInvocation, CancellationToken, Task<JsonElement>> Handler { get; }

    public LunetHostCapability(
        string id,
        string provider,
        Version version,
        LunetHostContext scope,
        IEnumerable<string> requiredPermissions,
        string lifecycle,
        Func<JsonElement, bool> validateInput,
        Func<JsonElement, bool> validateOutput,
        Func<LunetCapabilityInvocation, CancellationToken, Task<JsonElement>> handler)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(requiredPermissions);
        ArgumentNullException.ThrowIfNull(validateInput);
        ArgumentNullException.ThrowIfNull(validateOutput);
        ArgumentNullException.ThrowIfNull(handler);

        if (string.IsNullOrWhiteSpace(id) || !id.Contains('.') || string.IsNullOrWhiteSpace(provider)
            || version.Build < 0 || version.Revision >= 0)
            throw new ArgumentException("Capability inválida.");
        if (lifecycle is not ("stateless" or "session" or "long-running"))
            throw new ArgumentException("Lifecycle inválido.", nameof(lifecycle));

        Id = id;
        Provider = provider;
        Version = version;
        Scope = scope;
        RequiredPermissions = requiredPermissions.ToFrozenSet(StringComparer.Ordinal);
        Lifecycle = lifecycle;
        ValidateInput = validateInput;
        ValidateOutput = validateOutput;
        Handler = handler;
    }
}

/// <summary>
/// Host API em processo do Product Shell Lunet. Não depende de Hub.Core e não conhece Binder.
/// </summary>
public sealed class LunetCapabilityHost
{
    private readonly LunetHostCapability[] _capabilities;
    private readonly IReadOnlySet<string> _declaredPermissions;

    public LunetCapabilityHost(
        IEnumerable<LunetHostCapability> capabilities,
        IEnumerable<string> declaredPermissions)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(declaredPermissions);
        _capabilities = capabilities.ToArray();
        if (_capabilities.GroupBy(c => (c.Id, c.Version)).Any(g => g.Count() > 1))
            throw new ArgumentException("Registry ambíguo.", nameof(capabilities));
        _declaredPermissions = declaredPermissions.ToFrozenSet(StringComparer.Ordinal);
    }

    public static LunetCapabilityHost CreateDefault() => new(
        [LunetTextInspectionCapability.Definition()],
        []);

    public LunetHostSession Open(string identity, LunetHostContext context, IEnumerable<string> grants)
    {
        if (string.IsNullOrWhiteSpace(identity)) throw new LunetHostException("IDENTITY_REQUIRED");
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(grants);

        var captured = grants.Where(_declaredPermissions.Contains).ToArray();
        return new LunetHostSession(identity, context, _capabilities, captured);
    }

    public LunetHostSession OpenForProject(LunetProject project, string identity = "lunet-user") =>
        Open(identity, LunetHostContext.ForProject(project), []);
}

public sealed class LunetHostSession : IDisposable
{
    private readonly object _gate = new();
    private readonly string _identity;
    private readonly LunetHostContext _context;
    private readonly LunetHostCapability[] _capabilities;
    private readonly HashSet<string> _grants;
    private bool _closed;
    private CancellationTokenSource? _active;
    private IReadOnlySet<string> _activePermissions = new HashSet<string>(StringComparer.Ordinal);
    private bool _activeRevoked;

    internal LunetHostSession(
        string identity,
        LunetHostContext context,
        LunetHostCapability[] capabilities,
        IEnumerable<string> grants)
    {
        _identity = identity;
        _context = context;
        _capabilities = capabilities;
        _grants = new HashSet<string>(grants, StringComparer.Ordinal);
    }

    public string Identity => _identity;
    public LunetHostContext Context => _context;

    public IReadOnlyList<LunetCapabilityDescriptor> Discover()
    {
        lock (_gate)
        {
            if (_closed) return [];
            return _capabilities
                .Where(Available)
                .OrderBy(c => c.Id, StringComparer.Ordinal)
                .ThenByDescending(c => c.Version)
                .Select(c => new LunetCapabilityDescriptor(
                    c.Id, c.Provider, c.Version, c.Lifecycle, c.RequiredPermissions))
                .ToArray();
        }
    }

    public async Task<LunetHostResponse> InvokeAsync(
        string capability,
        Version minimumVersion,
        JsonElement input,
        CancellationToken cancellationToken = default)
    {
        LunetHostCapability selected;
        CancellationTokenSource operation;

        lock (_gate)
        {
            if (_closed) return LunetHostResponse.HostFailure("SESSION_CLOSED");

            var available = _capabilities
                .Where(c => c.Id == capability && Available(c))
                .OrderByDescending(c => c.Version)
                .ToArray();
            if (available.Length == 0)
                return LunetHostResponse.HostFailure("CAPABILITY_UNAVAILABLE");

            selected = available.FirstOrDefault(c => Compatible(c.Version, minimumVersion))!;
            if (selected is null)
                return LunetHostResponse.HostFailure("VERSION_UNSUPPORTED");

            if (!selected.ValidateInput(input))
                return LunetHostResponse.CapabilityFailure("INVALID_INPUT");

            if (_active is not null)
                return LunetHostResponse.HostFailure("CAPABILITY_UNAVAILABLE");

            operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _active = operation;
            _activePermissions = selected.RequiredPermissions;
            _activeRevoked = false;
        }

        try
        {
            var output = await selected.Handler(
                new LunetCapabilityInvocation(_context, input),
                operation.Token).ConfigureAwait(false);

            lock (_gate)
            {
                if (_activeRevoked) return LunetHostResponse.HostFailure("REVOKED");
                if (_closed || operation.IsCancellationRequested)
                    return LunetHostResponse.HostFailure("CANCELLED");
            }

            if (!selected.ValidateOutput(output))
                return LunetHostResponse.HostFailure("PROVIDER_CONTRACT_VIOLATION");

            return LunetHostResponse.Success(output);
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
                return LunetHostResponse.HostFailure(_activeRevoked ? "REVOKED" : "CANCELLED");
        }
        catch (Exception)
        {
            return LunetHostResponse.CapabilityFailure("EXECUTION_FAILED");
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_active, operation))
                {
                    _active = null;
                    _activePermissions = new HashSet<string>(StringComparer.Ordinal);
                    _activeRevoked = false;
                }
            }
            operation.Dispose();
        }
    }

    public bool Revoke(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        var requested = permissions.ToArray();
        CancellationTokenSource? cancel = null;
        var changed = false;

        lock (_gate)
        {
            if (_closed) return false;
            foreach (var permission in requested)
                changed |= _grants.Remove(permission);

            if (changed && _active is not null && requested.Any(_activePermissions.Contains))
            {
                _activeRevoked = true;
                cancel = _active;
            }
        }

        cancel?.Cancel();
        return changed;
    }

    public void Dispose()
    {
        CancellationTokenSource? cancel = null;
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
            cancel = _active;
        }
        cancel?.Cancel();
    }

    private bool Available(LunetHostCapability capability) =>
        capability.Scope.Contains(_context)
        && capability.RequiredPermissions.All(_grants.Contains);

    private static bool Compatible(Version offered, Version minimum)
    {
        ArgumentNullException.ThrowIfNull(minimum);
        if (offered.Major == 0 || minimum.Major == 0) return offered == minimum;
        return offered.Major == minimum.Major && offered.CompareTo(minimum) >= 0;
    }
}
