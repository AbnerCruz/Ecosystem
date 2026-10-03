using System.Globalization;

namespace AgentRuntime;

/// <summary>
/// Dinheiro em unidade mínima inteira (centavos), nunca ponto flutuante (plano §8.1). Operações são checadas: estouro
/// numérico falha em vez de virar um número errado, e moedas diferentes não se misturam.
/// </summary>
public readonly record struct Money(long Minor, string Currency) : IComparable<Money>
{
    public static Money Zero(string currency) => new(0, currency);

    public bool IsNegative => Minor < 0;

    public static Money operator +(Money a, Money b) { Same(a, b); return new(checked(a.Minor + b.Minor), a.Currency); }
    public static Money operator -(Money a, Money b) { Same(a, b); return new(checked(a.Minor - b.Minor), a.Currency); }
    public static bool operator <(Money a, Money b) => a.CompareTo(b) < 0;
    public static bool operator >(Money a, Money b) => a.CompareTo(b) > 0;
    public static bool operator <=(Money a, Money b) => a.CompareTo(b) <= 0;
    public static bool operator >=(Money a, Money b) => a.CompareTo(b) >= 0;

    public int CompareTo(Money other) { Same(this, other); return Minor.CompareTo(other.Minor); }

    public static Money Min(Money a, Money b) => a <= b ? a : b;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Minor} {Currency} (unidade mínima)");

    private static void Same(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, StringComparison.Ordinal))
            throw new InvalidOperationException($"Moedas diferentes não se misturam: {a.Currency} e {b.Currency}.");
    }
}

/// <summary>Níveis do contrato de Context (docs/contracts/schemas/context.schema.json), em ordem estrita.</summary>
public enum ContextLevel { Ecosystem = 0, Product = 1, Project = 2, Workspace = 3, Tool = 4 }

public sealed record ContextStep(ContextLevel Level, string Id);

/// <summary>
/// Context: o escopo hierárquico em que um agente trabalha (ecosystem → product → project → workspace → tool). Reutiliza o
/// contrato existente; nunca cria referência Product → Product. Pode-se parar em qualquer nível, mas não pular para trás nem repetir.
/// </summary>
public sealed class ContextPath : IEquatable<ContextPath>
{
    private readonly string _text;

    public IReadOnlyList<ContextStep> Steps { get; }

    private ContextPath(IReadOnlyList<ContextStep> steps)
    {
        Steps = steps;
        _text = string.Join(" > ", steps.Select(s => $"{s.Level.ToString().ToLowerInvariant()}:{s.Id}"));
    }

    public static ContextPath Create(params ContextStep[] steps)
    {
        if (steps is null || steps.Length == 0) throw new ArgumentException("Um Context precisa de ao menos um nível.", nameof(steps));
        for (var i = 0; i < steps.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(steps[i].Id)) throw new ArgumentException("Todo nível de Context tem id.", nameof(steps));
            if (i > 0 && steps[i].Level <= steps[i - 1].Level)
                throw new ArgumentException($"Níveis de Context em ordem estrita: '{steps[i].Level}' não pode vir depois de '{steps[i - 1].Level}'.", nameof(steps));
        }
        return new ContextPath(steps.ToArray());
    }

    /// <summary>Este escopo contém <paramref name="other"/> (igual ou mais específico, no mesmo caminho).</summary>
    public bool Contains(ContextPath other)
    {
        if (other.Steps.Count < Steps.Count) return false;
        for (var i = 0; i < Steps.Count; i++)
            if (Steps[i] != other.Steps[i]) return false;
        return true;
    }

    public bool Equals(ContextPath? other) => other is not null && string.Equals(_text, other._text, StringComparison.Ordinal);
    public override bool Equals(object? obj) => Equals(obj as ContextPath);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_text);
    public override string ToString() => _text;
}

/// <summary>Identidade estável de um agente (NN-019): o nome de exibição pode mudar, o id não.</summary>
public sealed record AgentIdentity(string Id, string DisplayName, string Role);

/// <summary>Permissões do catálogo vigente (docs/contracts/permissions.json) que o runtime usa.</summary>
public static class Permissions
{
    /// <summary>Um agente agir em nome do usuário; nunca herdado por padrão.</summary>
    public const string AgentAct = "agent.act";
    public const string FsRead = "fs.read";
    public const string FsWrite = "fs.write";
}
