using Avalonia.Controls;
using Urbe.Client;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>Owner (UC-53): more decorations and a stronger tool — place many with one tap each, move one.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Decorations_PlaceSeveralInARowAndMoveOne() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                Assert.True(WorldDecorations.Kinds.Count >= 15);
                var kind = WorldDecorations.Find("arbusto")!;
                var city = view.World.City!;
                var spots = new List<(int X, int Y)>();
                for (int y = -40; y < 40 && spots.Count < 3; y++)
                    for (int x = -40; x < 40 && spots.Count < 3; x += 3)
                        if (view.CanDecorate(kind, x, y) && spots.All(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y) > 2)) spots.Add((x, y));
                Assert.Equal(3, spots.Count);

                view.BeginDecoration(kind);
                foreach (var (x, y) in spots.Take(2))
                {
                    var tool = Assert.IsType<DecorationTool>(view.World.Tool);
                    tool.Down(x, y);
                    tool.Up(x, y);
                    await view.ConfirmPlacementAsync();
                    // the tool stays for the next one
                    Assert.Same(tool, view.World.Tool);
                }
                view.EndTool();
                var placed = view.Customization.World.Decorations.Where(d => d.Kind == "arbusto").ToList();
                Assert.Equal(2, placed.Count);
                var file = File.ReadAllText(Path.Combine(vault, ".urbe", "mundo.json"));
                Assert.Contains("arbusto", file);

                // Mover: the same decoration goes to the third spot (no copy left behind)
                var moving = placed[0];
                typeof(MainView).GetField("_movingDecoration", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(view, moving);
                view.BeginDecoration(kind);
                var move = Assert.IsType<DecorationTool>(view.World.Tool);
                move.Down(spots[2].X, spots[2].Y);
                move.Up(spots[2].X, spots[2].Y);
                await view.ConfirmPlacementAsync();
                Assert.Null(view.World.Tool);
                var after = view.Customization.World.Decorations.Where(d => d.Kind == "arbusto").ToList();
                Assert.Equal(2, after.Count);
                Assert.Contains(after, d => d.Id == moving.Id && (d.X, d.Y) == spots[2]);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
