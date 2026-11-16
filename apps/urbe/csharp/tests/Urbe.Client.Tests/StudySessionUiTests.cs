using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core.Study;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#956: a study session keeps its sources by id; opening it shows them and opens one beside the note.</summary>
    [Fact]
    public async Task StudySession_ReferencesSourcesAndOpensOneInEstudo() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-session-" + Guid.NewGuid().ToString("N"));
        var vault = Path.Combine(dir, "vault");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "Newton.md"), "# Newton\n\nF = ma\n");
        File.WriteAllText(Path.Combine(vault, "Anotações.md"), "# Anotações\n");
        var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
        var window = new Window { Width = 412, Height = 860, Content = view };
        window.Show();
        async Task Click(string label)
        {
            bool Has() => view.Dialogs.IsOpen && view.Dialogs.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == label);
            for (int i = 0; i < 200 && !Has(); i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            ClickButtonWithText(view.Dialogs, label);
        }
        async Task Type(string text, string confirm)
        {
            for (int i = 0; i < 200 && !view.Dialogs.GetLogicalDescendants().OfType<TextBox>().Any(); i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
            view.Dialogs.GetLogicalDescendants().OfType<TextBox>().First().Text = text;
            ClickButtonWithText(view.Dialogs, confirm);
            for (int i = 0; i < 5; i++) { await Task.Delay(5); Dispatcher.UIThread.RunJobs(); }
        }
        try
        {
            await view.OpenVaultAsync();
            await Settle(window, view);
            var create = view.CreateStudySessionAsync();
            await Type("Física", "Continuar");
            await Type("Leis de Newton", "Escolher fontes");
            await Click("Newton.md");
            await Click("Concluir");
            var path = await create;
            Assert.Equal("Estudos/Física.estudo.json", path);
            var session = StudySession.Parse(File.ReadAllText(Path.Combine(vault, "Estudos", "Física.estudo.json")));
            Assert.Equal(view.Documents!.Get("Newton.md")!.Id, session.Sources.Single().DocumentId);
            Assert.Equal("Leis de Newton", session.Objectives.Single());
            var open = view.OpenStudySessionAsync(path!);
            await Click("Newton.md");
            await open;
            await Settle(window, view);
            Assert.True(view.StudyOpen);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
