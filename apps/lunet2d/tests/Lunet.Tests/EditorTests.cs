using Lunet.Compiler;
using Lunet.Editor;

namespace Lunet.Tests;

public class HighlighterTests
{
    private static Dictionary<string, TokenKind> Kinds(string code) =>
        SyntaxHighlighter.Classify(code)
            .GroupBy(s => code.Substring(s.Start, s.Length))
            .ToDictionary(g => g.Key, g => g.First().Kind);

    [Fact]
    public void ClassifiesKeywordsLiteralsCommentsAndTypes()
    {
        const string code = "// nota\nusing System;\npublic class Foo : Bar\n{\n    int x = 42;\n    string s = \"oi\";\n    void M() { if (x > 0) return; Texture2D t = new Texture2D(); Run(); }\n}\n";
        var k = Kinds(code);
        Assert.Equal(TokenKind.Comment, k["// nota"]);
        Assert.Equal(TokenKind.Keyword, k["public"]);
        Assert.Equal(TokenKind.Keyword, k["class"]);
        Assert.Equal(TokenKind.ControlKeyword, k["if"]);
        Assert.Equal(TokenKind.ControlKeyword, k["return"]);
        Assert.Equal(TokenKind.Number, k["42"]);
        Assert.Equal(TokenKind.String, k["\"oi\""]);
        Assert.Equal(TokenKind.Type, k["Foo"]);
        Assert.Equal(TokenKind.Type, k["Bar"]);
        Assert.Equal(TokenKind.Type, k["Texture2D"]);
        Assert.Equal(TokenKind.Method, k["Run"]);
        Assert.Equal(TokenKind.Method, k["M"]);
    }

    [Fact]
    public void SpansAreOrderedInBoundsAndDoNotOverlap()
    {
        var code = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../EditorTests.cs"));
        var spans = SyntaxHighlighter.Classify(code);
        Assert.NotEmpty(spans);
        var end = 0;
        foreach (var s in spans)
        {
            Assert.True(s.Start >= end, $"sobreposição em {s.Start}");
            Assert.True(s.Start + s.Length <= code.Length);
            end = s.Start + s.Length;
        }
    }

    [Fact]
    public void ToleratesBrokenCode()
    {
        var spans = SyntaxHighlighter.Classify("public class { int = ; \"aberta");
        Assert.NotEmpty(spans);
    }

    [Fact]
    public void HighlightsInterpolatedAndRawStringsAndDirectives()
    {
        var k = Kinds("#region x\nvar a = $\"v={1}\";\nvar b = \"\"\"raw\"\"\";\n#endregion\n");
        Assert.Contains(TokenKind.Preprocessor, k.Values);
        Assert.Contains(TokenKind.String, k.Values);
    }
}

public class IndentationTests
{
    [Fact]
    public void Enter_KeepsIndentAndDeepensAfterBrace()
    {
        var text = "class A\n{\n    void M() {";
        var edit = IndentationService.NewLine(text, text.Length);
        Assert.Equal("\n        ", edit.Insert);
        Assert.Equal(text.Length + edit.Insert.Length, edit.CaretAfter);

        var plain = "    int x;";
        Assert.Equal("\n    ", IndentationService.NewLine(plain, plain.Length).Insert);
    }

    [Fact]
    public void Enter_BetweenBraces_CreatesMiddleLine()
    {
        var text = "    void M() {}";
        var caret = text.Length - 1;
        var edit = IndentationService.NewLine(text, caret);
        Assert.Equal("    void M() {\n        \n    }", edit.Apply(text));
        Assert.Equal("    void M() {\n        ".Length, edit.CaretAfter);
    }

    [Fact]
    public void CloseBrace_DedentsOnBlankLine_OnlyThen()
    {
        var text = "{\n    x;\n        ";
        var edit = IndentationService.CloseBrace(text, text.Length)!.Value;
        Assert.Equal("{\n    x;\n    }", edit.Apply(text));
        Assert.Null(IndentationService.CloseBrace("int x = ", 8));
    }
}

public class UndoTests
{
    private static string ApplyEdit(string text, TextEdit e) => e.Apply(text);

    [Fact]
    public void TypingCoalescesAndUndoRestoresText()
    {
        var h = new UndoHistory();
        var text = "";
        long t = 0;
        foreach (var c in "hello")
        {
            h.Record(text.Length, "", c.ToString(), text.Length, t += 100);
            text += c;
        }
        text += "!"; h.Record(5, "", "!", 5, t += 5000); // pausa longa: passo novo
        var undo1 = h.Undo()!.Value;
        text = ApplyEdit(text, undo1);
        Assert.Equal("hello", text);
        text = ApplyEdit(text, h.Undo()!.Value);
        Assert.Equal("", text);
        Assert.False(h.CanUndo);
        text = ApplyEdit(text, h.Redo()!.Value);
        Assert.Equal("hello", text);
        text = ApplyEdit(text, h.Redo()!.Value);
        Assert.Equal("hello!", text);
    }

