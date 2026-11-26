using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Shell;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// The files of one tab (code, or notes) and the open document. Code is drawn by AvaloniaEdit (line numbers,
/// highlighting — LUNET-537 D1) and typed with the Lunet keyboard; notes use a text field with the system keyboard.
/// Saving is verified: the text is read back after writing.
/// </summary>
public sealed class DocumentEditor : UserControl
{
    private readonly LunetShell _shell;
    private readonly WorkspaceTab _tab;
    private readonly ITextSurface _surface;
    private string? _saved;

    public DocumentEditor(LunetShell shell, WorkspaceTab tab)
    {
        _shell = shell;
        _tab = tab;
        _surface = tab == WorkspaceTab.Code ? new CodeSurface() : new NotesSurface();
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        // Using the code (focus, or a tap) brings the code bar above the system keyboard.
        if (tab == WorkspaceTab.Code) _surface.Engaged += ShowKeyboard;
        if (_surface is CodeSurface code)
        {
            // M1: analyse a moment after typing stops (Roslyn runs off the UI thread; stale results are dropped).
            _analysisDelay.Tick += (_, _) => { _analysisDelay.Stop(); UpdateDirtyMark(); _ = AnalyzeNow(); };
            code.TextChanged += () => { _analysisDelay.Stop(); _analysisDelay.Start(); _keyboard?.TextChanged(); };
        }
    }

    /// <summary>The project-relative path of the open document, or null.</summary>
    public string? OpenPath { get; private set; }
    /// <summary>The text of the open document (code editor or notes field).</summary>
    public ITextSurface Surface => _surface;
    private readonly Avalonia.Threading.DispatcherTimer _analysisDelay = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private CancellationTokenSource? _analysis;
    private int _analysisVersion;
    private long _analysisRevision;

