using Android.App;
using Android.Content;
using Android.Graphics;
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
        header.AddView(MakeButton("+ Criar", () => { dialog.Dismiss(); ShowArtTools(); }));
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
                var row = new LinearLayout(this) { Orientation = Orientation.Vertical };
                row.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
                row.AddView(new TextView(this) { Text = Path.GetFileName(asset.Path), TextSize = 16 });
                var detail = new TextView(this) { Text = $"{AssetCategoryName(asset.Kind)} · {asset.Path}", TextSize = 11 };
                detail.SetTextColor(AndroidColor.Rgb(165, 180, 200));
                row.AddView(detail);
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

    private void ShowAssetDetails(ProjectAsset asset, Dialog catalog)
    {
        if (_project is null) return;
        var project = _project;
        var dialog = new Dialog(this);
        var root = Vertical();
        root.SetPadding(Dp(18), Dp(16), Dp(18), Dp(12));
        root.AddView(new TextView(this) { Text = Path.GetFileName(asset.Path), TextSize = 20 });
        root.AddView(new TextView(this) { Text = $"{AssetCategoryName(asset.Kind)}\n{asset.Path}", TextSize = 13 });

        Bitmap? bitmap = null;
        AndroidMediaPlayer? player = null;
        string? preview = asset.PreviewPath;
        if (asset.Kind == ProjectAssetKind.TileMap)
        {
            try
            {
                var map = TileMap.Parse(project.ReadText(asset.Path));
                preview = "Content/" + map.TexturePath;
                root.AddView(new TextView(this) { Text = $"Mapa: {map.Width}×{map.Height} · {map.LayerCount} camadas", TextSize = 13 });
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ProjectException)
            {
                root.AddView(new TextView(this) { Text = "Mapa inválido: " + ex.Message, TextSize = 12 });
            }
        }
        if (preview is not null)
        {
            try
            {
                var fullPath = SafeAssetFilePath(project, preview);
                if (new FileInfo(fullPath).Length <= 24L * 1024 * 1024)
                {
                    using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
                    BitmapFactory.DecodeFile(fullPath, bounds)?.Dispose();
                    if (bounds.OutWidth is > 0 and <= 8192 && bounds.OutHeight is > 0 and <= 8192)
                    {
                        var sample = 1;
                        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / sample > 360) sample *= 2;
                        using var opts = new BitmapFactory.Options { InSampleSize = sample };
                        bitmap = BitmapFactory.DecodeFile(fullPath, opts);
                        if (bitmap is not null)
                        {
                            var view = new ImageView(this);
                            view.SetImageBitmap(bitmap);
                            view.SetAdjustViewBounds(true);
                            view.SetMaxHeight(Dp(200));
                            root.AddView(view, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(200)));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
            {
                root.AddView(new TextView(this) { Text = "Não foi possível abrir a imagem: " + ex.Message, TextSize = 12 });
            }
        }

        if (asset.Kind == ProjectAssetKind.Audio)
        {
            root.AddView(MakeButton("▶ Ouvir áudio", () =>
            {
                try
                {
                    player?.Release();
                    player?.Dispose();
                    player = new AndroidMediaPlayer();
                    player.SetDataSource(SafeAssetFilePath(project, asset.Path));
                    player.Prepared += (_, _) => { if (dialog.IsShowing) player?.Start(); };
                    player.PrepareAsync();
                }
                catch (Exception ex) when (ex is IOException or ArgumentException or InvalidOperationException or global::Java.Lang.Exception)
                {
                    Toast.MakeText(this, "Não foi possível reproduzir: " + ex.Message, ToastLength.Long)?.Show();
                }
            }));
        }

        var code = new TextView(this) { Text = asset.UsageCode, TextSize = 12 };
        code.SetTypeface(Typeface.Monospace, TypefaceStyle.Normal);
        code.SetPadding(0, Dp(12), 0, Dp(12));
        root.AddView(code);
        root.AddView(MakeButton("Copiar uso em C#", () =>
        {
            var clipboard = GetSystemService(ClipboardService) as ClipboardManager;
            if (clipboard is not null) clipboard.PrimaryClip = ClipData.NewPlainText("Código Lunet", asset.UsageCode);
            Toast.MakeText(this, "Código copiado", ToastLength.Short)?.Show();
        }));

        if (asset.Kind == ProjectAssetKind.Audio && asset.Path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            root.AddView(MakeButton("Editar no Audio Studio", () =>
            {
                dialog.Dismiss();
                catalog.Dismiss();
                OpenWaveAudio(asset.Path);
            }));
        }

        if (asset.Kind is ProjectAssetKind.Sprite or ProjectAssetKind.TileMap or ProjectAssetKind.Data)
        {
            root.AddView(MakeButton(asset.Kind == ProjectAssetKind.Data ? "Abrir no editor" : "Editar no Studio", () =>
            {
                dialog.Dismiss();
                catalog.Dismiss();
                try
                {
                    if (asset.Kind == ProjectAssetKind.Sprite)
                        OpenSpriteStudio(asset.Path, SpriteDocument.Parse(project.ReadText(asset.Path)));
                    else if (asset.Kind == ProjectAssetKind.TileMap)
                        OpenTileMap(asset.Path, TileMap.Parse(project.ReadText(asset.Path)));
                    else OpenFile(asset.Path);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or ProjectException or ArgumentException)
                {
                    Toast.MakeText(this, "Não foi possível abrir: " + ex.Message, ToastLength.Long)?.Show();
                }
            }));
        }
        root.AddView(MakeButton("Fechar", () => dialog.Dismiss()));
        var scroll = new ScrollView(this);
        scroll.AddView(root);
        dialog.SetContentView(scroll);
        dialog.DismissEvent += (_, _) =>
        {
            if (player is not null) { player.Release(); player.Dispose(); player = null; }
            bitmap?.Dispose();
        };
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
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
