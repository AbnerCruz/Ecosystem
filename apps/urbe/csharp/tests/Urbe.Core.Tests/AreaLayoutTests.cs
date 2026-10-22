using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>UC-85 (DEC-0053, ADD-0030): .urbe/area.json keeps only how the boxes look and are ordered.</summary>
public sealed class AreaLayoutTests
{
    [Fact]
    public void BoxesAndOrderRoundTripAndDeclareTheirSource()
    {
        var a = new AreaLayout();
        a.Set("Matemática", new AreaLayout.Box(Color: 3, Border: "tracejada", Icon: "livro", Order: WorkspaceOrder.Name, AsList: true));
        a.Set("Personagens", new AreaLayout.Box(Border: "brilho"));
        Assert.True(a.Move("Personagens", -1, ["Matemática", "Personagens", "Romance"]));
        var json = a.Serialize();
        Assert.Contains("\"versao\": 1", json);
        Assert.Contains("única autoridade do conteúdo", json);
        var back = AreaLayout.Parse(json, out var future);
        Assert.False(future);
        Assert.Equal(new AreaLayout.Box(3, "tracejada", "livro", WorkspaceOrder.Name, true), back.Of("Matemática"));
        Assert.Equal(new AreaLayout.Box(Border: "brilho"), back.Of("Personagens"));
        Assert.Equal(["Personagens", "Matemática", "Romance", "Novo"], back.Arrange(["Matemática", "Novo", "Romance", "Personagens"], f => f));
        Assert.Equal(json, back.Serialize());
    }

    [Fact]
    public void TheColourIsTheProjectsAndDefaultsToThePalette()
    {
        var a = new AreaLayout();
        Assert.Equal(WorkspaceIndex.ColorOf("Matemática"), a.ColorOf("Matemática/curso"));
        a.Set("Matemática", new AreaLayout.Box(Color: 5));
        Assert.Equal(5, a.ColorOf("Matemática"));
        Assert.Equal(5, a.ColorOf("Matemática/curso/Módulo 1"));
        a.Set("Matemática", new AreaLayout.Box());   // empty: forgotten
        Assert.Null(a.Of("Matemática"));
        Assert.Throws<ArgumentException>(() => a.Set("X", new AreaLayout.Box(Border: "ondulada")));
        Assert.Throws<ArgumentOutOfRangeException>(() => a.Set("X", new AreaLayout.Box(Color: 99)));
    }

    [Fact]
    public void AFolderRenamedOrGoneTakesItsBoxAlong()
    {
        var a = new AreaLayout();
        a.Set("Matemática", new AreaLayout.Box(Color: 1));
        a.Set("Matemática/curso", new AreaLayout.Box(AsList: true));
        a.Move("Matemática", 1, ["Matemática", "Personagens"]);
        Assert.True(a.Renamed("Matemática", "Exatas"));
        Assert.Null(a.Of("Matemática"));
        Assert.Equal(1, a.Of("Exatas")!.Color);
        Assert.True(a.Of("Exatas/curso")!.AsList);
        Assert.Equal(["Personagens", "Exatas"], a.Arrange(["Exatas", "Personagens"], f => f));
        Assert.False(a.Renamed("Outra", "X"));
        Assert.True(a.Removed("Exatas"));
        Assert.Null(a.Of("Exatas/curso"));
    }

    [Theory]
    [InlineData("{\"versao\":2,\"caixas\":{\"A\":{\"cor\":1}}}")]
    [InlineData("não é json")]
    [InlineData("{\"caixas\":{}}")]
    public void ANewerOrUnreadableFileIsNeverWrittenOver(string json)
    {
        AreaLayout.Parse(json, out var future);
        Assert.True(future);
        Assert.False(AreaLayout.IsLayout(json));
    }

    [Fact]
    public void UnknownValuesAreIgnored()
    {
        var a = AreaLayout.Parse("{\"versao\":1,\"ordem\":[\"A\",\"A\",5],\"caixas\":{\"A\":{\"cor\":42,\"borda\":\"x\",\"icone\":\"livro\",\"ordem\":\"zz\"},\"B\":3}}", out var future);
        Assert.False(future);
        Assert.Equal(new AreaLayout.Box(Icon: "livro"), a.Of("A"));
        Assert.Null(a.Of("B"));
        Assert.Equal(["A", "C"], a.Arrange(["C", "A"], f => f));
    }

    [Fact]
    public void ABoxDraggedOntoAnotherTakesItsPlace()
    {
        var layout = new AreaLayout();
        Assert.True(layout.MoveTo("C", "A", ["A", "B", "C"]));
        Assert.Equal(["C", "A", "B"], layout.Arrange(["A", "B", "C"], x => x));
        Assert.True(layout.MoveTo("C", "B", ["C", "A", "B"]));
        Assert.Equal(["A", "B", "C"], layout.Arrange(["A", "B", "C"], x => x));
        Assert.False(layout.MoveTo("A", "A", ["A", "B", "C"]));
        Assert.False(layout.MoveTo("X", "A", ["A", "B", "C"]));
    }
}
