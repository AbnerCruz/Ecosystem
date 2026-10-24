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

    [Fact]
    public void FiveThousandNotesGetTheirSkyQuicklyAndApart()
    {
        // UC-72: a big vault — every star placed once, fast; no two stars on top of each other; reopening moves none
        var notes = Enumerable.Range(0, 5000).Select(i => ("id" + i, "Projeto " + (i % 40) + "/Pasta " + (i % 7) + "/Nota " + i + ".md")).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var sky = new Constellation();
        Assert.True(sky.Update(notes));
        var json = sky.Serialize();
        var again = Constellation.Parse(json, out var future);
        Assert.False(again.Update(notes));
        watch.Stop();
        Assert.False(future);
        Assert.Equal(5000, again.Stars.Count);
        Assert.True(watch.ElapsedMilliseconds < 2000, watch.ElapsedMilliseconds + " ms");
        var grid = again.Stars.GroupBy(st => ((long)Math.Floor(st.X / Constellation.Step), (long)Math.Floor(st.Y / Constellation.Step))).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var star in again.Stars)
        {
            var (cx, cy) = ((long)Math.Floor(star.X / Constellation.Step), (long)Math.Floor(star.Y / Constellation.Step));
            for (long dx = -1; dx <= 1; dx++)
                for (long dy = -1; dy <= 1; dy++)
                    if (grid.TryGetValue((cx + dx, cy + dy), out var near))
                        foreach (var o in near)
                            if (o.Id != star.Id) Assert.True(Math.Sqrt((o.X - star.X) * (o.X - star.X) + (o.Y - star.Y) * (o.Y - star.Y)) >= Constellation.MinGap - .02, "estrelas coladas: " + star.Id + " e " + o.Id);
        }
    }
}