    [Fact]
    public void BackspacesCoalesce_AndNewEditClearsRedo()
    {
        var h = new UndoHistory();
        var text = "abcdef";
        long t = 0;
        for (var i = 5; i >= 3; i--) { h.Record(i, text[i].ToString(), "", i + 1, t += 50); text = text.Remove(i, 1); }
        Assert.Equal("abc", text);
        text = ApplyEdit(text, h.Undo()!.Value);
        Assert.Equal("abcdef", text);
        Assert.False(h.CanUndo);
        h.Record(0, "", "x", 0, 9999);
        Assert.False(h.CanRedo);
    }

    [Fact]
    public void NewlinesAndReplacementsAreSeparateSteps()
    {
        var h = new UndoHistory();
        h.Record(0, "", "a", 0, 0);
        h.Record(1, "", "\n", 1, 10);
        h.Record(2, "", "b", 2, 20);
        h.Record(0, "ab", "X", 0, 30);
        Assert.Equal(4, Count(h));
    }

    private static int Count(UndoHistory h)
    {
        var n = 0;
        while (h.Undo() is not null) n++;
        return n;
    }

    [Fact]
    public void ReplaceLast_MakesTypedCharAndRuleOneStep()
    {
        var h = new UndoHistory();
        h.Record(3, "", "\n", 3, 0);
        h.ReplaceLast(3, "", "\n    ");
        var text = "abc\n    def";
        text = h.Undo()!.Value.Apply(text);
        Assert.Equal("abcdef", text);
    }

    [Fact]
    public void RespectsCapacity()
    {
        var h = new UndoHistory(capacity: 3);
        for (var i = 0; i < 10; i++) h.Record(0, "", "line\n", 0, i * 10_000);
        Assert.Equal(3, Count(h));
    }
}

public class FindReplaceTests
{
    [Fact]
    public void Find_CaseWholeWordAndRegex()
    {
        const string text = "Foo foo food FOO";
        Assert.Equal(4, FindReplace.FindAll(text, "foo").Count);
        Assert.Equal(1, FindReplace.FindAll(text, "foo", new FindOptions(MatchCase: true)).Count(m => m.Start == 4));
        Assert.Equal(2, FindReplace.FindAll(text, "foo", new FindOptions(MatchCase: true)).Count);
        Assert.Equal(3, FindReplace.FindAll(text, "foo", new FindOptions(WholeWord: true)).Count);
        Assert.Equal(2, FindReplace.FindAll(text, "fo+d?", new FindOptions(UseRegex: true, MatchCase: true)).Count);
        Assert.Empty(FindReplace.FindAll(text, ""));
    }

    [Fact]
    public void FindNext_WrapsAround()
    {
        var m = FindReplace.FindNext("ab ab", "ab", 1)!.Value;
        Assert.Equal(3, m.Start);
        Assert.Equal(0, FindReplace.FindNext("ab ab", "ab", 4)!.Value.Start);
        Assert.Null(FindReplace.FindNext("ab", "zz", 0));
    }

    [Fact]
    public void ReplaceAll_LiteralAndRegexGroups()
    {
        Assert.Equal("x.b x.b", FindReplace.ReplaceAll("a.b a.b", "a", "x", default, out var n));
        Assert.Equal(2, n);
        Assert.Equal("$1 $1", FindReplace.ReplaceAll("aa aa", "aa", "$1", default, out _));
        Assert.Equal("[aa] [aa]", FindReplace.ReplaceAll("aa aa", "(a+)", "[$1]", new FindOptions(UseRegex: true), out _));
    }

    [Fact]
    public void InvalidRegex_ThrowsFormatException() =>
        Assert.Throws<FormatException>(() => FindReplace.FindAll("x", "(", new FindOptions(UseRegex: true)));
}

public class CodeAnalyzerTests
{
    private const string Game = """
        using System.Numerics;
        using Lunet;
        using Lunet.Graphics;

        public sealed class Meu : Game
        {
            Texture2D bola = null!;
            int pontos;

            protected override void Update(GameTime time)
            {
                pontos++;
                bola.
            }

            void Outro() { Vec }
        }
        """;

    private static CodeAnalyzer NewAnalyzer(string code)
    {
        var a = new CodeAnalyzer(new LoadedAssembliesReferenceProvider(typeof(Lunet.Game).Assembly));
        a.SetFile("Game.cs", code);
        return a;
    }

    private static int After(string code, string marker) => code.IndexOf(marker, StringComparison.Ordinal) + marker.Length;

    [Fact]
    public void MemberCompletion_ListsInstanceMembersOnly()
    {
        var a = NewAnalyzer(Game);
        var items = a.GetCompletions("Game.cs", After(Game, "bola."));
        Assert.Contains(items, i => i.Label == "Width" && i.Kind == CompletionKind.Property);
        Assert.Contains(items, i => i.Label == "Dispose" && i.Kind == CompletionKind.Method);
        Assert.DoesNotContain(items, i => i.Label == "FromPixels"); // estático
        Assert.DoesNotContain(items, i => i.Kind == CompletionKind.Keyword);
    }

