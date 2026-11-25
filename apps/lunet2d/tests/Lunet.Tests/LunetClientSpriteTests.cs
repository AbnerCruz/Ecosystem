using Avalonia.Controls;
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
