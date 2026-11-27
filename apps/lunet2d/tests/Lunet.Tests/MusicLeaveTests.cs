using Avalonia.Controls;
using Lunet.Client;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>UX07: leaving the music studio by any route (not only its Close) stops a preview still rendering.</summary>
public sealed class MusicLeaveTests
{
    [Fact]
    public void LeavingTheStudioWhileThePreviewRenders_PlaysNothingAfterwards()
    {
        var root = Directory.CreateTempSubdirectory("lunet-music-leave-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var played = 0;
                var shell = new LunetShell(root) { PlaySound = _ => played++, StopSound = () => { } };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var path = Path.Combine(shell.Workspace.Project!.Directory, "Assets", "tema.song.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var music = new MusicStudioView(shell, path, () => { });
                var host = new Window { Width = 412, Height = 915, Content = music };
                host.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                var stayed = music.PlaySong();                       // control: staying, it plays
                Wait(stayed);
                Assert.Equal(1, played);

                var left = music.PlaySong();
                host.Content = null;                                  // another destination took the screen
                Wait(left);
                Assert.Equal(1, played);                              // nothing plays over the next screen
                host.Close(); window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }

    private static void Wait(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!task.IsCompleted && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
        Assert.True(task.IsCompleted);
    }
}
