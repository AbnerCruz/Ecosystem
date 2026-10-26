using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed class EditorAuthorshipProtectionTests
{
    private const string Marked = "Texto do agente {ia:\"modelo\"}";

    [Fact]
    public async Task SourceCannotDeleteAuthorshipButCanEditProse() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay { StartInVisual = false };
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Protegida.md", Marked));
            editor.SetMode(EditorMode.Fonte);
            editor.Editor.Text = "Texto do agente";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Marked, editor.Editor.Text);
            editor.Editor.Text = "Texto revisado do agente {ia:\"modelo\"}";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Texto revisado do agente {ia:\"modelo\"}", editor.Editor.Text);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task SourceCannotForgeAuthorshipMark() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay { StartInVisual = false };
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Limpa.md", "Texto comum"));
            editor.SetMode(EditorMode.Fonte);
            editor.Editor.Text = "Texto comum {ia:\"falso\"}";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Texto comum", editor.Editor.Text);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
    [Fact]
    public async Task VisualCannotDeleteProtectedMarkButNormalBlockEditsWork() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Visual.md", Marked + "\n"));
            Assert.Equal(EditorMode.Visual, editor.Mode);
            window.UpdateLayout();
            var start = Assert.Single(editor.Surface.BlockStarts);
            var holder = editor.Surface.BlockView(start)!;
            editor.Surface.TapBlock(start, holder, holder, new Point(8, 8), select: false);
            Dispatcher.UIThread.RunJobs();
            var block = Assert.Single(editor.Surface.GetVisualDescendants().OfType<TextBox>());
            block.Text = "Texto do agente";
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("{ia:", editor.Editor.Text);
            Assert.Equal(Marked + "\n", editor.Editor.Text);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

}
