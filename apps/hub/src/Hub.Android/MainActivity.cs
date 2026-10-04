using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Hub.Core;

namespace HubApp;

/// <summary>
/// Tela única do Hub: lê o estado (Hub.Core), escolhe o que mostrar (SnapshotPolicy) e desenha o HubScreen. Leituras do repositório são somente leitura:
/// nada é gravado no repositório; cache e APKs conferidos ficam na área privada do app.
/// </summary>
[Activity(Label = "Ecosystem Hub", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenLayout)]
public sealed partial class MainActivity : Activity
{
    // Ponto de partida do app: o repositório do Ecosystem é a única coisa que o Hub não descobre sozinho (HubOptions).
    static readonly HubOptions Options = new(new RepositoryRef("AbnerCruz", "Ecosystem"));
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    ScreenRenderer? _renderer;
    HubSnapshot? _snapshot;
    ReleaseNotesDialogs? _releaseNotes;
    LocalToolsDialog? _tools;
    Button? _refresh;
    TextView? _status;
    CancellationTokenSource? _load;
    bool _refreshing;
    bool _destroyed;

    string CacheFile => System.IO.Path.Combine(FilesDir!.AbsolutePath, "hub-snapshot.json");

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HubInstaller.Changed += InstallationChanged;

        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetOnApplyWindowInsetsListener(new SystemBarsPadding());

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        header.SetPadding(ScreenRenderer.Dp(this, 16), ScreenRenderer.Dp(this, 12), ScreenRenderer.Dp(this, 16), ScreenRenderer.Dp(this, 4));
        header.SetGravity(GravityFlags.CenterVertical);
        var title = new TextView(this) { Text = "Ecosystem Hub", TextSize = 22 };
        title.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
        header.AddView(title, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1f));
        _refresh = new Button(this) { Text = "Atualizar" };
        _refresh.Click += (_, _) => Refresh();
        header.AddView(_refresh, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent));
        root.AddView(header);

        _status = new TextView(this) { TextSize = 13, Alpha = 0.7f };
        _status.SetPadding(ScreenRenderer.Dp(this, 16), 0, ScreenRenderer.Dp(this, 16), ScreenRenderer.Dp(this, 8));
        root.AddView(_status);

        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        _tools = new LocalToolsDialog(this);
        var tools = new Button(this) { Text = "Analisar texto" };
        tools.Click += (_, _) => _tools.Show();
        content.AddView(tools);
        AddIpcControls(content);
        AddDownloads(content);
        var productsContent = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.AddView(productsContent);
        var scroll = new ScrollView(this);
        scroll.AddView(content);
        root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        SetContentView(root);

        _releaseNotes = new ReleaseNotesDialogs(this);
        _renderer = new ScreenRenderer(this, productsContent, id => _releaseNotes.ShowHistory(ReleaseHistoryBuilder.Build(_snapshot, id, loading: _refreshing)));

        // Mostra já o último estado conhecido (marcado como tal) e só então tenta a leitura atual.
        var cached = SnapshotPolicy.Choose(null, ReadCache());
        _snapshot = cached.Show;
        _renderer.Render(HubScreenBuilder.Build(cached.Show));
        ShowDownloadChoices(cached.Show);
        Refresh();
    }

    protected override void OnDestroy()
    {
        UnobserveDownloads();
        HubInstaller.Changed -= InstallationChanged;
        _prepareInstall?.Cancel();
        _installConfirmation?.Dismiss();
        _releaseNotes?.Dismiss();
        _tools?.Dismiss();
        _destroyed = true;
        _load?.Cancel();
        base.OnDestroy();
    }

    protected override void OnStart()
    {
        base.OnStart();
        ObserveDownloads();
        HubInstaller.Recover(this);
        UpdateInstallationControls();
        PromptPendingIpcPairing();
    }

    protected override void OnStop()
    {
        UnobserveDownloads();
        _tools?.Dismiss();
        base.OnStop();
    }

    async void Refresh()
    {
        if (HubDownloads.Session.Status.Active || _destroyed) return;
        _load?.Cancel();
        _load = new CancellationTokenSource();
        var ct = _load.Token;
        _refreshing = true;
        _refresh!.Enabled = false;
        UpdateDownloadControls();
        _status!.Text = "Atualizando…";
        _releaseNotes?.Refresh(_snapshot, true);

        HubSnapshot? fresh = null;
        try { fresh = await Task.Run(() => new HubLoader(Http, Options).LoadAsync(ct), ct); }
        catch (System.OperationCanceledException) { return; }
        catch (Exception) { /* falha isolada: a política cai para o último estado bom */ }

        if (ct.IsCancellationRequested || _destroyed) return;

        var choice = SnapshotPolicy.Choose(fresh, ReadCache());
        if (choice.ToCache is { } json) WriteCache(json);
        _snapshot = choice.Show;
        _renderer!.Render(HubScreenBuilder.Build(choice.Show));
        _releaseNotes?.Refresh(_snapshot, false);
        ShowDownloadChoices(choice.Show);
        _status.Text = choice.Show is null ? "Nada para mostrar."
            : choice.Show.Stale ? "Último estado conhecido — a leitura atual falhou."
            : $"Atualizado às {DateTime.Now:HH:mm}.";
        _refresh.Enabled = true;
        _refreshing = false;
        UpdateDownloadControls();
        try { await RefreshInstallationTrust(ct); } catch (System.OperationCanceledException) { }
    }

    string? ReadCache()
    {
        try { return File.Exists(CacheFile) ? File.ReadAllText(CacheFile) : null; }
        catch (Exception) { return null; }
    }

    void WriteCache(string json)
    {
        try { File.WriteAllText(CacheFile, json); }
        catch (Exception) { /* sem cache o Hub continua funcionando, só não mostra o último estado offline */ }
    }

    /// <summary>Evita que o conteúdo fique sob as barras do sistema (Android 15+ desenha de borda a borda).</summary>
    sealed class SystemBarsPadding : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View v, WindowInsets insets)
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var bars = insets.GetInsets(WindowInsets.Type.SystemBars());
                v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            }
            return insets;
        }
    }
}
