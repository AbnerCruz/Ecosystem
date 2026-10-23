using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Urbe.Client;
using Urbe.Core;

namespace Urbe.Client.Tests;

/// <summary>UC-24/UC-25: Configurações → Verificar atualização, only when asked, with a download opened in the browser.</summary>
public sealed partial class HeadlessUiTests
{
    [Fact]
    public async Task Update_IsOfferedOnlyWhenANewerReleaseHasThisPlatformsFile() =>
        Assert.True(await Session.Dispatch<bool>(async () =>
        {
            var view = new MainView();
            var window = new Window { Width = 412, Height = 860, Content = view };
            window.Show();
            await view.LoadAsync(TutorialNotes.Load());
            await Settle(window, view);
            var json = "[{\"tag_name\":\"urbe-csharp-v2.0.0-beta.19\",\"body\":\"Novidades\",\"assets\":[{\"name\":\"Urbe-CSharp-2.0.0-beta.19-Windows-x64.zip\"," +
                "\"browser_download_url\":\"https://github.com/AbnerCruz/Ecosystem/releases/download/urbe-csharp-v2.0.0-beta.19/Urbe-CSharp-2.0.0-beta.19-Windows-x64.zip\"}]}]";
            int asked = 0;
            Uri? opened = null;
            view.FetchReleases = () => { asked++; return Task.FromResult(json); };
            view.UpdateAsset = AppUpdate.WindowsAsset;
            view.OpenDownload = uri => { opened = uri; return Task.CompletedTask; };

            // nothing is checked until asked
            Assert.Equal(0, asked);

            // a newer one: offered, and Baixar opens its file
            view.InstalledVersion = "2.0.0-beta.18";
            await RunWithDialog(view.CheckForUpdateAsync(), () =>
            {
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Versão 2.0.0-beta.19 disponível");
                ClickButtonWithText(view.Dialogs, "Baixar");
                return Task.CompletedTask;
            });
            Assert.Equal(1, asked);
            Assert.EndsWith("/Urbe-CSharp-2.0.0-beta.19-Windows-x64.zip", opened!.AbsoluteUri);

            // the same version installed: nothing to download
            opened = null;
            view.InstalledVersion = "2.0.0-beta.19";
            await RunWithDialog(view.CheckForUpdateAsync(), () =>
            {
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Você já está na versão mais nova.", StringComparison.Ordinal) == true);
                ClickButtonWithText(view.Dialogs, "Ok");
                return Task.CompletedTask;
            });
            Assert.Null(opened);

            // no connection: said, and nothing breaks
            view.FetchReleases = () => throw new System.Net.Http.HttpRequestException("sem rede");
            await RunWithDialog(view.CheckForUpdateAsync(), () =>
            {
                Assert.Contains(view.Dialogs.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Não consegui verificar agora: sem rede", StringComparison.Ordinal) == true);
                ClickButtonWithText(view.Dialogs, "Ok");
                return Task.CompletedTask;
            });
            window.Close();
            return true;
        }, CancellationToken.None));
}
