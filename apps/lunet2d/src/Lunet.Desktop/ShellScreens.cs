using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Lunet.Client;
using Lunet.Client.Shell;

namespace Lunet.Desktop;

/// <summary>
/// <c>Lunet --screens [pasta]</c>: renders the real shell offscreen (Avalonia headless + Skia) at a phone width and a
/// wide width, through the same calls a person's taps make, and saves PNGs. Every interface PR attaches these
/// (study, Onda 0: "nenhuma tela entra sem captura"). Headless captures are not a device test.
/// </summary>
public static class ShellScreens
{
    public static readonly (string Name, int Width, int Height)[] Sizes = [("celular", 412, 915), ("largo", 1280, 800)];

    public static int Run(string output)
    {
        AppBuilder.Configure<ScreensApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
        Directory.CreateDirectory(output);
        var written = new List<string>();
        foreach (var (size, w, h) in Sizes)
        {
            var root = Path.Combine(Path.GetTempPath(), "lunet-screens-" + Guid.NewGuid().ToString("N"));
            try
            {
                var window = new MainWindow(root) { Width = w, Height = h };
                window.Show();
                var shell = window.Shell;
                shell.HardwareKeyboard = size != "celular"; // the phone captures show the Lunet keyboard
                Shot(window, output, size, "1-home-vazia", written);
                shell.CreateProject();
                shell.GoHome();
                Shot(window, output, size, "2-home", written);
                shell.OpenProject(shell.Workspace.Projects()[0]);
                Shot(window, output, size, "3-codigo", written);
                shell.WorkspaceView.Code.ShowKeyboard();
                Shot(window, output, size, "3b-teclas", written);
                if (shell.WorkspaceView.Code.Keyboard is { } keyboard)
                {
                    keyboard.Show(Lunet.Client.Views.KeyboardTab.Shortcuts);
                    Shot(window, output, size, "3c-atalhos", written);
                    keyboard.Catalog.RecordUse(keyboard.Catalog.All.First(m => m.Type == "SpriteBatch" && m.Name == "Draw"));
                    keyboard.Show(Lunet.Client.Views.KeyboardTab.Methods);
                    keyboard.ToggleNode("Lunet.Input");
                    keyboard.ToggleNode("Lunet.Input", "InputState");
                    Shot(window, output, size, "3d-metodos", written);
                    keyboard.Show(Lunet.Client.Views.KeyboardTab.Keys);
                }
                shell.WorkspaceView.Code.HideKeyboard();
                shell.RunGame();
                for (int i = 0; i < 20; i++) shell.TickGame(1 / 60.0);
                Shot(window, output, size, "4-jogo", written);
                shell.Game.TogglePause();
                shell.State.ShowPanel(SupportPanel.Console);
                Shot(window, output, size, "5-console", written);
                shell.StopGame();
                shell.WorkspaceView.Code.Surface.Text = "public sealed class Quebrado : Lunet.Game { void X() { int a = ; } }";
                shell.RunGame();
                Shot(window, output, size, "6-problemas", written);
                shell.State.ShowPanel(SupportPanel.None);
                shell.State.SelectTab(WorkspaceTab.Notes);
                Shot(window, output, size, "7-notas", written);
                shell.State.SelectTab(WorkspaceTab.Art);
                Shot(window, output, size, "8-arte", written);
                window.Close();
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
        foreach (var f in written) Console.WriteLine(f);
        return 0;
    }

    private static void Shot(Window window, string output, string size, string name, List<string> written)
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var file = Path.Combine(output, $"{size}-{name}.png");
#pragma warning disable CS0618 // plain PNG
        window.CaptureRenderedFrame()?.Save(file);
#pragma warning restore CS0618
        written.Add(file);
    }

    private sealed class ScreensApp : LunetApp;
}
