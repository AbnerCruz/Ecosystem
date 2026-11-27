using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>
/// Owner, 27/11 (on the phone): scrolling the code kept opening the keyboard. A finger that drags is a scroll: it never
/// asks for the keyboard, neither on the first touch (editor not focused) nor after Back closed it (editor focused).
/// A tap still does.
/// </summary>
public sealed class ScrollNoKeyboardTests
{
    private static void Drag(Window window, Point from, double dy)
    {
        var finger = window.TouchBegin(from, RawInputModifiers.None);
        for (var i = 1; i <= 10; i++) window.TouchMove(finger, from + new Point(0, dy * i / 10), RawInputModifiers.None);
        window.TouchEnd(finger, from + new Point(0, dy), RawInputModifiers.None);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static void Tap(Window window, Point at, double jitter = 0)
    {
        var finger = window.TouchBegin(at, RawInputModifiers.None);
        window.TouchEnd(finger, at + new Point(jitter, jitter), RawInputModifiers.None);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void DraggingTheCode_NeverOpensTheKeyboard_ButATapDoes()
    {
        var root = Directory.CreateTempSubdirectory("lunet-scroll-").FullName;
        try
        {
            UiSession.Run(() =>
            {
                var shell = new Lunet.Client.LunetShell(root) { HardwareKeyboard = false };
                var asked = new List<bool>();
                shell.SystemKeyboard = asked.Add;
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var editor = shell.WorkspaceView.Code;
                var area = ((CodeSurface)editor.Surface).Editor.TextArea;
                var middle = area.TranslatePoint(new Point(area.Bounds.Width / 2, area.Bounds.Height / 2), window)!.Value;
                var imeRequests = editor.Surface is CodeSurface c ? c.InputClient.KeyboardRequests : 0;

                Drag(window, middle, -250);                                   // first touch is a scroll (not focused yet)
                Assert.False(editor.KeyboardVisible, "rolar o código abriu o teclado");
                Assert.DoesNotContain(true, asked);

                Tap(window, middle, jitter: 3);                               // a tap (a few px of jitter is still a tap)
                Assert.True(editor.KeyboardVisible, "um toque no código devia abrir o teclado");

                shell.Back();                                                 // Back closes the keyboard; the editor keeps focus
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(editor.KeyboardVisible);
                asked.Clear();
                var before = ((CodeSurface)editor.Surface).InputClient.KeyboardRequests;
                Drag(window, middle, 250);                                    // scrolling back while focused
                Assert.DoesNotContain(true, asked);
                Assert.Equal(before, ((CodeSurface)editor.Surface).InputClient.KeyboardRequests);
                Assert.False(editor.KeyboardVisible, "rolar com o editor focado abriu o teclado");
                _ = imeRequests;
                window.Close();
            });
        }
        finally { Directory.Delete(root, true); }
    }
}
