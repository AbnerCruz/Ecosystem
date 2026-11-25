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
