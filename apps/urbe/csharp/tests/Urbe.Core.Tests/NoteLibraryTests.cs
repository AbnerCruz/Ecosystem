using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// The notes without the city (ADD-0028): ids, dates and folders recorded in .urbe/notas.json, taken once from the 1.8.4
/// map of a vault opened before, which is only read.
/// </summary>
public sealed class NoteLibraryTests
{
    private const string Map184 = """
        {"v":4,"mundo":"placas-1","camera":{"x":1,"y":2,"z":1},
         "regioes":[{"id":"reg_1","caminho":"Projetos","nome":"Projetos","x":1,"y":1,"w":9,"h":9},
                    {"id":"reg_2","caminho":"Projetos/Vazia","nome":"Vazia","x":2,"y":2,"w":3,"h":3},
                    {"id":"reg_3","caminho":"../fora","nome":"fora"}],
         "notas":{"Projetos/Plano.md":{"id":"doc_plano","x":3,"y":4,"tags":[],"criado":"2025-01-02","modificado":"2025-03-04"},
                  "Ideias.md":{"id":"doc_ideias","criado":"2024-12-31"},
                  "Sumiu.md":{"id":"doc_sumiu"}},
         "construcoes":[]}
        """;

    private static (string, string, string)[] Notes(params (string Path, string Content, string Id)[] notes) => notes;

    [Fact]
    public void AVaultOpenedBeforeKeepsTheIdsDatesAndFoldersOfItsMapWithoutUsingTheCity()
    {
        var ids = NoteLibrary.Ids(["Projetos/Plano.md", "Ideias.md", "Nova.md"], null, Map184);
        Assert.Equal("doc_plano", ids["Projetos/Plano.md"]);
        Assert.Equal("doc_ideias", ids["Ideias.md"]);
        Assert.Null(ids["Nova.md"]);

        var library = NoteLibrary.Open(Notes(("Projetos/Plano.md", "# Plano #tarefa", "doc_plano"), ("Ideias.md", "x", "doc_ideias"), ("Nova.md", "", "doc_nova")),
            null, Map184, ["Anexos/foto.png"]);
        var plano = library.Store.Get("Projetos/Plano.md")!;
        Assert.Equal("doc_plano", plano.Id);
        Assert.Equal(("2025-01-02", "2025-03-04"), (plano.Created, plano.Modified));
        // the tags come from the note's text, not from the map's houses
        Assert.Equal(["tarefa"], plano.Tags);
        // the empty folder of a neighbourhood, the folders of the files; never a path out of the vault
        Assert.Equal(["Anexos", "Projetos", "Projetos/Vazia"], library.Folders);
    }

    [Fact]
    public void TheRecordWinsOverTheMapOnceItExists()
    {
        var record = """{"versao":1,"notas":{"Ideias.md":{"id":"doc_novo","criado":"2026-10-22","modificado":"2026-10-22"}},"pastas":["Arquivo"]}""";
        // the map would give doc_ideias; after a rename recorded in notas.json the map is no longer read at all
        var ids = NoteLibrary.Ids(["Ideias.md", "Projetos/Plano.md"], record, Map184);
        Assert.Equal("doc_novo", ids["Ideias.md"]);
        Assert.Null(ids["Projetos/Plano.md"]);
        var library = NoteLibrary.Open(Notes(("Ideias.md", "", "doc_novo")), record, Map184);
        Assert.Equal(["Arquivo"], library.Folders);
        Assert.Equal("2026-10-22", library.Store.Get("Ideias.md")!.Created);
    }

    [Fact]
    public void TheRecordIsStableAndReadBackTheSame()
    {
        var library = NoteLibrary.Open(Notes(("b/B.md", "", "doc_b"), ("A.md", "", "doc_a")), null, null);
        library.AddFolder("Vazia/Sub");
        var json = library.Serialize();
        Assert.Equal(json, NoteLibrary.Open(Notes(("A.md", "", "doc_a"), ("b/B.md", "", "doc_b")), json, null).Serialize());
        Assert.True(NoteLibrary.IsRecord(json));
        Assert.Contains("\"pastas\": [\n  \"Vazia\",\n  \"Vazia/Sub\",\n  \"b\"\n ]", json);
        var ids = NoteLibrary.Ids(["A.md", "b/B.md"], json, Map184);
        Assert.Equal(("doc_a", "doc_b"), (ids["A.md"], ids["b/B.md"]));
    }

