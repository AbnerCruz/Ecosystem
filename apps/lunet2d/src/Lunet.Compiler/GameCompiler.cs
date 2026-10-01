using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Lunet.Compiler;

/// <summary>Compila o código C# de um jogo para um assembly em memória.</summary>
public sealed class GameCompiler
{
    private readonly IReferenceProvider _references;
    private readonly object _gate = new();
    private readonly Dictionary<string, (string Text, SyntaxTree Tree)> _treeCache = new(StringComparer.Ordinal);
    private string? _lastFingerprint;
    private CompileResult? _lastResult;

    public GameCompiler(IReferenceProvider references) => _references = references ?? throw new ArgumentNullException(nameof(references));

    /// <param name="assemblyName">Nome simples do assembly; deve ser único por compilação recarregada no mesmo processo.</param>
    public CompileResult Compile(string assemblyName, IReadOnlyList<SourceFile> sources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        if (sources.Count == 0)
        {
            var empty = new LunetDiagnostic(DiagnosticSeverity.Error, "LUNET0001", "O projeto não tem nenhum arquivo .cs.", null, 0, 0, 0, 0);
            return new CompileResult(false, [empty], null, null);
        }

        lock (_gate) return CompileIncremental(assemblyName, sources);
    }

    /// <summary>
    /// Compilação incremental entre Runs: se nada mudou devolve o resultado anterior; senão reaproveita as árvores de
    /// sintaxe dos arquivos que não mudaram e as referências (metadados) já carregadas.
    /// </summary>
    private CompileResult CompileIncremental(string assemblyName, IReadOnlyList<SourceFile> sources)
    {
        var fingerprint = Fingerprint(sources);
        if (_lastResult is { } previous && fingerprint == _lastFingerprint)
            return new CompileResult(previous.Success, previous.Diagnostics, previous.Assembly, previous.Symbols) { FromCache = true, ReusedFiles = sources.Count };

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = new List<SyntaxTree>(sources.Count);
        var reused = 0;
        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            live.Add(source.Path);
            if (_treeCache.TryGetValue(source.Path, out var cached) && cached.Text == source.Text)
            {
                trees.Add(cached.Tree);
                reused++;
                continue;
            }
            var tree = CSharpSyntaxTree.ParseText(source.Text, parseOptions, source.Path, System.Text.Encoding.UTF8);
            _treeCache[source.Path] = (source.Text, tree);
            trees.Add(tree);
        }
        foreach (var gone in _treeCache.Keys.Where(k => !live.Contains(k)).ToList()) _treeCache.Remove(gone);

        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithOptimizationLevel(OptimizationLevel.Debug)
            .WithNullableContextOptions(NullableContextOptions.Enable)
            .WithConcurrentBuild(false);

        var compilation = CSharpCompilation.Create(assemblyName, trees, _references.GetReferences(), options);

        using var assembly = new MemoryStream();
        using var symbols = new MemoryStream();
        var emit = compilation.Emit(assembly, symbols, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));

        var diagnostics = emit.Diagnostics
            .Where(d => d.Severity != Microsoft.CodeAnalysis.DiagnosticSeverity.Hidden)
            .Select(DiagnosticConverter.Convert)
            .OrderBy(d => d.Severity == DiagnosticSeverity.Error ? 0 : 1)
            .ThenBy(d => d.FilePath, StringComparer.Ordinal)
            .ThenBy(d => d.Line)
            .ToImmutableArray();

        var result = emit.Success
            ? new CompileResult(true, diagnostics, assembly.ToArray(), symbols.ToArray()) { ReusedFiles = reused }
            : new CompileResult(false, diagnostics, null, null) { ReusedFiles = reused };
        _lastFingerprint = fingerprint;
        _lastResult = result;
        return result;
    }

    private static string Fingerprint(IReadOnlyList<SourceFile> sources)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var source in sources.OrderBy(s => s.Path, StringComparer.Ordinal))
        {
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(source.Path));
            hash.AppendData([0]);
            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(source.Text));
            hash.AppendData([1]);
        }
        return System.Convert.ToHexString(hash.GetHashAndReset());
    }
}

/// <summary>Converte diagnósticos do Roslyn para <see cref="LunetDiagnostic"/>.</summary>
public static class DiagnosticConverter
{
    public static LunetDiagnostic Convert(Diagnostic diagnostic)
    {
        var severity = diagnostic.Severity switch
        {
            Microsoft.CodeAnalysis.DiagnosticSeverity.Error => DiagnosticSeverity.Error,
            Microsoft.CodeAnalysis.DiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Info,
        };
        var span = diagnostic.Location.GetLineSpan();
        var path = diagnostic.Location.IsInSource ? span.Path : null;
        return new LunetDiagnostic(severity, diagnostic.Id, diagnostic.GetMessage(),
            path, span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1);
    }
}
