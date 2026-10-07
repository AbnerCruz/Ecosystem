using System.Text;

namespace Urbe.Core;

public sealed record MathMacroExpansionResult(
    string Source,
    string Expanded,
    bool Success,
    string? Diagnostic);

/// <summary>
/// Bounded render-time expansion for the string macro dictionary exposed by
/// the legacy UrbeMath.setMacros API. Stored/user TeX is never mutated.
/// </summary>
public static class MathMacros
{
    public const int MaxDefinitions = 128;
    public const int MaxReplacementLength = 4_096;
    public const int MaxExpandedLength = 65_536;
    public const int MaxExpansionDepth = 16;
    public const int MaxExpansions = 1_024;

    public static MathMacroExpansionResult Expand(
        string? tex,
        IReadOnlyDictionary<string, string>? macros)
    {
        var source = tex ?? string.Empty;
        if (macros is null || macros.Count == 0)
            return new(source, source, true, null);

        if (macros.Count > MaxDefinitions)
        {
            return Failure(
                source,
                "Quantidade de macros excede o limite de " + MaxDefinitions + ".");
        }

        var definitions = new Dictionary<string, Definition>(StringComparer.Ordinal);
        var totalReplacementLength = 0;

        foreach (var pair in macros)
        {
            if (!IsValidName(pair.Key))
                return Failure(source, "Nome de macro inválido: " + pair.Key + ".");

            var replacement = pair.Value ?? string.Empty;
            if (replacement.Length > MaxReplacementLength)
            {
                return Failure(
                    source,
                    "Expansão de " + pair.Key + " excede o limite de " +
                    MaxReplacementLength + " caracteres.");
            }

            totalReplacementLength += replacement.Length;
            if (totalReplacementLength > MaxExpandedLength)
                return Failure(source, "Definições de macro excedem o limite total permitido.");

            if (!TryGetArity(replacement, out var arity, out var definitionError))
                return Failure(source, pair.Key + ": " + definitionError);

            definitions[pair.Key] = new Definition(pair.Key, replacement, arity);
        }

        try
        {
            var state = new ExpansionState(definitions);
            var expanded = state.Expand(source, 0, new HashSet<string>(StringComparer.Ordinal));
            return new(source, expanded, true, null);
        }
        catch (MacroExpansionException error)
        {
            return Failure(source, error.Message);
        }
    }

    private static MathMacroExpansionResult Failure(string source, string diagnostic) =>
        new(source, source, false, diagnostic);

