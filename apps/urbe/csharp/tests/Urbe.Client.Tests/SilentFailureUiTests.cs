using Avalonia.Controls;
using Urbe.Client;

namespace Urbe.Client.Tests;

/// <summary>A button whose work fails with an error nobody caught is not left silent: the app stays open (CrashLog) and
/// the screen says what failed (observation of 15/11/2026: "botões que não funcionam").</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Errors_ThatReachTheInterfaceAreToldOnTheScreen() => Assert.True(await Session.Dispatch<bool>(async () =>
    {
        var view = new MainView();
        var window = new Window { Width = 412, Height = 900, Content = view };
        window.Show();
        try
        {
            await view.LoadAsync(TutorialNotes.Load());
            CrashLog.Report(new UnauthorizedAccessException("negado"));
            Assert.Contains("Não foi possível concluir", view.NoticeText ?? "");
            Assert.Contains("permissão para gravar", view.NoticeText ?? "");
            CrashLog.Report(new IOException("O disco recusou a gravação."));
            Assert.Contains("O disco recusou a gravação.", view.NoticeText ?? "");
            return true;
        }
        finally { window.Close(); }
    }, CancellationToken.None));
}
