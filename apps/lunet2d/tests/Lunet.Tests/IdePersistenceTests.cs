using System.Diagnostics;
using Avalonia.Controls;
using UiDispatcher = Avalonia.Threading.Dispatcher;
using Lunet.Client;
using Lunet.Client.Views;
using Lunet.Core;

namespace Lunet.Tests;

public sealed class IdePersistenceTests
{
    [Fact]
    public void AFailedWriteLeavesTheDraftAndTheNextEditorRestoresItWithoutOverwritingExternalChanges()
    {
        var root = Directory.CreateTempSubdirectory("lunet-draft-").FullName;
        try
        {
            var project = new ProjectStore(root).Create("Game");
            var draftRoot = Path.Combine(root, ".lunet", "drafts");
            var file = project.Manifest.EntryPoint;
            var previous = project.ReadText(file);
            project.WriteText(file, "// edição externa");
            var journal = new AutosaveJournal(project, draftRoot);
            Assert.Throws<IOException>(() => journal.SaveVerified(file, previous, "// meu rascunho"));
            Assert.Equal("// edição externa", project.ReadText(file));

            UiSession.Run(() =>
            {
                var shell = new LunetShell(root);
                shell.OpenProject("Game");
                var editor = shell.WorkspaceView.Code;
                Assert.Equal("// meu rascunho", editor.Surface.Text);
                Assert.False(editor.Save());
                Assert.Equal("// edição externa", project.ReadText(file));
                Assert.Equal("Recuperado.cs", editor.SaveCopyAs("Recuperado"));
                Assert.Equal("// meu rascunho", project.ReadText("Recuperado.cs"));
                Assert.Null(journal.ReadBuffer(file));
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void EditorAutomaticallySavesAndMeasuresOpeningAndTyping()
    {
        var root = Directory.CreateTempSubdirectory("lunet-editor-save-").FullName;
        try
        {
            UiSession.RunAsync(async () =>
            {
                var project = new ProjectStore(root).Create("Game");
                var file = project.Manifest.EntryPoint;
                var source = project.ReadText(file) + "\n" + string.Join("\n", Enumerable.Repeat("// uma linha grande de código para o teste de latência", 3000));
                project.WriteText(file, source);
                var shell = new LunetShell(root);
                var window = new Window { Width = 412, Height = 915, Content = shell };
                window.Show();
                var watch = Stopwatch.StartNew();
                shell.OpenProject("Game");
                UiDispatcher.UIThread.RunJobs();
                var open = watch.Elapsed.TotalMilliseconds;
                var editor = shell.WorkspaceView.Code;
                var code = (CodeSurface)editor.Surface;
                code.Focus();
                code.Caret = source.Length;
                var ime = new AndroidImeSimulator(window, AndroidImeSimulator.ClientOf(code.Editor.TextArea)!);
                var samples = new List<double>();
                foreach (var ch in " texto recuperavel para medir latencia do editor em um arquivo longo")
                {
                    watch.Restart();
                    ime.CommitText(ch.ToString());
                    UiDispatcher.UIThread.RunJobs();
                    samples.Add(watch.Elapsed.TotalMilliseconds);
                }

                var pending = code.Text;
                var deadline = Stopwatch.StartNew();
                while (deadline.Elapsed < TimeSpan.FromSeconds(4) && (editor.IsDirty || project.ReadText(file) != pending))
                {
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }
                Assert.True(pending == project.ReadText(file), $"Estado do editor: dirty={editor.IsDirty}. {shell.LastReport}");
                Assert.False(editor.IsDirty);
                Assert.Null(new AutosaveJournal(project, shell.RecoveryPath).ReadBuffer(file));
                samples.Sort();
                var p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
                TestContext.Current.TestOutputHelper?.WriteLine($"abertura={open:0.00} ms, digitação p95={p95:0.00} ms, {samples.Count} teclas (headless)");
                Assert.True(open < 4000, $"Abertura: {open:0} ms.");
                Assert.True(p95 < 50, $"Digitação p95: {p95:0.0} ms.");
                window.Close();
                shell.Shutdown();
            });
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
