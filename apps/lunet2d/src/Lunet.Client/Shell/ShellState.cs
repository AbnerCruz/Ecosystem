namespace Lunet.Client.Shell;

/// <summary>The two fixed levels of the shell (ADR 0013): the Home, and the workspace of one game.</summary>
public enum ShellLevel { Home, Workspace }

/// <summary>The views of the same game, one touch apart (owner's wish 1: "fluxo contínuo").</summary>
public enum WorkspaceTab { Code, Game, Art, Map, Sound, Notes }

/// <summary>Support panels: a bottom sheet on a phone, a side column on a wide screen. Never a modal dialog.</summary>
/// <remarks>Explorer is the game's file tree: a drawer on a phone, the side column on a wide screen.</remarks>
public enum SupportPanel { None, Console, Problems, More, Commands, Ai, Inspector, Performance, Docs, Objectives, Explorer }

/// <summary>How the workspace is laid out for the current width (Material 3 canonical layouts: compact vs. expanded).</summary>
public enum ShellLayout { Compact, Expanded }

/// <summary>
/// The navigation state of the Lunet shell, with no UI dependency so it is tested directly. Rules:
/// <list type="bullet">
/// <item>Leaving a game and coming back restores the tab you were on (per game), never resets to a default.</item>
/// <item>Back from Preview returns to Code. In the rest of the workspace it closes support, returns through visited tabs, then goes Home.</item>
/// <item>The expanded editor has room for an idle game view beside Code.</item>
/// </list>
/// </summary>
public sealed class ShellState
{
    /// <summary>Width (dp) from which the workspace shows code and game side by side (Material 3 "expanded").</summary>
    public const double ExpandedMinWidth = 840;

    private readonly Dictionary<string, WorkspaceTab> _lastTab = new(StringComparer.Ordinal);
    // Tabs visited in this game, so Back returns to where the person was before (owner, 25/11: "voltar para o estado anterior").
    private readonly Stack<WorkspaceTab> _tabHistory = new();

    public ShellLevel Level { get; private set; } = ShellLevel.Home;
    public string? Project { get; private set; }
    public WorkspaceTab Tab { get; private set; } = WorkspaceTab.Code;
    public SupportPanel Panel { get; private set; } = SupportPanel.None;
    public ShellLayout Layout { get; private set; } = ShellLayout.Compact;

    /// <summary>Raised after any change, so a view re-renders only what it shows.</summary>
    public event Action? Changed;

    /// <summary>True when the code and the running game are both on screen.</summary>
    public bool CodeAndGameSideBySide => Layout == ShellLayout.Expanded && Tab == WorkspaceTab.Code;

    public void OpenProject(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Project = name;
        Level = ShellLevel.Workspace;
        Tab = _lastTab.GetValueOrDefault(name, WorkspaceTab.Code);
        Panel = SupportPanel.None;
        _tabHistory.Clear();
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
        if (tab != Tab && Tab != WorkspaceTab.Game) _tabHistory.Push(Tab);
        Tab = tab;
        _lastTab[Project] = tab;
        Changed?.Invoke();
    }

    /// <summary>Opens the game over the editor as a single navigation change.</summary>
    public void OpenPreview()
    {
        if (Level != ShellLevel.Workspace) return;
        Tab = WorkspaceTab.Game;
        Panel = SupportPanel.None;
        Changed?.Invoke();
    }

    /// <summary>Leaves the Preview at the code editor, with the Home as the next Back destination.</summary>
    public bool ClosePreview()
    {
        if (Level != ShellLevel.Workspace || Tab != WorkspaceTab.Game) return false;
        Tab = WorkspaceTab.Code;
        Panel = SupportPanel.None;
        _tabHistory.Clear();
        if (Project is not null) _lastTab[Project] = Tab;
        Changed?.Invoke();
        return true;
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
    public bool Back(Func<bool>? canLeaveWorkspace = null)
    {
        if (ClosePreview()) return true;
        if (Panel != SupportPanel.None) { Panel = SupportPanel.None; Changed?.Invoke(); return true; }
        if (Level == ShellLevel.Workspace && _tabHistory.TryPop(out var previous))
        {
            Tab = previous;
            if (Project is not null) _lastTab[Project] = previous;
            Changed?.Invoke();
            return true;
        }
        if (Level == ShellLevel.Workspace)
        {
            if (canLeaveWorkspace?.Invoke() != false) GoHome();
            return true;
        }
        return false;
    }
}
