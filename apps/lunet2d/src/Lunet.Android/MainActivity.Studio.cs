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
    private void OpenSpriteStudio(string path, SpriteDocument document, Action? afterClose = null, int tilesetTileSize = 0)
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
            () => { _spriteStudio?.Save(); if (!saved) return; dialog.Dismiss(); _spriteStudio = null; RefreshExplorer(); afterClose?.Invoke(); }, tilesetTileSize);
        _spriteStudio = view;
        dialog.SetContentView(view); dialog.SetCancelable(false); dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        view.Save();
    }

    private static string SpriteSheetPath(string path) =>
        path[..^".sprite.json".Length] + ".png";

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
        _project!.Import(Path.GetDirectoryName(png)!.Replace('\\', '/'), Path.GetFileName(png), stream);
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
            .SetItems(new[] { "Sprites e animação", "Tilemaps", "Áudio" }, (_, e) =>
            {
                if (e.Which == 0) ShowSpriteAssets();
                else if (e.Which == 1) ShowTileMaps();
                else ShowAudioStudio();
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
        // An imported PNG is no longer a prerequisite: every new map gets
        // an editable in-project tileset and the runtime's TileMap JSON v1.
        new AlertDialog.Builder(this)!.SetTitle("Tamanho do mapa")!
            .SetItems(new[] { "16 × 16 células", "32 × 32 células", "64 × 64 células" }, (_, mapSize) =>
            {
                int cells = new[] { 16, 32, 64 }[mapSize.Which];
                new AlertDialog.Builder(this)!.SetTitle("Tamanho dos tiles")!
                    .SetItems(new[] { "16 × 16 pixels", "32 × 32 pixels", "64 × 64 pixels" }, (_, tileSize) =>
                    {
                        int tile = new[] { 16, 32, 64 }[tileSize.Which];
                        CreateEditableTileMap(cells, tile);
                    })!.Show();
            })!.Show();
    }

    private void CreateEditableTileMap(int cells, int tile)
    {
        var project = _project!;
        var name = "tiles-" + Guid.NewGuid().ToString("N")[..8];
        string source = "Content/Textures/" + name + ".sprite.json";
        string png = SpriteSheetPath(source);
        string mapPath = "Content/Data/map-" + name + ".tilemap.json";
        try
        {
            // The atlas fits the existing native pixel editor (<=128px);
            // four flat-color placeholders are immediately usable and editable.
            int atlasSize = Math.Min(128, tile * 4);
            var atlas = SpriteDocument.Create(atlasSize);
            int columns = atlasSize / tile;
            uint[] colors = [0xff438956, 0xffb68b53, 0xff7b8796, 0xff3c8fbd];
            for (int i = 0; i < colors.Length; i++)
            {
                int x = (i % columns) * tile, y = (i / columns) * tile;
                atlas.Rectangle(0, x, y, x + tile - 1, y + tile - 1, colors[i], filled: true);
            }

            project.WriteText(source, atlas.ToJson());
            SaveSpriteSheet(source, atlas);
            var map = Lunet.Graphics.TileMap.Create("Textures/" + name + ".png", cells, cells, tile, tile);
            project.WriteText(mapPath, map.ToJson());
            RefreshExplorer();
            OpenTileMap(mapPath, map);
        }
        catch (Exception ex)
        {
            // These paths contain a new random ID and belong exclusively to this operation.
            // Never delete or rewrite older maps while handling a creation failure.
            foreach (string path in new[] { mapPath, png, source })
            {
                try { if (project.ListFiles().Contains(path)) project.DeleteFile(path); }
                catch (IOException) { /* preserve original failure */ }
            }
            Toast.MakeText(this, "Não foi possível criar o mapa: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    private void EditTileMapTileset(string mapPath, Lunet.Graphics.TileMap map, Action closeMap)
    {
        var project = _project!;
        string png = "Content/" + map.TexturePath;
        string source = png[..^".png".Length] + ".sprite.json";
        if (!project.ListFiles().Contains(source))
        {
            new AlertDialog.Builder(this)!.SetTitle("Tileset sem fonte editável")!
                .SetMessage("Este mapa utiliza um PNG importado. Crie um novo tilemap para desenhar o próprio tileset no Lunet.")!
                .SetPositiveButton("OK", (_, _) => { })!.Show();
            return;
        }
        try
        {
            var document = SpriteDocument.Parse(project.ReadText(source));
            if (document.Frames.Count != 1 || document.Width % map.TileWidth != 0 || document.Height % map.TileHeight != 0)
                throw new InvalidDataException("O tileset precisa ter um frame e dimensões múltiplas do tile.");
            closeMap();
            OpenSpriteStudio(source, document,
                () => OpenTileMap(mapPath, Lunet.Graphics.TileMap.Parse(project.ReadText(mapPath))),
                map.TileWidth);
        }
        catch (Exception ex)
        {
            Toast.MakeText(this, "Não foi possível editar o tileset: " + ex.Message, ToastLength.Long)?.Show();
        }
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
                () => EditTileMapTileset(path, map, () =>
                {
                    _tileStudio?.Save();
                    _tileStudio = null;
                    dialog.Dismiss();
                    bitmap.Dispose();
                }),
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