    [Fact]
    public void MemberCompletion_FiltersByPrefixAndReplacesIt()
    {
        var code = Game.Replace("bola.\n", "bola.Wi\n");
        var a = NewAnalyzer(code);
        var pos = After(code, "bola.Wi");
        var items = a.GetCompletions("Game.cs", pos);
        var width = Assert.Single(items, i => i.Label == "Width");
        Assert.Equal(pos - 2, width.ReplaceStart);
        Assert.Equal(2, width.ReplaceLength);
    }

    [Fact]
    public void StaticCompletion_OnTypeAndOverloadsAreGrouped()
    {
        var code = Game.Replace("bola.\n", "Texture2D.\n");
        var items = NewAnalyzer(code).GetCompletions("Game.cs", After(code, "Texture2D."));
        Assert.Contains(items, i => i.Label == "FromPixels");
        Assert.Contains(items, i => i.Label == "CreateSolid");
        Assert.DoesNotContain(items, i => i.Label == "Width");
    }

    [Fact]
    public void ScopeCompletion_IncludesLocalsMembersTypesAndKeywords()
    {
        var a = NewAnalyzer(Game);
        var items = a.GetCompletions("Game.cs", After(Game, "void Outro() { Vec"));
        Assert.Contains(items, i => i.Label == "Vector2" && i.Kind == CompletionKind.Struct);

        var all = a.GetCompletions("Game.cs", After(Game, "pontos++;\n        "), maxItems: 500);
        Assert.Contains(all, i => i.Label == "pontos" && i.Kind == CompletionKind.Field);
        Assert.Contains(all, i => i.Label == "time" && i.Kind == CompletionKind.Parameter);
        Assert.Contains(all, i => i.Label == "if" && i.Kind == CompletionKind.Keyword);
        Assert.Contains(all, i => i.Label == "GraphicsDevice");
    }

    [Fact]
    public void NoCompletionsInsideCommentsOrStrings()
    {
        var code = "using Lunet;\npublic class A : Game { void M() { // Tex\n var s = \"Tex\"; } }";
        var a = NewAnalyzer(code);
        Assert.Empty(a.GetCompletions("Game.cs", After(code, "// Tex")));
        Assert.Empty(a.GetCompletions("Game.cs", After(code, "\"Tex")));
    }

    [Fact]
    public void Hover_ShowsSignatureOfSymbol()
    {
        var a = NewAnalyzer(Game);
        var hover = a.GetHover("Game.cs", Game.IndexOf("pontos++", StringComparison.Ordinal) + 2)!;
        Assert.Contains("pontos", hover.Signature);
        Assert.Equal("Field", hover.Kind);
        Assert.Null(a.GetHover("Game.cs", Game.IndexOf("public", StringComparison.Ordinal)));
    }

    [Fact]
    public void GoToDefinition_AcrossFiles_AndNullForFrameworkTypes()
    {
        var a = NewAnalyzer("using Lunet;\npublic class Uso : Game { Outra o = new Outra(); Texture2D x; }");
        a.SetFile("Code/Outra.cs", "public class Outra\n{\n}\n");
        var code = "using Lunet;\npublic class Uso : Game { Outra o = new Outra(); }";
        a.SetFile("Game.cs", code);
        var def = a.GetDefinition("Game.cs", code.IndexOf("Outra o", StringComparison.Ordinal) + 1)!;
        Assert.Equal("Code/Outra.cs", def.FilePath);
        Assert.Equal(1, def.Line);
        Assert.Null(a.GetDefinition("Game.cs", code.IndexOf("Game", 20, StringComparison.Ordinal) + 1));
    }

    [Fact]
    public void FindReferences_ReturnsAllUsesInAllFiles()
    {
        var a = NewAnalyzer("using Lunet;\npublic class A : Game { public int Score; void M() { Score++; } }");
        a.SetFile("B.cs", "public class B { int F(A a) => a.Score; }");
        var code = "using Lunet;\npublic class A : Game { public int Score; void M() { Score++; } }";
        var refs = a.FindReferences("Game.cs", code.IndexOf("Score++", StringComparison.Ordinal));
        Assert.Equal(3, refs.Count); // declaração + uso em A + uso em B
        Assert.Contains(refs, r => r.FilePath == "B.cs");
    }

    [Fact]
    public void LiveDiagnostics_UpdateWhenFileChangesAndRemoved()
    {
        var a = NewAnalyzer("using Lunet;\npublic class A : Game { int x = \"a\"; }");
        var diag = Assert.Single(a.GetDiagnostics("Game.cs"), d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal("CS0029", diag.Id);
        a.SetFile("Game.cs", "using Lunet;\npublic class A : Game { int x = 1; }");
        Assert.DoesNotContain(a.GetDiagnostics("Game.cs"), d => d.Severity == DiagnosticSeverity.Error);
        a.RemoveFile("Game.cs");
        Assert.Empty(a.GetDiagnostics("Game.cs"));
    }
}
