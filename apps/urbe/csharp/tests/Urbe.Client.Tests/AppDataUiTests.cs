using Avalonia.Controls;
using Urbe.Client;
using Urbe.Core.Assistant;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>#1005 (P5): what the app keeps and where, measured and listed read-only.</summary>
    [Fact]
    public async Task AppData_ListsWhatTheAppKeepsAndWhere() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var dir = Path.Combine(Path.GetTempPath(), "urbe-appdata-" + Guid.NewGuid().ToString("N"));
        var vault = WorkspaceVault(dir);
        Directory.CreateDirectory(Path.Combine(vault, "Personalização"));
        File.WriteAllText(Path.Combine(vault, "Personalização", "tema.json"), "{}");
        var previous = UrbeApp.LocalStatePath;
        UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
        try
        {
            var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.OpenVaultAsync();
            await Settle(window, view);
            await ((IAssistantStudy)view).CreateDeckAsync("Animais", [("a", "b")], null);
            var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Length;
            var items = await view.AppDataAsync();
            var cards = Assert.Single(items, i => i.Name == "Flashcards");
            Assert.Equal(new FileInfo(Path.Combine(vault, ".urbe", "estudo", "flashcards.json")).Length, cards.Bytes);
            Assert.StartsWith("No vault", cards.Group);
            Assert.Contains(items, i => i.Name == "Personalização" && i.Group.StartsWith("No Explorer", StringComparison.Ordinal) && i.Files >= 1);
            Assert.Equal(before, Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Length);   // nothing moved or deleted
            window.Close();
            return true;
        }
        finally
        {
            UrbeApp.LocalStatePath = previous;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }, CancellationToken.None));
}
