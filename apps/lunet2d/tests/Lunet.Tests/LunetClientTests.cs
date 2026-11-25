using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using Lunet.Client;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Client.Views;
using Lunet.Host;

namespace Lunet.Tests;

/// <summary>ADR 0013 / DEC-0068: the one Avalonia shell (Lunet.Client) — navigation, game session, tokens and the real UI headless.</summary>
public sealed class LunetClientTests
{
    private const string RedGame = """
        using Lunet; using Lunet.Graphics;
        public sealed class G : Game
        {
            public int Frames;
            protected override void Update(GameTime time) => Frames++;
            protected override void Draw(GameTime time) => GraphicsDevice.Clear(new Color(255, 0, 0));
        }
        """;

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "lunet-client-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ShellState_BackClosesPanelThenLeavesWorkspaceThenIsNotConsumed()
    {
        var s = new ShellState();
        s.OpenProject("A");
        s.ShowPanel(SupportPanel.Console);
        Assert.True(s.Back());
        Assert.Equal(SupportPanel.None, s.Panel);
        Assert.Equal(ShellLevel.Workspace, s.Level);
        Assert.True(s.Back());
        Assert.Equal(ShellLevel.Home, s.Level);
        Assert.False(s.Back());
    }

    [Fact]
    public void ShellState_RemembersTheTabOfEachGame()
    {
        var s = new ShellState();
        s.OpenProject("A"); s.SelectTab(WorkspaceTab.Map);
        s.OpenProject("B"); Assert.Equal(WorkspaceTab.Code, s.Tab); s.SelectTab(WorkspaceTab.Sound);
        s.GoHome();
        s.OpenProject("A");
        Assert.Equal(WorkspaceTab.Map, s.Tab);
        s.OpenProject("B");
        Assert.Equal(WorkspaceTab.Sound, s.Tab);
    }

    [Fact]
    public void ShellState_ExpandedWidthPutsCodeAndGameSideBySide()
    {
        var s = new ShellState();
        s.OpenProject("A");
        Assert.False(s.Resize(412));
        Assert.False(s.CodeAndGameSideBySide);
        Assert.True(s.Resize(ShellState.ExpandedMinWidth));
        Assert.True(s.CodeAndGameSideBySide);
        s.SelectTab(WorkspaceTab.Art);
        Assert.False(s.CodeAndGameSideBySide);
    }

