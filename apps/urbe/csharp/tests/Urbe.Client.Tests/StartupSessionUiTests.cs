using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>UC-59 (UX P10): reopening the app lands on the last note at once; the city is built behind it.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Startup_SessionOpensTheLastNoteEditableBeforeTheWorldThenTheWorldLoads() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-startup-" + Guid.NewGuid().ToString("N"));
            var vault = GenerateVault(Path.Combine(dir, "cofre-inicio"), 120);
            var selection = Path.Combine(dir, "app", "sel.json");
            var device = new MemoryDeviceStore();
            const string target = "Pasta 3/Sub 0/Nota 51.md";
            var file = Path.Combine(vault, "Pasta 3", "Sub 0", "Nota 51.md");
            try
            {
                // first use: the vault is opened, its map saved, a note left open
                var first = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                first.Customization.Device = device;
                var window = new Window { Width = 412, Height = 900, Content = first };
                window.Show();
                await first.OpenVaultAsync();
                Assert.False(first.WorldLoading);
                first.OpenNoteByPath(target);
                Dispatcher.UIThread.RunJobs();
                await first.FlushMapAsync();
                window.Close();

                // the next start: the note is open and editable while the city is still being built
                var again = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                again.Customization.Device = device;
                var window2 = new Window { Width = 412, Height = 900, Content = again };
                window2.Show();
                var init = again.InitializeAsync();
                for (int i = 0; i < 2000 && !again.Editor.IsVisible && !init.IsCompleted; i++)
                {
                    await Task.Delay(2);
                    Dispatcher.UIThread.RunJobs();
                }
                Assert.True(again.Editor.IsVisible);
                Assert.Equal(target, again.Editor.Current!.Path);
                Assert.True(again.WorldLoading);
                Assert.Null(again.World.City);
                Assert.Equal("Carregando a cidade…", again.WorldLoadingText);
                Assert.Equal(File.ReadAllText(file), again.Editor.Editor.Text);

                // typing and a verified save, before the world exists
                var typed = again.Editor.Editor.Text + "\nEscrito enquanto a cidade carrega.\n";
                again.Editor.Editor.Text = typed;
                Dispatcher.UIThread.RunJobs();
                Assert.True(await again.Editor.SaveAsync());
                Assert.Equal(typed, File.ReadAllText(file));

                // then the world finishes behind the editor and takes in what was saved
                await init;
                Assert.False(again.WorldLoading);
                Assert.Null(again.WorldLoadingText);
                Assert.NotNull(again.World.City);
                Assert.Contains(again.World.City!.Buildings, b => b.IsNote && b.Path == target);
                Assert.Equal(typed, again.Documents!.Get(target)!.Content);
                Assert.True(again.Editor.IsVisible);
                Assert.Equal(target, again.Editor.Current!.Path);
                Assert.True(again.IsReady);
                window2.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Startup_WithoutSessionOpensTheCityAsBefore() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-startup-" + Guid.NewGuid().ToString("N"));
            var vault = GenerateVault(Path.Combine(dir, "cofre-sem-sessao"), 12);
            var selection = Path.Combine(dir, "app", "sel.json");
            try
            {
                var first = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                first.Customization.Device = new MemoryDeviceStore();
                var window = new Window { Width = 412, Height = 900, Content = first };
                window.Show();
                await first.OpenVaultAsync();
                window.Close();

                var again = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                again.Customization.Device = new MemoryDeviceStore();
                var window2 = new Window { Width = 412, Height = 900, Content = again };
                window2.Show();
                await again.InitializeAsync();
                Assert.False(again.Editor.IsVisible);
                Assert.False(again.WorldLoading);
                Assert.NotNull(again.World.City);
                window2.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
