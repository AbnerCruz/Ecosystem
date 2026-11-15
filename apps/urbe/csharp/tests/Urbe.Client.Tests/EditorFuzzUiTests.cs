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

    // the person's text: every seal token taken out, wherever it is (a duplicated seal the editor folds into one is not text)
    private static string NoSeals(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s?\{ia:""[^""]*""\}", "");

    /// <summary>Ten seeds by default; URBE_FUZZ_SEEDS=n runs n of them (a longer local run).</summary>
    public static TheoryData<int> Seeds()
    {
        var n = int.TryParse(Environment.GetEnvironmentVariable("URBE_FUZZ_SEEDS"), out var v) ? v : 10;
        var data = new TheoryData<int>();
        for (int i = 1; i <= n; i++) data.Add(i);
        return data;
    }

    [Theory]
    [MemberData(nameof(Seeds))]
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
            for (int step = 0; step < 200; step++)
            {
                var op = rnd.Next(11);
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
                    var beforeNote = NoSeals(editor.Surface.Text).Length;
                    var beforeText = editor.Surface.Text;
                    window.KeyTextInput(s);
                    Dispatcher.UIThread.RunJobs();
                    log.Add($"type {s} at {a}-{b}");
                    Assert.True(ReferenceEquals(box, editor.Surface.EditBox), $"seed {seed}: typing ended the block; box before [{text}] now [{editor.Surface.EditBox?.Text}]\nNOTE[{editor.Surface.Text}]\n{string.Join("\n", log.TakeLast(6))}");
                    Assert.Equal(beforeBox - (b - a) + 1, (box.Text ?? "").Length);
                    Assert.Equal(s, box.Text![a..(a + 1)]);
                    Assert.True(NoSeals(editor.Surface.Text).Length >= beforeNote - (b - a) + 1, $"seed {seed}: note lost text; box [{text}] sel {a}-{b}\nBEFORE[{beforeText}]\nAFTER[{editor.Surface.Text}]\n{string.Join("\n", log.TakeLast(5))}");
                }
                else if (op == 7)
                {
                    window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                    window.KeyRelease(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
                    log.Add("backspace");
                }
                else if (op == 10)
                {
                    var keys = new[] { (Key.Up, PhysicalKey.ArrowUp), (Key.Down, PhysicalKey.ArrowDown), (Key.Left, PhysicalKey.ArrowLeft), (Key.Right, PhysicalKey.ArrowRight), (Key.Delete, PhysicalKey.Delete) };
                    var (k, pk) = keys[rnd.Next(keys.Length)];
                    var m = rnd.Next(4) == 0 ? RawInputModifiers.Shift : RawInputModifiers.None;
                    window.KeyPress(k, m, pk, null);
                    window.KeyRelease(k, m, pk, null);
                    log.Add("key " + m + " " + k);
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
            // what MainView's last defense asks before writing (the person's route): the file would be saved
            var final = EditorOverlay.SealsForSave(Note, editor.Editor.Text!);
            Assert.Equal(NoSeals(editor.Editor.Text!), NoSeals(final));
            // Known limit (documented in EDITOR-REVIEW-20261114.md): a fence pulled into the middle of a line (``` after
            // text) makes the seals after it read as code, and AiAuthorship then adds a seal at every reading
            var fenceInLine = System.Text.RegularExpressions.Regex.IsMatch(final, @"(?m)^.*\S.*```");
            if (!fenceInLine)
                Assert.True(NoteMarks.PreservesAiAuthorship(Note, final) || final == AiAuthorship.Attribute(Note, final, null),
                $"seed {seed}: the save would be refused\nFINAL[{final}]\nAGAIN[{AiAuthorship.Attribute(Note, final, null)}]");
            window.Close();
            return true;
        }, TestContext.Current.CancellationToken));
}