    private static bool IsValidName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name[0] != '\\' || name.Length < 2)
            return false;

        for (var index = 1; index < name.Length; index++)
        {
            var value = name[index];
            if (value is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z'))
                return false;
        }

        return true;
    }

    private static bool TryGetArity(
        string replacement,
        out int arity,
        out string? error)
    {
        arity = 0;
        error = null;

        for (var index = 0; index < replacement.Length; index++)
        {
            if (replacement[index] != '#')
                continue;

            if (index + 1 >= replacement.Length)
            {
                error = "Marcador '#' incompleto.";
                return false;
            }

            var next = replacement[index + 1];
            if (next == '#')
            {
                index++;
                continue;
            }

            if (next is < '1' or > '9')
            {
                error = "Argumentos de macro devem usar #1 até #9.";
                return false;
            }

            arity = Math.Max(arity, next - '0');
            index++;
        }

        return true;
    }

    private sealed record Definition(string Name, string Replacement, int Arity);

    private sealed class ExpansionState(
        IReadOnlyDictionary<string, Definition> definitions)
    {
        private int _expansions;

        public string Expand(
            string text,
            int depth,
            HashSet<string> stack)
        {
            if (depth > MaxExpansionDepth)
                throw new MacroExpansionException(
                    "Expansão de macros excedeu a profundidade máxima de " +
                    MaxExpansionDepth + ".");

            var output = new StringBuilder(Math.Min(text.Length + 32, MaxExpandedLength));

            for (var index = 0; index < text.Length;)
            {
                if (text[index] == '%')
                {
                    var end = index + 1;
                    while (end < text.Length && text[end] is not '\r' and not '\n')
                        end++;
                    AppendBounded(output, text.AsSpan(index, end - index));
                    index = end;
                    continue;
                }

                if (text[index] != '\\' || index + 1 >= text.Length)
                {
                    AppendBounded(output, text.AsSpan(index, 1));
                    index++;
                    continue;
                }

                if (text[index + 1] == '\\')
                {
                    AppendBounded(output, text.AsSpan(index, 2));
                    index += 2;
                    continue;
                }

                var commandEnd = ReadCommandEnd(text, index);
                if (commandEnd <= index + 1)
                {
                    var count = Math.Min(2, text.Length - index);
                    AppendBounded(output, text.AsSpan(index, count));
                    index += count;
                    continue;
                }

                var command = text[index..commandEnd];
                if (!definitions.TryGetValue(command, out var definition))
                {
                    AppendBounded(output, text.AsSpan(index, commandEnd - index));
                    index = commandEnd;
                    continue;
                }

                _expansions++;
                if (_expansions > MaxExpansions)
                {
                    throw new MacroExpansionException(
                        "Expansão de macros excedeu o limite de " +
                        MaxExpansions + " substituições.");
                }

                if (!stack.Add(command))
                    throw new MacroExpansionException("Ciclo de macros detectado em " + command + ".");

                var cursor = commandEnd;
                var arguments = new string[definition.Arity];
                for (var argument = 0; argument < definition.Arity; argument++)
                {
                    SkipWhitespace(text, ref cursor);
                    arguments[argument] = ReadArgument(text, ref cursor, command, argument + 1);
                }

                var substituted = Substitute(definition.Replacement, arguments);
                var nested = Expand(substituted, depth + 1, stack);
                stack.Remove(command);
                AppendBounded(output, nested.AsSpan());
                index = cursor;
            }

            return output.ToString();
        }

        private static int ReadCommandEnd(string text, int slash)
        {
            var index = slash + 1;
            if (index >= text.Length)
                return index;

            if (!IsAsciiLetter(text[index]))
                return index;

            index++;
            while (index < text.Length && IsAsciiLetter(text[index]))
                index++;
            return index;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;
        }

        private static string ReadArgument(
            string text,
            ref int index,
            string macro,
            int argumentNumber)
        {
            if (index >= text.Length)
            {
                throw new MacroExpansionException(
                    macro + " requer argumento #" + argumentNumber + ".");
            }

            if (text[index] == '{')
                return ReadGroup(text, ref index, macro, argumentNumber);

            if (text[index] == '\\')
            {
                var end = ReadCommandEnd(text, index);
                if (end <= index + 1)
                    end = Math.Min(text.Length, index + 2);
                var token = text[index..end];
                index = end;
                return token;
            }

            if (char.IsHighSurrogate(text[index]) &&
                index + 1 < text.Length &&
                char.IsLowSurrogate(text[index + 1]))
            {
                var token = text.Substring(index, 2);
                index += 2;
                return token;
            }

            return text[index++].ToString();
        }

        private static string ReadGroup(
            string text,
            ref int index,
            string macro,
            int argumentNumber)
        {
            var start = ++index;
            var depth = 1;

            while (index < text.Length)
            {
                var current = text[index];

                if (current == '\\')
                {
                    index += Math.Min(2, text.Length - index);
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                    index++;
                    continue;
                }

                if (current == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        var value = text[start..index];
                        index++;
                        return value;
                    }
                }

                index++;
            }

            throw new MacroExpansionException(
                macro + " possui grupo não terminado no argumento #" +
                argumentNumber + ".");
        }

        private static string Substitute(string replacement, IReadOnlyList<string> arguments)
        {
            var output = new StringBuilder(replacement.Length + 16);

            for (var index = 0; index < replacement.Length; index++)
            {
                if (replacement[index] != '#')
                {
                    output.Append(replacement[index]);
                    continue;
                }

                var next = replacement[++index];
                if (next == '#')
                {
                    output.Append('#');
                    continue;
                }

                var argument = next - '1';
                if (argument < 0 || argument >= arguments.Count)
                {
                    throw new MacroExpansionException(
                        "Marcador de argumento inválido na expansão de macro.");
                }

                output.Append(arguments[argument]);
            }

            return output.ToString();
        }

        private static void AppendBounded(StringBuilder output, ReadOnlySpan<char> value)
        {
            if (output.Length + value.Length > MaxExpandedLength)
            {
                throw new MacroExpansionException(
                    "Resultado da expansão de macros excede o limite de " +
                    MaxExpandedLength + " caracteres.");
            }

            output.Append(value);
        }

        private static bool IsAsciiLetter(char value) =>
            value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private sealed class MacroExpansionException(string message) : Exception(message);
}
