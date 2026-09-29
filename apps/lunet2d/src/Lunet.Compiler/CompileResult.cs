namespace Lunet.Compiler;

public sealed class CompileResult
{
    public CompileResult(bool success, IReadOnlyList<LunetDiagnostic> diagnostics, byte[]? assembly, byte[]? symbols)
    {
        Success = success;
        Diagnostics = diagnostics;
        Assembly = assembly;
        Symbols = symbols;
    }

    public bool Success { get; }
    public IReadOnlyList<LunetDiagnostic> Diagnostics { get; }

    /// <summary>Assembly gerado (somente quando <see cref="Success"/>).</summary>
    public byte[]? Assembly { get; }

    /// <summary>PDB portátil para stack traces com linha (somente quando <see cref="Success"/>).</summary>
    public byte[]? Symbols { get; }

    /// <summary>Verdadeiro quando nenhum arquivo mudou desde a compilação anterior e o resultado foi reaproveitado.</summary>
    public bool FromCache { get; init; }

    /// <summary>Quantos arquivos tiveram a árvore de sintaxe reaproveitada (não precisaram ser lidos de novo).</summary>
    public int ReusedFiles { get; init; }

    public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
}
