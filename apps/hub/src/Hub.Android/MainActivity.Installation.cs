using Android.App;
using Android.Content;
using Android.Widget;
using Hub.Core;
using System.Text.Json;

namespace HubApp;

public sealed partial class MainActivity
{
    Button? _install;
    Button? _openProduct;
    Button? _cancelInstall;
    TextView? _installationStatus;
    CancellationTokenSource? _prepareInstall;
    bool _checkingInstall;
    AlertDialog? _installConfirmation;
    readonly Dictionary<string, AndroidProductTrust> _approvedPackages = new();
    static readonly AndroidProductTrust[] Candidates = ReadCandidates();

    static AndroidProductTrust[] ReadCandidates()
    {
        using var stream = typeof(MainActivity).Assembly.GetManifestResourceStream("hub.android-products.json");
        return stream is null ? [] : JsonSerializer.Deserialize<AndroidProductTrust[]>(stream) ?? [];
    }

    void AddInstallationControls(LinearLayout panel)
    {
        var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _install = new Button(this) { Text = "Instalar", Enabled = false };
        _openProduct = new Button(this) { Text = "Abrir", Enabled = false };
        _cancelInstall = new Button(this) { Text = "Cancelar instalação", Enabled = false };
        _install.Click += (_, _) => ConfirmInstallation();
        _openProduct.Click += (_, _) => OpenProduct();
        _cancelInstall.Click += (_, _) => { _prepareInstall?.Cancel(); HubInstaller.Cancel(this); UpdateInstallationControls(); };
        foreach (var button in new[] { _install, _openProduct, _cancelInstall })
            row.AddView(button, new LinearLayout.LayoutParams(0, Android.Views.ViewGroup.LayoutParams.WrapContent, 1f));
        panel.AddView(row);
        _installationStatus = new TextView(this) { TextSize = 12 };
        panel.AddView(_installationStatus);
    }

    async Task<AndroidProductTrust?> LoadTrust(ArtifactChoice choice, CancellationToken ct)
    {
        var candidate = Candidates.SingleOrDefault(x => x.ProductId == choice.ProductId);
        if (candidate is null) return null;
        var source = new RepositoryFileSource(Http, Options.Repository, Options.Branch, Options.Token);
        var decisionTask = source.ReadTextAsync("docs/governance/decisions.json", ct);
        var ecoTask = source.ReadTextAsync("ecosystem.json", ct);
        var profileTask = source.ReadTextAsync(DistributionReader.ProfilePath, ct);
        await Task.WhenAll(decisionTask, ecoTask, profileTask);
        if (ecoTask.Result.Availability != Availability.Derived || profileTask.Result.Availability != Availability.Derived || ecoTask.Result.Value is null) return null;
        var product = EcosystemReader.ReadProducts(ecoTask.Result.Value, _ => null).SingleOrDefault(p => p.Id == choice.ProductId);
        if (product is null) return null;
        var channel = DistributionReader.ReleaseChannelFor(product, profileTask.Result, Options.Repository);
        return channel.Availability == Availability.Derived && channel.Value == choice.Channel
            ? InstallationPolicy.Approved(candidate, decisionTask.Result) : null;
    }

    async Task RefreshInstallationTrust(CancellationToken ct)
    {
        _approvedPackages.Clear();
        // Only first APK of each Product; at most the number of declared, compiled candidates.
        foreach (var candidate in Candidates)
        {
            var choice = _choices.FirstOrDefault(x => x.ProductId == candidate.ProductId);
            if (choice is null) continue;
            try { if (await LoadTrust(choice, ct) is { } approved) _approvedPackages[candidate.ProductId] = approved; }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { /* no permission fallback or cached approval */ }
        }
        if (!_destroyed) UpdateInstallationControls();
    }

    void UpdateInstallationControls()
    {
        if (_installationStatus is null) return;
        var choice = SelectedArtifact();
        var trust = choice is null ? null : _approvedPackages.GetValueOrDefault(choice.ProductId);
        var transfer = HubDownloads.Session.Status;
        var busy = _installConfirmation is not null || _checkingInstall || HubInstaller.Preparing || HubInstaller.Operation(this) is not null;
        var downloadMatches = transfer is { Choice: not null, Result.State: ArtifactDownloadState.Verified } && transfer.Choice == choice;
        AndroidPackageEvidence? installed = null;
        try { if (trust is not null) installed = HubInstaller.Installed(this, trust.PackageId); }
        catch (Exception) { }
        _install!.Enabled = !_refreshing && !busy && !transfer.Active && trust is not null && downloadMatches;
        _install.Text = installed is null ? "Instalar" : "Atualizar";
        _openProduct!.Enabled = !_refreshing && _installConfirmation is null && !_checkingInstall && trust is not null && installed is not null && InstallationPolicy.TrustedPackage(trust, installed);
        _cancelInstall!.Enabled = _checkingInstall || HubInstaller.Preparing || HubInstaller.Operation(this) is not null;
        _installationStatus.Text = (trust is null ? "Instalação/abertura bloqueadas: identidade ou certificado sem aprovação atual. Canal independente continua disponível."
            : installed is null ? "Pacote não encontrado/acessível no Android."
            : $"Instalado: {installed.VersionName ?? "versão sem nome"} ({installed.VersionCode}).") + "\n" + HubInstaller.Message;
    }

