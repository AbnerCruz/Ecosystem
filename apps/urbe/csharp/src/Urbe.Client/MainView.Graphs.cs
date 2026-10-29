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
            panel.ShapeRequested += () => _ = AddGraphShapeAsync();
            panel.GroupRequested += () => _ = GroupGraphNodesAsync();
            panel.ResizeRequested += () => _ = ResizeGraphNodeAsync();
            panel.UndoRequested += () => _ = GraphUndoAsync(redo: false);
            panel.RedoRequested += () => _ = GraphUndoAsync(redo: true);
            panel.Canvas.Changed += () => { _graphDirty = true; panel.Refresh(); _ = SaveGraphAsync(); };
            panel.Canvas.Exists = ReferenceExists;
            _graphBoard = panel;
            _stage.Children.Add(panel);
            return panel;
        }
    }

    public bool GraphOpen => _graphBoard?.IsVisible == true;

    /// <summary>The graph open in the board (tests).</summary>
    public GraphDocument? OpenGraph => _openGraph;

    /// <summary>CST-066: a card's note (by its id first, so a rename never loses it), file or folder is in the vault.</summary>
    private bool ReferenceExists(GraphNode node)
    {
        if (node.Target is not { } target) return true;
        return node.Kind switch
        {
            GraphNodeKind.Note => node.ReferenceId is { } id && _store?.Get(id) is not null || _contents.ContainsKey(target),
            GraphNodeKind.File => _attachments.Contains(target) || _contents.ContainsKey(target),
            GraphNodeKind.Folder => _library?.Folders.Contains(target.TrimEnd('/')) == true ||
                _contents.Keys.Concat(_attachments).Any(p => p.StartsWith(target.TrimEnd('/') + "/", StringComparison.Ordinal)),
            _ => true
        };
    }

    /// <summary>
    /// CST-066: notes, files or folders renamed or moved in the Urbe — every graph of the vault follows (the open one in
    /// memory, saved with it; the others written and verified). A graph that cannot be read is left as it is.
    /// </summary>
    private async Task RetargetGraphsAsync(IReadOnlyList<(string From, string To)> moves)
    {
        if (moves.Count == 0 || _vault is not { IsConnected: true } vault || !VaultWritable) return;
        await _graphWriteGate.WaitAsync();
        var failed = new List<string>();
        try
        {
            // the open graph's own file may have moved with its folder
            if (_openGraphPath is { } openPath)
                foreach (var (from, to) in moves)
                {
                    if (string.Equals(openPath, from, StringComparison.OrdinalIgnoreCase)) _openGraphPath = to;
                    else if (openPath.StartsWith(from.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)) _openGraphPath = to.TrimEnd('/') + openPath[from.TrimEnd('/').Length..];
                }
            foreach (var path in _attachments.Where(GraphDocument.IsGraphPath).ToList())
            {
                bool open = _openGraph is not null && string.Equals(_openGraphPath, path, StringComparison.OrdinalIgnoreCase);
                try
                {
                    GraphDocument graph;
                    if (open) graph = _openGraph!;
                    else if ((await vault.ReadFileAsync(path, long.MaxValue))?.Bytes is { } bytes) graph = GraphDocument.Parse(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));
                    else continue;
                    int changed = 0;
                    foreach (var (from, to) in moves) changed += graph.Retarget(from, to);
                    if (changed == 0) continue;
                    if (open) { _graphDirty = true; GraphsPanel.Canvas.InvalidateVisual(); }
                    else await vault.SaveExistingTextFileAsync(path, graph.Serialize());
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException or InvalidOperationException)
                {
                    failed.Add(Path.GetFileName(path));
                }
            }
        }
        finally { _graphWriteGate.Release(); }
        if (_graphDirty) await SaveGraphAsync();
        if (failed.Count > 0) ShowLifeNotice("Não foi possível atualizar as referências em: " + string.Join(", ", failed) + ". Os grafos ficaram como estavam.", Ui.ToastKind.Warning);
    }

    /// <summary>The Grafos destination lists authored graph documents, never replaces the vault's hierarchy.</summary>
    public async Task ShowGraphsHomeAsync()
    {
        if (GraphOpen) return;
        if (Dialogs.IsOpen || StillReading()) return;
        var paths = _attachments.Where(GraphDocument.IsGraphPath)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var entries = paths.Select(p => new DialogItem(p[..^GraphDocument.FileSuffix.Length], UrbeTheme.Icons.Layers)).ToList();
        entries.Insert(0, new DialogItem("+ Novo grafo", UrbeTheme.Icons.Plus, "Criar um canvas livre no vault"));
        entries.Insert(1, new DialogItem("Gerar estrutura do vault", UrbeTheme.Icons.Folder,
            "Pastas, arquivos, notas e links resolvidos"));
        var choice = await Dialogs.ChooseAsync("Grafos", entries);
        if (choice is not { } selected) return;
        if (selected == 0) await CreateGraphAsync();
        else if (selected == 1) await CreateGraphStructureAsync();
        else await OpenGraphAsync(paths[selected - 2]);
    }

    /// <summary>Creates an editable snapshot of the actual vault hierarchy and resolved wiki-links.</summary>
    public async Task CreateGraphStructureAsync(string? scope = null)
    {
        if (_vault is not { IsConnected: true } || _store is null || !VaultWritable)
        {
            await Dialogs.AlertAsync("Estrutura do vault", "Abra um vault com escrita para criar a projeção.");
            return;
        }
        scope ??= Explorer.CurrentFolder;
        GraphDocument graph;
        try
        {
            using var index = new KnowledgeIndex(_store);
            var linked = _store.List().ToDictionary(d => d.Id,
                d => (IEnumerable<string>)index.Links(d.Id).Select(other => other.Id).ToArray(), StringComparer.Ordinal);
            graph = GraphProjection.FromVault(_store.List(), _attachments, NoteFolders(),
                id => linked.GetValueOrDefault(id) ?? [], scope);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException)
        {
            await Dialogs.AlertAsync("Não foi possível gerar o grafo", StorageProblems.Explain(ex));
            return;
        }
        var prefix = (scope.Length > 0 ? scope.TrimEnd('/') + "/" : "");
        var baseName = scope.Length > 0 ? "Estrutura de " + Path.GetFileName(scope) : "Estrutura do vault";
        var name = ArtifactModel.SafeName(baseName);
        var path = prefix + name + GraphDocument.FileSuffix;
        for (var i = 2; _attachments.Contains(path) || _contents.ContainsKey(path); i++)
            path = prefix + name + "-" + i + GraphDocument.FileSuffix;
        try { await _vault.CreateTextFileAsync(path, graph.Serialize()); }
        catch (Exception ex)
        {
            await Dialogs.AlertAsync("Não foi possível salvar a estrutura", StorageProblems.Explain(ex));
            return;
        }
        _attachments.Add(path);
        RefreshNotes();
        await OpenGraphAsync(path);
        ShowLifeNotice("Estrutura gerada a partir dos arquivos atuais. O canvas é uma cópia editável, não move pastas.");
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
            await Dialogs.AlertAsync("Não foi possível criar o grafo", StorageProblems.Explain(ex));
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
            var read = await _vault.ReadFileAsync(path, long.MaxValue);
            if (read?.Bytes is not { } bytes)
                throw new InvalidDataException("Arquivo de grafo não encontrado ou ilegível.");
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
            await Dialogs.AlertAsync("Não foi possível abrir o grafo", StorageProblems.Explain(ex) + " O arquivo original não foi alterado.");
        }
    }

    /// <summary>
    /// CST-068: the Assistente's edit_graph, after the approval. It waits for the editor's own save, never overwrites a
    /// graph open with edits not yet written, and an open graph shows the new version at once.
    /// </summary>
    internal async Task WriteGraphForAssistantAsync(string path, string json, bool create)
    {
        if (_vault is not { IsConnected: true } vault || !VaultWritable)
            throw new Urbe.Core.Assistant.AiToolException("O vault está em somente leitura; o grafo não foi gravado.");
        await _graphWriteGate.WaitAsync();
        try
        {
            bool open = _openGraphPath is { } current && string.Equals(current, path, StringComparison.OrdinalIgnoreCase);
            if (open && _graphDirty)
                throw new Urbe.Core.Assistant.AiToolException("O grafo está aberto com alterações não salvas. Nada foi gravado; tente de novo depois que o grafo for salvo.");
            if (create)
            {
                if (_contents.ContainsKey(path) || _attachments.Contains(path)) throw new Urbe.Core.Assistant.AiToolException("Já existe " + path + ".");
                await vault.CreateTextFileAsync(path, json);
                _attachments.Add(path);
                RefreshNotes();
            }
            else await vault.SaveExistingTextFileAsync(path, json);
            if (open)
            {
                _openGraph = GraphDocument.Parse(json);
                GraphsPanel.Show(_openGraph);
            }
        }
        finally { _graphWriteGate.Release(); }
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

    private async Task<bool> CloseGraphAsync()
    {
        if (!await SaveGraphAsync()) return false;
        if (_graphBoard is not null) _graphBoard.IsVisible = false;
        _openGraph = null;
        _openGraphPath = null;
        UpdateNav();
        return true;
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
        GraphsPanel.Refresh();
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
        var referenceId = selected.Kind == GraphNodeKind.Note ? _store?.Get(selected.Path)?.Id : null;
        var node = _openGraph.AddReference(selected.Kind, selected.Path, at.X - 95, at.Y - 42, referenceId);
        GraphsPanel.Canvas.Select(node.Id);
        _graphDirty = true;
        GraphsPanel.Refresh();
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
        GraphsPanel.Refresh();
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
        GraphsPanel.Refresh();
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
        GraphsPanel.Refresh();
        await SaveGraphAsync();
    }

    private async Task GraphUndoAsync(bool redo)
    {
        if (_openGraph is null) return;
        var changed = redo ? _openGraph.Redo() : _openGraph.Undo();
        if (!changed) return;
        GraphsPanel.Canvas.Select(null);
        GraphsPanel.Refresh();
        _graphDirty = true;
        await SaveGraphAsync();
    }

    private async Task AddGraphShapeAsync()
    {
        if (_openGraph is null) return;
        var label = await Dialogs.PromptAsync(new PromptOptions("Nova forma", Label: "Rótulo",
            Placeholder: "Ex.: Evento, condição, etapa", Required: false, Confirm: "Adicionar forma"));
        if (label is null) return;
        var at = GraphsPanel.Canvas.WorldCenter;
        var node = _openGraph.AddNode(GraphNodeKind.Shape, label, null, at.X - 95, at.Y - 42);
        GraphsPanel.Canvas.Select(node.Id);
        GraphsPanel.Refresh();
        _graphDirty = true;
        await SaveGraphAsync();
    }

    private async Task GroupGraphNodesAsync()
    {
        if (_openGraph is null || GraphsPanel.Canvas.SelectedIds.Count < 2) return;
        var label = await Dialogs.PromptAsync(new PromptOptions("Agrupar elementos", Label: "Nome do grupo",
            Value: "Novo grupo", Confirm: "Agrupar"));
        if (label is null) return;
        try
        {
            var group = _openGraph.GroupNodes(GraphsPanel.Canvas.SelectedIds, label);
            GraphsPanel.Canvas.MultipleSelection = false;
            GraphsPanel.Canvas.Select(group.Id);
            GraphsPanel.Refresh();
            _graphDirty = true;
            await SaveGraphAsync();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        { await Dialogs.AlertAsync("Não foi possível agrupar", StorageProblems.Explain(ex)); }
    }

    private async Task ResizeGraphNodeAsync()
    {
        if (_openGraph is null || GraphsPanel.Canvas.SelectedNode is not { } node) return;
        var input = await Dialogs.PromptAsync(new PromptOptions("Redimensionar", Label: "Largura × altura",
            Value: ((int)node.Width).ToString(System.Globalization.CultureInfo.InvariantCulture) + " x " +
                   ((int)node.Height).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Hint: "Use px do canvas, por exemplo: 240 x 120", Confirm: "Aplicar",
            Validate: v => ParseSize(v) is null ? "Informe dois números positivos separados por x." : null));
        if (input is null) return;
        var dimension = ParseSize(input);
        if (dimension is null) return;
        try
        {
            if (_openGraph.ResizeNode(node.Id, dimension.Value.Width, dimension.Value.Height))
            {
                _graphDirty = true;
                GraphsPanel.Refresh();
                await SaveGraphAsync();
            }
        }
        catch (InvalidDataException ex) { await Dialogs.AlertAsync("Tamanho inválido", StorageProblems.Explain(ex)); }
    }

    private static (double Width, double Height)? ParseSize(string value)
    {
        var pieces = value.Replace("×", "x", StringComparison.Ordinal).Split('x');
        if (pieces.Length != 2 || !double.TryParse(pieces[0].Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var width) ||
            !double.TryParse(pieces[1].Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var height))
            return null;
        return double.IsFinite(width) && double.IsFinite(height) && width > 0 &&
            height > 0 ? (width, height) : null;
    }

    private async Task OpenGraphReferenceAsync()
    {
        if (GraphsPanel.Canvas.SelectedNode is not { Target: { } target } selected) return;
        var path = selected.Kind == GraphNodeKind.Note && selected.ReferenceId is { } id
            ? _store?.Get(id)?.Path ?? target : target;
        if (!await SaveGraphAsync()) return;
        GraphsPanel.IsVisible = false;
        UpdateNav();
        await OpenReferenceAsync(selected with { Target = path });
    }

    /// <summary>What a card points to: its note (found by its id after a rename), file or folder.</summary>
    private async Task OpenReferenceAsync(GraphNode node)
    {
        if (node.Target is not { } target) return;
        var path = node.Kind == GraphNodeKind.Note && node.ReferenceId is { } id ? _store?.Get(id)?.Path ?? target : target;
        if (_contents.ContainsKey(path)) OpenNoteByPath(path);
        else if (_attachments.Contains(path)) await ShowFileAsync(path);
        else if (node.Kind == GraphNodeKind.Folder) Workspace.Open(path);
        else ShowLifeNotice("A referência não foi encontrada no vault: " + path, Ui.ToastKind.Warning);
    }
}
