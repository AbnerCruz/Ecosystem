using Urbe.Core.Calculus;

namespace Urbe.Core.Tests;

/// <summary>CST-206: the calculator gives exact answers where it can, says “≈” where it cannot, and errs in words.</summary>
public sealed class CalculatorTests
{
    private static string R(string line) { var r = Calculator.Evaluate(line); Assert.True(r.Ok, line + " → " + r.Error); return r.Result!; }

    [Theory]
    [InlineData("2 + 3*4", "14")]
    [InlineData("(2 + 3)*4", "20")]
    [InlineData("2^3^2", "512")]
    [InlineData("-2^2", "-4")]
    [InlineData("1,5 + 1,25", "2,75")]
    [InlineData("0.1 + 0.2", "0,3")]
    [InlineData("10/4", "2,5")]
    [InlineData("√16 + 3²", "13")]
    [InlineData("2π", "6,28318530718")]
    [InlineData("sen(pi/6)", "0,5")]
    [InlineData("cosd(60)", "0,5")]
    [InlineData("ln(e^3)", "3")]
    [InlineData("log(1000)", "3")]
    [InlineData("log(8; 2)", "3")]
    [InlineData("5!", "120")]
    [InlineData("comb(5; 2)", "10")]
    [InlineData("20% * 150", "30")]
    [InlineData("mdc(12; 18)", "6")]
    [InlineData("mmc(4; 6)", "12")]
    [InlineData("media(2; 4; 9)", "5")]
    [InlineData("mediana([5; 1; 3; 2])", "2,5")]
    [InlineData("desvio([2; 4; 4; 4; 5; 5; 7; 9])", "2,1380899353")]
    [InlineData("det([[1; 2]; [3; 4]])", "-2")]
    [InlineData("inv([[4; 7]; [2; 6]])", "[[0,6; -0,7]; [-0,2; 0,4]]")]
    [InlineData("[[1; 2]; [3; 4]] * [[5; 6]; [7; 8]]", "[[19; 22]; [43; 50]]")]
    [InlineData("[1; 2; 3] * 2", "[2; 4; 6]")]
    [InlineData("raiz(-8; 3)", "-2")]
    [InlineData("|-3| + 1", "4")]
    public void Values(string line, string expected) => Assert.Equal(expected, R(line));

    [Theory]
    [InlineData("100 km/h em m/s", "27,7777777778 m/s")]
    [InlineData("37 °C em °F", "98,6 °F")]
    [InlineData("0 °C em K", "273,15 K")]
    [InlineData("1 mi em km", "1,609344 km")]
    [InlineData("2 h + 30 min em min", "150 min")]
    [InlineData("5 kg * 9,8 m/s² em N", "49 N")]
    [InlineData("3 kWh em J", "10800000 J")]
    [InlineData("1 L em mL", "1000 mL")]
    public void Units(string line, string expected) => Assert.Equal(expected, R(line));

    [Fact]
    public void Units_RefuseMixingDimensions()
    {
        Assert.Contains("dimensões diferentes", Calculator.Evaluate("3 km + 2 h").Error);
        Assert.Contains("dimensões diferentes", Calculator.Evaluate("5 kg em m").Error);
    }

    [Theory]
    [InlineData("derivada de x^3 + 2x", "3x^2 + 2")]
    [InlineData("derivada de sen(x)", "cos(x)")]
    [InlineData("derivada de e^(2x)", "")]
    [InlineData("derivada de ln(x)", "1/x")]
    [InlineData("d/dx (x+1)^2", "2x + 2")]
    [InlineData("expandir (x+1)^2", "x^2 + 2x + 1")]
    [InlineData("simplificar (x+1)^2", "(x + 1)^2")]
    [InlineData("simplificar x + x + 3x", "5x")]
    [InlineData("integral de 3x^2", "x^3 + C")]
    [InlineData("integral de 6x^2 - 4x + 1", "2x^3 - 2x^2 + x + C")]
    public void Symbolic(string line, string expected)
    {
        var r = R(line);
        if (expected.Length > 0) Assert.Equal(expected, r);
    }

    [Fact]
    public void Derivative_OfExponential_EvaluatesRight()
    {
        var d = MathSymbolic.Derivative(MathParser.Parse("e^(2x)"), "x");
        Assert.Equal(2 * Math.Exp(2), MathEvaluator.Number(d, "x", 1), 9);
        var chain = MathSymbolic.Derivative(MathParser.Parse("sen(x^2)"), "x");
        Assert.Equal(2 * 1.3 * Math.Cos(1.69), MathEvaluator.Number(chain, "x", 1.3), 9);
    }

