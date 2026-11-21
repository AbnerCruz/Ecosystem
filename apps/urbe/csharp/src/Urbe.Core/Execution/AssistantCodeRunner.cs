namespace Urbe.Core.Execution;

/// <summary>What one run of the Assistente's code asks for: the language, the source, its input (stdin) and limits.</summary>
public sealed record CodeRun(string Language, string Source, string Input = "", TimeSpan? Timeout = null, long MemoryBytes = CodeRun.DefaultMemory)
{
    public const long DefaultMemory = 256L * 1024 * 1024;
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    public TimeSpan Limit => Timeout ?? DefaultTimeout;
}

/// <summary>How a run ended. <see cref="Status"/> is ok, error, timeout, memory or unavailable.</summary>
public sealed record CodeRunResult(string Status, string Output, string Errors, int ExitCode, TimeSpan Elapsed)
{
    public bool Ok => Status == "ok";
    public static CodeRunResult Unavailable(string why) => new("unavailable", "", why, -1, TimeSpan.Zero);
}

/// <summary>
/// DEC-0066 B (plan in docs/csharp/WASMTIME-PLAN-20261121.md): runs code the Assistente writes in an isolated
/// sandbox — no vault, no network, bounded time and memory. A head that has no sandbox uses
/// <see cref="UnavailableCodeRunner"/>, which says so instead of pretending to run.
/// </summary>
public interface IAssistantCodeRunner
{
    IReadOnlyList<string> Languages { get; }
    Task<CodeRunResult> RunAsync(CodeRun run, CancellationToken cancel);
}

public sealed class UnavailableCodeRunner : IAssistantCodeRunner
{
    public IReadOnlyList<string> Languages => [];
    public Task<CodeRunResult> RunAsync(CodeRun run, CancellationToken cancel) =>
        Task.FromResult(CodeRunResult.Unavailable("Executar código ainda não está disponível neste aparelho."));
}

public static class AssistantCodeRunners
{
    /// <summary>The one the app uses; a head with a sandbox replaces it.</summary>
    public static IAssistantCodeRunner Current { get; set; } = new UnavailableCodeRunner();

    /// <summary>Checks a run before it goes to the sandbox; null when it can go, else what is wrong.</summary>
    public static string? Validate(CodeRun run, IAssistantCodeRunner runner)
    {
        if (string.IsNullOrWhiteSpace(run.Source)) return "O código está vazio.";
        if (run.Source.Length > 200_000) return "O código passa de 200 mil caracteres.";
        if (!runner.Languages.Contains(run.Language, StringComparer.OrdinalIgnoreCase))
            return runner.Languages.Count == 0 ? "Executar código ainda não está disponível neste aparelho." : "Linguagem " + run.Language + " não disponível; use " + string.Join(", ", runner.Languages) + ".";
        if (run.Limit <= TimeSpan.Zero || run.Limit > TimeSpan.FromMinutes(2)) return "O tempo limite vai de 1 s a 2 min.";
        if (run.MemoryBytes is < 16L * 1024 * 1024 or > 1024L * 1024 * 1024) return "A memória vai de 16 MB a 1 GB.";
        return null;
    }
}
