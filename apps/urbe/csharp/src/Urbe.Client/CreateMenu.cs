using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Urbe.Client.World;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// The rest of the "Criar" (+) menu (app.js v23MenuCriar): Nova pasta, Desenhar pasta no mapa,
/// Adicionar construção and Importar. The rules live in Urbe.Core (LegacyCityCreate, ImportPipeline);
/// this file only asks, writes to the vault and draws the tool dock. Nothing is kept in memory only:
/// without a writable vault folder these actions refuse, like "Nova nota".
/// </summary>
public sealed partial class MainView
{
    private const string NoVaultMessage = "Abra a pasta do seu vault (botão Cidades) para criar pastas, construções e importar. O Tutorial desta prévia é só leitura.";

    private readonly Border _toolDock = new();
    private readonly TextBlock _toolText = new();
    private readonly Button _toolOk = new();

    /// <summary>Replaces the system picker (tests, other platforms): true = a whole folder, false = files.</summary>
    public Func<bool, Task<ImportSelection?>>? ImportPicker { get; set; }

    /// <summary>True while a map tool (draw a region / place a building) owns the one-finger drag.</summary>
    public bool ToolActive => World.Tool is not null;
    public string ToolDockText => _toolText.Text ?? "";

    private bool VaultWritable => _vault is { IsConnected: true } && _snapshot is { IsReadOnly: false };

    // ------------------------------------------------------------------ the dock of a tool (#v21PlacementDock)

