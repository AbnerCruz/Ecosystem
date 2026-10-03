using System.Text.Json;

namespace AgentRuntime.Tools.Files;

/// <summary>
/// Raiz sandbox de arquivos: todo caminho é resolvido DENTRO da raiz. Travessia (<c>..</c>), caminho absoluto e symlink que escapa
/// são recusados (T-3). Falha fechada: na dúvida, recusa.
/// </summary>
public sealed class FileSandbox
{
    public string Root { get; }

    public FileSandbox(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Raiz do sandbox vazia.", nameof(root));
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }

    /// <summary>Resolve <paramref name="relative"/> dentro da raiz, ou devolve nulo (com motivo) se escapar.</summary>
    public string? Resolve(string relative, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(relative)) { problem = "caminho vazio"; return null; }
        if (relative.Contains('\0')) { problem = "caminho inválido"; return null; }
        if (Path.IsPathRooted(relative)) { problem = "caminho absoluto não é permitido"; return null; }

        var full = Path.GetFullPath(Path.Combine(Root, relative));
        if (!IsInside(full)) { problem = "caminho fora da raiz do Context"; return null; }

        // Symlinks: cada componente existente é verificado contra a raiz real.
        var realRoot = RealPath(Root);
        var current = Root;
        foreach (var part in Path.GetRelativePath(Root, full).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if (!File.Exists(current) && !Directory.Exists(current)) break;
            var info = new FileInfo(current);
            if (info.LinkTarget is not null)
            {
                var target = RealPath(current);
                if (!IsInside(target, realRoot)) { problem = "link simbólico aponta para fora da raiz"; return null; }
            }
        }
        return full;
    }

    private bool IsInside(string path) => IsInside(path, Root);

    private static bool IsInside(string path, string root)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return path.Equals(root, comparison) || path.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }

    private static string RealPath(string path)
    {
        var info = new FileInfo(path);
        var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
        return Path.TrimEndingDirectorySeparator(resolved?.FullName ?? Path.GetFullPath(path));
    }
}

public static class FileToolIds
{
    public const string Read = "files.read";
    public const string Write = "files.write";
}

internal static class FileToolSupport
{
    public static bool TryGetString(JsonElement args, string name, out string value)
    {
        value = "";
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String) return false;
        value = p.GetString() ?? "";
        return true;
    }
}

/// <summary>files.read — lê um arquivo de texto preso à raiz do Context. Exige a permissão fs.read; risco: leitura.</summary>
public sealed class FilesReadTool(FileSandbox sandbox, ContextPath scope) : ITool
{
    public const long MaxBytes = 256 * 1024;

    public ToolDescriptor Descriptor { get; } = new(
        FileToolIds.Read, "1.0.0", "Lê um arquivo de texto dentro do workspace.",
        """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""",
        new HashSet<string> { Permissions.FsRead }, RiskClass.Read, scope, Idempotent: true);

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        if (!FileToolSupport.TryGetString(invocation.Arguments, "path", out var path))
            return ValueTask.FromResult(ToolOutcome.Error("argumento 'path' (texto) obrigatório"));
        var full = sandbox.Resolve(path, out var problem);
        if (full is null) return ValueTask.FromResult(ToolOutcome.Error($"recusado: {problem}"));
        if (!File.Exists(full)) return ValueTask.FromResult(ToolOutcome.Error("arquivo não encontrado"));
        if (new FileInfo(full).Length > MaxBytes) return ValueTask.FromResult(ToolOutcome.Error($"arquivo maior que {MaxBytes} bytes"));
        return ValueTask.FromResult(ToolOutcome.Ok(File.ReadAllText(full)));
    }
}

