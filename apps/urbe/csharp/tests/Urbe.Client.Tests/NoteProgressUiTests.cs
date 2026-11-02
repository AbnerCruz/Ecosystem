using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
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
}
