using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Lunet.Compiler;

/// <summary>Compila o código C# de um jogo para um assembly em memória.</summary>
public sealed class GameCompiler
{
    private readonly IReferenceProvider _references;

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

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = new List<SyntaxTree>(sources.Count);
        foreach (var source in sources)
            trees.Add(CSharpSyntaxTree.ParseText(source.Text, parseOptions, source.Path, System.Text.Encoding.UTF8));

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

        return emit.Success
            ? new CompileResult(true, diagnostics, assembly.ToArray(), symbols.ToArray())
            : new CompileResult(false, diagnostics, null, null);
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
