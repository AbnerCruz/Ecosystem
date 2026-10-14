using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>DEC-0045 (UC-41, first slice): the wall of a root neighbourhood, the outlines, and the root's name close up.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public void WorldFile_IsNeverWrittenOverANewerOne()
    {
        var newer = new VaultFile(WorldPrefs.FilePath, System.Text.Encoding.UTF8.GetBytes("{\"versao\":2}"));
        Assert.Throws<InvalidOperationException>(() => IUrbeVaultStorage.PlanWorldWrite([newer], WorldPrefs.Default.Text()));
        var (path, bytes) = IUrbeVaultStorage.PlanWorldWrite([], WorldPrefs.Default.Text());
        Assert.Equal(WorldPrefs.FilePath, path);
        Assert.Equal(WorldPrefs.Default.Text(), System.Text.Encoding.UTF8.GetString(bytes));
        Assert.Throws<InvalidDataException>(() => IUrbeVaultStorage.PlanWorldWrite([], "{\"versao\":9}"));
    }

    [Fact]
    public async Task World_WallsOutlinesAndTheRootNameCloseUp() =>
        Assert.True(await Session.Dispatch(WorldWallsScenario, CancellationToken.None));

    private static async Task<bool> WorldWallsScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-walls-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Bairro", "Sub"));
        File.WriteAllText(Path.Combine(vault, "Bairro", "Um.md"), "# Um\n\n[[Dois]]\n");
        File.WriteAllText(Path.Combine(vault, "Bairro", "Sub", "Dois.md"), "# Dois\n");
        async Task Settled() { for (int i = 0; i < 30; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); } }
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            Assert.Equal(WorldPrefs.Default, view.World.Prefs);
            Snapshot(window, "native-world-walls.png");

            // close up on a house of Bairro: its name at the top, whatever sub-neighbourhood the camera is in
            var house = view.World.City!.Buildings.Single(b => b.Path == "Bairro/Sub/Dois.md");
            view.World.CenterOn(house);
            view.World.Camera.SetZoom(2.4);
            await Settle(window, view);
            Assert.Equal("Bairro", view.World.RootDistrictShown);
            Snapshot(window, "native-world-root-name.png");
            view.World.Camera.SetZoom(1);
            await Settle(window, view);
            Assert.Null(view.World.RootDistrictShown);

            // Personalização → Cidade: Muralha off and Contornos ocultos, written to .urbe/mundo.json
            view.Customize.Open("cidade");
            Dispatcher.UIThread.RunJobs();
            var wall = view.Customize.GetLogicalDescendants().OfType<Border>()
                .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Muralha");
            await Settle(window, view);
            var at = wall.TranslatePoint(new Avalonia.Point(wall.Bounds.Width / 2, wall.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left);
            window.MouseUp(at, Avalonia.Input.MouseButton.Left);
            await Settled();
            var hide = view.Customize.GetLogicalDescendants().OfType<Button>()
                .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Contornos Ocultos");
            hide.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Settled();
            var file = Path.Combine(vault, ".urbe", "mundo.json");
            Assert.Equal(new WorldPrefs(false, WorldPrefs.Hidden), WorldPrefs.Parse(File.ReadAllText(file), out _));
            Assert.Equal(new WorldPrefs(false, WorldPrefs.Hidden), view.World.Prefs);
            // mapa.json keeps only the 1.8.4's own fields
            Assert.DoesNotContain("muralha", File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json")), StringComparison.OrdinalIgnoreCase);
            window.Close();
            return true;
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
