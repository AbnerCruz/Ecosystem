using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Desktop;

/// <summary>
/// UC-66 on the computer: an icon in the tray (Nota rápida, Abrir o Urbe, Sair) and, on Windows, the global shortcut
/// Ctrl+Alt+N. Both bring the window forward and open the same quick note the phone's widget does; the words go to
/// "Notas rápidas" with the verified write. The shortcut is registered for the window and freed with it.
/// </summary>
internal static class DesktopQuickNote
{
    public const string Shortcut = "Ctrl+Alt+N";
    private const int HotkeyId = 0x5552;   // "UR"
    private const uint WmHotkey = 0x0312, ModAlt = 0x1, ModControl = 0x2, ModNoRepeat = 0x4000, KeyN = 0x4E;

    public static void Install(Window window)
    {
        if (OperatingSystem.IsWindows())
        {
            // the system's dictation (Win+H) writes in the box; the app records nothing itself (NN-016)
            PlatformVoice.Transcribe ??= PlatformVoice.TypedWithDictation("Para falar, aperte Win+H: o ditado do Windows escreve aqui.");
            window.Opened += (_, _) => RegisterShortcut(window);
        }
        AddTrayIcon(window);
    }

    private static void QuickNote(Window window)
    {
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Show();
        window.Activate();
        PlatformVoice.RequestQuickNote();
    }

    private static void AddTrayIcon(Window window)
    {
        if (Application.Current is not { } app) return;
        var note = new NativeMenuItem("Nota rápida" + (OperatingSystem.IsWindows() ? " (" + Shortcut + ")" : ""));
        note.Click += (_, _) => QuickNote(window);
        var open = new NativeMenuItem("Abrir o Urbe");
        open.Click += (_, _) => { if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal; window.Show(); window.Activate(); };
        var quit = new NativeMenuItem("Sair");
        quit.Click += (_, _) => window.Close();
        var tray = new TrayIcon
        {
            ToolTipText = "Urbe — nota rápida",
            Menu = new NativeMenu { note, open, new NativeMenuItemSeparator(), quit }
        };
        try { tray.Icon = new WindowIcon(Icon()); }
        catch (Exception) { /* no icon: the menu still works where the tray shows one */ }
        tray.Clicked += (_, _) => QuickNote(window);
        TrayIcon.SetIcons(app, [tray]);
        window.Closed += (_, _) => tray.IsVisible = false;
    }

    /// <summary>The microphone of the app's icons on its accent, drawn once (the head ships no image of its own).</summary>
    private static Bitmap Icon()
    {
        var mark = new Border
        {
            Width = 32, Height = 32, CornerRadius = new CornerRadius(9),
            Background = UrbeTheme.Brush(UrbeTheme.Accent),
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(UrbeTheme.Icons.Mic), Stroke = Brushes.Black, StrokeThickness = 2.2,
                StrokeLineCap = PenLineCap.Round, Stretch = Stretch.Uniform, Margin = new Thickness(7)
            }
        };
        mark.Measure(new Size(32, 32));
        mark.Arrange(new Rect(0, 0, 32, 32));
        var bitmap = new RenderTargetBitmap(new PixelSize(32, 32));
        bitmap.Render(mark);
        return bitmap;
    }

    private static void RegisterShortcut(Window window)
    {
        if (!OperatingSystem.IsWindows() || window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero) return;
        // another program may own Ctrl+Alt+N: then only the tray offers the quick note
        if (!RegisterHotKey(hwnd, HotkeyId, ModControl | ModAlt | ModNoRepeat, KeyN)) return;
        Win32Properties.CustomWndProcHookCallback hook = (IntPtr _, uint msg, IntPtr wParam, IntPtr _, ref bool handled) =>
        {
            if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
            {
                handled = true;
                Dispatcher.UIThread.Post(() => QuickNote(window));
            }
            return IntPtr.Zero;
        };
        Win32Properties.AddWndProcHookCallback(window, hook);
        window.Closed += (_, _) =>
        {
            Win32Properties.RemoveWndProcHookCallback(window, hook);
            UnregisterHotKey(hwnd, HotkeyId);
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
