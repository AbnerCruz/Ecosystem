using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia;
using Avalonia.Headless;
using AvaloniaEdit;
using Avalonia.VisualTree;
using Lunet.Client.Views;

namespace Lunet.Tests;

/// <summary>
/// The code editor and the system keyboard (estudo do editor, 26/11/2026): Enter, cursor, composing words, deleting,
/// moving the caret from the keyboard. Each test drives the editor through <see cref="AndroidImeSimulator"/>, the
/// framework's half of the Android keyboard path. A real keyboard on a phone is still not tested here.
/// </summary>
public sealed class EditorKeyboardTests
{
    private static (Window Window, CodeSurface Code, AndroidImeSimulator Ime) Editor(string text, int caret)
    {
        var code = new CodeSurface();
        var window = new Window { Width = 412, Height = 600, Content = code.View };
        window.Show();
        code.Text = text;
        code.Caret = caret;
        code.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var client = AndroidImeSimulator.ClientOf(code.Editor.TextArea)!;
        return (window, code, new AndroidImeSimulator(window, client));
    }

    [Fact]
    public void TheCodeEditorIsAMultiLineFieldForTheKeyboard_EnterIsALineBreak()
    {
        UiSession.Run(() =>
        {
            var (window, code, ime) = Editor("int a;", 6);
            Assert.Equal("line break", AndroidImeSimulator.EnterAction(code.Editor.TextArea));
            Assert.False(TextInputOptions.GetShowSuggestions(code.Editor.TextArea)); // code: no autocorrect over it
            ime.Enter();
            ime.CommitText("b");
            Assert.Equal("int a;\nb", code.Text);
            Assert.True(code.IsFocused);
            window.Close();
        });
    }

    [Fact]
    public void AvaloniaEditsOwnBridge_ShowsTheBugsThatLunetsReplaces()
    {
        // The regression evidence: the bridge AvaloniaEdit 12.0 ships, driven the same way, gets the caret and the
        // composed word wrong. If a future AvaloniaEdit fixes it this test fails, and Lunet's bridge can be dropped.
        UiSession.Run(() =>
        {
            var editor = new TextEditor { Text = "var x = 1;" };
            var window = new Window { Width = 412, Height = 600, Content = editor };
            window.Show();
            editor.CaretOffset = 4;
            editor.TextArea.Focus();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var ime = new AndroidImeSimulator(window, AndroidImeSimulator.ClientOf(editor.TextArea)!);
            Assert.Equal("Done (closes the keyboard)", AndroidImeSimulator.EnterAction(editor.TextArea));
            Assert.Equal("var x", ime.TextBeforeCursor(10)); // the caret is after "var ", the keyboard reads one more letter
            ime.SetComposingText("y");
            ime.SetComposingText("ye");
            Assert.Equal("var yye = 1;", editor.Text); // should be "var yex = 1;": a letter doubled, the x after the caret deleted
            window.Close();
        });
    }

    [Fact]
    public void TheKeyboardSeesTheCaretWhereItIs()
    {
        UiSession.Run(() =>
        {
            var (window, code, ime) = Editor("using Lunet;\nvar x = 1;\n", 17);
            Assert.Equal("var ", ime.TextBeforeCursor(4));
            Assert.Equal(code.Text, ime.Text); // a short file is seen whole
            window.Close();
        });
    }

    [Fact]
    public void ComposingAWordTypesItOnce_AndReplacingItReplacesOnlyIt()
    {
        UiSession.Run(() =>
        {
            var (window, code, ime) = Editor("var x = 1;", 4);
            foreach (var step in new[] { "p", "po", "pos", "posi" }) ime.SetComposingText(step);
            Assert.Equal("var posix = 1;", code.Text);
            ime.CommitText("posicao"); // the keyboard's suggestion replaces the word being composed
            Assert.Equal("var posicaox = 1;", code.Text);
            Assert.Equal(11, code.Caret);
            window.Close();
        });
    }

    [Fact]
    public void AccentsAndSymbolsFromTheKeyboardArrive()
    {
        UiSession.Run(() =>
        {
            var (window, code, ime) = Editor("Log(\"\");", 5);
            ime.CommitText("ação ✓");
            Assert.Equal("Log(\"ação ✓\");", code.Text);
            window.Close();
        });
    }

