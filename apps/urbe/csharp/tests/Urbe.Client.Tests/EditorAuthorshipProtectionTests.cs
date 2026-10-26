using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
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
}
