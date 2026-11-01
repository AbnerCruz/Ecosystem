using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgentRuntime;
using Lunet.Core;

namespace Lunet.Ai;

public sealed record LunetAiFile(string Path, string Content, string Sha256);
public sealed record LunetAiFileChange(string Path, string Sha256);

/// <summary>Single project-scoped filesystem boundary shared by Lunet's agent tools.</summary>
public sealed class LunetAiProjectFiles(LunetProject project)
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".json", ".md", ".txt", ".xml", ".yml", ".yaml", ".glsl", ".hlsl", ".shader", ".csv"
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

    private readonly LunetAiProjectFiles _files;
    private readonly string _name;
    public ToolDescriptor Descriptor { get; }

    public LunetAiProjectTool(LunetProject project, string name)
    {
        if (name is not (ListName or ReadName or WriteName))
            throw new ArgumentException("Capability desconhecida.", nameof(name));
        _name = name;
        _files = new LunetAiProjectFiles(project);
        var write = name == WriteName;
        var schema = name switch
        {
            ListName => """{"type":"object","properties":{},"additionalProperties":false}""",
            ReadName => """{"type":"object","properties":{"path":{"type":"string"}},"required":["path"],"additionalProperties":false}""",
            _ => """{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"},"expected_sha256":{"type":"string","description":"SHA256 returned by lunet.files.read; empty string to create a new file"}},"required":["path","content","expected_sha256"],"additionalProperties":false}"""
        };
        Descriptor = new ToolDescriptor(name, "1.0.0",
            name switch
            {
                ListName => "List all text source files inside the open Lunet game project.",
                ReadName => "Read one project text file and its current SHA256. Read before editing.",
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
            string Arg(string key) => input.ValueKind == JsonValueKind.Object &&
                input.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? "" : "";
            if (_name == ListName)
                return ValueTask.FromResult(ToolOutcome.Ok(JsonSerializer.Serialize(_files.List())));
            if (_name == ReadName)
            {
                var file = _files.Read(Arg("path"));
                if (file.Content.Length > 24_000)
                    return ValueTask.FromResult(ToolOutcome.Error("Arquivo maior que 24.000 caracteres: edição por IA bloqueada até haver leitura paginada segura."));
                return ValueTask.FromResult(ToolOutcome.Ok(JsonSerializer.Serialize(new
                {
                    path = file.Path, sha256 = file.Sha256,
                    content = file.Content
                })));
            }
            var change = _files.Write(Arg("path"), Arg("content"), Arg("expected_sha256"));
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
