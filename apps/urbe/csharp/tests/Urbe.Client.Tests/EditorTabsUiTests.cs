using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>Normal navigation reuses a slot; only the explicit action adds a tab.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task EditorTabs_NormalNavigationReusesOneTabAndPreservesTheNoteBeingLeft() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var editor = view.Editor;
                view.OpenNoteByPath("Comece.md");
                editor.SetMode(EditorMode.Fonte);
                editor.Editor.Text = "# Texto que precisa ser salvo\n";
                view.OpenNoteByPath("Bairro/Um.md");
                await Settle(window, view);
                Assert.Equal(["Bairro/Um.md"], editor.Tabs);
                Assert.Equal("# Texto que precisa ser salvo\n", File.ReadAllText(Path.Combine(vault, "Comece.md")));
                view.OpenNoteByPath("Comece.md");
                Assert.Equal(["Comece.md"], editor.Tabs);
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));

    [Fact]
    public async Task EditorTabs_ExplicitOpenSwitchAndClosePreservesChosenSlots() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                var editor = view.Editor;
                CityNote Note(string path) => new(path, File.ReadAllText(Path.Combine(vault, path.Replace('/', Path.DirectorySeparatorChar))));
                editor.Open(Note("Comece.md"));
                Assert.Equal(["Comece.md"], editor.Tabs);
                await editor.OpenInNewTabAsync("Bairro/Um.md");
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

    [Fact]
    public async Task EditorTabs_PlusIsReachableWithOneTabAndUsesTheExistingNotePicker() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            window.Width = 320;
            await ((IAssistantApp)view).CreateNoteAsync("Bairro/Dois.md", "# Dois\n");
            view.OpenNoteByPath("Comece.md");
            await Settle(window, view);
            var add = view.Editor.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationProperties.GetName(b) == "Abrir nova aba");
            Assert.True(add.IsEffectivelyVisible);
            Assert.True(add.Bounds.Width >= 48 && add.Bounds.Height >= 48);
            var at = add.TranslatePoint(default, window)!.Value;
            Assert.InRange(at.X, 0, window.Bounds.Width - add.Bounds.Width);
            add.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Assert.True(view.Search.QuickOpen.IsOpen);
            view.Search.QuickOpen.Close();
            Assert.Equal(["Comece.md"], view.Editor.Tabs);
            // Reopening before the prior close callback runs must keep this new selection alive.
            add.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            view.Search.QuickOpen.Input.Text = "Bairro/Um";
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await Settle(window, view);
            Assert.Equal(["Comece.md", "Bairro/Um.md"], view.Editor.Tabs);
            Assert.Equal("Bairro/Um.md", view.Editor.Current!.Path);
            // The extra slot remains; normal navigation replaces only the active one.
            view.OpenNoteByPath("Bairro/Dois.md");
            Assert.Equal(["Comece.md", "Bairro/Dois.md"], view.Editor.Tabs);
            Snapshot(window, "native-explicit-tabs-320.png");
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));

    [Fact]
    public async Task EditorTabs_RestoresLegacyHistoryAsOneTabAndPersistsExplicitClosures() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            view.Customization.Device = new MemoryDeviceStore();
            view.Customization.Device.Set("urbe.editor.workspace.v1", new System.Text.Json.Nodes.JsonObject
            {
                ["tabs"] = new System.Text.Json.Nodes.JsonArray("Comece.md", "Bairro/Um.md"),
                ["activeId"] = "Bairro/Um.md", ["vault"] = ((IAssistantApp)view).VaultName
            }.ToJsonString());
            Assert.True(view.RestoreSession());
            Assert.Equal(["Bairro/Um.md"], view.Editor.Tabs);
            await view.Editor.OpenInNewTabAsync("Comece.md");
            await view.Editor.CloseTabAsync("Bairro/Um.md");
            Assert.True(view.RestoreSession());
            Assert.Equal(["Comece.md"], view.Editor.Tabs);
            await view.Editor.CloseTabAsync("Comece.md");
            Assert.False(view.RestoreSession());
            Assert.Empty(view.Editor.Tabs);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
