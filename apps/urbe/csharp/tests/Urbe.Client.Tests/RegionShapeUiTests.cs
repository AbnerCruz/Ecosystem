using Avalonia.Threading;
using Urbe.Client;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>v20EntrarShape → handles → Aplicar: the neighbourhood's new shape, and the root houses it covers join the folder.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task RegionShape_GrowsOverARootHouseAndAdoptsItsFile() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                var bairro = city.RegionByPath("Bairro")!;
                var comece = city.Buildings.Single(b => b.Path == "Comece.md");
                int cellsBefore = bairro.W * bairro.H;

                // a tap on the neighbourhood (not on a house) enters its shape
                var free = Enumerable.Range(0, bairro.W * bairro.H).Select(i => (X: bairro.X + i % bairro.W, Y: bairro.Y + i / bairro.W))
                    .First(t => city.RegionAt(t.X, t.Y) == bairro && city.BuildingAt(t.X, t.Y) is null && view.World.Prefs.DecorationAt(t.X, t.Y) is null);
                view.World.Camera.X = (free.X + .5) * 32;
                view.World.Camera.Y = (free.Y + .5) * 32;
                Dispatcher.UIThread.RunJobs();
                // (the tap itself: a headless frame of the whole city can take longer than the 520 ms hold)
                view.World.Tap(free.X, free.Y);
                Dispatcher.UIThread.RunJobs();
                var shape = Assert.IsType<RegionShapeTool>(view.World.Tool);
                Assert.Same(bairro, shape.Region);
                Assert.False(shape.Changed);

                // drag the corner handle towards Comece until the draft covers its lot
                bool west = comece.X + comece.W / 2.0 < bairro.X + bairro.W / 2.0, north = comece.Y + comece.H / 2.0 < bairro.Y + bairro.H / 2.0;
                string handle = (north ? "n" : "s") + (west ? "w" : "e");
                var (hx, hy) = RegionShapeTool.HandleAt(shape.Draft, handle);
                shape.Down((int)Math.Floor(hx - (west ? 0 : .5)), (int)Math.Floor(hy - (north ? 0 : .5)));
                shape.Up(west ? comece.X - 1 : comece.X + comece.W, north ? comece.Y - 1 : comece.Y + comece.H);
                Assert.True(shape.Changed);
                view.World.InvalidateVisual();
                Dispatcher.UIThread.RunJobs();
                Snapshot(window, "native-region-shape.png");
                var draft = shape.Draft;
                var before = (bairro.X, bairro.Y, bairro.W, bairro.H);
                var mask = city.ResizeRegionMask(bairro, draft.X, draft.Y, draft.W, draft.H);
                await view.ConfirmPlacementAsync();
                Assert.Null(view.World.Tool);

                // the neighbourhood grew; Comece's file moved into its folder; the map has both
                Assert.True(bairro.W * bairro.H > cellsBefore, $"handle={handle} before={before} draft={draft} after={(bairro.X, bairro.Y, bairro.W, bairro.H)} mask={mask.Cells.Count} dry={mask.Dry}/{mask.Total} comece={(comece.X, comece.Y)}");
                for (int i = 0; i < 100 && comece.Path != "Bairro/Comece.md"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
                Assert.Equal("Bairro/Comece.md", comece.Path);
                Assert.Equal(bairro.Id, comece.RegionId);
                Assert.True(File.Exists(Path.Combine(vault, "Bairro", "Comece.md")));
                Assert.False(File.Exists(Path.Combine(vault, "Comece.md")));
                await UntilMapSaved(view);
                Assert.Contains("\"Bairro/Comece.md\"", File.ReadAllText(Path.Combine(vault, ".urbe", "mapa.json")));
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