    [Fact]
    public void ARecordOfANewerVersionIsKeptAndNeverTakenAsThisOne()
    {
        var newer = """{"versao":2,"notas":{},"pastas":[]}""";
        Assert.True(NoteLibrary.IsFuture(newer));
        Assert.False(NoteLibrary.IsRecord(newer));
        Assert.False(NoteLibrary.IsFuture("não é json"));
        // the ids of a vault with a newer record come from its map, read only
        Assert.Equal("doc_ideias", NoteLibrary.Ids(["Ideias.md"], newer, Map184)["Ideias.md"]);
    }

    [Fact]
    public void ADuplicatedIdOfTheMapIsGivenOnlyOnce()
    {
        var map = """{"notas":{"A.md":{"id":"doc_x"},"B.md":{"id":"doc_x"}}}""";
        var ids = NoteLibrary.Ids(["A.md", "B.md"], null, map);
        Assert.Equal("doc_x", ids["A.md"]);
        Assert.Null(ids["B.md"]);
    }

    [Fact]
    public void NewNotesAreUniqueInTheirFolderAndStartWithAHeading()
    {
        var library = NoteLibrary.Open(Notes(("Pasta/Ideias.md", "", "a"), ("Ideias.md", "", "b")), null, null);
        Assert.Equal(("Pasta/Ideias (2).md", "# "), library.PlanNewNote("Pasta", "Ideias"));
        Assert.Equal(("Pasta/dados.json", ""), library.PlanNewNote("Pasta", "dados.json"));
        Assert.Equal(("Outra/Ideias.md", "# "), library.PlanNewNote("Outra", "Ideias"));
        var doc = library.AddNote("Outra/Ideias.md", "# ", Date(2026, 10, 22));
        Assert.Equal(("2026-10-22", "2026-10-22"), (doc.Created, doc.Modified));
        Assert.Contains("Outra", library.Folders);
    }

    [Fact]
    public void ARenameStaysInTheFolderKeepsTheTypeAndIgnoresTheNoteItself()
    {
        var library = NoteLibrary.Open(Notes(("P/Plano.txt", "", "a"), ("P/Outro.md", "", "b")), null, null);
        Assert.Equal(("Plano", ".txt", "P/Plano.txt"), library.PlanRename("P/Plano.txt", "Plano.txt"));
        // v21UniqueName: a name typed without an extension becomes .md, as in the 1.8.4
        Assert.Equal(("Novo", ".md", "P/Novo.md"), library.PlanRename("P/Plano.txt", "Novo"));
        Assert.Equal(("Novo", ".txt", "P/Novo.txt"), library.PlanRename("P/Plano.txt", "Novo.txt"));
        Assert.Equal(("Outro (2)", ".md", "P/Outro (2).md"), library.PlanRename("P/Plano.txt", "Outro.md"));
        Assert.Equal("Plano.txt", NoteLibrary.FullNoteName("P/Plano.txt"));
    }

    [Fact]
    public void FoldersMoveAndLeaveWithEverythingUnderThem()
    {
        var library = NoteLibrary.Open(Notes(("A/x.md", "", "x")), null, null);
        library.AddFolder("A/B/C");
        library.MoveFolder("A/B", "Z/B");
        Assert.Equal(["A", "Z", "Z/B", "Z/B/C"], library.Folders);
        library.RemoveFolder("Z");
        Assert.Equal(["A"], library.Folders);
    }

    [Fact]
    public void ThePropertiesAreTheNotesOwn()
    {
        var library = NoteLibrary.Open(Notes(("Projetos/Plano.md", "# Plano #tarefa", "doc_plano")), null, Map184);
        Assert.Equal("---\ntítulo: Plano\ncategoria: Projetos\ntags: [tarefa]\ncriado: 2025-01-02\nmodificado: 2025-03-04\n---",
            library.PropertiesText("Projetos/Plano.md", Date(2026, 10, 22)));
        library.Edited("Projetos/Plano.md", "# Plano", Date(2026, 10, 22));
        Assert.EndsWith("modificado: 2026-10-22\n---", library.PropertiesText("Projetos/Plano.md", Date(2026, 10, 22)));
        Assert.Null(library.PropertiesText("Nada.md", 0));
    }

    [Fact]
    public void ARestoredNoteComesBackWithItsIdAndDates()
    {
        var library = NoteLibrary.Open(Notes(), null, null);
        var gone = new DocumentStore().Upsert(new DocumentInput { Id = "doc_v", Path = "Volta/Nota.md", Content = "x", Created = "2025-01-01" });
        var back = library.Restore(gone, Date(2026, 10, 22));
        Assert.Equal(("doc_v", "2025-01-01", "2026-10-22"), (back.Id, back.Created, back.Modified));
        Assert.Equal(["Volta"], library.Folders);
    }

    private static long Date(int y, int m, int d) => new DateTimeOffset(y, m, d, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
}
