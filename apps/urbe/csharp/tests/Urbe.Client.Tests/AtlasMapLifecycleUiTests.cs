using Avalonia.Controls;
using Avalonia.Threading;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>Atlas lifecycle contract: native preview must never discard unsaved HTML without a decision.</summary>
public sealed partial class HeadlessUiTests
{
    private sealed class FakeAtlasViewer : Border, IAtlasMapPendingEdits
    {
        public bool Pending { get; set; } = true;
        public bool SaveSucceeds { get; set; } = true;
        public int Saves { get; private set; }
        public Task<bool> HasPendingEditsAsync() => Task.FromResult(Pending);
        public Task<bool> SavePendingEditsAsync()
        {
            Saves++;
            if (SaveSucceeds) Pending = false;
            return Task.FromResult(SaveSucceeds);
        }
    }

    [Fact]
    public async Task AtlasPreview_RequiresExplicitDecisionAndBlocksFailedSave() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var dir = Path.Combine(Path.GetTempPath(), "urbe-map-guard-" + Guid.NewGuid().ToString("N"));
            var vault = Path.Combine(dir, "vault");
            Directory.CreateDirectory(vault);
            File.WriteAllText(Path.Combine(vault, "Mapa.html"), "<!doctype html><html><head></head><body></body></html>");
            var storage = new FolderVaultStorage(_ => Task.FromResult<string?>(vault), Path.Combine(dir, "app", "sel.json"));
            var view = new MainView(storage);
            var window = new Window { Width = 412, Height = 860, Content = view };
            var original = ContentViewers.Factory;
            var map = new FakeAtlasViewer();
            try
            {
                ContentViewers.Factory = (req, _) => Task.FromResult<Control?>(
                    req.Kind == VaultFileKinds.Html ? map : null);
                window.Show();
                await view.OpenVaultAsync();
                await Settle(window, view);
                await view.FilePreview.OpenAsync("Mapa.html");
                Assert.True(view.FilePreview.IsVisible);
                Assert.Equal("viewer", view.FilePreview.Shown);

                // Without native dialogs, there can be no silent destruction.
                view.FilePreview.Dialogs = null;
                Assert.False(await view.FilePreview.TryCloseAsync());
                Assert.True(view.FilePreview.IsVisible);
                view.FilePreview.Dialogs = view.Dialogs;

                // Failed write: the panel stays open and the dirty state is preserved.
                map.SaveSucceeds = false;
                var close = view.FilePreview.TryCloseAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.Dialogs.IsOpen);
                ClickButtonWithText(view.Dialogs, "Salvar e continuar");
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.Dialogs.IsOpen);
                ClickButtonWithText(view.Dialogs, "Ok");
                Assert.False(await close);
                Assert.True(view.FilePreview.IsVisible && map.Pending);

                // A second attempt succeeds only after the map reports its data persisted.
                map.SaveSucceeds = true;
                close = view.FilePreview.TryCloseAsync();
                Dispatcher.UIThread.RunJobs();
                ClickButtonWithText(view.Dialogs, "Salvar e continuar");
                Assert.True(await close);
                Assert.False(view.FilePreview.IsVisible);
                Assert.False(map.Pending);
                Assert.Equal(2, map.Saves);
                window.Close();
                return true;
            }
            finally
            {
                ContentViewers.Factory = original;
                window.Close();
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));

    [Fact]
    public async Task AtlasEditorNavigation_RespectsPendingMapCancellation() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync(TutorialNotes.Load());
                await Settle(window, view);
                var editor = view.Editor;
                var first = new CityNote("Mapa.md", "# Mapa");
                var second = new CityNote("Destino.md", "# Destino");
                editor.Open(first);
                editor.ConfirmEmbeddedAtlasExitAsync = () => Task.FromResult(false);
                await editor.NavigateAsync(second);
                Assert.Equal("Mapa.md", editor.Current?.Path);
                await editor.BackAsync();
                Assert.True(editor.IsVisible);
                editor.Open(second);
                Assert.Equal("Mapa.md", editor.Current?.Path);

                editor.ConfirmEmbeddedAtlasExitAsync = () => Task.FromResult(true);
                await editor.NavigateAsync(second);
                Assert.Equal("Destino.md", editor.Current?.Path);
                return true;
            }
            finally { window.Close(); }
        }, CancellationToken.None));
}