    [Theory]
    [InlineData("resolver 2x + 3 = 11", "x = 4")]
    [InlineData("resolver x^2 - 5x + 6 = 0", "x = 2; x = 3")]
    [InlineData("resolver x^2 = 2", "x = -1,41421356237; x = 1,41421356237  (x = ±√2)")]
    [InlineData("resolver x^2 + 1 = 0", "Sem solução real (Δ = -4 < 0).")]
    [InlineData("x^2 - 4 = 0", "x = -2; x = 2")]
    [InlineData("resolver 2^t = 8 para t", "t = 3")]
    public void Equations(string line, string expected) => Assert.Equal(expected, R(line));

    [Fact]
    public void Equations_Transcendental_AreApproximateAndSaySo()
    {
        var r = Calculator.Evaluate("resolver cos(x) = x");
        Assert.True(r.Ok);
        Assert.True(r.Approximate);
        Assert.Equal("x = ≈ 0,739085133215", r.Result);
    }

    [Fact]
    public void Integrals_Definite_ExactOrApproximateWithError()
    {
        Assert.Equal("2", R("integral de sen(x) de 0 até pi"));
        Assert.Equal("0,333333333333", R("integral de x^2 de 0 até 1"));
        var gauss = Calculator.Evaluate("integral de e^(-x^2) de 0 até 1");
        Assert.True(gauss.Approximate);
        Assert.StartsWith("≈ 0,746824132812", gauss.Result);
        // 1/x across 0 is not continuous: the primitive is not trusted, the numeric value fails in words
        Assert.False(Calculator.Evaluate("integral de 1/x de -1 até 1").Ok);
        Assert.Contains("erro estimado <", gauss.Result);
    }

    [Fact]
    public void Block_DefinitionsFlowDown_ErrorsStayOnTheirLine()
    {
        var lines = Calculator.Run("// custos\npreco = 12,5\nqtd = 4\ntotal = preco * qtd\nf(x) = x^2 + 1\nf(3) + total\nderivada de f\nzz + 1\nresolver f(x) = 10");
        Assert.Null(lines[0].Result);
        Assert.Equal("total = 50", lines[3].Result);
        Assert.Equal("f(x) = x^2 + 1", lines[4].Result);
        Assert.Equal("60", lines[5].Result);
        Assert.Equal("2x", lines[6].Result);
        Assert.Equal("zz + 1", lines[7].Result); // a free name is algebra, not an error
        Assert.Equal("x = -3; x = 3", lines[8].Result);
    }

    [Theory]
    [InlineData("1/0", "Divisão por zero.")]
    [InlineData("ln(-1)", "ln recebe números maiores que zero.")]
    [InlineData("raiz(-4)", "Raiz quadrada de número negativo não é real.")]
    [InlineData("2 +", "A expressão terminou antes do esperado.")]
    [InlineData("(2 + 3", "Falta fechar um parêntese.")]
    [InlineData("det([[1; 2; 3]; [4; 5; 6]])", "A operação precisa de uma matriz quadrada.")]
    public void Errors_AreWords(string line, string message) => Assert.Equal(message, Calculator.Evaluate(line).Error);
}

/// <summary>CST-206: the calculator in publications and for the agents.</summary>
public sealed class CalculatorIntegrationTests
{
    [Fact]
    public void AgentTool_AnswersEachLine_MarksApproximateAndErrors()
    {
        var answer = Urbe.Core.Assistant.AssistantCalcTools.Answer("a = 3\n2a + 1\nresolver cos(x) = x\nln(-1)");
        Assert.Contains("2a + 1  →  7", answer);
        Assert.Contains("[aproximado]", answer);
        Assert.Contains("ERRO: ln recebe números maiores que zero.", answer);
    }

    [Fact]
    public async Task Publications_ShowTheCalculationTable_ComputedFromTheNote()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Conta.md", Content = "# Conta\n\n```calculo\npreco = 12,5\npreco * 4\n```\n" });
        using var catalog = new PublicationSourceCatalog(store);
        var project = PublicationProject.Create("Conta", note.Id);
        var html = PublicationHtml.Export(project, catalog).Html;
        Assert.Contains("<table class=\"urbe-calc\">", html);
        Assert.Contains("<td>50</td>", html);
        var docx = await PublicationDocx.ExportAsync(project, catalog, cancellationToken: TestContext.Current.CancellationToken);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(docx.Bytes));
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("<w:tbl>", xml);
        Assert.Contains(">50<", xml);
        // the note keeps only its lines
        Assert.DoesNotContain("50", store.Get(note.Id)!.Content.Replace("12,5", ""));
    }
}
