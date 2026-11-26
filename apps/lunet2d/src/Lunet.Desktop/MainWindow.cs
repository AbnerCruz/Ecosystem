using Avalonia.Controls;
using Lunet.Client;

namespace Lunet.Desktop;

/// <summary>
/// The desktop head (ADR 0013): a window around the one Lunet shell. Everything the person sees is
/// <see cref="LunetShell"/> from <c>Lunet.Client</c>, the same control the Android head will host.
/// </summary>
public sealed class MainWindow : Window
{
    public MainWindow(string projectsRoot)
    {
        Title = "Lunet";
        Width = 1280; Height = 800;
        MinWidth = 360; MinHeight = 480;
        var ai = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lunet", "ai");
        Shell = new LunetShell(projectsRoot)
        {
            HardwareKeyboard = true,
            LearningProgressPath = Path.Combine(Path.GetDirectoryName(ai)!, "learning", "progress.json"),
            LibraryPath = Path.Combine(Path.GetDirectoryName(ai)!, "Library"),
            GitAccountPath = Path.Combine(Path.GetDirectoryName(ai)!, "git-account.json"),
            StudioWorkspacePath = Path.Combine(Path.GetDirectoryName(ai)!, "StudioWorkspace"),
            SettingsPath = Path.Combine(Path.GetDirectoryName(ai)!, "settings.json"),
            AiHost = new Lunet.Client.Views.AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(ai, "settings.json"),
                new FileSecretVault(Path.Combine(ai, "secrets.json"))), ai),
        };
        Content = Shell;
        Background = Shell.Background;
        Opened += (_, _) => Shell.Report($"Pronto em {Program.SinceLaunch.ElapsedMilliseconds} ms (partida a frio até a janela) · pasta: {projectsRoot}");
    }

    public LunetShell Shell { get; }

    /// <summary>Measurement mode: open a project and start it as a person would (buttons call the same methods).</summary>
    internal void OpenAndRun(string project)
    {
        Shell.OpenProject(project);
        Shell.RunGame();
    }

    internal void Step() => Shell.TickGame(1 / 60.0);

    protected override void OnClosed(EventArgs e)
    {
        Shell.Shutdown();
        base.OnClosed(e);
    }
}