    [Fact]
    public void TheKeyboardsBackspaceAndWordDeleteRemoveWhatIsBeforeTheCaret()
    {
        UiSession.Run(() =>
        {
            var (window, code, ime) = Editor("int vida = 3;", 8);
            ime.DeleteSurroundingText(1, 0);
            Assert.Equal("int vid = 3;", code.Text);
            ime.DeleteSurroundingText(3, 0);
            Assert.Equal("int  = 3;", code.Text);
            Assert.Equal(4, code.Caret);
            window.Close();
        });
    }

    [Fact]
    public void TheKeyboardMovesAndSelectsWithTheCaret()
    {
        UiSession.Run(() =>
        {
            var (window, code, ime) = Editor("abc def", 7);
            ime.SetSelection(2, 2);                  // Gboard: drag on the space bar
            Assert.Equal(2, code.Caret);
            ime.SetSelection(4, 7);
            Assert.Equal((4, 7), (code.SelectionStart, code.SelectionEnd));
            ime.CommitText("xyz");
            Assert.Equal("abc xyz", code.Text);
            window.Close();
        });
    }

    [Fact]
    public void ALongFileIsSeenAsAWindowAroundTheCaret_ThatFollowsTheEdits()
    {
        UiSession.Run(() =>
        {
            var lines = string.Join("\n", Enumerable.Range(0, 2000).Select(i => $"// linha {i:0000}"));
            var caret = lines.IndexOf("linha 1000", StringComparison.Ordinal);
            var (window, code, ime) = Editor(lines, caret);
            Assert.True(ime.Text.Length < 4 * CodeInputClient.Reach);
            Assert.Equal("// ", ime.TextBeforeCursor(3));
            for (var i = 0; i < 50; i++) ime.CommitText("x");
            Assert.Contains("// " + new string('x', 50) + "linha 1000", code.Text, StringComparison.Ordinal);
            ime.Enter();
            Assert.Contains(new string('x', 50) + "\n", code.Text, StringComparison.Ordinal);
            code.Caret = 3; // far away (the start): the window moves with the caret
            Assert.Equal("// ", ime.TextBeforeCursor(3));
            window.Close();
        });
    }

    [Fact]
    public void ATapOnTheFocusedEditorAsksForTheKeyboardAgain()
    {
        UiSession.Run(() =>
        {
            var (window, code, _) = Editor("abc", 0);
            var before = code.InputClient.KeyboardRequests;
            var point = code.Editor.TextArea.TranslatePoint(new Avalonia.Point(20, 10), window)!.Value;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.True(code.InputClient.KeyboardRequests > before);
            window.Close();
        });
    }

    [Fact]
    public void LoadingAnotherFileStartsTheKeyboardAfresh()
    {
        UiSession.Run(() =>
        {
            var (window, code, _) = Editor("abc", 0);
            var resets = 0;
            code.InputClient.ResetRequested += (_, _) => resets++;
            code.Text = "outro arquivo";
            Assert.Equal(1, resets);
            window.Close();
        });
    }

    // ---------------- the screens around the fields (focus never dropped, the keyboard is asked for) ----------------

    private sealed class Echo : AgentRuntime.IModelProvider
    {
        public string ProviderId => "chat-completions";
        public ValueTask<AgentRuntime.ModelResponse> CompleteAsync(AgentRuntime.ModelRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AgentRuntime.ModelResponse([new AgentRuntime.TextBlock("ok")], AgentRuntime.StopReason.EndTurn,
                new AgentRuntime.Usage(1, 1, AgentRuntime.Money.Zero("USD"))));
    }

    private sealed class NoVault : Lunet.Ai.ISecretVault
    {
        public string? Get(string name) => null;
        public void Set(string name, string value) { }
        public void Delete(string name) { }
    }

    private static (Window Window, Lunet.Client.LunetShell Shell, List<bool> Keyboard) Shell(string root)
    {
        var ai = Path.Combine(root, ".ai");
        var shell = new Lunet.Client.LunetShell(root)
        {
            AiHost = new AiHost(new Lunet.Ai.LunetAiSettingsStore(Path.Combine(ai, "settings.json"), new NoVault()), ai, new Echo()),
        };
        var keyboard = new List<bool>();
        shell.SystemKeyboard = keyboard.Add;
        var window = new Window { Width = 412, Height = 915, Content = shell };
        window.Show();
        shell.CreateProject();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return (window, shell, keyboard);
    }

    private static string Root() => Path.Combine(Path.GetTempPath(), "lunet-keyboard-" + Guid.NewGuid().ToString("N"));