    [Fact]
    public void GameSession_PausesStepsExactlyOneFrameAndScalesTime()
    {
        var root = NewRoot();
        try
        {
            var workspace = new WorkspaceSession(root);
            var project = workspace.Create("Passo");
            workspace.Write(project.Manifest.EntryPoint, RedGame);
            using var session = new GameSession(workspace);
            Assert.True(session.Run(64, 64));
            Assert.Equal(GameState.Running, session.State);
            Assert.True(session.Tick(1 / 60.0));
            Assert.Equal(new Lunet.Graphics.Color(255, 0, 0), session.Game!.Screen.ScreenPixel(32, 32));

            session.TogglePause();
            Assert.False(session.Tick(1 / 60.0));
            var frames = session.FramesRendered;
            Assert.True(session.Step());
            Assert.Equal(frames + 1, session.FramesRendered);
            Assert.Equal(GameState.Paused, session.State);

            session.SetSpeed(0.25);
            Assert.Equal(0.25, session.Speed);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.SetSpeed(3));
            session.Stop();
            Assert.Equal(GameState.Stopped, session.State);
            Assert.Null(session.Game);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void GameSession_ReportsCompileProblemsInsteadOfRunning()
    {
        var root = NewRoot();
        try
        {
            var workspace = new WorkspaceSession(root);
            var project = workspace.Create("Quebrado");
            workspace.Write(project.Manifest.EntryPoint, "class X { void M() { int a = ; } }");
            using var session = new GameSession(workspace);
            Assert.False(session.Run(64, 64));
            Assert.Equal(GameState.CompileErrors, session.State);
            Assert.Contains(session.Problems, p => p.Contains("CS1525"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("Game.cs", WorkspaceTab.Code)]
    [InlineData("lunet.json", WorkspaceTab.Code)]
    [InlineData("Assets/heroi.sprite.json", WorkspaceTab.Art)]
    [InlineData("Assets/fase1.tilemap.json", WorkspaceTab.Map)]
    [InlineData("Content/Audio/beep.wav", WorkspaceTab.Sound)]
    [InlineData("Assets/tema.song.json", WorkspaceTab.Sound)]
    [InlineData("Notes/ideias.md", WorkspaceTab.Notes)]
    public void ProjectFiles_PutsEachFileInItsTab(string path, WorkspaceTab tab)
    {
        foreach (var t in Enum.GetValues<WorkspaceTab>().Where(t => t != WorkspaceTab.Game))
            Assert.Equal(t == tab, ProjectFiles.For(t, [path]).Count == 1);
    }

    [Fact]
    public void Tokens_TextPairsMeetAA()
    {
        foreach (var bg in new[] { LunetTokens.Color.Background, LunetTokens.Color.Surface, LunetTokens.Color.SurfaceRaised })
        foreach (var fg in new[] { LunetTokens.Color.TextPrimary, LunetTokens.Color.TextSecondary, LunetTokens.Color.TextTertiary, LunetTokens.Color.Primary, LunetTokens.Color.Danger, LunetTokens.Color.Code })
            Assert.True(LunetTokens.Contrast(fg, bg) >= 4.5, $"{fg} sobre {bg}: {LunetTokens.Contrast(fg, bg):0.00}");
        Assert.True(LunetTokens.Contrast(LunetTokens.Color.OnPrimary, LunetTokens.Color.Primary) >= 4.5);
    }

    [Fact]
    public void Shell_CreateEditRunShowsTheGameAndSavesVerified_WithoutAnyDialog()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Empty(shell.Home.Shown);

                shell.CreateProject();
                Assert.Equal(ShellLevel.Workspace, shell.State.Level);
                Assert.Equal(ShellLayout.Compact, shell.State.Layout);
                var entry = shell.Workspace.Project!.Manifest.EntryPoint;
                Assert.Equal(entry, shell.WorkspaceView.Code.OpenPath);

                shell.WorkspaceView.Code.Surface.Text = RedGame;
                shell.RunGame();
                Assert.Equal(GameState.Running, shell.Game.State);
                Assert.Equal(WorkspaceTab.Game, shell.State.Tab);
                Assert.Equal(RedGame, File.ReadAllText(Path.Combine(shell.Workspace.Project.Directory, entry)));
                for (int i = 0; i < 3; i++) shell.TickGame(1 / 60.0);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                var frame = window.CaptureRenderedFrame()!;
                Assert.True(CountRed(frame) > 1000, "the running game is visible in the Jogo tab");

                Assert.Empty(window.GetVisualDescendants().OfType<Window>());
                Assert.True(shell.State.Back());
                Assert.Equal(ShellLevel.Home, shell.State.Level);
                Assert.Equal(GameState.Stopped, shell.Game.State);
                Assert.Single(shell.Home.Shown);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Shell_WideWindowKeepsCodeBesideTheRunningGame_AndCompileErrorsOpenProblems()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 1280, Height = 800, Content = shell };
                window.Show();
                shell.CreateProject();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(shell.State.CodeAndGameSideBySide);

                shell.WorkspaceView.Code.Surface.Text = RedGame;
                shell.RunGame();
                shell.TickGame(1 / 60.0);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.Equal(WorkspaceTab.Code, shell.State.Tab);
                Assert.True(shell.WorkspaceView.Code.IsEffectivelyVisible && shell.WorkspaceView.Game.IsEffectivelyVisible);

                shell.WorkspaceView.Code.Surface.Text = "class X { void M() { int a = ; } }";
                shell.RunGame();
                Assert.Equal(GameState.CompileErrors, shell.Game.State);
                Assert.Equal(SupportPanel.Problems, shell.State.Panel);
                Assert.True(shell.WorkspaceView.Support.IsEffectivelyVisible);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(915, double.NaN, false, 0)]       // keyboard closed
    [InlineData(915, 600, true, 0)]               // Android ≤ 14, adjustResize: the system already shrank the window
    [InlineData(915, 600, false, 315)]            // Android 15+, edge-to-edge: the shell leaves the keyboard's height
    [InlineData(915, 1000, false, 0)]             // keyboard below the window (rotating): nothing covered
    public void KeyboardFit_CoversOnlyAnEdgeToEdgeWindow(double window, double keyboardTop, bool shrinks, double expected) =>
        Assert.Equal(expected, KeyboardFit.Cover(window, keyboardTop, shrinks));

    [Theory]
    [InlineData(0, 0, SwipeDirection.None)]
    [InlineData(10, -12, SwipeDirection.None)]
    [InlineData(0, -30, SwipeDirection.Up)]
    [InlineData(4, 25, SwipeDirection.Down)]
    [InlineData(-40, 10, SwipeDirection.Left)]
    [InlineData(30, -20, SwipeDirection.Right)]
    public void CodeKeys_ResolvesTapsAndSwipes(double dx, double dy, SwipeDirection expected) =>
        Assert.Equal(expected, CodeKeys.Resolve(dx, dy));

    [Fact]
    public void CodeKeys_BarIsTenKeysWithEveryCSharpSymbolOneGestureAway()
    {
        Assert.Equal(10, CodeKeys.Bar.Count);
        var reachable = CodeKeys.Bar.SelectMany(k => Enum.GetValues<SwipeDirection>().Select(k.For)).ToHashSet();
        foreach (var s in new[] { "{", "}", "(", ")", "[", "]", "<", ">", ";", ":", ",", ".", "=", "+", "-", "!", "=>", "\"", "'", "&", "|", "?", "*", "/", "%", "_", CodeKeys.Tab, CodeKeys.Left, CodeKeys.Right, CodeKeys.Up, CodeKeys.Down })
            Assert.Contains(s, reachable);
    }

    [Fact]
    public void CodeKeys_OpeningBraceMakesAnIndentedBlockInOneEdit()
    {
        var text = "    if (x)\n    ";
        var edit = CodeKeys.Apply(text, text.Length, text.Length, "{")!.Value;
        var result = text[..edit.Start] + edit.Insert + text[edit.End..];
        Assert.Equal("    if (x)\n    {\n        \n    }", result);
        Assert.Equal("    if (x)\n    {\n        ".Length, edit.Caret);
    }

    [Fact]
    public void CodeKeys_PairsWrapTheSelectionAndArrowsMove()
    {
        var edit = CodeKeys.Apply("a + b", 0, 5, "(")!.Value;
        Assert.Equal("(a + b)", edit.Insert);
        Assert.Equal(6, edit.Caret);
        Assert.Null(CodeKeys.Apply("abc", 1, 1, CodeKeys.Left));
        const string two = "abcd\nxy\nlonger";
        Assert.Equal(7, CodeKeys.Move(two, 2, CodeKeys.Down));      // column 2 on "xy" → its end
        Assert.Equal(2, CodeKeys.Move(two, 7, CodeKeys.Up));        // back to column 2 of "abcd"
        Assert.Equal(0, CodeKeys.Move(two, 0, CodeKeys.Up));
        Assert.Equal(two.Length, CodeKeys.Move(two, 9, CodeKeys.Down));
    }

    [Fact]
    public void KeyboardLayout_ShiftCapitalizesOnceOrLocksOnDoubleTap()
    {
        var q = KeyboardLayout.Letters[1][0];
        Assert.Equal("q", KeyboardLayout.Typed(q, ShiftState.Off));
        Assert.Equal("Q", KeyboardLayout.Typed(q, ShiftState.Once));
        Assert.Equal(ShiftState.Once, KeyboardLayout.Toggle(ShiftState.Off, TimeSpan.FromSeconds(5)));
        Assert.Equal(ShiftState.Locked, KeyboardLayout.Toggle(ShiftState.Once, TimeSpan.FromMilliseconds(200)));
        Assert.Equal(ShiftState.Off, KeyboardLayout.Toggle(ShiftState.Once, TimeSpan.FromSeconds(1)));
        Assert.Equal(ShiftState.Off, KeyboardLayout.AfterTyping(ShiftState.Once));
        Assert.Equal(ShiftState.Locked, KeyboardLayout.AfterTyping(ShiftState.Locked));
        Assert.Contains("ç", KeyboardLayout.Letters.SelectMany(r => r).Single(k => k.Label == "c").Variants!);
        Assert.Contains("ã", KeyboardLayout.Letters.SelectMany(r => r).Single(k => k.Label == "a").Variants!);
    }

    [Fact]
    public void KeyboardLayout_BackspaceDeletesSelectionCharOrWords_AndEnterKeepsIndentation()
    {
        Assert.Equal(new CodeEdit(1, 3, "", 1), KeyboardLayout.Backspace("abcd", 1, 3));
        Assert.Equal(new CodeEdit(3, 4, "", 3), KeyboardLayout.Backspace("abcd", 4, 4));
        Assert.Null(KeyboardLayout.Backspace("abcd", 0, 0));
        const string line = "var speed = player.Speed";
        var two = KeyboardLayout.Backspace(line, line.Length, line.Length, words: 2)!.Value;
        Assert.Equal("var speed = player", line[..two.Start] + line[two.End..]);
        Assert.Equal(1, KeyboardLayout.GlideWords(-30));
        Assert.Equal(0, KeyboardLayout.GlideWords(5));

        var enter = KeyboardLayout.Enter("    if (x) {", 12, 12);
        Assert.Equal("\n        ", enter.Insert);
        Assert.Equal("\n    ", KeyboardLayout.Enter("    x();", 8, 8).Insert);
        Assert.Equal(5, KeyboardLayout.GlideCursor("abcdefgh", 2, 3 * KeyboardLayout.CursorStep + 1));
        Assert.Equal(0, KeyboardLayout.GlideCursor("abc", 1, -100));
    }

    [Fact]
    public void MethodCatalog_ReadsTheGeneratedApi_SortsAlphabetically_AndAddsTheGamesMembers()
    {
        var catalog = new MethodCatalog();
        Assert.True(catalog.All.Count() > 500, "the generated docs/api/lunet-framework.json is embedded");
        var draw = catalog.All.Single(m => m.Type == "SpriteBatch" && m.Name == "Draw");
        Assert.True(draw.Signatures.Count > 1, "overloads are one entry");
        var tree = catalog.Tree();
        Assert.Equal(tree.Select(n => n.Name).Order(StringComparer.OrdinalIgnoreCase), tree.Select(n => n.Name));
        foreach (var ns in tree)
        {
            Assert.Equal(ns.Types.Select(t => t.Name).Order(StringComparer.OrdinalIgnoreCase), ns.Types.Select(t => t.Name));
            foreach (var t in ns.Types) Assert.Equal(t.Members.Select(m => m.Name).Order(StringComparer.OrdinalIgnoreCase), t.Members.Select(m => m.Name));
        }

        catalog.SetProjectSources([("Game.cs", "public sealed class Hero { public void Jump(float force) {} public int Lives { get; set; } protected override void Update(GameTime t) {} }")]);
        var hero = catalog.Tree().Single(n => n.Name == MethodCatalog.ProjectNamespace).Types.Single(t => t.Name == "Hero");
        Assert.Equal(["Jump", "Lives"], hero.Members.Select(m => m.Name));
    }

    [Fact]
    public void MethodCatalog_RecentAndMostUsedFollowTheirMeaning_AndPersist()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "metodos.json");
            var catalog = new MethodCatalog(path);
            var all = catalog.All.ToArray();
            CatalogMember M(string type, string name) => all.First(m => m.Type == type && m.Name == name);
            var t0 = new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);
            catalog.RecordUse(M("SpriteBatch", "Draw"), t0);
            catalog.RecordUse(M("SpriteBatch", "Draw"), t0.AddMinutes(1));
            catalog.RecordUse(M("SpriteBatch", "Begin"), t0.AddMinutes(2));
            catalog.RecordUse(M("SpriteBatch", "End"), t0.AddMinutes(2));
            Assert.Equal(["Begin", "End", "Draw"], catalog.Recent().Select(m => m.Name));      // newest first, ties alphabetical
            Assert.Equal(["Draw", "Begin", "End"], catalog.MostUsed().Select(m => m.Name));    // most used first, ties alphabetical
            Assert.Equal(["Begin", "End", "Draw"], new MethodCatalog(path).Recent().Select(m => m.Name));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void MethodCatalog_InsertsCallsWithoutPlaceholders()
    {
        var all = new MethodCatalog().All.ToArray();
        var draw = all.First(m => m.Type == "SpriteBatch" && m.Name == "Draw");
        var edit = MethodCatalog.Insertion(draw, "batch.", 6, 6);
        Assert.Equal("Draw()", edit.Insert);
        Assert.Equal(6 + "Draw(".Length, edit.Caret);                       // caret inside: it takes parameters
        var end = all.First(m => m.Type == "SpriteBatch" && m.Name == "End");
        Assert.Equal(6 + "End()".Length, MethodCatalog.Insertion(end, "batch.", 6, 6).Caret); // no parameters: after ()
        var staticMember = all.First(m => m.IsStatic && m.Kind == "method" && m.Namespace != MethodCatalog.ProjectNamespace);
        Assert.StartsWith(staticMember.Type + "." + staticMember.Name + "(", MethodCatalog.Insertion(staticMember, "", 0, 0).Insert);
        var ctor = all.First(m => m.Kind == "constructor" && m.Type == "SpriteBatch");
        Assert.Equal("new SpriteBatch()", MethodCatalog.Insertion(ctor, "", 0, 0).Insert);
        var prop = all.First(m => m.Kind == "property" && !m.IsStatic);
        Assert.Equal(prop.Name, MethodCatalog.Insertion(prop, "x.", 2, 2).Insert);
    }

