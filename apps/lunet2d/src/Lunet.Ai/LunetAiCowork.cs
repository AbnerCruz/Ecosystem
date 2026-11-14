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
    public ToolDescriptor Descriptor { get; }

    public LunetAiBuildTool(LunetProject project, ILunetAiBuild build)
    {
        _build = build;
        Descriptor = new ToolDescriptor(Name, "1.0.0",
            "Compile the whole Lunet project with Roslyn (same compiler as Run). Returns success and compiler errors with file(line,col).",
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
        return ToolOutcome.Ok(JsonSerializer.Serialize(new { success = report.Succeeded, errors = report.Errors.Take(60) }));
    }
}

/// <summary>One file the agent changed, with its content before the run (null = the agent created it).</summary>
public sealed record LunetAiChange(string Path, string? Before, string After)
{
    public IReadOnlyList<DiffLine> Diff() => LunetAiDiff.Lines(Before, After);
}

/// <summary>Captures each file (text or binary) before the agent's first write so the whole run can be reverted.</summary>
public sealed class LunetAiCheckpoint
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    { ".cs", ".json", ".md", ".txt", ".xml", ".yml", ".yaml", ".csv", ".glsl", ".shader" };

    private readonly LunetProject _project;
    private readonly Dictionary<string, byte[]?> _originals = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];
    private readonly object _gate = new();

    public LunetAiCheckpoint(LunetProject project) => _project = project;

    public bool IsEmpty { get { lock (_gate) return _order.Count == 0; } }

    internal void Capture(string relativePath)
    {
        var path = Normalize(relativePath);
        var root = System.IO.Path.GetFullPath(_project.Directory).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, path));
        // Never track (and therefore never restore) anything outside the project folder.
        if (!full.StartsWith(root, StringComparison.Ordinal)) return;
        lock (_gate)
        {
            if (_originals.ContainsKey(path)) return;
            _originals[path] = File.Exists(full) ? File.ReadAllBytes(full) : null;
            _order.Add(path);
        }
    }

    public IReadOnlyList<LunetAiChange> Changes()
    {
        lock (_gate)
        {
            var result = new List<LunetAiChange>();
            foreach (var path in _order)
            {
                var full = System.IO.Path.Combine(_project.Directory, path);
                var after = File.Exists(full) ? File.ReadAllBytes(full) : null;
                var before = _originals[path];
                if (before is null && after is null) continue;
                if (before is not null && after is not null && before.AsSpan().SequenceEqual(after)) continue;
                bool text = TextExtensions.Contains(System.IO.Path.GetExtension(path));
                string? Show(byte[]? bytes) => bytes is null ? null
                    : text ? System.Text.Encoding.UTF8.GetString(bytes) : $"[arquivo binário · {bytes.Length} bytes]";
                result.Add(new LunetAiChange(path, Show(before), Show(after) ?? ""));
            }
            return result;
        }
    }

    /// <summary>Restores every file to its content before the run; files the agent created are deleted.</summary>
    public void Revert(string? onlyPath = null)
    {
        lock (_gate)
        {
            foreach (var path in _order.ToArray())
            {
                if (onlyPath is not null && Normalize(onlyPath) != path) continue;
                var before = _originals[path];
                var full = System.IO.Path.Combine(_project.Directory, path);
                if (before is null) { if (File.Exists(full)) _project.DeleteFile(path); }
                else
                {
                    // Byte-exact atomic restore (text or binary, root or subfolder).
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
                    var temporary = full + ".lunet-revert";
                    File.WriteAllBytes(temporary, before);
                    File.Move(temporary, full, overwrite: true);
                }
                _originals.Remove(path);
                _order.Remove(path);
            }
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}

/// <summary>Wraps a write tool so the checkpoint captures the file before it changes.</summary>
internal sealed class CheckpointedTool(ITool inner, LunetAiCheckpoint checkpoint) : ITool
{
    public ToolDescriptor Descriptor => inner.Descriptor;

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        if (invocation.Arguments.ValueKind == JsonValueKind.Object &&
            invocation.Arguments.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.String &&
            path.GetString() is { Length: > 0 } relative)
        {
            try { checkpoint.Capture(relative); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return inner.InvokeAsync(invocation, context, cancellationToken);
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
    IReadOnlyList<string>? Attachments = null);
