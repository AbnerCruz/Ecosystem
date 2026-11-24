using Lunet.Compiler;
using Lunet.Core;
using Lunet.Editor;

namespace Lunet.Tests;

public sealed class EditorF4Tests
{
    [Theory]
    [InlineData("Draw(size: 2, name: |", "Draw", "name")]
    [InlineData("Draw(Inner(1, |), 2)", "Inner", "second")]
    [InlineData("Draw(Inner(1, 2), |)", "Draw", "size")]
    [InlineData("Generic<int>(|)", "Generic<int>", "value")]
    [InlineData("new G(|)", "G", "value")]
    public void SignatureUsesInnermostCallAndNamedSlots(string call, string label, string parameter)
    {
        var source = "class G { G(int value) {} void Draw(string name, int size) {} void Draw(string name) {} " +
            "string Inner(int first, int second) => \"\"; T Generic<T>(T value) => value; void M() { " + call + "; } }";
        int caret = source.IndexOf('|');
        var analyzer = new CodeAnalyzer(new LoadedAssembliesReferenceProvider());
        analyzer.SetFile("Game.cs", source.Remove(caret, 1));
        var help = Assert.IsType<CallSignatureHelp>(analyzer.GetCallSignatures("Game.cs", caret));
        Assert.Contains(help.Signatures, item => item.Label == label && item.ActiveParameter is int index && item.Parameters[index].Name == parameter);
        if (label == "Draw") Assert.Equal(2, help.Signatures.Count);
    }

    [Fact]
    public void SymbolsFindActualDeclarationsAcrossFiles()
    {
        var analyzer = new CodeAnalyzer(new LoadedAssembliesReferenceProvider());
        analyzer.SetFile("Game.cs", "class G { void Run() { new A().Render(); } }");
        analyzer.SetFile("A.cs", "class A { public void Render() {} public void Render(int count) {} }");
        var results = analyzer.FindProjectSymbols("rend");
        Assert.Equal(2, results.Count);
        Assert.All(results, symbol => Assert.Equal("A.cs", symbol.Location.FilePath));
        Assert.NotEqual(results[0].Location.Column, results[1].Location.Column);
    }

    [Fact]
    public void BlockAndPairsRestoreTheirOwnSelectionInAtomicHistory()
    {
        const string text = "    Do();";
        var block = IndentationService.InsertBlock(text, 4, text.Length);
        Assert.Equal("    {\n        Do();\n    }", block.Apply(text));
        Assert.Equal(block.Apply(text).IndexOf("Do();", StringComparison.Ordinal), block.SelectionStart);
        var history = new UndoHistory();
        history.Record(3, "", " ", 3, 1);
        history.RecordAtomic(block, text[4..], 4, text.Length);
        history.Record(block.SelectionStart, "", "x", block.SelectionStart, 2);
        Assert.Equal(1, history.Undo()!.Value.DeleteLength);
        Assert.Equal(new Selection(4, text.Length), history.UndoWithSelection()!.Value.Selection);
        Assert.Equal(new Selection(block.SelectionStart, block.SelectionEnd), history.RedoWithSelection()!.Value.Selection);
        var pair = IndentationService.InsertPair("olá", 0, 3, "(", ")");
        Assert.Equal("(olá)", pair.Apply("olá"));
        Assert.Equal((1, 4), (pair.SelectionStart, pair.SelectionEnd));
    }

    [Fact]
    public void TabsReplacePrimaryKeepExplicitTabsAndRestoreFilePositions()
    {
        var state = new EditorDocuments();
        state.Open("Game.cs");
        state.Capture(new(8, 15, 2, 500));
        state.Open("Helpers.cs", additional: true);
        state.Open("Other.cs");
        Assert.Equal(new[] { "Other.cs", "Helpers.cs" }, state.Tabs);
        state.Open("Game.cs");
        Assert.Equal(new EditorViewport(8, 15, 2, 500), state.Viewport);
        var copy = System.Text.Json.JsonSerializer.Deserialize<EditorDocuments>(System.Text.Json.JsonSerializer.Serialize(state))!;
        Assert.Equal(state.Tabs, copy.Tabs);
        Assert.Equal(state.Viewport, copy.Viewport);
        copy.Close();
        Assert.Equal("Helpers.cs", copy.Current);
        copy.Close();
        Assert.Null(copy.Current);
    }
}
