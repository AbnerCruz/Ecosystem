using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// tutorial.js (1.8.4): the "Tutorial" folder is created by itself the first time a city (a real folder) opens, and
/// only once — the mark is .urbe/tutorial.json inside the city, so a person who deletes it does not get it back.
/// Configurações → Tutorial opens it ("Comece aqui") or restores it: what is missing is created again and the notes
/// that differ from the originals are replaced only after asking.
/// </summary>
public sealed partial class MainView
{
    /// <summary>Whether opening a city creates its Tutorial (tutorial.js semear); tests that do not look at it turn it off.</summary>
    public static bool SeedsTutorial { get; set; } = true;

    private static IReadOnlyList<CityNote>? _tutorialFiles;
    private static string? _tutorialVersion;

    private static IReadOnlyList<CityNote> TutorialFiles => _tutorialFiles ??= TutorialNotes.Load();

    /// <summary>C.version (build-tutorial.mjs).</summary>
    public static string TutorialVersion => _tutorialVersion ??= LegacyTutorial.Version(TutorialFiles.Select(f => (f.Path, f.Content)));

    /// <summary>tutorial.js menu: "Tutorial" with Abrir o Tutorial and Restaurar o Tutorial.</summary>
    public Task ShowTutorialMenuAsync()
    {
        if (Dialogs.IsOpen) return Task.CompletedTask;
        return Dialogs.MenuAsync("Tutorial",
        [
            new DialogItem("Abrir o Tutorial", UrbeTheme.Icons.Book, "Começa pela nota “Comece aqui”", Run: OpenTutorialAsync),
            new DialogItem("Restaurar o Tutorial", UrbeTheme.Icons.Restore, "Recria as notas que faltam ou foram mudadas", Run: () => RestoreTutorialAsync(true))
        ]);
    }

    /// <summary>abrir: "Comece aqui" in the editor, or the offer to create the folder again when it is gone.</summary>
    public async Task OpenTutorialAsync()
    {
        if (_contents.ContainsKey(LegacyTutorial.Start))
        {
            OpenNoteByPath(LegacyTutorial.Start);
            return;
        }
        if (await Dialogs.ConfirmAsync("Tutorial", "A pasta Tutorial não está nesta cidade. Quer criá-la de novo?", "Criar Tutorial"))
            await RestoreTutorialAsync(true);
    }

    /// <summary>
    /// restaurar: creates what is missing (neighbourhoods sized first, prepararBairros); with notes that differ from
    /// the originals, asks "Substituir" / "Manter as minhas"; writes the mark; "Tutorial restaurado" or "O Tutorial já
    /// está completo"; then opens it.
    /// </summary>
    public async Task RestoreTutorialAsync(bool openAfter)
    {
        if (!VaultWritable || World.City is null || _store is null)
        {
            await Dialogs.AlertAsync("Tutorial", NoVaultMessage);
            return;
        }
        var files = TutorialFiles;
        var (missing, changed) = LegacyTutorial.Compare(files.Select(f => (f.Path, f.Content)),
            p => _contents.TryGetValue(p, out var c) ? c : null);
        int created = await CreateTutorialFilesAsync(missing);
        bool replace = false;
        if (changed.Count > 0)
        {
            replace = await Dialogs.ConfirmAsync("Restaurar o Tutorial",
                (missing.Count > 0 ? missing.Count + " nota(s) recriada(s). " : "") + changed.Count +
                " nota(s) do Tutorial estão diferentes das originais (você mudou, ou são de uma versão anterior). Substituir pelas originais?",
                "Substituir", cancel: "Manter as minhas");
            if (replace)
                foreach (var path in changed)
                    if (!await ReplaceTutorialFileAsync(path, files.First(f => f.Path == path).Content)) { replace = false; break; }
        }
        await MarkTutorialAsync();
        ShowLifeNotice(created > 0 || replace ? "Tutorial restaurado" : "O Tutorial já está completo");
        if (openAfter) await OpenTutorialAsync();
    }

