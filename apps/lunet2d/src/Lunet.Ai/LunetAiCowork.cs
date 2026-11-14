using System.Text;
using System.Text.Json;
using AgentRuntime;
using Lunet.Core;

namespace Lunet.Ai;

/// <summary>What the person is looking at when they ask: sent automatically, bounded, never instructions.</summary>
public sealed record LunetAiEditorContext(
    string? OpenFile,
    string? Selection,
    int CaretLine,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> ConsoleTail)
{
    public const int MaxLength = 12_000;
    public static LunetAiEditorContext Empty { get; } = new(null, null, 0, [], []);

    public string Describe()
    {
        var text = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(OpenFile))
            text.Append("Arquivo aberto no editor: ").Append(OpenFile)
                .Append(CaretLine > 0 ? $" (cursor na linha {CaretLine})" : "").AppendLine();
        if (!string.IsNullOrWhiteSpace(Selection))
        {
            var selection = Selection.Length > 6_000 ? Selection[..6_000] + "\n…(seleção truncada)" : Selection;
            text.AppendLine("Trecho selecionado pela pessoa:").AppendLine("```csharp").AppendLine(selection).AppendLine("```");
        }
        if (Diagnostics.Count > 0)
        {
            text.AppendLine("Diagnósticos atuais da compilação:");
            foreach (var d in Diagnostics.Take(40)) text.Append("- ").AppendLine(d.Length > 300 ? d[..300] : d);
        }
        if (ConsoleTail.Count > 0)
        {
            text.AppendLine("Últimas linhas do console do jogo:");
            foreach (var line in ConsoleTail.TakeLast(30)) text.Append("  ").AppendLine(line.Length > 300 ? line[..300] : line);
        }
        var result = text.ToString();
        return result.Length <= MaxLength ? result : result[..(MaxLength - 20)] + "\n…(contexto truncado)";
    }
}

public enum DiffKind { Same, Added, Removed }
public sealed record DiffLine(DiffKind Kind, string Text);

/// <summary>Line diff for reviewing agent edits. Trims the common prefix/suffix, then runs LCS on the bounded middle.</summary>
public static class LunetAiDiff
{
    private const long MaxCells = 4_000_000;

    public static IReadOnlyList<DiffLine> Lines(string? before, string? after)
    {
        var a = Split(before);
        var b = Split(after);
        int start = 0;
        while (start < a.Length && start < b.Length && a[start] == b[start]) start++;
        int endA = a.Length, endB = b.Length;
        while (endA > start && endB > start && a[endA - 1] == b[endB - 1]) { endA--; endB--; }

        var result = new List<DiffLine>(a.Length + 8);
        for (int i = 0; i < start; i++) result.Add(new DiffLine(DiffKind.Same, a[i]));
        int n = endA - start, m = endB - start;
        if ((long)n * m > MaxCells)
        {
            for (int i = start; i < endA; i++) result.Add(new DiffLine(DiffKind.Removed, a[i]));
            for (int j = start; j < endB; j++) result.Add(new DiffLine(DiffKind.Added, b[j]));
        }
        else
        {
            var lcs = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    lcs[i, j] = a[start + i] == b[start + j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            int x = 0, y = 0;
            while (x < n && y < m)
            {
                if (a[start + x] == b[start + y]) { result.Add(new DiffLine(DiffKind.Same, a[start + x])); x++; y++; }
                else if (lcs[x + 1, y] >= lcs[x, y + 1]) result.Add(new DiffLine(DiffKind.Removed, a[start + x++]));
                else result.Add(new DiffLine(DiffKind.Added, b[start + y++]));
            }
            while (x < n) result.Add(new DiffLine(DiffKind.Removed, a[start + x++]));
            while (y < m) result.Add(new DiffLine(DiffKind.Added, b[start + y++]));
        }
        for (int i = endA; i < a.Length; i++) result.Add(new DiffLine(DiffKind.Same, a[i]));
        return result;
    }

    public static (int Added, int Removed) Count(IReadOnlyList<DiffLine> diff) =>
        (diff.Count(l => l.Kind == DiffKind.Added), diff.Count(l => l.Kind == DiffKind.Removed));

    private static string[] Split(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var lines = text.Replace("\r\n", "\n").Split('\n');
        return lines.Length > 0 && lines[^1].Length == 0 ? lines[..^1] : lines;
    }
}

/// <summary>Compilation as seen by the agent. The host implements it with the same compiler the Run button uses.</summary>
public interface ILunetAiBuild
{
    Task<LunetAiBuildReport> CompileAsync(CancellationToken cancellationToken);
}

public sealed record LunetAiBuildReport(bool Succeeded, IReadOnlyList<string> Errors);

/// <summary><c>lunet.build.compile</c>: the agent compiles the project exactly like the Run button.</summary>
public sealed class LunetAiBuildTool : ITool
{
    public const string Name = "lunet.build.compile";
    private readonly ILunetAiBuild _build;
    private readonly LunetProject _project;
    public ToolDescriptor Descriptor { get; }

