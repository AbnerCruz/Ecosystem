using System.Text.RegularExpressions;

namespace Urbe.Core;

/// <summary>What a piece of code is, for its colour in the editor.</summary>
public enum CodeTokenKind { Keyword, String, Comment, Number, Literal, Tag, Attribute, Property }

/// <summary>A coloured piece of code: [Start, Start + Length) of the text.</summary>
public readonly record struct CodeToken(int Start, int Length, CodeTokenKind Kind);

/// <summary>
/// CST-141: the colours of the code files of the vault, by family of languages (C-like, Python, shell, Ruby, HTML
/// with its script and style, CSS, JSON, YAML, TOML/INI, SQL, XML). A scan of the text, in order, the pieces never
/// overlapping; what is not a token is shown in the text's own colour.
/// </summary>
public static partial class CodeHighlighter
{
    /// <summary>The family of a code file, or null for a note, plain text or a table.</summary>
    public static string? LanguageOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".js" or ".mjs" or ".cjs" or ".jsx" or ".ts" or ".tsx" or ".cs" or ".java" or ".kt" or ".kts" or ".c" or ".h" or ".cpp" or ".hpp"
            or ".cc" or ".go" or ".rs" or ".swift" or ".dart" or ".php" or ".gradle" or ".vue" or ".svelte" => "c",
        ".py" or ".r" => "python",
        ".sh" or ".bash" or ".ps1" or ".bat" => "shell",
        ".rb" or ".lua" => "ruby",
        ".html" or ".htm" or ".xml" => "html",
        ".css" or ".scss" or ".less" => "css",
        ".json" or ".jsonc" => "json",
        ".yaml" or ".yml" => "yaml",
        ".toml" or ".ini" => "toml",
        ".sql" => "sql",
        ".tex" => "tex",
        _ => null
    };

    public static IReadOnlyList<CodeToken> Tokens(string text, string language)
    {
        text ??= "";
        var tokens = new List<CodeToken>();
        if (language == "html") Html(text, 0, text.Length, tokens);
        else Scan(text, 0, text.Length, language, tokens);
        return tokens;
    }

    private static Regex? PatternOf(string language) => language switch
    {
        "c" => CLike(),
        "python" => Python(),
        "shell" => Shell(),
        "ruby" => Ruby(),
        "css" => Css(),
        "json" => Json(),
        "yaml" => Yaml(),
        "toml" => Toml(),
        "sql" => Sql(),
        "tex" => Tex(),
        _ => null
    };

    private static void Scan(string text, int start, int end, string language, List<CodeToken> into)
    {
        if (PatternOf(language) is not { } pattern) return;
        for (var m = pattern.Match(text, start, end - start); m.Success; m = m.NextMatch())
        {
            if (m.Length == 0) continue;
            foreach (var kind in Kinds)
            {
                var g = m.Groups[kind.Name];
                if (!g.Success) continue;
                into.Add(new CodeToken(g.Index, g.Length, kind.Kind));
                break;
            }
        }
    }

    private static readonly (string Name, CodeTokenKind Kind)[] Kinds =
    [
        ("comment", CodeTokenKind.Comment), ("string", CodeTokenKind.String), ("prop", CodeTokenKind.Property),
        ("kw", CodeTokenKind.Keyword), ("lit", CodeTokenKind.Literal), ("num", CodeTokenKind.Number), ("tag", CodeTokenKind.Tag)
    ];

    /// <summary>HTML and XML: comments, tags and their attributes; the inside of script and style is code and CSS.</summary>
    private static void Html(string text, int start, int end, List<CodeToken> into)
    {
        int i = start;
        while (i < end)
        {
            int lt = text.IndexOf('<', i, end - i);
            if (lt < 0) return;
            if (string.CompareOrdinal(text, lt, "<!--", 0, 4) == 0)
            {
                int close = text.IndexOf("-->", lt + 4, end - lt - 4, StringComparison.Ordinal);
                int stop = close < 0 ? end : close + 3;
                into.Add(new CodeToken(lt, stop - lt, CodeTokenKind.Comment));
                i = stop;
                continue;
            }
            var tag = HtmlTag().Match(text, lt, end - lt);
            if (!tag.Success || tag.Index != lt) { i = lt + 1; continue; }
            var name = tag.Groups["name"];
            into.Add(new CodeToken(name.Index, name.Length, CodeTokenKind.Tag));
            var rest = tag.Groups["rest"];
            for (var a = HtmlAttribute().Match(text, rest.Index, rest.Length); a.Success; a = a.NextMatch())
            {
                into.Add(new CodeToken(a.Groups["attr"].Index, a.Groups["attr"].Length, CodeTokenKind.Attribute));
                if (a.Groups["value"].Success) into.Add(new CodeToken(a.Groups["value"].Index, a.Groups["value"].Length, CodeTokenKind.String));
            }
            i = tag.Index + tag.Length;
            // the inside of <script> and <style>, to their closing tag
            var tagName = name.Value.ToLowerInvariant();
            if (tag.Groups["slash"].Value.Length == 0 && tagName is "script" or "style")
            {
                int close = text.IndexOf("</" + tagName, i, end - i, StringComparison.OrdinalIgnoreCase);
                int inner = close < 0 ? end : close;
                Scan(text, i, inner, tagName == "script" ? "c" : "css", into);
                i = inner;
            }
        }
    }

    private const string CKeywords = "abstract|as|async|await|base|break|case|catch|class|const|continue|def|default|defer|delete|do|else|enum|export|extends|extern|final|finally|fn|for|foreach|from|func|function|get|go|if|impl|implements|import|in|include|inline|instanceof|interface|internal|is|let|loop|match|mod|module|mut|namespace|new|of|operator|override|package|private|protected|pub|public|readonly|record|ref|return|sealed|select|set|static|struct|super|switch|this|throw|trait|try|type|typeof|union|unsafe|use|using|val|var|virtual|void|volatile|when|where|while|yield|int|long|short|float|double|bool|boolean|char|string|byte|object|dynamic|auto|unsigned|signed|self|Self";

    [GeneratedRegex(@"(?<comment>//[^\n]*|/\*.*?(?:\*/|\z))|(?<string>""(?:\\.|[^""\\\n])*""?|'(?:\\.|[^'\\\n])*'?|`(?:\\.|[^`\\])*`?)|(?<lit>\b(?:true|false|null|undefined|nil|None|NaN)\b)|(?<kw>\b(?:" + CKeywords + @")\b)|(?<num>\b0[xX][0-9a-fA-F_]+\b|\b\d[\d_]*(?:\.\d+)?(?:[eE][+-]?\d+)?[fFdDmMlLuU]*\b)", RegexOptions.Singleline)]
    private static partial Regex CLike();

    [GeneratedRegex(@"(?<comment>#[^\n]*)|(?<string>"""""".*?(?:""""""|\z)|'''.*?(?:'''|\z)|[rbfRBF]{0,2}""(?:\\.|[^""\\\n])*""?|[rbfRBF]{0,2}'(?:\\.|[^'\\\n])*'?)|(?<lit>\b(?:True|False|None|TRUE|FALSE|NULL|NA)\b)|(?<kw>\b(?:and|as|assert|async|await|break|class|continue|def|del|elif|else|except|finally|for|from|function|global|if|import|in|is|lambda|library|nonlocal|not|or|pass|raise|return|try|while|with|yield|match|case)\b)|(?<num>\b\d[\d_]*(?:\.\d+)?(?:[eE][+-]?\d+)?j?\b)", RegexOptions.Singleline)]
    private static partial Regex Python();

    [GeneratedRegex(@"(?<comment>(?:^|(?<=\s))(?:#|::|REM\b)[^\n]*)|(?<string>""(?:\\.|[^""\\])*""?|'[^']*'?)|(?<kw>\b(?:if|then|else|elif|fi|for|do|done|case|esac|while|until|function|in|export|local|return|echo|set|call|goto|exit|param|foreach)\b)|(?<prop>\$\{?[\w@#?*!-]+\}?)|(?<num>\b\d+\b)", RegexOptions.Multiline)]
    private static partial Regex Shell();

    [GeneratedRegex(@"(?<comment>--\[\[.*?(?:\]\]|\z)|--[^\n]*|#[^\n]*)|(?<string>""(?:\\.|[^""\\\n])*""?|'(?:\\.|[^'\\\n])*'?)|(?<lit>\b(?:true|false|nil)\b)|(?<kw>\b(?:and|begin|break|class|def|do|else|elsif|end|ensure|for|function|if|in|local|module|not|or|repeat|require|rescue|return|self|then|unless|until|while|yield)\b)|(?<num>\b\d[\d_]*(?:\.\d+)?\b)", RegexOptions.Singleline)]
    private static partial Regex Ruby();

    [GeneratedRegex(@"(?<comment>/\*.*?(?:\*/|\z)|//[^\n]*)|(?<string>""(?:\\.|[^""\\\n])*""?|'(?:\\.|[^'\\\n])*'?)|(?<kw>@[\w-]+|!important)|(?<prop>(?<=[{;\s])[\w-]+(?=\s*:(?!:)))|(?<num>#[0-9a-fA-F]{3,8}\b|-?\b\d*\.?\d+(?:px|em|rem|%|vh|vw|s|ms|deg|fr|pt|ch)?\b)", RegexOptions.Singleline)]
    private static partial Regex Css();

    [GeneratedRegex(@"(?<comment>//[^\n]*|/\*.*?(?:\*/|\z))|(?<prop>""(?:\\.|[^""\\\n])*""(?=\s*:))|(?<string>""(?:\\.|[^""\\\n])*""?)|(?<lit>\b(?:true|false|null)\b)|(?<num>-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)", RegexOptions.Singleline)]
    private static partial Regex Json();

    [GeneratedRegex(@"(?<comment>(?:^|(?<=\s))#[^\n]*)|(?<prop>^[ \t-]*[\w.\-""']+(?=\s*:(?:\s|$)))|(?<string>""(?:\\.|[^""\\\n])*""?|'[^'\n]*'?)|(?<lit>\b(?:true|false|null|yes|no|on|off)\b|~)|(?<num>-?\b\d+(?:\.\d+)?\b)", RegexOptions.Multiline)]
    private static partial Regex Yaml();

    [GeneratedRegex(@"(?<comment>(?:^|(?<=\s))[#;][^\n]*)|(?<tag>^\s*\[[^\]\n]*\])|(?<prop>^\s*[\w.\-""]+(?=\s*=))|(?<string>"""""".*?(?:""""""|\z)|""(?:\\.|[^""\\\n])*""?|'[^'\n]*'?)|(?<lit>\b(?:true|false)\b)|(?<num>-?\b\d[\d_]*(?:\.\d+)?\b)", RegexOptions.Multiline | RegexOptions.Singleline)]
    private static partial Regex Toml();

    [GeneratedRegex(@"(?<comment>--[^\n]*|/\*.*?(?:\*/|\z))|(?<string>'(?:''|[^'])*'?|""[^""]*""?)|(?<lit>\b(?:null|true|false)\b)|(?<kw>\b(?:select|from|where|insert|into|values|update|set|delete|create|table|drop|alter|add|join|left|right|inner|outer|full|on|group|by|order|having|limit|offset|as|and|or|not|is|in|like|between|distinct|union|all|primary|key|foreign|references|index|view|default|case|when|then|else|end|exists|asc|desc|with|returning|integer|int|text|varchar|real|boolean|date|timestamp)\b)|(?<num>\b\d+(?:\.\d+)?\b)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Sql();

    [GeneratedRegex(@"(?<comment>%[^\n]*)|(?<kw>\\[A-Za-z@]+)|(?<num>\b\d+(?:\.\d+)?(?:pt|cm|mm|em|in)?\b)")]
    private static partial Regex Tex();

    [GeneratedRegex(@"<(?<slash>[/?!]?)(?<name>[A-Za-z][\w:.-]*)(?<rest>[^>]*)>?")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"(?<attr>[\w:@.\-\[\]()#*]+)(?:\s*=\s*(?<value>""[^""]*""?|'[^']*'?|[^\s>""']+))?")]
    private static partial Regex HtmlAttribute();
}
