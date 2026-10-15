using Avalonia.Input.Platform;

namespace Urbe.Client;

/// <summary>Configurações → Relatório de erros: the errors the safety net caught (CrashLog), to copy and send.</summary>
public sealed partial class MainView
{
    private bool _errorNoticeShown;

    private void WatchErrors() => CrashLog.Reported += _ =>
    {
        // once per session: the Urbe kept running, the details are in Configurações
        if (_errorNoticeShown) return;
        _errorNoticeShown = true;
        ShowLifeNotice("Algo deu errado, mas o Urbe continuou. Detalhes em Configurações → Relatório de erros.");
    };

    private async Task ShowErrorReportAsync()
    {
        var log = CrashLog.Read();
        var tail = log.Length > 1500 ? "…" + log[^1500..] : log;
        await Dialogs.MenuAsync("Relatório de erros",
        [
            new DialogItem("Copiar relatório", UrbeTheme.Icons.Copy, "Para enviar a quem está desenvolvendo", Run: async () =>
            {
                if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) { ShowLifeNotice("Este aparelho não deixou copiar."); return; }
                await clipboard.SetTextAsync(log);
                CopiedText = log;
                ShowLifeNotice("Relatório copiado.");
            }),
            new DialogItem("Apagar relatório", UrbeTheme.Icons.Trash, Danger: true, Run: () => { CrashLog.Clear(); ShowLifeNotice("Relatório apagado."); return Task.CompletedTask; })
        ], tail);
    }
}
