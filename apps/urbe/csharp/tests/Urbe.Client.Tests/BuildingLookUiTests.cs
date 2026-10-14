using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>DEC-0045 (UC-41): "+" → Construção has a type (its building by extension), and Visual changes a building's
/// look, kept in .urbe/mundo.json by path and carried along when the file is renamed.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Building_TypeChosenOnCreationAndLookChangedByVisual() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                var c = city.Buildings.First();
                (int X, int Y)? lot = null;
                for (int r = 6; r < 40 && lot is null; r++)
                for (int dy = -r; dy <= r && lot is null; dy++)
                for (int dx = -r; dx <= r && lot is null; dx++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && city.CanHouse(c.X + dx, c.Y + dy)) lot = (c.X + dx, c.Y + dy);
                var (lx, ly) = lot!.Value;
                view.World.Camera.X = (lx + 1.5) * 32;
                view.World.Camera.Y = (ly + 1.5) * 32;
                Dispatcher.UIThread.RunJobs();

                // "+" → Construção → Página .html: a name without extension becomes .html, and the building is a hall
                await PickFromCreateMenu(view, "Construção", () =>
                {
                    ClickButtonWithText(view.Dialogs, "Página .html");
                    return Task.CompletedTask;
                });
                Assert.True(view.ToolActive);
                var spot = ScreenOf(window, view, lx + .5, ly + .5);
                window.MouseDown(spot, MouseButton.Left);
                window.MouseUp(spot, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                var confirm = view.ConfirmPlacementAsync();
                Dispatcher.UIThread.RunJobs();
                view.Dialogs.GetVisualDescendants().OfType<TextBox>().Single().Text = "Vitrine";
                ClickButtonWithText(view.Dialogs, "Construir");
                await confirm;
                Assert.True(File.Exists(Path.Combine(vault, "Vitrine.html")));
                var house = city.Buildings.Single(b => b.Path == "Vitrine.html");
                Assert.Equal("hall", WorldView.AutomaticKind(house));

                // Visual: Torre, then Variação 2 com flores
                var visual = view.EditLookAsync(house);
                await Task.Delay(30);
                Dispatcher.UIThread.RunJobs();
                Snapshot(window, "native-building-visual.png");
                ClickButtonWithText(view.Dialogs, "Torre");
                await Task.Delay(30);
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Variação 2 com flores");
                await visual;
                var expected = new BuildingLook("tower", 1, true);
                Assert.Equal(expected, view.World.LookOf(house));
                var file = Path.Combine(vault, ".urbe", "mundo.json");
                Assert.Equal(expected, WorldPrefs.Parse(File.ReadAllText(file), out _).Looks["Vitrine.html"]);
                Assert.DoesNotContain("tower", File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json")), StringComparison.Ordinal);
                Assert.NotSame(view.World.ArtOf(house, new BuildingLook()), view.World.ArtOf(house));
                view.World.Camera.SetZoom(2);
                await Settle(window, view);
                Snapshot(window, "native-building-tower.png");

                // the look follows the file when it is renamed
                await view.RenameNoteAsync("Vitrine.html", "Vitrine nova.html");
                var after = WorldPrefs.Parse(File.ReadAllText(file), out _).Looks;
                Assert.Equal(["Vitrine nova.html"], after.Keys);
                Assert.Equal(expected, view.World.LookOf(house));

                // Automático: back to the hall, nothing kept
                await view.SetLookAsync(house, new BuildingLook());
                Assert.Empty(WorldPrefs.Parse(File.ReadAllText(file), out _).Looks);
                Assert.Same(view.World.ArtOf(house, new BuildingLook()), view.World.ArtOf(house));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
