using Avalonia;
using Avalonia.Headless;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed class NoteProgressUiTests
{
    [Fact]
    public async Task NativeProgressRecalculatesFromActualCheckboxesAndSupportsUndo() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            const string markdown = "```progress\nlabel: Entrega\nsource: tasks\n```\n\n- [x] uma\n- [ ] duas\n";
            var editor = new EditorOverlay();
            var window = new Window { Width = 320, Height = 780, Content = editor };
            window.Show();
            editor.Open(new CityNote("Tarefas.md", markdown));
            Dispatcher.UIThread.RunJobs();
            var bar = editor.GetLogicalDescendants().OfType<ProgressBar>().Single();
            Assert.Equal(50d, bar.Value);
            Assert.Contains("faltam 1", AutomationProperties.GetName(bar));
            editor.SetMode(EditorMode.Fonte);
            editor.Editor.SelectionStart = markdown.IndexOf("[ ]", StringComparison.Ordinal) + 1;
            editor.Editor.SelectionEnd = editor.Editor.SelectionStart + 1;
            editor.InsertText("x");
            editor.SetMode(EditorMode.Visual);
            Assert.Equal(100d, editor.GetLogicalDescendants().OfType<ProgressBar>().Single().Value);
            editor.Undo();
            Assert.Equal(50d, editor.GetLogicalDescendants().OfType<ProgressBar>().Single().Value);
            Assert.Contains("Inserir progresso", editor.ToolbarNames);
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public void PublicationProgressUsesTheSameValueAndEscapesLabels()
    {
        var store = new DocumentStore();
        var note = store.Upsert(new DocumentInput { Path = "Leitura.md", Content = "```progress\nlabel: <leitura>\nvalue: 54\ntotal: 224\n```" });
        using var catalog = new PublicationSourceCatalog(store);
        var output = PublicationHtml.Export(PublicationProject.Create("Livro", note.Id), catalog);
        Assert.Contains("<progress", output.Html);
        Assert.Contains("faltam 170", output.Html);
        Assert.DoesNotContain("<leitura>", output.Html);
        Assert.Contains("&lt;leitura&gt;", output.Html);
    }

    private static async Task Pump(int times = 40)
    {
        for (int i = 0; i < times; i++) { Dispatcher.UIThread.RunJobs(); await Task.Delay(5); }
    }

    private static Button Named(Control root, string name) =>
        root.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == name);

    private static void Click(Button b) => b.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public async Task PlusAndMinusOnTheBarChangeTheValueInTheNoteAndStopAtTheGoal() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(async () =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 360, Height = 780, Content = editor };
            window.Show();
            try
            {
                editor.Open(new CityNote("Livro.md", "```progress\nlabel: Páginas\nvalue: 222\ntotal: 224\n```\n"));
                await Pump();
                // a real press and release, as a finger: the value moves and the block does not turn into a box
                var plus = Named(editor, "Aumentar Páginas");
                var at = plus.TranslatePoint(new Avalonia.Point(plus.Bounds.Width / 2, plus.Bounds.Height / 2), window)!.Value;
                window.MouseDown(at, Avalonia.Input.MouseButton.Left);
                window.MouseUp(at, Avalonia.Input.MouseButton.Left);
                await Pump();
                Assert.Contains("value: 223\n", editor.Editor.Text);
                Assert.DoesNotContain(editor.GetVisualDescendants().OfType<TextBox>(), t => t != editor.Editor && t.IsEffectivelyVisible);
                Click(Named(editor, "Aumentar Páginas"));
                await Pump();
                Assert.Contains("value: 224\n", editor.Editor.Text);
                Assert.False(Named(editor, "Aumentar Páginas").IsEnabled);
                Assert.Contains("faltam 0", AutomationProperties.GetName(editor.GetLogicalDescendants().OfType<ProgressBar>().Single()));
                Click(Named(editor, "Diminuir Páginas"));
                await Pump();
                Assert.Contains("value: 223\n", editor.Editor.Text);
                editor.Undo();
                await Pump();
                Assert.Contains("value: 224\n", editor.Editor.Text);
                return true;
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task PendingTasksOfASectionAreListedAndChoosingOneGoesToIt() =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(async () =>
        {
            var editor = new EditorOverlay();
            var dialogs = new UrbeDialogs();
            editor.Dialogs = dialogs;
            var window = new Window { Width = 360, Height = 780, Content = new Grid { Children = { editor, dialogs } } };
            window.Show();
            try
            {
                const string note = "```progress\nlabel: Hoje\nsource: tasks\nsection: Hoje\n```\n\n# Hoje\n\n- [x] feito\n- [ ] revisar capítulo\n\n# Depois\n\n- [ ] fora\n";
                editor.Open(new CityNote("Plano.md", note));
                await Pump();
                Assert.Contains("1/2", AutomationProperties.GetName(editor.GetLogicalDescendants().OfType<ProgressBar>().Single()));
                Click(Named(editor, "Ver tarefas pendentes de Hoje"));
                await Pump();
                var item = dialogs.GetLogicalDescendants().OfType<Button>()
                    .First(b => b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == "revisar capítulo"));
                Assert.DoesNotContain(dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "fora");
                Click(item);
                await Pump();
                Assert.Equal(note.IndexOf("- [ ] revisar", StringComparison.Ordinal), editor.Editor.CaretIndex);
                return true;
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ProgressCapture() => Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(async () =>
    {
        if (Environment.GetEnvironmentVariable("URBE_SHOT") is not { } path) return true;
        var editor = new EditorOverlay();
        var window = new Window { Width = 360, Height = 640, Content = editor };
        window.Show();
        editor.Open(new CityNote("Plano.md", "# Leitura\n\n```progress\nlabel: Coraline\nvalue: 54\ntotal: 224\n```\n\n```progress\nlabel: Hoje\nsource: tasks\nsection: Hoje\n```\n\n# Hoje\n\n- [x] feito\n- [ ] revisar capítulo\n- [ ] enviar\n"));
        for (int i = 0; i < 30; i++) { Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); await Task.Delay(5); }
        window.CaptureRenderedFrame()!.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        window.Close();
        return true;
    }, TestContext.Current.CancellationToken));
}
