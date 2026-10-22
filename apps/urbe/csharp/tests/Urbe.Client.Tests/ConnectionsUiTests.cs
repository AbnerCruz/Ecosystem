using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>UC-71 (ADD-0029): Mais ações → Conexões shows the note's local graph; a point opens its note and the graph follows.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Connections_ShowTheLocalGraphAndAPointOpensItsNote() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-conn-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(Path.Combine(vault, "Pessoas"));
            File.WriteAllText(Path.Combine(vault, "Centro.md"), "# Centro\n\n[[Amiga]] e [[Citada]].\n");
            File.WriteAllText(Path.Combine(vault, "Pessoas", "Amiga.md"), "# Amiga\n\n[[Centro]]\n");
            File.WriteAllText(Path.Combine(vault, "Citada.md"), "# Citada\n");
            File.WriteAllText(Path.Combine(vault, "Leitor.md"), "# Leitor\n\n[[Centro]]\n");
            File.WriteAllText(Path.Combine(vault, "Solta.md"), "# Solta\n");
            var previous = UrbeApp.LocalStatePath;
            UrbeApp.LocalStatePath = Path.Combine(dir, "app", "notes-state.json");
            try
            {
                var view = new MainView(new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json")));
                var window = new Window { Width = 412, Height = 860, Content = view };
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                await UntilNotesSaved(view);
                var before = Directory.GetFiles(vault, "*", SearchOption.AllDirectories).ToDictionary(f => f, File.ReadAllBytes);

                view.OpenNoteByPath("Centro.md");
                Dispatcher.UIThread.RunJobs();
                var more = view.Editor.GetLogicalDescendants().OfType<Button>().Single(b => ToolTip.GetTip(b) as string == "Mais ações");
                more.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.ActionSheet, "Conexões");
                for (int i = 0; i < 40 && !view.Editor.Connections.IsVisible; i++) await Pump(10);
                var panel = view.Editor.Connections;
                Assert.True(panel.IsVisible);
                Assert.False(view.Editor.Context.IsVisible);
                // grouped: the mutual link, the one it cites, the one citing it
                Assert.Equal(["Amiga", "Citada", "Leitor"], panel.Items);
                var texts = panel.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Conexões · Centro", texts);
                Assert.Contains("LIGAÇÃO MÚTUA · 1", texts);
                Assert.Contains("CITA · 1", texts);
                Assert.Contains("CITADA POR · 1", texts);
                await Pump(30);
                Snapshot(window, "native-connections-phone.png");

                // a tap on Leitor's point: the note opens and the graph follows it
                var at = panel.Graph.PointOf("Leitor.md")!.Value;
                Assert.True(panel.Graph.TapAt(at));
                for (int i = 0; i < 100 && view.Editor.Current?.Path != "Leitor.md"; i++) await Pump(10);
                Assert.Equal("Leitor.md", view.Editor.Current!.Path);
                Assert.True(panel.IsVisible);
                Assert.Equal(["Centro"], panel.Items);
                // the empty sky does nothing
                Assert.False(panel.Graph.TapAt(new Avalonia.Point(2, 2)));

                // a note without links says how to make one
                view.OpenNoteByPath("Solta.md");
                Dispatcher.UIThread.RunJobs();
                view.ShowEditorConnections();
                Assert.Empty(panel.Items);
                Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Esta nota ainda não tem ligações", StringComparison.Ordinal) == true);

                // looking is read-only: nothing in the vault changed
                Assert.Equal(before.Keys.Order(), Directory.GetFiles(vault, "*", SearchOption.AllDirectories).Order());
                foreach (var (file, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(file));
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
