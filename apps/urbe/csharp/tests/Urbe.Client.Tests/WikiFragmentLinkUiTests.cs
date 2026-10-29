using Avalonia.Controls;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>CST-120: following [[Nota#Título]] opens the note at that heading, in Visual too; [[#Título]] stays in the note.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Links_AHeadingLinkOpensTheNoteThere() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 760, Content = view };
        window.Show();
        try
        {
            var filler = string.Concat(Enumerable.Range(1, 40).Select(i => "Parágrafo " + i + " com texto.\n\n"));
            var b = "# Plano\n\n" + filler + "## Metas\n\nCorrer e ler.\n\n" + filler + "## Fim\n\nÚltimo ^fim\n";
            await view.LoadAsync([new CityNote("A.md", "# A\n\nVeja [[Plano#Metas]] e [[#Notas]].\n\n## Notas\n\nAqui.\n"), new CityNote("Plano.md", b)]);
            await Settle(window, view);
            view.OpenNoteByPath("A.md");
            await Settle(window, view);
            Assert.Equal(EditorMode.Visual, view.Editor.Mode);
            Assert.Contains(view.Editor.Surface.GetVisualDescendants().OfType<TextBlock>(),
                t => (t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text == "Plano › Metas") ?? false) || t.Text == "Plano › Metas");

            await view.Editor.FollowLinkAsync("Plano#Metas");
            await Settle(window, view);
            Assert.Equal("Plano.md", view.Editor.Current?.Path);
            var text = view.Editor.Editor.Text!;
            Assert.Equal(text.IndexOf("## Metas", StringComparison.Ordinal), view.Editor.Editor.CaretIndex);
            Assert.Equal(text.IndexOf("## Metas", StringComparison.Ordinal), view.Editor.Surface.RevealedBlock);

            await view.Editor.FollowLinkAsync("Plano#^fim");
            await Settle(window, view);
            Assert.Equal(text.IndexOf("Último", StringComparison.Ordinal), view.Editor.Surface.RevealedBlock);

            // a part of the note itself, and one that does not exist
            await view.Editor.FollowLinkAsync("#Fim");
            Assert.Equal("Plano.md", view.Editor.Current?.Path);
            Assert.Equal(text.IndexOf("## Fim", StringComparison.Ordinal), view.Editor.Surface.RevealedBlock);
            await view.Editor.FollowLinkAsync("#Inexistente");
            Assert.Contains("não tem", view.Editor.NoticeText);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));

    [Fact]
    public async Task Links_AfterNoteAndHashTheHeadingsAreSuggested() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 760, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync([new CityNote("A.md", "# A\n\n## Própria\n"), new CityNote("Plano.md", "# Plano\n\n## Metas\n\n## Orçamento\n\nGasto. ^custo\n")]);
            await Settle(window, view);
            view.OpenNoteByPath("A.md");
            await Settle(window, view);
            view.Editor.SetMode(EditorMode.Fonte);
            IReadOnlyList<string> Typed(string text)
            {
                view.Editor.Editor.Text = "# A\n\n## Própria\n\n" + text;
                view.Editor.Editor.CaretIndex = view.Editor.Editor.Text.Length;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                return view.Editor.WikiSuggestions;
            }
            Assert.Equal(["Plano#Plano", "Plano#Metas", "Plano#Orçamento"], Typed("Veja [[Plano#"));
            Assert.Equal(["Plano#Orçamento"], Typed("Veja [[Plano#orç"));
            Assert.Equal(["Plano#^custo"], Typed("Veja [[Plano#^"));
            Assert.Equal(["#A", "#Própria"], Typed("Veja [[#"));   // this note's own
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
