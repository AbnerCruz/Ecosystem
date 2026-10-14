using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>DEC-0045 (UC-43): how a #Tag: paragraph and a %%comment%% look in the rendered note.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task NoteMarks_TagsAndCommentsAreDrawnApart() => Assert.True(await Session.Dispatch(() =>
    {
        var view = new MarkdownReader().Build(NoteMarks.Parse("#Todo: revisar a tradução\n\nTexto %%lembrar disto%% normal."));
        var window = new Window { Width = 412, Height = 300, Content = view };
        window.Show();
        var texts = view.GetLogicalDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(texts, t => t.Text == "#Todo");
        var tagged = view.GetLogicalDescendants().OfType<Border>().Single(b => b.BorderThickness.Left == 3);
        Assert.Equal(UrbeTheme.Warn, UrbeTheme.TagColor("Todo"));
        Assert.NotNull(tagged.Background);
        var comment = texts.SelectMany(t => t.Inlines ?? []).OfType<Run>().Single(r => r.Text == "lembrar disto");
        Assert.Equal(Avalonia.Media.FontStyle.Italic, comment.FontStyle);
        Assert.Equal(UrbeTheme.TagColor("Projeto X"), UrbeTheme.TagColor("projeto x"));   // same tag, same colour
        Snapshot(window, "native-note-marks.png");
        window.Close();
        return Task.FromResult(true);
    }, CancellationToken.None));
}
