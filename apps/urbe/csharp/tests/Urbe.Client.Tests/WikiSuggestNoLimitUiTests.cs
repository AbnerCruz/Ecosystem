using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // DEC-0059: "[[" listed at most 20 notes and 20 files (12 and 8 with nothing typed); the rest could not be picked
    [Fact]
    public async Task Links_TheListAfterBracketsReachesEveryNote() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 760, Content = view };
        window.Show();
        try
        {
            var notes = Enumerable.Range(1, 150).Select(i => new CityNote($"Nota {i:000}.md", $"# Nota {i:000}\n")).Prepend(new CityNote("Aqui.md", "# Aqui\n")).ToList();
            await view.LoadAsync(notes);
            await Settle(window, view);
            view.OpenNoteByPath("Aqui.md");
            await Settle(window, view);
            view.Editor.SetMode(EditorMode.Fonte);
            view.Editor.Editor.Text = "# Aqui\n\n[[Nota";
            view.Editor.Editor.CaretIndex = view.Editor.Editor.Text.Length;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(150, view.Editor.WikiSuggestions.Count);
            view.Editor.Editor.Focus();
            for (int i = 0; i < 120; i++) window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            var chosen = view.Editor.WikiSuggestions[120];
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("[[" + chosen + "]]", view.Editor.Editor.Text);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
