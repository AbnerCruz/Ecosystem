using System.Collections.Frozen;
using System.Text.Json;

namespace Hub.Core.Capabilities;

/// <summary>API experimental local, versão 0. Não é o protocolo público nem IPC.</summary>
public static class LocalProtocol
{
    public const string Id = "ecosystem-local/0";
}

public sealed record ContextStep(string Level, string Id);

/// <summary>Mesmo vocabulário e ordem de context.schema.json; captura imutável.</summary>
public sealed class LocalContext
{
    private static readonly string[] Levels = ["ecosystem", "product", "project", "workspace", "tool"];
    public IReadOnlyList<ContextStep> Path { get; }

    public LocalContext(IEnumerable<ContextStep> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var steps = path.ToArray();
        if (steps.Length == 0 || steps.Any(s => s is null) || steps[0].Level != "ecosystem") throw new ArgumentException("Context precisa de ecosystem.");
        var previous = -1;
        foreach (var step in steps)
        {
            var order = Array.IndexOf(Levels, step.Level);
            if (order <= previous || string.IsNullOrWhiteSpace(step.Id)) throw new ArgumentException("Context inválido.");
            previous = order;
        }
        Path = Array.AsReadOnly(steps);
    }

    public bool Contains(LocalContext child) => Path.Count <= child.Path.Count
        && Path.SequenceEqual(child.Path.Take(Path.Count));
}

public sealed record LocalEnvelope(string Protocol, string Id, string Source, string Kind,
    string Capability, string Operation, Version MinimumVersion, JsonElement Input);

public sealed record LocalFrame(long Sequence, string Kind, string RequestId, string Source,
    string Capability, LocalContext Context, string? Error = null, int? Percent = null, string? Event = null);

public sealed record LocalResponse(string RequestId, JsonElement? Output, string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>Definição confiável do Host; chamada nunca fornece permissões próprias.</summary>
public sealed class LocalCapability
{
    public string Id { get; }
    public string Provider { get; }
    public Version Version { get; }
    public string Operation { get; }
    public LocalContext Scope { get; }
    public IReadOnlySet<string> RequiredPermissions { get; }
    internal Func<JsonElement, bool> ValidateInput { get; }
    internal Func<LocalInvocation, CancellationToken, Task<JsonElement>> Handler { get; }

    public LocalCapability(string id, string provider, Version version, string operation,
        LocalContext scope, IEnumerable<string> permissions, Func<JsonElement, bool> validateInput,
        Func<LocalInvocation, CancellationToken, Task<JsonElement>> handler)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(permissions);
        if (string.IsNullOrWhiteSpace(id) || !id.Contains('.') || string.IsNullOrWhiteSpace(provider)
            || string.IsNullOrWhiteSpace(operation) || version.Build < 0 || version.Revision >= 0)
            throw new ArgumentException("Capability inválida.");
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(validateInput);
        ArgumentNullException.ThrowIfNull(handler);
        Id = id; Provider = provider; Version = version; Operation = operation; Scope = scope;
        RequiredPermissions = permissions.ToFrozenSet(StringComparer.Ordinal);
        ValidateInput = validateInput; Handler = handler;
    }
}

public sealed class LocalInvocation
{
    public LocalContext Context { get; }
    public JsonElement Input { get; }
    private readonly Action<int> _progress;
    internal LocalInvocation(LocalContext context, JsonElement input, Action<int> progress)
        => (Context, Input, _progress) = (context, input, progress);
    public void Progress(int percent)
    {
        if (percent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percent));
        _progress(percent);
    }
}
