using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Urbe.Client;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    // CST-141: an HTML of the vault being edited is seen as a page that follows the code, with no save and no refresh
    [Fact]
    public async Task Code_AnHtmlPreviewFollowsTheCodeAsItIsWritten() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var (view, window, dir, vault) = await OpenRealVaultAsync(
            ("Site/index.html", "<html><head><link rel=\"stylesheet\" href=\"estilo.css\"></head><body><h1>Olá</h1><img src=\"logo.png\"></body></html>"),
            ("Site/estilo.css", "h1{color:teal}"),
            ("Site/logo.png", "PNGDATA"),
            ("Nota.md", "# Nota\n"));
        try
        {
            window.Width = 1280;
            await Settle(window, view);
            view.OpenNoteByPath("Site/index.html");
            await Settle(window, view);
            var buttons = view.Editor.GetLogicalDescendants().OfType<Button>().ToList();
            Assert.Contains(buttons, b => b.Content as string == "Código" && b.IsEffectivelyVisible);
            var preview = buttons.First(b => b.Content as string == "Prévia");
            preview.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            async Task<string?> ShownWhen(Func<string, bool> ok)
            {
                for (int i = 0; i < 100; i++)
                {
                    if (view.Editor.LivePane.Shown is { } s && ok(s)) return s;
                    await Task.Delay(20);
                    Dispatcher.UIThread.RunJobs();
                }
                return view.Editor.LivePane.Shown;
            }
            Assert.True(view.Editor.LivePane.IsVisible);
            var shown = await ShownWhen(s => s.Contains("<h1>Olá</h1>"));
            Assert.Contains("h1{color:teal}", shown);   // the style sheet of the vault is in the page
            shown = await ShownWhen(s => s.Contains("data:image/png;base64,"));
            Assert.Contains("data:image/png;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("PNGDATA")), shown);   // the picture, read from the vault
            Assert.True(view.Editor.Editor.IsEffectivelyVisible);   // wide: code and page side by side
            await Settle(window, view);
            Snapshot(window, "native-live-html-wide.png");

            view.Editor.Editor.Text = "<html><head><link rel=\"stylesheet\" href=\"estilo.css\"></head><body><h1>Mudou</h1></body></html>";
            shown = await ShownWhen(s => s.Contains("<h1>Mudou</h1>"));
            Assert.Contains("<h1>Mudou</h1>", shown);   // without saving

            // the computer has no viewer inside the app: the page opens in the browser and reloads there with the code
            var open = view.Editor.LivePane.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == "Abrir no navegador");
            view.Editor.LivePane.OpenBrowser = _ => { };
            open.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            var address = view.Editor.LivePane.BrowserAddress!;
            Assert.StartsWith("http://localhost:", address);
            using var http = new HttpClient();
            var page = await http.GetStringAsync(address);
            Assert.Contains("<h1>Mudou</h1>", page);
            Assert.Contains("EventSource", page);
            using var events = await http.GetStreamAsync(address + "__urbe/events");
            using var reader = new StreamReader(events);
            view.Editor.Editor.Text = "<html><body><h1>De novo</h1></body></html>";
            var line = "";
            var read = Task.Run(async () => { while (!(line = await reader.ReadLineAsync() ?? "").StartsWith("data:")) { } });
            for (int i = 0; i < 100 && !read.IsCompleted; i++) { await Task.Delay(20); Dispatcher.UIThread.RunJobs(); }
            Assert.True(read.IsCompleted, "the browser was not told to reload");
            Assert.Contains("<h1>De novo</h1>", await http.GetStringAsync(address));

            window.Width = 412;
            await Settle(window, view);
            Snapshot(window, "native-live-html-phone.png");
            Assert.True(view.Editor.LivePane.IsEffectivelyVisible);   // on a phone the page covers the code
            // Código: the code alone again
            buttons.First(b => b.Content as string == "Código").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.False(view.Editor.LivePane.IsVisible);
            // a note has no Prévia
            view.OpenNoteByPath("Nota.md");
            await Settle(window, view);
            Assert.DoesNotContain(view.Editor.GetLogicalDescendants().OfType<Button>(), b => b.Content as string == "Prévia" && b.IsEffectivelyVisible);
            return true;
        }
        finally { window.Close(); try { Directory.Delete(dir, true); } catch { } }
    }, CancellationToken.None));
}