    [Fact]
    public void Keyboard_ReplacesTheSystemKeyboard_AndSwitchingTabsKeepsCaretSelectionAndFocus()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var editor = shell.WorkspaceView.Code;
                editor.Surface.Text = "x";
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                editor.Surface.Focus();
                editor.Surface.Caret = 1;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                Assert.True(editor.KeyboardVisible, $"focused={editor.Surface.IsFocused} kb={editor.Keyboard is not null} layout={shell.State.Layout}");
                Assert.False(Avalonia.Input.InputMethod.GetIsInputMethodEnabled(editor.Surface.InputTarget)); // the system keyboard steps aside
                Assert.False(shell.WorkspaceView.TabBarVisible);
                var kb = editor.Keyboard!;
                Assert.Equal(KeyboardTab.Keys, kb.Tab);

                kb.Press(new KeyDef("⇧", KeyAction.Shift));
                kb.Press(KeyboardLayout.Letters[2][0]);                 // "a" → "A" (one capital)
                kb.Press(KeyboardLayout.Letters[2][0]);                 // "a"
                kb.ShowVariants(KeyboardLayout.Letters.SelectMany(r => r).Single(k => k.Label == "c"));
                kb.CommitVariant();                                      // long press c → ç
                Assert.Equal("xAaç", editor.Surface.Text);

