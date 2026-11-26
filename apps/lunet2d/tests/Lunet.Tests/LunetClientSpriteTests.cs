using Avalonia.Controls;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>A1: the Sprite Studio in the shell.</summary>
public sealed class LunetClientSpriteTests
{
    [Fact]
    public void PngEncoder_WritesAPngThatDecodesBackToTheSamePixels()
    {
        var rgba = new byte[3 * 2 * 4];
        for (var i = 0; i < rgba.Length; i++) rgba[i] = (byte)(i * 11);
        var png = PngEncoder.Rgba(3, 2, rgba);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
        UiSession.Run(() =>
        {
            using var bitmap = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(png));
            Assert.Equal(3, bitmap.PixelSize.Width);
            Assert.Equal(2, bitmap.PixelSize.Height);
        });
    }

    [Fact]
    public void SpriteStudio_DrawsFillsUndoesAnimatesAndSavesSourceAndSheetTogether()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-sprite-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var studio = shell.WorkspaceView.NewSprite();
                Assert.Same(studio, shell.WorkspaceView.Studio);
                Assert.Equal(16, studio.Document.Width);

                studio.SelectColor(0xFFFF004D);
                studio.Paint(0, 0, first: true);
                studio.Paint(3, 0, first: false);                       // a stroke joins the points
                Assert.All(Enumerable.Range(0, 4), x => Assert.Equal(0xFFFF004Du, studio.Document.Frames[0][x]));
                studio.SelectTool(SpriteTool.Fill);
                studio.SelectColor(0xFF29ADFF);
                studio.SelectTool(SpriteTool.Fill);
                studio.Paint(8, 8, first: true);
                Assert.Equal(0xFF29ADFFu, studio.Document.Frames[0][8 * 16 + 8]);
                studio.Undo();                                           // the fill, one step
                Assert.Equal(0u, studio.Document.Frames[0][8 * 16 + 8]);
                Assert.Equal(0xFFFF004Du, studio.Document.Frames[0][0]);

                studio.SelectTool(SpriteTool.Picker);
                studio.Paint(1, 0, first: true);
                Assert.Equal(0xFFFF004Du, studio.Color);
                Assert.Equal(SpriteTool.Pencil, studio.Tool);

                studio.TogglePicker();                                    // the colour picker: hex sets the colour
                Assert.True(studio.PickerOpen);
                var hex = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(studio).OfType<TextBox>().Single(t => t.Name == "ColorHex");
                hex.Text = "#00E436";
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(0xFF00E436u, studio.Color);
                hex.Text = "#FF004D";
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                studio.TogglePicker();
                Assert.False(studio.PickerOpen);

                studio.AddFrame(duplicate: true);
                Assert.Equal(2, studio.Document.Frames.Count);
                Assert.Equal(1, studio.Frame);
                Assert.Equal(0xFFFF004Du, studio.Document.Frames[1][0]);

                Avalonia.Threading.Dispatcher.UIThread.RunJobs();         // lay the canvas out
                studio.Canvas.SetView(4, new Avalonia.Vector(10, 0));     // pinch zoom: pixels under a point follow the view
                var area = studio.Canvas.Area;
                Assert.Equal((0, 0), studio.Canvas.PixelAt(new Avalonia.Point(area.X + 1, area.Y + 1)));
                studio.Canvas.ResetView();
                Assert.Equal(1, studio.Canvas.Zoom);

                Assert.True(shell.Back());                               // Back saves and leaves the Studio
                Assert.Null(shell.WorkspaceView.Studio);
                var dir = shell.Workspace.Project!.Directory;
                Assert.True(File.Exists(Path.Combine(dir, "Content", "Textures", "sprite-1.sprite.json")));
                var png = File.ReadAllBytes(Path.Combine(dir, "Content", "Textures", "sprite-1.png"));
                using var sheet = new Avalonia.Media.Imaging.Bitmap(new MemoryStream(png));
                Assert.Equal(32, sheet.PixelSize.Width);                 // two 16 px frames side by side
                Assert.Equal(16, sheet.PixelSize.Height);

                var again = shell.WorkspaceView.OpenSprite("Content/Textures/sprite-1.sprite.json");
                Assert.Equal(2, again.Document.Frames.Count);            // reopens what was saved
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>A2: the Tile Studio in the shell.</summary>
public sealed class LunetClientTileTests
{
    [Fact]
    public void TileStudio_CreatesAMapPaintsLayersUndoesAndSaves_AndEditsItsTileset()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-tile-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var studio = shell.WorkspaceView.NewMap();
                Assert.Equal(16, studio.Session.MaxTileId);                      // a 64 px atlas of 16 px tiles: four painted, twelve to draw
                Assert.NotNull(studio.TilesetSource);

                studio.SelectTile(2);
                studio.Paint(1, 1, first: true);
                studio.Paint(2, 1, first: false);
                studio.Canvas.GetType();                                       // stroke ends as the finger lifts:
                Assert.True(studio.Session.EndStroke());
                Assert.Equal(2, studio.Session.Map.GetTile(0, new Lunet.Pathfinding.GridPoint(2, 1)));
                studio.SetErase(true);
                studio.Paint(2, 1, first: true);
                studio.Session.EndStroke();
                Assert.Equal(0, studio.Session.Map.GetTile(0, new Lunet.Pathfinding.GridPoint(2, 1)));
                studio.Undo();
                Assert.Equal(2, studio.Session.Map.GetTile(0, new Lunet.Pathfinding.GridPoint(2, 1)));

