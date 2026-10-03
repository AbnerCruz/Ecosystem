namespace AgentRuntime;

/// <summary>Fonte de tempo e de espera. O Core nunca lê o relógio nem dorme por conta própria: quem hospeda fornece (e o teste fixa).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public enum BudgetScopeKind { Organization, Project, Agent, Task, Provider }

public readonly record struct BudgetScope(BudgetScopeKind Kind, string Id)
{
    public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}:{Id}";
}

/// <summary>Limites declarados de um escopo (plano §8.1): total, diário, mensal e por operação. Ausente = sem esse limite.</summary>
public sealed record BudgetLimits(Money? Total = null, Money? Daily = null, Money? Monthly = null, Money? PerOperation = null)
{
    internal bool Any => Total is not null || Daily is not null || Monthly is not null || PerOperation is not null;
}

public sealed record Reservation(string Id, IReadOnlyList<BudgetScope> Scopes, Money Amount, DateOnly Day, int MonthKey);

/// <param name="Overrun">Quanto o gasto real passou do reservado (nunca é absorvido em silêncio).</param>
/// <param name="ExceededScopes">Escopos cujo limite ficou ultrapassado depois de conciliar.</param>
public sealed record SettleResult(Money Overrun, IReadOnlyList<BudgetScope> ExceededScopes);

/// <summary>
/// Ledger de orçamento: toda operação que gasta pede RESERVA, executa e CONCILIA o uso. Há uma autoridade de ledger por organização
/// (NN-001), consumida pelo Agent Runtime e, no futuro, pelo Execution Runtime.
/// </summary>
public interface ILedger
{
    /// <summary>Reserva <paramref name="amount"/> em todos os escopos, ou em nenhum. Falha fechada: sem limite configurado, não gasta.</summary>
    bool TryReserve(IReadOnlyList<BudgetScope> scopes, Money amount, out Reservation? reservation, out string? reason);

    SettleResult Settle(Reservation reservation, Money actual);

    void Release(Reservation reservation);

    /// <summary>Quanto ainda cabe no escopo (o menor entre os limites declarados), ou nulo se o escopo não tem limite.</summary>
    Money? Remaining(BudgetScope scope);
}

/// <summary>Ledger em memória, determinístico (relógio injetado): a implementação de referência do R1.</summary>
public sealed class InMemoryLedger(IClock clock, string currency) : ILedger
{
    private sealed class Bucket
    {
        public BudgetLimits Limits = new();
        public long Total;
        public readonly Dictionary<DateOnly, long> Day = [];
        public readonly Dictionary<int, long> Month = [];
    }

    private readonly object _gate = new();
    private readonly Dictionary<BudgetScope, Bucket> _buckets = [];
    private long _next;

    public void SetLimits(BudgetScope scope, BudgetLimits limits)
    {
        lock (_gate) Get(scope).Limits = limits;
    }

    public bool TryReserve(IReadOnlyList<BudgetScope> scopes, Money amount, out Reservation? reservation, out string? reason)
    {
        lock (_gate)
        {
            reservation = null;
            if (amount.IsNegative) { reason = "valor negativo"; return false; }
            var limited = scopes.Where(s => _buckets.TryGetValue(s, out var b) && b.Limits.Any).ToList();
            if (limited.Count == 0) { reason = "nenhum escopo com orçamento configurado (deny-by-default)"; return false; }

            var now = clock.UtcNow.UtcDateTime;
            var day = DateOnly.FromDateTime(now);
            var month = (now.Year * 100) + now.Month;
            foreach (var scope in limited)
            {
                var b = _buckets[scope];
                var l = b.Limits;
                if (l.PerOperation is { } perOp && amount > perOp) { reason = $"limite por operação de {scope}"; return false; }
                if (l.Total is { } total && Used(b.Total) + amount > total) { reason = $"limite total de {scope}"; return false; }
                if (l.Daily is { } daily && Used(b.Day.GetValueOrDefault(day)) + amount > daily) { reason = $"limite diário de {scope}"; return false; }
                if (l.Monthly is { } monthly && Used(b.Month.GetValueOrDefault(month)) + amount > monthly) { reason = $"limite mensal de {scope}"; return false; }
            }

            foreach (var scope in limited) Apply(_buckets[scope], day, month, amount.Minor);
            reservation = new Reservation($"res-{++_next}", limited, amount, day, month);
            reason = null;
            return true;
        }
    }

    public SettleResult Settle(Reservation reservation, Money actual)
    {
        lock (_gate)
        {
            var delta = actual.Minor - reservation.Amount.Minor;
            var exceeded = new List<BudgetScope>();
            foreach (var scope in reservation.Scopes)
            {
                var b = _buckets[scope];
                Apply(b, reservation.Day, reservation.MonthKey, delta);
                if (IsExceeded(b, reservation.Day, reservation.MonthKey)) exceeded.Add(scope);
            }
            return new SettleResult(new Money(Math.Max(0, delta), currency), exceeded);
        }
    }

    public void Release(Reservation reservation)
    {
        lock (_gate)
            foreach (var scope in reservation.Scopes)
                Apply(_buckets[scope], reservation.Day, reservation.MonthKey, -reservation.Amount.Minor);
    }

    public Money? Remaining(BudgetScope scope)
    {
        lock (_gate)
        {
            if (!_buckets.TryGetValue(scope, out var b) || !b.Limits.Any) return null;
            var now = clock.UtcNow.UtcDateTime;
            var day = DateOnly.FromDateTime(now);
            var month = (now.Year * 100) + now.Month;
            Money? best = null;
            void Consider(Money? limit, long used)
            {
                if (limit is null) return;
                var left = limit.Value - Used(used);
                best = best is null ? left : Money.Min(best.Value, left);
            }
            Consider(b.Limits.Total, b.Total);
            Consider(b.Limits.Daily, b.Day.GetValueOrDefault(day));
            Consider(b.Limits.Monthly, b.Month.GetValueOrDefault(month));
            return best;
        }
    }

    private Bucket Get(BudgetScope scope)
    {
        if (!_buckets.TryGetValue(scope, out var b)) _buckets[scope] = b = new Bucket();
        return b;
    }

    private Money Used(long minor) => new(minor, currency);

    private static void Apply(Bucket b, DateOnly day, int month, long delta)
    {
        b.Total += delta;
        b.Day[day] = b.Day.GetValueOrDefault(day) + delta;
        b.Month[month] = b.Month.GetValueOrDefault(month) + delta;
    }

    private bool IsExceeded(Bucket b, DateOnly day, int month)
    {
        var l = b.Limits;
        return (l.Total is { } t && Used(b.Total) > t)
            || (l.Daily is { } d && Used(b.Day.GetValueOrDefault(day)) > d)
            || (l.Monthly is { } m && Used(b.Month.GetValueOrDefault(month)) > m);
    }
}
