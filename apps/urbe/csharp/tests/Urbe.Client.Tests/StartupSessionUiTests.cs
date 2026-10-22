using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>UC-59 (UX P10): reopening the app lands on the last note at once, editable.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Startup_SessionOpensTheLastNoteEditable() =>
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
                // first use: the vault is opened, a note left open
                var first = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                first.Customization.Device = device;
                var window = new Window { Width = 412, Height = 900, Content = first };
                window.Show();
                await first.OpenVaultAsync();
                first.OpenNoteByPath(target);
                Dispatcher.UIThread.RunJobs();
                await first.FlushPendingAsync();
                window.Close();

                // the next start: the note is open and editable
                var again = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                again.Customization.Device = device;
                var window2 = new Window { Width = 412, Height = 900, Content = again };
                window2.Show();
                await again.InitializeAsync();
                Assert.True(again.Editor.IsVisible);
                Assert.Equal(target, again.Editor.Current!.Path);
                Assert.Equal(File.ReadAllText(file), again.Editor.Editor.Text);
                Assert.True(again.IsReady);

                // typing and a verified save
                var typed = again.Editor.Editor.Text + "\nEscrito logo ao abrir.\n";
                again.Editor.Editor.Text = typed;
                Dispatcher.UIThread.RunJobs();
                Assert.True(await again.Editor.SaveAsync());
                Assert.Equal(typed, File.ReadAllText(file));
                Assert.Equal(typed, again.Documents!.Get(target)!.Content);
                window2.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task Startup_WithoutSessionOpensTheAreaDeTrabalho() =>
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
                Assert.True(again.Workspace.IsVisible);   // DEC-0053: the base; on a phone the Explorer is the drawer
                Assert.False(again.Explorer.IsVisible);
                Assert.Equal(12, again.Workspace.Index.Count);
                Assert.NotNull(again.Documents);
                window2.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
