using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task EditorLineNumbersFollowLogicalLinesAcrossWrapAndEdits() =>
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 320, Height = 780, Content = editor };
            window.Show();
            var text = "Linha um longa " + new string('a', 150) + "\nLinha dois\n\nLinha quatro";
            editor.Open(new CityNote("Linhas.md", text));
            editor.SetMode(EditorMode.Fonte);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(4, editor.LineNumbers.LineStarts.Count);
            Assert.Equal(text.IndexOf('\n') + 1, editor.LineNumbers.LineStarts[1]);
            Assert.True(editor.LineNumbers.Bounds.Width >= 28);
            Snapshot(window, "native-editor-line-numbers-320.png");
            editor.Editor.SelectionStart = editor.Editor.SelectionEnd = text.Length;
            editor.InsertText("\nLinha cinco");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(5, editor.LineNumbers.LineStarts.Count);
            editor.Undo();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(text, editor.Current!.Content);
            Assert.Equal(4, editor.LineNumbers.LineStarts.Count);
            editor.SetMode(EditorMode.Visual);
            Assert.Equal(text, editor.Current.Content);
            window.Width = 1280;
            editor.SetMode(EditorMode.Fonte);
            window.UpdateLayout();
            Snapshot(window, "native-editor-line-numbers-desktop.png");
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task EditorLineNumbers_NumberTheFirstVisualLineOfEachWrappedLineAndMarkTheCaretLine() =>
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            var editor = new EditorOverlay();
            var window = new Window { Width = 320, Height = 780, Content = editor };
            window.Show();
            try
            {
                var text = "Primeira " + string.Concat(Enumerable.Repeat("palavra ", 40)) + "\nSegunda\n\nQuarta";
                editor.Open(new CityNote("Linhas.md", text));
                editor.SetMode(EditorMode.Fonte);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var rows = editor.LineNumbers.VisibleRows();
                Assert.Equal([0, 1, 2, 3], rows.Select(r => r.Line));
                // the first line wraps: line 2's number waits for its own first visual line, not the next row
                Assert.True(rows[1].Y >= rows[0].Y + 2.5 * rows[0].Height, $"{rows[0]} / {rows[1]}");
                Assert.Equal(rows[2].Y, rows[1].Y + rows[1].Height, 1);
                // each number sits on the text line it names (same top as the text presenter's line)
                var presenter = editor.Editor.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
                var origin = presenter.TranslatePoint(default, editor.LineNumbers)!.Value;
                for (int i = 0; i < rows.Count; i++)
                    Assert.Equal(presenter.TextLayout.HitTestTextPosition(editor.LineNumbers.LineStarts[i]).Y + origin.Y, rows[i].Y, 1);

                editor.Editor.CaretIndex = text.IndexOf("Quarta", StringComparison.Ordinal) + 2;
                Assert.Equal(3, editor.LineNumbers.CurrentLine);
                editor.Editor.CaretIndex = 5;
                Assert.Equal(0, editor.LineNumbers.CurrentLine);
                editor.Editor.CaretIndex = text.IndexOf("Segunda", StringComparison.Ordinal);
                Assert.Equal(1, editor.LineNumbers.CurrentLine);
                // typing a new line moves the marked line with the caret
                editor.InsertText("\n");
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, editor.LineNumbers.CurrentLine);
                Assert.Equal(5, editor.LineNumbers.LineStarts.Count);
                window.UpdateLayout();
                Snapshot(window, "native-editor-line-numbers-current.png");
                return true;
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken));

    [Fact]
    public async Task EditorLineNumbers_TheSettingShowsThemAlwaysOnlyInCodeOrNeverAndIsKeptOnTheDevice() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var device = new MemoryDeviceStore();
            view.Customization.Device = device;
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            try
            {
                await view.LoadAsync(TutorialNotes.Load());
                await Settle(window, view);
                var gutter = view.Editor.LineNumbers;
                view.Editor.Open(new CityNote("Tutorial/Linhas.md", "a\nb\nc"));
                view.Editor.SetMode(EditorMode.Fonte);
                await Settle(window, view);
                // the default: numbers in Fonte, notes and code alike
                Assert.Equal(EditorLineNumbers.Always, view.Customization.LineNumbers);
                Assert.True(gutter.IsEffectivelyVisible);

                view.Customization.LineNumbers = EditorLineNumbers.CodeOnly;
                await Settle(window, view);
                Assert.Equal(EditorLineNumbers.CodeOnly, device.Get("urbe.editor.numeracao"));
                Assert.False(gutter.IsVisible);
                view.Editor.Open(new CityNote("Tutorial/script.cs", "class A\n{\n}\n"));
                await Settle(window, view);
                Assert.Equal(EditorMode.Fonte, view.Editor.Mode);
                Assert.True(gutter.IsEffectivelyVisible);
                Assert.Equal(4, gutter.LineStarts.Count);

                view.Customization.LineNumbers = EditorLineNumbers.Never;
                await Settle(window, view);
                Assert.False(gutter.IsVisible);
                // hidden, it does no work while typing, and catches up when shown again
                view.Editor.Editor.CaretIndex = view.Editor.Editor.Text!.Length;
                view.Editor.InsertText("\nmais");
                view.Customization.LineNumbers = EditorLineNumbers.Always;
                await Settle(window, view);
                Assert.True(gutter.IsEffectivelyVisible);
                Assert.Equal(5, gutter.LineStarts.Count);
                Assert.Null(device.Get("urbe.editor.numeracao"));
                // a fresh view over the same device keeps the choice
                device.Set("urbe.editor.numeracao", EditorLineNumbers.Never);
                Assert.Equal(EditorLineNumbers.Never, new Customization { Device = device }.LineNumbers);
                return true;
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken));

    [Fact]
    [Trait("Category", "Performance")]
    public async Task EditorLineNumbers_TypingInALargeNoteCostsAboutTheSameWithTheGutter() =>
        Assert.True(await Session.Dispatch<bool>(() =>
        {
            // opt-in (URBE_PERF=1): a note this size takes the headless TextBox ~0.3 s a key with or without the gutter
            if (Environment.GetEnvironmentVariable("URBE_PERF") != "1") return true;
            // 4000 lines (~200 KB): a long note or a big code file
            var line = "Uma linha de código ou de texto, com [[Ligação]] e #Tag, para medir a numeração.";
            var text = string.Join("\n", Enumerable.Range(0, 4000).Select(i => i + " " + line));
            var editor = new EditorOverlay();
            var window = new Window { Width = 412, Height = 860, Content = editor };
            window.Show();
            try
            {
                editor.Open(new CityNote("Grande.md", text));
                editor.SetMode(EditorMode.Fonte);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                editor.Editor.Focus();
                double Type(bool gutter)
                {
                    editor.LineNumbersMode = gutter ? EditorLineNumbers.Always : EditorLineNumbers.Never;
                    window.UpdateLayout();
                    editor.Editor.CaretIndex = text.Length / 2;
                    window.KeyTextInput("a");
                    Dispatcher.UIThread.RunJobs();
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    for (int i = 0; i < 20; i++) { window.KeyTextInput("a"); window.UpdateLayout(); }
                    watch.Stop();
                    Dispatcher.UIThread.RunJobs();
                    return watch.Elapsed.TotalMilliseconds / 20;
                }
                Type(true);   // warm up
                var without = Type(false);
                var with = Type(true);
                Assert.Equal(4000, editor.LineNumbers.LineStarts.Count);
                PerfLog($"ux.typing.gutter {with:0.00} ms per keystroke with numbers, {without:0.00} without (4000 lines)");
                if (Environment.GetEnvironmentVariable("URBE_PERF") == "1")
                    Assert.True(with < without * 1.25 + 2, $"{with:0.00} / {without:0.00} ms per keystroke");
                return true;
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken));
}
