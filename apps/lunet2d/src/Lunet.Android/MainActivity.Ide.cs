using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Android.Widget;
using Lunet.Android.Editor;
using Lunet.Compiler;
using Lunet.Core;
using Lunet.Editor;

namespace Lunet.Android;

/// <summary>Recursos de IDE da tela do workspace: menus, comandos e atalhos, refatorações, configurações e exportação de logs.</summary>
public sealed partial class MainActivity
{
    private SettingsStore _settingsStore = null!;
    private EditorSettings _settings = new();
    private MinimapView? _minimap;

    // ---------- Configurações ----------

    private void LoadSettings()
    {
        _settingsStore = new SettingsStore(System.IO.Path.Combine(FilesDir!.AbsolutePath, "settings.json"));
        _settings = _settingsStore.Load();
        ApplyWindowSettings();
        LoadLayouts();
    }

    private void ApplyWindowSettings()
    {
        if (_settings.KeepScreenOn) Window?.AddFlags(WindowManagerFlags.KeepScreenOn);
        else Window?.ClearFlags(WindowManagerFlags.KeepScreenOn);
    }

    private void ApplyEditorSettings()
    {
        _editor?.ApplySettings(_settings.FontSize, _settings.ShowLineNumbers);
        if (_minimap is not null) _minimap.Visibility = _settings.ShowMinimap ? ViewStates.Visible : ViewStates.Gone;
    }

