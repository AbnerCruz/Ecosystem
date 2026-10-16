using Avalonia.Threading;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The Tutorial. The 1.8.4 (tutorial.js) created a "Tutorial" folder in the city the first time it opened; the owner did
/// not want it in the world (DEC-0045, UC-40), so it is read in its own panel (<see cref="TutorialPanel"/>), and its text
/// is the native app's documentation (<see cref="AppDocs"/>, UC-64). Copying it into the city as notes is still there,
/// as the panel's own choice: what is missing is created, the notes that differ from the originals are replaced only
/// after asking, and the mark .urbe/tutorial.json is written.
/// </summary>
public sealed partial class MainView
{
    private static IReadOnlyList<CityNote>? _tutorialFiles;
    private static string? _tutorialVersion;

    private static IReadOnlyList<CityNote> TutorialFiles => _tutorialFiles ??= AppDocs.Load();

    /// <summary>C.version (build-tutorial.mjs).</summary>
    public static string TutorialVersion => _tutorialVersion ??= LegacyTutorial.Version(TutorialFiles.Select(f => (f.Path, f.Content)));

    /// <summary>The Tutorial panel (DEC-0045, UC-40), made the first time it opens.</summary>
    public TutorialPanel Tutorial => _tutorialPanel ??= MakeTutorialPanel();

    private TutorialPanel? _tutorialPanel;

    private TutorialPanel MakeTutorialPanel()
    {
        var panel = new TutorialPanel(TutorialFiles);
        panel.CopyRequested += () => _ = RestoreTutorialAsync(false);
        SetRowSpan(panel, 2);
        SetColumnSpan(panel, 2);
        // under the dialogs, over everything else
        Children.Insert(Math.Max(0, Children.IndexOf(Customize)), panel);
        return panel;
    }

    /// <summary>
    /// Configurações → Tutorial. The 1.8.4 created a Tutorial neighbourhood in the city the first time it opened and opened
    /// "Comece aqui" in the editor; the owner preferred a panel of its own, with navigation and search (DEC-0045, UC-40).
    /// </summary>
    public Task ShowTutorialMenuAsync()
    {
        Tutorial.Open();
        return Task.CompletedTask;
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
        ShowLifeNotice(created > 0 || replace ? "Tutorial restaurado" : "O Tutorial já está completo", created > 0 || replace ? Ui.ToastKind.Success : Ui.ToastKind.Info);
        if (openAfter) Tutorial.Open();
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
