using Avalonia.Controls;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>M6: the Inspector and Desempenho panels of the game running in the shell.</summary>
public sealed class LunetClientInspectionTests
{
    private const string TunableGame = """
        using Lunet; using Lunet.Graphics;
        public enum Mode { Calm, Wild }
        public sealed class G : Game
        {
            public bool Red = true;
            [Range(0, 255)] public int Green;
            public string Title = "Olá";
            public Mode Mode;
            public int Frames { get; private set; }
            protected override void Update(GameTime time) => Frames++;
            protected override void Draw(GameTime time) => GraphicsDevice.Clear(new Color((byte)(Red ? 255 : 0), (byte)Green, 0));
        }
        """;

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "lunet-inspect-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FrameStats_KeepsTheLastFramesOldestFirst_AndCountsHitches()
    {
        var stats = new FrameStats();
        for (var i = 0; i < FrameStats.Capacity + 10; i++) stats.Add(new FrameSample(i < FrameStats.Capacity + 5 ? 10 : 40, 4, 5, 1));
        Assert.Equal(FrameStats.Capacity, stats.Count);
        Assert.Equal(FrameStats.Capacity + 10, stats.Recorded);
        Assert.Equal(40, stats.Samples[^1].Total);
        Assert.Equal(10, stats.Samples[0].Total);
        Assert.Equal(5, stats.OverBudget);
        Assert.Equal(40, stats.Max);
        Assert.Equal(10, stats.Percentile95); // 5 of 300 frames over: under the 95th rank
        var report = stats.Report("Jogo-1");
        Assert.Contains("Jogo-1", report);
        Assert.Contains("acima de 33,3 ms: 5", report);
        stats.Clear();
        Assert.Equal(0, stats.Count);
        Assert.Equal(0, stats.Average);
        Assert.Empty(stats.Samples);
    }

    [Fact]
    public void Inspector_ShowsTheRunningGame_AndAnEditWhilePausedIsDrawnAtOnce()
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
                shell.State.ShowPanel(SupportPanel.Inspector);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var inspector = shell.WorkspaceView.Support.Inspector;
                Assert.True(inspector.IsEffectivelyVisible);
                Assert.Empty(inspector.Rows); // nothing runs: an empty state with Rodar

                shell.WorkspaceView.Code.Surface.Text = TunableGame;
                shell.RunGame();
                shell.TickGame(1 / 60.0);
                shell.State.ShowPanel(SupportPanel.Inspector);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                inspector.Refresh();
                var labels = inspector.Rows.Select(r => r.Label).ToArray();
                Assert.Contains("Red", labels);
                Assert.Contains("Green", labels);
                Assert.Contains("Title", labels);
                Assert.Contains("Mode", labels);
                var frames = inspector.Rows.Single(r => r.Label == "Frames");
                Assert.False(frames.IsEditable); // private setter: shown, not editable

                shell.Game.TogglePause();
                Assert.Equal(GameState.Paused, shell.Game.State);
                var framesWhenPaused = GameInspection.Text(frames);
                var green = inspector.Rows.Single(r => r.Label == "Green");
                Assert.Equal(0f, green.Min);
                Assert.Equal(255f, green.Max);
                Assert.False(inspector.Inspection.TryApplyText(green, "verde"));
                Assert.Equal("0", GameInspection.Text(green));
                Assert.True(inspector.Inspection.TryApplyText(green, "200"));
                Assert.Equal("200", GameInspection.Text(green)); // applied while paused, without advancing time
                var red = inspector.Rows.Single(r => r.Label == "Red");
                inspector.Inspection.Apply(red, false);
                Assert.Equal("false", GameInspection.Text(red));
                var mode = inspector.Rows.Single(r => r.Label == "Mode");
                inspector.Inspection.Apply(mode, Enum.Parse(mode.ValueType!, "Wild"));
                Assert.Equal("Wild", GameInspection.Text(mode));
                Assert.Equal(framesWhenPaused, GameInspection.Text(frames)); // a redraw does not update the game

                var screen = shell.Game.Game!.Screen;
                var center = screen.ScreenPixel(screen.Width / 2, screen.Height / 2);
                Assert.Equal(0, center.R);   // red off
                Assert.Equal(200, center.G); // green 200: the paused frame was drawn again

                shell.StopGame();
                inspector.Refresh();
                Assert.Null(inspector.Inspection.Game);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Performance_RecordsEachFrameWithThePanelClosed_AndReportsThem()
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
                shell.WorkspaceView.Code.Surface.Text = TunableGame;
                shell.RunGame();
                for (var i = 0; i < 12; i++) shell.TickGame(1 / 60.0);
                Assert.True(shell.Game.Frames.Count >= 12); // the shell's own timer may add frames
                Assert.All(shell.Game.Frames.Samples, s => Assert.True(s.Total > 0 && s.Draw >= 0));
                Assert.True(shell.Game.Frames.Samples.Sum(s => s.Steps) >= 11);

                shell.State.ShowPanel(SupportPanel.Performance);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var view = shell.WorkspaceView.Support.Performance;
                Assert.True(view.IsEffectivelyVisible);
                view.Refresh();
                Assert.Contains($"Últimos {shell.Game.Frames.Count} quadros", view.Report());

                shell.RunGame(); // a new run starts a new window of frames
                Assert.True(shell.Game.Frames.Count <= 1); // the shell draws the first frame at once
                shell.StopGame();
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void FrameGraph_ScaleLeavesRoomAboveTheThirtyFpsLineAndTheWorstFrame()
    {
        Assert.True(FrameGraph.Scale([]) > FrameStats.Budget30);
        Assert.True(FrameGraph.Scale([new FrameSample(80, 0, 0, 0)]) > 80);
    }
}