    /// <summary>
    /// Analyses the open code now: errors and warnings under the code and in Problemas, completions and the call
    /// signature in the keyboard strip. Returns when the result is applied (tests await it).
    /// </summary>
    public async Task AnalyzeNow()
    {
        if (_surface is not CodeSurface code || OpenPath is not { } path || _shell.Assistant is not { } assistant) return;
        // Only C# is analysed: a raw .json or .txt opened from Recursos shows no compiler errors.
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) { code.SetDiagnostics([]); code.SetFolds([]); return; }
        _analysis?.Cancel();
        var cts = _analysis = new CancellationTokenSource();
        var text = code.Text;
        var caret = code.Caret;
        var version = ++_analysisVersion;
        try
        {
            var result = await assistant.AnalyzeAsync(path, text, caret, version, ++_analysisRevision, complete: KeyboardVisible, cts.Token);
            if (version != _analysisVersion || path != OpenPath) return; // a newer edit or another file won
            code.SetDiagnostics(result.Diagnostics);
            code.SetFolds(result.Folds);
            _shell.SetLiveProblems(code.Diagnostics);
            _keyboard?.SetAnalysis(text, caret, result.Completions, result.Signature);
        }
        catch (OperationCanceledException) { }
    }

    private CodeBar? _keyboard;
    private bool _keyboardWanted;

    /// <summary>The code bar of the Código tab (created on first use, kept across layouts so its tab survives).</summary>
    public CodeBar? Keyboard => _keyboard;

    /// <summary>True when the code bar is on screen (typing is always the system keyboard's).</summary>
    public bool KeyboardVisible => _keyboard?.IsVisible == true;

    // A physical keyboard has its own symbols: the bar is for touch typing.
    private bool KeyboardApplies => _tab == WorkspaceTab.Code && !_shell.HardwareKeyboard;

    /// <summary>Shows the code bar for the editor (the editor keeps focus and the system keyboard).</summary>
    public void ShowKeyboard()
    {
        _keyboardWanted = true;
        UpdateKeyboard();
        _keyboard?.RefreshRow(); // the caret may have moved by a tap on the code
    }

    /// <summary>Hides the code bar and the system keyboard (the bar's ⌄, Back). Returns true when the bar was visible.</summary>
    public bool HideKeyboard()
    {
        var was = KeyboardVisible;
        _keyboardWanted = false;
        if (_keyboard is { Tab: CodeBarTab.Methods }) _keyboard.Show(CodeBarTab.Keys);
        UpdateKeyboard();
        if (was) _shell.SystemKeyboard?.Invoke(false);
        return was;
    }

    /// <summary>The system keyboard closed by itself (its own Back or hide key): the bar goes with it, unless Métodos is open.</summary>
    public void SystemKeyboardClosed()
    {
        if (_keyboard is { Tab: CodeBarTab.Methods }) return;
        if (!_keyboardWanted) return;
        _keyboardWanted = false;
        UpdateKeyboard();
    }

    /// <summary>Applies the current rules: the bar's visibility and the shell's tab bar.</summary>
    public void UpdateKeyboard()
    {
        var on = _keyboardWanted && KeyboardApplies;
        if (on && _keyboard is null)
        {
            _keyboard = new CodeBar(_surface, _shell.Methods, ProjectSources, () => _shell.OpenPalette(), () => HideKeyboard(), SetSystemKeyboard);
            DockPanel.SetDock(_keyboard, Dock.Bottom);
            // Into the fixed frame, before the text: the editor itself does not move (its focus and the keyboard stay).
            if (_frame is not null) _frame.Children.Insert(_frame.Children.Count - 1, _keyboard);
        }
        if (_keyboard is not null)
        {
            _keyboard.IsVisible = on;
            _keyboard.MethodsHeight = Math.Max(240, (_shell.Bounds.Height > 0 ? _shell.Bounds.Height : 800) * 0.42);
        }
        if (_tab == WorkspaceTab.Code) _shell.WorkspaceView.SetKeyboardOpen((on && _shell.WorkspaceView.CodeVisible) || _shell.KeyboardOpen);
    }

    /// <summary>Métodos takes the system keyboard's place: off while it is open, back on (and shown) after.</summary>
    private void SetSystemKeyboard(bool on)
    {
        InputMethod.SetIsInputMethodEnabled(_surface.InputTarget, on);
        if (on && _keyboardWanted) { _surface.Focus(); _shell.SystemKeyboard?.Invoke(true); }
        else if (!on) _shell.SystemKeyboard?.Invoke(false);
    }

    private IEnumerable<(string Path, string Text)> ProjectSources() =>
        _shell.Workspace.Project is null ? [] : _shell.Workspace.Files()
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => (f, _shell.Workspace.Read(f)));

    public bool IsDirty => OpenPath is not null && _surface.Text != _saved;

    private TextBlock? _openLabel;

    /// <summary>
    /// "•" after the open file's name while it has unsaved changes (anexo A §6.2). Updated when typing pauses and after
    /// saving — comparing the whole text on every key would cost as much as the file.
    /// </summary>
    public void UpdateDirtyMark()
    {
        if (_openLabel is null || OpenPath is null) return;
        _openLabel.Text = IsDirty ? OpenPath + " •" : OpenPath;
    }

    /// <summary>The open file's tab label as shown (tests).</summary>
    public string? OpenTabLabel => _openLabel?.Text;

    private Lunet.Core.EditorDocuments? _session;
    private string? _sessionFile;

    /// <summary>
    /// The editor session of the open game (M4): the files you opened, the current one and where the caret and the
    /// scroll were in each. Kept in the game itself (<c>.lunet/editor-session.json</c>), so it follows the game.
    /// </summary>
    public Lunet.Core.EditorDocuments? Session => _session;

    /// <summary>Opens the game's session: back to the file and the line you were on, or the entry point.</summary>
    public void OpenSession(string fallback)
    {
        _session = null;
        _sessionFile = _shell.Workspace.Project is { } project ? Path.Combine(project.Directory, ".lunet", "editor-session.json") : null;
        var session = _sessionFile is null ? new Lunet.Core.EditorDocuments() : Lunet.Core.EditorDocuments.Load(_sessionFile);
        session.Reconcile(_shell.Workspace.Files().Where(f => ProjectFiles.For(_tab, [f]).Count > 0));
        OpenPath = null; // the previous game's file must not be saved into this one
        _saved = null;
        _session = session;
        Load(session.Current ?? fallback);
    }

    public void Load(string? path)
    {
        Save();
        CaptureViewport();
        if (path is null) path = ProjectFiles.For(_tab, _shell.Workspace.Files()).FirstOrDefault();
        OpenPath = path;
        _saved = path is null ? null : _shell.Workspace.Read(path);
        _surface.Text = _saved ?? "";
        if (_surface is CodeSurface loaded) { loaded.SetDiagnostics([]); _analysisDelay.Stop(); _analysisDelay.Start(); }
        if (path is not null && _session is not null)
        {
            _session.Open(path, additional: true);
            RestoreViewport(_session.Viewport);
            SaveSession();
        }
        Rebuild();
    }

    /// <summary>Remembers the caret, the selection and the first visible line of the open file.</summary>
    public void CaptureViewport()
    {
        if (_session is null || OpenPath is null || _session.Current != OpenPath) return;
        int top = _surface is CodeSurface code ? code.Editor.TextArea.TextView.GetDocumentLineByVisualTop(code.Editor.TextArea.TextView.ScrollOffset.Y)?.LineNumber ?? 1 : 1;
        _session.Capture(new Lunet.Core.EditorViewport(_surface.SelectionStart, _surface.SelectionEnd, 0, top));
    }

    private void RestoreViewport(Lunet.Core.EditorViewport view)
    {
        var length = _surface.Text.Length;
        if (view.Start == 0 && view.End == 0 && view.Y <= 1) return;
        _surface.Select(Math.Min(view.Start, length), Math.Min(view.End, length));
        if (_surface is CodeSurface code && view.Y > 1)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => code.Editor.ScrollTo(Math.Min(view.Y, code.Editor.Document.LineCount), 1),
                Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>Writes the session next to the game (a lost session never loses code: it is only positions).</summary>
    public void SaveSession()
    {
        if (_session is null || _sessionFile is null) return;
        CaptureViewport();
        try { _session.Save(_sessionFile); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Writes the open document when it changed, then reads it back; reports a failed write instead of hiding it.</summary>
    public bool Save()
    {
        if (!IsDirty) return true;
        var text = _surface.Text;
        try
        {
            _shell.Workspace.Write(OpenPath!, text);
            if (_shell.Workspace.Read(OpenPath!) != text) throw new IOException("O arquivo gravado não confere com o texto.");
            _saved = text;
            UpdateDirtyMark();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _shell.Report($"Não foi possível salvar {OpenPath}: {ex.Message}. O texto continua aberto aqui.");
            return false;
        }
    }

    // ---------- Jumps and the way back ----------

    private readonly Stack<(string Path, int Caret)> _positions = new();
    public int PositionsBack => _positions.Count;

    /// <summary>Goes to a line (in this file or another), remembering where you were for <see cref="BackToPosition"/>.</summary>
    public void JumpTo(string? file, int line, int column)
    {
        if (OpenPath is { } here)
        {
            _positions.Push((here, _surface.Caret));
            if (_positions.Count > 50)
            {
                var kept = _positions.Take(50).Reverse().ToArray(); // newest 50, oldest first
                _positions.Clear();
                foreach (var p in kept) _positions.Push(p);
            }
        }
        if (file is not null && file != OpenPath && _shell.Workspace.Files().Contains(file)) Load(file);
        if (_surface is CodeSurface code) { code.GoTo(line, column); code.Focus(); }
    }

    /// <summary>Back to where you were before the last jump (definition, line, problem, symbol).</summary>
    public bool BackToPosition()
    {
        if (!_positions.TryPop(out var position)) return false;
        if (position.Path != OpenPath && _shell.Workspace.Files().Contains(position.Path)) Load(position.Path);
        _surface.Caret = Math.Min(position.Caret, _surface.Text.Length);
        if (_surface is CodeSurface code) code.Focus();
        return true;
    }

    // ---------- File actions (new, rename, delete) ----------

    private bool _fileMenu, _confirmDelete;

    public bool FileMenuOpen => _fileMenu;
    public void ToggleFileMenu() { _fileMenu = !_fileMenu; _confirmDelete = false; Rebuild(); }

    /// <summary>A new C# file with a class named after it (Scripts/ next to the game). Returns its path.</summary>
    public string NewCodeFile(string name = "NovaClasse")
    {
        var clean = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (clean.Length == 0 || char.IsDigit(clean[0])) clean = "NovaClasse" + clean;
        clean = char.ToUpperInvariant(clean[0]) + clean[1..];
        var existing = _shell.Workspace.Files().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var className = clean;
        for (var n = 2; existing.Contains($"Scripts/{className}.cs"); n++) className = clean + n;
        var path = $"Scripts/{className}.cs";
        _shell.Workspace.Write(path, $"using Lunet;\n\npublic sealed class {className}\n{{\n}}\n");
        _fileMenu = false;
        Load(path);
        return path;
    }

    /// <summary>Renames the open file (keeps its folder and, when no extension is typed, its extension).</summary>
    public string? RenameOpen(string newName)
    {
        if (OpenPath is not { } from || _shell.Workspace.Project is not { } project) return null;
        // "Inimigos/Slime" moves the file into a folder (created as needed), relative to its current folder.
        newName = newName.Trim().Replace('\\', '/').Trim('/');
        var segments = newName.Split('/');
        if (newName.Length == 0 || segments.Any(x => x.Trim().Length == 0 || x is "." or ".." || x.StartsWith(".lunet", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Use um nome de arquivo, ou Pasta/Nome para pôr numa pasta.");
        var ext = from.EndsWith(".sprite.json", StringComparison.OrdinalIgnoreCase) ? ".sprite.json" : Path.GetExtension(from);
        if (!newName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) newName += ext;
        var folder = Path.GetDirectoryName(from)?.Replace('\\', '/');
        var to = string.IsNullOrEmpty(folder) ? newName : folder + "/" + newName;
        if (to == from) return from;
        Save();
        project.Rename(from, to);
        _session?.Reconcile(_shell.Workspace.Files());
        OpenPath = null;
        _fileMenu = false;
        Load(to);
        return to;
    }

    /// <summary>Writes a copy of the open file under a new name (same folder and extension) and opens the copy.</summary>
    public string? SaveCopyAs(string newName)
    {
        if (OpenPath is not { } from) return null;
        newName = newName.Trim();
        if (newName.Length == 0 || newName.IndexOfAny(['/', '\\']) >= 0) throw new ArgumentException("Use só o nome do arquivo, sem pastas.");
        var ext = Path.GetExtension(from);
        if (!newName.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) newName += ext;
        var folder = Path.GetDirectoryName(from)?.Replace('\\', '/');
        var to = string.IsNullOrEmpty(folder) ? newName : folder + "/" + newName;
        if (_shell.Workspace.Files().Contains(to, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Já existe um arquivo com esse nome.");
        Save();
        _shell.Workspace.Write(to, _surface.Text);
        _fileMenu = false;
        Load(to);
        return to;
    }

    /// <summary>Opens find and replace with the word under the caret: change every occurrence at once.</summary>
    public void EditAllOccurrences()
    {
        var text = _surface.Text;
        int a = _surface.SelectionStart, b = _surface.SelectionEnd;
        if (a == b)
        {
            while (a > 0 && (char.IsLetterOrDigit(text[a - 1]) || text[a - 1] == '_')) a--;
            while (b < text.Length && (char.IsLetterOrDigit(text[b]) || text[b] == '_')) b++;
        }
        _findOpen = true;
        _matchCase = true;
        _wholeWord = true; // the symbol, not the same letters inside other words
        _findQuery = text[a..b];
        _replaceText = _findQuery;
        Rebuild();
        Find(_findQuery);
    }

    /// <summary>Deletes the open file (the game's entry and lunet.json are kept by the project).</summary>
    public void DeleteOpen()
    {
        if (OpenPath is not { } path || _shell.Workspace.Project is not { } project) return;
        project.Delete(path);
        _session?.Reconcile(_shell.Workspace.Files());
        OpenPath = null;
        _saved = null;
        _fileMenu = _confirmDelete = false;
        Load(null);
    }

    private void FileAction(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is Lunet.Core.ProjectException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            _shell.Report(e.Message);
            Rebuild();
        }
    }

    private Control FileMenu()
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(LunetTokens.Space.Xs, LunetTokens.Space.Xxs) };
        void Add(Control c) { c.Margin = new Thickness(0, 0, LunetTokens.Space.Xs, LunetTokens.Space.Xxs); row.Children.Add(c); }
        var find = LunetUi.Button("Buscar e substituir", ButtonKind.Secondary, LunetIcons.Search, () => { _fileMenu = false; ShowFind(); });
        find.Name = "ShowFind";
        Add(find);
        if (_tab == WorkspaceTab.Code)
        {
            var add = LunetUi.Button("Novo arquivo", ButtonKind.Secondary, LunetIcons.Plus, () => FileAction(() => NewCodeFile()));
            add.Name = "NewCodeFile";
            Add(add);
        }
        var manifest = _shell.Workspace.Project?.Manifest;
        if (OpenPath is { } open && open != Lunet.Core.ProjectStore.ManifestFileName && open != manifest?.EntryPoint)
        {
            var name = new TextBox { Text = Path.GetFileName(open), MinHeight = 40, Width = 180, VerticalContentAlignment = VerticalAlignment.Center, Name = "RenameFile" };
            AutomationProperties.SetName(name, "Novo nome do arquivo (Pasta/Nome põe numa pasta)");
            ToolTip.SetTip(name, "Pasta/Nome põe o arquivo numa pasta");
            name.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; FileAction(() => RenameOpen(name.Text ?? "")); } };
            Add(name);
            var rename = LunetUi.Button("Renomear", ButtonKind.Ghost, LunetIcons.Edit, () => FileAction(() => RenameOpen(name.Text ?? "")));
            rename.Name = "RenameOpen";
            Add(rename);
            var copy = LunetUi.Button("Salvar cópia", ButtonKind.Ghost, LunetIcons.Copy, () => FileAction(() => SaveCopyAs(name.Text ?? "")));
            copy.Name = "SaveCopyAs";
            Add(copy);
            var delete = LunetUi.Button(_confirmDelete ? "Apagar mesmo?" : "Apagar", ButtonKind.Ghost, LunetIcons.Trash, () =>
            {
                if (!_confirmDelete) { _confirmDelete = true; Rebuild(); }
                else FileAction(DeleteOpen);
            });
            delete.Name = "DeleteOpen";
            if (_confirmDelete) delete.Foreground = LunetTokens.Brush(LunetTokens.Color.Danger);
            Add(delete);
        }
        else if (OpenPath is { } kept)
        {
            var note = LunetUi.Text($"{Path.GetFileName(kept)} é a base do jogo: não se renomeia nem se apaga.", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary);
            note.VerticalAlignment = VerticalAlignment.Center;
            Add(note);
        }
        return new Border { Child = row, Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    // ---------- Find and replace ----------

    private bool _findOpen;
    private string _findQuery = "", _replaceText = "";
    private bool _matchCase, _wholeWord;
    private readonly TextBlock _findCount = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);

    public bool FindOpen => _findOpen;
    private Lunet.Editor.FindOptions FindOptions => new(MatchCase: _matchCase, WholeWord: _wholeWord);

    public void ShowFind() { _findOpen = true; _wholeWord = false; Rebuild(); }
    public void HideFind() { _findOpen = false; Rebuild(); }

    /// <summary>Sets what to look for; selects the first match from the caret. Returns how many there are.</summary>
    public int Find(string query)
    {
        _findQuery = query;
        var count = query.Length == 0 ? 0 : Lunet.Editor.FindReplace.FindAll(_surface.Text, query, FindOptions).Count;
        _findCount.Text = query.Length == 0 ? "" : count == 0 ? "nada" : $"{count}×";
        if (count > 0) FindNext(fromCaret: true);
        return count;
    }

    /// <summary>Selects the next match (wrapping to the start).</summary>
    public bool FindNext(bool fromCaret = false)
    {
        if (_findQuery.Length == 0) return false;
        var text = _surface.Text;
        var from = fromCaret ? _surface.SelectionStart : _surface.SelectionEnd;
        var match = Lunet.Editor.FindReplace.FindNext(text, _findQuery, Math.Min(from, text.Length), FindOptions) ?? Lunet.Editor.FindReplace.FindNext(text, _findQuery, 0, FindOptions);
        if (match is not { } m) return false;
        _surface.Select(m.Start, m.Start + m.Length);
        return true;
    }

    /// <summary>Replaces the selected match and goes to the next one.</summary>
    public bool ReplaceCurrent(string replacement)
    {
        _replaceText = replacement;
        var text = _surface.Text;
        var (start, end) = (_surface.SelectionStart, _surface.SelectionEnd);
        var selected = end > start ? text[start..end] : "";
        if (selected.Length == 0 || !string.Equals(selected, _findQuery, _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            return FindNext(fromCaret: true);
        _surface.Replace(start, end, replacement, start + replacement.Length);
        Find(_findQuery);
        return true;
    }

    /// <summary>Replaces every match as one undo step. Returns how many were replaced.</summary>
    public int ReplaceAll(string replacement)
    {
        _replaceText = replacement;
        if (_findQuery.Length == 0) return 0;
        var result = Lunet.Editor.FindReplace.ReplaceAll(_surface.Text, _findQuery, replacement, FindOptions, out var count);
        if (count > 0) _surface.Replace(0, _surface.Text.Length, result, Math.Min(_surface.Caret, result.Length));
        _findCount.Text = count == 0 ? "nada" : $"{count} trocado(s)";
        return count;
    }

    private Control FindBar()
    {
        var find = new TextBox { Text = _findQuery, PlaceholderText = "Buscar", MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Name = "FindText" };
        AutomationProperties.SetName(find, "Buscar no arquivo");
        find.TextChanged += (_, _) => Find(find.Text ?? "");
        find.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { e.Handled = true; FindNext(); } else if (e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; HideFind(); } };
        var replace = new TextBox { Text = _replaceText, PlaceholderText = "Substituir por", MinHeight = 40, VerticalContentAlignment = VerticalAlignment.Center, Name = "ReplaceText" };
        AutomationProperties.SetName(replace, "Substituir por");
        Button Small(string label, Action act, string name, string? tip = null)
        {
            var b = LunetUi.Button(label, ButtonKind.Ghost, null, act);
            b.MinHeight = 40; b.Name = name;
            AutomationProperties.SetName(b, tip ?? label);
            return b;
        }
        var top = new DockPanel();
        var close = LunetUi.IconButton(LunetIcons.Close, "Fechar a busca", HideFind);
        var next = Small("↓", () => FindNext(), "FindNext", "Próximo");
        var caseButton = Small("Aa", () => { _matchCase = !_matchCase; Find(find.Text ?? ""); Rebuild(); }, "MatchCase", "Diferenciar maiúsculas");
        if (_matchCase) caseButton.Foreground = LunetTokens.Brush(LunetTokens.Color.Primary);
        if (_findCount.Parent is Panel previous) previous.Children.Remove(_findCount);
        _findCount.VerticalAlignment = VerticalAlignment.Center;
        _findCount.Margin = new Thickness(LunetTokens.Space.Xs, 0);
        foreach (var c in new Control[] { close, next, caseButton, _findCount }) { DockPanel.SetDock(c, Dock.Right); top.Children.Add(c); }
        top.Children.Add(find);
        var bottom = new DockPanel { Margin = new Thickness(0, LunetTokens.Space.Xxs, 0, 0) };
        var all = Small("Tudo", () => ReplaceAll(replace.Text ?? ""), "ReplaceAll", "Substituir todos");
        var one = Small("Trocar", () => ReplaceCurrent(replace.Text ?? ""), "ReplaceOne", "Substituir este");
        DockPanel.SetDock(all, Dock.Right); DockPanel.SetDock(one, Dock.Right);
        bottom.Children.Add(all); bottom.Children.Add(one); bottom.Children.Add(replace);
        return new Border
        {
            Child = new StackPanel { Children = { top, bottom } }, Padding = new Thickness(LunetTokens.Space.Xs),
            Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 0, 1),
        };
    }

    private void NewNote()
    {
        var existing = _shell.Workspace.Files().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var n = 1;
        string path;
        do path = $"{Lunet.Core.ProjectNotes.Folder}/nota-{n++}.md"; while (existing.Contains(path));
        _shell.Workspace.Write(path, "# Nova nota\n\n");
        Load(path);
    }

    private void Rebuild()
    {
        var entries = new List<Control>();
        // Files you opened come first (most recent session order), then the rest of the game's files.
        var open = _session?.Tabs.ToList() ?? [];
        // A text file opened from Recursos (raw .json, .txt…) shows in the strip while it is open.
        var listed = ProjectFiles.For(_tab, _shell.Workspace.Files()).ToList();
        if (OpenPath is { } extra && !listed.Contains(extra)) listed.Insert(0, extra);
        foreach (var file in listed.OrderBy(f => f == OpenPath && !ProjectFiles.For(_tab, [f]).Any() ? -1 : open.IndexOf(f) is var i && i >= 0 ? i : int.MaxValue))
        {
            var selected = file == OpenPath;
            var label = LunetUi.Text(selected && IsDirty ? file + " •" : file, LunetTokens.Type.Label, selected ? LunetTokens.Color.Primary : LunetTokens.Color.TextSecondary,
                selected ? FontWeight.SemiBold : FontWeight.Normal);
            if (selected) _openLabel = label;
            label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.VerticalAlignment = VerticalAlignment.Center;
            var b = new Button
            {
                Content = label, MinHeight = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.S, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : "#00000000"), BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(LunetTokens.Radius.S),
            };
            AutomationProperties.SetName(b, "Abrir " + file);
            b.Click += (_, _) => Load(file);
            entries.Add(b);
        }
        if (_tab == WorkspaceTab.Notes)
        {
            // P2: the game's mind maps sit with its notes; they open as an outline.
            foreach (var map in _shell.Workspace.Files().Where(f => f.StartsWith(Lunet.Core.ProjectNotes.Folder + "/", StringComparison.Ordinal)
                && f.EndsWith(Lunet.Core.ProjectMindMap.Extension, StringComparison.OrdinalIgnoreCase)))
            {
                var path = map;
                var b = LunetUi.Button(Path.GetFileName(map).Replace(Lunet.Core.ProjectMindMap.Extension, "", StringComparison.OrdinalIgnoreCase), ButtonKind.Ghost, LunetIcons.Map,
                    () => _shell.WorkspaceView.OpenMindMap(path));
                b.Name = "OpenMindMap-" + map;
                entries.Add(b);
            }
            entries.Add(LunetUi.Button("Nova nota", ButtonKind.Ghost, LunetIcons.Plus, NewNote));
            var newMap = LunetUi.Button("Novo mapa mental", ButtonKind.Ghost, LunetIcons.Map, () => _shell.WorkspaceView.NewMindMap());
            newMap.Name = "NewMindMap";
            entries.Add(newMap);
        }

        var more = LunetUi.IconButton(LunetIcons.More, "Arquivo: novo, renomear, apagar, buscar", ToggleFileMenu, _fileMenu ? LunetTokens.Color.Primary : LunetTokens.Color.TextSecondary);
        more.Name = "FileMenu";
        entries.Add(more);

        // The editor (and the code bar) never leave their place in the tree: moving a focused text field clears its
        // focus, and Android then closes the keyboard (estudo do editor, 26/11/2026 — "teclado fechando sozinho").
        // Only the file list, the find bar and the file menu around it are rebuilt.
        EnsureFrame();
        _side.Content = null; _chrome.Children.Clear();
        if (OpenPath is null && _tab == WorkspaceTab.Notes)
        {
            var start = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
            start.Children.Add(LunetUi.Button("Nova nota", ButtonKind.Primary, LunetIcons.Plus, NewNote));
            start.Children.Add(LunetUi.Button("Novo mapa mental", ButtonKind.Secondary, LunetIcons.Map, () => _shell.WorkspaceView.NewMindMap()));
            _empty.Content = LunetUi.EmptyState(LunetIcons.Notes, "Nenhuma nota neste jogo",
                "Ideias, regras e roteiro ficam em Notes/ dentro do jogo, em Markdown ou como mapa mental, no seu disco.", start);
            _empty.IsVisible = true;
            _surface.View.IsVisible = false;
            UpdateKeyboard();
            return;
        }
        _empty.Content = null; _empty.IsVisible = false;
        _surface.View.IsVisible = true;
        // On a phone the file list collapses: the text gets the width; files are one tap away in the header.
        if (_shell.State.Layout == ShellLayout.Expanded && !_shell.State.CodeAndGameSideBySide)
        {
            var column = new StackPanel { Spacing = 2, Margin = new Thickness(LunetTokens.Space.Xs) };
            foreach (var e in entries) { e.HorizontalAlignment = HorizontalAlignment.Stretch; column.Children.Add(e); }
            _side.Content = new Border
            {
                Child = new ScrollViewer { Content = column }, Width = 220, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
                BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 1, 0),
            };
        }
        else
        {
            var strip = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xxs, Margin = new Thickness(LunetTokens.Space.Xs, LunetTokens.Space.Xxs) },
            };
            var row = (StackPanel)strip.Content!;
            foreach (var e in entries) { e.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(e); }
            _chrome.Children.Add(new Border { Child = strip, Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 0, 1) });
        }
        if (_findOpen && OpenPath is not null) _chrome.Children.Add(FindBar());
        if (_fileMenu) _chrome.Children.Add(FileMenu());
        if (_keyboard is not null && _keyboard.Parent is null) _frame!.Children.Insert(_frame.Children.Count - 1, _keyboard);
        UpdateKeyboard();
    }

    private DockPanel? _frame;
    private readonly ContentControl _side = new();
    private readonly StackPanel _chrome = new();
    private readonly ContentControl _empty = new() { IsVisible = false };

    /// <summary>The fixed frame: list (left), file strip, find bar and menu (top), code bar (bottom), the text.</summary>
    private void EnsureFrame()
    {
        if (_frame is not null) return;
        _frame = new DockPanel();
        DockPanel.SetDock(_side, Dock.Left);
        DockPanel.SetDock(_chrome, Dock.Top);
        _frame.Children.Add(_side);
        _frame.Children.Add(_chrome);
        if (_surface.View.Parent is Panel old) old.Children.Remove(_surface.View);
        _frame.Children.Add(new Panel { Children = { _surface.View, _empty } });
        Content = _frame;
    }

    /// <summary>Re-lays out the file list for the current shell layout.</summary>
    public void Relayout() => Rebuild();
}
