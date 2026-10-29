namespace Urbe.Core;

/// <summary>
/// The one list of the vault's text types (NN-001): what the Urbe reads as text, keeps as a document, opens in the
/// editor and writes. Notes (.md), the app's own data (.json, .yaml), the web (.html, .css, .js) and code (CST-141).
/// Anything else is a file: listed, previewed and opened, never rewritten.
/// </summary>
public static class VaultTextFiles
{
    public static readonly IReadOnlySet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // notes and plain text
        ".md", ".markdown", ".txt",
        // data
        ".json", ".jsonc", ".yaml", ".yml", ".csv", ".tsv", ".toml", ".ini", ".xml",
        // the web
        ".html", ".htm", ".css", ".scss", ".less", ".js", ".mjs", ".cjs", ".jsx", ".ts", ".tsx", ".vue", ".svelte",
        // code
        ".cs", ".py", ".java", ".kt", ".kts", ".c", ".h", ".cpp", ".hpp", ".cc", ".go", ".rs", ".rb", ".php", ".lua",
        ".swift", ".dart", ".r", ".sql", ".sh", ".bash", ".ps1", ".bat", ".gradle", ".tex"
    };

    /// <summary>True for a path whose extension is a text type of the vault.</summary>
    public static bool IsText(string path) => Extensions.Contains(System.IO.Path.GetExtension(path));
}
