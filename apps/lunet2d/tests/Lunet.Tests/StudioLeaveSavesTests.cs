using Avalonia.Controls;
using Lunet.Client;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>
/// Leaving the Studios page through the global bar (Jogar, Aprender…) with a Studio open saves it, like Back does —
/// switching destination never discards a drawing or a song.
/// </summary>
public sealed class StudioLeaveSavesTests
{
    [Theory]
    [InlineData("music", ".song.json", false)]
    [InlineData("sprite", ".sprite.json", false)]
    [InlineData("sprite", ".sprite.json", true)]
    public void SwitchingDestination_WithAStudioOpen_SavesIt(string studio, string extension, bool home)
    {
        var root = Directory.CreateTempSubdirectory("lunet-studio-leave-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root) { PlaySound = _ => { }, StopSound = () => { } };
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                var studios = shell.Home.ShowStudios();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                if (studio == "music") studios.NewMusic();                     // a new song starts with the starter tune
                else studios.NewSprite().Paint(2, 3, first: true);            // a drawing: one pixel painted
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                bool Saved() => Directory.Exists(studios.Storage.RootDirectory) &&
                    Directory.EnumerateFiles(studios.Storage.RootDirectory, "*" + extension, SearchOption.AllDirectories).Any();
                Assert.False(Saved());                                         // a new document is only in memory
                if (home) shell.Home.Go(HomeView.Destination.Games); else shell.Home.ShowPlay();         // the bar (Início or Jogar), not Back
                var until = DateTime.UtcNow.AddSeconds(30);
                while (!Saved() && DateTime.UtcNow < until) { Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
                Assert.True(Saved(), "o Studio aberto foi descartado ao trocar de destino");
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }
}
