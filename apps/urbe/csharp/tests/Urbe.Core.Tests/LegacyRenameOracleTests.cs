using System.Text.Json;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// "Renomear" of a note house must leave the vault exactly as the 1.8.4-beta leaves it. Fixture recorded
/// from the unmodified 1.8.4-beta in Chromium (csharp/tools/legacy-rename-oracle.mjs): the editor's
/// "Mais ações" → "Renomear" with a typed name, then every file of the vault after the app's save
/// (urbeReconcileWorld moves the document, workspace.js writes the new path, removes the old one and
/// rewrites .urbe/mapa.json). Four renames in one session: a plain one, a name taken in the same
/// neighbourhood (" (2)"), a new extension and a change of case only.
/// </summary>
public sealed class LegacyRenameOracleTests
{
    private static readonly Lazy<LegacyTileChunks> World = new(() =>
    {
        var climate = LegacyElevationClimateField.Generate("urbe", 512);
        var hydrology = LegacyHydrologyField.Generate(climate);
        return new LegacyTileChunks(new LegacyTileSampler(hydrology, LegacyWorldSpawn.Choose(hydrology)));
    });

    [Fact]
    public void EveryRenameLeavesTheFilesAndTheMapOfTheOriginal()
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "legacy-city", "rename-note-run-a.json")));
        var rootEl = json.RootElement;
        var open = rootEl.GetProperty("open");
        var files = rootEl.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString()!)).ToList();

        // id('r') per region, id('b') per text file in listing order (as LegacyCityMapOracleTests)
        var regionIds = new Queue<string>(open.GetProperty("regions").EnumerateArray().Select(r => r.GetProperty("id").GetString()!));
        var noteIds = open.GetProperty("buildings").EnumerateArray().ToDictionary(b => b.GetProperty("path").GetString()!, b => b.GetProperty("id").GetString()!);
        var buildingIds = new Queue<string>(files.Select(f => noteIds[f.Item1]));
        var documentIds = open.GetProperty("documents").EnumerateArray().ToDictionary(d => d.GetProperty("path").GetString()!, d => d.GetProperty("id").GetString()!);
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), p => p == "r" ? regionIds.Dequeue() : buildingIds.Dequeue());
        var store = city.Open(files, null, documentIds, open.GetProperty("now").GetInt64());
        var writer = new LegacyCityMap(_ => throw new InvalidOperationException("every id comes from the open"));
        Assert.Equal(open.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, open));

        var disk = files.ToDictionary(f => f.Item1, f => f.Item2, StringComparer.Ordinal);
        foreach (var step in rootEl.GetProperty("steps").EnumerateObject().Select(p => p.Value))
        {
            var from = step.GetProperty("from").GetString()!;
            var doc = store.Get(from)!;
            var house = city.Buildings.Single(b => b.IsNote && b.DocumentId == doc.Id);
            var plan = city.PlanRename(house, step.GetProperty("typed").GetString()!)!.Value;
            var expected = step.GetProperty("buildings").EnumerateArray().Single(b => b.GetProperty("id").GetString() == house.Id);
            Assert.Equal(expected.GetProperty("name").GetString(), plan.Name);
            Assert.Equal(expected.GetProperty("ext").GetString(), plan.Ext);
            Assert.Equal(expected.GetProperty("path").GetString(), plan.Path);

            // the file moves (what workspace.js flush does: write the new path, remove the old one), then the house
            var content = disk[from];
            disk.Remove(from);
            disk[plan.Path] = content;
            long now = step.GetProperty("now").GetInt64();
            city.ApplyRename(house, plan, now);
            var moved = LegacyCity.MoveDocument(store, doc, plan.Path);
            Assert.Equal(doc.Id, moved.Id);
            Assert.Equal(doc.Title, moved.Title);   // the 1.8.4 keeps the document's title ({...doc})

            Assert.Equal(step.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, step));
            Assert.Equal(step.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString())),
                disk.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, (string?)p.Value)));
        }
    }

    /// <summary>
    /// "Mover para…" from the Explorer (held row → v21MoveEntry → moverEntradaPara), recorded from the 1.8.4
    /// (csharp/tools/legacy-move-oracle.mjs): to another neighbourhood (the file moves), to the root (the house
    /// moves, the file stays in its folder) and from the root into a neighbourhood. The lot, the files and the
    /// text of .urbe/mapa.json after each save are the original's.
    /// </summary>
    [Fact]
    public void EveryMoveLeavesTheLotTheFilesAndTheMapOfTheOriginal()
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "legacy-city", "move-note-run-a.json")));
        var (city, store, writer, disk) = OpenFixture(json.RootElement);
        foreach (var step in json.RootElement.GetProperty("steps").EnumerateObject().Select(p => p.Value))
        {
            var from = step.GetProperty("from").GetString()!;
            var doc = store.Get(from)!;
            var house = city.Buildings.Single(b => b.IsNote && b.DocumentId == doc.Id);
            var expected = step.GetProperty("buildings").EnumerateArray().Single(b => b.GetProperty("id").GetString() == house.Id);
            var regionId = expected.GetProperty("regionId").ValueKind == JsonValueKind.Null ? null : expected.GetProperty("regionId").GetString();
            var destination = regionId is null ? null : city.Regions.Single(r => r.Id == regionId);
            var camera = step.GetProperty("camera");
            city.CameraX = camera.GetProperty("x").GetDouble();
            city.CameraY = camera.GetProperty("y").GetDouble();
            city.CameraZoom = camera.GetProperty("z").GetDouble();
            long now = step.GetProperty("now").GetInt64();

            var plan = city.PlanMove(house, destination, now, doc.Path)!.Value;
            Assert.Equal((expected.GetProperty("x").GetInt32(), expected.GetProperty("y").GetInt32()), (plan.X, plan.Y));
            Assert.Equal(expected.GetProperty("path").GetString(), plan.Path);
            if (plan.Path != from)
            {
                var content = disk[from];
                disk.Remove(from);
                disk[plan.Path] = content;
            }
            city.ApplyMove(house, destination, plan, now);
            if (plan.Path != doc.Path) LegacyCity.MoveDocument(store, doc, plan.Path);

            Assert.Equal(step.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, step));
            Assert.Equal(step.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString())),
                disk.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, (string?)p.Value)));
        }
    }

    /// <summary>The vault of a fixture opened like the original (ids fed back) and its first map checked.</summary>
    private static (LegacyCity City, DocumentStore Store, LegacyCityMap Writer, Dictionary<string, string> Disk) OpenFixture(JsonElement rootEl)
    {
        var open = rootEl.GetProperty("open");
        var files = rootEl.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString()!)).ToList();
        var regionIds = new Queue<string>(open.GetProperty("regions").EnumerateArray().Select(r => r.GetProperty("id").GetString()!));
        var noteIds = open.GetProperty("buildings").EnumerateArray().ToDictionary(b => b.GetProperty("path").GetString()!, b => b.GetProperty("id").GetString()!);
        var buildingIds = new Queue<string>(files.Select(f => noteIds[f.Item1]));
        var documentIds = open.GetProperty("documents").EnumerateArray().ToDictionary(d => d.GetProperty("path").GetString()!, d => d.GetProperty("id").GetString()!);
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), p => p == "r" ? regionIds.Dequeue() : buildingIds.Dequeue());
        var store = city.Open(files, null, documentIds, open.GetProperty("now").GetInt64());
        var writer = new LegacyCityMap(_ => throw new InvalidOperationException("every id comes from the open"));
        Assert.Equal(open.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, open));
        return (city, store, writer, files.ToDictionary(f => f.Item1, f => f.Item2, StringComparer.Ordinal));
    }

    [Fact]
    public void ARenameOfAFileBuildingIsNotPlanned()
    {
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), p => p + Guid.NewGuid().ToString("N")[..6]);
        city.Open([("A.md", "# A\n")], """{"v":4,"mundo":"placas-1","notas":{"A.md":{"id":"doc_a","x":40,"y":30}},"construcoes":[{"id":"ast-1","tipo":"arquivo","fileClass":"image","name":"foto.png","caminho":"","x":50,"y":30}]}""",
            new Dictionary<string, string> { ["A.md"] = "doc_a" });
        var file = city.Buildings.Single(b => !b.IsNote);
        Assert.Null(city.PlanRename(file, "outra"));
        var note = city.Buildings.Single(b => b.IsNote);
        Assert.Equal("Nota.md", city.PlanRename(note, "")!.Value.Path);   // v21UniqueName: '' → 'Nota.md'
        Assert.Equal("A.md", city.PlanRename(note, "A")!.Value.Path);     // its own name is not taken
    }

    private static string Produce(LegacyCityMap writer, LegacyCity city, DocumentStore store, JsonElement state)
    {
        var camera = state.GetProperty("camera");
        var text = writer.Produce(city, store, camera.GetProperty("x").GetDouble(), camera.GetProperty("y").GetDouble(),
            camera.GetProperty("z").GetDouble(), state.GetProperty("now").GetInt64(), LegacyCityMap.ReferenceVersion, out var refusal);
        Assert.Null(refusal);
        return text!;
    }

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
