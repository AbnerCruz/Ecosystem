using System.Text;

namespace Urbe.Core.Tests;

public sealed class InterruptedWritesTests
{
    private const string Guid1 = "0123456789abcdef0123456789abcdef";
    private const string Guid2 = "fedcba9876543210fedcba9876543210";

    private static Dictionary<string, VaultFile> Files(params (string Path, string Text)[] files) =>
        files.ToDictionary(f => f.Path, f => new VaultFile(f.Path, Encoding.UTF8.GetBytes(f.Text)), StringComparer.Ordinal);

    [Fact]
    public void ACopyEqualToTheFileMeansTheWriteNeverStarted()
    {
        var plan = InterruptedWrites.Plan(Files(("Nota.md", "texto"), ("Nota.md.urbe-recovery-" + Guid1, "texto")));
        var r = Assert.Single(plan);
        Assert.Equal(InterruptedWrites.Resolution.Unchanged, r.Resolution);
        Assert.Equal("Nota.md", r.Target);
    }

    [Fact]
    public void AFileCutAtTheStartOrGoneGetsThePreviousTextBack()
    {
        var empty = Assert.Single(InterruptedWrites.Plan(Files(("Pasta/Nota.md", ""), ("Pasta/Nota.md.urbe-recovery-" + Guid1, "o texto inteiro"))));
        Assert.Equal(InterruptedWrites.Resolution.Restore, empty.Resolution);
        Assert.Equal("o texto inteiro", empty.Text);
        var gone = Assert.Single(InterruptedWrites.Plan(Files(("Pasta/Nota.md.urbe-recovery-" + Guid1, "o texto"))));
        Assert.Equal(InterruptedWrites.Resolution.Restore, gone.Resolution);
        Assert.Equal("Pasta/Nota.md", gone.Target);
    }

    [Fact]
    public void TwoDifferentTextsAreBothKeptSideBySide()
    {
        var plan = InterruptedWrites.Plan(Files(
            ("Pasta/Nota.md", "texto novo, talvez cortado"),
            ("Pasta/Nota (antes da gravação interrompida).md", "um nome já usado"),
            ("Pasta/Nota.md.urbe-recovery-" + Guid1, "texto anterior"),
            ("Pasta/Nota.md.urbe-recovery-" + Guid2, "texto ainda mais antigo")));
        Assert.Equal(2, plan.Count);
        Assert.All(plan, r => Assert.Equal(InterruptedWrites.Resolution.KeepBoth, r.Resolution));
        Assert.Equal("Pasta/Nota (antes da gravação interrompida 2).md", plan[0].KeepAs);
        Assert.Equal("Pasta/Nota (antes da gravação interrompida 3).md", plan[1].KeepAs);
        Assert.Equal("texto anterior", plan[0].Text);
    }

    [Fact]
    public void TheAppsOwnFilesAreLeftToTheirReaders() =>
        Assert.Empty(InterruptedWrites.Plan(Files((".urbe/notas.json", "{\"version\":1,\"no"), (".urbe/notas.json.urbe-recovery-" + Guid1, "{\"version\":1}"))));

    [Fact]
    public void OnlyTheStoragesCopiesAreRecognised()
    {
        Assert.True(InterruptedWrites.IsCopy("a/b.md.urbe-recovery-" + Guid1));
        Assert.False(InterruptedWrites.IsCopy("a/b.md.urbe-recovery-123"));
        Assert.False(InterruptedWrites.IsCopy("Receita urbe-recovery.md"));
        Assert.Empty(InterruptedWrites.Plan(Files(("Nota.md", "x"), ("Outra.md", "y"))));
    }
}
