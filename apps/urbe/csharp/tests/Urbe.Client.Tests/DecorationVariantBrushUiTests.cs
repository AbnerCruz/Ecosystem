using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>Owner (UC-53): variations of each decoration, Girar/Espelhar and the brush that paints along a drag.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Decoration_VariantMenuAndBrush() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var file = Path.Combine(vault, ".urbe", "mundo.json");
                var flores = WorldDecorations.Find("flores")!;
                int cx = (int)(view.World.Camera.X / 32), cy = (int)(view.World.Camera.Y / 32);
                var spot = Enumerable.Range(0, 400).Select(i => (X: cx - 10 + i % 20, Y: cy - 10 + i / 20)).First(t => view.CanDecorate(flores, t.X, t.Y));

                // one flower bed, placed with the default variation
                view.BeginDecoration(flores);
                var tool = Assert.IsType<DecorationTool>(view.World.Tool);
                tool.Down(spot.X, spot.Y);
                tool.Up(spot.X, spot.Y);
                await view.ConfirmPlacementAsync();
                view.EndTool();
                var bed = Assert.Single(view.Customization.World.Decorations);
                Assert.Equal(0, bed.Variant);
                Assert.DoesNotContain("variacao", File.ReadAllText(file));

                // a tap on it: Variação → Rosa
                var on = ScreenOf(window, view, spot.X + .5, spot.Y + .5);
                window.MouseDown(on, MouseButton.Left);
                window.MouseUp(on, MouseButton.Left);
                for (int i = 0; i < 20 && !view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.True(view.Dialogs.IsOpen);
                ClickButtonWithText(view.Dialogs, "Variação");
                for (int i = 0; i < 20 && !view.Dialogs.IsOpen; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                ClickButtonWithText(view.Dialogs, "Rosa");
                for (int i = 0; i < 40 && view.Customization.World.Decorations[0].Variant != 1; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal(1, view.Customization.World.Decorations[0].Variant);
                Assert.Equal(1, Assert.Single(WorldPrefs.Parse(File.ReadAllText(file), out _).Decorations).Variant);
                // the next ones are placed with the last chosen variation
                Assert.Equal(1, view.DecorationLook(flores).Variant);
                view.BeginDecoration(flores);
                Assert.Equal(1, Assert.IsType<DecorationTool>(view.World.Tool).Variant);
                Assert.Contains("Rosa", view.ToolDockText);
                view.CycleToolVariant();
                Assert.Equal(2, Assert.IsType<DecorationTool>(view.World.Tool).Variant);
                Assert.Contains("Branco", view.ToolDockText);
                view.EndTool();

                // the brush: a fence line along a row, one per free tile, with the single placement's rule
                var fence = WorldDecorations.Find("cerca")!;
                (int X, int Y) start = default;
                bool found = false;
                for (int y = cy - 20; y < cy + 20 && !found; y++)
                    for (int x = cx - 20; x < cx + 20 && !found; x++)
                        if (Enumerable.Range(0, 6).Count(i => view.CanDecorate(fence, x + i, y)) >= 4 && view.CanDecorate(fence, x, y)) { start = (x, y); found = true; }
                Assert.True(found);
                var expected = Enumerable.Range(0, 6).Select(i => (X: start.X + i, start.Y)).Where(t => view.CanDecorate(fence, t.X, t.Y)).ToList();
                int before = view.Customization.World.Decorations.Count;

                view.BeginDecoration(fence);
                view.CycleToolVariant();                     // Branca
                view.ToggleBrush();
                var brush = Assert.IsType<DecorationTool>(view.World.Tool);
                Assert.True(brush.Brush);
                Assert.Contains("arraste", view.ToolDockText);
                brush.Down(start.X, start.Y);
                brush.Move(start.X + 3, start.Y);
                Assert.Equal(expected.Count(t => t.X <= start.X + 3), brush.Planned().Count());
                brush.Up(start.X + 5, start.Y);
                for (int i = 0; i < 40 && view.Customization.World.Decorations.Count == before; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                var fences = view.Customization.World.Decorations.Where(d => d.Kind == "cerca").ToList();
                Assert.Equal(expected, fences.Select(d => (d.X, d.Y)));
                Assert.All(fences, d => Assert.Equal((1, false), (d.Variant, d.Turned)));   // east-west stroke: not turned
                Assert.Same(brush, view.World.Tool);                                         // the brush stays for more strokes
                await Settle(window, view);

                // the same stroke again adds nothing (all taken)
                brush.Down(start.X, start.Y);
                brush.Up(start.X + 5, start.Y);
                for (int i = 0; i < 10; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal(fences.Count, view.Customization.World.Decorations.Count(d => d.Kind == "cerca"));
                view.EndTool();
                var saved = WorldPrefs.Parse(File.ReadAllText(file), out _);
                Assert.Equal(view.Customization.World.Decorations, saved.Decorations);

                // Girar on a fence turns it a quarter, kept in mundo.json
                await view.ChangeDecorationAsync(fences[0] with { Turned = true });
                Assert.True(WorldPrefs.Parse(File.ReadAllText(file), out _).Decorations.Single(d => d.Id == fences[0].Id).Turned);
                Assert.Contains("\"girada\": true", File.ReadAllText(file));
                view.World.Camera.SetZoom(2);
                view.World.Camera.X = (start.X + 3) * 32;
                view.World.Camera.Y = (start.Y + .5) * 32;
                await Settle(window, view);
                Snapshot(window, "native-decoration-brush-fence.png");
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