    private Control ToolDock()
    {
        _toolText.FontFamily = UrbeTheme.UiFont;
        _toolText.FontSize = 13;
        _toolText.Foreground = UrbeTheme.Brush(UrbeTheme.Text2);
        _toolText.VerticalAlignment = VerticalAlignment.Center;
        _toolText.TextTrimming = TextTrimming.CharacterEllipsis;
        var cancel = new Button { Content = UrbeTheme.Icon(UrbeTheme.Icons.Close, 18, UrbeTheme.Text2), Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Background = UrbeTheme.Brush(UrbeTheme.Surface3) };
        ToolTip.SetTip(cancel, "Cancelar");
        cancel.Click += (_, _) => EndTool();
        _toolOk.Content = "Construir";
        _toolOk.Height = 40;
        _toolOk.Padding = new Thickness(14, 0);
        _toolOk.CornerRadius = new CornerRadius(12);
        _toolOk.Background = UrbeTheme.Brush(UrbeTheme.Accent);
        _toolOk.Foreground = UrbeTheme.Brush(UrbeTheme.AccentInk);
        _toolOk.IsVisible = false;
        _toolOk.Click += async (_, _) => await ConfirmPlacementAsync();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _toolText, _toolOk, cancel } };
        _toolDock.Child = row;
        _toolDock.Background = UrbeTheme.Brush(UrbeTheme.Surface2);
        _toolDock.BorderBrush = UrbeTheme.Brush(UrbeTheme.Line2);
        _toolDock.BorderThickness = new Thickness(1);
        _toolDock.CornerRadius = new CornerRadius(16);
        _toolDock.Padding = new Thickness(14, 8, 8, 8);
        _toolDock.HorizontalAlignment = HorizontalAlignment.Center;
        _toolDock.VerticalAlignment = VerticalAlignment.Bottom;
        _toolDock.Margin = new Thickness(12, 0, 12, 14);
        _toolDock.MaxWidth = 520;
        _toolDock.IsVisible = false;
        return _toolDock;
    }

    private void BeginTool(MapTool tool, string text, bool okVisible)
    {
        // v23Navegar('mundo'): back to the city, panels away
        Explorer.IsVisible = false;
        CityMap.IsVisible = false;
        World.Select(null);
        World.Tool = tool;
        _copySource = null;
        _toolOk.Content = "Construir";
        _toolText.Text = text;
        _toolOk.IsVisible = okVisible;
        _toolOk.IsEnabled = false;
        _toolDock.IsVisible = true;
    }

    /// <summary>setTool('select'): the normal city again.</summary>
    public void EndTool()
    {
        World.Tool = null;
        _toolDock.IsVisible = false;
        _toolOk.IsVisible = false;
        _toolOk.Content = "Construir";
        _copySource = null;
    }

    // ------------------------------------------------------------------ Nova pasta

    /// <summary>criarPastaNoDestino (v21): name dialog, the neighbourhood on the map, the folder in the vault.</summary>
    public async Task CreateFolderAsync(string? parentId = null)
    {
        if (World.City is not { } city) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Nova pasta", NoVaultMessage);
            return;
        }
        var typed = await Dialogs.PromptAsync(new PromptOptions("Nova pasta", Label: "Nome da pasta", Placeholder: "Ex.: Projetos",
            Hint: "Cada pasta vira uma região da cidade.", Confirm: "Criar pasta"));
        if (typed is null || typed.Trim().Length == 0) return;
        if (city.CreateFolder(typed, parentId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) is not { } region)
        {
            ShowLifeNotice("Não encontrei área livre.");
            return;
        }
        if (!await WriteFolderAsync(city, region, undo: () => city.Regions.Remove(region))) return;
        World.InvalidateVisual();
        ShowLifeNotice("Pasta criada.");
    }

    /// <summary>Makes the region's folder in the vault; on failure the region is taken back.</summary>
    private async Task<bool> WriteFolderAsync(LegacyCity city, LegacyCityRegion region, Action undo)
    {
        try
        {
            await _vault!.CreateDirectoryAsync(city.RegionPaths()[region.Id]);
            RefreshNotes(); // model.addFolder: the folder is in Notas even before it has a note
            return true;
        }
        catch (Exception ex)
        {
            undo();
            World.InvalidateVisual();
            await Dialogs.AlertAsync("Não foi possível criar a pasta", ex.Message);
            return false;
        }
    }

    // ------------------------------------------------------------------ Desenhar pasta no mapa

    /// <summary>tool 'region': "Arraste no mapa para delimitar a nova pasta."</summary>
    public async Task BeginDrawRegionAsync()
    {
        if (World.City is null) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Desenhar pasta no mapa", NoVaultMessage);
            return;
        }
        BeginTool(new RegionDrawTool((x, y, w, h) => _ = FinishDrawnRegionAsync(x, y, w, h)),
            "Arraste no mapa para delimitar a nova pasta.", okVisible: false);
    }

    private async Task FinishDrawnRegionAsync(int x, int y, int w, int h)
    {
        if (World.City is not { } city || Dialogs.IsOpen) return;
        if (w < 3 || h < 3)
        {
            ShowLifeNotice("A região precisa ser maior.");
            return;
        }
        // "Nova região": the 1.8.4 dialog opens blank; the name is made unique on confirm
        var typed = await Dialogs.PromptAsync(new PromptOptions("Nova região", Label: "Nome", Confirm: "Criar", Required: false));
        if (typed is null) return;
        if (city.CreateDrawnRegion(typed, "", city.NextRegionColor(), x, y, w, h) is not { } drawn)
        {
            ShowLifeNotice("Não há terreno disponível nesta seleção.");
            return;
        }
        if (!await WriteFolderAsync(city, drawn.Region, drawn.Undo)) return;
        var stay = city.RootBuildingsUnder(drawn.Region).Count;
        EndTool();
        World.InvalidateVisual();
        ShowLifeNotice("Região criada com prioridade territorial" + (stay > 0
            ? $" · {stay} item(ns) da raiz continuam na raiz até o app nativo mover arquivos."
            : "."));
    }

    // ------------------------------------------------------------------ Adicionar construção

    /// <summary>v21BeginNewPlacement: tap the map to position the new building, then "Construir".</summary>
    public async Task BeginPlacementAsync()
    {
        if (World.City is not { } city) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Adicionar construção", NoVaultMessage);
            return;
        }
        var tool = new PlacementTool(city.CanHouse, c =>
        {
            _toolOk.IsEnabled = c is { } p && city.CanHouse(p.X, p.Y);
            _toolText.Text = "Posicione a construção" + (c is { } q ? $" · {q.X},{q.Y}" : " · toque no mapa");
        });
        BeginTool(tool, "Posicione a construção · toque no mapa", okVisible: true);
    }

    /// <summary>The house being copied while the placement tool is up (v21Placement = {kind:'copy', source}).</summary>
    private LegacyCityBuilding? _copySource;

    /// <summary>
    /// The house panel's Copiar (v21BeginCopyPlacement): the panel closes and a tap on the map positions the copy, then
    /// ✓ asks "Configurar cópia". Unlike the 1.8.4 (whose Copiar writes no copy), the copy is a real note: a new file
    /// with the source's text (owner's decision, ROADMAP UC-19).
    /// </summary>
    public async Task BeginCopyAsync(LegacyCityBuilding source)
    {
        if (World.City is not { } city || !source.IsNote) return;
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Copiar", NoVaultMessage);
            return;
        }
        var tool = new PlacementTool(city.CanHouse, c =>
        {
            _toolOk.IsEnabled = c is { } p && city.CanHouse(p.X, p.Y);
            _toolText.Text = "Posicione a cópia" + (c is { } q ? $" · {q.X},{q.Y}" : " · toque no mapa");
        });
        BeginTool(tool, "Posicione a cópia · toque no mapa", okVisible: true);
        _copySource = source;
        _toolOk.Content = "Copiar";
        ShowLifeNotice("Toque no mapa para posicionar a cópia.");
    }

    /// <summary>v21ConfirmPlacement (copy): "Configurar cópia", then the new file, its document and the house.</summary>
    private async Task ConfirmCopyAsync(LegacyCityBuilding source, (int X, int Y) p)
    {
        if (World.City is not { } city) return;
        var typed = await Dialogs.PromptAsync(new PromptOptions("Configurar cópia", Label: "Nome", Placeholder: "Ex.: Ideias",
            Hint: "Sem extensão, vira .md. Também aceita .txt, .html, .css, .js, .json, .yaml e .csv.", Confirm: "Copiar"));
        if (typed is null) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (city.CopyNoteAt(source, p.X, p.Y, typed, now) is not { } copy)
        {
            ShowLifeNotice("Posição inválida.");
            return;
        }
        var content = _contents.GetValueOrDefault(source.Path, "");
        if (Editor.IsVisible && Editor.Current is { } open && open.Path == source.Path) content = open.Content;
        try
        {
            await _vault!.CreateTextFileAsync(copy.Path, content);
        }
        catch (Exception ex)
        {
            city.RemoveBuilding(copy);
            World.InvalidateVisual();
            await Dialogs.AlertAsync("Não foi possível copiar", ex.Message);
            return;
        }
        RegisterNewNote(copy, content, now);
        EndTool();
        World.Select(copy);
        ShowLifeNotice("Cópia posicionada.");
    }

    /// <summary>
    /// A note house whose file was just written gets its document, as urbeReconcileWorld does on the next save
    /// ({path, content, created, modified} = the house's nowDate(), 'world.create'), and the map is saved with it
    /// (agendarSalvar + marcarSinc). Without this the next map save would be refused (a house without document).
    /// </summary>
    private void RegisterNewNote(LegacyCityBuilding house, string content, long nowMs)
    {
        _contents[house.Path] = content;
        if (_store is not null)
        {
            var today = LegacyCity.NowDate(nowMs);
            _store.Upsert(new DocumentInput { Path = house.Path, Content = content, Created = today, Modified = today }, "world.create");
            _mapSave?.Schedule();
            _mapSave?.MarkSync();
        }
        RefreshNotes();
    }

    /// <summary>v21ConfirmPlacement (new): "Configurar arquivo", the house at the tile, its file in the vault.</summary>
    public async Task ConfirmPlacementAsync()
    {
        if (World.City is not { } city || World.Tool is not PlacementTool { Candidate: { } p } || Dialogs.IsOpen) return;
        if (_copySource is { } source)
        {
            if (!city.CanHouse(p.X, p.Y)) { ShowLifeNotice("Posição inválida."); return; }
            await ConfirmCopyAsync(source, p);
            return;
        }
        if (!city.CanHouse(p.X, p.Y))
        {
            ShowLifeNotice("Posição inválida.");
            return;
        }
        var typed = await Dialogs.PromptAsync(new PromptOptions("Configurar arquivo", Label: "Nome", Placeholder: "Ex.: Ideias",
            Hint: "Sem extensão, vira .md. Também aceita .txt, .html, .css, .js, .json, .yaml e .csv.", Confirm: "Construir"));
        if (typed is null) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (city.CreateNoteAt(p.X, p.Y, typed, now) is not { } made)
        {
            ShowLifeNotice("Posição inválida.");
            return;
        }
        try
        {
            await _vault!.CreateTextFileAsync(made.House.Path, made.Content);
        }
        catch (Exception ex)
        {
            city.RemoveBuilding(made.House);
            World.InvalidateVisual();
            await Dialogs.AlertAsync("Não foi possível criar a nota", ex.Message);
            return;
        }
        RegisterNewNote(made.House, made.Content, now);
        EndTool();
        World.Select(made.House);
        ShowLifeNotice("Arquivo construído.");
    }

    // ------------------------------------------------------------------ Importar

    private bool _importBusy;

    /// <summary>v21ImportPicker + importarEntradasDestino for documents: choose files or a folder, check,
    /// confirm, then every new text document is written (and verified) into the vault.</summary>
    public async Task ImportAsync(string? regionId = null)
    {
        if (World.City is not { } city) return;
        if (_importBusy)
        {
            ShowLifeNotice("Uma importação está em andamento. Aguarde o resultado.");
            return;
        }
        if (!VaultWritable)
        {
            await Dialogs.AlertAsync("Importar", NoVaultMessage);
            return;
        }
        _importBusy = true;
        try
        {
            var mode = await Dialogs.ChooseAsync("O que deseja importar?",
            [
                new DialogItem("Arquivos", UrbeTheme.Icons.Upload, "Selecione um ou vários arquivos. O Urbe identifica automaticamente o conteúdo."),
                new DialogItem("Pasta inteira", UrbeTheme.Icons.Folder, "Selecione um vault ou uma pasta com seus documentos.")
            ]);
            if (mode is null) return;
            ImportSelection? selection;
            try { selection = await PickImportAsync(mode == 1); }
            catch (Exception ex)
            {
                await Dialogs.AlertAsync("Não foi possível abrir a seleção", ex.Message);
                return;
            }
            if (selection is null) return;
            await ImportSelectionAsync(city, selection, regionId);
        }
        finally { _importBusy = false; }
    }

    /// <summary>The checked selection into the vault (everything after the system picker).</summary>
    public async Task ImportSelectionAsync(LegacyCity city, ImportSelection selection, string? regionId)
    {
        if (selection.Sources.Count == 0)
        {
            await Dialogs.AlertAsync("Nenhum arquivo encontrado", "A seleção está vazia. Escolha uma pasta com documentos ou um backup.");
            return;
        }
        ImportInspection inspection;
        try
        {
            ShowLifeNotice("Conferindo os arquivos…");
            inspection = await ImportPipeline.InspectAsync(selection);
            if (inspection.SourceConflicts is { Count: > 0 } conflicts)
            {
                var chosen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var conflict in conflicts)
                {
                    var pick = await Dialogs.ChooseAsync("Arquivos com o mesmo nome",
                        conflict.Candidates.Select((c, i) => new DialogItem("Versão " + (i + 1) + " — " + c.Path, Detail: c.Bytes.Length + " bytes")).ToList(),
                        conflict.Path + " tem versões diferentes na seleção. Escolha qual importar ou cancele para selecionar novamente.");
                    if (pick is null)
                    {
                        ShowLifeNotice("Importação cancelada.");
                        return;
                    }
                    chosen[conflict.Path] = pick.Value;
                }
                inspection = ImportPipeline.ResolveSources(inspection, chosen);
            }
        }
        catch (InvalidDataException ex)
        {
            await Dialogs.AlertAsync("Não foi possível importar", ex.Message);
            return;
        }
        if (inspection.Kind != ImportKind.Documents)
        {
            await Dialogs.AlertAsync("Não foi possível importar",
                "Restaurar um vault ou backup ainda não está disponível no app nativo. Nenhum dado foi alterado. Selecione apenas documentos ou uma pasta de documentos.");
            return;
        }

        var plan = ImportDocuments.Plan(inspection, _contents, regionId is not null ? city.RegionPaths().GetValueOrDefault(regionId) : null);
        var description = $"{inspection.DocumentCount} documento(s) e {inspection.AssetCount} arquivo(s). Os documentos serão incorporados ao vault atual, mantendo suas pastas e referências."
            + (plan.Skipped.Count > 0 ? $" {plan.Skipped.Count} arquivo(s) (anexos que não são texto e metadados) não podem ser gravados no app nativo ainda e ficam de fora." : "")
            + (plan.Kept.Count > 0 ? $" {plan.Kept.Count} documento(s) já existem com conteúdo diferente e serão mantidos como estão." : "");
        if (await Dialogs.ChooseAsync("Documentos para incorporar", [new DialogItem("Continuar")], description) is null)
        {
            ShowLifeNotice("Importação cancelada.");
            return;
        }
        int written = 0;
        try
        {
            foreach (var (path, content) in plan.New)
            {
                if (written % 10 == 0) ShowLifeNotice($"Importando {written} de {plan.New.Count}…");
                await WriteVaultFileAsync(path, content, exists: false);
                written++;
            }
        }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível importar", $"{written} de {plan.New.Count} documento(s) foram gravados antes do erro: {ex.Message}");
            return;
        }
        World.InvalidateVisual();
        ShowLifeNotice($"Importação concluída. {written} arquivo(s) gravado(s); {plan.Unchanged} preservado(s), sem nova gravação.");
    }

    private async Task<ImportSelection?> PickImportAsync(bool folder)
    {
        if (ImportPicker is not null) return await ImportPicker(folder);
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return null;
        var sources = new List<ImportSource>();
        if (folder)
        {
            var picked = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Importar pasta", AllowMultiple = false });
            if (picked.Count == 0) return null;
            async Task Walk(IStorageFolder dir, string prefix, int depth)
            {
                if (depth > 32) throw new IOException("A pasta ultrapassa 32 níveis.");
                await foreach (var item in dir.GetItemsAsync())
                {
                    if (item is IStorageFolder sub) await Walk(sub, prefix + item.Name + "/", depth + 1);
                    else if (item is IStorageFile file) sources.Add(new ImportSource(prefix + file.Name, async _ => await file.OpenReadAsync()));
                }
            }
            await Walk(picked[0], "", 0);
        }
        else
        {
            var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Importar arquivos", AllowMultiple = true });
            foreach (var file in picked) sources.Add(new ImportSource(file.Name, async _ => await file.OpenReadAsync()));
        }
        return sources.Count == 0 ? null : new ImportSelection(sources, folder);
    }
}

