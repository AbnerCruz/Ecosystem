namespace Lunet.Compiler;

public enum DiagnosticSeverity { Info, Warning, Error }

/// <summary>Diagnóstico de compilação. Linhas e colunas começam em 1.</summary>
public sealed record LunetDiagnostic(
    DiagnosticSeverity Severity,
    string Id,
    string Message,
    string? FilePath,
    int Line,
    int Column,
    int EndLine,
    int EndColumn)
{
    public override string ToString()
    {
        var location = FilePath is null ? "" : $"{FilePath}({Line},{Column}): ";
        return $"{location}{Severity.ToString().ToLowerInvariant()} {Id}: {Message}";
    }
}
