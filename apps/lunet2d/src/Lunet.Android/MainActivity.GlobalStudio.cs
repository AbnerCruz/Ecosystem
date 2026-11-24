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
                var path = StandaloneWorkspace.NewPath("Content/Textures", "sprite", ".sprite.json");
                EditStandaloneSprite(path, SpriteDocument.Create(new[] { 16, 32, 64 }[e.Which]));
            })!.SetNegativeButton("Voltar", (_, _) => { })!.Show();
    }

    private void EditStandaloneSprite(string path, SpriteDocument doc, Action? afterClose = null, int tileSize = 0)
    {
        var dialog = new Dialog(this, global::Android.Resource.Style.ThemeMaterialNoActionBar);
        var view = new SpriteStudioView(this, doc,
            current => SaveStudioSprite(StandaloneWorkspace, path, current),
            _ => Toast.MakeText(this, "Fonte editável e PNG salvos no Studio.", ToastLength.Short)?.Show(),
            () =>
            {
                _spriteStudio?.Save();
                dialog.Dismiss();
                _spriteStudio = null;
                if (afterClose is not null) afterClose(); else AskPublishStandaloneAsset(path);
            }, tileSize);
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
                var expectedSource = current.ToJson();
                var snapshot = MusicSong.Parse(expectedSource);
                Task.Run(() => new MusicRenderer().RenderWav(snapshot)).ContinueWith(task =>
                    RunOnUiThread(() =>
                    {
                        if (task.IsFaulted)
                        {
                            Toast.MakeText(this, "Falha ao renderizar: " +
                                task.Exception!.GetBaseException().Message, ToastLength.Long)?.Show();
                            return;
                        }
                        if (storage.ReadText(path) != expectedSource) return; // A stale render must not replace a newer draft.
                        storage.WriteBytes(path[..^MusicSongExtension.Length] + ".wav", task.Result);
                        Toast.MakeText(this, "Música WAV salva no Studio.", ToastLength.Short)?.Show();
                    }), TaskScheduler.Default);
            },
            () =>
            {
                dialog.Dismiss();
                _musicStudio = null;
                // Export the exact saved source before offering publication; the old branch
                // could publish a JSON revision with a missing or stale WAV derivative.
                var savedSource = storage.ReadText(path);
                Task.Run(() => new MusicRenderer().RenderWav(MusicSong.Parse(savedSource)))
                    .ContinueWith(task => RunOnUiThread(() =>
                    {
                        if (task.IsFaulted)
                        {
                            Toast.MakeText(this, "Falha ao renderizar: " +
                                task.Exception!.GetBaseException().Message, ToastLength.Long)?.Show();
                            return;
                        }
                        if (storage.ReadText(path) != savedSource)
                        {
                            Toast.MakeText(this, "A composição mudou. Salve novamente antes de publicar.",
                                ToastLength.Long)?.Show();
                            return;
                        }
                        storage.WriteBytes(path[..^MusicSongExtension.Length] + ".wav", task.Result);
                        AskPublishStandaloneAsset(path);
                    }), TaskScheduler.Default);
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

    private void PublishStandaloneAsset(string path) => ShowAssetPublishForm(null, path, null);

    private LibraryAssetRender? PrepareLibraryRender(string path, StudioAssetWorkspace storage)
    {
        if (path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(MusicSongExtension, StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(SoundEffectExtension, StringComparison.OrdinalIgnoreCase))
        {
            var snapshot = storage.ReadBytes(path);
            using var reader = new StreamReader(new MemoryStream(snapshot, writable: false));
            var source = reader.ReadToEnd();
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(snapshot));
            if (path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase))
                return new(hash, SpriteSheetPath(path), SpritePng(SpriteDocument.Parse(source)));
            if (path.EndsWith(MusicSongExtension, StringComparison.OrdinalIgnoreCase))
                return new(hash, path[..^MusicSongExtension.Length] + ".wav", new MusicRenderer().RenderWav(MusicSong.Parse(source)));
            return new(hash, path[..^SoundEffectExtension.Length] + ".wav", SoundDesigner.RenderWav(SoundDesigner.Parse(source)));
        }

        if (IsDataTable(path) && storage.Exists(SchemaPath(path)))
        {
            var table = Lunet.Studio.Data.DataTable.Parse(storage.ReadText(path), storage.ReadText(SchemaPath(path)));
            var issues = table.Validate(DataTableKeys(path, storage));
            if (issues.Count > 0) throw new InvalidDataException("Corrija a tabela no Data Studio: " + issues[0].Message);
        }
        return null;
    }

    private static bool IsStandaloneDraft(string path) =>
        new[] { ".sprite.json", MusicSongExtension, SoundEffectExtension, ".tilemap.json", ".palette.json", ParticleExtension }
            .Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) ||
        IsDataTable(path) || path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);

    private void NewStandaloneTileMap()
    {
        new AlertDialog.Builder(this)!.SetTitle("Tamanho do mapa")!
            .SetItems(new[] { "16 × 16", "32 × 32", "64 × 64" }, (_, e) =>
                CreateEditableTileMap(new[] { 16, 32, 64 }[e.Which], 16, standalone: true))!
            .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void NewStandalonePalette()
    {
        var palette = new ProjectPalette();
        var path = StandaloneWorkspace.NewPath("Assets", "paleta", ".palette.json");
        StandaloneWorkspace.WriteTextPair(path, palette.ToJson(), path[..^".palette.json".Length] + ".gpl", palette.ToGpl());
        OpenPaletteStudio(palette, path, StandaloneWorkspace);
    }

    private void NewStandaloneParticles()
    {
        var presets = ParticlePresets.All;
        new AlertDialog.Builder(this)!.SetTitle("Começar de")!
            .SetItems(presets.Select(p => p.Name).ToArray(), (_, e) =>
                OpenParticleStudio(StandaloneWorkspace.NewPath("Content/Effects", presets[e.Which].Id, ParticleExtension),
                    ParticleDraft.From(presets[e.Which]), StandaloneWorkspace))!
            .SetNegativeButton("Cancelar", (_, _) => { })!.Show();
    }

    private void NewStandaloneData()
    {
        var path = StandaloneWorkspace.NewPath("Content/Data", "tabela", ".json");
        var table = Lunet.Studio.Data.DataTable.Create("Registro",
            new Lunet.Studio.Data.DataColumn { Name = "id", Key = true, Required = true },
            new Lunet.Studio.Data.DataColumn { Name = "nome" });
        StandaloneWorkspace.WriteTextPair(path, table.ToJson(), SchemaPath(path), table.SchemaToJson());
        OpenDataTable(path, StandaloneWorkspace);
    }

    private void OpenStandaloneDraft(string path)
    {
        var storage = StandaloneWorkspace;
        if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            OpenWaveAudio(WaveAudioDocument.Parse(storage.ReadBytes(path)), Path.GetFileNameWithoutExtension(path), storage, path);
            return;
        }
        var source = storage.ReadText(path);
        if (path.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase)) EditStandaloneSprite(path, SpriteDocument.Parse(source));
        else if (path.EndsWith(MusicSongExtension, StringComparison.OrdinalIgnoreCase)) EditStandaloneMusic(path, MusicSong.Parse(source));
        else if (path.EndsWith(SoundEffectExtension, StringComparison.OrdinalIgnoreCase)) OpenSoundDesigner(path, SoundDesigner.Parse(source));
        else if (path.EndsWith(".tilemap.json", StringComparison.OrdinalIgnoreCase)) OpenTileMap(path, Lunet.Graphics.TileMap.Parse(source), storage);
        else if (path.EndsWith(".palette.json", StringComparison.OrdinalIgnoreCase)) OpenPaletteStudio(ProjectPalette.Parse(source), path, storage);
        else if (path.EndsWith(ParticleExtension, StringComparison.OrdinalIgnoreCase)) OpenSavedParticle(path, storage, standalone: true);
        else if (IsDataTable(path)) OpenDataTable(path, storage);
    }

    private void ShowStandaloneDrafts()
    {
        var drafts = StandaloneWorkspace.ListFiles()
            .Where(IsStandaloneDraft)
            .Where(path => !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                !StandaloneWorkspace.Exists(path[..^4] + MusicSongExtension) && !StandaloneWorkspace.Exists(path[..^4] + SoundEffectExtension))
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
                            OpenStandaloneDraft(path);
                        }
                        catch (Exception ex)
                        {
                            Toast.MakeText(this, "Falha ao abrir criação: " + ex.Message, ToastLength.Long)?.Show();
                        }
                    })!.Show();
            })!.SetNegativeButton("Fechar", (_, _) => { })!.Show();
    }
}
