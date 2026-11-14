using Lunet.Graphics;
using Lunet.Host;
using Lunet.Input;

namespace Lunet.Tests;

/// <summary>LUNET-482: the portable workspace a desktop host uses — create, edit, compile, run and see a game.</summary>
public sealed class DesktopHostTests
{
    [Fact]
    public void Workspace_CreatesEditsCompilesRunsAndRendersAGame()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-desktop-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new WorkspaceSession(root);
            var project = workspace.Create("Desktop");
            Assert.Contains("Desktop", workspace.Projects());
            workspace.Write(project.Manifest.EntryPoint, """
                using Lunet; using Lunet.Graphics; using Lunet.Input;
                public sealed class G : Game
                {
                    private SpriteBatch _b = null!; private Texture2D _t = null!; private bool _touched;
                    protected override void LoadContent() { _b = new SpriteBatch(GraphicsDevice); _t = Texture2D.CreateSolid(GraphicsDevice, 1, 1, Color.White); }
                    protected override void Update(GameTime time) { if (Input.TryGetPointer(out _)) _touched = true; }
                    protected override void Draw(GameTime time)
                    {
                        GraphicsDevice.Clear(_touched ? new Color(0, 255, 0) : new Color(255, 0, 0));
                    }
                }
                """);
            Assert.True(workspace.Compile().Success, string.Join("\n", workspace.LastCompile!.Diagnostics));
            using var game = workspace.Run(320, 180)!;
            Assert.True(game.Frame(1 / 30.0));
            var first = game.Screen.ScreenPixel(160, 90);
            Assert.True(first == new Color(255, 0, 0), $"{first.R},{first.G},{first.B},{first.A}");
            game.Pointer(TouchPhase.Pressed, 100, 50);
            game.Frame(1 / 30.0); game.Frame(1 / 30.0);
            Assert.Equal(new Color(0, 255, 0), game.Screen.ScreenPixel(160, 90));
            Assert.True(game.AverageFrameMilliseconds >= 0);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Workspace_ReportsCompileErrorsInsteadOfRunning()
    {
        var root = Path.Combine(Path.GetTempPath(), "lunet-desktop-" + Guid.NewGuid().ToString("N"));
        try
        {
            var workspace = new WorkspaceSession(root);
            var project = workspace.Create("Quebrado");
            workspace.Write(project.Manifest.EntryPoint, "class X { void M() { int a = ; } }");
            Assert.Null(workspace.Run(100, 100));
            Assert.False(workspace.LastCompile!.Success);
            Assert.Throws<InvalidOperationException>(() => new WorkspaceSession(root).Files());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
