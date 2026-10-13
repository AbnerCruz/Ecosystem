using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// UC-37 (DEC-0045): where the editor leaves the 1.8.4 on the owner's order. Voltar walks back the followed links, a
/// [[link]] to a missing note offers to create it, the toolbar shows what the text under the caret is, and on a
/// phone the toolbar sits next to the keyboard.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Editor_BackWalksTheFollowedLinksAndAMissingLinkCreatesItsNote() =>
        Assert.True(await Session.Dispatch(EditorNavigationScenario, CancellationToken.None));

    private static async Task<bool> EditorNavigationScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-nav-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(Path.Combine(vault, "Bairro"));
        File.WriteAllText(Path.Combine(vault, "Bairro", "Casa.md"), "# Casa\n\n[[Outra]] e [[Nova ideia]]\n");
        File.WriteAllText(Path.Combine(vault, "Outra.md"), "# Outra\n");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            var editor = view.Editor;

            // following a link remembers where it came from; Voltar goes back there, then closes
            view.OpenNoteByPath("Bairro/Casa.md");
            Dispatcher.UIThread.RunJobs();
            await editor.FollowLinkAsync("Outra");
            Assert.Equal("Outra.md", editor.Current!.Path);
            Assert.Equal(1, editor.TrailDepth);
            await editor.BackAsync();
            Assert.True(editor.IsVisible);
            Assert.Equal("Bairro/Casa.md", editor.Current!.Path);
            Assert.Equal(0, editor.TrailDepth);
            await editor.BackAsync();
            Assert.False(editor.IsVisible);

            // a fresh open starts a new trail
            view.OpenNoteByPath("Bairro/Casa.md");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, editor.TrailDepth);

            // [[Nova ideia]] names no note: the 1.8.4 only said so; now it asks, and creates it next to Casa, in its
            // neighbourhood, written to the vault, with Casa on the trail
            var follow = editor.FollowLinkAsync("Nova ideia");
            Dispatcher.UIThread.RunJobs();
            Assert.True(view.Dialogs.IsOpen);
            ClickButtonWithText(view.Dialogs, "Criar nota");
            await follow;
            Assert.True(File.Exists(Path.Combine(vault, "Bairro", "Nova ideia.md")));
            Assert.Equal("Bairro/Nova ideia.md", editor.Current!.Path);
            Assert.Contains(view.World.City!.Buildings, b => b.Path == "Bairro/Nova ideia.md");
            Assert.Equal(1, editor.TrailDepth);
            await editor.BackAsync();
            Assert.Equal("Bairro/Casa.md", editor.Current!.Path);

            // the toolbar is lit by the Markdown under the caret, and goes off again when the caret leaves it
            editor.SetMode(EditorMode.Fonte);
            editor.Editor.Text = "a **forte** b\n# Título";
            editor.Editor.CaretIndex = 5;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("bold", editor.ActiveTools);
            editor.Editor.CaretIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain("bold", editor.ActiveTools);
            editor.Editor.CaretIndex = editor.Editor.Text.Length;
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("h1", editor.ActiveTools);

            // Fixar em painel (UC-39): the selection stays on top, and goes on showing when another note opens
            editor.Editor.Text = "Frase de referência\n\nresto da nota";
            editor.Editor.SelectionStart = 0;
            editor.Editor.SelectionEnd = "Frase de referência".Length;
            editor.PinSelectionOrNote();
            Dispatcher.UIThread.RunJobs();
            Assert.True(editor.Pinned.IsVisible);
            Assert.Equal("Frase de referência", editor.Pinned.Text);
            Assert.Equal("Bairro/Casa.md", editor.Pinned.SourcePath);
            Assert.Contains(editor.Pinned.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Fixado de Casa.md");
            await editor.FollowLinkAsync("Outra");
            Assert.Equal("Outra.md", editor.Current!.Path);
            Assert.True(editor.Pinned.IsVisible);
            Assert.Equal("Frase de referência", editor.Pinned.Text);
            Snapshot(window, "native-editor-pinned.png");
            // nothing selected: the whole note
            editor.SetMode(EditorMode.Fonte);
            editor.Editor.SelectionStart = editor.Editor.SelectionEnd = 0;
            editor.PinSelectionOrNote();
            Assert.Equal(editor.Editor.Text, editor.Pinned.Text);
            editor.Pinned.Unpin();
            Assert.False(editor.Pinned.IsVisible);
            await editor.BackAsync();

            // phone: the toolbar on the bottom row, next to the keyboard; a wide window keeps it on top
            Assert.True(editor.ToolbarAtBottom);
            Snapshot(window, "native-editor-toolbar-bottom.png");
            window.Width = 1280;
            await Settle(window, view);
            Assert.False(editor.ToolbarAtBottom);
            window.Close();
            return true;
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
    }
}
