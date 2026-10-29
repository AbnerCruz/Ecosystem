using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-141: writing code in the Urbe keeps its indentation (Enter, Tab, Shift+Tab, a closing brace).</summary>
public sealed class CodeEditingTests
{
    private static (string Text, int Caret) Enter(string withCaret)
    {
        int at = withCaret.IndexOf('|');
        var edit = CodeEditing.NewLine(withCaret.Remove(at, 1), at, at);
        return (edit.Text, edit.SelectionStart);
    }

    private static string Show((string Text, int Caret) r) => r.Text.Insert(r.Caret, "|");

    [Fact]
    public void EnterKeepsTheIndentationOfTheLine() =>
        Assert.Equal("    a = 1\n    |", Show(Enter("    a = 1|")));

    [Fact]
    public void EnterAfterAnOpeningGoesOneLevelIn()
    {
        Assert.Equal("if (x) {\n    |", Show(Enter("if (x) {|")));
        Assert.Equal("def f():\n    |", Show(Enter("def f():|")));
        Assert.Equal("  lista = [\n    |", Show(Enter("  lista = [|")));
    }

    [Fact]
    public void EnterBetweenBracesOpensTheBlock() =>
        Assert.Equal("function f() {\n    |\n}", Show(Enter("function f() {|}")));

    [Fact]
    public void TheUnitIsTheFilesOwn()
    {
        Assert.Equal("\t", CodeEditing.IndentUnit("a {\n\tb\n}"));
        Assert.Equal("  ", CodeEditing.IndentUnit("a:\n  b: 1\n  c:\n    d: 2"));
        Assert.Equal("    ", CodeEditing.IndentUnit("x = 1"));
        Assert.Equal("if (x) {\n  |", Show(Enter("a {\n  b\n}\nif (x) {|")) is var s ? s[(s.IndexOf("if", StringComparison.Ordinal))..] : "");
    }

    [Fact]
    public void TabIndentsTheSelectedLinesAndShiftTabTakesItBack()
    {
        var text = "a\nb\nc";
        var tab = CodeEditing.Indent(text, 0, 3, outdent: false);
        Assert.Equal("    a\n    b\nc", tab.Text);
        var back = CodeEditing.Indent(tab.Text, tab.SelectionStart, tab.SelectionEnd, outdent: true);
        Assert.Equal(text, back.Text);
    }

    [Fact]
    public void TabWithoutSelectionInsertsOneLevelAtTheCaret()
    {
        var edit = CodeEditing.Indent("x", 0, 0, outdent: false);
        Assert.Equal("    x", edit.Text);
        Assert.Equal(4, edit.SelectionStart);
    }

    [Fact]
    public void AClosingBraceOnAnEmptyLineGoesOneLevelOut()
    {
        var edit = CodeEditing.Closing("if (x) {\n    a\n    ", "if (x) {\n    a\n    ".Length, '}');
        Assert.NotNull(edit);
        Assert.Equal("if (x) {\n    a\n}", edit!.Text);
        Assert.Null(CodeEditing.Closing("x = f(    ", 10, '}'));   // not alone on its line
    }
}
