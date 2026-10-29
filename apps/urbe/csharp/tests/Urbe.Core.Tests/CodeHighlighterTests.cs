using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-141: the colours of code in the editor (what each piece of a code file is).</summary>
public sealed class CodeHighlighterTests
{
    private static List<(string Text, CodeTokenKind Kind)> Of(string code, string path) =>
        [.. CodeHighlighter.Tokens(code, CodeHighlighter.LanguageOf(path)!).Select(t => (code.Substring(t.Start, t.Length), t.Kind))];

    [Fact]
    public void TheLanguageComesFromTheExtension()
    {
        Assert.Equal("c", CodeHighlighter.LanguageOf("a/b.ts"));
        Assert.Equal("c", CodeHighlighter.LanguageOf("Program.cs"));
        Assert.Equal("python", CodeHighlighter.LanguageOf("main.py"));
        Assert.Equal("html", CodeHighlighter.LanguageOf("index.html"));
        Assert.Equal("css", CodeHighlighter.LanguageOf("site.scss"));
        Assert.Equal("json", CodeHighlighter.LanguageOf("dados.json"));
        Assert.Null(CodeHighlighter.LanguageOf("Nota.md"));
        Assert.Null(CodeHighlighter.LanguageOf("tabela.csv"));
    }

    [Fact]
    public void CLikeCode()
    {
        var t = Of("// soma\nconst x = \"a // b\" + 42; /* fim */ return x;", "app.js");
        Assert.Contains(("// soma", CodeTokenKind.Comment), t);
        Assert.Contains(("const", CodeTokenKind.Keyword), t);
        Assert.Contains(("\"a // b\"", CodeTokenKind.String), t);   // a comment inside a string is the string's
        Assert.Contains(("42", CodeTokenKind.Number), t);
        Assert.Contains(("/* fim */", CodeTokenKind.Comment), t);
        Assert.Contains(("return", CodeTokenKind.Keyword), t);
        Assert.DoesNotContain(t, x => x.Text == "x");
    }

    [Fact]
    public void Python()
    {
        var t = Of("def f(a):\n    # nada\n    return None if a else 'x'", "m.py");
        Assert.Contains(("def", CodeTokenKind.Keyword), t);
        Assert.Contains(("# nada", CodeTokenKind.Comment), t);
        Assert.Contains(("None", CodeTokenKind.Literal), t);
        Assert.Contains(("'x'", CodeTokenKind.String), t);
    }

    [Fact]
    public void HtmlWithItsScriptAndStyle()
    {
        var t = Of("<!-- c --><a href=\"x.html\">oi</a><script>let n = 1;</script><style>p { color: red; }</style>", "i.html");
        Assert.Contains(("<!-- c -->", CodeTokenKind.Comment), t);
        Assert.Contains(("a", CodeTokenKind.Tag), t);
        Assert.Contains(("href", CodeTokenKind.Attribute), t);
        Assert.Contains(("\"x.html\"", CodeTokenKind.String), t);
        Assert.Contains(("let", CodeTokenKind.Keyword), t);   // the script is code
        Assert.Contains(("color", CodeTokenKind.Property), t);   // the style is CSS
        Assert.DoesNotContain(t, x => x.Text == "oi");
    }

    [Fact]
    public void JsonKeysAndValues()
    {
        var t = Of("{\"nome\": \"Ana\", \"idade\": 3, \"ok\": true}", "a.json");
        Assert.Contains(("\"nome\"", CodeTokenKind.Property), t);
        Assert.Contains(("\"Ana\"", CodeTokenKind.String), t);
        Assert.Contains(("3", CodeTokenKind.Number), t);
        Assert.Contains(("true", CodeTokenKind.Literal), t);
    }

    [Fact]
    public void SqlIgnoresCase()
    {
        var t = Of("select nome FROM pessoas -- todas", "q.sql");
        Assert.Contains(("select", CodeTokenKind.Keyword), t);
        Assert.Contains(("FROM", CodeTokenKind.Keyword), t);
        Assert.Contains(("-- todas", CodeTokenKind.Comment), t);
    }

    [Fact]
    public void TokensAreInOrderAndNeverOverlap()
    {
        var code = "<div class='a'><script>if (a < b) { s = \"</div>\"; }</script></div>";
        var tokens = CodeHighlighter.Tokens(code, "html");
        for (int i = 1; i < tokens.Count; i++) Assert.True(tokens[i].Start >= tokens[i - 1].Start + tokens[i - 1].Length);
        Assert.All(tokens, x => Assert.True(x.Start >= 0 && x.Start + x.Length <= code.Length));
    }
}
