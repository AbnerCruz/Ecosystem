using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>
/// Random taps, typing, deleting and selections in the Visual mode of a varied note: typing never ends the block being
/// written, never loses a typed character, and the note's text and the Visual surface always agree.
/// </summary>
public sealed class EditorFuzzUiTests
{
    internal const string Note =
        "# Título da nota\n\nParágrafo com **negrito**, *itálico*, `código`, [[Outra nota|apelido]] e [link](https://exemplo.com) no meio.\n\n" +
        "- item um\n- item dois com **força**\n  - aninhado\n\n- [ ] tarefa aberta\n- [x] tarefa feita\n\n1. primeiro\n2. segundo\n\n" +
        "> Uma citação com *ênfase*.\n\nTexto do agente que foi escrito com ajuda {ia:\"modelo\"}\n\n```csharp\nvar x = 1;\n```\n\n" +
        "Fórmula $x^2 + 1$ no texto e %%comentário%% escondido.\n\n$$\n\\int_0^1 x\\,dx\n$$\n\n## Seção dois\n\nÚltimo parágrafo.\n";

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task Visual_RandomEditingNeverLosesTypedText(int seed) =>
        Assert.True(await HeadlessUiTests.Session.Dispatch<bool>(() =>
        {
            var rnd = new Random(seed);
            var editor = new EditorOverlay();
            var window = new Window { Width = 390, Height = 800, Content = editor };
            window.Show();
            editor.Open(new CityNote("Fuzz.md", Note));
            Dispatcher.UIThread.RunJobs();
            var log = new List<string>();
            void Check(string what)
            {
                Dispatcher.UIThread.RunJobs();
                var source = (editor.Editor.Text ?? "").Replace("\r\n", "\n");
                Assert.True(source == editor.Surface.Text, $"seed {seed}: Fonte and Visual differ after {what}\n{string.Join("\n", log)}");
            }
            for (int step = 0; step < 120; step++)
            {
                var op = rnd.Next(10);
                var box = editor.Surface.EditBox;
                if (box is null || op < 2)
                {
                    var starts = editor.Surface.BlockStarts;
                    var start = starts[rnd.Next(starts.Count)];
                    if (editor.Surface.BlockView(start) is not { } holder) continue;
                    holder.BringIntoView();
                    window.UpdateLayout();
                    var at = new Point(rnd.Next(0, 300), rnd.Next(0, Math.Max(1, (int)holder.Bounds.Height)));
                    editor.Surface.TapBlock(start, holder, holder.InputHitTest(at) as Visual, at, select: rnd.Next(5) == 0);
                    log.Add($"tap {start} {at}");
                    Check("tap");
                    continue;
                }
                var text = box.Text ?? "";
                if (op < 7)
                {
                    var s = new string((char)('a' + rnd.Next(26)), 1);
                    int a = Math.Min(box.SelectionStart, box.SelectionEnd), b = Math.Max(box.SelectionStart, box.SelectionEnd);
                    var beforeBox = text.Length;
                    var beforeNote = editor.Surface.Text.Length;
                    window.KeyTextInput(s);
                    Dispatcher.UIThread.RunJobs();
                    log.Add($"type {s} at {a}-{b}");
                    Assert.True(ReferenceEquals(box, editor.Surface.EditBox), $"seed {seed}: typing ended the block\n{string.Join("\n", log)}");
                    Assert.Equal(beforeBox - (b - a) + 1, (box.Text ?? "").Length);
                    Assert.Equal(s, box.Text![a..(a + 1)]);
                    Assert.True(editor.Surface.Text.Length >= beforeNote - (b - a) + 1, $"seed {seed}: note lost text\n{string.Join("\n", log)}");
                }
                else if (op == 7)
                {
                    window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                    window.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                    log.Add("backspace");
                }
                else if (op == 8)
                {
                    var a = rnd.Next(text.Length + 1); var b = Math.Min(text.Length, a + rnd.Next(8));
                    box.SelectionStart = a; box.SelectionEnd = b;
                    log.Add($"select {a}-{b}");
                }
                else
                {
                    var c = rnd.Next(text.Length + 1);
                    box.SelectionStart = box.SelectionEnd = box.CaretIndex = c;
                    log.Add($"caret {c}");
                }
                Check(log[^1]);
            }
            editor.Surface.FinishEdit();
            Check("finish");
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
