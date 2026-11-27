using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Editor;

namespace Lunet.Client.Views;

/// <summary>
/// The command palette (LUNET-536 M3), in the support sheet: a query field with prefixes (&gt; commands, @ outline,
/// : line, # game symbols, / find) and a ranked list. It brings the IDE tools of the previous Android screens
/// (definition, references, rename, quick fixes, format, line operations) into the shell, over the portable
/// <see cref="EditorAssistant"/> and <see cref="LineOperations"/>.
/// </summary>
public sealed class CommandPaletteView : UserControl
{
    private readonly LunetShell _shell;
    private readonly TextBox _query = new()
    {
        PlaceholderText = "Comando, @símbolo, :linha, #no jogo, /buscar",
        FontFamily = LunetTokens.UiFont, FontSize = LunetTokens.Type.Body, MinHeight = LunetTokens.MinTouch,
        Background = LunetTokens.Brush(LunetTokens.Color.Surface), CornerRadius = new CornerRadius(LunetTokens.Radius.M),
        Padding = new Thickness(LunetTokens.Space.M, LunetTokens.Space.S),
    };
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly List<string> _recents = [];
    private IReadOnlyList<PaletteItem>? _results;
    private string? _resultsTitle;
    private Func<string, Task>? _input; // a command waiting for text (rename)

