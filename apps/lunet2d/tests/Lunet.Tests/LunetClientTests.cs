using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Host;

namespace Lunet.Tests;

/// <summary>ADR 0013 / DEC-0068: the one Avalonia shell (Lunet.Client) — navigation, game session, tokens and the real UI headless.</summary>
public sealed class LunetClientTests
{
    private const string RedGame = """
        using Lunet; using Lunet.Graphics;
        public sealed class G : Game
        {
            public int Frames;
            protected override void Update(GameTime time) => Frames++;
            protected override void Draw(GameTime time) => GraphicsDevice.Clear(new Color(255, 0, 0));
        }
        """;

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "lunet-client-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ShellState_BackClosesPanelThenLeavesWorkspaceThenIsNotConsumed()
    {
        var s = new ShellState();
        s.OpenProject("A");
        s.ShowPanel(SupportPanel.Console);
        Assert.True(s.Back());
        Assert.Equal(SupportPanel.None, s.Panel);
        Assert.Equal(ShellLevel.Workspace, s.Level);
        Assert.True(s.Back());
        Assert.Equal(ShellLevel.Home, s.Level);
        Assert.False(s.Back());
    }

    [Fact]
    public void ShellState_RemembersTheTabOfEachGame()
    {
        var s = new ShellState();
        s.OpenProject("A"); s.SelectTab(WorkspaceTab.Map);
        s.OpenProject("B"); Assert.Equal(WorkspaceTab.Code, s.Tab); s.SelectTab(WorkspaceTab.Sound);
        s.GoHome();
        s.OpenProject("A");
        Assert.Equal(WorkspaceTab.Map, s.Tab);
        s.OpenProject("B");
        Assert.Equal(WorkspaceTab.Sound, s.Tab);
    }

    [Fact]
    public void ShellState_ExpandedWidthPutsCodeAndGameSideBySide()
    {
        var s = new ShellState();
        s.OpenProject("A");
        Assert.False(s.Resize(412));
        Assert.False(s.CodeAndGameSideBySide);
        Assert.True(s.Resize(ShellState.ExpandedMinWidth));
        Assert.True(s.CodeAndGameSideBySide);
        s.SelectTab(WorkspaceTab.Art);
        Assert.False(s.CodeAndGameSideBySide);
    }

