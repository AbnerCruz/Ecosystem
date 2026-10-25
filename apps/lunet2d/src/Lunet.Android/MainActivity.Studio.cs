using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Studio;

namespace Lunet.Android;

public sealed partial class MainActivity
{
    private SpriteStudioView? _spriteStudio;
    private void ShowSpriteAssets()
    {
        if (_project is null) return;
        SaveCurrent();
        var paths = _project.ListFiles().Where(p => p.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        var items = new[] { "+ Criar sprite / animação" }.Concat(paths).ToArray();
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
            else
            {
                try { OpenSpriteStudio(paths[e.Which - 1], SpriteDocument.Parse(_project.ReadText(paths[e.Which - 1]))); }
                catch (Exception ex) { Toast.MakeText(this, "Não foi possível abrir: " + ex.Message, ToastLength.Long)?.Show(); }
            }
        })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
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
        string png = path[..^".sprite.json".Length] + ".png";
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
