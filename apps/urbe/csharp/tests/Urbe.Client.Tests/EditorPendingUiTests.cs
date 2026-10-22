using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-55: the editor's open points of the owner's notes. Inline code wraps in backticks instead of taking a line; a
/// tapped [[nova nota]] in Visual offers to create the note and opens it; the lit buttons follow the text and a lit
/// button turns off; the Assistente and the Relógio are in the editor's top bar.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task EditorPending_InlineCodeToolStateMissingLinkAndTopBarButtons() =>
        Assert.True(await Session.Dispatch(EditorPendingScenario, CancellationToken.None));

    private static async Task<bool> EditorPendingScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-uc55-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Casa.md"), "# Título\n\na **forte** b\n\n[[nova nota]]\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            view.Customization.Device = new MemoryDeviceStore();
            view.SetNativePlugin("relogio", true);
            var editor = view.Editor;
            view.OpenNoteByPath("Casa.md");
            await Settle(window, view);
            Assert.Equal(EditorMode.Visual, editor.Mode);

            // the Assistente and the Relógio are in the editor's top bar, on screen on a phone and on a wide window
            foreach (var width in new[] { 412.0, 1280.0 })
            {
                window.Width = width;
                await Settle(window, view);
                foreach (var name in new[] { "Assistente", "Relógio" })
                {
                    var b = TopBarButton(editor, name);
                    Assert.True(b.IsEffectivelyVisible, name + " @" + width);
                    var at = b.TranslatePoint(new Point(0, 0), window)!.Value;
                    Assert.True(at.X >= 0 && at.X + b.Bounds.Width <= width && at.Y >= 0 && at.Y < 60, name + " @" + width + ": " + at);
                }
            }
            window.Width = 412;
            await Settle(window, view);

            // Visual: a tap at the start of the heading opens it with the caret on the tapped letter, after the "# " mark
            // (UC-69: the drawn character is mapped back to the Markdown), and H1 is lit at once
            var heading = RenderedText(editor, "Título");
            var start = heading.TranslatePoint(new Point(1, heading.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseUp(start, MouseButton.Left);
            await Ticks(window);
            var box = editor.Surface.GetLogicalDescendants().OfType<TextBox>().Single();
            Assert.Equal("# Título", box.Text);
            Assert.Equal("# ".Length, box.CaretIndex);
            Assert.Contains("h1", editor.ActiveTools);

            // the caret inside **forte**: B is lit, and tapping it takes the marks off (it used to add another pair)
            box.Text = "a **forte** b";
            box.CaretIndex = 6;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("bold", editor.ActiveTools);
            ClickTool(editor, "Negrito");
            Assert.Equal("a forte b", box.Text);
            Assert.DoesNotContain("bold", editor.ActiveTools);

            // Código with nothing selected: a pair of backticks, the caret between them, no "código" word nor a new line
            box.CaretIndex = 2;
            ClickTool(editor, "Código");
            Assert.Equal("a ``forte b", box.Text);
            Assert.Equal(3, box.CaretIndex);
            Assert.Contains("code", editor.ActiveTools);
            // the block drawn again (Personalização changed the tags' colours): its buttons do not stay lit
            editor.RefreshVisual();
            Dispatcher.UIThread.RunJobs();
            Assert.Empty(editor.ActiveTools);

            // Fonte: the same buttons toggle what they show
            editor.SetMode(EditorMode.Fonte);
            editor.Editor.Text = "a x b";
            editor.Editor.SelectionStart = 2;
            editor.Editor.SelectionEnd = 3;
            ClickTool(editor, "Código");
            Assert.Equal("a `x` b", editor.Editor.Text);
            Assert.Contains("code", editor.ActiveTools);
            ClickTool(editor, "Código");
            Assert.Equal("a x b", editor.Editor.Text);
            Assert.DoesNotContain("code", editor.ActiveTools);
            editor.Editor.SelectionStart = editor.Editor.SelectionEnd = editor.Editor.CaretIndex = 2;
            ClickTool(editor, "Código");
            Assert.Equal("a ``x b", editor.Editor.Text);
            Assert.Equal(3, editor.Editor.CaretIndex);
            editor.Editor.Text = "a **forte** b\n# Título";
            editor.Editor.SelectionStart = editor.Editor.SelectionEnd = editor.Editor.CaretIndex = 6;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("bold", editor.ActiveTools);
            ClickTool(editor, "Negrito");
            Assert.Equal("a forte b\n# Título", editor.Editor.Text);
            Assert.DoesNotContain("bold", editor.ActiveTools);
            editor.Editor.SelectionStart = editor.Editor.SelectionEnd = editor.Editor.CaretIndex = editor.Editor.Text.Length - 2;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("h1", editor.ActiveTools);
            ClickTool(editor, "Título 1");
            Assert.Equal("a forte b\nTítulo", editor.Editor.Text);
            Assert.DoesNotContain("h1", editor.ActiveTools);

            // back to the note as it was, in Visual: a tap on [[nova nota]] asks, creates the note and opens it
            editor.Editor.Text = "# Título\n\n[[nova nota]]\n";
            editor.SetMode(EditorMode.Visual);
            await Ticks(window);
            var link = RenderedText(editor, "nova nota");
            var rect = link.TextLayout.HitTestTextPosition(2);
            var on = link.TranslatePoint(rect.Center, window)!.Value;
            window.MouseDown(on, MouseButton.Left);
            window.MouseUp(on, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Dialogs.IsOpen);
            Assert.Empty(editor.Surface.GetLogicalDescendants().OfType<TextBox>());   // the tap followed the link, not an edit
            ClickButtonWithText(view.Dialogs, "Criar nota");
            for (int i = 0; i < 200 && editor.Current?.Path != "nova nota.md"; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.Equal("nova nota.md", editor.Current!.Path);
            Assert.True(File.Exists(Path.Combine(vault, "nova nota.md")));
            Assert.Contains(view.World.City!.Buildings, b => b.Path == "nova nota.md");
            Assert.Contains("[[nova nota]]", File.ReadAllText(Path.Combine(vault, "Casa.md")));   // the note left was saved
            Assert.Equal(1, editor.TrailDepth);
            return true;
        }
        finally
        {
            window.Close();
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    private static async Task Ticks(Window window)
    {
        for (int i = 0; i < 4; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }
        window.CaptureRenderedFrame();
        Dispatcher.UIThread.RunJobs();
    }

    private static Button TopBarButton(EditorOverlay editor, string tip) =>
        editor.GetLogicalDescendants().OfType<Button>().First(b => ToolTip.GetTip(b) as string == tip && !b.GetLogicalAncestors().Contains(editor.Surface));

    /// <summary>A button of the formatting bar (not of the selection bubble inside the Visual surface).</summary>
    private static void ClickTool(EditorOverlay editor, string tip)
    {
        TopBarButton(editor, tip).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The drawn text of the Visual surface holding <paramref name="text"/>.</summary>
    private static TextBlock RenderedText(EditorOverlay editor, string text) =>
        editor.Surface.GetVisualDescendants().OfType<TextBlock>().First(t =>
            t.Text == text || (t.Inlines?.OfType<Run>().Any(r => r.Text == text) ?? false));
}
