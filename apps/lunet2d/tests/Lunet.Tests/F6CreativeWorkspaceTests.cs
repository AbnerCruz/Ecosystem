using Lunet.Core;
using Lunet.Content;
using Lunet.Graphics;
using Lunet.Studio;
using Lunet.Studio.Data;
using Lunet.Studio.Music;

namespace Lunet.Tests;

public sealed class F6CreativeWorkspaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-f6-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void MindMapSurvivesProjectMoveAndKeepsPreviousFilesAndBrokenReference()
    {
        var store = new ProjectStore(Path.Combine(_root, "games"));
        var project = store.Create("Original");
        var note = ProjectNotes.CreateNote(project, "Roteiro");
        var sketch = ProjectNotes.NewSketchPath(project, "Arte");
        using (var bytes = new MemoryStream(ContentTests.EncodePng(1, 1, 6, [0, 0, 0, 0, 255])))
            project.Import("Notes", Path.GetFileName(sketch), bytes);
        var path = ProjectNotes.CreateMindMap(project, "Campanha");
        var map = ProjectMindMap.Parse(project.ReadText(path));
        Assert.Single(map.Walk(map.Root));
        map = map.Add(map.Root, "Início").Add(map.Root, "Final");
        var first = map.Topics[1].Id;
        var second = map.Topics[2].Id;
        map = map.Edit(first, "Início", note).ToggleConnection(first, second);
        project.WriteText(path, map.Serialize());
        Directory.Move(project.Directory, Path.Combine(Path.GetDirectoryName(project.Directory)!, "Movido"));
        project = store.Open("Movido");
        Assert.Equal(map.Root, ProjectMindMap.Parse(project.ReadText(path)).Root);
        Assert.Contains(ProjectNotes.List(project), item => item.Kind == NoteKind.MindMap);
        Assert.Contains(ProjectNotes.List(project), item => item.Kind == NoteKind.Sketch);
        Assert.Equal("# Roteiro\n\n", project.ReadText(note));
        project.DeleteFile(note);
        Assert.Equal(note, ProjectMindMap.Parse(project.ReadText(path)).Topics[1].Reference);
    }

    [Fact]
    public void MapNavigationFoldMovesAndUndoRemainConsistentWithManyTopics()
    {
        var map = ProjectMindMap.Create("Centro");
        for (int i = 0; i < 250; i++) map = map.Add(map.Root, "Tópico " + i);
        var first = map.Topics[1].Id;
        var second = map.Topics[2].Id;
        var history = new MindMapHistory(map);
        history.Apply(map.Move(second, first).Fold(first), _ => { });
        Assert.Equal(250, history.Current.Walk(map.Root).Count);
        Assert.Throws<InvalidOperationException>(() => history.Current.Move(first, second));
        Assert.Throws<IOException>(() => history.Undo(_ => throw new IOException("Falha")));
        Assert.True(history.CanUndo);
        history.Undo(_ => { });
        Assert.Equal(251, history.Current.Walk(map.Root).Count);
        history.Redo(_ => { });
        Assert.Equal(250, history.Current.Walk(map.Root).Count);
        Assert.Equal(2, ProjectMindMap.Parse(history.Current.Serialize()).Walk(first, fold: false).Count);
    }

    [Fact]
    public void StudioPublishesAndForksAllFamiliesWithoutAnotherProjectAsSource()
    {
        var workspace = new StudioAssetWorkspace(Path.Combine(_root, "studio"));
        var library = new ReusableAssetLibrary(Path.Combine(_root, "library"));
        var png = ContentTests.EncodePng(16, 16, 6, new byte[16 * 65]);
        var atlas = SpriteDocument.Create(16);
        workspace.WriteBundle(("Content/Textures/tiles.png", png),
            ("Content/Textures/tiles.sprite.json", System.Text.Encoding.UTF8.GetBytes(atlas.ToJson())));
        var map = TileMap.Create("Textures/tiles.png", 2, 2, 16, 16);
        workspace.WriteText("Content/Data/world.tilemap.json", map.ToJson());
        var song = MusicSong.Empty("Tema");
        workspace.WriteText("Content/Audio/tema.song.json", song.ToJson());
        workspace.WriteBytes("Content/Audio/tema.wav", new MusicRenderer().RenderWav(song, cancellationToken: TestContext.Current.CancellationToken));
        var fx = SoundDesigner.Generate("coin", 1);
        workspace.WriteText("Content/Audio/efeito.sfx.json", SoundDesigner.ToJson(fx));
        workspace.WriteBytes("Content/Audio/efeito.wav", SoundDesigner.RenderWav(fx));
        var palette = new ProjectPalette { Name = "Noite" };
        palette.AddRange([0xff445577]);
        workspace.WriteTextPair("Assets/noite.palette.json", palette.ToJson(), "Assets/noite.gpl", palette.ToGpl());
        workspace.WriteText("Content/Effects/luz.particles.json", new ParticleSettings().ToJson());
        var items = DataTable.Create("Itens", new DataColumn { Name = "id", Key = true });
        var player = DataTable.Create("Jogador", new DataColumn { Name = "item", Type = DataColumnType.Reference, ReferenceTable = "itens" });
        workspace.WriteTextPair("Content/Data/itens.json", items.ToJson(), "Content/Data/itens.schema.json", items.SchemaToJson());
        workspace.WriteTextPair("Content/Data/jogador.json", player.ToJson(), "Content/Data/jogador.schema.json", player.SchemaToJson());
        var expected = new Dictionary<string, int>
        {
            ["Content/Textures/tiles.sprite.json"] = 2, ["Content/Data/world.tilemap.json"] = 3,
            ["Content/Audio/tema.song.json"] = 2, ["Content/Audio/efeito.sfx.json"] = 2,
            ["Assets/noite.palette.json"] = 2, ["Content/Effects/luz.particles.json"] = 1, ["Content/Data/jogador.json"] = 4
        };
        var store = new ProjectStore(Path.Combine(_root, "games"));
        foreach (var (path, files) in expected)
        {
            var revision = library.PublishWorkspace(workspace.RootDirectory, path, "Arte", "Lunet", "Uso privado");
            Assert.Equal(path, revision.PrimaryPath);
            Assert.Equal(files, revision.Files.Length);
            library.VerifyIntegrity(revision.Id, revision.Version);
            var first = store.Create("Jogo" + expected.Keys.ToList().IndexOf(path) + "A");
            var second = store.Create("Jogo" + expected.Keys.ToList().IndexOf(path) + "B");
            library.ImportToProject(revision.Id, revision.Version, first, TestContext.Current.CancellationToken);
            library.ImportToProject(revision.Id, revision.Version, second, TestContext.Current.CancellationToken);
            using var zip = new MemoryStream();
            second.ExportZip(zip);
            zip.Position = 0;
            using var exported = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Read);
            foreach (var file in revision.Files) Assert.NotNull(exported.GetEntry(second.Name + "/" + file.Path));
            string original = second.ReadText(path);
            first.WriteText(path, "{}");
            Assert.Equal(original, second.ReadText(path));
            Assert.Single(Directory.GetFiles(Path.Combine(first.Directory, ".lunet", "asset-forks")));
            var next = library.PublishWorkspace(workspace.RootDirectory, path, "Nova revisão", "Lunet", "Uso privado", existingAssetId: revision.Id);
            Assert.Equal(1, next.BasedOnVersion);
            Assert.Throws<IOException>(() => library.ImportToProject(next.Id, next.Version, second, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public void MissingDependenciesCancelledForkAndBundleFailurePreserveLastGoodState()
    {
        var studio = new StudioAssetWorkspace(Path.Combine(_root, "studio"));
        var library = new ReusableAssetLibrary(Path.Combine(_root, "library"));
        var path = "Content/Data/level.tilemap.json";
        studio.WriteText(path, TileMap.Create("Textures/missing.png", 2, 2, 16, 16).ToJson());
        Assert.Throws<FileNotFoundException>(() => library.PublishWorkspace(studio.RootDirectory, path, "Mapa", "", "Uso privado"));
        studio.WriteBytes("Content/Textures/missing.png", [1, 2, 3]);
        var asset = library.PublishWorkspace(studio.RootDirectory, path, "Mapa", "", "Uso privado");
        var game = new ProjectStore(Path.Combine(_root, "games")).Create("Jogo");
        Assert.Throws<OperationCanceledException>(() => library.ImportToProject(asset.Id, 1, game, new CancellationToken(true)));
        Assert.DoesNotContain(path, game.ListFiles());
        studio.WriteText("Assets/estado.json", "anterior");
        Directory.CreateDirectory(Path.Combine(studio.RootDirectory, "Assets", "ocupado"));
        Assert.ThrowsAny<IOException>(() => studio.WriteTextPair("Assets/estado.json", "novo", "Assets/ocupado", "bloqueado"));
        Assert.Equal("anterior", studio.ReadText("Assets/estado.json"));
        library.VerifyIntegrity(asset.Id, asset.Version);
    }

    [Fact]
    public void InterruptedBundleRestoresPreviousBytesWhenReopened()
    {
        var studio = new StudioAssetWorkspace(Path.Combine(_root, "studio"));
        studio.WriteText("Assets/fonte.json", "novo");
        studio.WriteText("Assets/derivado.png", "original");
        var stage = Path.Combine(studio.RootDirectory, StudioAssetWorkspace.RecoveryFolder, "stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        File.WriteAllText(Path.Combine(stage, "0.before"), "anterior");
        File.WriteAllText(Path.Combine(stage, "1.next"), "incompleto");
        static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
        File.WriteAllText(Path.Combine(stage, "journal.json"), System.Text.Json.JsonSerializer.Serialize(new[]
        {
            new { Relative = "Assets/fonte.json", Before = Hash("anterior"), After = Hash("novo") },
            new { Relative = "Assets/derivado.png", Before = Hash("original"), After = Hash("incompleto") }
        }));
        studio = new StudioAssetWorkspace(studio.RootDirectory);
        Assert.Equal("anterior", studio.ReadText("Assets/fonte.json"));
        Assert.Equal("original", studio.ReadText("Assets/derivado.png"));
        Assert.False(Directory.Exists(stage));
    }

    [Fact]
    public void PublishingRenderedOutputUsesExactSourceAndDoesNotRewriteTheDraft()
    {
        var studio = new StudioAssetWorkspace(Path.Combine(_root, "studio"));
        var library = new ReusableAssetLibrary(Path.Combine(_root, "library"));
        const string source = "Content/Audio/som.sfx.json";
        const string wave = "Content/Audio/som.wav";
        var fx = SoundDesigner.Generate("coin", 73);
        studio.WriteText(source, SoundDesigner.ToJson(fx));
        studio.WriteBytes(wave, [1, 2, 3]);
        var render = new LibraryAssetRender(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(studio.ReadBytes(source))),
            wave, SoundDesigner.RenderWav(fx));
        var revision = library.PublishWorkspace(studio.RootDirectory, source, "Original", "", "Uso privado", render: render);
        Assert.Equal([1, 2, 3], studio.ReadBytes(wave));
        Assert.Equal(render.Bytes.Length, revision.Files.Single(f => f.Path == wave).Length);
        studio.WriteText(source, SoundDesigner.ToJson(SoundDesigner.Generate("coin", 82)));
        Assert.Throws<IOException>(() => library.PublishWorkspace(studio.RootDirectory, source, "Revision", "", "Uso privado",
            existingAssetId: revision.Id, render: render));
        Assert.Single(library.List());
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
