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

    [Fact]
    public async Task ClearingAnAuthoredLineRemovesOnlyItsOrphanBadgeInBothModes() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay { StartInVisual = false };
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Linha.md", Marked + "\nOutra linha"));
            editor.SetMode(EditorMode.Fonte);
            editor.Editor.Text = " {ia:\"modelo\"}\nOutra linha";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("\nOutra linha", editor.Editor.Text);
            editor.Open(new CityNote("Notas/Visual.md", Marked + "\n"));
            editor.SetMode(EditorMode.Visual);
            window.UpdateLayout();
            var start = Assert.Single(editor.Surface.BlockStarts);
            var holder = editor.Surface.BlockView(start)!;
            editor.Surface.TapBlock(start, holder, holder, new Point(8, 8), select: false);
            Dispatcher.UIThread.RunJobs();
            var box = Assert.Single(editor.Surface.GetVisualDescendants().OfType<TextBox>());
            box.Text = " {ia:\"modelo\"}";
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain("{ia:", editor.Editor.Text);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    private static TextBox EditBlock(EditorOverlay editor, Window window, int index = 0)
    {
        window.UpdateLayout();
        var start = editor.Surface.BlockStarts.ElementAt(index);
        var holder = editor.Surface.BlockView(start)!;
        editor.Surface.TapBlock(start, holder, holder, new Point(8, 8), select: false);
        Dispatcher.UIThread.RunJobs();
        return Assert.Single(editor.Surface.GetVisualDescendants().OfType<TextBox>());
    }

    [Fact]
    public async Task VisualBoxNeverShowsTheSealAndTheEditedBlockKeepsIt() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Visual.md", Marked + "\n"));
            var box = EditBlock(editor, window);
            Assert.Equal("Texto do agente", box.Text);
            box.Text = "Texto revisado pela pessoa";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Texto revisado pela pessoa {ia:\"modelo\"}\n", editor.Editor.Text);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task VisualTypedSealIsNotASeal() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Humana.md", "Meu texto\n"));
            var box = EditBlock(editor, window);
            box.Text = "Meu texto {ia:\"falso\"}";
            Dispatcher.UIThread.RunJobs();
            Assert.DoesNotContain("{ia:", editor.Editor.Text);
            Assert.DoesNotContain("{ia:", box.Text);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task VisualSplittingAnAiParagraphKeepsTheSealOnBothParts() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Divide.md", "Primeira parte do texto e segunda parte do texto {ia:\"m\"}\n"));
            var box = EditBlock(editor, window);
            box.Text = "Primeira parte do texto\ne segunda parte do texto";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Primeira parte do texto {ia:\"m\"}\ne segunda parte do texto {ia:\"m\"}\n", editor.Editor.Text);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task VisualCodeBlockKeepsTheSealOnTheLineAfterIt() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Codigo.md", "```\nvar a = 1;\n```\n{ia:\"m\"}\n"));
            window.UpdateLayout();
            Assert.Single(editor.Surface.BlockStarts);
            var box = EditBlock(editor, window);
            Assert.Equal("```\nvar a = 1;\n```", box.Text);
            box.Text = "```\nvar a = 2;\n```";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("```\nvar a = 2;\n```\n{ia:\"m\"}\n", editor.Editor.Text);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task SourceCannotDeleteOnlyTheSealOfACodeBlockButCanDeleteTheBlock() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            const string code = "```\nx\n```\n{ia:\"m\"}\nDepois";
            var editor = new EditorOverlay { StartInVisual = false };
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Fonte.md", code));
            editor.SetMode(EditorMode.Fonte);
            editor.Editor.Text = "```\nx\n```\nDepois";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(code, editor.Editor.Text);
            editor.Editor.Text = "Depois";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Depois", editor.Editor.Text);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task FormattingBarIncludesUndoRedoEvenWithPersonalizedButtons() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 720, Content = editor };
            window.Show();
            editor.Open(new CityNote("Notas/Botoes.md", "Teste"));
            Assert.Contains("undo", EditorOverlay.ToolbarIds);
            Assert.Contains("redo", EditorOverlay.ToolbarIds);
            Assert.Contains("Desfazer", editor.ToolbarNames);
            Assert.Contains("Refazer", editor.ToolbarNames);
            editor.ApplyLayout(["bold"], null);
            Assert.Contains("Desfazer", editor.ToolbarNames);
            Assert.Contains("Refazer", editor.ToolbarNames);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));


}
