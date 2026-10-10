using System.Text;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>Saved layouts from the 1.8.4-beta must be read, never silently
/// regenerated or written back. Reject invalid geometry as a whole.</summary>
public sealed class LegacySavedMapTests
{
    private static readonly (string Path, string Content)[] Notes =
    [
        ("A.md", "# A\n\n[[B]]"),
        ("B.md", "# B")
    ];

    private static LegacyCity NewCity()
    {
        int next = 0;
        var city = new LegacyCity(new LegacyCityTerrain((_, _) => LegacyBiome.Grass),
            prefix => prefix + (++next).ToString("x"));
        return city;
    }

    private static VaultSnapshot Snapshot(string map) => VaultReader.Read(
    [
        new VaultFile(".urbe/mapa.json", Encoding.UTF8.GetBytes(map)),
        .. Notes.Select(n => new VaultFile(n.Path, Encoding.UTF8.GetBytes(n.Content)))
    ]);

    [Fact]
    public void ExistingMap_RestoresBothNotePositionsAndRebuildsSemanticRoads()
    {
        var city = NewCity();
        var store = city.OpenFirstTime(Notes);
        var snapshot = Snapshot(
            """{"v":4,"mundo":"urbe","notas":{"A.md":{"x":120,"y":110},"B.md":{"x":135,"y":110}}}""");

        Assert.False(snapshot.IsMapReadOnly);
        Assert.Equal(2, city.RestoreSavedNotePositions(snapshot.Documents, store));
        Assert.Equal((120, 110), (city.Buildings.Single(b => b.Path == "A.md").X,
            city.Buildings.Single(b => b.Path == "A.md").Y));
        Assert.Equal((135, 110), (city.Buildings.Single(b => b.Path == "B.md").X,
            city.Buildings.Single(b => b.Path == "B.md").Y));
        Assert.NotEmpty(city.Roads);
        Assert.Single(city.Links);
    }

    [Fact]
    public void InvalidOrOverlappingSavedPositionsNeverPartiallyMoveTheCity()
    {
        var city = NewCity();
        var store = city.OpenFirstTime(Notes);
        var before = city.Buildings.Select(b => (b.Path, b.X, b.Y)).ToArray();
        var overlapping = Snapshot(
            """{"v":4,"notas":{"A.md":{"x":120,"y":110},"B.md":{"x":120,"y":110}}}""");
        Assert.Equal(0, city.RestoreSavedNotePositions(overlapping.Documents, store));
        Assert.Equal(before, city.Buildings.Select(b => (b.Path, b.X, b.Y)).ToArray());

        var fractional = Snapshot(
            """{"v":4,"notas":{"A.md":{"x":120.5,"y":110},"B.md":{"x":135,"y":110}}}""");
        Assert.Equal(0, city.RestoreSavedNotePositions(fractional.Documents, store));
        Assert.Equal(before, city.Buildings.Select(b => (b.Path, b.X, b.Y)).ToArray());
    }

    [Fact]
    public void OriginalMapCamera_RoundTripsOnlyValidV4Coordinates()
    {
        var current = Snapshot(
            """{"v":4,"camera":{"x":1120.25,"y":880.5,"z":1.15}}""");
        Assert.True(LegacySavedMapCamera.TryRead(current, out var camera));
        Assert.Equal((1120.25, 880.5, 1.15), camera);

        var future = Snapshot(
            """{"v":99,"camera":{"x":10,"y":12,"z":1}}""");
        Assert.True(future.IsMapReadOnly);
        Assert.False(LegacySavedMapCamera.TryRead(future, out _));

        var invalid = Snapshot(
            """{"v":4,"camera":{"x":10,"y":12,"z":100}}""");
        Assert.False(LegacySavedMapCamera.TryRead(invalid, out _));
    }
}
