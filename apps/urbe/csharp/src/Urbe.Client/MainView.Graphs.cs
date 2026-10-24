using System.Text;
using Avalonia;
using Urbe.Core;

namespace Urbe.Client;

/// <summary>
/// User-authored graph files live in the vault beside the other artefacts. Their JSON has its own
/// ownership and is saved through the same verified text-write boundary as the editor.
/// </summary>
public sealed partial class MainView
{
    private GraphBoardPanel? _graphBoard;
    private GraphDocument? _openGraph;
    private string? _openGraphPath;
    private bool _graphDirty;
    private readonly SemaphoreSlim _graphWriteGate = new(1, 1);

    public GraphBoardPanel GraphsPanel
    {
        get
        {
            if (_graphBoard is not null) return _graphBoard;
            var panel = new GraphBoardPanel { ZIndex = 8, IsVisible = false };
            panel.BackRequested += () => _ = CloseGraphAsync();
            panel.SaveRequested += () => _ = SaveGraphAsync();
            panel.AddTextRequested += () => _ = AddGraphTextAsync();
            panel.AddVaultRequested += () => _ = AddGraphVaultAsync();
            panel.EditRequested += () => _ = EditGraphNodeAsync();
            panel.LinkRequested += () => _ = ConnectGraphNodesAsync();
            panel.DeleteRequested += () => _ = RemoveGraphNodeAsync();
            panel.OpenRequested += () => _ = OpenGraphReferenceAsync();
            panel.Canvas.Changed += () => { _graphDirty = true; _ = SaveGraphAsync(); };
            _graphBoard = panel;
            _stage.Children.Add(panel);
            return panel;
        }
    }

    public bool GraphOpen => _graphBoard?.IsVisible == true;

    /// <summary>The Grafos destination lists authored graph documents, never replaces the vault's hierarchy.</summary>
    public async Task ShowGraphsHomeAsync()
    {
        if (GraphOpen) return;
        if (Dialogs.IsOpen || StillReading()) return;
        var paths = _attachments.Where(GraphDocument.IsGraphPath)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var entries = paths.Select(p => new DialogItem(p[..^GraphDocument.FileSuffix.Length], UrbeTheme.Icons.Layers)).ToList();
        entries.Insert(0, new DialogItem("+ Novo grafo", UrbeTheme.Icons.Plus, "Criar um canvas livre no vault"));
        var choice = await Dialogs.ChooseAsync("Grafos", entries);
        if (choice is not { } selected) return;
        if (selected == 0) await CreateGraphAsync();
        else await OpenGraphAsync(paths[selected - 1]);
    }

    /// <summary>Creates a standalone graph file. Never changes any existing note or the derived Constelação.</summary>
    public async Task CreateGraphAsync(string? typedName = null, string? folder = null)
    {
        if (_vault is not { IsConnected: true } || !VaultWritable)
        {
            await Dialogs.AlertAsync("Grafos", "Abra um vault com permissão de escrita para criar grafos.");
            return;
        }
        folder ??= Explorer.CurrentFolder;
        var requested = typedName ?? await Dialogs.PromptAsync(new PromptOptions("Novo grafo", Label: "Nome",
            Placeholder: "Ex.: Árvore da Família Real", Confirm: "Criar grafo"));
        if (requested is null) return;
        var name = ArtifactModel.SafeName(requested.Trim());
        if (string.IsNullOrWhiteSpace(requested)) return;
        if (name.EndsWith(GraphDocument.FileSuffix, StringComparison.OrdinalIgnoreCase))
            name = name[..^GraphDocument.FileSuffix.Length];
        var path = (folder.Length > 0 ? folder.TrimEnd('/') + "/" : "") + name + GraphDocument.FileSuffix;
        if (_contents.ContainsKey(path) || _attachments.Contains(path))
        {
            await Dialogs.AlertAsync("Grafo existente", "Já existe um arquivo com esse nome no vault.");
            return;
        }
        var document = GraphDocument.Create(name);
        try { await _vault.CreateTextFileAsync(path, document.Serialize()); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível criar o grafo", ex.Message);
            return;
        }
        _attachments.Add(path);
        RefreshNotes();
        await OpenGraphAsync(path);
    }

