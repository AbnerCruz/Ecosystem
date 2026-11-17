using System.Xml.Linq;
using Urbe.Core.Study;

namespace Urbe.Core.Tests;

public sealed class InfographicTests
{
    private const string Note = "# A água no Brasil\n\nO país tem 12% da água doce do mundo.\n\n## Rios\n\nO [[Amazonas]] é o maior rio. Ele corre por 6.992 km.\n\n## Uso\n\nA irrigação usa 50% da água. O resto vai para cidades.\n\n## Vazia\n";

    [Fact]
    public void ReadsTitleFiguresAndSections()
    {
        var (title, figures, sections) = Infographic.Read(Note, "fallback");
        Assert.Equal("A água no Brasil", title);
        Assert.Equal(["12%", "6.992 km", "50%"], figures.Select(f => f.Value));
        Assert.Equal("da água doce do mundo", figures[0].Label);
        Assert.Equal(["Rios", "Uso", "Vazia"], sections.Select(s => s.Title));
        Assert.Equal("O Amazonas é o maior rio.", sections[0].Summary);
        Assert.Equal("", sections[2].Summary);
    }

    [Fact]
    public void TheSvgIsValidAndEscapesText()
    {
        var svg = Infographic.Svg("# A & B <c>\n\n## Um\n\nTexto.", "x");
        var root = XDocument.Parse(svg).Root!;
        Assert.Equal("svg", root.Name.LocalName);
        Assert.Contains(root.Descendants(), e => e.Name.LocalName == "text" && e.Value == "A & B <c>");
        Assert.Contains(root.Descendants(), e => e.Name.LocalName == "text" && e.Value == "Texto.");
    }
}