                Assert.True(shell.Back());                                       // saves and leaves
                Assert.Null(shell.WorkspaceView.Studio);
                var reopened = shell.WorkspaceView.OpenMap("Content/Data/mapa-1.tilemap.json");
                Assert.Equal(2, reopened.Session.Map.GetTile(0, new Lunet.Pathfinding.GridPoint(1, 1)));

                // Tileset → Sprite Studio → back to the map.
                var sprite = shell.WorkspaceView.OpenSprite(reopened.TilesetSource!, reopen: () => shell.WorkspaceView.OpenMap("Content/Data/mapa-1.tilemap.json"));
                Assert.IsType<SpriteStudioView>(shell.WorkspaceView.Studio);
                Assert.True(shell.Back());
                Assert.IsType<TileStudioView>(shell.WorkspaceView.Studio);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>A4: the sound effects Studio in the shell.</summary>
public sealed class LunetClientSoundTests
{
    [Fact]
    public void SoundStudio_GeneratesVariesPlaysAndSavesSourceAndWavTogether()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-sound-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var played = new List<byte[]>();
                var shell = new LunetShell(root) { PlaySound = played.Add };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var studio = shell.WorkspaceView.NewSound();
                var first = studio.Sound;
                studio.Generate();
                Assert.NotEqual(first, studio.Sound);
                studio.Vary();
                Assert.Equal(2, played.Count);                                  // every change is heard
                Assert.Equal((byte)'R', played[0][0]);                          // a WAV ("RIFF")
                studio.Play();
                Assert.Equal(3, played.Count);

                Assert.True(shell.Back());                                      // saves and leaves
                Assert.Null(shell.WorkspaceView.Studio);
                var dir = shell.Workspace.Project!.Directory;
                Assert.True(File.Exists(Path.Combine(dir, "Content", "Audio", "efeito-1.sfx.json")));
                Assert.True(new FileInfo(Path.Combine(dir, "Content", "Audio", "efeito-1.wav")).Length > 44);
                var again = shell.WorkspaceView.OpenSound("Content/Audio/efeito-1.sfx.json");
                Assert.Equal(studio.Sound, again.Sound);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>P2: mind maps as an outline in the shell.</summary>
public sealed class LunetClientMindMapTests
{
    [Fact]
    public void MindMap_AddsEditsFoldsOutdentsRemovesUndoes_AndWritesEachChange()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-mind-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var view = shell.WorkspaceView.NewMindMap();
                var center = view.Map.Root;
                var enemies = view.AddChild(center, "Inimigos");
                var slime = view.AddChild(enemies, "Slime");
                view.Rename(slime, "Slime verde");
                view.Outdent(slime);                                     // up one level: beside Inimigos
                Assert.Equal(center, view.Map.Topics.First(t => t.Id == slime).Parent);
                view.Fold(center);
                Assert.Single(view.Map.Walk(center));                    // folded: only the centre shows
                view.Fold(center);
                view.Remove(enemies);
                Assert.DoesNotContain(view.Map.Topics, t => t.Id == enemies);
                view.History.Undo(_ => { });                              // (persist is tested below)
                var saved = Lunet.Core.ProjectMindMap.Parse(shell.Workspace.Project!.ReadText("Notes/mapa-1.mindmap.json"));
                Assert.DoesNotContain(saved.Topics, t => t.Id == enemies);  // each change is written at once
                Assert.Contains(saved.Topics, t => t.Text == "Slime verde");
                Assert.True(shell.Back());
                Assert.Null(shell.WorkspaceView.Studio);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>A3: the Music Studio in the shell.</summary>
public sealed class LunetClientMusicTests
{
    [Fact]
    public void MusicStudio_StartsWithASong_TogglesNotesPlaysAndSaves()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-music-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var played = new List<byte[]>();
                var shell = new LunetShell(root) { PlaySound = played.Add };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var studio = shell.WorkspaceView.NewMusic();
                Assert.Equal(4, studio.Song.Tracks.Count);                           // melody, chords, bass, drums
                Assert.NotEmpty(studio.Song.Patterns[studio.Pattern].Notes);
                studio.SelectTrack(0);
                var before = studio.Song.Patterns[studio.Pattern].Notes.Count;
                var pitch = studio.Roll.PitchOfRow(0);
                studio.Toggle(1, 0, first: true);                                   // a note where there was none (or removes it)
                var after = studio.Song.Patterns[studio.Pattern].Notes.Count;
                Assert.NotEqual(before, after);
                studio.Toggle(1, 0, first: true);                                   // the same cell again: back as it was
                Assert.Equal(before, studio.Song.Patterns[studio.Pattern].Notes.Count);
                studio.AddPattern(duplicate: true);
                Assert.Equal(3, studio.Song.Patterns.Count);
                studio.PlayPattern();
                Assert.Equal((byte)'R', played[^1][0]);