    /// <summary>Opens any graph file listed by the explorer, including files created outside Urbe.</summary>
    public async Task OpenGraphAsync(string path)
    {
        if (!GraphDocument.IsGraphPath(path)) return;
        if (_vault is null) return;
        if (GraphOpen && !await SaveGraphAsync()) return;
        try
        {
            var read = await _vault.ReadFileAsync(path, 2L * 1024 * 1024);
            if (read?.Bytes is not { } bytes)
                throw new InvalidDataException("Grafo ausente ou maior que 2 MB.");
            var graph = GraphDocument.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
            _openGraph = graph;
            _openGraphPath = path;
            _graphDirty = false;
            GraphsPanel.Show(graph);
            GraphsPanel.IsVisible = true;
            UpdateNav();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            await Dialogs.AlertAsync("Não foi possível abrir o grafo", ex.Message + " O arquivo original não foi alterado.");
        }
    }

    private async Task<bool> SaveGraphAsync()
    {
        await _graphWriteGate.WaitAsync();
        try
        {
            if (!_graphDirty) return true;
            if (_openGraph is null || _openGraphPath is null || _vault is null || !VaultWritable)
            {
                ShowLifeNotice("O grafo tem alterações não salvas. O vault está em somente leitura.", Ui.ToastKind.Warning);
                return false;
            }
            var json = _openGraph.Serialize();
            try { await _vault.SaveExistingTextFileAsync(_openGraphPath, json); }
            catch (Exception ex)
            {
                ShowLifeNotice("Não foi possível salvar o grafo: " + ex.Message, Ui.ToastKind.Warning);
                return false;
            }
            // If an edit occurred while the write was in progress, keep it pending for the next save.
            _graphDirty = _openGraph.Serialize() != json;
            return !_graphDirty || await SaveGraphAsyncAfterEdit();
        }
        finally { _graphWriteGate.Release(); }
    }

    private async Task<bool> SaveGraphAsyncAfterEdit()
    {
        if (_openGraph is null || _openGraphPath is null || _vault is null) return false;
        var current = _openGraph.Serialize();
        try { await _vault.SaveExistingTextFileAsync(_openGraphPath, current); }
        catch (Exception ex) { ShowLifeNotice("Grafo não salvo: " + ex.Message, Ui.ToastKind.Warning); return false; }
        _graphDirty = _openGraph.Serialize() != current;
        return !_graphDirty;
    }

    private async Task CloseGraphAsync()
    {
        if (!await SaveGraphAsync()) return;
        if (_graphBoard is not null) _graphBoard.IsVisible = false;
        UpdateNav();
        _openGraph = null;
        _openGraphPath = null;
    }

    private async Task AddGraphTextAsync()
    {
        if (_openGraph is null) return;
        var label = await Dialogs.PromptAsync(new PromptOptions("Adicionar texto", Label: "Conteúdo",
            Placeholder: "Ideia, personagem, acontecimento…", Confirm: "Adicionar"));
        if (string.IsNullOrWhiteSpace(label)) return;
        var at = GraphsPanel.Canvas.WorldCenter;
        var item = _openGraph.AddText(label, at.X - 95, at.Y - 42);
        GraphsPanel.Canvas.Select(item.Id);
        _graphDirty = true;
        await SaveGraphAsync();
    }

