using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // FeaturesCheck "Aberturas seguintes": the last note on the screen before the whole folder is read
    [Fact]
    public async Task Startup_TheLastNoteShowsBeforeTheFolderIsRead() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-early-" + Guid.NewGuid().ToString("N"));
            var vault = GenerateVault(Path.Combine(dir, "cofre-cedo"), 3000);
            var selection = Path.Combine(dir, "app", "sel.json");
            var device = new MemoryDeviceStore();
            const string target = "Pasta 3/Sub 0/Nota 51.md";
            var file = Path.Combine(vault, "Pasta 3", "Sub 0", "Nota 51.md");
            try
            {
                var first = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                first.Customization.Device = device;
                var window = new Window { Width = 412, Height = 900, Content = first };
                window.Show();
                await first.OpenVaultAsync();
                first.OpenNoteByPath(target);
                Dispatcher.UIThread.RunJobs();
                await first.FlushPendingAsync();
                window.Close();

                var again = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                again.Customization.Device = device;
                var window2 = new Window { Width = 412, Height = 900, Content = again };
                window2.Show();
                var init = again.InitializeAsync();
                bool early = false;
                for (int i = 0; i < 400 && !init.IsCompleted; i++)
                {
                    if (again.Editor.IsVisible && again.Editor.Editor.Text == File.ReadAllText(file)) { early = true; break; }
                    await Task.Delay(5);
                    Dispatcher.UIThread.RunJobs();
                }
                Assert.True(early, "the note waited for the whole folder");
                Assert.False(init.IsCompleted);
                Assert.True(again.Editor.Editor.IsReadOnly);   // read while the folder is read: nothing to lose
                again.Editor.Editor.CaretIndex = 10;
                await init;
                Dispatcher.UIThread.RunJobs();
                Assert.False(again.Editor.Editor.IsReadOnly);   // the same note, now editable where it was
                Assert.Equal(target, again.Editor.Current!.Path);
                Assert.Equal(10, again.Editor.Editor.CaretIndex);
                var typed = again.Editor.Editor.Text + "\nDepois de abrir.\n";
                again.Editor.Editor.Text = typed;
                Assert.True(await again.Editor.SaveAsync());
                Assert.Equal(typed, File.ReadAllText(file));
                window2.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