                Assert.True(shell.Back());
                var dir = shell.Workspace.Project!.Directory;
                Assert.True(File.Exists(Path.Combine(dir, "Content", "Audio", "musica-1.song.json")));
                Assert.True(new FileInfo(Path.Combine(dir, "Content", "Audio", "musica-1.wav")).Length > 1000);
                Assert.Equal(3, shell.WorkspaceView.OpenMusic("Content/Audio/musica-1.song.json").Song.Patterns.Count);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>P6: Versões (Git) in the shell.</summary>
public sealed class LunetClientGitTests
{
    private static string Git(string dir, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        start.Environment["GIT_CONFIG_SYSTEM"] = "/dev/null";
        foreach (var k in new[] { "GIT_AUTHOR_NAME", "GIT_COMMITTER_NAME" }) start.Environment[k] = "Bia";
        foreach (var k in new[] { "GIT_AUTHOR_EMAIL", "GIT_COMMITTER_EMAIL" }) start.Environment[k] = "bia@example.com";
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException($"git {string.Join(' ', arguments)}: {error}");
        return output;
    }

    private static T Wait<T>(Task<T> task)
    {
        while (!task.IsCompleted) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return task.GetAwaiter().GetResult();
    }

    [Fact]
    public void GerarApk_SetsIdentity_CompilesPacksContentAndReportsErrors()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-apk-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                IReadOnlyDictionary<string, byte[]>? packed = null;
                Lunet.Core.LunetProject? packedProject = null;
                var shell = new LunetShell(root) { PackageGameApk = (project, files) => { packedProject = project; packed = files; return new byte[2048]; } };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.Workspace.Write("Content/Data/fase.json", "[]");
                var apk = shell.WorkspaceView.OpenApk();
                Assert.Same(apk, shell.WorkspaceView.Studio);

                Assert.Throws<ArgumentException>(() => apk.SetPackage("Não Vale"));
                apk.SetPackage("com.exemplo.meujogo");
                apk.SetOrientation("landscape");
                var manifest = Lunet.Core.ProjectManifest.Parse(shell.Workspace.Read(Lunet.Core.ProjectStore.ManifestFileName));
                Assert.Equal("com.exemplo.meujogo", manifest.PackageId);
                Assert.Equal("landscape", manifest.Orientation);

                var bytes = Wait(apk.BuildAsync());
                Assert.NotNull(bytes);
                Assert.Equal("com.exemplo.meujogo", packedProject!.Manifest.PackageId);
                Assert.True(packed!.ContainsKey("game/Game.dll"));
                Assert.True(packed.ContainsKey("game/Content/Data/fase.json"));
                Assert.DoesNotContain(packed.Keys, k => k.EndsWith(".cs", StringComparison.Ordinal));   // never the source
                Assert.StartsWith("APK pronto", apk.Status);

                var entry = shell.Workspace.Project!.Manifest.EntryPoint;
                shell.Workspace.Write(entry, shell.Workspace.Read(entry) + "\nclass Quebrado { void X() { y = ; } }");
                shell.WorkspaceView.ReloadFromDisk();
                Assert.Null(Wait(apk.BuildAsync()));
                Assert.NotEmpty(apk.Errors);
                Assert.Contains("não compila", apk.Status);

