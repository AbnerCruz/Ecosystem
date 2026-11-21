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

    /// <summary>
    /// AbnerCruz/Urbe#51: after a pause longer than the person's setting, the Urbe opens on the Área de trabalho, not the
    /// last note; the note is untouched and opens from there. Back from the background after a long pause, the open note
    /// closes (saved) and the Área de trabalho shows; a short pause goes on where it was.
    /// </summary>
    [Fact]
    public async Task Startup_AfterALongPauseOpensTheAreaDeTrabalho() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-pausa-" + Guid.NewGuid().ToString("N"));
            var vault = GenerateVault(Path.Combine(dir, "cofre-pausa"), 20);
            var selection = Path.Combine(dir, "app", "sel.json");
            var device = new MemoryDeviceStore();
            const string target = "Pasta 1/Sub 0/Nota 5.md";
            var clock = MainView.SessionClock;
            var now = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
            MainView.SessionClock = () => now;
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
                Assert.True(first.Editor.IsVisible);

                // back from the background after 10 minutes: the same session, the note is still open
                now = now.AddMinutes(10);
                await first.ReturnFromBackgroundAsync();
                Assert.True(first.ContinuesSession);
                Assert.True(first.Editor.IsVisible);
                // after 2 hours: the note closes, saved, and the Área de trabalho shows
                var text = first.Editor.Editor.Text + "\nAntes da pausa.\n";
                first.Editor.Editor.Text = text;
                Dispatcher.UIThread.RunJobs();
                now = now.AddHours(2);
                await first.ReturnFromBackgroundAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.False(first.ContinuesSession);
                Assert.False(first.Editor.IsVisible);
                Assert.True(first.Workspace.IsVisible);
                await first.FlushPendingAsync();
                Assert.Equal(text, File.ReadAllText(Path.Combine(vault, "Pasta 1", "Sub 0", "Nota 5.md")));
                window.Close();

                // a new start a day later: the Área de trabalho, not the note
                now = now.AddDays(1);
                var again = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), selection));
                again.Customization.Device = device;
                var window2 = new Window { Width = 412, Height = 900, Content = again };
                window2.Show();
                await again.InitializeAsync();
                Assert.False(again.ContinuesSession);
                Assert.False(again.Editor.IsVisible);
                Assert.True(again.IsReady);
                window2.Close();

                // "Sempre reabrir onde parei" is kept on the device, and every start continues the session
                var prefs = new MainView();
                prefs.Customization.Device = device;
                prefs.Customization.SessionPause = null;
                Assert.Null(prefs.Customization.SessionPause);
                prefs.Customization.SessionPause = TimeSpan.FromHours(2);
                Assert.Equal(TimeSpan.FromHours(2), prefs.Customization.SessionPause);
                prefs.Customization.SessionPause = Urbe.Core.ProductionSession.DefaultPause;
                Assert.Equal(Urbe.Core.ProductionSession.DefaultPause, prefs.Customization.SessionPause);
                return true;
            }
            finally { MainView.SessionClock = clock; try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
