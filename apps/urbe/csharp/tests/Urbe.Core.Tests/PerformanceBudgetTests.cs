using System.Diagnostics;
using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// UC-60/UC-65: the core's part of opening, writing, searching and the quick note, timed on a vault of 2,000 notes,
/// in every run of the suite. The budgets are about ten times what each step takes alone on a laptop, so a busy CI
/// machine passes and a regression of an order of magnitude (a step made quadratic, a full re-read where one file was
/// enough) fails. Each step is timed three times and the best one counts.
/// </summary>
public sealed class PerformanceBudgetTests(ITestOutputHelper output)
{
    private const int Count = 2000;

    private static readonly Lazy<VaultFile[]> Files = new(() =>
    {
        var rng = new Random(60);
        var files = new List<VaultFile> { new(".urbe/vault.json", Encoding.UTF8.GetBytes("""{"formatVersion":2,"created":"2026-01-01T00:00:00.000Z"}""")) };
        for (int i = 0; i < Count; i++)
        {
            var text = "# Nota " + i + "\n\n" + string.Join(" ", Enumerable.Repeat("Texto de exemplo para medir, com #tag: e **negrito**.", 12)) +
                "\n\n[[Nota " + rng.Next(Count) + "]] e [[Nota " + rng.Next(Count) + "]]\n";
            files.Add(new VaultFile("Pasta " + i % 12 + "/Sub " + i / 12 % 4 + "/Nota " + i + ".md", Encoding.UTF8.GetBytes(text)));
        }
        return [.. files];
    });

    private double Best(string step, double budgetMs, Action run)
    {
        run();   // warm up (JIT)
        double best = double.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            var watch = Stopwatch.StartNew();
            run();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }
        output.WriteLine($"perf {step}: {best:0.0} ms (limite {budgetMs} ms)");
        Assert.True(best < budgetMs, $"{step}: {best:0.0} ms, limite {budgetMs} ms");
        return best;
    }

    private static NoteLibrary Library(VaultSnapshot snapshot) =>
        NoteLibrary.Open([.. snapshot.Documents.Where(d => d.Text is not null).Select(d => (d.Path, d.Text!, d.Id))], null, null);

    [Fact]
    public void Opening_ReadsAndIndexesTwoThousandNotesWithinBudget()
    {
        var files = Files.Value;
        VaultSnapshot snapshot = null!;
        Best("ler o vault (2000 notas)", 300, () => snapshot = VaultReader.Read(files));
        Assert.Equal(Count, snapshot.Documents.Count(d => d.Path.EndsWith(".md", StringComparison.Ordinal)));
        NoteLibrary library = null!;
        Best("abrir as notas", 800, () => library = Library(snapshot));
        var docs = library.Store.List();
        Best("índice da área de trabalho", 400, () => _ = new WorkspaceIndex(docs.Select(d => (d.Path, (string?)d.Title, (string?)d.Content, d.Modified))).Projects);
        Best("ligações de todas as notas", 1500, () =>
        {
            using var index = new KnowledgeIndex(library.Store);
            foreach (var d in docs) _ = index.Backlinks(d.Id).Count + index.Links(d.Id).Count;
        });
        Best("posições da Constelação", 150, () =>
        {
            var sky = Constellation.Parse(null, out _);
            sky.Update(docs.Select(d => (d.Id, d.Path)));
            _ = sky.Serialize();
        });
    }

    [Fact]
    public void Writing_OneNoteAndAQuickNoteStayWithinBudget()
    {
        var files = Files.Value;
        var snapshot = VaultReader.Read(files);
        var target = "Pasta 3/Sub 0/Nota 51.md";
        Best("plano de gravação de uma nota", 50, () => Assert.NotNull(VaultWriter.PlanOne(files, snapshot, target, "# Nota 51\n\nmudada\n")));

        // a month of quick notes: 31 days, 20 lines each
        var now = new DateTime(2026, 10, 31, 22, 0, 0);
        string? month = null;
        for (int day = 1; day <= 31; day++)
            for (int line = 0; line < 20; line++)
                month = QuickNotes.Append(month, "ideia " + line + " do dia " + day, new DateTime(2026, 10, day, 8 + line % 12, line, 0))!.Value.Content;
        Best("acrescentar uma nota rápida ao mês", 20, () => Assert.NotNull(QuickNotes.Append(month, "mais uma", now)));
    }

    [Fact]
    public void Rendering_ALongNoteStaysWithinBudget()
    {
        var paragraph = "Uma linha da nota com [[Ligação]], **negrito**, `código` e um #Tag: para o motor ter trabalho.\n\n";
        var text = "# Nota longa\n\n" + string.Concat(Enumerable.Repeat(paragraph, 50 * 1024 / paragraph.Length + 1));
        Best("renderizar uma nota de 50 KB", 100, () => Assert.Contains("<strong>", MarkdownEngine.Render(text), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Importing_FiveHundredNotesIsPlannedWithinBudget()
    {
        // UC-65: inspecting a selection of 500 notes and planning it into the 2,000-note vault (the writing is the adapter's)
        var sources = Enumerable.Range(0, 500).Select(i => new ImportSource("Importadas/Nota importada " + i + ".md",
            _ => ValueTask.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("# Importada " + i + "\n\n[[Nota " + i + "]]\n"), false)))).ToList();
        var current = VaultReader.Read(Files.Value);
        async Task<int> Run()
        {
            var inspection = await ImportPipeline.InspectAsync(new ImportSelection(sources), cancellationToken: TestContext.Current.CancellationToken);
            return ImportPipeline.Plan(inspection, current, "rev").Operations.Count;
        }
        Assert.True(await Run() >= 500);   // warm up
        double best = double.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            var watch = Stopwatch.StartNew();
            await Run();
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }
        output.WriteLine($"perf importar 500 notas: {best:0.0} ms (limite 300 ms)");
        Assert.True(best < 300, $"importar 500 notas: {best:0.0} ms");
    }
}