    private async Task AddGraphVaultAsync()
    {
        if (_openGraph is null) return;
        var references = _contents.Keys.Select(p => (Path:p, Kind:GraphNodeKind.Note))
            .Concat(_attachments.Where(p => !p.StartsWith(".", StringComparison.Ordinal))
                .Select(p => (Path:p, Kind:GraphNodeKind.File)))
            .Concat(NoteFolders().Where(p => p.Length > 0).Select(p => (Path:p, Kind:GraphNodeKind.Folder)))
            .DistinctBy(x => (x.Path, x.Kind))
            .OrderBy(x => x.Path, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (references.Count == 0)
        {
            await Dialogs.AlertAsync("Adicionar do vault", "O vault não tem arquivos nem pastas.");
            return;
        }
        var choice = await Dialogs.ChooseAsync("Adicionar do vault",
            references.Select(x => new DialogItem(x.Path, x.Kind == GraphNodeKind.Folder ? UrbeTheme.Icons.Folder : UrbeTheme.Icons.File)).ToList());
        if (choice is not { } index) return;
        var selected = references[index];
        var at = GraphsPanel.Canvas.WorldCenter;
        var node = _openGraph.AddReference(selected.Kind, selected.Path, at.X - 95, at.Y - 42);
        GraphsPanel.Canvas.Select(node.Id);
        _graphDirty = true;
        await SaveGraphAsync();
    }

    private async Task EditGraphNodeAsync()
    {
        if (_openGraph is null || GraphsPanel.Canvas.SelectedNode is not { } node) return;
        var value = await Dialogs.PromptAsync(new PromptOptions("Editar elemento", Label: "Texto do cartão",
            Value: node.Label, Confirm: "Aplicar"));
        if (value is null) return;
        _openGraph.RenameNode(node.Id, value);
        GraphsPanel.Canvas.Refresh();
        _graphDirty = true;
        await SaveGraphAsync();
    }

    private async Task ConnectGraphNodesAsync()
    {
        if (_openGraph is null || GraphsPanel.Canvas.SelectedNode is not { } source) return;
        var options = _openGraph.Nodes.Where(n => n.Id != source.Id).ToArray();
        if (options.Length == 0) return;
        var picked = await Dialogs.ChooseAsync("Conectar a", options.Select(n => new DialogItem(n.Label)).ToArray());
        if (picked is not { } index) return;
        var kind = await Dialogs.ChooseAsync("Estilo da conexão",
            [new DialogItem("Linha — associação livre"), new DialogItem("Seta — relação direcionada")]);
        if (kind is null) return;
        var label = await Dialogs.PromptAsync(new PromptOptions("Rótulo da conexão", Label: "Relação (opcional)",
            Placeholder: "Ex.: descende, referencia, depende de…", Required: false, Confirm: "Conectar"));
        if (label is null) return;
        _openGraph.Connect(source.Id, options[index].Id, label, relation: "", directed: kind == 1);
        GraphsPanel.Canvas.Refresh();
        _graphDirty = true;
        await SaveGraphAsync();
    }

    private async Task RemoveGraphNodeAsync()
    {
        if (_openGraph is null || GraphsPanel.Canvas.SelectedNode is not { } node) return;
        var action = await Dialogs.ChooseAsync("Remover do grafo",
            [new DialogItem("Remover apenas o cartão", Detail: "O arquivo no vault não será apagado."),
             new DialogItem("Cancelar")]);
        if (action != 0) return;
        _openGraph.RemoveNode(node.Id);
        GraphsPanel.Canvas.Select(null);
        _graphDirty = true;
        await SaveGraphAsync();
    }

    private async Task OpenGraphReferenceAsync()
    {
        if (GraphsPanel.Canvas.SelectedNode?.Target is not { } path) return;
        if (!await SaveGraphAsync()) return;
        GraphsPanel.IsVisible = false;
        UpdateNav();
        if (_contents.ContainsKey(path)) OpenNoteByPath(path);
        else if (_attachments.Contains(path)) await ShowFileAsync(path);
        else if (GraphsPanel.Canvas.SelectedNode?.Kind == GraphNodeKind.Folder) Workspace.Open(path);
        else ShowLifeNotice("A referência não foi encontrada no vault: " + path, Ui.ToastKind.Warning);
    }
}
