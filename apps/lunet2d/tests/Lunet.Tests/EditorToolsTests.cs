using Lunet.Editor;

namespace Lunet.Tests;

public class LineOperationsTests
{
    [Fact]
    public void DuplicateLines_CopiesTheSelectedLinesBelow()
    {
        var edit = LineOperations.DuplicateLines("a\nb\nc", 2, 2);
        Assert.Equal("a\nb\nb\nc", edit.Apply("a\nb\nc"));
        Assert.Equal("a\nb\nc\nc", LineOperations.DuplicateLines("a\nb\nc", 4, 5).Apply("a\nb\nc"));
        Assert.Equal("a\nb\na\nb\nc", LineOperations.DuplicateLines("a\nb\nc", 0, 3).Apply("a\nb\nc"));
    }

    [Fact]
    public void DeleteLines_RemovesWholeLines_IncludingTheLastOne()
    {
        Assert.Equal("a\nc", LineOperations.DeleteLines("a\nb\nc", 2, 2).Apply("a\nb\nc"));
        Assert.Equal("a\nb", LineOperations.DeleteLines("a\nb\nc", 4, 4).Apply("a\nb\nc"));
        Assert.Equal("c", LineOperations.DeleteLines("a\nb\nc", 0, 3).Apply("a\nb\nc"));
    }

    [Fact]
    public void MoveLines_SwapsWithNeighbour_AndStopsAtTheEdges()
    {
        const string text = "a\nb\nc";
        Assert.Equal("b\na\nc", LineOperations.MoveLines(text, 2, 2, up: true)!.Value.Apply(text));
        Assert.Equal("a\nc\nb", LineOperations.MoveLines(text, 2, 2, up: false)!.Value.Apply(text));
        Assert.Equal("a\nc\nb", LineOperations.MoveLines(text, 2, 2, up: false)!.Value.Apply(text));
        Assert.Null(LineOperations.MoveLines(text, 0, 0, up: true));
        Assert.Null(LineOperations.MoveLines(text, 4, 4, up: false));
        Assert.Equal("a\nc\nb", LineOperations.MoveLines(text, 4, 4, up: true)!.Value.Apply(text));
    }

    [Fact]
    public void ToggleComment_CommentsAndUncomments()
    {
        const string text = "    a();\n    b();\n";
        var commented = LineOperations.ToggleComment(text, 0, 13).Apply(text);
        Assert.Equal("    // a();\n    // b();\n", commented);
        var edit = LineOperations.ToggleComment(commented, 0, commented.Length - 1);
        Assert.Equal(text, edit.Apply(commented));
    }

    [Fact]
    public void IndentAndOutdent_ShiftBlocks()
    {
        const string text = "a\n\nb";
        Assert.Equal("    a\n\n    b", LineOperations.Indent(text, 0, text.Length).Apply(text));
        Assert.Equal("a\n\nb", LineOperations.Outdent("    a\n\n  b", 0, 10).Apply("    a\n\n  b"));
    }
}

public class ShortcutTests
{
    [Fact]
    public void Resolve_MapsKeyCombinations_AndIgnoresUnknown()
    {
        Assert.Equal(EditorCommand.Save, Shortcuts.Resolve("S", true, false, false));
        Assert.Equal(EditorCommand.Redo, Shortcuts.Resolve("Z", true, true, false));
        Assert.Equal(EditorCommand.MoveLineUp, Shortcuts.Resolve("Up", false, false, true));
        Assert.Equal(EditorCommand.None, Shortcuts.Resolve("Q", true, false, false));
    }

    [Fact]
    public void All_ListsEveryCommandOnce_WithReadableKeys()
    {
        var all = Shortcuts.All();
        Assert.Equal(all.Count, all.Select(a => a.Command).Distinct().Count());
        Assert.Contains(all, a => a.Command == EditorCommand.Save && a.Keys == "Ctrl+S");
        Assert.Contains(all, a => a.Command == EditorCommand.ToggleComment && a.Keys == "Ctrl+/");
    }
}

public class PieceTableTests
{
    [Fact]
    public void InsertDeleteAndLines_MatchAStringBuilder_ForRandomEdits()
    {
        var random = new Random(1234);
        var reference = new System.Text.StringBuilder("first\nsecond\nthird");
        var table = new PieceTable(reference.ToString());
        for (var i = 0; i < 4000; i++)
        {
            if (reference.Length == 0 || random.Next(3) > 0)
            {
                var at = random.Next(reference.Length + 1);
                var text = new string(Enumerable.Range(0, random.Next(1, 6)).Select(_ => "ab\nc"[random.Next(4)]).ToArray());
                reference.Insert(at, text);
                table.Insert(at, text);
            }
            else
            {
                var at = random.Next(reference.Length);
                var length = random.Next(1, Math.Min(8, reference.Length - at) + 1);
                reference.Remove(at, length);
                table.Delete(at, length);
            }
            if (i % 250 == 0)
            {
                var expected = reference.ToString();
                Assert.Equal(expected, table.ToString());
                var lines = expected.Split('\n');
                Assert.Equal(lines.Length, table.LineCount);
                var line = random.Next(lines.Length);
                Assert.Equal(lines[line], table.GetLine(line));
                var position = random.Next(expected.Length + 1);
                Assert.Equal(expected.AsSpan(0, position).Count('\n'), table.LineOf(position));
                if (expected.Length > 0) Assert.Equal(expected[position % expected.Length], table[position % expected.Length]);
            }
        }
        Assert.Equal(reference.ToString(), table.ToString());
        Assert.Equal(reference.Length, table.Length);
    }

