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

    /// <summary>
    /// "Excluir" (house panel and the editor's "Mover para a lixeira"), recorded from the 1.8.4
    /// (csharp/tools/legacy-trash-oracle.mjs): the file leaves the vault, .urbe/trash.v2.json keeps the document,
    /// and the map loses the house. Trash, files and map are the original's bytes after each deletion.
    /// </summary>
    [Fact]
    public void EveryDeletionLeavesTheTrashTheFilesAndTheMapOfTheOriginal()
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "legacy-city", "trash-note-run-a.json")));
        var (city, store, writer, disk) = OpenFixture(json.RootElement, out var houseIds);
        var trash = LegacyTrash.Read(null, null, out var readOnly);
        Assert.False(readOnly);
        foreach (var step in json.RootElement.GetProperty("steps").EnumerateObject().Select(p => p.Value))
        {
            var from = step.GetProperty("from").GetString()!;
            var doc = store.Get(from)!;
            var house = city.Buildings.Single(b => b.IsNote && b.DocumentId == doc.Id);
            Assert.NotNull(trash.Trash(store, doc.Id, step.GetProperty("now").GetInt64()));
            city.DeleteNote(house);
            disk.Remove(from);
            Assert.Null(store.Get(from));
            Assert.Equal(step.GetProperty("trashWritten").GetString(), trash.Export());
            Assert.Equal(step.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, step));
            Assert.Equal(step.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString())),
                disk.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, (string?)p.Value)));
        }
        // Notas → Lixeira: Restaurar (the file and a house come back), then ✕ Excluir de vez
        var after = json.RootElement.GetProperty("after");
        var restore = after.GetProperty("restore");
        var restorePath = restore.GetProperty("path").GetString()!;
        var item = trash.Items.Single(i => i["originalPath"]!.GetValue<string>() == restorePath);
        long restoreNow = restore.GetProperty("now").GetInt64();
        var back = trash.Restore(store, item["document"]!["id"]!.GetValue<string>())!;
        var expectedHouse = restore.GetProperty("buildings").EnumerateArray().Last();
        houseIds.Enqueue(expectedHouse.GetProperty("id").GetString()!);
        var made = city.AddDocumentHouse(back, restoreNow);
        Assert.Equal(expectedHouse.GetProperty("path").GetString(), made.Path);
        Assert.Equal((expectedHouse.GetProperty("x").GetInt32(), expectedHouse.GetProperty("y").GetInt32()), (made.X, made.Y));
        disk[restorePath] = back.Content;
        Assert.Equal(restore.GetProperty("trashWritten").GetString(), trash.Export());
        Assert.Equal(restore.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, restore));
        Assert.Equal(restore.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString())),
            disk.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, (string?)p.Value)));
        Assert.Throws<InvalidOperationException>(() => LegacyTrash.Read(LastTrash(json), null, out _).Restore(store, back.Id));

        var purge = after.GetProperty("purge");
        var purged = trash.Items.Single(i => i["originalPath"]!.GetValue<string>() == purge.GetProperty("path").GetString());
        Assert.True(trash.Purge(purged["document"]!["id"]!.GetValue<string>()));
        Assert.Equal(purge.GetProperty("trashWritten").GetString(), trash.Export());
        Assert.Equal(purge.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, purge));

        // what is written is read back as it was: a later deletion keeps every earlier item
        var last = json.RootElement.GetProperty("steps").GetProperty("editor").GetProperty("trashWritten").GetString()!;
        Assert.Equal(last, LegacyTrash.Read(last, null, out _).Export());
    }

    private static string LastTrash(JsonDocument json) =>
        json.RootElement.GetProperty("steps").GetProperty("editor").GetProperty("trashWritten").GetString()!;

    [Theory]
    [InlineData("""{"version":3,"items":[]}""")]
    [InlineData("""{"items":[]}""")]
    [InlineData("não é json")]
    public void ATrashOfAnotherVersionIsPreservedAndNeverWritten(string v2)
    {
        var trash = LegacyTrash.Read(v2, null, out var readOnly);
        Assert.True(readOnly);
        Assert.Empty(trash.Items);
    }

    [Fact]
    public void AVersion1TrashIsReadWhenThereIsNoVersion2()
    {
        var v1 = """{"items":[{"document":{"id":"doc_a","path":"A.md","content":"x"},"deletedAt":5,"originalPath":"A.md"},{"document":{"path":"sem id"}}]}""";
        var trash = LegacyTrash.Read(null, v1, out var readOnly);
        Assert.False(readOnly);
        Assert.Equal("{\n \"version\": 2,\n \"items\": [\n  {\n   \"document\": {\n    \"id\": \"doc_a\",\n    \"path\": \"A.md\",\n    \"content\": \"x\"\n   },\n   \"deletedAt\": 5,\n   \"originalPath\": \"A.md\"\n  }\n ]\n}", trash.Export());
    }

    /// <summary>
    /// The selection bar of Notas (csharp/tools/legacy-selection-oracle.mjs): Mover para pasta (explorer.move: the file
    /// moves, the house changes neighbourhood but keeps its lot), Duplicar ("Plano cópia.md", the house 4 tiles away)
    /// and Mover para a lixeira with two notes. Files, map and trash are the original's after each step.
    /// </summary>
    [Fact]
    public void TheSelectionBarLeavesTheFilesTheMapAndTheTrashOfTheOriginal()
    {
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Root().FullName, "csharp", "tests", "fixtures", "legacy-city", "notes-selection-run-a.json")));
        var (city, store, writer, disk) = OpenFixture(json.RootElement, out var houseIds);
        var trash = LegacyTrash.Read(null, null, out _);
        var steps = json.RootElement.GetProperty("steps");
        void Check(JsonElement step)
        {
            Assert.Equal(step.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, step));
            Assert.Equal(step.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString())),
                disk.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, (string?)p.Value)));
        }

        // Favoritar changes no file (the 1.8.4 keeps favourites in localStorage)
        Check(steps.GetProperty("favorite"));

        var move = steps.GetProperty("move");
        foreach (var from in move.GetProperty("paths").EnumerateArray().Select(p => p.GetString()!))
        {
            var doc = store.Get(from)!;
            var house = city.Buildings.Single(b => b.IsNote && b.DocumentId == doc.Id);
            var path = LegacyCity.FolderMovePath(store, doc, move.GetProperty("to").GetString()!);
            var moved = LegacyCity.MoveDocument(store, doc, path);
            city.ApplyFolderMove(house, moved, move.GetProperty("now").GetInt64());
            disk[path] = disk[from];
            disk.Remove(from);
            var expected = move.GetProperty("buildings").EnumerateArray().Single(b => b.GetProperty("id").GetString() == house.Id);
            Assert.Equal((expected.GetProperty("x").GetInt32(), expected.GetProperty("y").GetInt32()), (house.X, house.Y));
            Assert.Equal(expected.GetProperty("regionId").GetString(), house.RegionId);
        }
        Check(move);

        var dup = steps.GetProperty("duplicate");
        var plano = store.Get(dup.GetProperty("paths")[0].GetString()!)!;
        var planoHouse = city.Buildings.Single(b => b.IsNote && b.DocumentId == plano.Id);
        var madeHouse = dup.GetProperty("buildings").EnumerateArray().Last();
        var copyId = dup.GetProperty("documents").EnumerateArray().Single(d => d.GetProperty("path").GetString() == madeHouse.GetProperty("path").GetString()).GetProperty("id").GetString();
        var copy = LegacyCity.DuplicateDocument(store, plano, copyId);
        houseIds.Enqueue(madeHouse.GetProperty("id").GetString()!);
        var copyHouse = city.DuplicateHouse(planoHouse, copy, dup.GetProperty("now").GetInt64());
        Assert.Equal((madeHouse.GetProperty("x").GetInt32(), madeHouse.GetProperty("y").GetInt32()), (copyHouse.X, copyHouse.Y));
        disk[copy.Path] = copy.Content;
        Check(dup);

        var del = steps.GetProperty("delete");
        foreach (var from in del.GetProperty("paths").EnumerateArray().Select(p => p.GetString()!))
        {
            var doc = store.Get(from)!;
            var house = city.Buildings.Single(b => b.IsNote && b.DocumentId == doc.Id);
            trash.Trash(store, doc.Id, del.GetProperty("now").GetInt64());
            city.DeleteNote(house);
            disk.Remove(from);
        }
        Assert.Equal(del.GetProperty("trashWritten").GetString(), trash.Export());
        Check(del);
    }

    /// <summary>The vault of a fixture opened like the original (ids fed back) and its first map checked.</summary>
    private static (LegacyCity City, DocumentStore Store, LegacyCityMap Writer, Dictionary<string, string> Disk) OpenFixture(JsonElement rootEl) => OpenFixture(rootEl, out _);

    private static (LegacyCity City, DocumentStore Store, LegacyCityMap Writer, Dictionary<string, string> Disk) OpenFixture(JsonElement rootEl, out Queue<string> buildingIds)
    {
        var open = rootEl.GetProperty("open");
        var files = rootEl.GetProperty("files").EnumerateObject().Select(p => (p.Name, p.Value.GetString()!)).ToList();
        var regionIds = new Queue<string>(open.GetProperty("regions").EnumerateArray().Select(r => r.GetProperty("id").GetString()!));
        var noteIds = open.GetProperty("buildings").EnumerateArray().ToDictionary(b => b.GetProperty("path").GetString()!, b => b.GetProperty("id").GetString()!);
        var ids = buildingIds = new Queue<string>(files.Select(f => noteIds[f.Item1]));
        var documentIds = open.GetProperty("documents").EnumerateArray().ToDictionary(d => d.GetProperty("path").GetString()!, d => d.GetProperty("id").GetString()!);
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), p => p == "r" ? regionIds.Dequeue() : ids.Dequeue());
        var store = city.Open(files, null, documentIds, open.GetProperty("now").GetInt64());
        var writer = new LegacyCityMap(_ => throw new InvalidOperationException("every id comes from the open"));
        Assert.Equal(open.GetProperty("mapaWritten").GetString(), Produce(writer, city, store, open));
        return (city, store, writer, files.ToDictionary(f => f.Item1, f => f.Item2, StringComparer.Ordinal));
    }

    /// <summary>CopyNoteAt names the copy as the 1.8.4 names a placed clone (v21ConfirmPlacement 'copy').</summary>
    [Fact]
    public void ACopyIsNamedAsTheOriginalNamesItsCloneAndIsANewHouse()
    {
        var city = new LegacyCity(new LegacyCityTerrain((x, y) => World.Value.At(x, y).Biome), p => p + Guid.NewGuid().ToString("N")[..6]);
        city.Open([("Nota.txt", "texto"), ("Nota cópia.txt", "outra")],
            """{"v":4,"mundo":"placas-1","notas":{"Nota.txt":{"id":"doc_n","x":40,"y":30,"sprite":"house2","tags":["a"],"aiLocal":{"memory":"m"}},"Nota cópia.txt":{"id":"doc_c","x":46,"y":30}}}""",
            new Dictionary<string, string> { ["Nota.txt"] = "doc_n", ["Nota cópia.txt"] = "doc_c" });
        var source = city.Buildings.Single(b => b.Path == "Nota.txt");
        (int X, int Y) lot = default;
        for (int r = 4; r < 60 && lot == default; r++)
        for (int dy = -r; dy <= r && lot == default; dy++)
        for (int dx = -r; dx <= r && lot == default; dx++)
            if (city.CanHouse(source.X + dx, source.Y + dy) && city.RegionAt(source.X + dx, source.Y + dy) is null) lot = (source.X + dx, source.Y + dy);
        Assert.Null(city.CopyNoteAt(source, source.X, source.Y, "x", 0));            // not on a taken lot

        var copy = city.CopyNoteAt(source, lot.X, lot.Y, "Nota cópia", 1760000000000)!;
        Assert.Equal("Nota cópia (2).txt", copy.Path);                               // the source's extension, unique in the root
        Assert.Equal("house2", copy.Sprite);
        Assert.NotSame(source, copy);
        Assert.Null(copy.DocumentId);                                                // its own document, given by the caller
        Assert.Equal((lot.X, lot.Y), (copy.X, copy.Y));
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
