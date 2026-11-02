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
        Assert.DoesNotContain("50", directive);
    }
}