    public CommandPaletteView(LunetShell shell)
    {
        _shell = shell;
        AutomationProperties.SetName(_query, "Buscar comando");
        _query.TextChanged += (_, _) => { _ = RefreshAsync(); };
        _query.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; _ = SubmitAsync(); }
        };
        var root = new DockPanel { Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.S) };
        DockPanel.SetDock(_query, Dock.Top);
        root.Children.Add(_query);
        root.Children.Add(new ScrollViewer { Content = _list, Margin = new Thickness(0, LunetTokens.Space.Xs, 0, 0) });
        Content = root;
    }

    /// <summary>The rows shown now (tests run them the way a tap does).</summary>
    public IReadOnlyList<PaletteItem> Items { get; private set; } = [];

    /// <summary>Opens the palette with a query (e.g. "@" for the outline); keeps the editor's caret.</summary>
    public void Open(string query = "")
    {
        _results = null; _input = null; _resultsTitle = null; _quickOpen = false;
        _query.PlaceholderText = "Comando, @símbolo, :linha, #no jogo, /buscar";
        _query.Text = query;
        _query.CaretIndex = query.Length;
        _ = RefreshAsync();
    }

    /// <summary>
    /// Quick Open (VS Code's Ctrl+P): every file of the game by name, fuzzy, the open tabs first; a tap opens it where it
    /// belongs (code as a tab, art in its Studio).
    /// </summary>
    private bool _quickOpen;

    public void OpenFiles()
    {
        _input = null;
        var tabs = _shell.WorkspaceView.Code.OpenTabs;
        var files = _shell.Workspace.Project?.ListFiles() ?? [];
        _results = files.OrderBy(f => { var i = tabs.ToList().IndexOf(f); return i < 0 ? int.MaxValue : i; })
            .Select(f => new PaletteItem(f, tabs.Contains(f) ? "Aberto" : "", () =>
            {
                _shell.State.ShowPanel(SupportPanel.None);
                _shell.WorkspaceView.OpenFile(f);
            }, "file:" + f)).ToList();
        _resultsTitle = "Ir para arquivo";
        _quickOpen = true;
        _query.PlaceholderText = "Nome do arquivo";
        _query.Text = "";
        _ = RefreshAsync();
    }

    public void SetQuery(string query) { _query.Text = query; _query.CaretIndex = query.Length; }

    /// <summary>Runs an item, remembering it as recent, and closes the palette unless it opened a list of its own.</summary>
    public void Run(PaletteItem item)
    {
        var key = item.Key ?? item.Title;
        _recents.Remove(key);
        _recents.Insert(0, key);
        item.Run();
    }

    private CodeSurface? Code => _shell.WorkspaceView.Code.Surface as CodeSurface;
    private string? Path => _shell.WorkspaceView.Code.OpenPath;

    private void Close(bool focusCode = true)
    {
        _shell.State.ShowPanel(SupportPanel.None);
        if (focusCode && Code is { } code) { _shell.State.SelectTab(WorkspaceTab.Code); code.Focus(); }
    }

    private async Task SubmitAsync()
    {
        if (_input is { } input) { await input(_query.Text ?? ""); return; }
        await RefreshAsync();
        if (Items.Count > 0) Run(Items[0]);
    }

    public async Task RefreshAsync()
    {
        if (_input is not null) { Show([]); return; }
        // In Quick Open, a VS Code prefix (> @ : # /) switches to commands, the outline, a line, symbols or find.
        if (_quickOpen && (_query.Text ?? "") is { Length: > 0 } typed && ">@:#/".Contains(typed[0])) { _results = null; _resultsTitle = null; _quickOpen = false; }
        if (_results is not null) { Show(CommandPalette.Rank(_results, (_query.Text ?? "").Trim(), [])); return; }
        var (mode, term) = CommandPalette.Parse(_query.Text ?? "");
        IReadOnlyList<PaletteItem> items = mode switch
        {
            PaletteMode.Commands => CommandPalette.Rank(Commands(), term, _recents),
            PaletteMode.Line => LineItems(term),
            PaletteMode.Find => FindItems(term),
            PaletteMode.Outline => CommandPalette.Rank(await OutlineItems(), term, []),
            PaletteMode.Symbols => await SymbolItems(term),
            _ => [],
        };
        Show(items);
    }

    private void Show(IReadOnlyList<PaletteItem> items)
    {
        Items = items;
        _list.Children.Clear();
        if (_resultsTitle is { } title) _list.Children.Add(LunetUi.Text(title, LunetTokens.Type.Caption, LunetTokens.Color.Primary, FontWeight.SemiBold));
        foreach (var item in items.Take(60))
        {
            var body = new StackPanel { Spacing = 1 };
            body.Children.Add(LunetUi.Text(item.Title, LunetTokens.Type.Callout));
            if (item.Detail.Length > 0)
            {
                var detail = LunetUi.Text(item.Detail, LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
                detail.TextWrapping = TextWrapping.NoWrap; detail.TextTrimming = TextTrimming.CharacterEllipsis;
                body.Children.Add(detail);
            }
            var row = new Button
            {
                Content = body, MinHeight = LunetTokens.MinTouch, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xs),
                Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
            };
            row.Classes.Add("lunet-ghost");
            AutomationProperties.SetName(row, item.Title);
            var captured = item;
            row.Click += (_, _) => Run(captured);
            _list.Children.Add(row);
        }
        if (items.Count == 0 && _input is null)
            _list.Children.Add(LunetUi.Text("Nada encontrado.", LunetTokens.Type.Label, LunetTokens.Color.TextSecondary));
    }

    // ---------------- commands ----------------

    private IEnumerable<PaletteItem> Commands()
    {
        var hasCode = Code is not null && Path is not null;
        if (_shell.Workspace.Project is not null)
            yield return new("Buscar no jogo…", "Texto em todos os arquivos, com substituir (Ctrl+Shift+F)", () => { Close(); _shell.WorkspaceView.ShowSearch(); });
        if (hasCode)
        {
            yield return new("Formatar documento", "Arruma indentação e espaços do arquivo", () => { EditWhole(CodeFormatter.Format(Code!.Text)); Close(); });
            yield return new("Ir para definição", "Onde o símbolo sob o cursor foi declarado", () => _ = GoToDefinition());
            yield return new("Documentação do símbolo", "A página do que está sob o cursor, sem internet", () => _ = SymbolDocs());
            yield return new("Encontrar referências", "Todos os usos do símbolo sob o cursor", () => _ = References());
            yield return new("Renomear símbolo…", "Em todos os arquivos do jogo", AskRename);
            yield return new("Correções rápidas", "Sugestões para o erro sob o cursor", () => _ = QuickFixes());
            yield return new("Estrutura do arquivo", "Classes e métodos (@)", () => SetQuery("@"));
            yield return new("Ir para a linha…", "Digite o número (:)", () => SetQuery(":"));
            yield return new("Buscar no arquivo…", "Texto no código aberto (/)", () => SetQuery("/"));
            yield return new("Símbolos do jogo…", "Classes e métodos de todos os arquivos (#)", () => SetQuery("#"));
            yield return new("Comentar ou descomentar linhas", "// nas linhas selecionadas", () => Line(LineOperations.ToggleComment));
            yield return new("Duplicar linha", "", () => Line(LineOperations.DuplicateLines));
            yield return new("Apagar linha", "", () => Line(LineOperations.DeleteLines));
            yield return new("Mover linha para cima", "", () => Line((t, a, b) => LineOperations.MoveLines(t, a, b, up: true)));
            yield return new("Mover linha para baixo", "", () => Line((t, a, b) => LineOperations.MoveLines(t, a, b, up: false)));
            yield return new("Inserir bloco", "{ } com a linha recuada dentro (Ctrl+Shift+B)", () => Line(IndentationService.InsertBlock));
            yield return new("Selecionar próxima ocorrência", "A palavra sob o cursor, depois a próxima igual", () => { Close(); Code!.SelectNextOccurrence(); });
            yield return new("Editar todas as ocorrências", "Troca a palavra sob o cursor em todo o arquivo", () => { Close(); _shell.WorkspaceView.Code.EditAllOccurrences(); });
            yield return new("Dobrar ou desdobrar região", "O bloco onde está o cursor", () => { Close(); Code!.ToggleFoldAtCaret(); });
            yield return new("Dobrar tudo", "", () => { Close(); Code!.SetAllFolded(true); });
            yield return new("Desdobrar tudo", "", () => { Close(); Code!.SetAllFolded(false); });
            yield return new("Voltar à posição anterior", "Antes do último salto (Alt+←)", () => { Close(); _shell.WorkspaceView.Code.BackToPosition(); });
            yield return new("Avançar à próxima posição", "Desfaz o último voltar (Alt+→)", () => { Close(); _shell.WorkspaceView.Code.ForwardToPosition(); });
            yield return new("Indentar", "", () => Line(LineOperations.Indent));
            yield return new("Recuar", "", () => Line(LineOperations.Outdent));
        }
        yield return new("Rodar o jogo", "F5", () => { Close(false); _shell.RunGame(); });
        yield return new("Parar o jogo", "", () => { Close(false); _shell.StopGame(); });
        yield return new("Documentação", "Manual, guias e referência da API", () => _shell.State.ShowPanel(SupportPanel.Docs));
        yield return new("Console", "O que o jogo escreveu", () => _shell.State.ShowPanel(SupportPanel.Console));
        yield return new("Problemas", "Erros e avisos do código", () => _shell.State.ShowPanel(SupportPanel.Problems));
    }

    private void EditWhole(string newText)
    {
        var code = Code!;
        var caret = Math.Min(code.Caret, newText.Length);
        if (newText != code.Text) code.Replace(0, code.Text.Length, newText, caret);
    }

    private void Line(Func<string, int, int, EditResult?> op)
    {
        var code = Code!;
        if (op(code.Text, code.SelectionStart, code.SelectionEnd) is { } r)
        {
            code.Replace(r.Start, r.Start + r.DeleteLength, r.Insert, r.SelectionEnd);
            code.Select(r.SelectionStart, r.SelectionEnd);
        }
        Close();
    }

    private void Line(Func<string, int, int, EditResult> op) => Line((t, a, b) => (EditResult?)op(t, a, b));

    /// <summary>Runs on the analyzer with the editor's current text (the same snapshot the person sees).</summary>
    private Task<T> Analyze<T>(Func<CodeAnalyzer, T> action)
    {
        var assistant = _shell.Assistant!;
        assistant.SetFile(Path!, Code!.Text);
        return assistant.RunAsync(action);
    }

    private async Task GoToDefinition()
    {
        if (_shell.Assistant is null || Code is not { } code) return;
        var caret = code.Caret;
        var path = Path!;
        var location = await Analyze(a => a.GetDefinition(path, caret));
        if (location is null) { ShowResults("Definição", []); return; }
        Close();
        OpenAt(location.FilePath, location.Line, location.Column);
    }

    /// <summary>Opens the documentation of the symbol under the caret (the API's own page, or a search by its name).</summary>
    private async Task SymbolDocs()
    {
        if (_shell.Assistant is null || Code is not { } code) return;
        var caret = code.Caret;
        var path = Path!;
        var hover = await Analyze(a => a.GetHover(path, caret) ?? (caret > 0 ? a.GetHover(path, caret - 1) : null));
        if (hover is null) { ShowResults("Nenhum símbolo sob o cursor", []); return; }
        var name = code.Text.Substring(hover.Start, hover.Length);
        Close(false);
        _shell.State.ShowPanel(SupportPanel.Docs);
        _shell.WorkspaceView.Support.Docs.OpenSymbol(hover.DocumentationId, name);
    }

    private async Task References()
    {
        if (_shell.Assistant is null || Code is not { } code) return;
        var caret = code.Caret;
        var path = Path!;
        var found = await Analyze(a => a.FindReferences(path, caret));
        ShowResults($"{found.Count} referência(s)", found.Select(l => new PaletteItem($"{l.FilePath}:{l.Line}", LineText(l.FilePath, l.Line),
            () => { Close(); OpenAt(l.FilePath, l.Line, l.Column); })).ToArray());
    }

    private async Task QuickFixes()
    {
        if (_shell.Assistant is null || Code is not { } code) return;
        var caret = code.Caret;
        var path = Path!;
        var fixes = await Analyze(a => a.GetQuickFixes(path, caret));
        ShowResults(fixes.Count == 0 ? "Nenhuma correção para este ponto" : "Correções rápidas", fixes.Select(f => new PaletteItem(f.Title, f.Path,
            () => { ApplyEdits([new FileEdits(f.Path, f.Changes)]); Close(); })).ToArray());
    }

    private void AskRename()
    {
        _results = null;
        _resultsTitle = "Renomear: digite o novo nome e toque em Enter";
        _query.Text = "";
        _query.PlaceholderText = "Novo nome";
        _input = async name =>
        {
            if (_shell.Assistant is null || Code is not { } code) return;
            var caret = code.Caret;
            var path = Path!;
            var result = await Analyze(a => a.Rename(path, caret, name.Trim()));
            _input = null;
            if (!result.Success) { ShowResults(result.Error ?? "Não foi possível renomear.", []); return; }
            ApplyEdits(result.Edits);
            _shell.Report($"Renomeado para {name.Trim()} em {result.Occurrences} lugar(es).");
            Close();
        };
        Show([]);
    }

    /// <summary>Applies analyzer edits: the open file as one undo step, other files written to disk (verified by the store).</summary>
    private void ApplyEdits(IEnumerable<FileEdits> edits)
    {
        foreach (var e in edits)
        {
            if (e.Path == Path && Code is { } code) EditWhole(TextChanges.Apply(code.Text, e.Changes));
            else
            {
                var updated = TextChanges.Apply(_shell.Workspace.Read(e.Path), e.Changes);
                _shell.Workspace.Write(e.Path, updated);
                _shell.Assistant?.SetFile(e.Path, updated); // the analyzer must see the file as it is now on disk
            }
        }
        _shell.Assistant?.SetFile(Path!, Code!.Text);
    }

    private void ShowResults(string title, IReadOnlyList<PaletteItem> items)
    {
        _resultsTitle = title;
        _results = items;
        _query.Text = "";
        Show(items);
    }

    private void OpenAt(string? file, int line, int column)
    {
        _shell.State.SelectTab(WorkspaceTab.Code);
        _shell.WorkspaceView.Code.JumpTo(file, line, column);
    }

    private string LineText(string? file, int line)
    {
        try
        {
            var text = file is null || file == Path ? Code?.Text ?? "" : _shell.Workspace.Read(file);
            var lines = text.Split('\n');
            return line >= 1 && line <= lines.Length ? lines[line - 1].Trim() : "";
        }
        catch (IOException) { return ""; }
    }

    // ---------------- other modes ----------------

    private IReadOnlyList<PaletteItem> LineItems(string term) =>
        CommandPalette.Line(term) is { } line && Code is not null
            ? [new($"Ir para a linha {line}", LineText(Path, line), () => { Close(); _shell.WorkspaceView.Code.JumpTo(null, line, 1); })]
            : [];

    private IReadOnlyList<PaletteItem> FindItems(string term)
    {
        if (term.Length == 0 || Code is not { } code) return [];
        var text = code.Text;
        return FindReplace.FindAll(text, term).Take(200).Select(m =>
        {
            int line = text.AsSpan(0, m.Start).Count('\n') + 1;
            return new PaletteItem($"Linha {line}", LineText(Path, line), () => { Close(); code.Select(m.Start, m.Start + m.Length); });
        }).ToArray();
    }

    private async Task<IReadOnlyList<PaletteItem>> OutlineItems()
    {
        if (_shell.Assistant is null || Path is not { } path || Code is null) return [];
        var outline = await Analyze(a => a.GetOutline(path));
        var items = new List<PaletteItem>();
        void Walk(IEnumerable<OutlineItem> nodes, string indent)
        {
            foreach (var n in nodes)
            {
                var node = n;
                items.Add(new(indent + n.Name, $"{n.Kind} · linha {n.Line}", () => { Close(); _shell.WorkspaceView.Code.JumpTo(null, node.Line, 1); }));
                Walk(n.Children, indent + "  ");
            }
        }
        Walk(outline, "");
        return items;
    }

    private async Task<IReadOnlyList<PaletteItem>> SymbolItems(string term)
    {
        if (_shell.Assistant is null || Path is null || Code is null || term.Length == 0) return [];
        var found = await Analyze(a => a.FindProjectSymbols(term));
        return found.Select(s => new PaletteItem(s.Display, $"{s.Location.FilePath}:{s.Location.Line}",
            () => { Close(); OpenAt(s.Location.FilePath, s.Location.Line, s.Location.Column); })).ToArray();
    }
}
