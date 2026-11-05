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
}