/// <summary>What a documents import will do to the current vault (importarEntradasDestino for text files).</summary>
public sealed record ImportDocumentsPlan(IReadOnlyList<(string Path, string Content)> New, IReadOnlyList<string> Kept,
    IReadOnlyList<string> Skipped, int Unchanged);

public static class ImportDocuments
{
    /// <summary>The selection's text documents (under the target folder when one is chosen) against the
    /// vault's: new ones are written, identical ones are left alone, different ones are KEPT as they are
    /// (the native vault has no backup-and-replace yet); non-text files and hidden metadata are skipped.</summary>
    public static ImportDocumentsPlan Plan(ImportInspection inspection, IReadOnlyDictionary<string, string> current, string? targetFolder)
    {
        var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in current) existing[pair.Key] = pair.Value;
        var added = new List<(string, string)>();
        var kept = new List<string>();
        var skipped = new List<string>();
        int unchanged = 0;
        foreach (var file in inspection.Files.Values.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var path = string.IsNullOrEmpty(targetFolder) ? file.Path : targetFolder + "/" + file.Path;
            if (!IUrbeVaultStorage.WritableExtensions.Contains(System.IO.Path.GetExtension(path)) || path.Split('/').Any(s => s.StartsWith('.')))
            {
                skipped.Add(path);
                continue;
            }
            var text = new UTF8Encoding(false, true).GetString(file.Bytes.Span);
            if (existing.TryGetValue(path, out var old))
            {
                if (old == text) unchanged++; else kept.Add(path);
            }
            else added.Add((path, text));
        }
        return new ImportDocumentsPlan(added, kept, skipped, unchanged);
    }
}
