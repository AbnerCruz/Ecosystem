using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>DEC-0045 (UC-42): a well placed beside a house gets a resident fetching water.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Trades_ANeighbourFetchesWaterAtTheWell() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var city = view.World.City!;
                var house = city.Buildings.First(b => b.IsNote);
                var poco = WorldDecorations.Find("poco")!;
                var spot = Enumerable.Range(0, 200).Select(i => (X: house.X - 6 + i % 15, Y: house.Y + 4 + i / 15))
                    .First(t => view.CanDecorate(poco, t.X, t.Y) && view.CanDecorate(poco, t.X, t.Y + 1));
                await view.Customization.SetWorldAsync(view.World.Prefs with { Decorations = [new WorldDecoration("d1", "poco", spot.X, spot.Y)] });
                view.World.Camera.SetZoom(2.4);
                view.World.Camera.X = (spot.X + .5) * 32;
                view.World.Camera.Y = (spot.Y + 1) * 32;
                await Settle(window, view);

                TownTrades.Worker? w = null;
                for (int i = 0; i < 60 && w?.Phase != TownTrades.Phase.Working; i++)
                {
                    view.World.AdvanceLife(1);
                    w = view.World.Trades.Workers.SingleOrDefault();
                }
                Assert.NotNull(w);
                Assert.Equal(TownTrades.Phase.Working, w!.Phase);
                Assert.Equal("bucket", w.Person.Look.Acc);
                Assert.Contains(w.Person, view.World.Trades.People);
                view.World.InvalidateVisual();
                Dispatcher.UIThread.RunJobs();
                Snapshot(window, "native-trade-well.png");
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