    [Fact]
    public void BigDocument_EditsAreFast_AndSequentialTypingCoalesces()
    {
        var text = string.Join('\n', Enumerable.Range(0, 200_000).Select(i => $"line {i} = {i * 3};"));
        var table = new PieceTable(text);
        Assert.Equal(200_000, table.LineCount);
        Assert.Equal("line 123456 = 370368;", table.GetLine(123_456));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var random = new Random(7);
        for (var i = 0; i < 300; i++) table.Insert(random.Next(table.Length), "x");
        for (var i = 0; i < 200; i++) table.Delete(random.Next(table.Length - 4), 3);
        watch.Stop();
        Assert.True(watch.ElapsedMilliseconds < 2000, $"500 edições em 200 mil linhas levaram {watch.ElapsedMilliseconds} ms");

        var typing = new PieceTable("abc");
        for (var i = 0; i < 1000; i++) typing.Insert(typing.Length, "z");
        Assert.True(typing.PieceCount <= 2);
        Assert.Equal(1003, typing.Length);
    }
}

public class MultiCursorTests
{
    [Fact]
    public void Replace_TypesAtEveryCursor_AndTracksCarets()
    {
        var result = MultiCursor.Replace("a b c", [new Selection(1, 1), new Selection(3, 3), new Selection(5, 5)], "X");
        Assert.Equal("aX bX cX", result.Text);
        Assert.Equal([2, 5, 8], result.Selections.Select(s => s.Start));
    }

    [Fact]
    public void Replace_OverwritesSelections_AndBackspaceDeletes()
    {
        var replaced = MultiCursor.Replace("foo bar foo", [new Selection(0, 3), new Selection(8, 11)], "baz");
        Assert.Equal("baz bar baz", replaced.Text);
        var back = MultiCursor.Backspace("abc abc", [new Selection(3, 3), new Selection(7, 7)]);
        Assert.Equal("ab ab", back.Text);
        var forward = MultiCursor.DeleteForward("abc abc", [new Selection(0, 0), new Selection(4, 4)]);
        Assert.Equal("bc bc", forward.Text);
    }

    [Fact]
    public void SelectNextOccurrence_ExpandsToTheWord_ThenAddsOccurrences_AndWraps()
    {
        const string text = "score = score + score2; score";
        var first = MultiCursor.SelectNextOccurrence(text, [new Selection(2, 2)]);
        Assert.Equal(new Selection(0, 5), first[0]);
        var second = MultiCursor.SelectNextOccurrence(text, first);
        Assert.Equal(2, second.Count);
        Assert.Equal(new Selection(8, 13), second[1]);
        var all = MultiCursor.SelectAllOccurrences(text, [new Selection(0, 5)]);
        Assert.Equal([0, 8, 24], all.Select(s => s.Start)); // "score2" não conta (palavra inteira)
    }

    [Fact]
    public void AddCursorVertically_KeepsTheColumn_ClampedToShortLines()
    {
        const string text = "abcdef\nab\nabcdef";
        var down = MultiCursor.AddCursorVertically(text, [new Selection(4, 4)], above: false);
        Assert.Equal([4, 9], down.Select(s => s.Start));
        var again = MultiCursor.AddCursorVertically(text, down, above: false);
        Assert.Equal(3, again.Count);
        Assert.Equal(12, again[2].Start);
        var up = MultiCursor.AddCursorVertically(text, [new Selection(13, 13)], above: true);
        Assert.Equal([9, 13], up.Select(s => s.Start));
    }

    [Fact]
    public void Replicate_CopiesATypedCharacter_ADeletionAndAReplacement_ToTheOtherCursors()
    {
        // Digitar "X" no primeiro cursor (posição 1).
        var typed = MultiCursor.Replicate("a b c", "aX b c", new Selection(1, 1), [new Selection(3, 3), new Selection(5, 5)])!;
        Assert.Equal("aX bX cX", typed.Text);
        Assert.Equal(0, typed.PrimaryIndex);

        // Backspace no primeiro cursor (posição 3 → apaga o caractere 2).
        var backspace = MultiCursor.Replicate("abc abc", "ab abc", new Selection(3, 3), [new Selection(7, 7)])!;
        Assert.Equal("ab ab", backspace.Text);

        // Substituição da seleção "foo" por "baz".
        var replaced = MultiCursor.Replicate("foo bar foo", "baz bar foo", new Selection(0, 3), [new Selection(8, 11)])!;
        Assert.Equal("baz bar baz", replaced.Text);

        // Edição longe do cursor principal não é replicável.
        Assert.Null(MultiCursor.Replicate("a b c", "a b cX", new Selection(1, 1), [new Selection(3, 3)]));
    }
}