                editor.Surface.Select(1, 3);
                foreach (var tab in new[] { KeyboardTab.Shortcuts, KeyboardTab.Methods, KeyboardTab.Keys, KeyboardTab.Shortcuts })
                {
                    kb.Show(tab);
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    Assert.Equal((1, 3), (editor.Surface.SelectionStart, editor.Surface.SelectionEnd));
                    Assert.True(editor.Surface.IsFocused);
                }
                kb.Shortcut("(");                                        // wraps the selection
                Assert.Equal("x(Aa)ç", editor.Surface.Text);

                kb.Show(KeyboardTab.Methods);
                var end = kb.Catalog.All.First(m => m.Type == "SpriteBatch" && m.Name == "End");
                editor.Surface.Caret = editor.Surface.Text.Length;
                kb.Insert(end);
                Assert.EndsWith("çEnd()", editor.Surface.Text); // instance member: no type prefix
                Assert.Contains(kb.Catalog.Recent(), m => m.Name == "End");

                Assert.True(shell.Back());                               // Back closes the keyboard first
                Assert.False(editor.KeyboardVisible);
                Assert.True(Avalonia.Input.InputMethod.GetIsInputMethodEnabled(editor.Surface.InputTarget));
                Assert.Equal(ShellLevel.Workspace, shell.State.Level);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Keyboard_StepsAsideForAHardwareKeyboardAndTheSystemKey()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var editor = shell.WorkspaceView.Code;
                editor.ShowKeyboard();
                Assert.True(editor.KeyboardVisible);

