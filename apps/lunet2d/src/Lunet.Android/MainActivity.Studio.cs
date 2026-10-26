using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Studio;
using Path = System.IO.Path;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private SpriteStudioView? _spriteStudio;
    private void ShowSpriteAssets()
    {
        if (_project is null) return;
        SaveCurrent();
        var paths = _project.ListFiles().Where(p => p.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        var items = new[] { "+ Criar sprite / animação", "Importar PNG do projeto" }.Concat(paths).ToArray();
        new AlertDialog.Builder(this)!.SetTitle("Sprites e animação")!.SetItems(items, (_, e) =>
        {
            if (e.Which == 0)
            {
                new AlertDialog.Builder(this)!.SetTitle("Tamanho do sprite")!.SetItems(new[] { "16 × 16", "32 × 32", "64 × 64" }, (_, size) =>
                {
                    string path = "Assets/sprite-" + Guid.NewGuid().ToString("N")[..8] + ".sprite.json";
                    OpenSpriteStudio(path, SpriteDocument.Create(new[] { 16, 32, 64 }[size.Which]));
                })!.Show();
            }
            else if (e.Which == 1) ImportSpritePng();
            else
            {
                try { OpenSpriteStudio(paths[e.Which - 2], SpriteDocument.Parse(_project.ReadText(paths[e.Which - 2]))); }
                catch (Exception ex) { Toast.MakeText(this, "Não foi possível abrir: " + ex.Message, ToastLength.Long)?.Show(); }
            }
        })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
    }
    private void ImportSpritePng()
    {
        var project = _project!;
        var files = project.ListFiles().Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (files.Length == 0) { Toast.MakeText(this, "Importe um PNG pelo Explorer primeiro.", ToastLength.Long)?.Show(); return; }
        new AlertDialog.Builder(this)!.SetTitle("PNG do projeto (até 128 × 128)")!.SetItems(files, (_, e) =>
        {
            try
            {
                string absolute = Path.Combine(project.Directory, files[e.Which]);
                using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
                BitmapFactory.DecodeFile(absolute, bounds)?.Dispose();
                if (bounds.OutWidth is < 1 or > 128 || bounds.OutHeight is < 1 or > 128) throw new InvalidDataException("Use um PNG de até 128 × 128 pixels nesta versão.");
                using var bitmap = BitmapFactory.DecodeFile(absolute) ?? throw new InvalidDataException("PNG inválido.");
                var pixels = new int[bitmap.Width * bitmap.Height]; bitmap.GetPixels(pixels, 0, bitmap.Width, 0, 0, bitmap.Width, bitmap.Height);
                var doc = new SpriteDocument { Width = bitmap.Width, Height = bitmap.Height, Frames = [pixels.Select(p => unchecked((uint)p)).ToArray()] };
                OpenSpriteStudio("Assets/sprite-" + Guid.NewGuid().ToString("N")[..8] + ".sprite.json", doc);
            }
            catch (Exception ex) { Toast.MakeText(this, "Não foi possível importar: " + ex.Message, ToastLength.Long)?.Show(); }
        })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }
    private void OpenSpriteStudio(string path, SpriteDocument document)
    {
        var project = _project!;
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        bool saved = false;
        var view = new SpriteStudioView(this, document,
            doc =>
            {
                saved = false;
                // JSON is the editable source; the PNG sheet is a derived asset
                // that follows every save, including frame and timing edits.
                project.WriteText(path, doc.ToJson());
                SaveSpriteSheet(path, doc);
                saved = true;
            },
            doc => ShowSpriteUsage(path, doc),
            () => { _spriteStudio?.Save(); if (!saved) return; dialog.Dismiss(); _spriteStudio = null; RefreshExplorer(); });
        _spriteStudio = view;
        dialog.SetContentView(view); dialog.SetCancelable(false); dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        view.Save();
    }

    private static string SpriteSheetPath(string path) =>
        "Assets/" + Path.GetFileName(path[..^".sprite.json".Length]) + ".png";

    private void SaveSpriteSheet(string path, SpriteDocument doc)
    {
        string png = SpriteSheetPath(path);
        int width = checked(doc.Width * doc.Frames.Count);
        byte[] rgba = doc.SheetRgba();
        var pixels = new int[checked(width * doc.Height)];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = unchecked((int)((uint)rgba[i * 4 + 3] << 24 |
                                        (uint)rgba[i * 4] << 16 |
                                        (uint)rgba[i * 4 + 1] << 8 | rgba[i * 4 + 2]));
        using var bitmap = Bitmap.CreateBitmap(pixels, width, doc.Height, Bitmap.Config.Argb8888!)!;
        using var stream = new MemoryStream();
        if (!bitmap.Compress(Bitmap.CompressFormat.Png!, 100, stream))
            throw new IOException("Falha ao gerar spritesheet PNG.");
        stream.Position = 0;
        _project!.Import("Assets", Path.GetFileName(png), stream);
    }

    private void ShowSpriteUsage(string path, SpriteDocument doc)
    {
        string png = SpriteSheetPath(path);
        var frames = string.Join(", ", Enumerable.Range(0, doc.Frames.Count));
        new AlertDialog.Builder(this)!.SetTitle("Animação pronta para o jogo")!.SetMessage(
            $"{png}\n{doc.Frames.Count} frames · {doc.Width}×{doc.Height} · {doc.Fps} FPS\n" +
            $"Fonte editável: {path}\n\n" +
            "No LoadContent:\n" +
            $"var sheet = new SpriteSheet(Content.LoadTexture(\"{png}\"), {doc.Width}, {doc.Height});\n" +
            $"var clip = SpriteAnimationClip.FromSheet(sheet, new[] {{ {frames} }}, 1.0 / {doc.Fps});\n" +
            "var animator = new SpriteAnimator(clip);\n\n" +
            "No Update:\nanimator.Update(time.DeltaSeconds);\n\n" +
            "No Draw (dentro de SpriteBatch.Begin/End):\n" +
            "batch.Draw(animator.Clip.Texture, position, animator.Source, Color.White);")!
            .SetPositiveButton("OK", (_, _) => { })!.Show();
    }

    private TileStudioView? _tileStudio;

    private void ShowArtTools()
    {
        new AlertDialog.Builder(this)!.SetTitle("Ferramentas visuais")!
            .SetItems(new[] { "Sprites e animação", "Tilemaps" }, (_, e) =>
            {
                if (e.Which == 0) ShowSpriteAssets();
                else ShowTileMaps();
            })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
    }

    private void ShowTileMaps()
    {
        if (_project is null) return;
        SaveCurrent();
        var paths = _project.ListFiles().Where(p => p.EndsWith(".tilemap.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        var actions = new[] { "+ Criar tilemap" }.Concat(paths).ToArray();
        new AlertDialog.Builder(this)!.SetTitle("Tilemaps")!.SetItems(actions, (_, e) =>
        {
            if (e.Which == 0) CreateTileMap();
            else
            {
                try
                {
                    string path = paths[e.Which - 1];
                    OpenTileMap(path, Lunet.Graphics.TileMap.Parse(_project.ReadText(path)));
                }
                catch (Exception ex) { Toast.MakeText(this, "Não foi possível abrir o mapa: " + ex.Message, ToastLength.Long)?.Show(); }
            }
        })!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void CreateTileMap()
    {
        var project = _project!;
        var textures = project.ListFiles().Where(p =>
            p.StartsWith("Content/Textures/", StringComparison.Ordinal) &&
            p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (textures.Length == 0)
        {
            new AlertDialog.Builder(this)!.SetTitle("Primeiro importe um tileset PNG")!
                .SetMessage("Use Explorer / Importar imagem PNG. O tileset deve estar em Content/Textures e ter dimensões múltiplas de 16, 32 ou 64.")!
                .SetPositiveButton("OK", (_, _) => { })!.Show();
            return;
        }
        new AlertDialog.Builder(this)!.SetTitle("Tileset do mapa")!.SetItems(textures, (_, e) =>
        {
            var path = textures[e.Which];
            new AlertDialog.Builder(this)!.SetTitle("Tamanho dos tiles")!
                .SetItems(new[] { "16 × 16", "32 × 32", "64 × 64" }, (_, size) =>
                {
                    int tile = new[] { 16, 32, 64 }[size.Which];
                    try
                    {
                        string texturePath = path["Content/".Length..];
                        string mapPath = "Content/Data/map-" + Guid.NewGuid().ToString("N")[..8] + ".tilemap.json";
                        OpenTileMap(mapPath, Lunet.Graphics.TileMap.Create(texturePath, 16, 16, tile, tile));
                    }
                    catch (Exception ex) { Toast.MakeText(this, "Mapa inválido: " + ex.Message, ToastLength.Long)?.Show(); }
                })!.Show();
        })!.Show();
    }

    private void OpenTileMap(string path, Lunet.Graphics.TileMap map)
    {
        var project = _project!;
        Bitmap? bitmap = null;
        try
        {
            string relative = Path.Combine("Content", map.TexturePath.Replace('/', Path.DirectorySeparatorChar));
            string absolute = Path.GetFullPath(Path.Combine(project.Directory, relative));
            string contentRoot = Path.GetFullPath(Path.Combine(project.Directory, "Content")) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(contentRoot, StringComparison.Ordinal)) throw new InvalidDataException("Tileset fora de Content.");
            using var options = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(absolute, options)?.Dispose();
            if (options.OutWidth is < 1 or > 2048 || options.OutHeight is < 1 or > 2048 ||
                options.OutWidth % map.TileWidth != 0 || options.OutHeight % map.TileHeight != 0)
                throw new InvalidDataException("PNG ausente ou incompatível com o mapa (máximo 2048 × 2048).");
            if (map.Width > 64 || map.Height > 64) throw new InvalidDataException("Esta versão do editor abre mapas de até 64 × 64 células. O jogo continua suportando mapas maiores.");
            bitmap = BitmapFactory.DecodeFile(absolute) ?? throw new InvalidDataException("Não foi possível ler o PNG.");
            int maxTiles = (bitmap.Width / map.TileWidth) * (bitmap.Height / map.TileHeight);
            if (maxTiles > 1024) throw new InvalidDataException("Paleta grande demais para esta versão (máximo 1024 tiles).");
            var edit = new TileStudioSession(map, maxTiles);
            var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
            bool saved = false;
            var view = new TileStudioView(this, edit, bitmap,
                json => { saved = false; project.WriteText(path, json); saved = true; },
                () =>
                {
                    _tileStudio?.Save();
                    if (!saved) return;
                    _tileStudio = null;
                    dialog.Dismiss();
                    bitmap.Dispose();
                    RefreshExplorer();
                });
            _tileStudio = view;
            dialog.SetContentView(view);
            dialog.SetCancelable(false);
            dialog.Show();
            dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            view.Save();
        }
        catch (Exception ex)
        {
            bitmap?.Dispose();
            Toast.MakeText(this, "Não foi possível abrir Tile Studio: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

}
