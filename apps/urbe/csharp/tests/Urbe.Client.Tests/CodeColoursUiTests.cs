using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // CST-141: a code file is shown in colour; while typing it is plain, and the colours come back a moment after
    [Fact]
    public async Task Code_AFileOfCodeIsShownInColour() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 900, Height = 760, Content = view };
        window.Show();
        try
        {
            var code = "// conta\nconst total = soma(2, 3); // fim\nfunction soma(a, b) {\n    return a + b + \"!\";\n}\n";
            await view.LoadAsync([new CityNote("app.js", code), new CityNote("Nota.md", "# Nota\n\nconst não é código aqui.\n")]);
            await Settle(window, view);
            view.OpenNoteByPath("app.js");
            await Settle(window, view);
            Assert.True(view.Editor.CodeColoured);
            // a code file has one way to be seen: no Visual | Fonte switch that would do nothing
            Assert.DoesNotContain(Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(view.Editor).OfType<Button>(), b => b.Content as string == "Visual" && b.IsEffectivelyVisible);
            var colours = view.Editor.CodeColours;
            Assert.Contains(colours, c => c.Text == "const");
            Assert.Contains(colours, c => c.Text == "// conta");
            Assert.Contains(colours, c => c.Text == "\"!\"");
            Assert.NotEqual(colours.First(c => c.Text == "const").Colour!.ToString(), colours.First(c => c.Text == "// conta").Colour!.ToString());
            Snapshot(window, "native-code-colours.png");
            // diagnostic: the box's own letters in red over the colours; they must fall on the same places
            var ink = view.Editor.Editor.Foreground;
            view.Editor.Editor.Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 0, 0));
            await Settle(window, view);
            Snapshot(window, "native-code-colours-alignment.png");
            view.Editor.Editor.Foreground = ink;

            view.Editor.Editor.Text = code + "let x = 1;\n";
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.Editor.CodeColoured);   // typing: the plain text, never colours of another text
            for (int i = 0; i < 50 && !view.Editor.CodeColoured; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            Assert.True(view.Editor.CodeColoured);
            Assert.Contains(view.Editor.CodeColours, c => c.Text == "let");

            view.OpenNoteByPath("Nota.md");
            await Settle(window, view);
            Assert.False(view.Editor.CodeColoured);   // a note is not code
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
