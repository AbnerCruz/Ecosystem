using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Hub.Core;

namespace HubApp;

public sealed partial class MainActivity
{
    Spinner? _artifact;
    Button? _startDownload;
    Button? _cancelDownload;
    TextView? _downloadStatus;
    IReadOnlyList<ArtifactChoice> _choices = [];
    bool _observingDownloads;
    static bool _notificationPermissionRequested;
    ArtifactChoice? _awaitingNotificationPermission;
    const int NotificationPermissionRequest = 4202;

    void AddDownloads(LinearLayout root)
    {
        HubDownloads.Recover(this);
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(ScreenRenderer.Dp(this, 16), 0, ScreenRenderer.Dp(this, 16), ScreenRenderer.Dp(this, 8));
        panel.AddView(new TextView(this) { Text = "Baixar APK", TextSize = 16 });
        _artifact = new Spinner(this);
        _artifact.ItemSelected += (_, _) => UpdateDownloadControls(showSelection: true);
        panel.AddView(_artifact);
        var actions = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        _startDownload = new Button(this) { Text = "Baixar e conferir", Enabled = false };
        _cancelDownload = new Button(this) { Text = "Cancelar", Enabled = false };
        _startDownload.Click += (_, _) => DownloadSelected();
        _cancelDownload.Click += (_, _) => HubDownloads.Session.Cancel(HubDownloads.Session.Status.Operation);
        actions.AddView(_startDownload, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        actions.AddView(_cancelDownload, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
        panel.AddView(actions);
        _downloadStatus = new TextView(this) { TextSize = 13 };
        panel.AddView(_downloadStatus);
        AddInstallationControls(panel);
        root.AddView(panel);
    }

    void ShowDownloadChoices(HubSnapshot? snapshot)
    {
        if (HubDownloads.Session.Status.Active) return;
        _choices = ArtifactCatalog.Choices(snapshot);
        var labels = _choices.Select(c => $"{c.ProductName} · {c.Tag} · {c.Asset.Name}").ToArray();
        if (labels.Length == 0) labels = ["Nenhum APK disponível no catálogo"];
        var adapter = new ArrayAdapter<string>(this, global::Android.Resource.Layout.SimpleSpinnerItem, labels);
        adapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
        _artifact!.Adapter = adapter;
        UpdateDownloadControls(showSelection: true);
    }

    ArtifactChoice? SelectedArtifact() => _artifact is { SelectedItemPosition: >= 0 } spinner &&
        spinner.SelectedItemPosition < _choices.Count ? _choices[spinner.SelectedItemPosition] : null;

    void UpdateDownloadControls(bool showSelection = false)
    {
        var choice = SelectedArtifact();
        var invalid = choice is null ? null : ArtifactDownloader.Ineligible(choice);
        bool busy = HubDownloads.Session.Status.Active || HubInstaller.Preparing || _checkingInstall || _installConfirmation is not null || HubInstaller.Operation(this) is not null;
        _refresh!.Enabled = !busy && !_refreshing;
        _artifact!.Enabled = !busy && !_refreshing && _choices.Count > 0;
        _startDownload!.Enabled = !busy && !_refreshing && choice is not null && invalid is null;
        _cancelDownload!.Enabled = HubDownloads.Session.Status.Active && !HubDownloads.Session.Status.Cancelling;
        if (showSelection && !busy)
            _downloadStatus!.Text = choice is null ? "Nenhum APK com metadados disponível; atualize ou consulte o canal do Product."
                : invalid?.Message ?? $"{choice.Asset.Size:N0} bytes · limite de {ArtifactDownloader.DefaultMaxBytes / 1024 / 1024} MiB."
                    + (choice.Stale ? " Metadados do último estado conhecido." : "");
        if (showSelection && !busy && HubDownloads.Session.Status is { Result: { } prior } previous)
            _downloadStatus!.Text += $"\nÚltima tentativa: {previous.Choice?.Asset.Name ?? "interrompida"} · {prior.Message}";
        UpdateInstallationControls();
    }

    void ObserveDownloads()
    {
        if (_observingDownloads) return;
        _observingDownloads = true;
        HubDownloads.Session.Changed += DownloadChanged;
        RenderDownloadStatus();
    }

    void UnobserveDownloads()
    {
        _observingDownloads = false;
        HubDownloads.Session.Changed -= DownloadChanged;
    }

    void DownloadChanged() => RunOnUiThread(() =>
    {
        if (!_destroyed && _observingDownloads) RenderDownloadStatus();
    });

    void RenderDownloadStatus()
    {
        var status = HubDownloads.Session.Status;
        if (_downloadStatus is null) return;
        if (status.Active)
            _downloadStatus.Text = status.Cancelling ? "Cancelando e descartando o arquivo incompleto…"
                : $"{status.Choice?.ProductName} · {status.Choice?.Asset.Name}\n"
                    + (status.Progress is { } p ? $"Baixando: {p.Bytes:N0} de {p.ExpectedBytes:N0} bytes."
                        : "Preparando download…")
                    + " Continua em segundo plano."
                    + (OperatingSystem.IsAndroidVersionAtLeast(33) && CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted
                        ? " Notificações bloqueadas; use Cancelar aqui ou o painel de apps ativos do Android." : "");
        else if (status.Result is { } result) _downloadStatus.Text = status.Choice is { } completed
            ? $"{completed.ProductName} · {completed.Asset.Name}\n{result.Message}" : result.Message;
        _refresh!.Enabled = !status.Active && !_refreshing;
        UpdateDownloadControls();
    }

    void DownloadSelected()
    {
        if (_destroyed || _refreshing || HubInstaller.Preparing || _checkingInstall || _installConfirmation is not null || HubInstaller.Operation(this) is not null || HubDownloads.Session.Status.Active || SelectedArtifact() is not { } choice) return;
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && !_notificationPermissionRequested &&
            CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
        {
            _notificationPermissionRequested = true;
            _awaitingNotificationPermission = choice;
            RequestPermissions([Android.Manifest.Permission.PostNotifications], NotificationPermissionRequest);
            return;
        }
        StartDownload(choice);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != NotificationPermissionRequest) return;
        var choice = _awaitingNotificationPermission;
        _awaitingNotificationPermission = null;
        if (!_destroyed && choice is not null) StartDownload(choice);
    }

    void StartDownload(ArtifactChoice choice)
    {
        if (HubDownloads.Session.Queue(choice) is not { } operation) return;
        try
        {
            var intent = new Intent(this, typeof(ArtifactDownloadService))
                .SetAction(ArtifactDownloadService.StartAction)
                .PutExtra(ArtifactDownloadService.OperationExtra, operation.ToString());
            StartForegroundService(intent);
        }
        catch (Exception)
        { HubDownloads.Session.FailQueued(operation, "O Android não permitiu iniciar o download. Abra o Hub e tente novamente."); }
        RenderDownloadStatus();
    }
}