    public LunetAiBuildTool(LunetProject project, ILunetAiBuild build)
    {
        _build = build;
        _project = project;
        Descriptor = new ToolDescriptor(Name, "1.0.0",
            "Compile the whole Lunet project with Roslyn (same compiler as Run). Returns success, compiler errors with file(line,col) and architecture warnings (files too big, many classes in one file) to fix.",
            """{"type":"object","properties":{},"additionalProperties":false}""",
            new HashSet<string> { Permissions.AgentAct, Permissions.FsRead }, RiskClass.Read,
            ContextPath.Create(
                new ContextStep(ContextLevel.Ecosystem, "ecosystem"),
                new ContextStep(ContextLevel.Product, "lunet2d"),
                new ContextStep(ContextLevel.Project, project.Manifest.GameId)), true);
    }

    public async ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        var report = await _build.CompileAsync(cancellationToken);
        var map = new LunetAiProjectMap(_project);
        var symbols = map.Symbols();
        var lines = symbols.Select(s => s.File).Distinct().ToDictionary(f => f, f =>
        {
            try { return _project.ReadText(f).Count(c => c == '\n') + 1; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException) { return 0; }
        }, StringComparer.Ordinal);
        var architecture = LunetAiArchitecture.Review(symbols, lines);
        return ToolOutcome.Ok(JsonSerializer.Serialize(new { success = report.Succeeded, errors = report.Errors.Take(60), architecture }));
    }
}

/// <summary>One file the agent changed, with its content before the run (null = the agent created it).</summary>
public sealed record LunetAiChange(string Path, string? Before, string After)
{
    public IReadOnlyList<DiffLine> Diff() => LunetAiDiff.Lines(Before, After);
}

/// <summary>Persistent, conflict-safe checkpoint for a complete agent run.</summary>
public sealed class LunetAiCheckpoint
{
    private readonly LunetAiCheckpointStore _store;

    public LunetAiCheckpoint(LunetProject project, string? runId = null)
        => _store = new LunetAiCheckpointStore(project, runId);

    private LunetAiCheckpoint(LunetAiCheckpointStore store) => _store = store;

    public bool IsEmpty => _store.IsEmpty;
    public IReadOnlyList<string> Paths => _store.Paths;

    public static IReadOnlyList<LunetAiRecoveryPoint> Available(LunetProject project)
        => LunetAiCheckpointStore.List(project);

    public static LunetAiCheckpoint Reopen(LunetProject project, string runId)
        => new(new LunetAiCheckpointStore(project, runId, existing: true));

    internal void Capture(string relativePath) => _store.Capture(relativePath);
    internal void RecordAfter(string relativePath) => _store.RecordAfter(relativePath);
    public IReadOnlyList<LunetAiChange> Changes() => _store.Changes();
    public void Revert(string? onlyPath = null) => _store.Revert(onlyPath);
    public string SaveOriginalAsCopy(string relativePath) => _store.SaveOriginalAsCopy(relativePath);
}

/// <summary>Never invokes a write when the original bytes could not be saved durably.</summary>
internal sealed class CheckpointedTool(ITool inner, LunetAiCheckpoint checkpoint) : ITool
{
    public ToolDescriptor Descriptor => inner.Descriptor;

    public async ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        if (invocation.Arguments.ValueKind != JsonValueKind.Object ||
            !invocation.Arguments.TryGetProperty("path", out var path) || path.ValueKind != JsonValueKind.String ||
            path.GetString() is not { Length: > 0 } relative)
            return ToolOutcome.Error("Caminho ausente: gravação não autorizada.");

        try { checkpoint.Capture(relative); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or ProjectException)
        {
            return ToolOutcome.Error("Edição bloqueada porque não foi possível salvar a versão anterior: " + ex.Message);
        }

        try { return await inner.InvokeAsync(invocation, context, cancellationToken); }
        finally { checkpoint.RecordAfter(relative); }
    }
}

/// <summary>Everything a request needs. <paramref name="Provider"/> overrides the HTTP provider (tests, local models).</summary>
public sealed record LunetAiRequest(
    LunetAiSettings Settings,
    string ApiKey,
    LunetAiEditorContext Context,
    ILunetAiBuild? Build = null,
    IModelProvider? Provider = null,
    IProgress<string>? Progress = null,
    LunetAiKnowledge? Knowledge = null,
    ILunetAiImages? Images = null,
    IReadOnlyList<string>? Attachments = null,
    IProgress<LunetAiStep>? Steps = null,
    LunetAiMode Mode = LunetAiMode.Auto,
    ILunetAiWeb? Web = null);
