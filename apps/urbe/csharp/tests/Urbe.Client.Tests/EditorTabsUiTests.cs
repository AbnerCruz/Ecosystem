using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>v21OpenTabs: every note opened is a tab; tapping one switches (saving first); × closes it.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task EditorTabs_OpenSwitchAndCloseLikeThe184() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var editor = view.Editor;
                CityNote Note(string path) => new(path, File.ReadAllText(Path.Combine(vault, path.Replace('/', Path.DirectorySeparatorChar))));
                editor.Open(Note("Comece.md"));
                Assert.Equal(["Comece.md"], editor.Tabs);
                editor.Open(Note("Bairro/Um.md"));
                Assert.Equal(["Comece.md", "Bairro/Um.md"], editor.Tabs);
                Dispatcher.UIThread.RunJobs();
                Snapshot(window, "native-editor-tabs.png");

                // switching saves the open note first
                editor.Editor.Text = "# Um\n\neditado\n";
                await editor.SwitchTabAsync("Comece.md");
                Assert.Equal("Comece.md", editor.Current!.Path);
                Assert.Equal("# Um\n\neditado\n", File.ReadAllText(Path.Combine(vault, "Bairro", "Um.md")));

                // × of the open tab: the last one left opens; × of the last closes the editor
                await editor.CloseTabAsync("Comece.md");
                Assert.Equal("Bairro/Um.md", editor.Current!.Path);
                Assert.Equal(["Bairro/Um.md"], editor.Tabs);
                Assert.True(editor.IsVisible);
                await editor.CloseTabAsync("Bairro/Um.md");
                Assert.Empty(editor.Tabs);
                Assert.False(editor.IsVisible);
                window.Close();
                return true;
            }
            finally { try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
}
