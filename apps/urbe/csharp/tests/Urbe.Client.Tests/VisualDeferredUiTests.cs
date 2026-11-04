using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    /// <summary>
    /// A long note opens with its first screens drawn; the blocks further down are made as they come near. Before, every
    /// block of the note was drawn when it opened (a 14 KB note: ~110 ms here, several times that on a phone).
    /// </summary>
    [Fact]
    public async Task Visual_ALongNoteDrawsItsFirstScreensAndTheRestAsTheyComeNear() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var text = BenchNote();
        var (editor, window) = StandaloneEditor(text, 860);
        try
        {
            await Pump(window);
            var surface = editor.Surface;
            var waiting = surface.Children.OfType<Ui.Deferred>().Count(d => !d.IsMade);
            Assert.True(waiting > 50, waiting + " blocos esperando");
            // the last block, revealed from far away, is drawn and on the screen
            var last = surface.BlockStarts[^1];
            surface.RevealOffset(last);
            await Pump(window);
            var view = surface.BlockView(last)!;
            var scroll = surface.FindAncestorOfType<ScrollViewer>()!;
            var top = view.TranslatePoint(default, scroll)!.Value.Y;
            Assert.InRange(top, -view.Bounds.Height, scroll.Viewport.Height);
            // scrolling back up makes the blocks it passes
            scroll.Offset = new Vector(0, scroll.Extent.Height / 2);
            await Pump(window);
            Assert.True(surface.Children.OfType<Ui.Deferred>().Count(d => !d.IsMade) < waiting);
            // editing a block far down keeps the whole text
            var target = surface.BlockStarts.Where(s => text.AsSpan(s).StartsWith("Parágrafo")).Last();
            var holder = surface.BlockView(target)!;
            holder.BringIntoView();
            await Pump(window);
            var line = holder.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains(MarkdownReader.TextClass));
            surface.TapBlock(target, holder, line, line.TranslatePoint(new Point(30, line.LineHeight / 2), holder)!.Value, false);
            await Pump(window);
            Assert.NotNull(surface.EditBox);
            surface.EditBox!.CaretIndex = 0;
            window.KeyTextInput("Novo: ");
            surface.FinishEdit();
            await Pump(window);
            Assert.Equal(text.Length + "Novo: ".Length, surface.Text.Length);
            Assert.Contains("Novo: Parágrafo", surface.Text);
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}

public sealed partial class HeadlessUiTests
{
    /// <summary>On a phone a reader is the whole screen; a tap on the picture hides the bars and Back brings them back.</summary>
    [Fact]
    public async Task Reader_OnAPhoneIsFullScreenAndATapHidesItsBars() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, vault, dir) = await OpenCreateVault();
        try
        {
            window.Width = 390;
            using (var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(400, 300)))
                bmp.Save(Path.Combine(vault, "foto.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
            await view.FilePreview.OpenAsync("foto.png");
            await Settle(window, view);
            var card = view.FilePreview.Children.OfType<Border>().First();
            Assert.Equal(window.Bounds.Width, card.Bounds.Width, 1);
            Assert.Equal(window.Bounds.Height, card.Bounds.Height, 1);
            view.FilePreview.SetChromeHidden(true);
            Assert.True(view.FilePreview.ChromeHidden);
            Assert.True(view.HandleSystemBack());
            Assert.False(view.FilePreview.ChromeHidden);
            Assert.True(view.FilePreview.IsVisible);
            Assert.True(view.HandleSystemBack());
            Assert.False(view.FilePreview.IsVisible);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }, CancellationToken.None));
}
