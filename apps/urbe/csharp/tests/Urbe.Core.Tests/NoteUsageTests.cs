using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-226: everything that depends on a note, found from the vault as it is (nothing stored).</summary>
public sealed class NoteUsageTests
{
    [Fact]
    public void Finds_PublicationsDataProgressEmbedsAndLinks()
    {
        var store = new DocumentStore();
        var livros = store.Upsert(new DocumentInput { Path = "Estudo/Livros.md", Content = "# Livros\n\n## Lidos\n\n| t | p |\n|---|---|\n| A | 1 |\n\n- [ ] ler B\n" });
        var a = PublicationProject.Create("Leituras", livros.Id);
        var b = PublicationProject.Create("Só lidos").AddSource(new PublicationSourceReference(livros.Id, "Lidos"));
        store.Upsert(new DocumentInput { Path = "Publicações/Leituras.publication.json", Content = PublicationProjectCodec.Serialize(a) });
        store.Upsert(new DocumentInput { Path = "Publicações/Só lidos.publication.json", Content = PublicationProjectCodec.Serialize(b) });
        store.Upsert(new DocumentInput { Path = "Relatório.md", Content = "```dados\nfonte: Estudo/Livros.md#Lidos\nresumo\n```\n\n```progress\nsource: tasks\nnote: " + livros.Id + "\n```\n" });
        store.Upsert(new DocumentInput { Path = "Diário.md", Content = "Hoje li ![[Livros]] e [[Estudo/Livros|minha lista]] e [[Livros#Lidos]]." });
        store.Upsert(new DocumentInput { Path = "Outra.md", Content = "[[Livros de receitas]] não é a mesma." });
        var usage = NoteUsage.Of(store, livros);
        Assert.Equal(["publicação", "publicação", "dados", "progresso", "incorporação", "ligação"], usage.Select(u => u.Kind));
        Assert.Contains(usage, u => u.Detail == "Só lidos (trecho “Lidos”)");
        Assert.Contains(usage, u => u.Kind == "ligação" && u.Detail == "2 ligações");
        Assert.DoesNotContain(usage, u => u.Path == "Outra.md");
        var summary = NoteUsage.Summary(usage)!;
        Assert.StartsWith("Usada em 2 publicações, 1 bloco de dados, 1 barra de progresso, 1 incorporação", summary);
        Assert.Null(NoteUsage.Summary(NoteUsage.Of(store, store.Get("Outra.md")!)));
    }

    [Fact]
    public void ManyNotesAtOnceGiveWhatEachGivesAlone()
    {
        // deleting many notes asks once for all of them (one pass over the vault), with the same answer per note
        var store = new DocumentStore();
        var a = store.Upsert(new DocumentInput { Path = "P/A.md", Content = "# A\n\n[[B]] e ![[C]]" });
        var b = store.Upsert(new DocumentInput { Path = "B.md", Content = "# B\n\n![[P/A]] [[A]] [[C]]" });
        var c = store.Upsert(new DocumentInput { Path = "C.md", Content = "```progress\nsource: tasks\nnote: " + a.Id + "\n```\n```dados\nfonte: B.md\n```\n" });
        for (int i = 0; i < 50; i++) store.Upsert(new DocumentInput { Path = $"N{i}.md", Content = "[[A]] [[C]]" });
        var many = NoteUsage.OfMany(store, [a, b, c]);
        foreach (var n in new[] { a, b, c })
            Assert.Equal(NoteUsageSingle(store, n), many[n.Id].Select(d => (d.Kind, d.Path, d.Detail)));
        Assert.Contains(many[a.Id], d => d.Kind == "progresso" && d.Path == "C.md");
        Assert.Contains(many[b.Id], d => d.Kind == "dados" && d.Path == "C.md");
        Assert.Contains(many[c.Id], d => d.Kind == "incorporação" && d.Path == "P/A.md");
        Assert.DoesNotContain(many[a.Id], d => d.Path == "P/A.md");   // a note never depends on itself

        static IEnumerable<(string, string, string)> NoteUsageSingle(DocumentStore s, UrbeDocument n) => NoteUsage.Of(s, n).Select(d => (d.Kind, d.Path, d.Detail));
    }
}
