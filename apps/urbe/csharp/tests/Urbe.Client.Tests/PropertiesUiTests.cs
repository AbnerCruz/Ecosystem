using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>24/10/2026: the properties form (aliases, tags…) and the reading mode of the editor.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Properties_AreEditedByAFormInBothModesAndReadingFreezesThem() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 900, Content = editor };
            window.Show();
            try
            {
                editor.Properties.TagSource = () => ["ideia", "projeto"];
                editor.Open(new CityNote("Notas/Plano.md", "---\ntitle: Plano\naliases:\n  - P1\ntags: [ideia]\n---\n\n# Corpo\n\ntexto\n"));
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var panel = editor.Properties;
                Assert.True(panel.IsVisible);
                // the frontmatter card of the Visual mode is not drawn twice: the form shows it
                Assert.DoesNotContain(editor.Surface.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("urbe-embed"));
                panel.Expand(true);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var texts = panel.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("aliases", texts);
                Assert.Contains("tags", texts);
                Assert.Contains("#ideia", texts);
                Assert.Contains("P1", texts);
                Snapshot(window, "native-properties-form.png");

                // a new alias, typed and confirmed with Enter, goes to the note's text (Visual)
                var input = panel.GetVisualDescendants().OfType<TextBox>().First(t => t.PlaceholderText == "adicionar");   // the aliases row comes first
                input.Focus();
                input.Text = "Plano de ação";
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(["P1", "Plano de ação"], NoteProperties.Parse(editor.Current!.Content).Aliases);
                Assert.Contains("# Corpo", editor.Current.Content);
                Assert.Contains("title: Plano", editor.Current.Content);

                // a tag offered from the vault is one tap away
                var suggestion = panel.GetVisualDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Usar a tag projeto");
                suggestion.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(["ideia", "projeto"], NoteProperties.Parse(editor.Current!.Content).Tags);

                // the same in Fonte
                editor.SetMode(EditorMode.Fonte);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Same(editor.Properties.Parent, editor.Properties.Parent);
                var chip = panel.GetVisualDescendants().OfType<Button>().First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Remover P1");
                chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(["Plano de ação"], NoteProperties.Parse(editor.Editor.Text!).Aliases);

                // reading: nothing can be changed, in Fonte or in Visual
                var before = editor.Editor.Text;
                editor.ReadOnly = true;
                Dispatcher.UIThread.RunJobs();
                Assert.True(editor.Editor.IsReadOnly);
                Snapshot(window, "native-reading-mode.png");
                Assert.All(panel.GetVisualDescendants().OfType<TextBox>(), t => Assert.True(t.IsReadOnly));
                Assert.DoesNotContain(panel.GetVisualDescendants().OfType<TextBox>(), t => t.PlaceholderText == "adicionar");
                Assert.DoesNotContain(panel.GetVisualDescendants().OfType<Button>(), b => Avalonia.Automation.AutomationProperties.GetName(b)?.StartsWith("Remover", StringComparison.Ordinal) == true);
                editor.SetMode(EditorMode.Visual);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var start = editor.Surface.BlockStarts.Last();
                editor.Surface.TapBlock(start, editor.Surface.BlockView(start)!, null, new Avalonia.Point(5, 5), select: false);
                Dispatcher.UIThread.RunJobs();
                Assert.False(editor.Surface.IsEditing);
                Assert.Equal(before, editor.Editor.Text);
                editor.ReadOnly = false;
                Dispatcher.UIThread.RunJobs();
                Assert.False(editor.Editor.IsReadOnly);
                editor.Surface.TapBlock(start, editor.Surface.BlockView(start)!, null, new Avalonia.Point(5, 5), select: false);
                Dispatcher.UIThread.RunJobs();
                Assert.True(editor.Surface.IsEditing);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
