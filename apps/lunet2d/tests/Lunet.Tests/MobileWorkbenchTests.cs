using Lunet.Core;

namespace Lunet.Tests;

public sealed class MobileWorkbenchTests
{
    [Fact]
    public void FreshWorkspaceOpensWithCodeAsThePrimarySurface()
    {
        var layout = new WorkspaceLayout();
        Assert.False(layout.ShowPanel);
        Assert.Equal("problems", layout.PanelTab);
    }

    [Fact]
    public void OpeningDiagnosticsAndReturningToCodePreservesTheSelectedTool()
    {
        var layout = new WorkspaceLayout();
        layout.OpenPanel("console");
        Assert.True(layout.ShowPanel);
        Assert.Equal("console", layout.PanelTab);

        layout.FocusCode();
        Assert.False(layout.ShowPanel);
        Assert.Equal("console", layout.PanelTab);

        layout.OpenPanel("problems");
        Assert.True(layout.ShowPanel);
        Assert.Equal("problems", layout.PanelTab);
    }

    [Fact]
    public void OpeningUnknownPanelCannotInvalidateLayout()
    {
        var layout = new WorkspaceLayout();
        Assert.False(layout.OpenPanel("not-a-panel"));
        Assert.False(layout.ShowPanel);
        Assert.Equal("problems", layout.PanelTab);
    }

    [Fact]
    public void CommandSearchIsCaseAndAccentInsensitiveAndPreservesOrder()
    {
        string[] labels = ["Ações rápidas", "Gerar APK", "Configurações", "Buscar no projeto"];
        Assert.Equal(["Ações rápidas", "Configurações"], WorkbenchCommands.Filter(labels, "acoes"));
        Assert.Equal(["Ações rápidas"], WorkbenchCommands.Filter(labels, "acoes rápidas"));
        Assert.Equal(["Gerar APK"], WorkbenchCommands.Filter(labels, "apk"));
        Assert.Equal(["Configurações"], WorkbenchCommands.Filter(labels, "CONFIGURAÇÕES"));
        Assert.Equal(labels, WorkbenchCommands.Filter(labels, " "));
        Assert.Empty(WorkbenchCommands.Filter(labels, "comando inexistente"));
    }
}
