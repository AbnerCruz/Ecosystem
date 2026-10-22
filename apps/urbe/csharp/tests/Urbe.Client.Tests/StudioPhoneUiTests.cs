using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>
/// Owner's report (UC-48): on the phone the page studio did not work — the preview (a platform view, drawn over the
/// app) covered the sheets and menus, nothing could be chosen and nothing could be written in the preview.
/// </summary>
public sealed partial class HeadlessUiTests
{
    private sealed class FakeNativePreview : Avalonia.Controls.NativeControlHost, IHtmlReloadable
    {
        public List<string> Posted { get; } = [];
        public List<string> Reloaded { get; } = [];
        public void Reload(string html) => Reloaded.Add(html);
        public void Post(string json) => Posted.Add(json);
    }

    [Fact]
    public async Task Studio_OnAPhoneTheSheetIsUnderThePreviewAndThePreviewHidesUnderMenus() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var before = ContentViewers.Factory;
            FakeNativePreview? fake = null;
            ContentViewers.Factory = (request, _) => Task.FromResult<Control?>(request.Messages is null ? null : fake = new FakeNativePreview());
            var (view, window, vault, dir) = await OpenCreateVault();
            try
            {
                window.Width = 400;
                window.Height = 860;
                await view.NewPageAsync();
                for (int i = 0; i < 100 && (view.Studio.Path is null || fake is null); i++) await Pump(20);
                Assert.NotNull(fake);
                var st = view.Studio.Model!;
                Assert.False(view.Studio.Wide);

                // a tap on a part of the page selects it: the Editar sheet opens under the preview, and the part is marked
                var secId = (string)st.Sections[0]!["id"]!;
                var headId = (string)st.Get("sections.0.props.root.children.0.id")!;
                view.Studio.PreviewMessage("{\"urbePage\":\"select\",\"id\":\"" + secId + "\",\"nid\":\"" + headId + "\"}");
                await Pump(120);
                Assert.Equal("edit", view.Studio.Sheet);
                Assert.Contains(fake!.Posted, p => p.Contains("\"host\":true") && p.Contains(headId));
                var sheet = view.Studio.GetVisualDescendants().OfType<Border>().First(b => b.Child is DockPanel d && d.Children.OfType<DockPanel>().Any(h => h.Children.OfType<TextBlock>().Any()) && b.IsVisible && b.CornerRadius.TopLeft == 18);
                var previewBottom = fake.TranslatePoint(new Point(0, fake.Bounds.Height), view)!.Value.Y;
                var sheetTop = sheet.TranslatePoint(new Point(0, 0), view)!.Value.Y;
                Assert.True(sheetTop >= previewBottom - 1, $"sheet {sheetTop} over the preview {previewBottom}");

                // the block library (over everything) hides the preview while it is open
                view.Studio.OpenLibrary();
                await Pump(400);
                Assert.False(fake.IsVisible);
                var hero = view.Studio.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == "Capa");
                hero.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Pump(400);
                Assert.True(fake.IsVisible);

                // leaving the studio lets the preview go: it never stays over the other screens
                await view.Studio.CloseAsync();
                Assert.Null(fake.Parent);
                window.Close();
                return true;
            }
            finally
            {
                ContentViewers.Factory = before;
                try { Directory.Delete(dir, true); } catch (IOException) { }
            }
        }, CancellationToken.None));
}
