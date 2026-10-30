using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// CST-019: the formatting bar is arranged by touch in Personalização → Editor, without writing JSON by hand. It is the
/// same Personalização/editor.json, whose "/" menu is kept.
/// </summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Customize_TheBarIsArrangedByTouchAndKeepsTheSlashMenu() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var menu = "{\n  \"versao\": 1,\n  \"menu\": [\"h1\", \"table\"]\n}\n";
        var (view, window, dir, vault) = await VaultViewAsync(new() { [EditorLayout.Path] = menu }, width: 412, height: 900);
        var file = Path.Combine(vault, EditorLayout.Path);
        Button Named(string name) => view.Customize.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == name);
        async Task Tap(string name)
        {
            Named(name).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            for (int i = 0; i < 50; i++) { await Task.Delay(10); Dispatcher.UIThread.RunJobs(); }
        }
        try
        {
            view.Customize.Open("editor");
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("BARRA DE FORMATAÇÃO", Texts(view.Customize));
            Snapshot(window, "native-customize-bar.png");

            // Citação out of the bar, Negrito one place down
            await Tap("Tirar Citação da barra");
            await Tap("Descer Negrito");
            var bar = (System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!["barra"] as System.Text.Json.Nodes.JsonArray)!.Select(x => (string)x!).ToList();
            Assert.DoesNotContain("quote", bar);
            Assert.True(bar.IndexOf("italic") < bar.IndexOf("bold"));
            Assert.Equal(["h1", "table"], System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!["menu"]!.AsArray().Select(x => (string)x!));
            // the editor's bar follows at once
            Assert.DoesNotContain(view.Editor.ToolbarNames, n => n.StartsWith("Citação", StringComparison.Ordinal));
            Assert.True(view.Editor.ToolbarNames.ToList().FindIndex(n => n.StartsWith("Itálico", StringComparison.Ordinal)) <
                        view.Editor.ToolbarNames.ToList().FindIndex(n => n.StartsWith("Negrito", StringComparison.Ordinal)));

            // back into the bar, at the end
            await Tap("Pôr Citação na barra");
            Assert.StartsWith("Citação", view.Editor.ToolbarNames[^1]);

            // Padrão: the default bar, the menu still there
            await Tap("Barra padrão");
            Assert.Null(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!["barra"]);
            Assert.NotNull(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(file))!["menu"]);
            Assert.Equal(EditorOverlay.ToolbarOrder.Count(x => x != "|"), view.Editor.ToolbarNames.Count(n => n != "|"));
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
