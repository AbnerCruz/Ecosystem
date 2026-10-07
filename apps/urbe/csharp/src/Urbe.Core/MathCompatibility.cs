using System.Collections.ObjectModel;

namespace Urbe.Core;

public static class MathCompatibilityCodes
{
    public const string UnsupportedCommand = "MATH-COMPAT-UNSUPPORTED";
    public const string MacroDeclaration = "MATH-COMPAT-MACRO";
}

public sealed record MathCompatibilityDiagnostic(
    string Code,
    string Command,
    int Start,
    int Length,
    string Message,
    string? Suggestion,
    bool BlocksRendering);

/// <summary>
/// Host-neutral compatibility analysis for TeX entered in Urbe.
///
/// This layer never rewrites or expands user TeX. It only reports constructs that
/// the renderer investigation identified as unsupported or unsafe to infer.
/// Positions are UTF-16 offsets, matching the editor contract.
/// </summary>
public static class MathCompatibility
{
    private static readonly ReadOnlyDictionary<string, string?> Unsupported =
        new(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["\\dfrac"] =
                "Use \\frac only when forcing display-style fractions is not required.",
            ["\\boldsymbol"] =
                "Use \\mathbf only for content where that visual/semantic difference is acceptable.",
            ["\\lVert"] =
                "Use a supported norm delimiter only after preserving double-bar semantics.",
            ["\\rVert"] =
                "Use a supported norm delimiter only after preserving double-bar semantics.",
            ["\\overset"] = null,
            ["\\underset"] = null,
            ["\\boxed"] = null,
            ["\\cancel"] = null
        });

    private static readonly HashSet<string> MacroDeclarations =
        new(StringComparer.Ordinal)
        {
            "\\newcommand",
            "\\renewcommand",
            "\\providecommand",
            "\\def",
            "\\gdef",
            "\\edef",
            "\\xdef"
        };

    public static IReadOnlyDictionary<string, string?> KnownUnsupportedCommands => Unsupported;

    public static IReadOnlyList<MathCompatibilityDiagnostic> Analyze(string? tex)
    {
        var source = tex ?? string.Empty;
        if (source.Length == 0)
            return Array.Empty<MathCompatibilityDiagnostic>();

        var diagnostics = new List<MathCompatibilityDiagnostic>();

        for (var index = 0; index < source.Length;)
        {
            if (source[index] == '%')
            {
                index = SkipComment(source, index + 1);
                continue;
            }

            if (source[index] != '\\')
            {
                index++;
                continue;
            }

            if (index + 1 >= source.Length)
                break;

            // TeX linebreak/control symbol. In particular, "\\\\boxed" is not
            // a boxed command; it is a linebreak followed by literal letters.
            if (source[index + 1] == '\\')
            {
                index += 2;
                continue;
            }

            if (!IsAsciiLetter(source[index + 1]))
            {
                // Control symbol such as \\, or \\%.
                index += 2;
                continue;
            }

            var end = index + 2;
            while (end < source.Length && IsAsciiLetter(source[end]))
                end++;

            var command = source[index..end];

            if (MacroDeclarations.Contains(command))
            {
                diagnostics.Add(new(
                    MathCompatibilityCodes.MacroDeclaration,
                    command,
                    index,
                    end - index,
                    "Declarações dinâmicas de macro não são expandidas pelo Core C#.",
                    "Preserve a declaração na fonte e substitua por TeX explícito antes de depender da renderização.",
                    true));
            }
            else if (Unsupported.TryGetValue(command, out var suggestion))
            {
                diagnostics.Add(new(
                    MathCompatibilityCodes.UnsupportedCommand,
                    command,
                    index,
                    end - index,
                    "O renderer C# avaliado não oferece suporte nativo a " + command + ".",
                    suggestion,
                    true));
            }

            index = end;
        }

        return diagnostics.AsReadOnly();
    }

    private static int SkipComment(string source, int index)
    {
        while (index < source.Length && source[index] is not '\r' and not '\n')
            index++;
        return index;
    }

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
}
