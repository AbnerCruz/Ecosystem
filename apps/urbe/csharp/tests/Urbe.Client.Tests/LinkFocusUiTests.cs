using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>UC-70: a tap on a building darkens the city around its links (cita, citada por, mútua).</summary>
public sealed partial class HeadlessUiTests
{
    private static readonly CityNote[] FocusNotes =
    [
        new("Ideias/Jardim.md", "# Jardim\nVer [[Sementes]] e [[Regar]]."),
        new("Ideias/Sementes.md", "# Sementes\nVolta ao [[Jardim]]."),
        new("Ideias/Regar.md", "# Regar\nToda manhã."),
        new("Ideias/Estufa.md", "# Estufa\nPerto do [[Jardim]]."),
        new("Ideias/Vasos.md", "# Vasos\nPara a [[Estufa]]."),
        new("Ideias/Solta.md", "# Solta\nSem links."),
        new("Diário/Hoje.md", "# Hoje\nNada ligado.")
    ];

    private static LegacyCityBuilding FocusHouse(MainView view, string name) => view.World.City!.Buildings.Single(b => b.Name == name);

    private static void TapHouse(Window window, MainView view, LegacyCityBuilding b)
    {
        var centre = view.World.Camera.WorldToScreen((b.X + b.W / 2.0) * 32, (b.Y + b.H / 2.0) * 32);
        var p = view.World.TranslatePoint(centre, window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task<MainView> FocusCity(Window window)
    {
        var view = new MainView();
        window.Content = view;
        window.Show();
        await view.LoadAsync(FocusNotes);
        await Settle(window, view);
        // no holds here: a slow headless frame between press and release could otherwise turn a tap into a hold
        view.World.HoldGestures = false;
        // the neighbourhood framed in the upper part of the screen, above the card
        var j = FocusHouse(view, "Jardim");
        view.World.Camera.SetZoom(.9);
        view.World.Camera.X = (j.X + 1.5) * 32;
        view.World.Camera.Y = (j.Y + 4.5) * 32;
        view.World.InvalidateVisual();
        await Settle(window, view);
        return view;
    }

    private static Avalonia.Media.Color PixelAt(Bitmap frame, Point p)
    {
        var pixels = new byte[4];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { frame.CopyPixels(new PixelRect((int)p.X, (int)p.Y, 1, 1), handle.AddrOfPinnedObject(), 4, 4); }
        finally { handle.Free(); }
        bool rgba = frame.Format == Avalonia.Platform.PixelFormats.Rgba8888;
        return Avalonia.Media.Color.FromArgb(pixels[3], rgba ? pixels[0] : pixels[2], pixels[1], rgba ? pixels[2] : pixels[0]);
    }

    private static double Luma(Bitmap frame, Rect area)
    {
        double sum = 0;
        int n = 0;
        for (double y = area.Y; y < area.Bottom; y += 6)
        for (double x = area.X; x < area.Right; x += 6)
        {
            var c = PixelAt(frame, new Point(x, y));
            sum += .3 * c.R + .59 * c.G + .11 * c.B;
            n++;
        }
        return sum / Math.Max(1, n);
    }

    private static Point BadgeCentre(MainView view, Window window, LegacyCityBuilding b)
    {
        double z = view.World.Camera.Zoom, s = 32 * z / LegacyWorld.TilePixels;
        var p = view.World.Camera.WorldToScreen((b.X + b.W) * 32, b.Y * 32);
        double h = 56 * s * (b.H / 3.0), top = p.Y + b.H * 32 * z - h;
        // below the arrow strokes, inside the badge's disc
        double r = Math.Clamp(9 * z, 8, 13);
        return view.World.TranslatePoint(new Point(p.X - 4 * z, top + 6 * z + r * .8), window)!.Value;
    }

    private static void AssertNear(string hex, Avalonia.Media.Color c)
    {
        var want = Avalonia.Media.Color.Parse(hex);
        Assert.True(Math.Abs(want.R - c.R) + Math.Abs(want.G - c.G) + Math.Abs(want.B - c.B) < 40, $"{c} != {hex}");
    }

    [Fact]
    public async Task Focus_TapOnABuildingShowsOnlyItsLinksByGroup() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var window = new Window { Width = 412, Height = 900 };
        var view = await FocusCity(window);
        var before = window.CaptureRenderedFrame()!;
        Snapshot(window, "focus-0-cidade.png");

        var jardim = FocusHouse(view, "Jardim");
        TapHouse(window, view, jardim);
        await Settle(window, view);
        var linked = view.World.Linked;
        Assert.NotNull(linked);
        Assert.Same(jardim, linked!.Focus);
        Assert.Same(jardim, view.World.Selected);
        Assert.Equal(["Regar"], linked.Outgoing.Select(b => b.Name));
        Assert.Equal(["Estufa"], linked.Incoming.Select(b => b.Name));
        Assert.Equal(["Sementes"], linked.Mutual.Select(b => b.Name));
        Assert.Null(linked.GroupOf(FocusHouse(view, "Vasos")));
        Assert.Equal(1, view.World.FocusAmount, 3);
        // the card instead of the house card: name, legend (words and arrows, not colour only), Abrir/Detalhes
        Assert.True(view.FocusBar.IsVisible);
        Assert.Equal("Jardim", view.FocusBar.Title);
        Assert.Equal(["cita 1", "citada por 1", "mútua 1"], view.FocusBar.Legend);
        Assert.Equal("Detalhes", view.FocusBar.SecondAction);
        Snapshot(window, "focus-1-jardim.png");

        // pixels: an unlinked building is darkened; each linked one wears its group's badge
        var after = window.CaptureRenderedFrame()!;
        var solta = FocusHouse(view, "Solta");
        var sp = view.World.TranslatePoint(view.World.Camera.WorldToScreen(solta.X * 32, solta.Y * 32), window)!.Value;
        var soltaArea = new Rect(sp.X, sp.Y, 3 * 32 * view.World.Camera.Zoom, 3 * 32 * view.World.Camera.Zoom).Intersect(new Rect(0, 0, 412, 600));
        if (soltaArea.Width > 6 && soltaArea.Height > 6) Assert.True(Luma(after, soltaArea) < Luma(before, soltaArea) * .5, "the rest of the city is darkened");
        AssertNear(WorldView.FocusColors.Mutual, PixelAt(after, BadgeCentre(view, window, FocusHouse(view, "Sementes"))));
        AssertNear(WorldView.FocusColors.Outgoing, PixelAt(after, BadgeCentre(view, window, FocusHouse(view, "Regar"))));
        AssertNear(WorldView.FocusColors.Incoming, PixelAt(after, BadgeCentre(view, window, FocusHouse(view, "Estufa"))));
        // the connecting streets are part of the focus
        Assert.NotEmpty(linked.Roads);
        Assert.Contains(linked.Roads.Values, g => g == LinkGroup.Mutual);

        // a linked building: its name and "Abrir"; a second tap makes it the focus
        var sementes = FocusHouse(view, "Sementes");
        TapHouse(window, view, sementes);
        await Settle(window, view);
        Assert.Same(sementes, view.World.Peeked);
        Assert.Same(jardim, view.World.Linked!.Focus);
        Assert.Equal("Sementes", view.FocusBar.Title);
        Assert.Equal("Ligação mútua com Jardim", view.FocusBar.Subtitle);
        Assert.Equal("Focar", view.FocusBar.SecondAction);
        Snapshot(window, "focus-2-sementes.png");
        ClickButtonWithText(view.FocusBar, "Abrir");
        Assert.True(view.Editor.IsVisible);
        Assert.Equal("Ideias/Sementes.md", view.Editor.Current?.Path);
        view.Editor.IsVisible = false;
        Dispatcher.UIThread.RunJobs();
        TapHouse(window, view, sementes);
        Assert.Same(sementes, view.World.Linked!.Focus);
        Assert.Null(view.World.Peeked);
        Assert.Equal(["cita 0", "citada por 0", "mútua 1"], view.FocusBar.Legend);

        // a second tap on the focus: the 1.8.4 house card (Detalhes)
        TapHouse(window, view, sementes);
        Assert.False(view.FocusBar.IsVisible);
        Assert.NotNull(view.World.Linked);

        // a tap on empty ground leaves the focus and the city comes back
        var (gx, gy) = (sementes.X - 6, sementes.Y + 1);
        for (; view.World.City!.BuildingAt(gx, gy) is not null || view.World.Prefs.DecorationAt(gx, gy) is not null; gx--) { }
        var empty = view.World.TranslatePoint(view.World.Camera.WorldToScreen((gx + .5) * 32, (gy + .5) * 32), window)!.Value;
        window.MouseDown(empty, MouseButton.Left);
        window.MouseUp(empty, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(view.World.Linked);
        Assert.Null(view.World.Selected);
        Assert.False(view.FocusBar.IsVisible);
        Assert.Equal(0, view.World.FocusAmount, 3);
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Focus_LeavesWithCloseBackAndEscape() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var window = new Window { Width = 412, Height = 900 };
        var view = await FocusCity(window);
        var jardim = FocusHouse(view, "Jardim");
        TapHouse(window, view, jardim);
        Assert.NotNull(view.World.Linked);
        view.FocusBar.GetLogicalDescendantsOfType<Button>(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Sair do destaque")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Null(view.World.Linked);
        Assert.False(view.FocusBar.IsVisible);

        TapHouse(window, view, jardim);
        Assert.True(view.LeaveLinkFocus());   // Back
        Assert.Null(view.World.Linked);
        Assert.False(view.LeaveLinkFocus());  // nothing to leave: Back goes on

        TapHouse(window, view, jardim);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Null(view.World.Linked);

        // the "+" steps aside while the focus is on
        TapHouse(window, view, jardim);
        Assert.DoesNotContain(view.GetLogicalDescendantsOfType<Button>(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Criar", all: true), b => b.IsEffectivelyVisible);
        view.LeaveLinkFocus();

        // a note without links: the city darkens around it alone and the card says so
        TapHouse(window, view, FocusHouse(view, "Solta"));
        await Settle(window, view);
        Assert.Equal("Nenhuma ligação ainda", view.FocusBar.Subtitle);
        Assert.Equal(["cita 0", "citada por 0", "mútua 0"], view.FocusBar.Legend);
        Assert.Empty(view.World.Linked!.Roads);
        Snapshot(window, "focus-3-solta.png");
        view.LeaveLinkFocus();
        return true;
    }, CancellationToken.None));

    [Fact]
    public async Task Focus_DarkensIn220MsAndInstantlyWithReducedMotion() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var window = new Window { Width = 412, Height = 900 };
        var view = await FocusCity(window);
        var jardim = FocusHouse(view, "Jardim");
        UrbeTokens.Motion.Reduced = false;
        try
        {
            view.World.FocusLinks(jardim);
            Assert.InRange(view.World.FocusAmount, 0, .5);
            await Task.Delay(UrbeTokens.Motion.Normal + TimeSpan.FromMilliseconds(40));
            Assert.Equal(1, view.World.FocusAmount, 3);
            view.World.ClearLinkFocus();
            Assert.InRange(view.World.FocusAmount, .5, 1);
            await Task.Delay(UrbeTokens.Motion.Normal + TimeSpan.FromMilliseconds(40));
            Assert.Equal(0, view.World.FocusAmount, 3);
        }
        finally { UrbeTokens.Motion.Reduced = true; }
        view.World.FocusLinks(jardim);
        Assert.Equal(1, view.World.FocusAmount, 3);
        view.World.ClearLinkFocus();
        Assert.Equal(0, view.World.FocusAmount, 3);
        // the house card still opens straight from Select (Explorer, "Mostrar na cidade", tests)
        view.World.Select(jardim);
        Assert.Null(view.World.Linked);
        return true;
    }, CancellationToken.None));
}

internal static class FocusTestExtensions
{
    public static T GetLogicalDescendantsOfType<T>(this Control root, Func<T, bool> match) where T : Control =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<T>().First(match);

    public static IEnumerable<T> GetLogicalDescendantsOfType<T>(this Control root, Func<T, bool> match, bool all) where T : Control =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<T>().Where(match);
}
