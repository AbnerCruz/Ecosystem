using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using Lunet.Graphics;
using Lunet.Studio;
using AndroidColor = Android.Graphics.Color;
using AndroidMediaPlayer = global::Android.Media.MediaPlayer;
using Path = System.IO.Path;

namespace Lunet.Android;

/// <summary>Catálogo visual de arquivos Content/ e Assets/, sem índice persistente.</summary>
public sealed partial class MainActivity
{
    private Dialog? _assetsDialog;

    private void ShowAssets()
    {
        if (_project is null) return;
        SaveCurrent();
        _assetsDialog?.Dismiss();

        // Cada abertura relê a fonte de verdade; edits e imports aparecem sem sincronização adicional.
        var assets = ProjectAssetCatalog.Build(_project.ListFiles());
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        _assetsDialog = dialog;
        string query = "";
        ProjectAssetKind? category = null;
        var root = Vertical();
        root.SetPadding(Dp(12), Dp(10), Dp(12), Dp(4));

        var header = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        header.AddView(new TextView(this) { Text = "Assets", TextSize = 22 },
            new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
        header.AddView(MakeButton("＋ Criar", () => { dialog.Dismiss(); ShowArtTools(); }));
        header.AddView(MakeButton("Fechar", () => dialog.Dismiss()));
        root.AddView(header);

        var summary = new TextView(this) { Text = "Recursos do projeto", TextSize = 12 };
        summary.SetPadding(0, Dp(4), 0, Dp(8));
        root.AddView(summary);

        var search = new EditText(this) { Hint = "Buscar por nome ou caminho..." };
        search.SetSingleLine(true);
        root.AddView(search);

        var categories = new HorizontalScrollView(this) { HorizontalScrollBarEnabled = false };
        var categoryRow = new LinearLayout(this) { Orientation = Orientation.Horizontal };
        categories.AddView(categoryRow);
        root.AddView(categories);

        var items = Vertical();
        var scroll = new ScrollView(this);
        scroll.AddView(items);
        root.AddView(scroll, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, 0, 1));

        void Render()
        {
            items.RemoveAllViews();
            var filtered = assets.Where(a =>
                (category is null || a.Kind == category) &&
                (query.Length == 0 || a.Path.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
            summary.Text = $"{filtered.Length} recurso(s) · {assets.Count} no projeto";
            if (filtered.Length == 0)
            {
                var empty = new TextView(this) { Text = "Nenhum recurso encontrado. Use + Criar ou importe arquivos no Explorer.", TextSize = 14 };
                empty.SetPadding(Dp(6), Dp(28), Dp(6), Dp(10));
                items.AddView(empty);
                return;
            }
            foreach (var asset in filtered.Take(250))
            {
                var captured = asset;
                var row = new LinearLayout(this) { Orientation = Orientation.Horizontal };
                row.SetGravity(GravityFlags.CenterVertical);
                row.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
                row.AddView(LunetUi.IconImage(this, AssetIcon(asset.Kind), 22, LunetUi.AccentText), new LinearLayout.LayoutParams(Dp(36), Dp(36)));
                var texts = Vertical();
                texts.AddView(new TextView(this) { Text = Path.GetFileName(asset.Path), TextSize = 16 });
                var detail = new TextView(this) { Text = $"{AssetCategoryName(asset.Kind)} · {asset.Path}", TextSize = 11 };
                detail.SetTextColor(AndroidColor.Rgb(165, 180, 200));
                texts.AddView(detail);
                row.AddView(texts, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1));
                row.AddView(LunetUi.IconImage(this, "chevron-right", 20, LunetUi.TextFaint), new LinearLayout.LayoutParams(Dp(24), Dp(24)));
                row.Clickable = true;
                row.Click += (_, _) => ShowAssetDetails(captured, dialog);
                items.AddView(row);
                var line = new View(this);
                line.SetBackgroundColor(AndroidColor.Rgb(60, 66, 78));
                items.AddView(line, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(1)));
            }
            if (filtered.Length > 250)
                items.AddView(new TextView(this) { Text = "Exibindo os primeiros 250. Refine a busca para encontrar outros arquivos.", TextSize = 12 });
        }

        var filterButtons = new List<(Button Button, ProjectAssetKind? Value)>();

        void Choose(ProjectAssetKind? kind)
        {
            category = kind;
            foreach (var (button, value) in filterButtons)
                button.Alpha = category == value ? 1f : 0.55f;
            Render();
        }

        void AddCategory(string label, ProjectAssetKind? kind)
        {
            var button = MakeButton(label, () => Choose(kind));
            button.SetMinWidth(0);
            button.SetMinimumWidth(0);
            categoryRow.AddView(button);
            filterButtons.Add((button, kind));
        }
        AddCategory("Tudo", null);
        AddCategory("Sprites", ProjectAssetKind.Sprite);
        AddCategory("Mapas", ProjectAssetKind.TileMap);
        AddCategory("Imagens", ProjectAssetKind.Image);
        AddCategory("Áudio", ProjectAssetKind.Audio);
        AddCategory("Fontes", ProjectAssetKind.Font);
        AddCategory("Dados", ProjectAssetKind.Data);
        search.TextChanged += (_, _) => { query = (search.Text ?? "").Trim(); Render(); };
        Choose(null);

        dialog.SetContentView(root);
        dialog.SetCancelable(true);
        dialog.DismissEvent += (_, _) => { if (ReferenceEquals(_assetsDialog, dialog)) _assetsDialog = null; };
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }

    /// <summary>Painel de um recurso: prévia real (sprite animado, imagem, onda com reprodução, fonte, dados) e ações.</summary>
    private void ShowAssetDetails(ProjectAsset asset, Dialog? catalog)
    {
        if (_project is null) return;
        var project = _project;
        var dialog = new Dialog(this);
        dialog.RequestWindowFeature((int)WindowFeatures.NoTitle);
        var root = LunetUi.Column(this, 10);
        root.SetPadding(Dp(16), Dp(14), Dp(16), Dp(14));
        var disposables = new List<Action>();

        var head = LunetUi.Row(this, 8);
        head.AddView(LunetUi.IconImage(this, AssetIcon(asset.Kind), 24, LunetUi.AccentText), new LinearLayout.LayoutParams(Dp(30), Dp(30)));
        var name = LunetUi.Label(this, Path.GetFileName(asset.Path), LunetUi.TextTitle, LunetUi.Text, bold: true);
        name.SetSingleLine(true);
        name.Ellipsize = TextUtils.TruncateAt.Middle;
        head.AddView(name, LunetUi.Weight());
        head.AddView(LunetUi.IconByName(this, "close", "Fechar", () => dialog.Dismiss()), new LinearLayout.LayoutParams(Dp(44), Dp(44)));
        root.AddView(head);

        string? fullPath = null;
        try { fullPath = SafeAssetFilePath(project, asset.Path); }
        catch (IOException) { /* arquivo some entre a lista e o toque */ }
        var info = new List<string> { AssetCategoryName(asset.Kind) };
        if (fullPath is not null) info.Add(FormatSize(new FileInfo(fullPath).Length));
        var details = LunetUi.Label(this, "", LunetUi.TextCaption, LunetUi.TextMuted);
        root.AddView(details);

        void AddPreview(View view, int heightDp) => root.AddView(view, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(heightDp)));
        void Note(string text) => root.AddView(LunetUi.Label(this, text, LunetUi.TextLabel, LunetUi.TextMuted));

