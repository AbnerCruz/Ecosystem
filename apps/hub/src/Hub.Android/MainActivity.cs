using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using Hub.Core;

namespace HubApp;

/// <summary>
/// Tela única do Hub: lê o estado (Hub.Core), escolhe o que mostrar (SnapshotPolicy) e desenha o HubScreen. Somente leitura:
/// nada é gravado no repositório; o único arquivo local é o cache do último estado bom.
/// </summary>
[Activity(Label = "Ecosystem Hub", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenLayout)]
public sealed class MainActivity : Activity
{
    // Ponto de partida do app: o repositório do Ecosystem é a única coisa que o Hub não descobre sozinho (HubOptions).
    static readonly HubOptions Options = new(new RepositoryRef("AbnerCruz", "Ecosystem"));
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    ScreenRenderer? _renderer;
    Button? _refresh;
    TextView? _status;
    CancellationTokenSource? _load;

    string CacheFile => System.IO.Path.Combine(FilesDir!.AbsolutePath, "hub-snapshot.json");

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

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
        var scroll = new ScrollView(this);
        scroll.AddView(content);
        root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));
        SetContentView(root);

        _renderer = new ScreenRenderer(this, content);

        // Mostra já o último estado conhecido (marcado como tal) e só então tenta a leitura atual.
        var cached = SnapshotPolicy.Choose(null, ReadCache());
        _renderer.Render(HubScreenBuilder.Build(cached.Show));
        Refresh();
    }

    protected override void OnDestroy()
    {
        _load?.Cancel();
        base.OnDestroy();
    }

    async void Refresh()
    {
        _load?.Cancel();
        _load = new CancellationTokenSource();
        var ct = _load.Token;
        _refresh!.Enabled = false;
        _status!.Text = "Atualizando…";

        HubSnapshot? fresh = null;
        try { fresh = await Task.Run(() => new HubLoader(Http, Options).LoadAsync(ct), ct); }
        catch (OperationCanceledException) { return; }
        catch (Exception) { /* falha isolada: a política cai para o último estado bom */ }

        var choice = SnapshotPolicy.Choose(fresh, ReadCache());
        if (choice.ToCache is { } json) WriteCache(json);
        _renderer!.Render(HubScreenBuilder.Build(choice.Show));
        _status.Text = choice.Show is null ? "Nada para mostrar."
            : choice.Show.Stale ? "Último estado conhecido — a leitura atual falhou."
            : $"Atualizado às {DateTime.Now:HH:mm}.";
        _refresh.Enabled = true;
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
