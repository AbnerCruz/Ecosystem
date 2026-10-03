using Android.Views;
using Android.Widget;
using Hub.Core;

namespace HubApp;

public sealed partial class MainActivity
{
    // Cliente separado das leituras do GitHub: sem token, cookies ou credencial. O Core limita todo o download a 5 minutos.
    static readonly HttpClient ArtifactHttp = new(new HttpClientHandler { UseCookies = false })
        { Timeout = Timeout.InfiniteTimeSpan };
    ArtifactDownloader? _downloader;
    Spinner? _artifact;
    Button? _startDownload;
    Button? _cancelDownload;
    TextView? _downloadStatus;
    IReadOnlyList<ArtifactChoice> _choices = [];
    CancellationTokenSource? _download;

    void AddDownloads(LinearLayout root)
    {
        _downloader = new ArtifactDownloader(ArtifactHttp, Path.Combine(CacheDir!.AbsolutePath, "hub-artifacts"));
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
        _cancelDownload.Click += (_, _) => { _download?.Cancel(); _cancelDownload!.Enabled = false; };
        actions.AddView(_startDownload, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        actions.AddView(_cancelDownload, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
        panel.AddView(actions);
        _downloadStatus = new TextView(this) { TextSize = 13 };
        panel.AddView(_downloadStatus);
        root.AddView(panel);
    }

    void ShowDownloadChoices(HubSnapshot? snapshot)
    {
        if (_download is not null) return;
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
        bool busy = _download is not null;
        _artifact!.Enabled = !busy && !_refreshing && _choices.Count > 0;
        _startDownload!.Enabled = !busy && !_refreshing && choice is not null && invalid is null;
        _cancelDownload!.Enabled = busy;
        if (showSelection && !busy)
            _downloadStatus!.Text = choice is null ? "Nenhum APK com metadados disponível; atualize ou consulte o canal do Product."
                : invalid?.Message ?? $"{choice.Asset.Size:N0} bytes · limite de {ArtifactDownloader.DefaultMaxBytes / 1024 / 1024} MiB."
                    + (choice.Stale ? " Metadados do último estado conhecido." : "");
    }

    async void DownloadSelected()
    {
        if (_destroyed || _refreshing || _download is not null || SelectedArtifact() is not { } choice) return;
        using var cancellation = new CancellationTokenSource();
        _download = cancellation;
        _refresh!.Enabled = false;
        UpdateDownloadControls();
        _downloadStatus!.Text = "Baixando e conferindo…";
        var progress = new Progress<ArtifactDownloadProgress>(p =>
        {
            if (!_destroyed && ReferenceEquals(_download, cancellation) && !cancellation.IsCancellationRequested)
                _downloadStatus!.Text = $"Baixando: {p.Bytes:N0} de {p.ExpectedBytes:N0} bytes.";
        });
        try
        {
            var result = await _downloader!.DownloadAsync(choice, progress, cancellation.Token);
            if (!_destroyed) _downloadStatus!.Text = result.Message;
        }
        catch (Exception)
        {
            if (!_destroyed) _downloadStatus!.Text = "Não foi possível concluir o download. Tente atualizar o catálogo.";
        }
        finally
        {
            _download = null;
            if (!_destroyed)
            {
                _refresh!.Enabled = true;
                UpdateDownloadControls();
            }
        }
    }
}
