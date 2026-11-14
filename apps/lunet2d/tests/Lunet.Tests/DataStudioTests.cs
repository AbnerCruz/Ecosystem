using Lunet.Compiler;
using Lunet.Studio.Data;

namespace Lunet.Tests;

/// <summary>LUNET-505: game data as typed tables — infer, edit, validate, CSV and a C# class the game loads.</summary>
public sealed class DataStudioTests
{
    private const string Enemies = """
        [
          { "id": "slime", "nome": "Slime", "vida": 10, "velocidade": 1.5, "voa": false, "tipo": "terra" },
          { "id": "morcego", "nome": "Morcego", "vida": 6, "velocidade": 3, "voa": true, "tipo": "ar" }
        ]
        """;

    [Fact]
    public void Parse_InfersTypesAndTheKey()
    {
        var table = DataTable.Parse(Enemies, className: "Inimigo");
        Assert.Equal(DataColumnType.Text, table.Column("nome").Type);
        Assert.Equal(DataColumnType.Integer, table.Column("vida").Type);
        Assert.Equal(DataColumnType.Number, table.Column("velocidade").Type);
        Assert.Equal(DataColumnType.Boolean, table.Column("voa").Type);
        Assert.True(table.Column("id").Key);
        Assert.Equal("sim", table.Display(1, "voa"));
        Assert.Throws<InvalidDataException>(() => DataTable.Parse("[1,2]"));
        Assert.Throws<InvalidDataException>(() => DataTable.Parse("{ quebrado"));
    }

    [Fact]
    public void Set_ParsesByTypeAndReportsMistakes()
    {
        var table = DataTable.Parse(Enemies);
        Assert.Null(table.Set(0, "vida", "25"));
        Assert.Equal("25", table.Display(0, "vida"));
        Assert.NotNull(table.Set(0, "vida", "muito"));
        Assert.Null(table.Set(0, "velocidade", "2,5"));
        Assert.Equal("2.5", table.Display(0, "velocidade"));
        Assert.Null(table.Set(0, "voa", "sim"));
        Assert.NotNull(table.Set(0, "voa", "talvez"));
    }

    [Fact]
    public void Schema_AddsChoicesRangesAndReferences_AndValidates()
    {
        var table = DataTable.Parse(Enemies);
        table.ReplaceColumn("tipo", table.Column("tipo") with { Type = DataColumnType.Choice, Choices = ["terra", "ar", "agua"] });
        table.ReplaceColumn("vida", table.Column("vida") with { Min = 1, Max = 100, Required = true });
        table.AddColumn(new DataColumn { Name = "drop", Type = DataColumnType.Reference, ReferenceTable = "itens" });
        Assert.NotNull(table.Set(0, "tipo", "fogo"));
        table.Rows[1]["vida"] = 500;
        table.Rows[0]["drop"] = "espada";
        table.Rows[1]["drop"] = "nada";
        var issues = table.Validate(new Dictionary<string, HashSet<string>> { ["itens"] = ["espada", "pocao"] });
        Assert.Contains(issues, i => i.Row == 1 && i.Column == "vida");
        Assert.Contains(issues, i => i.Row == 1 && i.Column == "drop");
        Assert.DoesNotContain(issues, i => i.Row == 0 && i.Column == "drop");

        var again = DataTable.Parse(table.ToJson(), table.SchemaToJson());
        Assert.Equal(["terra", "ar", "agua"], again.Column("tipo").Choices);
        Assert.Equal(100, again.Column("vida").Max);
    }

    [Fact]
    public void Rows_AddDuplicateMoveSortAndKeepKeysUnique()
    {
        var table = DataTable.Parse(Enemies);
        int added = table.AddRow();
        Assert.Equal("novo-3", table.Display(added, "id"));
        int copy = table.DuplicateRow(0);
        Assert.Equal("Slime", table.Display(copy, "nome"));
        Assert.NotEqual(table.Display(0, "id"), table.Display(copy, "id"));
        Assert.DoesNotContain(table.Validate(), i => i.Message.Contains("repetido"));
        table.Rows[copy]["id"] = "slime";
        Assert.Contains(table.Validate(), i => i.Message.Contains("repetido"));
        table.SortBy("vida", descending: true);
        Assert.Equal("Slime", table.Display(0, "nome"));
        table.MoveRow(0, 3);
        Assert.Equal("Slime", table.Display(3, "nome"));
    }

    [Fact]
    public void Columns_RenameMovesTheValues_AndUnknownDataIsKept()
    {
        var table = DataTable.Parse(Enemies);
        table.ReplaceColumn("vida", table.Column("vida") with { Name = "hp" });
        Assert.Equal("10", table.Display(0, "hp"));
        Assert.DoesNotContain("\"vida\"", table.ToJson());
        var partial = DataTable.Parse("""[{"a":1,"extra":{"x":2}}]""", """{"Version":1,"Columns":[{"Name":"a","Type":"Integer"}]}""");
        Assert.Contains(partial.Columns, c => c.Name == "extra" && c.Type == DataColumnType.Json);
        Assert.Contains("\"x\": 2", partial.ToJson());
        table.RemoveColumn("hp");
        Assert.DoesNotContain("hp", table.ToJson());
    }

    [Fact]
    public void Csv_RoundTripsQuotesAndReportsBadCells()
    {
        var table = DataTable.Parse(Enemies);
        table.Set(0, "nome", "Slime, o \"grudento\"");
        var csv = table.ToCsv();
        var other = DataTable.Parse(Enemies);
        Assert.Empty(other.ImportCsv(csv));
        Assert.Equal("Slime, o \"grudento\"", other.Display(0, "nome"));
        var issues = other.ImportCsv("id,vida\nx,abc\n");
        Assert.Single(issues);
    }

    [Fact]
    public void CSharpClass_CompilesAndLoadsTheJsonInAGame()
    {
        var table = DataTable.Parse(Enemies, className: "Inimigo");
        var code = table.ToCSharp();
        Assert.Contains("public sealed class Inimigo", code);
        Assert.Contains("[JsonPropertyName(\"vida\")]", code);
        var game = """
            using System.Collections.Generic; using Lunet;
            public sealed class G : Game
            {
                public List<Inimigo> Inimigos = new();
                protected override void LoadContent() => Inimigos = Content.LoadJson<List<Inimigo>>("Data/inimigos.json");
            }
            """;
        var result = new GameCompiler(new LoadedAssembliesReferenceProvider(typeof(Lunet.Game).Assembly)).Compile("data_game",
            [new SourceFile("Data/Inimigo.cs", code), new SourceFile("Game.cs", game)]);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        // and the generated shape really deserializes the data
        var type = System.Reflection.Assembly.Load(result.Assembly!).GetType("Inimigo")!;
        var list = System.Text.Json.JsonSerializer.Deserialize(table.ToJson(), typeof(List<>).MakeGenericType(type)) as System.Collections.IList;
        Assert.Equal(2, list!.Count);
        Assert.Equal(10L, type.GetProperty("Vida")!.GetValue(list[0]));
    }
}
