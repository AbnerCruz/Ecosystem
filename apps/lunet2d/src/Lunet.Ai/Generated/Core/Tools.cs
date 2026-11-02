using System.Text.Json;

namespace AgentRuntime;

/// <summary>Classe de risco de uma ferramenta, herdada da IA que já existe: leitura, escrita e destrutiva (a destrutiva exige aprovação).</summary>
public enum RiskClass { Read, Write, Destructive }

/// <summary>
/// Uma Tool de agente É uma Capability (NN-006, plano §5.2): id estável, versão, permissões exigidas, risco e escopo. Não existe
/// "tool de IA" paralela.
/// </summary>
/// <param name="Scope">A ferramenta só existe dentro deste Context (e abaixo dele).</param>
/// <param name="Idempotent">Pode ser reexecutada com segurança na retomada de um run interrompido no meio da chamada.</param>
public sealed record ToolDescriptor(
    string CapabilityId,
    string Version,
    string Description,
    string InputSchemaJson,
    IReadOnlySet<string> RequiredPermissions,
    RiskClass Risk,
    ContextPath Scope,
    bool Idempotent);

public sealed record ToolInvocation(string CallId, string Capability, JsonElement Arguments);

/// <summary>Resultado de uma ferramenta. Falha esperada é <c>IsError</c> com mensagem; exceção é reservada a defeito.</summary>
public sealed record ToolOutcome(bool IsError, string Content, IReadOnlyList<ArtifactRef> Artifacts)
{
    public static ToolOutcome Ok(string content, params ArtifactRef[] artifacts) => new(false, content, artifacts);
    public static ToolOutcome Error(string content) => new(true, content, []);
}

public sealed record ToolContext(string RunId, AgentIdentity Agent, ContextPath Context);

public interface ITool
{
    ToolDescriptor Descriptor { get; }

    ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken);
}

/// <summary>As capabilities que o Host oferece. Não decide quem as usa: isso é do <see cref="ToolResolver"/>.</summary>
public sealed class ToolHost
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.Ordinal);

    public ToolHost Register(ITool tool)
    {
        var id = tool.Descriptor.CapabilityId;
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Tool sem capability id.", nameof(tool));
        if (!_tools.TryAdd(id, tool)) throw new InvalidOperationException($"Capability '{id}' já registrada no Host.");
        return this;
    }

    public IReadOnlyList<ITool> All => _tools.Values.OrderBy(t => t.Descriptor.CapabilityId, StringComparer.Ordinal).ToList();
}

/// <summary>O que um nível de governança (organização, projeto ou agente) permite. Vazio permite nada (deny-by-default).</summary>
public sealed record ToolGrant(IReadOnlySet<string> Capabilities, IReadOnlySet<string> Permissions)
{
    public static ToolGrant None { get; } = new(new HashSet<string>(), new HashSet<string>());

    public static ToolGrant Of(IEnumerable<string> capabilities, IEnumerable<string> permissions) =>
        new(new HashSet<string>(capabilities, StringComparer.Ordinal), new HashSet<string>(permissions, StringComparer.Ordinal));
}

public sealed record ToolDenial(string CapabilityId, string Reason);

public sealed record ResolvedTools(IReadOnlyList<ITool> Tools, IReadOnlyList<ToolDenial> Denied)
{
    public ITool? Find(string capabilityId) => Tools.FirstOrDefault(t => t.Descriptor.CapabilityId == capabilityId);
}

/// <summary>
/// Ferramentas disponíveis = capabilities do Host ∩ permissões da organização ∩ do projeto ∩ do agente (plano §5.5). Função
/// pura e testável. Sem <c>agent.act</c> no conjunto efetivo, nenhuma ferramenta: agir nunca é herdado.
/// </summary>
public static class ToolResolver
{
    public static ResolvedTools Resolve(ToolHost host, ToolGrant organization, ToolGrant project, ToolGrant agent, ContextPath runContext)
    {
        var effective = new HashSet<string>(organization.Permissions, StringComparer.Ordinal);
        effective.IntersectWith(project.Permissions);
        effective.IntersectWith(agent.Permissions);

        var allowed = new List<ITool>();
        var denied = new List<ToolDenial>();
        foreach (var tool in host.All)
        {
            var d = tool.Descriptor;
            var reason = Deny(d, organization, project, agent, effective, runContext);
            if (reason is null) allowed.Add(tool); else denied.Add(new ToolDenial(d.CapabilityId, reason));
        }
        return new ResolvedTools(allowed, denied);
    }

    private static string? Deny(ToolDescriptor d, ToolGrant org, ToolGrant project, ToolGrant agent, HashSet<string> effective, ContextPath runContext)
    {
        if (!effective.Contains(Permissions.AgentAct)) return $"permissão '{Permissions.AgentAct}' ausente no conjunto efetivo";
        if (!org.Capabilities.Contains(d.CapabilityId)) return "fora das capabilities da organização";
        if (!project.Capabilities.Contains(d.CapabilityId)) return "fora das capabilities do projeto";
        if (!agent.Capabilities.Contains(d.CapabilityId)) return "fora das capabilities do agente";
        foreach (var required in d.RequiredPermissions.Order(StringComparer.Ordinal))
            if (!effective.Contains(required)) return $"permissão '{required}' ausente no conjunto efetivo";
        if (!d.Scope.Contains(runContext)) return $"fora do escopo de Context ({d.Scope})";
        return null;
    }
}
