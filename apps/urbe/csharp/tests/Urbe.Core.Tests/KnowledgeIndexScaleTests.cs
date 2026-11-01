using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Indexes shared terms without quadratic duplicate checks, preserving Search semantics.</summary>
public sealed class KnowledgeIndexScaleTests
{
    [Fact]
    public void SharedTermAcrossManyNotesKeepsDeterministicOrderAndUpdates()
    {
        var store = new DocumentStore();
        var ids = new List<string>();
        for (int i = 0; i < 1200; i++)
        {
            var doc = store.Upsert(new DocumentInput
            {
                Path = "N/" + i.ToString("0000") + ".md",
                Content = "PalavraComum PalavraComum PalavraComum [[Outra]]."
            });
            ids.Add(doc.Id);
        }
        using var index = new KnowledgeIndex(store);
        var first = index.Search("palavracomum", limit:2000);
        Assert.Equal(ids, first.Select(d => d.Id));

        var changed = store.Get(ids[100])!;
        store.Upsert(new DocumentInput { Id = changed.Id, Path = changed.Path, Content = "OutroTermo" });
        var second = index.Search("palavracomum", limit:2000);
        Assert.Equal(1199, second.Count);
        Assert.DoesNotContain(second, d => d.Id == changed.Id);
        Assert.Equal(changed.Id, Assert.Single(index.Search("outrotermo")).Id);
    }
}
