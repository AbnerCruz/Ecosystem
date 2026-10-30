using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-042 (DEC-0058): the tags of a project's folder note reach its notes. The Área de trabalho filters by them, and the
/// note's properties show each inherited tag with the folder it comes from; × refuses one with sem-tags.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task ProjectTags_TheBoxFiltersByAnInheritedTagAndTheNoteShowsWhereItComesFrom() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-project-tags-" + Guid.NewGuid().ToString("N"));
            var vault = WorkspaceVault(dir);
            File.WriteAllText(Path.Combine(vault, "Personagens", "Personagens.md"), "---\ntags: [worldbuilding]\n---\n# Personagens\n");
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);

                // the box of the project offers the inherited tag, and it picks the notes inside, not the others
                var w = view.Workspace;
                w.Open("Personagens");
                Click(Named(w, "#worldbuilding"));
                var shown = w.GetLogicalDescendants().OfType<Button>()
                    .Where(b => b.IsEffectivelyVisible && ToolTip.GetTip(b) is string tip && tip.StartsWith("Personagens/", StringComparison.Ordinal))
                    .Select(AutomationProperties.GetName).ToHashSet();
                Assert.Contains("Herói", shown);
                Assert.Contains("Vilã", shown);
                Click(Named(w, "#worldbuilding"));

                // the note: the tag shown with its origin, and nothing written in it
                view.OpenNoteByPath("Personagens/Herói.md");
                await Settle(window, view);
                var panel = view.Editor.Properties;
                panel.Expand(true);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var texts = panel.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("herdadas", texts);
                Assert.Contains("de Personagens", texts);
                Assert.Contains(panel.GetVisualDescendants().OfType<Control>(), c => AutomationProperties.GetName(c) == "#worldbuilding · de Personagens");
                // already inherited, it is not offered as a tag to add
                Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => AutomationProperties.GetName(b) == "Usar a tag worldbuilding");
                Assert.DoesNotContain("worldbuilding", view.Editor.Current!.Content);
                Snapshot(window, "native-project-tags.png");

                // × refuses it for this note only
                var refuse = panel.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Não herdar worldbuilding");
                refuse.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(["worldbuilding"], NoteProperties.Parse(view.Editor.Current!.Content).Get("sem-tags")!.Items);
                Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Control>(), c => AutomationProperties.GetName(c) == "#worldbuilding · de Personagens");
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