                Assert.True(shell.Back());
                Assert.Null(shell.WorkspaceView.Studio);
                var desktop = new LunetShell(Path.Combine(root, "b"));                 // no packer: says where it is made
                window.Content = desktop;
                desktop.CreateProject();
                var none = desktop.WorkspaceView.OpenApk();
                Assert.Null(Wait(none.BuildAsync()));
                Assert.Contains("aparelho Android", none.Status);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Versions_SendToAndReceiveFromARemote_WithTheAccountSaved()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-gitremote-ui-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var bare = Path.Combine(root, "servidor.git");
            Directory.CreateDirectory(bare);
            Git(bare, "init", "--bare", "-q");
            UiSession.Run(() =>
            {
                var shell = new LunetShell(Path.Combine(root, "jogos")) { GitTransport = (_, _) => new ProcessGitTransport(bare) };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var git = shell.WorkspaceView.OpenGit();
                git.Start();
                git.SaveVersion("Primeira versão", "Ana", "ana@example.com");

                Assert.Contains("endereço", Wait(git.SendAsync()));                    // no address yet: says so
                const string address = "https://example.com/ana/jogo.git";
                git.SetRemoteUrl(address);
                Assert.Equal(address, git.RemoteUrl);
                git.OpenAccount();
                git.SaveAccount(new Lunet.Core.GitAccount { Name = "Ana", Email = "ana@example.com", Token = "segredo" });
                Assert.Equal("segredo", new Lunet.Core.GitAccountStore(shell.GitAccountPath).Load().Token);
                Assert.DoesNotContain("segredo", File.ReadAllText(Path.Combine(shell.Workspace.Project!.Directory, ".git", "config")));

                var sent = Wait(git.SendAsync());
                Assert.StartsWith("Enviado", sent);
                Assert.Contains("Primeira versão", Git(bare, "log", "--oneline", "--all"));

                // Someone else changes the game on the server.
                var other = Path.Combine(root, "outra");
                var line = Lunet.Git.GitRepository.Open(shell.Workspace.Project.Directory).CurrentBranch!;
                Git(root, "clone", "-q", "-b", line, bare, other);
                var entry = shell.Workspace.Project.Manifest.EntryPoint;
                File.AppendAllText(Path.Combine(other, entry), "\n// da Bia");
                Git(other, "commit", "-qam", "Mudança da Bia");
                Git(other, "push", "-q", "origin", line);

                var received = Wait(git.ReceiveAsync());
                Assert.DoesNotContain("Não foi possível", received);
                Assert.Contains("// da Bia", File.ReadAllText(Path.Combine(shell.Workspace.Project.Directory, entry)));
                Assert.Equal("Mudança da Bia", Lunet.Git.GitRepository.Open(shell.Workspace.Project.Directory).Log(1)[0].Summary);
                window.Close();
            });
        }
        finally
        {
            if (Directory.Exists(root))
            {
                foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void Versions_StartHistory_ShowChanges_SaveAVersion_AndListIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-git-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var git = shell.WorkspaceView.OpenGit();
                Assert.False(git.IsRepository);
                git.Start();
                Assert.True(git.IsRepository);
                var dir = shell.Workspace.Project!.Directory;
                Assert.Contains(".lunet/", File.ReadAllText(Path.Combine(dir, ".gitignore")));

                var first = git.SaveVersion("Primeira versão", "Pessoa", "pessoa@example.com");
                Assert.NotNull(first);
                var repo = Lunet.Git.GitRepository.Open(dir);
                Assert.Equal("Primeira versão", repo.Log(5)[0].Summary);
                Assert.DoesNotContain(repo.GetStatus(), s => s.Unstaged != Lunet.Git.FileChange.None);  // everything went in
                Assert.Equal("Pessoa", repo.Config.Get("user", null, "name"));

                File.AppendAllText(Path.Combine(dir, shell.Workspace.Project.Manifest.EntryPoint), "\n// mudança");
                git.Refresh();
                Assert.Contains(repo.GetStatus(), s => s.Unstaged == Lunet.Git.FileChange.Modified);
                Assert.True(shell.Back());
                Assert.Null(shell.WorkspaceView.Studio);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>A5: the particles Studio in the shell.</summary>
public sealed class LunetClientParticleTests
{
    [Fact]
    public void ParticleStudio_RunsTheRealEmitter_BurstsAndSavesTheEffect()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-particles-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var studio = shell.WorkspaceView.NewParticles();
                studio.Burst(new System.Numerics.Vector2(120, 120));
                studio.Step(0.05);
                Assert.True(studio.Alive > 0);
                Assert.True(shell.Back());
                var path = Path.Combine(shell.Workspace.Project!.Directory, "Content", "Effects", "efeito-1.particles.json");
                Assert.True(File.Exists(path));
                var settings = Lunet.Graphics.ParticleSettings.FromJson(File.ReadAllText(path));
                Assert.True(settings.LifetimeSeconds > 0);
                Assert.Null(shell.WorkspaceView.Studio);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

/// <summary>A5: the game's palette in the shell, used by the Sprite Studio.</summary>
public sealed class LunetClientPaletteTests
{
    [Fact]
    public void Palette_PresetRampExtractRemove_SavesAndFeedsTheSpriteStudio()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-palette-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var sprite = shell.WorkspaceView.NewSprite();
                sprite.SelectColor(0xFF123456);
                sprite.Paint(0, 0, first: true);
                shell.Back();

                var palette = shell.WorkspaceView.OpenPalette();
                palette.UsePreset("neon");
                Assert.Equal(8, palette.Palette.Colors.Count);
                Assert.True(palette.AddColors(Lunet.Studio.ProjectPalette.Ramp(0xFFFF2E88, 5)) > 0);
                Assert.True(palette.ExtractFromArt() >= 1);                       // the sprite's colour
                Assert.Contains(palette.Palette.Colors, c => c.Argb == 0xFF123456);
                var count = palette.Palette.Colors.Count;
                palette.Remove(0);
                Assert.Equal(count - 1, palette.Palette.Colors.Count);
                var saved = Lunet.Studio.ProjectPalette.Parse(shell.Workspace.Project!.ReadText(Lunet.Studio.ProjectPalette.DefaultPath));
                Assert.Equal(count - 1, saved.Colors.Count);
                shell.Back();

                var again = shell.WorkspaceView.OpenSprite("Content/Textures/sprite-1.sprite.json");
                Assert.Equal(saved.Argb, again.GamePalette);                       // the Sprite Studio offers the game's colours
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void DataStudio_CreatesATableEditsRecordsAndColumnsAndSavesDataAndSchemaTogether()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-data-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var data = shell.WorkspaceView.NewData("Inimigos");
                Assert.Same(data, shell.WorkspaceView.Studio);
                Assert.Single(data.Table.Rows);
                Assert.Null(data.SetField(0, "id", "slime"));
                Assert.Null(data.SetField(0, "nome", "Slime"));
                data.AddColumn("vida", Lunet.Studio.Data.DataColumnType.Integer);
                Assert.NotNull(data.SetField(0, "vida", "muito"));             // rejected, with the reason
                Assert.Null(data.SetField(0, "vida", "12"));
                var copy = data.DuplicateRecord(0);
                Assert.Equal(1, copy);
                Assert.Equal(1, data.Record);
                Assert.Null(data.SetField(1, "id", "morcego"));
                Assert.True(shell.Back());                                     // record → list
                Assert.Null(data.Record);
                Assert.Same(data, shell.WorkspaceView.Studio);

                var path = "Content/Data/inimigos.json";
                Assert.True(DataView.IsTable(path));
                var project = shell.Workspace.Project!;
                var saved = Lunet.Studio.Data.DataTable.Parse(project.ReadText(path), project.ReadText(DataView.SchemaPath(path)));
                Assert.Equal(2, saved.Rows.Count);
                Assert.Equal("12", saved.Display(0, "vida"));
                Assert.Equal("morcego", saved.Display(1, "id"));
                data.RemoveRecord(1);
                data.RemoveColumn("vida");
                shell.Back();
                Assert.Null(shell.WorkspaceView.Studio);

                var reopened = shell.WorkspaceView.OpenData(path);
                Assert.Single(reopened.Table.Rows);
                Assert.DoesNotContain(reopened.Table.Columns, c => c.Name == "vida");
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void SpriteStudio_SelectsMovesFlipsDuplicatesAndErasesPixels()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-select-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var studio = shell.WorkspaceView.NewSprite();
                var doc = studio.Document;
                const uint red = 0xFFFF0000, blue = 0xFF0000FF;
                studio.SelectColor(red);
                studio.Paint(1, 1, first: true);
                studio.SelectColor(blue);
                studio.Paint(2, 1, first: true);
                uint At(int x, int y) => studio.Document.Frames[0][y * doc.Width + x];

                studio.SelectTool(SpriteTool.Select);
                studio.DragSelect(1, 1, 2, 1);                                   // a 2×1 rectangle
                Assert.Equal(2, studio.Selection!.Count);
                studio.DragSelect(1, 1, 5, 6);                                   // dragging inside moves it by (4,5)
                Assert.NotNull(studio.Floating);
                studio.FlipSelection(horizontal: true);
                studio.CommitFloating();
                Assert.Null(studio.Floating);
                Assert.Equal(0u, At(1, 1));
                Assert.Equal(blue, At(5, 6));                                    // flipped: blue first
                Assert.Equal(red, At(6, 6));

                studio.Undo();                                                   // one step back: the move
                Assert.Equal(red, At(1, 1));
                Assert.Equal(0u, At(5, 6));

                studio.SetSelectionMode(SpriteSelectMode.Wand);
                studio.Paint(1, 1, first: true);                                 // same colour: only the red pixel
                Assert.Equal(1, studio.Selection!.Count);
                studio.DuplicateSelection();                                     // a copy floats 2 px away
                studio.CommitFloating();
                Assert.Equal(red, At(1, 1));
                Assert.Equal(red, At(3, 3));

                studio.SelectAll();
                Assert.Equal(doc.Width * doc.Height, studio.Selection!.Count);
                studio.DeleteSelection();
                Assert.All(studio.Document.Frames[0], p => Assert.Equal(0u, p));
                studio.Undo();
                Assert.Equal(red, At(3, 3));

                studio.SetSelectionMode(SpriteSelectMode.Rectangle);
                studio.DragSelect(1, 1, 2, 1);
                studio.MoveSelection(0, 2);
                studio.SelectTool(SpriteTool.Pencil);                           // leaving Select applies the move
                Assert.Null(studio.Floating);
                Assert.Equal(red, At(1, 3));
                Assert.True(shell.Back());                                       // saves
                var saved = Lunet.Studio.SpriteDocument.Parse(shell.Workspace.Project!.ReadText("Content/Textures/sprite-1.sprite.json"));
                Assert.Equal(red, saved.Frames[0][3 * doc.Width + 1]);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void TileStudio_PaintsTerrainWithAutotileAndManagesLayers()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-terrain-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var map = shell.WorkspaceView.NewMap();
                Assert.Equal(16, map.Session.MaxTileId);
                map.SelectTile(1);
                map.UseTerrain();                                                // 16 tiles from tile 1: fits
                Assert.True(map.TerrainTool);
                Assert.NotNull(map.Session.Terrain);
                Assert.NotNull(map.SetTerrain(Lunet.Studio.AutotileLayout.Blob47)); // 47 do not: the reason
                Assert.Null(map.Session.Terrain);
                Assert.Null(map.SetTerrain(Lunet.Studio.AutotileLayout.Sides16));
                map.Paint(3, 5, first: true);
                map.Paint(4, 5, first: false);
                map.Paint(5, 5, first: false);
                Assert.True(map.Session.EndStroke());
                int At(int x, int y) => map.Session.Map.GetTile(0, new Lunet.Pathfinding.GridPoint(x, y));
                Assert.Equal(1 + 2, At(3, 5));                                    // east only
                Assert.Equal(1 + 2 + 8, At(4, 5));                                // east and west
                Assert.Equal(1 + 8, At(5, 5));                                    // west only
                map.SetTerrainErase(true);
                map.Paint(5, 5, first: true);
                map.Session.EndStroke();
                Assert.Equal(0, At(5, 5));
                Assert.Equal(1 + 8, At(4, 5));                                    // the neighbour lost its east side

                map.ToggleLayerMenu();
                Assert.True(map.LayerMenuOpen);
                map.Session.AddLayer("Detalhes");
                map.RenameLayer("Decoração");
                Assert.Equal("Decoração", map.Session.Map.GetLayerName(map.Session.Layer));
                var layer = map.Session.Layer;
                map.MoveLayer(-1);
                Assert.Equal(layer - 1, map.Session.Layer);
                Assert.Equal("Decoração", map.Session.Map.GetLayerName(0));
                Assert.True(map.RemoveLayer());
                Assert.Equal(1, map.Session.Map.LayerCount);
                Assert.False(map.RemoveLayer());                                  // at least one layer
                Assert.True(shell.Back());
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MusicStudio_SetsNoteLengthInstrumentMixerAndArrangement()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-music2-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var played = new List<byte[]>();
                var shell = new LunetShell(root) { PlaySound = played.Add };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var studio = shell.WorkspaceView.NewMusic();
                studio.SelectTrack(0);
                var notes = studio.Song.Patterns[studio.Pattern].Notes;
                notes.RemoveAll(n => n.Track == 0);
                studio.SetNoteLength(4);
                studio.Toggle(2, 3, first: true);
                Assert.Equal(4, notes.Single(n => n.Track == 0).Length);
                var steps = studio.Song.Patterns[studio.Pattern].Steps;
                studio.Toggle(steps - 1, 5, first: true);                          // near the end: cut to fit
                Assert.Equal(1, notes.Single(n => n.Track == 0 && n.Step == steps - 1).Length);

                studio.ShowPanel(MusicPanel.Track);
                studio.SetInstrument("Sino FM");
                Assert.Equal("Sino FM", studio.Song.Tracks[0].Instrument.Name);
                Assert.Equal((byte)'R', played[^1][0]);                            // a preview of the new sound
                studio.SetVolume(0.3f);
                studio.SetPan(-0.5f);
                studio.ToggleMute();
                studio.ToggleSolo();
                Assert.Equal(0.3f, studio.Song.Tracks[0].Volume);
                Assert.True(studio.Song.Tracks[0].Muted && studio.Song.Tracks[0].Solo);
                Assert.True(studio.AddTrack());
                Assert.Equal(5, studio.Song.Tracks.Count);
                Assert.Equal(4, studio.Track);

                studio.ShowPanel(MusicPanel.Arrangement);
                var order = studio.Song.Order.ToList();
                studio.AppendToOrder(0);
                Assert.Equal(order.Count + 1, studio.Song.Order.Count);
                studio.MoveOrder(studio.Song.Order.Count - 1, -1);
                Assert.Equal(0, studio.Song.Order[^2]);
                studio.RemoveOrder(studio.Song.Order.Count - 2);
                Assert.Equal(order, studio.Song.Order);

                Assert.True(shell.Back());
                var saved = Lunet.Studio.Music.MusicSong.Parse(shell.Workspace.Project!.ReadText("Content/Audio/musica-1.song.json"));
                Assert.Equal("Sino FM", saved.Tracks[0].Instrument.Name);
                Assert.Equal(5, saved.Tracks.Count);
                Assert.Contains(saved.Patterns.SelectMany(p => p.Notes), n => n.Track == 0 && n.Length == 4);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Resources_GroupsWithoutTwins_ImportsSearchesShowsDetailsAndOpensStudios()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-resources-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                shell.WorkspaceView.NewSprite().Paint(1, 1, first: true);
                shell.Back();
                shell.State.SelectTab(WorkspaceTab.Art);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var page = window.GetVisualDescendants().OfType<ResourcesPage>().Single();

                var groups = ResourcesPage.Groups(shell.Workspace.Files());
                var art = groups.First(g => g.Label == "Arte").Files;
                Assert.Contains("Content/Textures/sprite-1.sprite.json", art);
                Assert.DoesNotContain("Content/Textures/sprite-1.png", art);          // the sheet is the sprite's twin

                var png = Lunet.Client.Shell.PngEncoder.Rgba(2, 2, new byte[16]);
                Assert.Equal("Content/Textures/fundo.png", page.Import("fundo.png", png));
                Assert.Equal("Content/Textures/fundo-2.png", page.Import("fundo.png", png));   // never over another file
                Assert.Equal("Content/Audio/pulo.wav", page.Import("pulo.wav", [1, 2, 3]));
                Assert.Equal("Content/Fonts/pixel.ttf", page.Import("pixel.ttf", [0]));
                Assert.Contains("Content/Fonts/pixel.ttf", page.Shown);

                var search = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Name == "ResourceSearch");
                search.Text = "fundo";
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(["Content/Textures/fundo-2.png", "Content/Textures/fundo.png"], page.Shown);

                page.Activate("Content/Textures/fundo.png");                         // not editable: the details sheet
                Assert.Equal("Content/Textures/fundo.png", page.Open);
                Assert.Equal("Textures/fundo.png", ResourcesPage.CodePath(page.Open!));
                Assert.True(shell.Back());                                             // Back closes the sheet first
                Assert.Null(page.Open);
                Assert.Equal(ShellLevel.Workspace, shell.State.Level);

                Assert.Equal("Content/Data/config.json", page.Import("config.json", System.Text.Encoding.UTF8.GetBytes("{ \"vidas\": 3 }")));
                Assert.Equal("Content/Data/notas.txt", page.Import("notas.txt", [65]));
                Assert.Contains("Content/Data/notas.txt", ResourcesPage.Groups(shell.Workspace.Files()).First(g => g.Label == "Outros").Files);
                Assert.Equal("Content/Data/quebrado.json", page.Import("quebrado.json", System.Text.Encoding.UTF8.GetBytes("vidas = 3")));
                page.Activate("Content/Data/quebrado.json");                           // not readable as a table: the sheet
                Assert.Null(shell.WorkspaceView.Studio);
                Assert.Equal("Content/Data/quebrado.json", page.Open);
                page.CloseSheet();
                page.Activate("Content/Data/config.json");                             // an object reads as one record…
                var table = Assert.IsType<DataView>(shell.WorkspaceView.Studio);
                table.EditAsText();                                                    // …and can be edited as text
                Assert.Null(shell.WorkspaceView.Studio);
                Assert.Equal(WorkspaceTab.Code, shell.State.Tab);
                Assert.Equal("Content/Data/config.json", shell.WorkspaceView.Code.OpenPath);
                var analysis = shell.WorkspaceView.Code.AnalyzeNow();
                while (!analysis.IsCompleted) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Empty(((CodeSurface)shell.WorkspaceView.Code.Surface).Diagnostics);  // no C# errors on JSON
                shell.WorkspaceView.Code.Surface.Text = "{ \"vidas\": 5 }";
                Assert.True(shell.WorkspaceView.Code.Save());
                Assert.Contains("5", shell.Workspace.Read("Content/Data/config.json"));
                shell.State.SelectTab(WorkspaceTab.Art);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                page = window.GetVisualDescendants().OfType<ResourcesPage>().Single();
                page.Activate("Content/Textures/sprite-1.sprite.json");
                Assert.IsType<SpriteStudioView>(shell.WorkspaceView.Studio);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void CodeEditor_CreatesRenamesDeletesFiles_AndFindsAndReplaces()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-files-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var code = shell.WorkspaceView.Code;
                var entry = shell.Workspace.Project!.Manifest.EntryPoint;

                code.ToggleFileMenu();
                Assert.True(code.FileMenuOpen);
                var path = code.NewCodeFile("inimigo");
                Assert.Equal("Scripts/Inimigo.cs", path);
                Assert.Equal(path, code.OpenPath);
                Assert.Contains("class Inimigo", shell.Workspace.Read(path));
                Assert.True(shell.Workspace.Compile().Success);                       // the new file compiles

                code.Surface.Text = "int vida = 3; vida = vida - 1; // Vida";
                Assert.Equal("Scripts/Chefe.cs", code.RenameOpen("Chefe"));            // the extension is kept
                Assert.DoesNotContain(path, shell.Workspace.Files());
                Assert.Contains("vida = vida", shell.Workspace.Read("Scripts/Chefe.cs"));  // unsaved text went with it
                Assert.Equal("Scripts/Inimigos/Chefe.cs", code.RenameOpen("Inimigos/Chefe")); // Pasta/Nome: into a folder
                Assert.Throws<ArgumentException>(() => code.RenameOpen("../fora"));

                code.ShowFind();
                Assert.True(code.FindOpen);
                Assert.Equal(4, code.Find("vida"));                                     // case-insensitive by default
                Assert.Equal(8, code.Surface.SelectionEnd);
                Assert.True(code.ReplaceCurrent("hp"));
                Assert.StartsWith("int hp = 3;", code.Surface.Text);
                Assert.Equal(3, code.ReplaceAll("hp"));
                Assert.Equal("int hp = 3; hp = hp - 1; // hp", code.Surface.Text);

                code.DeleteOpen();
                Assert.DoesNotContain("Scripts/Inimigos/Chefe.cs", shell.Workspace.Files());
                Assert.NotNull(code.OpenPath);
                code.Load(entry);
                Assert.Throws<Lunet.Core.ProjectException>(() => code.DeleteOpen());   // the game's entry stays
                Assert.Contains(entry, shell.Workspace.Files());
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void CodeEditor_FoldsRegions_SelectsOccurrences_InsertsBlocks_AndGoesBack()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-fold-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var editor = shell.WorkspaceView.Code;
                var code = (CodeSurface)editor.Surface;
                code.Text = "public sealed class A\n{\n    void M()\n    {\n        int x = 1;\n        x = x + 1;\n        int max = 2;\n    }\n}\n";
                var analysis = editor.AnalyzeNow();
                while (!analysis.IsCompleted) Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(code.FoldCount >= 2);                                   // the class and the method
                code.GoTo(3, 5);
                Assert.True(code.ToggleFoldAtCaret());
                Assert.Equal(1, code.FoldedCount);
                code.SetAllFolded(false);
                Assert.Equal(0, code.FoldedCount);

                code.GoTo(5, 13);                                                    // inside "x"
                Assert.True(code.SelectNextOccurrence());
                Assert.Equal("x", code.Text[code.SelectionStart..code.SelectionEnd]);
                var first = code.SelectionStart;
                Assert.True(code.SelectNextOccurrence());
                Assert.True(code.SelectionStart > first);

                editor.JumpTo(null, 1, 1);
                Assert.Equal(1, editor.PositionsBack);
                Assert.True(editor.BackToPosition());
                Assert.True(code.Caret > 0);
                Assert.False(editor.BackToPosition());

                code.GoTo(5, 13);
                editor.EditAllOccurrences();
                Assert.True(editor.FindOpen);
                Assert.Equal(3, editor.ReplaceAll("valor"));                          // x, x, x — "max"-like words untouched
                Assert.Contains("int valor = 1;", code.Text);
                Assert.Contains("int max = 2;", code.Text);
                editor.HideFind();

                var copy = editor.SaveCopyAs("Copia");
                Assert.Equal("Copia.cs", copy);
                Assert.Contains("int valor = 1;", shell.Workspace.Read(copy!));
                Assert.Equal(copy, editor.OpenPath);
                Assert.Throws<ArgumentException>(() => editor.SaveCopyAs("Copia"));

                code.GoTo(5, 9);
                var r = Lunet.Editor.IndentationService.InsertBlock(code.Text, code.SelectionStart, code.SelectionEnd);
                code.Replace(r.Start, r.Start + r.DeleteLength, r.Insert, r.SelectionEnd);
                Assert.Contains("{", code.Text.Split('\n')[4] + code.Text.Split('\n')[5]);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MindMap_PresentsOneBranchPerSlide_AndBackLeavesThePresentation()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-present-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var map = shell.WorkspaceView.NewMindMap();
                var a = map.AddChild(map.Map.Root, "Mecânicas");
                map.AddChild(a, "Pulo duplo");
                map.AddChild(map.Map.Root, "Fases");
                map.Present();
                Assert.Equal(0, map.Slide);
                Assert.Equal(2, map.SlideCount);
                window.UpdateLayout();
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Pulo duplo") == true);
                map.Present(1);
                window.UpdateLayout();
                Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Fases");
                Assert.True(shell.Back());                                             // Back ends the presentation…
                Assert.Null(map.Slide);
                Assert.Same(map, shell.WorkspaceView.Studio);                          // …and stays in the map
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Studios_WithoutAGame_CreateEditSaveInTheStudioFolder_AndPublish()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-studios-" + Guid.NewGuid().ToString("N"));
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.Home.Go(HomeView.Destination.Studios);
                var studios = Assert.IsType<StudiosView>(shell.Home.Sheet);
                Assert.Null(shell.Workspace.Project);                                  // no game open

                var sprite = studios.NewSprite();
                sprite.Paint(2, 2, first: true);
                Assert.True(shell.Back());                                             // saves and returns to the page
                Assert.Null(studios.Studio);
                Assert.Contains("Content/Textures/sprite-1.sprite.json", studios.Creations);
                Assert.True(File.Exists(Path.Combine(shell.StudioWorkspacePath, "Content", "Textures", "sprite-1.png")));

                var map = studios.NewMap();
                map.EditTileset!(map.TilesetSource!);                                  // the tileset opens here, not in a game
                Assert.IsType<SpriteStudioView>(studios.Studio);
                shell.Back();
                Assert.IsType<TileStudioView>(studios.Studio);                         // back to the map
                shell.Back();

                studios.NewMusic(); shell.Back();
                studios.NewSound(); shell.Back();
                studios.NewParticles(); shell.Back();
                var data = studios.NewData("itens");
                window.UpdateLayout();
                Assert.False(window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "DataAsText").IsVisible);
                shell.Back();
                Assert.Contains(studios.Creations, f => f.EndsWith(".song.json"));
                Assert.Contains(studios.Creations, f => f.EndsWith(".sfx.json"));
                Assert.Contains(studios.Creations, f => f.EndsWith(".particles.json"));
                Assert.Contains("Content/Data/itens.json", studios.Creations);

                Assert.True(studios.Activate("Content/Textures/sprite-1.sprite.json"));
                Assert.Equal(16, Assert.IsType<SpriteStudioView>(studios.Studio).Document.Width);
                shell.Back();

                // Publicar goes to the Biblioteca with the creation; it then lives there for any game.
                var library = new Lunet.Core.ReusableAssetLibrary(shell.LibraryPath);
                var published = library.PublishWorkspace(shell.StudioWorkspacePath, "Content/Textures/sprite-1.sprite.json", "Herói", "Ana", "CC0");
                Assert.Equal(1, published.Version);
                Assert.True(shell.Back());                                             // closes the Studios page
                Assert.Null(shell.Home.Sheet);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
