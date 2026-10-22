namespace Urbe.Core.Tests;

/// <summary>UC-72: the Constelação's positions, stable and derived from the notes.</summary>
public sealed class ConstellationTests
{
    private static readonly (string, string)[] Notes =
    [
        ("a", "Matemática/Aula.md"), ("b", "Matemática/curso/Plano.md"), ("c", "Personagens/Herói.md"), ("d", "Comece.md")
    ];

    [Fact]
    public void EachProjectIsARegionAndItsNotesSitAroundIt()
    {
        var sky = new Constellation();
        Assert.True(sky.Update(Notes));
        Assert.Equal(["", "Matemática", "Personagens"], sky.Regions.Select(r => r.Project).Order(StringComparer.Ordinal));
        var math = sky.Regions.Single(r => r.Project == "Matemática");
        Assert.Equal(2, math.Seats);
        foreach (var id in new[] { "a", "b" })
        {
            var (x, y) = sky.Of(id)!.Value;
            Assert.True(Math.Sqrt((x - math.X) * (x - math.X) + (y - math.Y) * (y - math.Y)) < Constellation.Step * 3);
        }
        // the regions are apart
        var people = sky.Regions.Single(r => r.Project == "Personagens");
        Assert.True(Math.Sqrt((math.X - people.X) * (math.X - people.X) + (math.Y - people.Y) * (math.Y - people.Y)) > Constellation.RegionStep * .9);
    }

    [Fact]
    public void ANewNoteTakesASeatAndNobodyElseMoves()
    {
        var sky = new Constellation();
        sky.Update(Notes);
        var before = sky.Stars.ToDictionary(s => s.Id, s => (s.X, s.Y));
        Assert.False(sky.Update(Notes));   // the same notes: nothing changes
        Assert.True(sky.Update([.. Notes.Where(n => n.Item1 != "c"), ("e", "Matemática/Nova.md"), ("f", "Receitas/Bolo.md")]));
        foreach (var id in new[] { "a", "b", "d" }) Assert.Equal(before[id], (sky.Of(id)!.Value.X, sky.Of(id)!.Value.Y));
        Assert.Null(sky.Of("c"));
        Assert.Equal(3, sky.Regions.Single(r => r.Project == "Matemática").Seats);
        Assert.NotNull(sky.Of("f"));
        // a moved note keeps its place too
        Assert.False(sky.Update([("a", "Personagens/Aula.md"), ("b", "Matemática/curso/Plano.md"), ("d", "Comece.md"), ("e", "Matemática/Nova.md"), ("f", "Receitas/Bolo.md")]));
    }

    [Fact]
    public void TheFileSaysItsSourceAndReadsBack()
    {
        var sky = new Constellation();
        sky.Update(Notes);
        var json = sky.Serialize();
        Assert.Contains("única autoridade", json);
        var back = Constellation.Parse(json, out var future);
        Assert.False(future);
        Assert.Equal(sky.Stars.OrderBy(s => s.Id), back.Stars.OrderBy(s => s.Id));
        Assert.Equal(sky.Regions.OrderBy(r => r.Project), back.Regions.OrderBy(r => r.Project));
        Assert.False(back.Update(Notes));
        // a newer or unreadable file is never written over
        Constellation.Parse("{\"versao\": 9}", out future);
        Assert.True(future);
        Assert.False(Constellation.IsConstellation("{ não é json"));
        Assert.True(Constellation.IsConstellation(json));
    }
}