        try
        {
            switch (asset.Kind)
            {
                case ProjectAssetKind.Sprite:
                {
                    var sprite = SpriteDocument.Parse(project.ReadText(asset.Path));
                    var view = FramePreviewView.FromSprite(this, sprite);
                    disposables.Add(view.Release);
                    info.Add($"{sprite.Width}×{sprite.Height} px");
                    info.Add(sprite.Frames.Count == 1 ? "1 frame" : $"{sprite.Frames.Count} frames · {sprite.Fps} FPS");
                    AddPreview(view, 240);
                    break;
                }
                case ProjectAssetKind.Image or ProjectAssetKind.TileMap:
                {
                    string? source = asset.PreviewPath;
                    if (asset.Kind == ProjectAssetKind.TileMap)
                    {
                        var map = TileMap.Parse(project.ReadText(asset.Path));
                        source = "Content/" + map.TexturePath;
                        info.Add($"{map.Width}×{map.Height} células · {map.LayerCount} camada(s)");
                    }
                    if (source is null) { Note("Sem imagem para mostrar."); break; }
                    var bitmap = DecodeForPreview(SafeAssetFilePath(project, source), out int w, out int h);
                    if (bitmap is null) { Note("Imagem grande demais ou ilegível para a prévia."); break; }
                    var view = new FramePreviewView(this, [bitmap]);
                    disposables.Add(view.Release);
                    if (asset.Kind == ProjectAssetKind.Image) info.Add($"{w}×{h} px");
                    AddPreview(view, 240);
                    break;
                }
                case ProjectAssetKind.Audio when fullPath is not null:
                {
                    WavePeak[]? peaks = null;
                    double seconds = 0;
                    if (asset.Path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) && new FileInfo(fullPath).Length <= 24L * 1024 * 1024)
                    {
                        try
                        {
                            var wave = WaveAudioDocument.Parse(File.ReadAllBytes(fullPath));
                            peaks = wave.GetPeaks(320);
                            seconds = wave.DurationSeconds;
                            info.Add($"{wave.SampleRate} Hz · {(wave.Channels == 1 ? "mono" : "estéreo")} · {TimeSpan.FromSeconds(seconds):m\\:ss\\.f}");
                        }
                        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
                        {
                            Note("Não foi possível desenhar a onda (" + ex.Message + "), mas dá para ouvir.");
                        }
                    }
                    var preview = new AudioPreviewView(this, peaks, seconds, Path.Combine(CacheDir!.AbsolutePath, "audio-studio"), file: fullPath);
                    disposables.Add(preview.Dispose);
                    root.AddView(preview);
                    break;
                }
                case ProjectAssetKind.Font when fullPath is not null:
                {
                    var sample = LunetUi.Label(this, "Aa Bb Cc 0123\nO rato roeu a roupa do rei", 26, LunetUi.Text);
                    sample.SetTypeface(Typeface.CreateFromFile(fullPath), TypefaceStyle.Normal);
                    sample.SetPadding(Dp(14), Dp(14), Dp(14), Dp(14));
                    sample.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
                    root.AddView(sample);
                    break;
                }
                case ProjectAssetKind.Data when fullPath is not null:
                {
                    var text = File.ReadAllText(fullPath);
                    var snippet = LunetUi.Label(this, text.Length > 1400 ? text[..1400] + "\n…" : text, 11, LunetUi.TextMuted);
                    snippet.SetTypeface(LunetUi.Mono, TypefaceStyle.Normal);
                    snippet.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
                    snippet.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.Radius, LunetUi.Border);
                    var scroller = new ScrollView(this);
                    scroller.AddView(snippet);
                    AddPreview(scroller, 160);
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ProjectException or ArgumentException or UnauthorizedAccessException)
        {
            Note("Não foi possível mostrar a prévia: " + ex.Message);
        }
        details.Text = string.Join(" · ", info) + "\n" + asset.Path;

        if (asset.UsageCode.StartsWith("//", StringComparison.Ordinal) is false)
        {
            var code = LunetUi.Label(this, asset.UsageCode, 12, LunetUi.AccentText);
            code.SetTypeface(LunetUi.Mono, TypefaceStyle.Normal);
            code.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
            code.Background = LunetUi.Shape(this, LunetUi.Surface, LunetUi.RadiusSmall, LunetUi.Border);
            root.AddView(code);
        }

