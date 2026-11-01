using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentRuntime;
using Lunet.Core;

namespace Lunet.Ai;

public sealed record LunetAiFile(string Path, string Content, string Sha256);
public sealed record LunetAiFileChange(string Path, string Sha256);
public sealed record LunetAiSearchHit(string Path, int Line, int Column, string Preview);
public sealed record LunetAiExcerpt(string Path, string Content, string Sha256, int StartLine, int EndLine, int TotalLines, bool Truncated);

/// <summary>Single project-scoped filesystem boundary shared by Lunet's agent tools.</summary>
public sealed class LunetAiProjectFiles(LunetProject project)
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".json", ".md", ".txt", ".xml", ".yml", ".yaml", ".glsl", ".hlsl", ".shader", ".csv", ".csproj", ".slnx", ".props", ".targets"
    };

    public IReadOnlyList<string> List()
    {
        var result = new List<string>();
        void Walk(string absolute, string prefix)
        {
            foreach (var file in Directory.EnumerateFiles(absolute))
            {
                if (IsLink(file)) continue;
                var relative = prefix + Path.GetFileName(file);
                if (IsAllowed(relative)) result.Add(relative);
            }
            foreach (var directory in Directory.EnumerateDirectories(absolute))
            {
                var name = Path.GetFileName(directory);
                if (name is ".git" or ".lunet" or "bin" or "obj" || name.StartsWith(".", StringComparison.Ordinal) ||
                    IsLink(directory)) continue;
                Walk(directory, prefix + name + "/");
            }
        }
        Walk(project.Directory, "");
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    public LunetAiFile Read(string relativePath)
    {
        Validate(relativePath, writing: false);
        EnsureNoLinks(relativePath);
        if (!List().Contains(relativePath, StringComparer.Ordinal))
            throw new FileNotFoundException("Arquivo não encontrado no projeto.");
        var content = project.ReadText(relativePath);
        return new(relativePath, content, Hash(content));
    }

    /// <summary>Compare-and-swap. Existing file requires its exact read hash; new file requires empty hash.</summary>
    public LunetAiFileChange Write(string relativePath, string content, string expectedSha256)
    {
        Validate(relativePath, writing: true);
        EnsureNoLinks(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        if (Encoding.UTF8.GetByteCount(content) > 120_000)
            throw new InvalidOperationException("Arquivo excede 120 KB; divida a alteração.");
        var exists = List().Contains(relativePath, StringComparer.Ordinal);
        if (exists)
        {
            var current = Read(relativePath);
            if (!string.Equals(current.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Arquivo mudou desde a leitura. Leia novamente antes de escrever.");
        }
        else if (!string.IsNullOrEmpty(expectedSha256))
            throw new InvalidOperationException("Arquivo novo exige expected_sha256 vazio.");
        project.WriteText(relativePath, content);
        var saved = Read(relativePath);
        if (!string.Equals(saved.Content, content, StringComparison.Ordinal))
            throw new IOException("O arquivo persistido difere do solicitado.");
        return new(relativePath, saved.Sha256);
    }

    /// <summary>Bounded project search. Only enumerates files the agent may read; no regex or untrusted code execution.</summary>
    public IReadOnlyList<LunetAiSearchHit> Search(string query, int maxResults = 40)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
            throw new ArgumentException("Pesquisa precisa ter entre 1 e 200 caracteres.", nameof(query));
        if (maxResults is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(maxResults));
        var hits = new List<LunetAiSearchHit>();
        foreach (var path in List())
        {
            if (hits.Count >= maxResults) break;
            var absolute = Path.Combine(project.Directory, path.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                EnsureNoLinks(path);
                if (new FileInfo(absolute).Length > 1_000_000) continue;
                var lines = project.ReadText(path).Split('\n');
                for (int i = 0; i < lines.Length && hits.Count < maxResults; i++)
                {
                    var line = lines[i].TrimEnd('\r');
                    int offset = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                    if (offset < 0) continue;
                    var preview = line.Trim();
                    if (preview.Length > 250) preview = preview[..250] + "…";
                    hits.Add(new LunetAiSearchHit(path, i + 1, offset + 1, preview));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                // A file disappearing during enumeration must not expose another path.
            }
        }
        return hits;
    }

    /// <summary>Bounded excerpt for large files; hash always covers the FULL file, not just displayed lines.</summary>
    public LunetAiExcerpt ReadExcerpt(string relativePath, int startLine, int lineCount)
    {
        if (startLine < 1) throw new ArgumentOutOfRangeException(nameof(startLine));
        if (lineCount is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(lineCount));
        var file = Read(relativePath);
        var lines = file.Content.Split('\n');
        if (startLine > lines.Length) throw new ArgumentOutOfRangeException(nameof(startLine));
        int count = Math.Min(lineCount, lines.Length - startLine + 1);
        var content = string.Join("\n", lines.Skip(startLine - 1).Take(count));
        var truncated = content.Length > 20_000;
        if (truncated) content = content[..20_000];
        return new LunetAiExcerpt(relativePath, content, file.Sha256, startLine, startLine + count - 1, lines.Length, truncated);
    }

    /// <summary>Replace exactly one matching span; fail on ambiguity and concurrent changes.</summary>
    public LunetAiFileChange Replace(string relativePath, string oldText, string newText, string expectedSha256)
    {
        Validate(relativePath, writing: true);
        if (string.IsNullOrEmpty(oldText) || oldText.Length > 20_000)
            throw new ArgumentException("Trecho original deve conter de 1 a 20.000 caracteres.", nameof(oldText));
        ArgumentNullException.ThrowIfNull(newText);
        var current = Read(relativePath);
        if (!string.Equals(current.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Arquivo mudou desde a leitura. Leia novamente.");
        int first = current.Content.IndexOf(oldText, StringComparison.Ordinal);
        if (first < 0 || current.Content.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Trecho não encontrado ou ambíguo; especifique um trecho único.");
        var updated = string.Concat(current.Content.AsSpan(0, first), newText, current.Content.AsSpan(first + oldText.Length));
        return Write(relativePath, updated, expectedSha256);
    }

    private void EnsureNoLinks(string path)
    {
        var current = project.Directory;
        foreach (var part in path.Split('/'))
        {
            current = Path.Combine(current, part);
            if (IsLink(current))
                throw new InvalidOperationException("Links simbólicos não podem ser usados por ferramentas de IA.");
        }
    }

    private static bool IsLink(string path)
    {
        try { return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static void Validate(string path, bool writing)
    {
        if (!IsAllowed(path))
            throw new InvalidOperationException("Somente arquivos de texto regulares do projeto, sem caminhos ocultos ou especiais.");
        if (writing && path.Equals("lunet.json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O manifesto do jogo não pode ser alterado por IA.");
    }

    private static bool IsAllowed(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') ||
            path.Contains(':') || path.Contains('\0') || path.StartsWith('/')) return false;
        var segments = path.Split('/');
        if (segments.Any(s => s is "" or "." or ".." || s.StartsWith(".", StringComparison.Ordinal))) return false;
        return TextExtensions.Contains(Path.GetExtension(path));
    }

    public static string Hash(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}

/// <summary>Tools use the Ecosystem Agent Runtime contracts; none touch global storage or another project.</summary>
public sealed class LunetAiProjectTool : ITool
{
    public const string ListName = "lunet.files.list";
    public const string ReadName = "lunet.files.read";
    public const string WriteName = "lunet.files.write";
    public const string SearchName = "lunet.files.search";
    public const string ExcerptName = "lunet.files.excerpt";
    public const string ReplaceName = "lunet.files.replace";

    private readonly LunetAiProjectFiles _files;
    private readonly string _name;
    public ToolDescriptor Descriptor { get; }

    public LunetAiProjectTool(LunetProject project, string name)
    {
        if (name is not (ListName or ReadName or WriteName or SearchName or ExcerptName or ReplaceName))
            throw new ArgumentException("Capability desconhecida.", nameof(name));
        _name = name;
        _files = new LunetAiProjectFiles(project);
        var write = name is WriteName or ReplaceName;
        var schema = name switch
        {
            ListName => """{"type":"object","properties":{},"additionalProperties":false}""",
            ReadName => """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""",
            SearchName => """{"type":"object","properties":{"query":{"type":"string"},"max_results":{"type":"integer","minimum":1,"maximum":100}},"required":["query"],"additionalProperties":false}""",
            ExcerptName => """{"type":"object","properties":{"path":{"type":"string"},"start_line":{"type":"integer","minimum":1},"line_count":{"type":"integer","minimum":1,"maximum":100}},"required":["path","start_line","line_count"],"additionalProperties":false}""",
            ReplaceName => """{"type":"object","properties":{"path":{"type":"string"},"old_text":{"type":"string"},"new_text":{"type":"string"},"expected_sha256":{"type":"string"}},"required":["path","old_text","new_text","expected_sha256"],"additionalProperties":false}""",
            _ => """{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"},"expected_sha256":{"type":"string","description":"SHA256 returned by lunet.files.read; empty string to create a new file"}},"required":["path","content","expected_sha256"],"additionalProperties":false}"""
        };
        Descriptor = new ToolDescriptor(name, "1.0.0",
            name switch
            {
                ListName => "List all text source files inside the open Lunet game project.",
                ReadName => "Read a short project text file and its SHA256. For long files use lunet.files.excerpt.",
                SearchName => "Search text within allowed project source files, returning grounded line numbers.",
                ExcerptName => "Read an exact bounded line range, returning whole-file SHA256 and truncation metadata.",
                ReplaceName => "Replace a unique exact text span in an existing file, guarded by full-file SHA256.",
                _ => "Create or atomically replace one project text file using the SHA256 from read. Never overwrite stale content."
            }, schema,
            new HashSet<string> { Permissions.AgentAct, write ? Permissions.FsWrite : Permissions.FsRead },
            write ? RiskClass.Write : RiskClass.Read,
            ContextPath.Create(
                new ContextStep(ContextLevel.Ecosystem, "ecosystem"),
                new ContextStep(ContextLevel.Product, "lunet2d"),
                new ContextStep(ContextLevel.Project, project.Manifest.GameId)), !write);
    }

    public ValueTask<ToolOutcome> InvokeAsync(ToolInvocation invocation, ToolContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var input = invocation.Arguments;
            int ArgInt(string key, int fallback) => input.ValueKind == JsonValueKind.Object &&
                input.TryGetProperty(key, out var number) && number.ValueKind == JsonValueKind.Number &&
                number.TryGetInt32(out var parsed) ? parsed : fallback;
            string Arg(string key) => input.ValueKind == JsonValueKind.Object &&
                input.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? "" : "";
            if (_name == ListName)
                return ValueTask.FromResult(ToolOutcome.Ok(JsonSerializer.Serialize(_files.List())));
            if (_name == SearchName)
                return ValueTask.FromResult(ToolOutcome.Ok(JsonSerializer.Serialize(
                    _files.Search(Arg("query"), ArgInt("max_results", 40)))));
            if (_name == ExcerptName)
                return ValueTask.FromResult(ToolOutcome.Ok(JsonSerializer.Serialize(
                    _files.ReadExcerpt(Arg("path"), ArgInt("start_line", 0), ArgInt("line_count", 0)))));
            if (_name == ReadName)
            {
                var file = _files.Read(Arg("path"));
                if (file.Content.Length > 24_000)
                    return ValueTask.FromResult(ToolOutcome.Error("Arquivo maior que 24.000 caracteres; use lunet.files.excerpt para leitura por linhas e lunet.files.replace para edição cirúrgica."));
                return ValueTask.FromResult(ToolOutcome.Ok(JsonSerializer.Serialize(new
                {
                    path = file.Path, sha256 = file.Sha256,
                    content = file.Content
                })));
            }
            var change = _name == ReplaceName
                ? _files.Replace(Arg("path"), Arg("old_text"), Arg("new_text"), Arg("expected_sha256"))
                : _files.Write(Arg("path"), Arg("content"), Arg("expected_sha256"));
            return ValueTask.FromResult(ToolOutcome.Ok(
                JsonSerializer.Serialize(new { changed = change.Path, sha256 = change.Sha256 }),
                new ArtifactRef(change.Sha256, "lunet.text-file", change.Path)));
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(ToolOutcome.Error(ex.Message));
        }
    }
}