    private View BuildEditorHost()
    {
        var editor = new CodeEditText(this);
        editor.Settled += OnEditorSettled;
        editor.CommandRequested += HandleCommand;
        _editor = editor;
        _minimap = new MinimapView(this, editor);
        var frame = new FrameLayout(this);
        frame.AddView(editor, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        frame.AddView(_minimap, new FrameLayout.LayoutParams(Dp(44), ViewGroup.LayoutParams.MatchParent, GravityFlags.Right));
        ApplyEditorSettings();
        return frame;
    }

    private void ShowSettings()
    {
        var draft = _settings.Clone();
        var form = Vertical();
        form.SetPadding(Dp(16), Dp(8), Dp(16), 0);

        var fontLabel = new TextView(this) { TextSize = 14 };
        var fontSlider = new SeekBar(this) { Max = EditorSettings.MaxFontSize - EditorSettings.MinFontSize, Progress = draft.FontSize - EditorSettings.MinFontSize };
        void UpdateLabel() => fontLabel.Text = $"Tamanho da fonte do editor: {draft.FontSize}";
        UpdateLabel();
        fontSlider.ProgressChanged += (_, args) =>
        {
            draft.FontSize = args.Progress + EditorSettings.MinFontSize;
            UpdateLabel();
        };
        form.AddView(fontLabel);
        form.AddView(fontSlider);

        CheckBox Toggle(string label, bool value, Action<bool> set)
        {
            var box = new CheckBox(this) { Text = label, Checked = value };
            box.CheckedChange += (_, args) => set(args.IsChecked);
            form.AddView(box);
            return box;
        }
        Toggle("Números de linha", draft.ShowLineNumbers, v => draft.ShowLineNumbers = v);
        Toggle("Minimapa do código", draft.ShowMinimap, v => draft.ShowMinimap = v);
        Toggle("Formatar o código ao executar", draft.FormatOnRun, v => draft.FormatOnRun = v);
        Toggle("Manter a tela ligada", draft.KeepScreenOn, v => draft.KeepScreenOn = v);
        Toggle("Taxa de atualização alta no Preview (90/120 Hz)", draft.HighRefreshRate, v => draft.HighRefreshRate = v);

        var scroll = new ScrollView(this);
        scroll.AddView(form);
        new AlertDialog.Builder(this)!
            .SetTitle("Configurações")!
            .SetView(scroll)!
            .SetNegativeButton("Cancelar", (_, _) => { })!
            .SetPositiveButton("Salvar", (_, _) =>
            {
                _settings = draft.Normalized();
                try { _settingsStore.Save(_settings); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(this, "Não foi possível salvar as configurações: " + ex.Message, ToastLength.Long)?.Show();
                }
                ApplyEditorSettings();
                ApplyWindowSettings();
            })!.Show();
    }

    // ---------- Menus ----------

    private void ShowMenu() => ShowActionMenu("Menu",
    [
        ("Arquivo", ShowFileMenu),
        ("Navegar", ShowNavigateMenu),
        ("Símbolo", ShowSymbolMenu),
        ("Edição", ShowEditMenu),
        ("Ferramentas", ShowToolsMenu),
    ]);

    private void ShowActionMenu(string title, IReadOnlyList<(string Label, Action Action)> items) =>
        new AlertDialog.Builder(this)!.SetTitle(title)!
            .SetItems(items.Select(i => i.Label).ToArray(), (_, args) => items[args.Which].Action())!.Show();

    private void ShowFileMenu() => ShowActionMenu("Arquivo",
    [
        ("Salvar", () => HandleCommand(EditorCommand.Save)),
        ("Formatar documento", () => HandleCommand(EditorCommand.Format)),
        ("Exportar projeto (ZIP)", () => ExportProject()),
        ("Importar imagem PNG", ImportImage),
    ]);

    private void ShowNavigateMenu() => ShowActionMenu("Navegar",
    [
        ("Estrutura do arquivo", () => HandleCommand(EditorCommand.Outline)),
        ("Ir para definição", () => HandleCommand(EditorCommand.GoToDefinition)),
        ("Referências do símbolo", ShowReferences),
        ("Buscar no projeto", ShowProjectSearch),
        ("Localizar e substituir", ShowFind),
    ]);

    private void ShowSymbolMenu() => ShowActionMenu("Símbolo",
    [
        ("Dica do símbolo", ShowHover),
        ("Informações do símbolo", () => HandleCommand(EditorCommand.ShowSymbolInfo)),
        ("Documentação do símbolo", ExplainSymbol),
        ("Renomear símbolo", () => HandleCommand(EditorCommand.Rename)),
        ("Correções rápidas", () => HandleCommand(EditorCommand.QuickFix)),
    ]);

    private void ShowEditMenu() => ShowActionMenu("Edição",
    [
        ("Selecionar próxima ocorrência", () => HandleCommand(EditorCommand.SelectNextOccurrence)),
        ("Selecionar todas as ocorrências", () => HandleCommand(EditorCommand.SelectAllOccurrences)),
        ("Adicionar cursor na linha acima", () => _editor?.AddCursor(above: true)),
        ("Adicionar cursor na linha abaixo", () => _editor?.AddCursor(above: false)),
        ("Limpar cursores extras", () => _editor?.ClearExtraCursors()),
        ("Duplicar linha", () => HandleCommand(EditorCommand.DuplicateLine)),
        ("Apagar linha", () => HandleCommand(EditorCommand.DeleteLine)),
        ("Mover linha para cima", () => HandleCommand(EditorCommand.MoveLineUp)),
        ("Mover linha para baixo", () => HandleCommand(EditorCommand.MoveLineDown)),
        ("Comentar / descomentar", () => HandleCommand(EditorCommand.ToggleComment)),
        ("Indentar", () => HandleCommand(EditorCommand.Indent)),
        ("Recuar", () => HandleCommand(EditorCommand.Outdent)),
        ("Dobrar / desdobrar região", () => HandleCommand(EditorCommand.ToggleFold)),
        ("Desdobrar tudo", () => _editor?.UnfoldAll()),
    ]);

    private void ShowToolsMenu() => ShowActionMenu("Ferramentas",
    [
        ("Documentação", () => HandleCommand(EditorCommand.Documentation)),
        ("Exportar logs (Console e Problemas)", ExportLogs),
        ("Layout do workspace", ShowLayoutDialog),
        ("Configurações", ShowSettings),
        ("Atalhos de teclado", ShowShortcutHelp),
    ]);

    // ---------- Inspector e mudanças ----------

    private void ToggleInspector()
    {
        if (_inspector is null) return;
        _inspector.Visibility = _inspector.Visibility == ViewStates.Visible ? ViewStates.Gone : ViewStates.Visible;
    }

    /// <summary>Texto curto sobre o que mudou desde o Run anterior (hot reload possível × reinício necessário).</summary>
    private static string DescribeChange(ChangeReport? report, bool fromCache)
    {
        if (report is null) return "";
        var suffix = fromCache ? " (compilação reaproveitada)" : "";
        return report.Kind switch
        {
            ChangeKind.None => " · sem mudanças de código" + suffix,
            ChangeKind.HotReloadPossible => $" · só corpos mudaram ({report.Reasons.Count}): hot reload possível, jogo reiniciado",
            _ => $" · reinício necessário: {report.Reasons[0]}" + (report.Reasons.Count > 1 ? $" (+{report.Reasons.Count - 1})" : ""),
        };
    }

    // ---------- Comandos ----------

    private void HandleCommand(EditorCommand command)
    {
        if (_editor is null) return;
        switch (command)
        {
            case EditorCommand.Save:
                SaveCurrent();
                Toast.MakeText(this, "Salvo", ToastLength.Short)?.Show();
                break;
            case EditorCommand.Run: Run(); break;
            case EditorCommand.Find: ShowFind(); break;
            case EditorCommand.FindInProject: ShowProjectSearch(); break;
            case EditorCommand.Undo: _editor.Undo(); break;
            case EditorCommand.Redo: _editor.Redo(); break;
            case EditorCommand.Format: FormatDocument(silent: false); break;
            case EditorCommand.DuplicateLine: EditLines((t, a, b) => LineOperations.DuplicateLines(t, a, b)); break;
            case EditorCommand.DeleteLine: EditLines((t, a, b) => LineOperations.DeleteLines(t, a, b)); break;
            case EditorCommand.MoveLineUp: EditLines((t, a, b) => LineOperations.MoveLines(t, a, b, up: true)); break;
            case EditorCommand.MoveLineDown: EditLines((t, a, b) => LineOperations.MoveLines(t, a, b, up: false)); break;
            case EditorCommand.ToggleComment: EditLines((t, a, b) => LineOperations.ToggleComment(t, a, b)); break;
            case EditorCommand.Indent: EditLines((t, a, b) => LineOperations.Indent(t, a, b)); break;
            case EditorCommand.Outdent: EditLines((t, a, b) => LineOperations.Outdent(t, a, b)); break;
            case EditorCommand.GoToDefinition: GoToDefinition(); break;
            case EditorCommand.Documentation: ExplainSymbol(); break;
            case EditorCommand.Rename: AskRename(); break;
            case EditorCommand.QuickFix: ShowQuickFixes(); break;
            case EditorCommand.Outline: ShowOutline(); break;
            case EditorCommand.SelectNextOccurrence: _editor.SelectNextOccurrence(); break;
            case EditorCommand.SelectAllOccurrences: _editor.SelectAllOccurrences(); break;
            case EditorCommand.ToggleFold:
                if (!_editor.ToggleFoldAtCaret()) Toast.MakeText(this, "Nenhuma região para dobrar aqui.", ToastLength.Short)?.Show();
                break;
            case EditorCommand.ToggleExplorer: ToggleExplorer(); break;
            case EditorCommand.ShowSymbolInfo: ShowSymbolInfo(); break;
        }
    }

    private void EditLines(Func<string, int, int, EditResult?> operation)
    {
        if (_editor is null) return;
        var start = Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
        var end = Math.Max(_editor.SelectionStart, _editor.SelectionEnd);
        if (operation(_editor.Text ?? "", start, end) is { } edit) _editor.ApplyEdit(edit);
    }

    private bool IsCSharpOpen => _openFile?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true;

    // ---------- Formatação ----------

    private void FormatDocument(bool silent)
    {
        if (_editor is null) return;
        if (!IsCSharpOpen)
        {
            if (!silent) Toast.MakeText(this, "Formatar funciona em arquivos .cs.", ToastLength.Short)?.Show();
            return;
        }
        var text = _editor.Text ?? "";
        var formatted = CodeFormatter.Format(text);
        if (formatted == text)
        {
            if (!silent) Toast.MakeText(this, "O código já está formatado (ou tem chaves sem par).", ToastLength.Short)?.Show();
            return;
        }
        _editor.ReplaceWholeText(formatted, MapCaret(text, formatted, _editor.SelectionStart));
        if (!silent) Toast.MakeText(this, "Código formatado", ToastLength.Short)?.Show();
    }

    /// <summary>Mantém o cursor na mesma linha e no mesmo trecho do texto depois de reindentar.</summary>
    private static int MapCaret(string before, string after, int caret)
    {
        caret = Math.Clamp(caret, 0, before.Length);
        var line = 0;
        var lineStart = 0;
        for (var i = 0; i < caret; i++)
            if (before[i] == '\n') { line++; lineStart = i + 1; }
        var oldIndent = 0;
        while (lineStart + oldIndent < before.Length && before[lineStart + oldIndent] is ' ' or '\t') oldIndent++;
        var column = Math.Max(0, caret - lineStart - oldIndent);

        var newStart = 0;
        for (var current = 0; current < line && newStart < after.Length; current++)
        {
            var next = after.IndexOf('\n', newStart);
            if (next < 0) return after.Length;
            newStart = next + 1;
        }
        var newIndent = 0;
        while (newStart + newIndent < after.Length && after[newStart + newIndent] == ' ') newIndent++;
        var lineEnd = after.IndexOf('\n', newStart);
        if (lineEnd < 0) lineEnd = after.Length;
        return Math.Min(lineEnd, newStart + newIndent + column);
    }

    // ---------- Renomear, correções rápidas, estrutura e símbolo ----------

    private void AskRename()
    {
        if (_editor is null || _openFile is null || _assistant is null || !IsCSharpOpen) return;
        var path = _openFile;
        var caret = _editor.SelectionStart;
        var text = _editor.Text ?? "";
        _assistant.RunAsync(a =>
        {
            a.SetFile(path, text);
            return a.GetHover(path, caret);
        }).ContinueWith(task => RunOnUiThread(() =>
        {
            var hover = task.IsFaulted ? null : task.Result;
            if (hover is null)
            {
                Toast.MakeText(this, "Toque sobre o nome que deseja renomear.", ToastLength.Long)?.Show();
                return;
            }
            var current = text.Substring(hover.Start, hover.Length);
            var input = new EditText(this) { Text = current };
            input.SetSingleLine(true);
            input.SetSelection(0, current.Length);
            var holder = Vertical();
            holder.SetPadding(Dp(16), Dp(8), Dp(16), 0);
            holder.AddView(input);
            new AlertDialog.Builder(this)!
                .SetTitle($"Renomear \"{current}\"")!
                .SetView(holder)!
                .SetNegativeButton("Cancelar", (_, _) => { })!
                .SetPositiveButton("Renomear", (_, _) => DoRename(path, hover.Start, (input.Text ?? "").Trim()))!.Show();
        }));
    }

    private void DoRename(string path, int position, string newName)
    {
        if (_assistant is null || _project is null || _editor is null) return;
        var text = _editor.Text ?? "";
        _assistant.RunAsync(a =>
        {
            a.SetFile(path, text);
            return a.Rename(path, position, newName);
        }).ContinueWith(task => RunOnUiThread(() =>
        {
            if (task.IsFaulted || _project is null || _editor is null || _openFile != path) return;
            var result = task.Result;
            if (!result.Success)
            {
                new AlertDialog.Builder(this)!.SetTitle("Não foi possível renomear")!.SetMessage(result.Error)!.SetPositiveButton("Ok", (_, _) => { })!.Show();
                return;
            }
            foreach (var file in result.Edits)
            {
                if (file.Path == path)
                {
                    _editor.ReplaceWholeText(TextChanges.Apply(_editor.Text ?? "", file.Changes));
                    continue;
                }
                try
                {
                    var updated = TextChanges.Apply(_project.ReadText(file.Path), file.Changes);
                    _project.WriteText(file.Path, updated);
                    _assistant.SetFile(file.Path, updated);
                }
                catch (Exception ex) when (ex is ProjectException or IOException or UnauthorizedAccessException)
                {
                    Toast.MakeText(this, $"Não foi possível atualizar {file.Path}: {ex.Message}", ToastLength.Long)?.Show();
                }
            }
            Toast.MakeText(this, $"{result.Occurrences} ocorrência(s) renomeada(s) em {result.Edits.Count} arquivo(s)", ToastLength.Long)?.Show();
        }));
    }

    private void ShowQuickFixes()
    {
        if (_editor is null || _openFile is null || _assistant is null || !IsCSharpOpen) return;
        var path = _openFile;
        var caret = _editor.SelectionStart;
        var text = _editor.Text ?? "";
        _assistant.RunAsync(a =>
        {
            a.SetFile(path, text);
            return a.GetQuickFixes(path, caret);
        }).ContinueWith(task => RunOnUiThread(() =>
        {
            var fixes = task.IsFaulted ? [] : task.Result;
            if (fixes.Count == 0 || _editor is null || _openFile != path)
            {
                Toast.MakeText(this, "Nenhuma correção sugerida para esta linha.", ToastLength.Short)?.Show();
                return;
            }
            new AlertDialog.Builder(this)!
                .SetTitle("Correções rápidas")!
                .SetItems(fixes.Select(f => f.Title).ToArray(), (_, args) =>
                    _editor.ReplaceWholeText(TextChanges.Apply(_editor.Text ?? "", fixes[args.Which].Changes)))!.Show();
        }));
    }

    private void ShowOutline()
    {
        if (_editor is null || _openFile is null || _assistant is null || !IsCSharpOpen) return;
        var path = _openFile;
        var text = _editor.Text ?? "";
        _assistant.RunAsync(a =>
        {
            a.SetFile(path, text);
            return a.GetOutline(path);
        }).ContinueWith(task => RunOnUiThread(() =>
        {
            var outline = task.IsFaulted ? [] : task.Result;
            var flat = new List<(int Depth, OutlineItem Item)>();
            void Walk(IEnumerable<OutlineItem> items, int depth)
            {
                foreach (var item in items)
                {
                    flat.Add((depth, item));
                    Walk(item.Children, depth + 1);
                }
            }
            Walk(outline, 0);
            if (flat.Count == 0)
            {
                Toast.MakeText(this, "Nada para mostrar na estrutura.", ToastLength.Short)?.Show();
                return;
            }
            var labels = flat.Select(f => $"{new string(' ', f.Depth * 2)}{OutlineGlyph(f.Item.Kind)} {f.Item.Name}{(f.Item.Detail.Length > 0 ? " " + f.Item.Detail : "")}   :{f.Item.Line}").ToArray();
            new AlertDialog.Builder(this)!
                .SetTitle("Estrutura do arquivo")!
                .SetItems(labels, (_, args) => Jump(path, flat[args.Which].Item.Line, 1))!.Show();
        }));
    }

    private static string OutlineGlyph(string kind) => kind switch
    {
        "namespace" => "▣",
        "class" or "record" => "C",
        "struct" or "record struct" => "S",
        "interface" => "I",
        "enum" => "E",
        "method" => "ƒ",
        "constructor" => "◆",
        "property" => "P",
        "field" => "▪",
        "event" => "⚡",
        _ => "•",
    };

    private void ShowSymbolInfo()
    {
        if (_editor is null || _openFile is null || _assistant is null || !IsCSharpOpen) return;
        var path = _openFile;
        var caret = _editor.SelectionStart;
        var text = _editor.Text ?? "";
        _assistant.RunAsync(a =>
        {
            a.SetFile(path, text);
            return a.GetSymbolDetails(path, caret);
        }).ContinueWith(task => RunOnUiThread(() =>
        {
            var details = task.IsFaulted ? null : task.Result;
            if (details is null)
            {
                Toast.MakeText(this, "Toque sobre um nome no código.", ToastLength.Short)?.Show();
                return;
            }
            var lines = new List<string> { details.Signature, "", $"Tipo de símbolo: {details.Kind}", $"Acesso: {details.Accessibility}" };
            if (details.Modifiers.Count > 0) lines.Add("Modificadores: " + string.Join(", ", details.Modifiers));
            if (details.Type is not null) lines.Add("Tipo/retorno: " + details.Type);
            if (details.ContainingType is not null) lines.Add("Declarado em: " + details.ContainingType);
            if (details.Namespace is not null) lines.Add("Namespace: " + details.Namespace);
            if (details.BaseType is not null) lines.Add("Herda de: " + details.BaseType);
            if (details.Interfaces.Count > 0) lines.Add("Implementa: " + string.Join(", ", details.Interfaces));
            if (details.Attributes.Count > 0) lines.Add("Atributos: " + string.Join(", ", details.Attributes));
            lines.Add(details.IsFromSource ? "Origem: código do projeto" : "Origem: framework/sistema");
            if (details.Members.Count > 0) lines.Add("\nMembros públicos:\n  " + string.Join("\n  ", details.Members));
            var message = new TextView(this) { Text = string.Join('\n', lines), TextSize = 13 };
            message.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
            message.SetTextIsSelectable(true);
            var scroll = new ScrollView(this);
            scroll.AddView(message);
            var dialog = new AlertDialog.Builder(this)!.SetTitle(details.Name)!.SetView(scroll)!.SetPositiveButton("Fechar", (_, _) => { })!;
            if (details.DocumentationId is { } id)
                dialog.SetNeutralButton("Documentação", (_, _) =>
                {
                    var target = DocumentationPanel.TargetFor(this, id, details.Name);
                    if (target is not null) DocumentationPanel.Open(this, target);
                });
            dialog.Show();
        }));
    }

    // ---------- Logs e atalhos ----------

    private void ExportLogs()
    {
        var version = "dev";
        try { version = PackageManager?.GetPackageInfo(PackageName!, 0)?.VersionName ?? "dev"; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { }
        var device = $"{Build.Manufacturer} {Build.Model}, Android {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt})";
        var problems = _problems.Select(p => new LogProblem(p.Severity.ToString().ToLowerInvariant(), p.Id, p.Message, p.FilePath, p.Line, p.Column));
        var text = LogExport.Build(_project?.Name ?? "(sem projeto)", version, device, DateTimeOffset.Now, _console, problems);
        var intent = new Intent(Intent.ActionSend);
        intent.SetType("text/plain");
        intent.PutExtra(Intent.ExtraSubject, "Logs do Lunet");
        intent.PutExtra(Intent.ExtraText, text);
        StartActivity(Intent.CreateChooser(intent, "Exportar logs"));
    }

    private static string CommandLabel(EditorCommand command) => command switch
    {
        EditorCommand.Save => "Salvar",
        EditorCommand.Run => "Executar",
        EditorCommand.Find => "Localizar e substituir",
        EditorCommand.FindInProject => "Buscar no projeto",
        EditorCommand.Undo => "Desfazer",
        EditorCommand.Redo => "Refazer",
        EditorCommand.Format => "Formatar documento",
        EditorCommand.DuplicateLine => "Duplicar linha",
        EditorCommand.DeleteLine => "Apagar linha",
        EditorCommand.MoveLineUp => "Mover linha para cima",
        EditorCommand.MoveLineDown => "Mover linha para baixo",
        EditorCommand.ToggleComment => "Comentar / descomentar",
        EditorCommand.Indent => "Indentar",
        EditorCommand.Outdent => "Recuar",
        EditorCommand.GoToDefinition => "Ir para definição",
        EditorCommand.Documentation => "Documentação do símbolo",
        EditorCommand.Rename => "Renomear símbolo",
        EditorCommand.QuickFix => "Correções rápidas",
        EditorCommand.Outline => "Estrutura do arquivo",
        EditorCommand.SelectNextOccurrence => "Selecionar próxima ocorrência",
        EditorCommand.SelectAllOccurrences => "Selecionar todas as ocorrências",
        EditorCommand.ToggleFold => "Dobrar / desdobrar região",
        EditorCommand.ToggleExplorer => "Mostrar / esconder Explorer",
        EditorCommand.ShowSymbolInfo => "Informações do símbolo",
        _ => command.ToString(),
    };

    private void ShowShortcutHelp()
    {
        var lines = Shortcuts.All().OrderBy(s => CommandLabel(s.Command), StringComparer.CurrentCulture)
            .Select(s => $"{s.Keys,-22} {CommandLabel(s.Command)}");
        var text = new TextView(this) { Text = "Para teclado físico conectado:\n\n" + string.Join('\n', lines), TextSize = 12 };
        text.SetTypeface(global::Android.Graphics.Typeface.Monospace, global::Android.Graphics.TypefaceStyle.Normal);
        text.SetPadding(Dp(16), Dp(12), Dp(16), Dp(12));
        var scroll = new ScrollView(this);
        scroll.AddView(text);
        new AlertDialog.Builder(this)!.SetTitle("Atalhos de teclado")!.SetView(scroll)!.SetPositiveButton("Fechar", (_, _) => { })!.Show();
    }
}
