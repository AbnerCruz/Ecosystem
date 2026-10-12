using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>tutorial.js: the Tutorial folder created the first time a city opens, the mark, and Configurações → Tutorial.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Tutorial_IsCreatedOnceLikeTheOriginal() =>
        Assert.True(await Session.Dispatch(TutorialScenario, CancellationToken.None));

    private static async Task<bool> TutorialScenario()
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-tutorial-" + Guid.NewGuid().ToString("N"));
        var empty = Path.Combine(dir, "Vazia");
        var marked = Path.Combine(dir, "Marcada");
        Directory.CreateDirectory(empty);
        Directory.CreateDirectory(Path.Combine(marked, ".urbe"));
        File.WriteAllText(Path.Combine(marked, "Minha.md"), "# Minha\n");
        File.WriteAllText(Path.Combine(marked, ".urbe", "tutorial.json"), "{\"versao\":\"x\",\"em\":\"2025-01-01T00:00:00.000Z\"}");
        MainView.SeedsTutorial = true;
        try
        {
            // an empty city: the whole folder, the mark, the houses, and "Comece aqui" opens
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(empty), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            for (int i = 0; i < 400 && !view.Editor.IsVisible; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            var expected = TutorialNotes.Load();
            foreach (var f in expected)
                Assert.Equal(f.Content, File.ReadAllText(Path.Combine(empty, f.Path)));
            var mark = File.ReadAllText(Path.Combine(empty, ".urbe", "tutorial.json"));
            Assert.StartsWith("{\"versao\":\"" + MainView.TutorialVersion + "\",\"em\":\"", mark);
            Assert.Equal(expected.Count, view.World.City!.Buildings.Count(b => b.IsNote));
            Assert.True(view.World.City.Links.Count > 0 || view.RebuildRoadsNow());
            Assert.True(view.Editor.IsVisible);
            Assert.Equal(LegacyTutorial.Start, view.Editor.Current!.Path);

            // Configurações → Tutorial → Restaurar: nothing is missing
            view.Editor.IsVisible = false;
            await view.RestoreTutorialAsync(false);
            Assert.Contains(view.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "O Tutorial já está completo");
            _ = view.ShowSettingsAsync();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(view.Dialogs.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Guia completo de tudo o que o Urbe faz");
            window.Close();

            // a city with the mark (the 1.8.4 already created it once, even if it was deleted): nothing is created
            var second = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(marked), Path.Combine(dir, "app2", "sel.json")));
            var window2 = new Window { Width = 412, Height = 860, Content = second };
            window2.Show();
            await second.OpenVaultAsync();
            for (int i = 0; i < 60; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
            Assert.False(Directory.Exists(Path.Combine(marked, "Tutorial")));
            Assert.Single(second.World.City!.Buildings, b => b.IsNote);
            window2.Close();
            return true;
        }
        finally
        {
            MainView.SeedsTutorial = false;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }
}
