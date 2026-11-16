using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>CST-048: conditions, calculations, repeatable groups and messages, from initial values to error and correction.</summary>
public sealed class FormRulesTests
{
    private static FormSession Session(params FormField[] fields) => FormSession.Create(new FormDefinition { Fields = [.. fields] });

    [Fact]
    public void AHiddenFieldIsNotAddedToACalculation()
    {
        var s = Session(
            new FormField { Id = "frete", Label = "Frete", Type = "toggle", Initial = false },
            new FormField { Id = "valor", Label = "Valor", Type = "number", Initial = 10 },
            new FormField { Id = "custo", Label = "Custo do frete", Type = "number", Initial = 5, VisibleWhen = "frete", EqualsValue = true },
            new FormField { Id = "total", Label = "Total", Type = "calculated", Sum = ["valor", "custo"] });
        Assert.Equal(10d, (double)s.Answers()["total"]!);
        s.Values["frete"] = true;
        Assert.Equal(15d, (double)s.Answers()["total"]!);
        Assert.False(s.Answers().ContainsKey("custo") && !s.Visible(s.Definition.Fields[2]));
    }

    [Fact]
    public void ARepeatableGroupSaysWhichItemAndWhatToFix()
    {
        var s = Session(new FormField
        {
            Id = "autores", Label = "Autores", Type = "repeat", Min = 2,
            Fields = [new FormField { Id = "nome", Label = "Nome", Required = true }, new FormField { Id = "ano", Label = "Ano", Type = "integer" }]
        });
        s.Values["autores"] = new JsonArray(new JsonObject { ["nome"] = "Ana" });
        Assert.Equal("Adicione pelo menos 2 itens.", s.Validate()["autores"]);
        s.Values["autores"] = new JsonArray(new JsonObject { ["nome"] = "Ana" }, new JsonObject { ["nome"] = "", ["ano"] = 1.5 });
        Assert.Equal("Item 2 — Nome: Preencha este campo.", s.Validate()["autores"]);
        ((JsonObject)((JsonArray)s.Values["autores"]!)[1]!)["nome"] = "Bia";
        Assert.Equal("Item 2 — Ano: Use um número inteiro.", s.Validate()["autores"]);
        ((JsonObject)((JsonArray)s.Values["autores"]!)[1]!)["ano"] = 1999;
        Assert.Empty(s.Validate());
        var most = Session(new FormField { Id = "r", Label = "R", Type = "repeat", Max = 1, Fields = [new FormField { Id = "a", Label = "A" }] });
        most.Values["r"] = new JsonArray(new JsonObject { ["a"] = "1" }, new JsonObject { ["a"] = "2" });
        Assert.Equal("Use no máximo 1 item.", most.Validate()["r"]);
    }

    [Theory]
    [InlineData("date", "31/12/2026", "Use uma data como 2026-12-31.")]
    [InlineData("time", "25:00", "Use uma hora como 14:30.")]
    [InlineData("color", "azul", "Use uma cor como #3366cc.")]
    [InlineData("select", "talvez", "Escolha uma das opções.")]
    [InlineData("note", "Inexistente", "Escolha uma nota que existe no vault.")]
    [InlineData("folder", "Nada", "Escolha uma pasta que existe no vault.")]
    public void EachKindOfFieldSaysWhatToWrite(string type, string value, string message)
    {
        var s = Session(new FormField { Id = "f", Label = "F", Type = type, Options = ["sim", "não"] });
        s.Values["f"] = value;
        Assert.Equal(message, s.Validate(["Nota"], ["Pasta"])["f"]);
    }

    [Fact]
    public void TextLengthLimitsAreSaidInCharacters()
    {
        var s = Session(new FormField { Id = "f", Label = "Resumo", Min = 10, Max = 20 });
        s.Values["f"] = "curto";
        Assert.Equal("Use pelo menos 10 caracteres.", s.Validate()["f"]);
        s.Values["f"] = new string('a', 25);
        Assert.Equal("Use no máximo 20 caracteres.", s.Validate()["f"]);
        s.Values["f"] = "do tamanho certo";
        Assert.Empty(s.Validate());
    }

    public static TheoryData<string, JsonNode?, JsonNode?> Kinds => new()
    {
        { "text", null, "Ana" },
        { "long_text", null, "Um\nb" },
        { "integer", 1.5, 3 },
        { "number", "abc", 2.5 },
        { "date", "31/12/2026", "2026-12-31" },
        { "time", "25:00", "14:30" },
        { "select", "talvez", "sim" },
        { "multi_select", new JsonArray("talvez"), new JsonArray("sim", "não") },
        { "scale", 9, 3 },
        { "list", null, new JsonArray("a", "b") },
        { "note", "Inexistente", "Nota" },
        { "folder", "Nada", "Pasta" },
        { "color", "azul", "#3366cc" },
    };

    /// <summary>Every kind of field: required and empty is an error, a wrong value is an error, the corrected value clears it.</summary>
    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKindGoesFromEmptyAndWrongToCorrected(string type, JsonNode? wrong, JsonNode? right)
    {
        var s = Session(new FormField { Id = "f", Label = "F", Type = type, Required = true, Options = ["sim", "não"], Min = 1, Max = 5 });
        Assert.Equal("Preencha este campo.", s.Validate(["Nota"], ["Pasta"])["f"]);
        if (wrong is not null)
        {
            s.Values["f"] = wrong.DeepClone();
            Assert.True(s.Validate(["Nota"], ["Pasta"]).ContainsKey("f"), type + " aceitou " + wrong.ToJsonString());
        }
        s.Values["f"] = right!.DeepClone();
        Assert.Empty(s.Validate(["Nota"], ["Pasta"]));
    }
}
