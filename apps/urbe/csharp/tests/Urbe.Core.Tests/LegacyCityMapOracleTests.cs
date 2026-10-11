using System.Text.Json;
using System.Text.Json.Nodes;
using Urbe.Core;

namespace Urbe.Core.Tests;

/// <summary>
/// The .urbe/mapa.json the native city writes must be the bytes the 1.8.4-beta writes. Fixtures are
/// recorded from the unmodified 1.8.4-beta in Chromium (csharp/tools/legacy-mapa-oracle.mjs): the
/// text of .urbe/mapa.json read back from the app's own IndexedDB storage after its save
/// (workspace.js flush), for a vault opened without a map ("fresh") and a session on a vault with a
/// map (open, an edit that keeps "salvo", a camera-only move that saves nothing, an edit that adds a
/// tag, and a "Nova nota" created through the + button). The ids, clock and camera the original used
/// are fed back.
/// </summary>
public sealed class LegacyCityMapOracleTests
{
    private static readonly Lazy<LegacyTileChunks> World = new(() =>
    {
        var climate = LegacyElevationClimateField.Generate("urbe", 512);
        var hydrology = LegacyHydrologyField.Generate(climate);
        return new LegacyTileChunks(new LegacyTileSampler(hydrology, LegacyWorldSpawn.Choose(hydrology)));
    });

    private sealed class Ids
    {
        public readonly Queue<string> Regions = new();
        public readonly Queue<string> Buildings = new();
        public string Next(string prefix) => prefix == "r" ? Regions.Dequeue() : Buildings.Dequeue();
    }