/// <summary>
/// files.write — cria um arquivo de texto novo preso à raiz. Exige fs.write; risco: escrita. Sobrescrever é destrutivo e esta
/// ferramenta o recusa (a remoção é <see cref="FilesDeleteTool"/>, que exige aprovação).
/// </summary>
public sealed class FilesWriteTool(FileSandbox sandbox, ContextPath scope) : ITool
{
    public ToolDescriptor Descriptor { get; } = new(
        FileToolIds.Write, "1.0.0", "Cria um arquivo de texto novo dentro do workspace (não sobrescreve).",
        """{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"}},"required":["path","content"],"additionalProperties":false}""",
        new HashSet<string> { Permissions.FsWrite }, RiskClass.Write, scope, Idempotent: false);

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        if (!FileToolSupport.TryGetString(invocation.Arguments, "path", out var path) ||
            !FileToolSupport.TryGetString(invocation.Arguments, "content", out var content))
            return ValueTask.FromResult(ToolOutcome.Error("argumentos 'path' e 'content' (texto) obrigatórios"));
        var full = sandbox.Resolve(path, out var problem);
        if (full is null) return ValueTask.FromResult(ToolOutcome.Error($"recusado: {problem}"));
        if (File.Exists(full)) return ValueTask.FromResult(ToolOutcome.Error("arquivo já existe; sobrescrever não é permitido por esta ferramenta"));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return ValueTask.FromResult(ToolOutcome.Ok($"gravado {path}", new ArtifactRef($"file:{path}", "file", path)));
    }
}

/// <summary>files.delete — remove um arquivo do workspace. Risco DESTRUTIVO: o runtime só executa com aprovação.</summary>
public sealed class FilesDeleteTool(FileSandbox sandbox, ContextPath scope) : ITool
{
    public const string Id = "files.delete";

    public ToolDescriptor Descriptor { get; } = new(
        Id, "1.0.0", "Remove um arquivo dentro do workspace.",
        """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""",
        new HashSet<string> { Permissions.FsWrite }, RiskClass.Destructive, scope, Idempotent: true);

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        if (!FileToolSupport.TryGetString(invocation.Arguments, "path", out var path))
            return ValueTask.FromResult(ToolOutcome.Error("argumento 'path' (texto) obrigatório"));
        var full = sandbox.Resolve(path, out var problem);
        if (full is null) return ValueTask.FromResult(ToolOutcome.Error($"recusado: {problem}"));
        if (!File.Exists(full)) return ValueTask.FromResult(ToolOutcome.Error("arquivo não encontrado"));
        File.Delete(full);
        return ValueTask.FromResult(ToolOutcome.Ok($"removido {path}"));
    }
}

/// <summary>
/// Verificador de arquivos: olha o disco, não a palavra do modelo. Critérios suportados: <c>exists:&lt;caminho&gt;</c> e
/// <c>contains:&lt;caminho&gt;:&lt;texto&gt;</c>. Critério que ele não sabe verificar FALHA (falha fechada).
/// </summary>
public sealed class FileExistsVerifier(FileSandbox sandbox) : IVerifier
{
    public ValueTask<VerificationResult> VerifyAsync(TaskSpec task, RunOutput output, CancellationToken cancellationToken)
    {
        if (task.Acceptance.Count == 0)
            return ValueTask.FromResult(VerificationResult.Fail(task.Id, "tarefa sem critérios de aceite verificáveis"));

        var checks = new List<string>();
        var failures = new List<string>();
        foreach (var criterion in task.Acceptance)
        {
            checks.Add(criterion);
            var problem = Check(criterion);
            if (problem is not null) failures.Add($"{criterion}: {problem}");
        }
        return ValueTask.FromResult(failures.Count == 0
            ? VerificationResult.Pass(task.Id, $"{checks.Count} critério(s) verificado(s) no disco", [.. checks])
            : VerificationResult.Fail(task.Id, string.Join("; ", failures), [.. checks]));
    }

    private string? Check(string criterion)
    {
        if (criterion.StartsWith("exists:", StringComparison.Ordinal))
        {
            var full = sandbox.Resolve(criterion["exists:".Length..], out var problem);
            if (full is null) return problem;
            return File.Exists(full) ? null : "arquivo não existe";
        }
        if (criterion.StartsWith("contains:", StringComparison.Ordinal))
        {
            var rest = criterion["contains:".Length..];
            var split = rest.IndexOf(':');
            if (split <= 0) return "formato esperado contains:<caminho>:<texto>";
            var full = sandbox.Resolve(rest[..split], out var problem);
            if (full is null) return problem;
            if (!File.Exists(full)) return "arquivo não existe";
            return File.ReadAllText(full).Contains(rest[(split + 1)..], StringComparison.Ordinal) ? null : "texto não encontrado";
        }
        return "critério desconhecido para este verificador (falha fechada)";
    }
}
