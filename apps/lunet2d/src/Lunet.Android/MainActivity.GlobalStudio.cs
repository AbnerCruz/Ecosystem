using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using Lunet.Core;
using Lunet.Studio;
using Lunet.Studio.Music;
using Path = System.IO.Path;

namespace Lunet.Android;

/// <summary>
/// Standalone Studio entrypoints. Every editor works with a normal persistent folder,
/// not an invisible LunetProject. Assets can be published to the shared versioned
/// library, then imported to any project by explicit user action.
/// </summary>
public sealed partial class MainActivity
{
    private StudioAssetWorkspace? _standaloneWorkspace;
    private StudioAssetWorkspace StandaloneWorkspace =>
        _standaloneWorkspace ??= new StudioAssetWorkspace(Path.Combine(FilesDir!.AbsolutePath, "StudioWorkspace"));

    private void NewStandaloneSprite()
    {
        new AlertDialog.Builder(this)!.SetTitle("Novo sprite · Studio independente")!
            .SetItems(new[] { "16 × 16", "32 × 32", "64 × 64" }, (_, e) =>
            {
                var path = StandaloneWorkspace.NewPath("Assets", "sprite", ".sprite.json");
                EditStandaloneSprite(path, SpriteDocument.Create(new[] { 16, 32, 64 }[e.Which]));
            })!.SetNegativeButton("Voltar", (_, _) => { })!.Show();
    }

    private void EditStandaloneSprite(string path, SpriteDocument doc)
    {
        var storage = StandaloneWorkspace;
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        var view = new SpriteStudioView(this, doc, current =>
        {
            storage.WriteText(path, current.ToJson());
            int width = checked(current.Width * current.Frames.Count);
            byte[] rgba = current.SheetRgba();
            var pixels = new int[checked(width * current.Height)];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = unchecked((int)((uint)rgba[i * 4 + 3] << 24 |
                    (uint)rgba[i * 4] << 16 | (uint)rgba[i * 4 + 1] << 8 | rgba[i * 4 + 2]));
            using var bmp = Bitmap.CreateBitmap(pixels, width, current.Height, Bitmap.Config.Argb8888!)!;
            using var memory = new MemoryStream();
            if (!bmp.Compress(Bitmap.CompressFormat.Png!, 100, memory))
                throw new IOException("Falha ao gerar PNG do sprite.");
            storage.WriteBytes(path[..^".sprite.json".Length] + ".png", memory.ToArray());
        }, _ => Toast.MakeText(this, "Fonte editável e PNG salvos no Studio.", ToastLength.Short)?.Show(),
        () =>
        {
            _spriteStudio?.Save();
            dialog.Dismiss();
            _spriteStudio = null;
            AskPublishStandaloneAsset(path);
        });
        _spriteStudio = view;
        dialog.SetContentView(view);
        dialog.SetCancelable(false);
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        view.Save();
    }

    private void NewStandaloneMusic()
    {
        var song = MusicSong.Empty("Música");
        var path = StandaloneWorkspace.NewPath("Content/Audio", "musica", MusicSongExtension);
        StandaloneWorkspace.WriteText(path, song.ToJson());
        EditStandaloneMusic(path, song);
    }

    private void EditStandaloneMusic(string path, MusicSong song)
    {
        var storage = StandaloneWorkspace;
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        var view = new MusicStudioView(this, song, Path.Combine(CacheDir!.AbsolutePath, "music-studio"),
            current => storage.WriteText(path, current.ToJson()),
            current =>
            {
                var snapshot = MusicSong.Parse(current.ToJson());
                Task.Run(() => new MusicRenderer().RenderWav(snapshot)).ContinueWith(task =>
                    RunOnUiThread(() =>
                    {
                        if (task.IsFaulted)
                        {
                            Toast.MakeText(this, "Falha ao renderizar: " +
                                task.Exception!.GetBaseException().Message, ToastLength.Long)?.Show();
                            return;
                        }
                        storage.WriteBytes(path[..^MusicSongExtension.Length] + ".wav", task.Result);
                        Toast.MakeText(this, "Música WAV salva no Studio.", ToastLength.Short)?.Show();
                    }), TaskScheduler.Default);
            },
            () =>
            {
                dialog.Dismiss();
                _musicStudio = null;
                AskPublishStandaloneAsset(path);
            });
        _musicStudio = view;
        dialog.SetContentView(view);
        dialog.SetCancelable(false);
        dialog.DismissEvent += (_, _) => { if (ReferenceEquals(_musicStudio, view)) _musicStudio = null; };
        dialog.Show();
        dialog.Window?.SetLayout(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
    }

    private void AskPublishStandaloneAsset(string path)
    {
        new AlertDialog.Builder(this)!.SetTitle("Produção salva no Studio")!
            .SetMessage("O original editável permanece no armazenamento local do Studio. Publicar na Biblioteca cria uma versão independente para usar em qualquer jogo.")!
            .SetNegativeButton("Manter no Studio", (_, _) => { })!
            .SetPositiveButton("Publicar na Biblioteca", (_, _) => PublishStandaloneAsset(path))!
            .Show();
    }

    private void PublishStandaloneAsset(string path)
    {
        try
        {
            string title = Path.GetFileName(path).Split('.')[0];
            var asset = AssetLibrary.PublishWorkspace(StandaloneWorkspace.RootDirectory, path,
                title, "", "Uso próprio");
            new AlertDialog.Builder(this)!.SetTitle("Asset na Biblioteca")!
                .SetMessage("Versão " + asset.Version + " salva. Deseja enviar uma cópia para algum projeto?")!
                .SetNegativeButton("Mais tarde", (_, _) => { })!
                .SetPositiveButton("Usar em…", (_, _) => ChooseAssetDestination(asset))!.Show();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or ArgumentException or InvalidOperationException)
        {
            new AlertDialog.Builder(this)!.SetTitle("Não foi possível publicar")!
                .SetMessage(ex.Message + "\nO original continua salvo no Studio.")!
                .SetPositiveButton("OK", (_, _) => { })!.Show();
        }
    }

    private void ShowStandaloneDrafts()
    {
        var drafts = StandaloneWorkspace.ListFiles()
            .Where(path => path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(MusicSongExtension, StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(SoundEffectExtension, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (drafts.Length == 0)
        {
            Toast.MakeText(this, "Nenhuma criação salva ainda.", ToastLength.Short)?.Show();
            return;
        }
        new AlertDialog.Builder(this)!.SetTitle("Criações salvas no Studio")!
            .SetItems(drafts, (_, e) =>
            {
                var path = drafts[e.Which];
                new AlertDialog.Builder(this)!.SetTitle(Path.GetFileName(path))!
                    .SetItems(new[] { "Continuar editando", "Publicar na Biblioteca" }, (_, action) =>
                    {
                        if (action.Which == 1) { PublishStandaloneAsset(path); return; }
                        try
                        {
                            string source = StandaloneWorkspace.ReadText(path);
                            if (path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase))
                                EditStandaloneSprite(path, SpriteDocument.Parse(source));
                            else if (path.EndsWith(MusicSongExtension, StringComparison.OrdinalIgnoreCase))
                                EditStandaloneMusic(path, MusicSong.Parse(source));
                            else
                                OpenSoundDesigner(path, SoundDesigner.Parse(source));
                        }
                        catch (Exception ex)
                        {
                            Toast.MakeText(this, "Falha ao abrir criação: " + ex.Message, ToastLength.Long)?.Show();
                        }
                    })!.Show();
            })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
    }
}
