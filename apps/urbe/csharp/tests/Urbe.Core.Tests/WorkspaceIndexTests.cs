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
    public void TheAppsOwnFolderIsNotAProject()
    {
        // owner's note of 25/10/2026: Personalização is the app's folder, not a project of the Área de trabalho
        var w = new WorkspaceIndex(
            [("Personalização/AGENTS.md", "AGENTS", "Regras", "2026-10-25T09:00:00Z"), ("Projeto/Nota.md", "Nota", "x", "2026-10-25T08:00:00Z")],
            ["Personalização/temas/escuro.css", "Personalização/plugins/relogio.js"],
            ["Personalização", "Personalização/estilos", "Projeto"]);
        Assert.Equal(["Projeto"], w.Projects.Select(p => p.Folder));
        Assert.Empty(w.Search("AGENTS"));
        Assert.Equal(1, w.Count);
        Assert.True(WorkspaceIndex.IsAppFolder("Personalização/temas"));
        Assert.False(WorkspaceIndex.IsAppFolder("Personalizações"));
    }

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
        Assert.Equal(w.Box("Personagens").Color, new WorkspaceIndex(Array.Empty<WorkspaceNote>()).Box("Personagens").Color);
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

    [Fact]
    public void AHeldNoteTellsWhereWhenHowLongAndHowLinked()
    {
        var store = new DocumentStore();
        var doc = store.Upsert(new DocumentInput
        {
            Id = "doc_1", Path = "Personagens/Herói.md", Content = "# Herói\n\nAlto, de uma região do norte. #ficcao",
            Created = "2026-10-20T10:00:00Z", Modified = "2026-10-22T08:30:00Z"
        });
        var text = WorkspaceIndex.Describe(doc, cites: 2, citedBy: 1, TimeZoneInfo.Utc);
        Assert.Equal("Pasta: Personagens\nCriada em 20/10/2026 10:00 · alterada em 22/10/2026 08:30\n9 palavras · cita 2 · citada por 1"
            + (doc.Tags.Count > 0 ? "\nTags: " + string.Join(" ", doc.Tags.Select(t => "#" + t.TrimStart('#'))) : ""), text);
        var loose = store.Upsert(new DocumentInput { Id = "doc_2", Path = "Comece.md", Content = "um" });
        Assert.Equal("Pasta: raiz do vault\n1 palavra · cita 0 · citada por 0", WorkspaceIndex.Describe(loose, 0, 0, TimeZoneInfo.Utc));
    }

    [Fact]
    public void ItemsAreFilteredByKindAndTagAndOrdered()
    {
        var w = new WorkspaceIndex(
            [
                new WorkspaceNote("P/Beta.md", "Beta", "b", "2026-10-21T10:00:00Z", "2026-10-01T10:00:00Z", ["#aula", "calculo"]),
                new WorkspaceNote("P/Alfa.md", "Alfa", "a", "2026-10-20T10:00:00Z", "2026-10-10T10:00:00Z", ["calculo"]),
                new WorkspaceNote("P/Gama.md", "Gama", "g", "2026-10-22T10:00:00Z", "2026-09-01T10:00:00Z", [])
            ],
            ["P/Apostila.page.json", "P/foto.png"]);
        var all = w.Items("P");
        Assert.Equal(["Gama", "Beta", "Alfa", "Apostila", "foto.png"], WorkspaceIndex.Arrange(all).Select(i => i.Title));
        Assert.Equal(["Alfa", "Apostila", "Beta", "foto.png", "Gama"], WorkspaceIndex.Arrange(all, order: WorkspaceOrder.Name).Select(i => i.Title));
        Assert.Equal(["Alfa", "Beta", "Gama"], WorkspaceIndex.Arrange(all, kind: WorkspaceItemKind.Note, order: WorkspaceOrder.Created).Select(i => i.Title));
        Assert.Equal(["Beta"], WorkspaceIndex.Arrange(all, tag: "AULA").Select(i => i.Title));
        Assert.Equal(["Apostila"], WorkspaceIndex.Arrange(all, kind: WorkspaceItemKind.Page).Select(i => i.Title));
        Assert.Equal(["calculo", "aula"], w.TagsIn("P"));
        Assert.Empty(w.TagsIn("Outra"));
    }

    [Fact]
    public void AnIndexOfTheSameVaultIsTheSameScreen()
    {
        // CST-129: closing the editor rebuilt the whole Área de trabalho even when nothing a card shows had changed
        WorkspaceIndex Of(params WorkspaceNote[] extra) => new(
            [new("Casa/Lista.md", null, "# Lista\n\n- pão\n", "2026-10-01", "2026-09-01", ["casa"]), new("Solta.md", "Solta", "Texto", null), .. extra],
            ["Casa/foto.png"], ["Casa/Vazia"]);
        static WorkspaceIndex With(WorkspaceNote[] notes, string file, string folder) => new(notes, [file], [folder]);
        var shown = Of();
        Assert.True(shown.SameAs(Of()));
        Assert.False(shown.SameAs(null));
        Assert.False(shown.SameAs(Of(new WorkspaceNote("Nova.md", null, "", null))));
        Assert.False(shown.SameAs(With([new("Casa/Lista.md", null, "# Lista\n\n- café\n- pão\n", "2026-10-01", "2026-09-01", ["casa"]), new("Solta.md", "Solta", "Texto", null)], "Casa/foto.png", "Casa/Vazia")));
        Assert.False(shown.SameAs(With([new("Casa/Lista.md", null, "# Lista\n\n- pão\n", "2026-10-02", "2026-09-01", ["casa"]), new("Solta.md", "Solta", "Texto", null)], "Casa/foto.png", "Casa/Vazia")));
        Assert.False(shown.SameAs(With([new("Casa/Lista.md", null, "# Lista\n\n- pão\n", "2026-10-01", "2026-09-01", ["mercado"]), new("Solta.md", "Solta", "Texto", null)], "Casa/foto.png", "Casa/Vazia")));
        Assert.False(shown.SameAs(With([new("Casa/Lista.md", null, "# Lista\n\n- pão\n", "2026-10-01", "2026-09-01", ["casa"]), new("Solta.md", "Outro título", "Texto", null)], "Casa/foto.png", "Casa/Vazia")));
        Assert.False(shown.SameAs(With([new("Casa/Lista.md", null, "# Lista\n\n- pão\n", "2026-10-01", "2026-09-01", ["casa"]), new("Solta.md", "Solta", "Texto", null)], "Casa/foto.png", "Casa/Outra")));
        Assert.False(shown.SameAs(With([new("Casa/Lista.md", null, "# Lista\n\n- pão\n", "2026-10-01", "2026-09-01", ["casa"]), new("Solta.md", "Solta", "Texto", null)], "Casa/foto.jpg", "Casa/Vazia")));
    }
}
