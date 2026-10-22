using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-84 (DEC-0053): the Área de trabalho built from the vault's folders and files.</summary>
public sealed class WorkspaceIndexTests
{
    private static WorkspaceIndex Vault() => new(
        [
            ("Matemática/curso de calculo/Matriz curricular.md", "Matriz curricular", "# Matriz curricular\n\nCinco módulos de **cálculo** com [[Plano de Aulas|plano]].\n", "2026-10-20T10:00:00Z"),
            ("Matemática/curso de calculo/Plano de Aulas.md", "Plano de Aulas", "---\ntags: [aula]\n---\n# Plano\n\n- [ ] Aula 01 — variáveis\n", "2026-10-22T08:00:00Z"),
            ("Matemática/Ideias.md", null, "texto", "2026-10-21T09:00:00Z"),
            ("Personagens/Herói.md", "Herói", "# Herói\n\nAlto, de uma região do norte.", "2026-10-19T09:00:00Z"),
            ("Comece.md", "Comece", "# Comece\n\nBem-vindo.", "2026-10-01T09:00:00Z")
        ],
        ["Matemática/curso de calculo/Apostila - Módulo 1.page.json", "Personagens/retrato.png", "Livro.pdf"],
        ["Vazio", "Matemática/curso de calculo/Módulo 1"]);

    [Fact]
    public void TopFoldersAreTheProjectsAndTheRootIsLoose()
    {
        var w = Vault();
        Assert.Equal(["Matemática", "Personagens", "Vazio"], w.Projects.Select(p => p.Folder));
        var math = w.Projects[0];
        Assert.Equal(("Matemática", 4, "2026-10-22T08:00:00Z"), (math.Name, math.Count, math.Modified));
        Assert.Equal(0, w.Projects[2].Count);
        Assert.Equal(["Comece.md", "Livro.pdf"], w.Loose.Select(i => i.Path).Order(StringComparer.Ordinal));
        Assert.Equal(8, w.Count);
    }

    [Fact]
    public void ABoxHoldsItsSubfoldersAndItsOwnItemsMostRecentFirst()
    {
        var w = Vault();
        Assert.Equal(["Matemática/curso de calculo"], w.Children("Matemática"));
        Assert.Equal(["Matemática/curso de calculo/Módulo 1"], w.Children("Matemática/curso de calculo"));
        Assert.Equal(["Plano de Aulas", "Matriz curricular", "Apostila - Módulo 1"], w.Items("Matemática/curso de calculo").Select(i => i.Title));
        Assert.Equal(WorkspaceItemKind.Page, w.Items("Matemática/curso de calculo")[2].Kind);
        Assert.Equal("Ideias", Assert.Single(w.Items("Matemática")).Title);   // no title: the file's name
        // the glass shows the most recent items of the whole project
        Assert.Equal(["Plano de Aulas", "Ideias"], w.Recent("Matemática", 2).Select(i => i.Title));
    }

    [Fact]
    public void ColoursAreStableAndShareTheProjects()
    {
        var w = Vault();
        Assert.Equal(WorkspaceIndex.ColorOf("Matemática"), WorkspaceIndex.ColorOf("Matemática/curso de calculo"));
        Assert.InRange(w.Box("Personagens").Color, 0, WorkspaceIndex.Palette - 1);
        Assert.Equal(w.Box("Personagens").Color, new WorkspaceIndex([]).Box("Personagens").Color);
        Assert.Equal("Matemática", WorkspaceIndex.ProjectOf("Matemática/curso de calculo/Plano de Aulas.md"));
        Assert.Equal("", WorkspaceIndex.ProjectOf("Comece.md"));
    }

    [Fact]
    public void SearchFindsByEveryWordIgnoringAccentsInABoxOrEverywhere()
    {
        var w = Vault();
        Assert.Equal(["Plano de Aulas"], w.Search("aulas PLANO").Select(i => i.Title));
        Assert.Equal(["Herói"], w.Search("heroi").Select(i => i.Title));
        Assert.Equal(["Apostila - Módulo 1"], w.Search("modulo", "Matemática").Select(i => i.Title));
        Assert.Empty(w.Search("heroi", "Matemática"));
        Assert.Empty(w.Search("   "));
    }

    [Fact]
    public void TheSnippetIsTheFirstTextWithoutItsMarks()
    {
        Assert.Equal("Cinco módulos de cálculo com plano.", WorkspaceIndex.Snippet("# Matriz curricular\n\nCinco módulos de **cálculo** com [[Plano de Aulas|plano]].\n"));
        Assert.Equal("Aula 01 — variáveis", WorkspaceIndex.Snippet("---\ntags: [aula]\n---\n# Plano\n\n- [ ] Aula 01 — variáveis\n"));
        Assert.Equal("Texto da IA", WorkspaceIndex.Snippet("Texto da IA {ia:\"modelo\"}"));
        Assert.Equal("", WorkspaceIndex.Snippet("# Só título\n"));
        Assert.EndsWith("…", WorkspaceIndex.Snippet(new string('a', 300)));
    }
}
