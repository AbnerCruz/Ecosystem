using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>DEC-0045 (UC-41): "+" → Decoração places a decoration with a tap and ✓; a tap on it removes it.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Decoration_IsPlacedOnTheMapKeptInMundoJsonAndRemoved() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                var file = Path.Combine(vault, ".urbe", "mundo.json");
                var mapa = File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json"));

                // "+" → Decoração → Poço
                await PickFromCreateMenu(view, "Decoração", () =>
                {
                    ClickButtonWithText(view.Dialogs, "Poço");
                    return Task.CompletedTask;
                });
                Assert.True(view.ToolActive);

                // a free tile near the camera, then a tap there and Colocar
                var poco = WorldDecorations.Find("poco")!;
                int cx = (int)(view.World.Camera.X / 32), cy = (int)(view.World.Camera.Y / 32);
                var spot = Enumerable.Range(0, 400).Select(i => (X: cx - 10 + i % 20, Y: cy - 10 + i / 20)).First(t => view.CanDecorate(poco, t.X, t.Y));
                var at = ScreenOf(window, view, spot.X + .5, spot.Y + .5);
                window.MouseDown(at, MouseButton.Left);
                window.MouseUp(at, MouseButton.Left);
                Dispatcher.UIThread.RunJobs();
                await view.ConfirmPlacementAsync();
                Assert.False(view.ToolActive);
                var placed = Assert.Single(view.World.Prefs.Decorations);
                Assert.Equal(("poco", spot.X, spot.Y), (placed.Kind, placed.X, placed.Y));
                Assert.Equal(placed, Assert.Single(WorldPrefs.Parse(File.ReadAllText(file), out _).Decorations));
                Assert.Equal(mapa.Length > 0, File.Exists(Path.Combine(vault, ".urbe", "mapa.json")));
                Assert.DoesNotContain("decorac", File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json")), StringComparison.OrdinalIgnoreCase);
                view.World.Camera.SetZoom(2);
                view.World.Camera.X = (spot.X + .5) * 32;
                view.World.Camera.Y = (spot.Y + .5) * 32;
                await Settle(window, view);
                Snapshot(window, "native-decoration-poco.png");

                // the same tile cannot take another decoration
                Assert.False(view.CanDecorate(poco, spot.X, spot.Y));

                // a tap on it: Remover decoração
                var on = ScreenOf(window, view, spot.X + .5, spot.Y + .5);
                window.MouseDown(on, MouseButton.Left);
                window.MouseUp(on, MouseButton.Left);
                for (int i = 0; i < 20 && !view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(view.Dialogs.IsOpen);
                ClickButtonWithText(view.Dialogs, "Remover decoração");
                for (int i = 0; i < 40 && view.World.Prefs.Decorations.Count > 0; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Empty(view.World.Prefs.Decorations);
                Assert.Empty(WorldPrefs.Parse(File.ReadAllText(file), out _).Decorations);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