    private static void Tap(Window window, Control target)
    {
        // A finger, not the mouse: the touch path is the phone's (focus on lift, the keyboard request).
        var point = target.TranslatePoint(new Point(Math.Min(20, target.Bounds.Width / 2), Math.Min(10, target.Bounds.Height / 2)), window)!.Value;
        var finger = window.TouchBegin(point, RawInputModifiers.None);
        window.TouchEnd(finger, point, RawInputModifiers.None);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public void TheFirstTouchOnTheCode_BringsTheCodeBarWithoutMovingTheEditor()
    {
        var root = Root();
        try
        {
            UiSession.Run(() =>
            {
                var (window, shell, _) = Shell(root);
                var editor = shell.WorkspaceView.Code;
                var area = ((CodeSurface)editor.Surface).Editor.TextArea;
                var detached = 0;
                area.DetachedFromVisualTree += (_, _) => detached++;
                editor.Surface.Focus();                       // the code bar appears (Engaged → ShowKeyboard)
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(editor.KeyboardVisible);
                editor.ShowFind(); editor.HideFind(); editor.ToggleFileMenu(); editor.ToggleFileMenu();
                Assert.Equal(0, detached);                    // never out of the tree: focus and keyboard stay
                Assert.True(editor.Surface.IsFocused);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void AGameStateChangeWhileWriting_KeepsTheFocusOnTheCode()
    {
        var root = Root();
        try
        {
            UiSession.Run(() =>
            {
                var (window, shell, _) = Shell(root);
                var editor = shell.WorkspaceView.Code;
                editor.Surface.Focus();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                shell.WorkspaceView.Rebuild();                // what Game.StateChanged does
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(editor.Surface.IsFocused);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void ATapOnAFieldAsksForTheKeyboard_ButNotTheKeysOfTheCodeBar()
    {
        var root = Root();
        try
        {
            UiSession.Run(() =>
            {
                var (window, shell, keyboard) = Shell(root);
                var editor = shell.WorkspaceView.Code;
                var area = ((CodeSurface)editor.Surface).Editor.TextArea;
                Tap(window, area);
                Assert.True(area.IsFocused);
                Assert.Contains(true, keyboard);              // explicit request: Android honours it even after Back
                keyboard.Clear();
                var hide = window.GetVisualDescendants().OfType<Control>().First(c => c.Name == "HideKeyboard");
                Tap(window, hide);                            // ⌄ closes the keyboard and nothing reopens it
                Assert.DoesNotContain(true, keyboard);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void TheAiMessage_KeepsItsSheetFocusAndKeyboard_AndEnterIsALineBreak()
    {
        var root = Root();
        try
        {
            UiSession.Run(() =>
            {
                var (window, shell, keyboard) = Shell(root);
                shell.State.ShowPanel(Lunet.Client.Shell.SupportPanel.Ai);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var ai = shell.WorkspaceView.Support.Ai;
                Assert.Equal("line break", AndroidImeSimulator.EnterAction(ai.Input));
                Tap(window, ai.Input);
                Assert.True(ai.Input.IsFocused);
                Assert.Contains(true, keyboard);
                shell.SetKeyboard(true);                      // the keyboard opened: the sheet with the field stays
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(ai.Input.IsEffectivelyVisible);
                Assert.True(ai.Input.IsFocused);
                var fazer = window.GetVisualDescendants().OfType<Button>().First(b => b.Content is StackPanel sp
                    && sp.Children.OfType<TextBlock>().Any(t => t.Text == "Fazer") || (b.Content as TextBlock)?.Text == "Fazer");
                Tap(window, fazer);                           // choosing the mode does not take the focus
                Assert.Equal(Lunet.Ai.LunetAiMode.Do, ai.Mode);
                Assert.True(ai.Input.IsFocused);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void EveryMultiLineFieldOfTheApp_HasEnterAsALineBreak()
    {
        var root = Root();
        try
        {
            UiSession.Run(() =>
            {
                var (window, shell, _) = Shell(root);
                shell.State.SelectTab(Lunet.Client.Shell.WorkspaceTab.Notes);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var notes = (NotesSurface)shell.WorkspaceView.Notes.Surface;
                Assert.Equal("line break", AndroidImeSimulator.EnterAction(notes.Box));
                var single = new TextBox();
                window.Content = new StackPanel { Children = { single } };
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal("Done (closes the keyboard)", AndroidImeSimulator.EnterAction(single)); // one-line fields keep Done
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
