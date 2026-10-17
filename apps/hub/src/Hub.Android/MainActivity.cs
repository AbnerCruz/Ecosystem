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
    bool _startupFailed;

    string CacheFile => System.IO.Path.Combine(FilesDir!.AbsolutePath, "hub-snapshot.json");

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        try { InitializeScreen(); }
        catch (Exception error) when (error is not OutOfMemoryException)
        { HandleStartupFailure(error); }
    }

    private void InitializeScreen()
    {
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

    private void HandleStartupFailure(Exception error)
    {
        _startupFailed = true;
        _load?.Cancel();
        _approvedPackages.Clear();
        _refreshing = false;
        if (_refresh is not null) _refresh.Enabled = false;

        // Uma exceção gerenciada deve resultar em tela recuperável, não processo
        // encerrado nem instalação autorizada por estado incompleto.
        var diagnostic = HubStartupDiagnostic.From(error);
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetPadding(ScreenRenderer.Dp(this, 20), ScreenRenderer.Dp(this, 40),
            ScreenRenderer.Dp(this, 20), ScreenRenderer.Dp(this, 20));
        var title = new TextView(this) { Text = "Ecosystem Hub não iniciou", TextSize = 22 };
        title.SetTypeface(null, global::Android.Graphics.TypefaceStyle.Bold);
        var detail = new TextView(this) { Text = diagnostic.Explanation, TextSize = 16 };
        var code = new TextView(this) { Text = "Diagnóstico: " + diagnostic.Id, TextSize = 14 };
        var hint = new TextView(this)
        {
            Text = "Os dados e aplicativos instalados não foram apagados. " +
                "Se o problema persistir, informe o código de diagnóstico.",
            TextSize = 14
        };
        var retry = new Button(this) { Text = "Tentar novamente" };
        retry.Click += (_, _) => Recreate(); // nova Activity; nenhuma instalação automática
        root.AddView(title);
        root.AddView(detail);
        root.AddView(code);
        root.AddView(hint);
        root.AddView(retry);
        SetContentView(root);
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
        if (_startupFailed) return;
        try
        {
            ObserveDownloads();
            HubInstaller.Recover(this);
            UpdateInstallationControls();
            PromptPendingIpcPairing();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        { HandleStartupFailure(error); }
    }

    protected override void OnStop()
    {
        UnobserveDownloads();
        _tools?.Dismiss();
        base.OnStop();
    }

    async void Refresh()
    {
        if (_startupFailed || HubDownloads.Session.Status.Active || _destroyed) return;
        _load?.Cancel();
        // Uma atualização anterior pode terminar depois da nova. Só a atualização
        // ainda ativa pode reabilitar controles ou publicar resultados.
        var request = new CancellationTokenSource();
        _load = request;
        var ct = request.Token;

        try
        {
            _refreshing = true;
            _refresh!.Enabled = false;
            UpdateDownloadControls();
            _status!.Text = "Atualizando…";
            _releaseNotes?.Refresh(_snapshot, true);

            HubSnapshot? fresh = null;
            try { fresh = await Task.Run(() => new HubLoader(Http, Options).LoadAsync(ct), ct); }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception) { /* rede/catálogo falhou; política usa último estado bom */ }

            if (ct.IsCancellationRequested || _destroyed || _startupFailed || !ReferenceEquals(_load, request)) return;

            var choice = SnapshotPolicy.Choose(fresh, ReadCache());
            if (choice.ToCache is { } json) WriteCache(json);
            _snapshot = choice.Show;
            _renderer!.Render(HubScreenBuilder.Build(choice.Show));
            _releaseNotes?.Refresh(_snapshot, false);
            ShowDownloadChoices(choice.Show);
            _status.Text = choice.Show is null ? "Nada para mostrar."
                : choice.Show.Stale ? "Último estado conhecido — a leitura atual falhou."
                : $"Atualizado às {DateTime.Now:HH:mm}.";

            try { await RefreshInstallationTrust(ct); }
            catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Falha de rede/parsing da aprovação não pode virar exceção solta
                // de async void nem autorização antiga para instalar/abrir.
                if (ct.IsCancellationRequested || _destroyed || _startupFailed || !ReferenceEquals(_load, request)) return;
                _approvedPackages.Clear();
                _status.Text += " Confiança de instalação indisponível; instalação bloqueada.";
                UpdateInstallationControls();
            }
        }
        catch (System.OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception)
        {
            // Nunca inferir sucesso; manter apenas a última projeção já exibida.
            if (!_destroyed && !_startupFailed && !ct.IsCancellationRequested && ReferenceEquals(_load, request))
            {
                _approvedPackages.Clear();
                _status!.Text = "Falha ao atualizar o Hub. Tente novamente; instalações bloqueadas.";
                UpdateInstallationControls();
            }
        }
        finally
        {
            if (ReferenceEquals(_load, request))
            {
                _load = null;
                _refreshing = false;
                if (!_destroyed && !_startupFailed)
                {
                    _releaseNotes?.Refresh(_snapshot, false);
                    UpdateDownloadControls();
                }
            }
            request.Dispose();
        }
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