    async void ConfirmInstallation()
    {
        if (_destroyed || _installConfirmation is not null || _checkingInstall || HubInstaller.Preparing || HubInstaller.Operation(this) is not null || SelectedArtifact() is not { } choice) return;
        var status = HubDownloads.Session.Status;
        if (status.Active || status.Choice != choice || status.Result is not { State: ArtifactDownloadState.Verified } download) return;
        _prepareInstall?.Cancel();
        _prepareInstall = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = _prepareInstall.Token;
        _checkingInstall = true;
        UpdateInstallationControls();
        try
        {
            var trust = await LoadTrust(choice, ct);
            if (_destroyed || ct.IsCancellationRequested) return;
            if (trust is null) { HubInstaller.SetMessage("Aprovação/canal atual indisponível. Não foi iniciada instalação."); return; }
            var installed = HubInstaller.Installed(this, trust.PackageId);
            _installConfirmation = new AlertDialog.Builder(this).SetTitle(installed is null ? "Instalar " + choice.ProductName : "Atualizar " + choice.ProductName)
                .SetMessage($"{trust.PackageId}\n{choice.Tag}\nInstalado: {installed?.VersionName ?? "não encontrado"}\n"
                    + "O pacote e os bytes serão reconferidos. A confirmação do Android ainda será necessária."
                    + (trust.PublicDevelopmentKey ? "\nChave pública de desenvolvimento: não comprova autoria." : ""))
                .SetNegativeButton("Cancelar", (_, _) => { })
                .SetPositiveButton("Continuar", async (_, _) =>
                {
                    if (_destroyed || ct.IsCancellationRequested || SelectedArtifact() != choice || HubDownloads.Session.Status.Choice != choice) return;
                    if (!PackageManager!.CanRequestPackageInstalls())
                    {
                        // No automatic resume on return; the user must tap again, redoing trust/bytes checks.
                        new AlertDialog.Builder(this).SetTitle("Permitir esta fonte no Android")
                            .SetMessage("Abra as configurações e permita instalar pelo Hub. Ao voltar, toque novamente em Instalar/Atualizar. Negar mantém o fluxo bloqueado.")
                            .SetNegativeButton("Cancelar", (_, _) => { })
                            .SetPositiveButton("Configurações", (_, _) =>
                            {
                                try { StartActivity(new Intent(Android.Provider.Settings.ActionManageUnknownAppSources, Android.Net.Uri.Parse("package:" + PackageName))); }
                                catch (Exception) { HubInstaller.SetMessage("Configurações de instalação indisponíveis."); }
                            }).Show();
                        return;
                    }
                    await HubInstaller.BeginAsync(this, trust, choice, download, ct,
                        async token => await LoadTrust(choice, token));
                }).Create();
            _installConfirmation.DismissEvent += (_, _) => { _installConfirmation = null; if (!_destroyed) UpdateDownloadControls(); };
            _installConfirmation.Show();
        }
        catch (OperationCanceledException) { HubInstaller.SetMessage("Consulta/preparação cancelada."); }
        catch (Exception) { HubInstaller.SetMessage("Não foi possível conferir confiança atual; instalação bloqueada."); }
        finally { _checkingInstall = false; if (!_destroyed) UpdateInstallationControls(); }
    }

    async void OpenProduct()
    {
        if (_destroyed || _checkingInstall || SelectedArtifact() is not { } choice) return;
        _checkingInstall = true;
        UpdateInstallationControls();
        try
        {
            var trust = await LoadTrust(choice, CancellationToken.None);
            if (_destroyed || trust is null) { HubInstaller.SetMessage("Abertura bloqueada: aprovação atual indisponível."); return; }
            var actual = HubInstaller.Installed(this, trust.PackageId);
            if (actual is null || !InstallationPolicy.TrustedPackage(trust, actual))
            { HubInstaller.SetMessage("Pacote instalado/assinatura não corresponde à aprovação."); return; }
            var launch = PackageManager!.GetLaunchIntentForPackage(trust.PackageId);
            if (launch is null) { HubInstaller.SetMessage("App não tem entrada de launcher disponível."); return; }
            StartActivity(launch); // fixed approved package, never an asset-supplied URL or command
        }
        catch (Exception) { HubInstaller.SetMessage("Android não permitiu abrir o pacote aprovado."); }
        finally { _checkingInstall = false; if (!_destroyed) UpdateInstallationControls(); }
    }

    void InstallationChanged() => RunOnUiThread(() => { if (!_destroyed) { UpdateInstallationControls(); UpdateDownloadControls(); } });
}
