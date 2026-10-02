using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using Hub.Core;

namespace Hub.Android;

/// <summary>
/// Tela única e somente leitura do Hub. Toda decisão do que mostrar vem do <see cref="HubScreenBuilder"/> (testado no CI);
/// aqui só se desenha. Ao abrir, mostra na hora o último estado guardado e em seguida lê o Ecosystem de novo.
/// </summary>
[Activity(Label = "Hub", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenLayout)]
public sealed class MainActivity : Activity
{
    // Ponto de partida: o repositório do Ecosystem é a única coisa que o app não descobre sozinho. Público (DEC-0012-A): sem token.
    const string RepositoryUrl = "https://github.com/AbnerCruz/Ecosystem";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    string _cachePath = "";
    Button? _refresh;
    LinearLayout? _content;
    CancellationTokenSource? _cts;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _cachePath = System.IO.Path.Combine(FilesDir!.AbsolutePath, "snapshot.json");
        BuildLayout();
        Render(HubScreenBuilder.Build(SnapshotFile.Load(_cachePath)));   // o último estado conhecido aparece na hora
        _ = RefreshAsync();
    }

    protected override void OnDestroy()
    {
        _cts?.Cancel();
        base.OnDestroy();
    }

    void BuildLayout()
    {
        var root = new LinearLayout(this) { Orientation = Orientation.Vertical };
        root.SetFitsSystemWindows(true);
        root.SetPadding(Dp(16), Dp(8), Dp(16), Dp(8));

        _refresh = new Button(this) { Text = "Atualizar" };
        _refresh.Click += (_, _) => _ = RefreshAsync();
        root.AddView(_refresh, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));

        var scroll = new ScrollView(this);
        _content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        scroll.AddView(_content, new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent));
        root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1f));

        SetContentView(root);
    }

    async Task RefreshAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _refresh!.Enabled = false;
        _refresh.Text = "Atualizando…";
        try
        {
            var repo = RepositoryRef.TryParse(RepositoryUrl)!;
            var live = await new HubLoader(Http, new HubOptions(repo)).LoadAsync(ct);
            var (show, save) = HubRefresh.Resolve(live, SnapshotFile.Load(_cachePath));
            if (save) SnapshotFile.Save(_cachePath, show);
            Render(HubScreenBuilder.Build(show));
        }
        catch (OperationCanceledException) { /* tela fechada ou nova atualização pedida */ }
        finally
        {
            _refresh.Enabled = true;
            _refresh.Text = "Atualizar";
        }
    }

    void Render(HubScreen screen)
    {
        var content = _content!;
        content.RemoveAllViews();

        if (screen.Banner is { } banner)
        {
            var b = Text(banner, 14, bold: true, color: Color.Black);
            b.SetBackgroundColor(Color.ParseColor("#FFE082"));
            b.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            content.AddView(b);
        }

        foreach (var section in screen.Sections)
        {
            var title = Text(section.Title, 20, bold: true);
            title.SetPadding(0, Dp(18), 0, Dp(4));
            content.AddView(title);

            foreach (var line in section.Lines)
            {
                var row = new LinearLayout(this) { Orientation = Orientation.Vertical };
                row.SetPadding(0, Dp(4), 0, Dp(4));
                row.AddView(Text(line.Text, 15));
                if (line.Detail is { Length: > 0 } detail) row.AddView(Text(detail, 13, color: Color.Gray));
                content.AddView(row);
            }
        }
    }

    TextView Text(string text, float sp, bool bold = false, Color? color = null)
    {
        var t = new TextView(this) { Text = text };
        t.SetTextSize(ComplexUnitType.Sp, sp);
        if (bold) t.SetTypeface(null, TypefaceStyle.Bold);
        if (color is { } c) t.SetTextColor(c);
        return t;
    }

    int Dp(float v) => (int)TypedValue.ApplyDimension(ComplexUnitType.Dip, v, Resources!.DisplayMetrics);
}
