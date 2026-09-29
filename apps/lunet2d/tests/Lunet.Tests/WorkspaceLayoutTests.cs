using Lunet.Core;

namespace Lunet.Tests;

public sealed class WorkspaceLayoutTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lunet-layout-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Normalized_ClampsValues_AndDockFollowsOrientationWhenAuto()
    {
        var layout = new WorkspaceLayout { PanelFraction = 5, ExplorerWidthDp = 10, PanelTab = "x", Dock = (PanelDock)99 }.Normalized();
        Assert.Equal(WorkspaceLayout.MaxPanelFraction, layout.PanelFraction);
        Assert.Equal(WorkspaceLayout.MinExplorerWidth, layout.ExplorerWidthDp);
        Assert.Equal("problems", layout.PanelTab);
        Assert.Equal(PanelDock.Auto, layout.Dock);
        Assert.Equal(PanelDock.Right, layout.EffectiveDock(landscape: true));
        Assert.Equal(PanelDock.Bottom, layout.EffectiveDock(landscape: false));
        layout.Dock = PanelDock.Bottom;
        Assert.Equal(PanelDock.Bottom, layout.EffectiveDock(landscape: true));
    }

    [Fact]
    public void Store_PersistsCurrentAndNamedLayouts_ProtectsPresets_AndSurvivesCorruption()
    {
        var path = Path.Combine(_root, "layouts.json");
        var store = new LayoutStore(path);
        store.Load();
        Assert.Equal(["Padrão", "Foco no código", "Depuração"], store.Names);

        store.Current = new WorkspaceLayout { PanelFraction = 0.45, Dock = PanelDock.Right, PanelTab = "console" };
        Assert.True(store.SaveAs("Meu layout"));
        Assert.False(store.SaveAs("padrão"));
        Assert.False(store.SaveAs("   "));
        store.Save();

        var again = new LayoutStore(path);
        again.Load();
        Assert.Equal(0.45, again.Current.PanelFraction);
        Assert.Equal(PanelDock.Right, again.Current.Dock);
        Assert.Contains("Meu layout", again.Names);

        Assert.True(again.Apply("Foco no código"));
        Assert.False(again.Current.ShowPanel);
        Assert.True(again.Apply("meu LAYOUT"));
        Assert.Equal("console", again.Current.PanelTab);
        Assert.False(again.Apply("não existe"));
        Assert.False(again.Delete("Depuração"));
        Assert.True(again.Delete("Meu layout"));
        Assert.DoesNotContain("Meu layout", again.Names);

        File.WriteAllText(path, "{ quebrado");
        var broken = new LayoutStore(path);
        broken.Load();
        Assert.Equal(0.3, broken.Current.PanelFraction);
    }
}