    /// <summary>
    /// semear (workspace:loaded): the first time this city opens — no Tutorial/ note, no mark in .urbe — the whole
    /// folder is created; on an empty city the camera frames the houses and "Comece aqui" opens, otherwise a notice.
    /// Never on a read-only vault (REQ-035).
    /// </summary>
    private async Task SeedTutorialAsync(VaultSnapshot snapshot)
    {
        if (!VaultWritable || World.City is null || _store is null) return;
        if (_store.List().Any(d => d.Path.StartsWith(LegacyTutorial.Folder, StringComparison.Ordinal))) return;
        if (snapshot.Files.Keys.Any(k => string.Equals(k, LegacyTutorial.MarkPath, StringComparison.OrdinalIgnoreCase))) return;
        bool empty = !_store.List().Any(d => !d.Path.StartsWith("Personalização/", StringComparison.Ordinal));
        if (await CreateTutorialFilesAsync(TutorialFiles.Select(f => f.Path).ToList()) == 0) return;
        await MarkTutorialAsync();
        if (empty)
        {
            await Task.Delay(900);
            World.FitNotes();
            await OpenTutorialAsync();
        }
        else ShowLifeNotice("Nova pasta Tutorial: tudo o que o Urbe faz, explicado passo a passo.");
    }

    private async Task SeedTutorialLaterAsync(VaultSnapshot snapshot)
    {
        await Task.Delay(300);
        if (!ReferenceEquals(_snapshot, snapshot)) return;
        try { await SeedTutorialAsync(snapshot); }
        catch (Exception ex) { ShowLifeNotice("Tutorial: " + ex.Message); }   // semear().catch(console.warn)
    }

    /// <summary>prepararBairros + criar: neighbourhoods of the right size, then one file and one house per note.</summary>
    private async Task<int> CreateTutorialFilesAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || World.City is not { } city || _store is null) return 0;
        city.EnsureFolders(LegacyCity.FolderQuantities(paths));
        int made = 0;
        bool links = false;
        foreach (var path in paths)
        {
            var content = TutorialFiles.First(f => f.Path == path).Content;
            try { await _vault!.CreateTextFileAsync(path, content); }
            catch (Exception ex)
            {
                await Dialogs.AlertAsync("Tutorial", "Não foi possível criar " + path + ": " + ex.Message);
                break;
            }
            var doc = _store.Upsert(new DocumentInput { Path = path, Content = content }, "tutorial");
            city.AddDocumentHouse(doc, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _contents[path] = content;
            links |= LegacyCity.LinkSignature(content).Length > 0;
            made++;
        }
        if (made == 0) return 0;
        // document:created → urbeCasaParaDocumento (scheduleRoadRebuild when it has links) → marcarSinc
        if (links) ScheduleRoadRebuild();
        _mapSave?.Schedule();
        _mapSave?.MarkSync();
        RefreshNotes();
        World.InvalidateVisual();
        return made;
    }

    private async Task<bool> ReplaceTutorialFileAsync(string path, string content)
    {
        try { await _vault!.SaveExistingTextFileAsync(path, content); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Tutorial", "Não foi possível substituir " + path + ": " + ex.Message);
            return false;
        }
        var before = _contents.GetValueOrDefault(path, "");
        _contents[path] = content;
        if (_store!.Get(path) is { } doc)
            _store.Upsert(new DocumentInput { Id = doc.Id, Path = doc.Path, Title = doc.Title, Content = content, Created = doc.Created, Modified = doc.Modified }, "tutorial");
        // document:updated (not editor.input): a new [[link]] signature redraws the streets
        if (LegacyCity.LinkSignature(before) != LegacyCity.LinkSignature(content)) ScheduleRoadRebuild();
        if (Editor.IsVisible && Editor.Current?.Path == path) Editor.Open(new CityNote(path, content));
        return true;
    }

    /// <summary>marcar: {versao, em} in .urbe/tutorial.json (errors are ignored, as the 1.8.4's try/catch).</summary>
    private async Task MarkTutorialAsync()
    {
        try { await _vault!.SaveTutorialMarkAsync(LegacyTutorial.Mark(TutorialVersion, DateTimeOffset.UtcNow)); }
        catch (Exception) { }
    }
}
