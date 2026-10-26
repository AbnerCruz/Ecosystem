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
        : new DialogItem("Versões e novidades", UrbeTheme.Icons.Download, "Instalada: " + (InstalledVersion ?? "desconhecida") + " · histórico e instalação manual", Run: ShowVersionCenterAsync);

    private static async Task<string> FetchGitHubReleasesAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AppUpdate.ReleasesUrl);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("User-Agent", "Urbe");
        using var response = await UpdateHttp.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("GitHub respondeu " + (int)response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }


    /// <summary>Embedded release notes are available even offline and do not depend on GitHub release descriptions.</summary>
    public static string CurrentPatchnotes()
    {
        using var stream = typeof(MainView).Assembly.GetManifestResourceStream("urbe/patchnotes.md");
        if (stream is null) return "As notas desta versão não foram incluídas nesta compilação.";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>In Settings, offers installed-version notes or an explicit on-demand remote catalog.</summary>
    public async Task ShowVersionCenterAsync()
    {
        if (UpdateAsset is not { } asset) return;
        var action = await Dialogs.ChooseAsync("Versões do Urbe",
        [
            new DialogItem("Notas da versão instalada", Detail: "Versão " + (InstalledVersion ?? "desconhecida") + " · disponível offline"),
            new DialogItem("Todas as versões disponíveis", Detail: "Consultar GitHub, comparar versões e selecionar um download")
        ], "Instalada: " + (InstalledVersion ?? "desconhecida") + ". O Urbe não altera a versão nem o vault automaticamente.");
        if (action is null) return;
        if (action == 0)
        {
            await Dialogs.AlertAsync("Novidades — " + (InstalledVersion ?? "Urbe"), CurrentPatchnotes());
            return;
        }

        var releases = new List<AppRelease>();
        try
        {
            ShowLifeNotice("Consultando versões publicadas…");
            for (var page = 1; page <= 100; page++)
            {
                string json = page == 1 && FetchReleases is not null
                    ? await FetchReleases()
                    : await FetchGitHubReleasesPageAsync(page);
                releases.AddRange(AppUpdate.List(json, asset));
                if (!HasFullReleasePage(json)) break;
                if (page == 100) throw new IOException("Catálogo com mais de 100 páginas. Não é seguro afirmar que a lista está completa.");
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            await Dialogs.AlertAsync("Versões do Urbe", "Não foi possível consultar o catálogo completo: " + e.Message);
            return;
        }
        releases = releases.GroupBy(r => r.Version, StringComparer.Ordinal).Select(g => g.First())
            .OrderByDescending(r => r.Version, Comparer<string>.Create((x, y) =>
                AppUpdate.Newer(x, y) ? 1 : AppUpdate.Newer(y, x) ? -1 : string.CompareOrdinal(x, y))).ToList();
        if (releases.Count == 0)
        {
            await Dialogs.AlertAsync("Versões do Urbe", "Não foi encontrada uma release do cliente nativo com um arquivo compatível com este dispositivo.");
            return;
        }
        var latest = releases[0].Version;
        var installed = InstalledVersion ?? "desconhecida";
        var chosen = await Dialogs.ChooseAsync("Selecionar versão",
            releases.Select(r => new DialogItem(r.Version == installed ? r.Version + " · instalada" :
                r.Version == latest ? r.Version + " · última disponível" : r.Version,
                Detail: r.Notes.Length == 0 ? "Sem descrição na release." :
                    r.Notes.Replace('\n', ' ').Replace('\r', ' ')[..Math.Min(140, r.Notes.Length)])).ToList(),
            "Instalada: " + installed + " · mais recente: " + latest +
            ". Os downloads são manuais; uma versão antiga pode não conseguir abrir recursos novos.");
        if (chosen is null) return;
        var selection = releases[chosen.Value];
        bool downgrade = AppUpdate.Newer(installed, selection.Version);
        var warning = downgrade
            ? "Esta versão é anterior à instalada. O Android normalmente não aceita instalar APKs com versionCode menor por cima de outro.\n\nDesinstalar o Urbe pode apagar conversas, memórias, configurações e outros dados privados. Exporte esses dados e faça backup do vault antes de considerar qualquer reinstalação. Nunca presuma compatibilidade de escrita de arquivos novos com uma versão antiga."
            : "O download será aberto no navegador. O Urbe não instala APKs automaticamente nem modifica o vault nesta operação. Faça backup antes de mudar de versão.";
        if (!await Dialogs.ConfirmAsync("Baixar Urbe " + selection.Version,
                "Instalada: " + installed + "\nMais recente: " + latest + "\n\n" + warning,
                "Abrir download", danger: downgrade, cancel: "Cancelar")) return;
        var uri = new Uri(selection.Url);
        if (OpenDownload is not null) { await OpenDownload(uri); return; }
        if (Avalonia.Controls.TopLevel.GetTopLevel(this)?.Launcher is not { } launcher || !await launcher.LaunchUriAsync(uri))
            await Dialogs.AlertAsync("Versões do Urbe", "Não consegui abrir o navegador. Baixe em:\n" + selection.Url);
    }

    private static bool HasFullReleasePage(string json)
    {
        try { return System.Text.Json.Nodes.JsonNode.Parse(json) is System.Text.Json.Nodes.JsonArray a && a.Count == 100; }
        catch (System.Text.Json.JsonException) { return false; }
    }

    private static async Task<string> FetchGitHubReleasesPageAsync(int page)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AppUpdate.ReleasesUrl + "&page=" + page);
        request.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        request.Headers.TryAddWithoutValidation("User-Agent", "Urbe");
        using var response = await UpdateHttp.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("GitHub respondeu " + (int)response.StatusCode);
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
