using Urbe.Core;

namespace Urbe.Core.Tests;

public sealed class NoteProgressTests
{
    [Fact]
    public void ManualProgressReportsActualRemainingAndRejectsFalseCompletion()
    {
        var result = NoteProgress.Evaluate("label: Coraline\nvalue: 54\ntotal: 224", "");
        Assert.True(result.IsValid);
        Assert.Equal(170m, result.Remaining);
        Assert.Contains("24,1%", result.Summary);
        foreach (var body in new[] { "value: -1\ntotal: 2", "value: 3\ntotal: 2", "value: x\ntotal: 2", "value: 0\ntotal: 0", "value: 1\ntotal: 2\ntotal: 3" })
            Assert.False(NoteProgress.Evaluate(body, "").IsValid);
    }

    [Fact]
    public void TasksExcludeFencesFrontmatterCommentsOrdinaryAndOutOfScopeLists()
    {
        const string note = "---\ntitle: test\n---\n# Dentro\n- [x] um\n  - [ ] dois\n1. [X] três\n\n```md\n- [x] literal\n```\n~~~\n- [ ] literal\n~~~\n%%\n- [x] privado\n%%\n`- [x] inline`\n# Fora\n- [ ] quatro";
        var result = NoteProgress.Evaluate("source: tasks\nsection: Dentro", note);
        Assert.True(result.IsValid);
        Assert.Equal(2m, result.Value);
        Assert.Equal(3m, result.Total);
        Assert.Equal(1m, result.Remaining);
        Assert.Equal(4m, NoteProgress.Evaluate("source: tasks", note).Total);
        var none = NoteProgress.Evaluate("source: tasks", "Sem tarefas");
        Assert.Equal("Sem tarefas", none.Summary);
        Assert.Equal(0m, none.Percent);
        Assert.False(NoteProgress.Evaluate("source: tasks\nsection: ausente", note).IsValid);
    }

    [Fact]
    public void PropertyAndStableNoteReferencesResolveLiveAndNeverStoreDerivedTotals()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "A.md", Content = "- [x] um\n- [ ] dois" });
        string? Resolve(string id) => store.Get(id) is { } d && d.Id == id ? d.Content : null;
        var directive = "source: tasks\nnote: " + note.Id;
        Assert.Equal(50m, NoteProgress.Evaluate(directive, "", Resolve).Percent);
        store.Upsert(new DocumentInput { Id = note.Id, Path = "B.md", Content = "- [x] um\n- [x] dois" });
        Assert.Equal(100m, NoteProgress.Evaluate(directive, "", Resolve).Percent);
        store.Remove(note.Id);
        Assert.False(NoteProgress.Evaluate(directive, "", Resolve).IsValid);
        Assert.Equal(25m, NoteProgress.Evaluate("value: $lidas\ntotal: $paginas", "---\nlidas: 25\npaginas: 100\n---\n").Percent);
        Assert.False(NoteProgress.Evaluate("value: $ausente\ntotal: 100", "").IsValid);
        Assert.DoesNotContain("value:", directive, StringComparison.Ordinal);
        Assert.DoesNotContain("total:", directive, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingTasksAreFoundAtTheirLinesEvenWithSealsAndFrontmatter()
    {
        const string note = "---\ntitle: t\n---\n# Hoje\n- [x] feito\n- [ ] ler {ia:\"m\"}\n  - [ ] resumir\n# Depois\n- [ ] fora";
        var result = NoteProgress.Evaluate("source: tasks\nsection: Hoje", note);
        Assert.Equal([5, 6], result.Pending.Select(p => p.Line));
        Assert.Equal("resumir", result.Pending[1].Text);
        Assert.Empty(NoteProgress.Evaluate("label: x\nvalue: 1\ntotal: 2", note).Pending);
    }

    [Fact]
    public void StepMovesTheValueWhereItLivesAndStopsAtTheEnds()
    {
        const string block = "```progress\nlabel: Livro\nvalue: 54\ntotal: 224\n```";
        var note = "Antes\n\n" + block + "\n";
        var start = note.IndexOf("```", StringComparison.Ordinal);
        var more = NoteProgress.Step(note, start, start + block.Length, 1);
        Assert.Contains("value: 55\n", more);
        Assert.StartsWith("Antes", more);
        var top = "```progress\nvalue: 2\ntotal: 2\n```";
        Assert.Null(NoteProgress.Step(top, 0, top.Length, 1));
        var low = "```progress\nvalue: 0\ntotal: 2\n```";
        Assert.Null(NoteProgress.Step(low, 0, low.Length, -1));
        var linked = "---\nlidas: 25\npaginas: 100\n---\n```progress\nvalue: $lidas\ntotal: $paginas\n```";
        var b = linked.IndexOf("```", StringComparison.Ordinal);
        var moved = NoteProgress.Step(linked, b, linked.Length, -1)!;
        Assert.Contains("lidas: 24", moved);
        Assert.Contains("value: $lidas", moved);
        var tasks = "```progress\nsource: tasks\n```\n- [ ] a";
        Assert.Null(NoteProgress.Step(tasks, 0, tasks.IndexOf("\n-", StringComparison.Ordinal), 1));
    }

    [Fact]
    public void SummariesReadEveryBlockAsTheScreenDoes()
    {
        const string note = "```progress\nlabel: Livro\nvalue: 54\ntotal: 224\n```\n\n~~~\n```progress\nvalue: 1\ntotal: 1\n```\n~~~\n\n```progresso\nlabel: Tarefas\nsource: tasks\n```\n- [x] a\n- [ ] b";
        var all = NoteProgress.Summaries(note).ToList();
        Assert.Contains("Livro: 54/224 · 24,1% · faltam 170", all);
        Assert.Contains("Tarefas: 1/2 · 50% · faltam 1", all);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void CodeFencesFollowCommonMarkInTheEditorsBlocksAndTheHtml()
    {
        // a ~~~ example holds ``` lines; a ```` fence holds a ``` one; only the same mark, as long or longer, closes
        const string note = "~~~md\n```progress\nvalue: 1\ntotal: 1\n```\n~~~\n\n````\n```js\nx\n```\n````\n\nfim";
        var blocks = MarkdownEngine.SplitBlocks(note);
        Assert.Equal(["code", "code", "paragraph"], blocks.Select(b => b.Kind));
        Assert.Equal("fim", note[blocks[2].Start..blocks[2].End]);
        var html = MarkdownEngine.Render(note);
        Assert.Contains("```progress", html);
        Assert.Contains("```js", html);
        Assert.Contains("data-lang=\"md\"", html);
        Assert.Empty(NoteProgress.Summaries(note));
    }
}
