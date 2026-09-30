using System.Diagnostics;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Lunet.Editor;

namespace Lunet.Android;

/// <summary>Medição do editor com arquivos grandes, no aparelho (dados para decidir sobre virtualização, ADR 0003).</summary>
public sealed partial class MainActivity
{
    private static readonly int[] BenchmarkSizes = [2_000, 10_000, 40_000];

    private static string SyntheticSource(int lines)
    {
        var builder = new StringBuilder(lines * 48);
        builder.Append("namespace Bench;\n");
        for (var i = 1; i < lines; i++)
            builder.Append(i % 5 == 0 ? $"    // linha {i}: comentário de exemplo\n" : $"    public int Value{i} = {i} * 2 + Compute({i}); // texto\n");
        return builder.ToString();
    }

    /// <summary>Carrega arquivos sintéticos de vários tamanhos no editor e mede carga, realce e layout. Não grava nada no projeto.</summary>
    private void MeasureEditor()
    {
        if (_editor is null || _project is null) return;
        SaveCurrent();
        _session.Unload(); // o texto sintético nunca pode ser salvo no arquivo aberto
        var reopen = _openFile;
        _openFile = null;
        var report = new StringBuilder();
        var runtime = Java.Lang.Runtime.GetRuntime();
        report.Append($"Aparelho: {Build.Manufacturer} {Build.Model}, Android {Build.VERSION.Release}\n");
        report.Append($"Memória do app antes: {(runtime!.TotalMemory() - runtime.FreeMemory()) / 1_048_576} MB de {runtime.MaxMemory() / 1_048_576} MB\n\n");
        Toast.MakeText(this, "Medindo… aguarde alguns segundos", ToastLength.Long)?.Show();
        RunBenchmarkStep(0, report, reopen);
    }

    private void RunBenchmarkStep(int index, StringBuilder report, string? reopen)
    {
        var editor = _editor;
        if (editor is null) return;
        if (index >= BenchmarkSizes.Length)
        {
            var runtime = Java.Lang.Runtime.GetRuntime()!;
            report.Append($"\nMemória do app depois: {(runtime.TotalMemory() - runtime.FreeMemory()) / 1_048_576} MB");
            if (reopen is not null) OpenFile(reopen);
            ShowBenchmarkReport(report.ToString());
            return;
        }
        var lines = BenchmarkSizes[index];
        var text = SyntheticSource(lines);
        var load = Stopwatch.StartNew();
        editor.LoadText(text);
        var loadMs = load.ElapsedMilliseconds;
        var layout = Stopwatch.StartNew();
        editor.Post(() =>
        {
            var layoutMs = layout.ElapsedMilliseconds;
            var highlight = Stopwatch.StartNew();
            var spans = SyntaxHighlighter.Classify(text);
            var highlightMs = highlight.ElapsedMilliseconds;
            var scroll = Stopwatch.StartNew();
            for (var step = 0; step < 20; step++) editor.ScrollToLine(editor.TotalLines * step / 20);
            var scrollMs = scroll.ElapsedMilliseconds;
            report.Append($"{lines:N0} linhas ({text.Length / 1024} KB): carregar {loadMs} ms · primeiro layout {layoutMs} ms · realce ({spans.Count} trechos) {highlightMs} ms · 20 rolagens {scrollMs} ms\n");
            RunBenchmarkStep(index + 1, report, reopen);
        });
    }

    private void ShowBenchmarkReport(string text)
    {
        var view = new TextView(this) { Text = text, TextSize = 13 };
        view.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
        view.SetTextIsSelectable(true);
        new AlertDialog.Builder(this)!.SetTitle("Medição do editor")!.SetView(view)!
            .SetPositiveButton("Fechar", (_, _) => { })!
            .SetNeutralButton("Compartilhar", (_, _) =>
            {
                var intent = new Intent(Intent.ActionSend);
                intent.SetType("text/plain");
                intent.PutExtra(Intent.ExtraSubject, "Medição do editor Lunet");
                intent.PutExtra(Intent.ExtraText, text);
                StartActivity(Intent.CreateChooser(intent, "Compartilhar medição"));
            })!.Show();
    }
}
