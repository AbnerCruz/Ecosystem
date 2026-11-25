namespace Lunet.Client.Shell;

/// <summary>The two fixed levels of the shell (ADR 0013): the Home, and the workspace of one game.</summary>
public enum ShellLevel { Home, Workspace }

/// <summary>The views of the same game, one touch apart (owner's wish 1: "fluxo contínuo").</summary>
public enum WorkspaceTab { Code, Game, Art, Map, Sound, Notes }

/// <summary>Support panels: a bottom sheet on a phone, a side column on a wide screen. Never a modal dialog.</summary>
public enum SupportPanel { None, Console, Problems, More }

/// <summary>How the workspace is laid out for the current width (Material 3 canonical layouts: compact vs. expanded).</summary>
public enum ShellLayout { Compact, Expanded }

/// <summary>
/// The navigation state of the Lunet shell, with no UI dependency so it is tested directly. Rules:
/// <list type="bullet">
/// <item>Leaving a game and coming back restores the tab you were on (per game), never resets to a default.</item>
/// <item>Back closes the open support panel first, then returns from the workspace to the Home; on the Home it is not consumed.</item>
/// <item>On an expanded screen, Code and Game are shown side by side, so selecting either keeps both visible.</item>
/// </list>
/// </summary>
public sealed class ShellState
{
    /// <summary>Width (dp) from which the workspace shows code and game side by side (Material 3 "expanded").</summary>
    public const double ExpandedMinWidth = 840;

    private readonly Dictionary<string, WorkspaceTab> _lastTab = new(StringComparer.Ordinal);

    public ShellLevel Level { get; private set; } = ShellLevel.Home;
    public string? Project { get; private set; }
    public WorkspaceTab Tab { get; private set; } = WorkspaceTab.Code;
    public SupportPanel Panel { get; private set; } = SupportPanel.None;
    public ShellLayout Layout { get; private set; } = ShellLayout.Compact;

    /// <summary>Raised after any change, so a view re-renders only what it shows.</summary>
    public event Action? Changed;

    /// <summary>True when the code and the running game are both on screen.</summary>
    public bool CodeAndGameSideBySide => Layout == ShellLayout.Expanded && Tab is WorkspaceTab.Code or WorkspaceTab.Game;

    public void OpenProject(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Project = name;
        Level = ShellLevel.Workspace;
        Tab = _lastTab.GetValueOrDefault(name, WorkspaceTab.Code);
        Panel = SupportPanel.None;
        Changed?.Invoke();
    }

    public void GoHome()
    {
        if (Level == ShellLevel.Home) return;
        Level = ShellLevel.Home;
        Panel = SupportPanel.None;
        Changed?.Invoke();
    }

    public void SelectTab(WorkspaceTab tab)
    {
        if (Level != ShellLevel.Workspace || Project is null) return;
        Tab = tab;
        _lastTab[Project] = tab;
        Changed?.Invoke();
    }

    /// <summary>Opens a support panel, or closes it when it is already the open one.</summary>
    public void TogglePanel(SupportPanel panel)
    {
        Panel = Panel == panel ? SupportPanel.None : panel;
        Changed?.Invoke();
    }

    public void ShowPanel(SupportPanel panel)
    {
        if (Panel == panel) return;
        Panel = panel;
        Changed?.Invoke();
    }

    /// <summary>Updates the layout for a new available width; returns true when it changed.</summary>
    public bool Resize(double width)
    {
        var layout = width >= ExpandedMinWidth ? ShellLayout.Expanded : ShellLayout.Compact;
        if (layout == Layout) return false;
        Layout = layout;
        Changed?.Invoke();
        return true;
    }

    /// <summary>The system Back gesture/key. Returns false when the shell has nothing to close (the app may exit).</summary>
    public bool Back()
    {
        if (Panel != SupportPanel.None) { Panel = SupportPanel.None; Changed?.Invoke(); return true; }
        if (Level == ShellLevel.Workspace) { GoHome(); return true; }
        return false;
    }
}
