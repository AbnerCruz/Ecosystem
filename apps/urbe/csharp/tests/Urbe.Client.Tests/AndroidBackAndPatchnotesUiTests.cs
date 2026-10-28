using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task AndroidBackClosesTopLayerBeforeEditorAndNeverKillsAppInsideNavigation() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            try
            {
                view.Editor.Open(new CityNote("Teste.md", "# Teste"));
                Assert.True(view.Editor.IsVisible);
                Assert.True(view.HandleSystemBack());
                Assert.False(view.Editor.IsVisible);

                view.Assistant.Open();
                Assert.True(view.Assistant.IsVisible);
                Assert.NotNull(view.Assistant.ViewTitle);
                Assert.True(view.HandleSystemBack()); // closes nested Agentes screen first
                Assert.True(view.Assistant.IsVisible);
                Assert.Null(view.Assistant.ViewTitle);
                Assert.True(view.HandleSystemBack()); // closes assistant overlay
                Assert.False(view.Assistant.IsVisible);

                var confirmed = view.Dialogs.ConfirmAsync("Confirma?", null);
                Assert.True(view.Dialogs.IsOpen);
                Assert.True(view.HandleSystemBack());
                Assert.False(await confirmed);
                Assert.False(view.Dialogs.IsOpen);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));

    [Fact]
    public async Task AndroidBackGoesUpTheFoldersOfTheWorkspaceBeforeLeaving() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            // CST-010 (owner: "o Voltar fecha o aplicativo"): inside a project, Voltar walks back up, then lets the app close
            var dir = Path.Combine(Path.GetTempPath(), "urbe-back-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Projeto", "Capítulos"));
            File.WriteAllText(Path.Combine(vault, "Projeto", "Capítulos", "Um.md"), "# Um\n");
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.OpenVaultAsync();
                await Settle(window, view);
                view.Workspace.Open("Projeto/Capítulos");
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.HandleSystemBack());
                Assert.Equal("Projeto", view.Workspace.Folder);
                Assert.True(view.HandleSystemBack());
                Assert.Equal("", view.Workspace.Folder);
                Assert.False(view.HandleSystemBack());   // the root: the Activity may close the app
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public void EmbeddedPatchnotesComeFromCanonicalPreparedVersion()
    {
        // the version the Android head publishes is the one authority (NN-001): the embedded notes follow it
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Urbe.Portable.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var csproj = File.ReadAllText(Path.Combine(root!.FullName, "src", "Urbe.Android", "Urbe.Android.csproj"));
        var version = System.Text.RegularExpressions.Regex.Match(csproj, "<UrbeBetaVersion>([^<]+)</UrbeBetaVersion>").Groups[1].Value;
        Assert.NotEmpty(version);
        var notes = MainView.CurrentPatchnotes();
        Assert.Contains("**Versão canônica preparada:** " + version, notes);
        Assert.Contains("publicação", notes, StringComparison.OrdinalIgnoreCase);
    }
}
