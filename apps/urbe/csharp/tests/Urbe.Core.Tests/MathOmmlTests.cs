using System.Xml.Linq;

namespace Urbe.Core.Tests;

/// <summary>Formulas as native Word equations (OMML), or null when a part has no faithful equivalent.</summary>
public sealed class MathOmmlTests
{
    private static readonly XNamespace M = MathOmml.M;

    private static XElement Of(string tex) => MathOmml.Convert(tex) ?? throw new Xunit.Sdk.XunitException("não converteu: " + tex);

    [Fact]
    public void FractionsRootsAndScripts()
    {
        var f = Of(@"G\frac{m_1 m_2}{r^2}");
        var frac = Assert.Single(f.Descendants(M + "f"));
        Assert.Equal(2, frac.Element(M + "num")!.Elements(M + "sSub").Count());
        Assert.Equal("2", frac.Element(M + "den")!.Descendants(M + "sup").Single().Value);
        var root = Of(@"\sqrt[3]{x}").Descendants(M + "rad").Single();
        Assert.Equal("3", root.Element(M + "deg")!.Value);
        Assert.NotNull(Of(@"\sqrt{x}").Descendants(M + "degHide").SingleOrDefault());
        Assert.Single(Of("x_i^2").Descendants(M + "sSubSup"));
        Assert.Single(Of(@"\binom{n}{k}").Descendants(M + "type"), t => (string?)t.Attribute(M + "val") == "noBar");
    }

    [Fact]
    public void BigOperatorsTakeWhatFollowsUpToTheNextSign()
    {
        var sum = Of(@"\sum_{i=1}^{n} i^2 + 1").Descendants(M + "nary").Single();
        Assert.Equal("∑", (string?)sum.Element(M + "naryPr")!.Element(M + "chr")!.Attribute(M + "val"));
        Assert.Equal("i=1", sum.Element(M + "sub")!.Value);
        Assert.Equal("n", sum.Element(M + "sup")!.Value);
        Assert.Equal("i2", sum.Element(M + "e")!.Value);   // the + 1 stays outside
        Assert.NotNull(Of(@"\int x\,dx").Descendants(M + "subHide").SingleOrDefault());
    }

    [Fact]
    public void DelimitersAccentsMatricesAndUprightNames()
    {
        var d = Of(@"\left( a+b \right)").Descendants(M + "d").Single();
        Assert.Equal("(", (string?)d.Descendants(M + "begChr").Single().Attribute(M + "val"));
        Assert.Single(Of(@"\hat{x}").Descendants(M + "acc"));
        Assert.Single(Of(@"\overline{AB}").Descendants(M + "bar"));
        var m = Of(@"\begin{pmatrix} 1 & 2 \\ 3 & 4 \end{pmatrix}");
        Assert.Equal(2, m.Descendants(M + "mr").Count());
        Assert.Equal("(", (string?)m.Descendants(M + "begChr").Single().Attribute(M + "val"));
        // a function name stands upright; a variable stays in math italic
        var sin = Of(@"\sin x").Elements(M + "r").ToList();
        Assert.Equal("p", (string?)sin[0].Descendants(M + "sty").Single().Attribute(M + "val"));
        Assert.Empty(sin[^1].Descendants(M + "sty"));
    }

    [Fact]
    public void WhatHasNoEquivalentIsNullNeverAWrongEquation()
    {
        Assert.Null(MathOmml.Convert(@"\underbrace{a+b}_{n}"));
        Assert.Null(MathOmml.Convert(@"\frac{a"));
        Assert.Null(MathOmml.Convert(""));
    }
}

/// <summary>Formulas as MathML for OpenDocument's formula objects, with the same rule.</summary>
public sealed class MathMathMlTests
{
    private static readonly XNamespace M = MathMathMl.Ns;

    [Fact]
    public void TheCommonPartsAndTheTexAlongside()
    {
        var m = MathMathMl.Convert(@"\sum_{i=1}^{n} \frac{\sqrt[3]{x_i^2}}{\sin y}", display: true)!;
        Assert.Equal("block", (string?)m.Attribute("display"));
        Assert.Single(m.Descendants(M + "munderover"));
        Assert.Single(m.Descendants(M + "mfrac"));
        Assert.Equal("3", m.Descendants(M + "mroot").Single().Elements().Last().Value);
        Assert.Single(m.Descendants(M + "msubsup"));
        Assert.Contains(m.Descendants(M + "mi"), e => e.Value == "sin" && (string?)e.Attribute("mathvariant") == "normal");
        Assert.Equal(@"\sum_{i=1}^{n} \frac{\sqrt[3]{x_i^2}}{\sin y}", m.Descendants(M + "annotation").Single().Value);
        // an integral keeps its limits beside it
        Assert.Single(MathMathMl.Convert(@"\int_0^1 x\,dx", display: false)!.Descendants(M + "msubsup"));
        Assert.Null(MathMathMl.Convert(@"\underbrace{a}_{b}", display: false));
    }
}