                editor.Keyboard!.Press(new KeyDef("🌐", KeyAction.System));  // the system keyboard (dictation, languages)
                Assert.True(shell.PreferSystemKeyboard);
                Assert.False(editor.KeyboardVisible);
                Assert.True(Avalonia.Input.InputMethod.GetIsInputMethodEnabled(editor.Surface.InputTarget));

                shell.PreferSystemKeyboard = false;
                shell.HardwareKeyboard = true;
                editor.ShowKeyboard();
                Assert.False(editor.KeyboardVisible);                         // a physical keyboard types directly
                shell.HardwareKeyboard = false;
                editor.ShowKeyboard();
                Assert.True(editor.KeyboardVisible);

                window.Width = 1280;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.False(shell.WorkspaceView.Code.KeyboardVisible);       // wide layout: desktop/tablet keyboard
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Shell_RoutesToTheScreensNotMovedYet_AndReloadsWhatTheyChanged()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var routes = new List<(string Route, string? Project)>();
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                Assert.DoesNotContain(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "Route-studios"); // no routes on desktop
                shell.OpenRoute = (r, p) => routes.Add((r, p));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                foreach (var (route, _, _) in LunetShell.HomeRoutes)
                    Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "Route-" + route);

                shell.CreateProject();
                var entry = shell.Workspace.Project!.Manifest.EntryPoint;
                shell.State.ShowPanel(SupportPanel.More);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var git = window.GetVisualDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Git");
                git.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(("git", (string?)shell.State.Project), routes.Last());

                // The routed screen (e.g. the AI) edits the game on disk; back in the shell the editor shows it.
                File.WriteAllText(Path.Combine(shell.Workspace.Project.Directory, entry), "// editado pela IA");
                shell.ReturnedFromRoute();
                Assert.Equal("// editado pela IA", shell.WorkspaceView.Code.Surface.Text);

                // Unsaved edits made in the shell are never overwritten by a reload.
                shell.WorkspaceView.Code.Surface.Text = "// meu texto";
                File.WriteAllText(Path.Combine(shell.Workspace.Project.Directory, entry), "// outro");
                shell.ReturnedFromRoute();
                Assert.Equal("// meu texto", shell.WorkspaceView.Code.Surface.Text);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void CodeEditor_HasLineNumbersHighlightingAndUndoesKeyboardEditsInOneStep()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var code = Assert.IsType<CodeSurface>(shell.WorkspaceView.Code.Surface);
                Assert.True(code.Editor.ShowLineNumbers);
                Assert.Equal("C#", code.Editor.SyntaxHighlighting?.Name);
                Assert.True(code.Editor.Options.HighlightCurrentLine);
                Assert.Contains(window.GetVisualDescendants(), v => v.GetType().Name == "LineNumberMargin");

                shell.WorkspaceView.Code.ShowKeyboard();
                var kb = shell.WorkspaceView.Code.Keyboard!;
                var before = code.Text;
                code.Caret = 0;
                kb.Shortcut("{");                                   // a whole indented block…
                Assert.StartsWith("{\n    \n}", code.Text);
                code.Undo();                                        // …undoes in one step
                Assert.Equal(before, code.Text);
                Assert.False(Avalonia.Input.InputMethod.GetIsInputMethodEnabled(code.InputTarget));
                Assert.IsType<NotesSurface>(shell.WorkspaceView.Notes.Surface); // notes keep the system keyboard
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void Suggestions_FollowTheWordBeingTyped_TypesAndMembers()
    {
        var catalog = new MethodCatalog();
        Assert.Equal("Spri", MethodCatalog.WordBefore("x = Spri", 8));
        Assert.Equal("", MethodCatalog.WordBefore("x = ", 4));
        Assert.Contains(catalog.Suggest("Spri"), m => m.Name == "SpriteBatch" && m.Kind == "type");
        var afterDot = catalog.Suggest("Dra");
        Assert.Contains(afterDot, m => m.Name == "Draw");
        Assert.All(catalog.Suggest("cle"), m => Assert.StartsWith("cle", m.Name, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(catalog.Suggest("Dra").Select(m => m.Name).Distinct().Count(), catalog.Suggest("Dra").Count); // one per name
        Assert.DoesNotContain(catalog.Tree().SelectMany(n => n.Types).SelectMany(t => t.Members), m => m.Kind == "type");
    }

    [Fact]
    public void Keyboard_SuggestionStripCompletesTheWordAndPreviewsKeys()
    {
        var root = NewRoot();
        try
        {
            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                shell.CreateProject();
                var editor = shell.WorkspaceView.Code;
                editor.Surface.Text = "batch.";
                editor.ShowKeyboard();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var kb = editor.Keyboard!;
                editor.Surface.Caret = 6;
                foreach (var c in "Dr") kb.Press(new KeyDef(c.ToString()));
                Assert.Contains(kb.Suggestions, m => m.Name == "Draw");
                kb.AcceptSuggestion(kb.Suggestions.First(m => m.Name == "Draw"));
                Assert.Equal("batch.Draw()", editor.Surface.Text);
                Assert.Equal("batch.Draw(".Length, editor.Surface.Caret);   // inside the parentheses
                Assert.True(kb.Height <= LunetKeyboard.StripHeight + 5 * LunetKeyboard.RowHeight + 6);
                Assert.True(LunetTokens.Contrast(LunetTokens.Color.TextPrimary, LunetTokens.Color.Key) >= 4.5);
                Assert.True(LunetTokens.Contrast(LunetTokens.Color.TextPrimary, LunetTokens.Color.KeyModifier) >= 4.5);
                Assert.True(LunetTokens.Contrast(LunetTokens.Color.OnPrimary, LunetTokens.Color.Primary) >= 4.5);
                window.Close();
            });
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static int CountRed(Avalonia.Media.Imaging.Bitmap frame)
    {
        var w = frame.PixelSize.Width; var h = frame.PixelSize.Height;
        var pixels = new byte[w * h * 4];
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(pixels.Length);
        try
        {
            frame.CopyPixels(new PixelRect(0, 0, w, h), buffer, pixels.Length, w * 4);
            System.Runtime.InteropServices.Marshal.Copy(buffer, pixels, 0, pixels.Length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
        int red = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            // Bgra8888 or Rgba8888: pure red has one channel at 255 and the other two at 0.
            byte a = pixels[i], b = pixels[i + 1], c = pixels[i + 2];
            if ((a == 0 && b == 0 && c == 255) || (a == 255 && b == 0 && c == 0)) red++;
        }
        return red;
    }
}

/// <summary>One Avalonia headless (Skia) session for the whole test run; every UI test runs on its dispatcher.</summary>
internal static class UiSession
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(() => HeadlessUnitTestSession.StartNew(typeof(TestApp)));

    public static void Run(Action action) =>
        Session.Value.Dispatch(action, TestContext.Current.CancellationToken).GetAwaiter().GetResult();

    public sealed class TestApp : LunetApp
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}
