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

    public int ErrorCount => Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
}