    /// <summary>id('r') per region in world.regions order; id('b') per text file in listing order, then file buildings.</summary>
    private static (LegacyCity City, DocumentStore Store, Ids Ids) Open(JsonElement phase, JsonElement state, string? mapa)
    {
        var files = phase.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString()!)).ToList();
        var ids = new Ids();
        foreach (var r in state.GetProperty("regions").EnumerateArray()) ids.Regions.Enqueue(r.GetProperty("id").GetString()!);
        var buildings = state.GetProperty("buildings").EnumerateArray().ToList();
        var noteIds = buildings.Where(b => b.GetProperty("tipo").GetString() == "nota")
            .ToDictionary(b => b.GetProperty("path").GetString()!, b => b.GetProperty("id").GetString()!);
        foreach (var f in files) ids.Buildings.Enqueue(noteIds[f.Item1]);
        foreach (var b in buildings.Where(b => b.GetProperty("tipo").GetString() != "nota")) ids.Buildings.Enqueue(b.GetProperty("id").GetString()!);
        var documentIds = state.GetProperty("documents").EnumerateArray()
            .ToDictionary(d => d.GetProperty("path").GetString()!, d => d.GetProperty("id").GetString()!);

        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), ids.Next);
        var store = city.Open(files, mapa, documentIds, state.GetProperty("now").GetInt64());
        Assert.Empty(ids.Regions);
        Assert.Empty(ids.Buildings);
        return (city, store, ids);
    }

    private static string Produce(LegacyCityMap writer, LegacyCity city, DocumentStore store, JsonElement state)
    {
        var camera = state.GetProperty("camera");
        var text = writer.Produce(city, store, camera.GetProperty("x").GetDouble(), camera.GetProperty("y").GetDouble(),
            camera.GetProperty("z").GetDouble(), state.GetProperty("now").GetInt64(), LegacyCityMap.ReferenceVersion, out var refusal);
        Assert.Null(refusal);
        return text!;
    }

    /// <summary>document.update {...d, content} (DocumentStore.upsert: tags/links reparsed, dates kept).</summary>
    private static void Edit(DocumentStore store, string path, string content)
    {
        var d = store.Get(path)!;
        store.Upsert(new DocumentInput { Id = d.Id, Path = d.Path, Title = d.Title, Content = content, Created = d.Created, Modified = d.Modified });
    }

    private static string Content(JsonElement state, string path) =>
        state.GetProperty("documents").EnumerateArray().First(d => d.GetProperty("path").GetString() == path).GetProperty("content").GetString()!;

    [Theory]
    [InlineData("save-mapa-run-a.json")]
    [InlineData("save-mapa-run-b.json")]
    public void TheFirstSaveOfAVaultWithoutMapIsTheOriginalText(string fixture)
    {
        using var json = Fixture(fixture);
        var fresh = json.RootElement.GetProperty("fresh");
        var (city, store, _) = Open(fresh, fresh, null);
        var writer = new LegacyCityMap(_ => throw new InvalidOperationException("every id comes from the open"));
        Assert.Equal(fresh.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, fresh));
    }

    [Theory]
    [InlineData("save-mapa-run-a.json")]
    [InlineData("save-mapa-run-b.json")]
    public void ASessionWritesTheOriginalTextAtEverySave(string fixture)
    {
        using var json = Fixture(fixture);
        var session = json.RootElement.GetProperty("session");
        var steps = session.GetProperty("steps");
        var open = steps.GetProperty("open");
        var (city, store, ids) = Open(session, open, session.GetProperty("mapaJson").GetString());
        var writer = new LegacyCityMap(_ => throw new InvalidOperationException("every id comes from the open"));

        // 1. after the open
        Assert.Equal(open.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, open));

        // 2. later, a note edited without changing anything of the map: same text, "salvo" kept
        var edit = steps.GetProperty("edit");
        Edit(store, "Área/Leituras.md", Content(edit, "Área/Leituras.md"));
        Assert.Equal(edit.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, edit));
        Assert.Equal(open.GetProperty("mapaWritten").GetString(), edit.GetProperty("mapaWritten").GetString());

        // 3. camera only: the 1.8.4 schedules no save, the file stays as it was
        var pan = steps.GetProperty("pan");
        Assert.Equal(edit.GetProperty("mapaWritten").GetString(), pan.GetProperty("mapaWritten").GetString());
        Assert.NotEqual(edit.GetProperty("camera").GetProperty("x").GetDouble(), pan.GetProperty("camera").GetProperty("x").GetDouble());

        // 4. an edit that adds tags: the next save carries the new camera, the tags and a new "salvo"
        var tag = steps.GetProperty("tag");
        Edit(store, "Comece.md", Content(tag, "Comece.md"));
        Assert.Equal(tag.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, tag));

        // 5. "Nova nota" (+ → v21CreateFileInRegion(null)): the house, then the document urbeReconcileWorld creates
        var create = steps.GetProperty("create");
        var typed = create.GetProperty("typed").GetString()!;
        var made = create.GetProperty("buildings").EnumerateArray().Last(b => b.GetProperty("tipo").GetString() == "nota");
        ids.Buildings.Enqueue(made.GetProperty("id").GetString()!);
        var nowMs = create.GetProperty("now").GetInt64();
        // one camera in app.js: the loose lot is searched where the camera looks now (centroBuscaRaiz)
        city.CameraX = tag.GetProperty("camera").GetProperty("x").GetDouble();
        city.CameraY = tag.GetProperty("camera").GetProperty("y").GetDouble();
        city.CameraZoom = tag.GetProperty("camera").GetProperty("z").GetDouble();
        var house = city.CreateNote(null, typed, nowMs)!.Value;
        Assert.Equal(made.GetProperty("path").GetString(), house.House.Path);
        var doc = create.GetProperty("documents").EnumerateArray().First(d => d.GetProperty("path").GetString() == house.House.Path);
        var today = LegacyCity.NowDate(nowMs);
        store.Upsert(new DocumentInput { Id = doc.GetProperty("id").GetString(), Path = house.House.Path, Content = house.Content, Created = today, Modified = today });
        Assert.Equal(doc.GetProperty("content").GetString(), house.Content);
        Assert.Equal(create.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, create));
    }

    [Fact]
    public void AHouseWhoseFileTheOriginalWouldMoveIsRefusedNotWritten()
    {
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), p => p + Guid.NewGuid().ToString("N")[..6]);
        // "Nota.MD": urbeBuildingPath gives "Nota.md", so the 1.8.4 would rename the file on its save
        var store = city.Open([("Nota.MD", "# Nota\n")], """{"v":4,"mundo":"placas-1","notas":{"Nota.MD":{"id":"doc_n","x":40,"y":30}}}""",
            new Dictionary<string, string> { ["Nota.MD"] = "doc_n" });
        var text = new LegacyCityMap().Produce(city, store, 0, 0, 1, 0, LegacyCityMap.ReferenceVersion, out var refusal);
        Assert.Null(text);
        Assert.Contains("Nota.md", refusal);
    }

    [Theory]
    [InlineData(1152.0, "1152")]
    [InlineData(-0.0, "0")]
    [InlineData(1.15, "1.15")]
    [InlineData(0.1 + 0.2, "0.30000000000000004")]
    [InlineData(1e21, "1e+21")]
    [InlineData(123456789012345680000.0, "123456789012345680000")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(0.000001, "0.000001")]
    [InlineData(-1.5e-10, "-1.5e-10")]
    [InlineData(5e-324, "5e-324")]
    [InlineData(1371.7067890123, "1371.7067890123")]
    public void NumbersAreWrittenAsJavaScriptWritesThem(double value, string expected) =>
        Assert.Equal(expected, LegacyJson.NumberToString(value));

    [Fact]
    public void ObjectsKeepTheJavaScriptPropertyOrderAndEscaping()
    {
        var node = JsonNode.Parse("""{"b":1,"10":"dez","2":"dois","a":[],"o":{}}""")!.AsObject();
        node["s"] = "Á \"q\" \\ \t\u0001 \ud800";
        Assert.Equal("{\n \"2\": \"dois\",\n \"10\": \"dez\",\n \"b\": 1,\n \"a\": [],\n \"o\": {},\n \"s\": \"Á \\\"q\\\" \\\\ \\t\\u0001 \\ud800\"\n}",
            LegacyJson.Stringify(node, 1));
    }

    private static JsonDocument Fixture(string name) =>
        JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "legacy-city", name)));

    private static DirectoryInfo Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp", "Urbe.Portable.slnx"))) dir = dir.Parent;
        return dir ?? throw new InvalidOperationException("apps/urbe not found");
    }
}
