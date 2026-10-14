using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// Holding a house and dragging it (v21MoveHold → finalizarMovimentoMundo) and the house panel's Mover
/// (escolherDestinoEMover): the house changes lot, and when it changes neighbourhood its file changes folder first.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private static (int X, int Y)? FreeLot(LegacyCity city, LegacyCityBuilding house, Func<LegacyCityRegion?, bool> where)
    {
        for (int r = 4; r < 60; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    int x = house.X + dx, y = house.Y + dy;
                    var (ok, region) = city.ValidMove(house, x, y);
                    if (ok && where(region)) return (x, y);
                }
        return null;
    }

    [Fact]
    public async Task HouseMove_DraggedToTheRootMovesItsFileAndInsideKeepsIt() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                var um = city.Buildings.Single(b => b.Path == "Bairro/Um.md");
                var bairro = city.RegionByPath("Bairro")!;

                // an invalid lot: on top of another house
                var comece = city.Buildings.Single(b => b.Path == "Comece.md");
                Assert.False(await view.MoveHouseAsync(um, comece.X, comece.Y));
                Assert.True(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));

                // inside the same neighbourhood: only the lot changes
                if (FreeLot(city, um, r => r == bairro) is { } inside)
                {
                    Assert.True(await view.MoveHouseAsync(um, inside.X, inside.Y));
                    Assert.Equal(inside, (um.X, um.Y));
                    Assert.Equal("Bairro/Um.md", um.Path);
                }

                // out to the root: the file goes to the root first, then the house
                var root = FreeLot(city, um, r => r is null)!.Value;
                // the real gesture: hold 430 ms, drag, let go
                view.World.Camera.X = (um.X + 1.5) * 32;
                view.World.Camera.Y = (um.Y + 1.5) * 32;
                view.World.Camera.SetZoom(1);
                Dispatcher.UIThread.RunJobs();
                var at = ScreenOf(window, view, um.X + 1.5, um.Y + 1.5);
                window.MouseDown(at, MouseButton.Left);
                for (int i = 0; i < 60 && view.World.Moving is null; i++) { await Task.Delay(15); Dispatcher.UIThread.RunJobs(); }
                if (view.World.Moving is null) view.World.PickUp(um, um.X + 1, um.Y + 1);   // headless timers may not tick
                Assert.Same(um, view.World.Moving);
                view.World.Carry(root.X + 1, root.Y + 1);
                Assert.Equal(root, view.World.MovePos);
                Snapshot(window, "native-house-carried.png");
                view.World.Drop();
                window.MouseUp(at, MouseButton.Left);
                for (int i = 0; i < 100 && um.Path != "Um.md"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("Um.md", um.Path);
                Assert.Equal(root, (um.X, um.Y));
                Assert.Null(um.RegionId);
                Assert.True(File.Exists(Path.Combine(vault, "Um.md")));
                Assert.False(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));
                Assert.Equal("# Um\n", File.ReadAllText(Path.Combine(vault, "Um.md")));
                await UntilMapSaved(view);
                using (var m = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json"))))
                {
                    var n = m.RootElement.GetProperty("notas").GetProperty("Um.md");
                    Assert.Equal(root.X, n.GetProperty("x").GetInt32());
                    Assert.Equal(root.Y, n.GetProperty("y").GetInt32());
                }

                // the panel's Mover: back into Bairro, at a free lot there
                await view.MoveHouseToFolderAsync(um, "Bairro");
                Assert.Equal("Bairro/Um.md", um.Path);
                Assert.Equal(bairro.Id, um.RegionId);
                Assert.True(File.Exists(Path.Combine(vault, "Bairro", "Um.md")));
                Assert.Same(bairro, city.RegionAt(um.X + 1, um.Y + 1));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