        var actions = LunetUi.Row(this, 8);
        if (asset.UsageCode.StartsWith("//", StringComparison.Ordinal) is false)
            actions.AddView(LunetUi.Button(this, "⧉ Copiar uso", () =>
            {
                if (GetSystemService(ClipboardService) is global::Android.Content.ClipboardManager clipboard)
                    clipboard.PrimaryClip = ClipData.NewPlainText("Código Lunet", asset.UsageCode);
                Toast.MakeText(this, "Código copiado", ToastLength.Short)?.Show();
            }), LunetUi.Weight());
        string? editLabel = asset.Kind switch
        {
            ProjectAssetKind.Sprite => "✎ Editar sprite",
            ProjectAssetKind.TileMap => "▦ Editar mapa",
            ProjectAssetKind.Audio when asset.Path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) => "∿ Editar áudio",
            ProjectAssetKind.Data => "⌨ Abrir no editor",
            _ => null,
        };
        if (editLabel is not null)
            actions.AddView(LunetUi.Button(this, editLabel, () =>
            {
                dialog.Dismiss();
                catalog?.Dismiss();
                if (_explorerOpenForPreview) { _explorerOpenForPreview = false; if (_drawer?.Visibility == ViewStates.Visible) ToggleExplorer(); }
                try
                {
                    if (asset.Kind == ProjectAssetKind.Sprite) OpenSpriteStudio(asset.Path, SpriteDocument.Parse(project.ReadText(asset.Path)));
                    else if (asset.Kind == ProjectAssetKind.TileMap) OpenTileMap(asset.Path, TileMap.Parse(project.ReadText(asset.Path)));
                    else if (asset.Kind == ProjectAssetKind.Audio) OpenWaveAudio(asset.Path);
                    else OpenFile(asset.Path);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ProjectException or ArgumentException)
                {
                    Toast.MakeText(this, "Não foi possível abrir: " + ex.Message, ToastLength.Long)?.Show();
                }
            }, ButtonStyle.Primary), LunetUi.Weight());
        root.AddView(actions);
        if (asset.Kind is ProjectAssetKind.Sprite or ProjectAssetKind.TileMap)
            root.AddView(LunetUi.Button(this, "{ } Ver o arquivo como código", () =>
            {
                dialog.Dismiss();
                catalog?.Dismiss();
                if (_drawer?.Visibility == ViewStates.Visible) ToggleExplorer();
                OpenFile(asset.Path);
            }, ButtonStyle.Quiet));

        var scroll = new ScrollView(this);
        scroll.AddView(root);
        dialog.SetContentView(scroll);
        dialog.DismissEvent += (_, _) => { foreach (var dispose in disposables) dispose(); };
        dialog.Show();
        dialog.Window?.SetBackgroundDrawable(LunetUi.Shape(this, LunetUi.SurfaceRaised, LunetUi.RadiusLarge));
        dialog.Window?.SetLayout((int)(Resources!.DisplayMetrics!.WidthPixels * 0.94), ViewGroup.LayoutParams.WrapContent);
    }

    private bool _explorerOpenForPreview;

    private static string AssetIcon(ProjectAssetKind kind) => kind switch
    {
        ProjectAssetKind.Sprite => "sprite",
        ProjectAssetKind.TileMap => "tilemap",
        ProjectAssetKind.Image => "image",
        ProjectAssetKind.Audio => "waveform",
        ProjectAssetKind.Font => "font",
        _ => "data",
    };

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => bytes + " B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.#} MB",
    };

    /// <summary>Decodifica com amostragem para caber na memória; devolve null se for grande demais.</summary>
    private static Bitmap? DecodeForPreview(string path, out int width, out int height)
    {
        width = height = 0;
        if (new FileInfo(path).Length > 24L * 1024 * 1024) return null;
        using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        BitmapFactory.DecodeFile(path, bounds)?.Dispose();
        width = bounds.OutWidth; height = bounds.OutHeight;
        if (width is <= 0 or > 8192 || height is <= 0 or > 8192) return null;
        int sample = 1;
        while (Math.Max(width, height) / sample > 1024) sample *= 2;
        using var options = new BitmapFactory.Options { InSampleSize = sample };
        return BitmapFactory.DecodeFile(path, options);
    }

    private static string AssetCategoryName(ProjectAssetKind kind) => kind switch
    {
        ProjectAssetKind.Sprite => "Sprite / animação",
        ProjectAssetKind.TileMap => "Tilemap",
        ProjectAssetKind.Image => "Imagem",
        ProjectAssetKind.Audio => "Áudio",
        ProjectAssetKind.Font => "Fonte",
        _ => "Dados"
    };

    private static string SafeAssetFilePath(LunetProject project, string relative)
    {
        var basePath = Path.GetFullPath(project.Directory);
        var path = Path.GetFullPath(Path.Combine(basePath, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(basePath + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(path))
            throw new IOException("Arquivo fora do projeto ou inexistente.");
        return path;
    }
}
