using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>DEC-0045 (UC-41): .urbe/mundo.json, apart from mapa.json (which the 1.8.4 rebuilds with only its own fields).</summary>
public sealed class WorldPrefsTests
{
    [Fact]
    public void DefaultsAreWallsAndSoftOutlines()
    {
        var p = WorldPrefs.Parse(null, out var newer);
        Assert.False(newer);
        Assert.True(p.Walls);
        Assert.Equal(WorldPrefs.Soft, p.Outlines);
    }

    [Fact]
    public void TextRoundTrips()
    {
        var p = new WorldPrefs(false, WorldPrefs.Hidden);
        Assert.Equal("{\n  \"versao\": 1,\n  \"muralhas\": false,\n  \"contornos\": \"oculto\"\n}\n", p.Text().Replace("\r\n", "\n"));
        Assert.Equal(p, WorldPrefs.Parse(p.Text(), out _));
    }

    [Theory]
    [InlineData("{ quebrado")]
    [InlineData("[]")]
    [InlineData("{\"versao\":1,\"contornos\":\"neon\",\"muralhas\":\"sim\"}")]
    public void BadContentFallsBackToDefaults(string text) => Assert.Equal(WorldPrefs.Default, WorldPrefs.Parse(text, out _));

    [Fact]
    public void ANewerFileIsRecognised()
    {
        Assert.Equal(WorldPrefs.Default, WorldPrefs.Parse("{\"versao\":2,\"muralhas\":false}", out var newer));
        Assert.True(newer);
    }
}

public sealed class WorldDecorationsTests
{
    [Fact]
    public void EverySpriteFillsWholeTiles()
    {
        foreach (var k in WorldDecorations.Kinds)
            foreach (var frame in k.Sprite.Frames)
            {
                Assert.Equal(k.H * WorldDecorations.CellsPerTile, frame.Length);
                Assert.All(frame, row => Assert.Equal(k.W * WorldDecorations.CellsPerTile, row.Length));
                Assert.All(frame.SelectMany(r => r).Where(c => c != '.'), c => Assert.True(k.Sprite.Palette.ContainsKey(c), k.Id + ": " + c));
            }
    }

    [Fact]
    public void DecorationsRoundTripAndAreFoundByAnyOfTheirTiles()
    {
        var p = WorldPrefs.Default with { Decorations = [new("d1", "poco", 4, 7), new("d2", "roca", 10, 2)] };
        Assert.Contains("\"decoracoes\"", p.Text());
        var back = WorldPrefs.Parse(p.Text(), out _);
        Assert.Equal(p, back);
        Assert.Equal("d2", back.DecorationAt(11, 2)!.Id);   // the field is two tiles wide
        Assert.Null(back.DecorationAt(12, 2));
        Assert.Equal("d3", back.NextDecorationId());
        Assert.NotEqual(p, WorldPrefs.Default);
    }

    [Fact]
    public void UnknownKindsAndRepeatedIdsAreLeftOut()
    {
        var p = WorldPrefs.Parse("{\"versao\":1,\"decoracoes\":[{\"id\":\"a\",\"tipo\":\"disco-voador\",\"x\":1,\"y\":1},{\"id\":\"b\",\"tipo\":\"banco\",\"x\":2,\"y\":2},{\"id\":\"b\",\"tipo\":\"poco\",\"x\":3,\"y\":3}]}", out _);
        var only = Assert.Single(p.Decorations);
        Assert.Equal(new WorldDecoration("b", "banco", 2, 2), only);
    }
}

public sealed class BuildingLookTests
{
    [Fact]
    public void LooksRoundTripByPathAndAutomaticOnesAreNotKept()
    {
        var p = WorldPrefs.Default
            .WithLook("Projetos/Ideias.md", new BuildingLook("tower", 2, true))
            .WithLook("Casa.md", new BuildingLook(Flowers: false))
            .WithLook("Nada.md", new BuildingLook());
        Assert.Equal(2, p.Looks.Count);
        var text = p.Text();
        Assert.Contains("\"Projetos/Ideias.md\"", text);
        Assert.Contains("\"tipo\": \"tower\"", text);
        Assert.DoesNotContain("\"variante\": null", text);
        var back = WorldPrefs.Parse(text, out _);
        Assert.Equal(p, back);
        Assert.Equal(new BuildingLook("tower", 2, true), back.Looks["Projetos/Ideias.md"]);
        Assert.Equal(new BuildingLook(null, null, false), back.Looks["Casa.md"]);
        Assert.Empty(back.WithLook("Casa.md", new BuildingLook()).WithLook("Projetos/Ideias.md", new BuildingLook()).Looks);
    }

    [Fact]
    public void LooksFollowMovedFilesAndBadEntriesAreLeftOut()
    {
        var p = WorldPrefs.Default.WithLook("A.md", new BuildingLook("hall"));
        Assert.Null(p.WithMoved(new Dictionary<string, string> { ["B.md"] = "C.md" }));
        var moved = p.WithMoved(new Dictionary<string, string> { ["A.md"] = "Pasta/A.md" })!;
        Assert.Equal(["Pasta/A.md"], moved.Looks.Keys);

        var bad = WorldPrefs.Parse("{\"versao\":1,\"construcoes\":{\"X.md\":{\"tipo\":\"castelo\",\"variante\":9},\"Y.md\":{\"tipo\":\"store\",\"variante\":-1,\"flores\":\"sim\"}}}", out _);
        Assert.Equal(["Y.md"], bad.Looks.Keys);
        Assert.Equal(new BuildingLook("store"), bad.Looks["Y.md"]);
    }
}
