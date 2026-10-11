using Android.Content;
using Android.Graphics;
using Android.Widget;

namespace Lunet.Android;

/// <summary>Inspeção da última captura do Preview no painel redimensionável da IDE, sem executar o jogo em segundo plano.</summary>
public sealed partial class MainActivity
{
    private void SetProfilerPanel()
    {
        _showProfiler = true;
        _showConsole = false;
        if (_layoutStore.Current.PanelTab != "profiler")
        {
            _layoutStore.Current.PanelTab = "profiler";
            SaveLayouts();
        }
        if (_panelList is null) return;
        _panelList.RemoveAllViews();
        var help = new TextView(this)
        {
            TextSize = 12,
            Text = _lastProfilerReport is null
                ? "Sem captura. Aperte Run, ligue ▥ Perf no Preview e jogue. Ao tocar Stop, a última janela medida aparece aqui."
                : "Última captura do Preview encerrado. O jogo está parado; estes valores não são uma medição ao vivo."
        };
        help.SetPadding(Dp(8), Dp(8), Dp(8), Dp(8));
        _panelList.AddView(help);
        if (_lastProfilerReport is null) return;
        _panelList.AddView(MakeButton("Exportar captura", ExportProfilerReport));
        var report = new TextView(this) { Text = _lastProfilerReport, TextSize = 12 };
        report.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
        report.SetTextIsSelectable(true);
        report.SetPadding(Dp(8), Dp(8), Dp(8), Dp(8));
        _panelList.AddView(report);
    }

    private void ExportProfilerReport()
    {
        if (_lastProfilerReport is null) return;
        var intent = new Intent(Intent.ActionSend);
        intent.SetType("text/plain");
        intent.PutExtra(Intent.ExtraSubject, "Profiler do Lunet — " + _project?.Name);
        intent.PutExtra(Intent.ExtraText, "Projeto: " + _project?.Name + "\nÚltima captura do Preview (jogo parado)\n\n" + _lastProfilerReport);
        StartActivity(Intent.CreateChooser(intent, "Exportar captura do Profiler"));
    }
}
