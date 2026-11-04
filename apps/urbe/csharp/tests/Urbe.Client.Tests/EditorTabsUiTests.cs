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
    public async Task EditorTabs_ANewTabComesFromTheMenuAndTheStripOnlyShowsWithTwoTabs() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            window.Width = 320;
            await ((IAssistantApp)view).CreateNoteAsync("Bairro/Dois.md", "# Dois\n");
            view.OpenNoteByPath("Comece.md");
            await Settle(window, view);
            // one note: no strip taking a row of the phone's screen
            var add = view.Editor.GetLogicalDescendants().OfType<Button>()
                .Single(b => AutomationProperties.GetName(b) == "Abrir nova aba");
            Assert.False(add.IsEffectivelyVisible);
            // the editor's menu offers the new tab, through the same note picker
            view.Search.OpenNewTabPicker();
            Assert.True(view.Search.QuickOpen.IsOpen);
            view.Search.QuickOpen.Close();
            Assert.Equal(["Comece.md"], view.Editor.Tabs);
            // Reopening before the prior close callback runs must keep this new selection alive.
            view.Search.OpenNewTabPicker();
            view.Search.QuickOpen.Input.Text = "Bairro/Um";
            Dispatcher.UIThread.RunJobs();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            await Settle(window, view);
            Assert.Equal(["Comece.md", "Bairro/Um.md"], view.Editor.Tabs);
            Assert.Equal("Bairro/Um.md", view.Editor.Current!.Path);
            // two tabs: the strip shows, with + at its end, inside the screen
            Assert.True(add.IsEffectivelyVisible);
            Assert.True(add.Bounds.Height >= 40);
            var at = add.TranslatePoint(default, window)!.Value;
            Assert.InRange(at.X, 0, window.Bounds.Width - add.Bounds.Width);
            // The extra slot remains; normal navigation replaces only the active one.
            view.OpenNoteByPath("Bairro/Dois.md");
            Assert.Equal(["Comece.md", "Bairro/Dois.md"], view.Editor.Tabs);
            await Settle(window, view);
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
    [Fact]
    public async Task EditorTabs_FlatTabsKeepTheirNamesAndAWholeTabTouchTarget()
    {
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                window.Width = 320;
                view.OpenNoteByPath("Comece.md");
                await view.Editor.OpenInNewTabAsync("Bairro/Um.md");
                await Settle(window, view);
                var tab = view.Editor.GetLogicalDescendants().OfType<Button>()
                    .Single(b => AutomationProperties.GetName(b) == "Aba Comece.md");
                Assert.True(tab.Bounds.Height >= 40, "A aba inteira é a área de toque");
                Assert.Equal("Comece", tab.GetLogicalDescendants().OfType<TextBlock>().First().Text);
                // only the active tab carries its ×: the others keep their width for the name
                Assert.Empty(tab.GetLogicalDescendants().OfType<Button>());
                var active = view.Editor.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Aba Um.md");
                Assert.Single(active.GetLogicalDescendants().OfType<Button>());
                Assert.True(tab.IsEffectivelyVisible);
                return true;
            }
            finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
        }, CancellationToken.None));
    }

}