    [Fact]
    public void GameSession_PausesStepsExactlyOneFrameAndScalesTime()
    {
        var root = NewRoot();
        try
        {
            var workspace = new WorkspaceSession(root);
            var project = workspace.Create("Passo");
            workspace.Write(project.Manifest.EntryPoint, RedGame);
            using var session = new GameSession(workspace);
            Assert.True(session.Run(64, 64));
            Assert.Equal(GameState.Running, session.State);
            Assert.True(session.Tick(1 / 60.0));
            Assert.Equal(new Lunet.Graphics.Color(255, 0, 0), session.Game!.Screen.ScreenPixel(32, 32));

            session.TogglePause();
            Assert.False(session.Tick(1 / 60.0));
            var frames = session.FramesRendered;
            Assert.True(session.Step());
            Assert.Equal(frames + 1, session.FramesRendered);
            Assert.Equal(GameState.Paused, session.State);

            session.SetSpeed(0.25);
            Assert.Equal(0.25, session.Speed);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.SetSpeed(3));
            session.Stop();
            Assert.Equal(GameState.Stopped, session.State);
            Assert.Null(session.Game);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void GameSession_ReportsCompileProblemsInsteadOfRunning()
    {
        var root = NewRoot();
        try
        {
            var workspace = new WorkspaceSession(root);
            var project = workspace.Create("Quebrado");
            workspace.Write(project.Manifest.EntryPoint, "class X { void M() { int a = ; } }");
            using var session = new GameSession(workspace);
            Assert.False(session.Run(64, 64));
            Assert.Equal(GameState.CompileErrors, session.State);
            Assert.Contains(session.Problems, p => p.Contains("CS1525"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Game.cs", WorkspaceTab.Code)]
    [InlineData("lunet.json", WorkspaceTab.Code)]
    [InlineData("Assets/heroi.sprite.json", WorkspaceTab.Art)]
    [InlineData("Assets/fase1.tilemap.json", WorkspaceTab.Map)]
    [InlineData("Content/Audio/beep.wav", WorkspaceTab.Sound)]
    [InlineData("Assets/tema.song.json", WorkspaceTab.Sound)]
    [InlineData("Notes/ideias.md", WorkspaceTab.Notes)]
    public void ProjectFiles_PutsEachFileInItsTab(string path, WorkspaceTab tab)
    {
        foreach (var t in Enum.GetValues<WorkspaceTab>().Where(t => t != WorkspaceTab.Game))
            Assert.Equal(t == tab, ProjectFiles.For(t, [path]).Count == 1);
    }

    [Fact]
    public void Tokens_TextPairsMeetAA()
    {
        foreach (var bg in new[] { LunetTokens.Color.Background, LunetTokens.Color.Surface, LunetTokens.Color.SurfaceRaised })
        foreach (var fg in new[] { LunetTokens.Color.TextPrimary, LunetTokens.Color.TextSecondary, LunetTokens.Color.TextTertiary, LunetTokens.Color.Primary, LunetTokens.Color.Danger, LunetTokens.Color.Code })
            Assert.True(LunetTokens.Contrast(fg, bg) >= 4.5, $"{fg} sobre {bg}: {LunetTokens.Contrast(fg, bg):0.00}");
        Assert.True(LunetTokens.Contrast(LunetTokens.Color.OnPrimary, LunetTokens.Color.Primary) >= 4.5);
    }

    [Fact]
    public void Shell_CreateEditRunShowsTheGameAndSavesVerified_WithoutAnyDialog()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Empty(shell.Home.Shown);

                shell.CreateProject();
                Assert.Equal(ShellLevel.Workspace, shell.State.Level);
                Assert.Equal(ShellLayout.Compact, shell.State.Layout);
                var entry = shell.Workspace.Project!.Manifest.EntryPoint;
                Assert.Equal(entry, shell.WorkspaceView.Code.OpenPath);

                shell.WorkspaceView.Code.TextBox.Text = RedGame;
                shell.RunGame();
                Assert.Equal(GameState.Running, shell.Game.State);
                Assert.Equal(WorkspaceTab.Game, shell.State.Tab);
                Assert.Equal(RedGame, File.ReadAllText(Path.Combine(shell.Workspace.Project.Directory, entry)));
                for (int i = 0; i < 3; i++) shell.TickGame(1 / 60.0);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                var frame = window.CaptureRenderedFrame()!;
                Assert.True(CountRed(frame) > 1000, "the running game is visible in the Jogo tab");

                Assert.Empty(window.GetVisualDescendants().OfType<Window>());
                Assert.True(shell.State.Back());
                Assert.Equal(ShellLevel.Home, shell.State.Level);
                Assert.Equal(GameState.Stopped, shell.Game.State);
                Assert.Single(shell.Home.Shown);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Shell_WideWindowKeepsCodeBesideTheRunningGame_AndCompileErrorsOpenProblems()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 1280, Height = 800, Content = shell };
                window.Show();
                shell.CreateProject();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(shell.State.CodeAndGameSideBySide);

                shell.WorkspaceView.Code.TextBox.Text = RedGame;
                shell.RunGame();
                shell.TickGame(1 / 60.0);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(WorkspaceTab.Code, shell.State.Tab);
                Assert.True(shell.WorkspaceView.Code.IsEffectivelyVisible && shell.WorkspaceView.Game.IsEffectivelyVisible);

                shell.WorkspaceView.Code.TextBox.Text = "class X { void M() { int a = ; } }";
                shell.RunGame();
                Assert.Equal(GameState.CompileErrors, shell.Game.State);
                Assert.Equal(SupportPanel.Problems, shell.State.Panel);
                Assert.True(shell.WorkspaceView.Support.IsEffectivelyVisible);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(915, double.NaN, false, 0)]       // keyboard closed
    [InlineData(915, 600, true, 0)]               // Android ≤ 14, adjustResize: the system already shrank the window
    [InlineData(915, 600, false, 315)]            // Android 15+, edge-to-edge: the shell leaves the keyboard's height
    [InlineData(915, 1000, false, 0)]             // keyboard below the window (rotating): nothing covered
    public void KeyboardFit_CoversOnlyAnEdgeToEdgeWindow(double window, double keyboardTop, bool shrinks, double expected) =>
        Assert.Equal(expected, KeyboardFit.Cover(window, keyboardTop, shrinks));

    [Theory]
    [InlineData(0, 0, SwipeDirection.None)]
    [InlineData(10, -12, SwipeDirection.None)]
    [InlineData(0, -30, SwipeDirection.Up)]
    [InlineData(4, 25, SwipeDirection.Down)]
    [InlineData(-40, 10, SwipeDirection.Left)]
    [InlineData(30, -20, SwipeDirection.Right)]
    public void CodeKeys_ResolvesTapsAndSwipes(double dx, double dy, SwipeDirection expected) =>
        Assert.Equal(expected, CodeKeys.Resolve(dx, dy));

    [Fact]
    public void CodeKeys_BarIsTenKeysWithEveryCSharpSymbolOneGestureAway()
    {
        Assert.Equal(10, CodeKeys.Bar.Count);
        var reachable = CodeKeys.Bar.SelectMany(k => Enum.GetValues<SwipeDirection>().Select(k.For)).ToHashSet();
        foreach (var s in new[] { "{", "}", "(", ")", "[", "]", "<", ">", ";", ":", ",", ".", "=", "+", "-", "!", "=>", "\"", "'", "&", "|", "?", "*", "/", "%", "_", CodeKeys.Tab, CodeKeys.Left, CodeKeys.Right, CodeKeys.Up, CodeKeys.Down })
            Assert.Contains(s, reachable);
    }

    [Fact]
    public void CodeKeys_OpeningBraceMakesAnIndentedBlockInOneEdit()
    {
        var text = "    if (x)\n    ";
        var edit = CodeKeys.Apply(text, text.Length, text.Length, "{")!.Value;
        var result = text[..edit.Start] + edit.Insert + text[edit.End..];
        Assert.Equal("    if (x)\n    {\n        \n    }", result);
        Assert.Equal("    if (x)\n    {\n        ".Length, edit.Caret);
    }

    [Fact]
    public void CodeKeys_PairsWrapTheSelectionAndArrowsMove()
    {
        var edit = CodeKeys.Apply("a + b", 0, 5, "(")!.Value;
        Assert.Equal("(a + b)", edit.Insert);
        Assert.Equal(6, edit.Caret);
        Assert.Null(CodeKeys.Apply("abc", 1, 1, CodeKeys.Left));
        const string two = "abcd\nxy\nlonger";
        Assert.Equal(7, CodeKeys.Move(two, 2, CodeKeys.Down));      // column 2 on "xy" → its end
        Assert.Equal(2, CodeKeys.Move(two, 7, CodeKeys.Up));        // back to column 2 of "abcd"
        Assert.Equal(0, CodeKeys.Move(two, 0, CodeKeys.Up));
        Assert.Equal(two.Length, CodeKeys.Move(two, 9, CodeKeys.Down));
    }

    [Fact]
    public void Shell_OnAPhoneTheCodeBarTypesIntoTheEditor_AndTheKeyboardMovesTheTabsAside()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var editor = shell.WorkspaceView.Code;
                Assert.NotNull(editor.Keys);
                editor.TextBox.Text = "x";
                editor.TextBox.CaretIndex = 1;
                Assert.True(editor.Keys!.Type("("));
                Assert.Equal("x()", editor.TextBox.Text);
                Assert.Equal(2, editor.TextBox.CaretIndex);

                Assert.True(shell.WorkspaceView.TabBarVisible);
                shell.SetKeyboard(true);
                Assert.False(shell.WorkspaceView.TabBarVisible);
                shell.SetKeyboard(false);
                Assert.True(shell.WorkspaceView.TabBarVisible);

                window.Width = 1280;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Null(shell.WorkspaceView.Code.Keys); // a wide screen has a hardware keyboard and no code bar
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static int CountRed(Avalonia.Media.Imaging.Bitmap frame)
    {
        var w = frame.PixelSize.Width; var h = frame.PixelSize.Height;
        var pixels = new byte[w * h * 4];
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(pixels.Length);
        try
        {
            frame.CopyPixels(new PixelRect(0, 0, w, h), buffer, pixels.Length, w * 4);
            System.Runtime.InteropServices.Marshal.Copy(buffer, pixels, 0, pixels.Length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
        int red = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            // Bgra8888 or Rgba8888: pure red has one channel at 255 and the other two at 0.
            byte a = pixels[i], b = pixels[i + 1], c = pixels[i + 2];
            if ((a == 0 && b == 0 && c == 255) || (a == 255 && b == 0 && c == 0)) red++;
        }
        return red;
    }
}

/// <summary>One Avalonia headless (Skia) session for the whole test run; every UI test runs on its dispatcher.</summary>
internal static class UiSession
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(TestApp)));

    public static void Run(Action action) =>
        Session.Value.Dispatch(action, TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    public sealed class TestApp : LunetApp
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
