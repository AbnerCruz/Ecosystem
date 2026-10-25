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
            doc => { saved = false; project.WriteText(path, doc.ToJson()); saved = true; },
            doc => ExportSprite(path, doc),
            () => { _spriteStudio?.Save(); if (!saved) return; dialog.Dismiss(); _spriteStudio = null; RefreshExplorer(); });
        _spriteStudio = view;
        dialog.SetContentView(view); dialog.SetCancelable(false); dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        view.Save();
    }
    private void ExportSprite(string path, SpriteDocument doc)
    {
        var project = _project!;
        string png = "Assets/" + Path.GetFileName(path[..^".sprite.json".Length]) + ".png";
        void Write()
        {
            try
            {
                int width = doc.Width * doc.Frames.Count; var rgba = doc.SheetRgba(); var pixels = new int[width * doc.Height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = unchecked((int)((uint)rgba[i * 4 + 3] << 24 | (uint)rgba[i * 4] << 16 | (uint)rgba[i * 4 + 1] << 8 | rgba[i * 4 + 2]));
                using var bitmap = Bitmap.CreateBitmap(pixels, width, doc.Height, Bitmap.Config.Argb8888!)!;
                using var stream = new MemoryStream();
                if (!bitmap.Compress(Bitmap.CompressFormat.Png!, 100, stream)) throw new IOException("Falha ao gerar PNG.");
                stream.Position = 0; project.Import("Assets", Path.GetFileName(png), stream);
                new AlertDialog.Builder(this)!.SetTitle("Spritesheet exportado")!.SetMessage(
                    $"{png}\n{doc.Frames.Count} frames · {doc.Width}×{doc.Height} · {doc.Fps} FPS\n\nNo jogo:\nvar sheet = new SpriteSheet(Content.LoadTexture(\"{png}\"), {doc.Width}, {doc.Height});\nvar clip = SpriteAnimationClip.FromSheet(sheet, new[] {{ {string.Join(", ", Enumerable.Range(0, doc.Frames.Count))} }}, 1.0 / {doc.Fps});")!
                    .SetPositiveButton("OK", (_, _) => { })!.Show();
            }
            catch (Exception ex) { Toast.MakeText(this, "Não foi possível exportar: " + ex.Message, ToastLength.Long)?.Show(); }
        }
        if (project.ListFiles().Contains(png))
            new AlertDialog.Builder(this)!.SetTitle("Atualizar spritesheet?")!.SetMessage(png)!.SetPositiveButton("Substituir PNG", (_, _) => Write())!.SetNegativeButton("Cancelar", (_, _) => { })!.Show();
        else Write();
    }
}
