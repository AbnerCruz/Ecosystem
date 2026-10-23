using System.Net.Http;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// UC-24/UC-25: Configurações → "Verificar atualização" (settings.js and bridge.js update.check of the 1.8.4). Only when
/// asked; the newest direct release of this platform is offered to download in the browser, and the app never installs
/// anything itself (the APK goes over the installed one; on Windows the zip is extracted over this folder).
/// </summary>
public sealed partial class MainView
{
    private static readonly HttpClient UpdateHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>The releases' JSON (replaced in tests); by default GitHub's list of the Ecosystem's releases.</summary>
    public Func<Task<string>>? FetchReleases { get; set; }

    /// <summary>The platform's file in a release (replaced in tests): <see cref="UrbeApp.UpdateAsset"/>.</summary>
    public Func<string, string>? UpdateAsset { get => _updateAsset ?? UrbeApp.UpdateAsset; set => _updateAsset = value; }
    private Func<string, string>? _updateAsset;

    /// <summary>The installed version (replaced in tests): <see cref="UrbeApp.InstalledVersion"/>.</summary>
    public string? InstalledVersion { get => _installed ?? UrbeApp.InstalledVersion; set => _installed = value; }
    private string? _installed;

    /// <summary>Opens a download in the browser (replaced in tests).</summary>
    public Func<Uri, Task>? OpenDownload { get; set; }

    private DialogItem? UpdateItem() => UpdateAsset is null ? null
        : new DialogItem("Verificar atualização", UrbeTheme.Icons.Download, "Versão instalada: " + (InstalledVersion ?? "desconhecida"), Run: CheckForUpdateAsync);

    private static async Task<string> FetchGitHubReleasesAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AppUpdate.ReleasesUrl);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("User-Agent", "Urbe");
        using var response = await UpdateHttp.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("GitHub respondeu " + (int)response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Looks for a newer release and offers to download it.</summary>
    public async Task CheckForUpdateAsync()
    {
        if (UpdateAsset is not { } asset) return;
        var installed = InstalledVersion;
        AppRelease? release;
        try
        {
            ShowLifeNotice("Verificando atualização…");
            release = AppUpdate.Pick(await (FetchReleases ?? FetchGitHubReleasesAsync)(), asset);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            await Dialogs.AlertAsync("Atualização", "Não consegui verificar agora: " + ex.Message + ".\n\nVersão instalada: " + (installed ?? "desconhecida"));
            return;
        }
        if (release is null || (installed is not null && !AppUpdate.Newer(release.Version, installed)))
        {
            await Dialogs.AlertAsync("Atualização", "Você já está na versão mais nova.\n\nVersão instalada: " + (installed ?? "desconhecida"));
            return;
        }
        var notes = release.Notes.Trim();
        if (notes.Length > 600) notes = notes[..600].TrimEnd() + "…";
        var how = OperatingSystem.IsAndroid() ? "O arquivo abre no navegador; instale por cima do Urbe atual (suas notas ficam onde estão)."
            : "O arquivo abre no navegador; extraia por cima da pasta do Urbe (suas notas ficam onde estão).";
        if (!await Dialogs.ConfirmAsync("Versão " + release.Version + " disponível",
                "Versão instalada: " + (installed ?? "desconhecida") + "\n\n" + (notes.Length > 0 ? notes + "\n\n" : "") + how, "Baixar", cancel: "Agora não"))
            return;
        var uri = new Uri(release.Url);
        if (OpenDownload is not null) { await OpenDownload(uri); return; }
        if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.Launcher is not { } launcher || !await launcher.LaunchUriAsync(uri))
            await Dialogs.AlertAsync("Atualização", "Não consegui abrir o navegador. Baixe em:\n" + release.Url);
    }
}
