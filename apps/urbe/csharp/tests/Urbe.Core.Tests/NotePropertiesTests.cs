using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>The frontmatter of a note read and written by the properties form without disturbing what it does not know.</summary>
public sealed class NotePropertiesTests
{
    private const string Note = "---\ntitle: \"Plano: fase 1\"\naliases:\n  - Plano\n  - \"Fase 1\"\ntags: [ideia, \"projeto x\"]\ndone: false\nprioridade: 3\nprazo: 2026-11-02\nautor: Ana\nresumo: |\n  duas\n  linhas\n---\n\n# Corpo\n\ntexto\n";

    [Fact]
    public void ReadsEveryKindAndKeepsTheBody()
    {
        var p = NoteProperties.Parse(Note);
        Assert.Equal(["Plano", "Fase 1"], p.Aliases);
        Assert.Equal(["ideia", "projeto x"], p.Tags);
        Assert.Equal(("Plano: fase 1", PropertyKind.Text), (p.Get("title")!.Value, p.Get("title")!.Kind));
        Assert.Equal(PropertyKind.Check, p.Get("done")!.Kind);
        Assert.Equal(PropertyKind.Number, p.Get("prioridade")!.Kind);
        Assert.Equal(PropertyKind.Date, p.Get("prazo")!.Kind);
        Assert.Equal(PropertyKind.Raw, p.Get("resumo")!.Kind);
    }

    [Fact]
    public void AnUntouchedNoteIsWrittenBackIdentically() => Assert.Equal(Note, NoteProperties.Parse(Note).Render());

    [Fact]
    public void ChangingOnePropertyLeavesTheOthersAsTheyWere()
    {
        var after = NoteProperties.Parse(Note).SetList("aliases", ["Plano", "Fase 1", "P1"]).Set("done", PropertyKind.Check, "true").Render();
        Assert.Equal(Note.Replace("  - \"Fase 1\"\n", "  - Fase 1\n  - P1\n").Replace("done: false", "done: true"), after.Replace("  - \"Fase 1\"\n", "  - Fase 1\n"));
        Assert.Contains("resumo: |\n  duas\n  linhas\n", after);
        Assert.EndsWith("---\n\n# Corpo\n\ntexto\n", after);
    }

    [Fact]
    public void ANoteWithoutPropertiesGetsThemOnlyWhenOneIsAdded()
    {
        var plain = "# Só texto\n";
        Assert.Equal(plain, NoteProperties.Parse(plain).Render());
        Assert.Equal("---\naliases:\n  - Ideia: o começo\n---\n# Só texto\n".Replace("Ideia: o começo", "\"Ideia: o começo\""), NoteProperties.Parse(plain).SetList("aliases", ["Ideia: o começo"]).Render());
        Assert.Equal(plain, NoteProperties.Parse(plain).SetList("tags", ["a"]).Remove("tags").Render());
    }

    [Fact]
    public void ValuesThatWouldBeReadBackWrongAreQuoted()
    {
        var p = NoteProperties.Parse("").Set("a", PropertyKind.Text, "true").Set("b", PropertyKind.Text, "2026-01-01").Set("c", PropertyKind.Text, "x: y").Set("d", PropertyKind.Text, "simples");
        var read = NoteProperties.Parse(p.Render());
        Assert.All(["a", "b", "c", "d"], k => Assert.Equal(PropertyKind.Text, read.Get(k)!.Kind));
        Assert.Equal("x: y", read.Get("c")!.Value);
        Assert.Contains("d: simples\n", p.Render());
    }

    [Fact]
    public void TagsAndAliasesOfAnyNote()
    {
        Assert.Equal(["a", "b"], NoteProperties.AliasesOf("---\naliases: a, b\n---\n"));
        Assert.Equal(["a"], NoteProperties.AliasesOf("---\naliases: [a]\n---\n"));
        Assert.Empty(NoteProperties.AliasesOf("# nada"));
        Assert.Equal("minha-tag", NoteProperties.NormalizeTag("  #minha tag "));
    }
}

/// <summary>A link written with an alias of the properties reaches the note.</summary>
public sealed class AliasLinkTests
{
    [Fact]
    public void AnAliasResolvesTheLinkAndCountsAsABacklink()
    {
        var store = new DocumentStore();
        var alvo = store.Upsert(new DocumentInput { Id = "a", Path = "Alvo.md", Content = "---\naliases:\n  - Apelido\n  - Outro nome\n---\n# Alvo\n" });
        var origem = store.Upsert(new DocumentInput { Id = "o", Path = "Origem.md", Content = "Veja [[apelido]] e [[Outro nome]].\n" });
        using var index = new KnowledgeIndex(store);
        Assert.Equal(["o"], index.Backlinks(alvo.Id).Select(d => d.Id));
        Assert.Equal(["a"], index.Links(origem.Id).Select(d => d.Id));
    }
}
