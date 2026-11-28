using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Avalonia.Threading;
using Lunet.Core;

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
    private string? _recoveryBase;
    private bool _loading;
    private bool _journalPending;
    private readonly DispatcherTimer _saveDelay = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private Task<bool>? _pendingSave;

    public DocumentEditor(LunetShell shell, WorkspaceTab tab)
    {
        _shell = shell;
        _tab = tab;
        _surface = tab == WorkspaceTab.Code ? new CodeSurface() : new NotesSurface();
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        _surface.TextChanged += () =>
        {
            if (!_loading && OpenPath is not null && !_saveDelay.IsEnabled)
                _saveDelay.Start();
        };
        _saveDelay.Tick += async (_, _) =>
        {
            _saveDelay.Stop();
            if (_pendingSave is not null || (!IsDirty && !_journalPending)) return;
            UpdateDirtyMark();
            var path = OpenPath!;
            var text = _surface.Text;
            var owner = Dispatcher;
            var save = _pendingSave = StartWrite(path, text);
            var saved = await save.ConfigureAwait(false);
            owner.Post(() =>
            {
                if (!ReferenceEquals(save, _pendingSave)) return;
                CompleteWrite(text, saved);
                if (IsDirty && (saved || text != _surface.Text) && !_saveDelay.IsEnabled) _saveDelay.Start();
            });
        };
        // Using the code (focus, or a tap) brings the code bar above the system keyboard.
        if (tab == WorkspaceTab.Code) _surface.Engaged += ShowKeyboard;
        if (_surface is CodeSurface code)
        {
            // M1: analyse a moment after typing stops (Roslyn runs off the UI thread; stale results are dropped).
            _analysisDelay.Tick += (_, _) => { _analysisDelay.Stop(); UpdateDirtyMark(); _ = AnalyzeNow(); };
            code.TextChanged += () =>
            {
                _analysis?.Cancel();
                _analysisDelay.Stop();
                _analysisDelay.Start();
                if (KeyboardVisible) _keyboard?.TextChanged();
            };
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
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            code.SetDiagnostics([]);
            code.SetFolds([]);
            _shell.SetLiveProblems([]);
            return;
        }
        _analysis?.Cancel();
        var cts = _analysis = new CancellationTokenSource();
        var text = code.Text;
        var caret = code.Caret;
        var version = ++_analysisVersion;
        try
        {
            var result = await assistant.AnalyzeAsync(path, text, caret, version, ++_analysisRevision, complete: KeyboardVisible, cts.Token);
            if (cts.IsCancellationRequested || version != _analysisVersion || path != OpenPath || !ReferenceEquals(assistant, _shell.Assistant)) return; // a newer edit or another file won
            code.SetDiagnostics(result.Diagnostics);
            code.SetFolds(result.Folds);
            _shell.SetLiveProblems(code.Diagnostics);
            _keyboard?.SetAnalysis(text, caret, result.Completions, result.Signature);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!cts.IsCancellationRequested) _shell.Report("Não foi possível analisar o código: " + ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_analysis, cts)) _analysis = null;
            cts.Dispose();
        }
    }

    private CodeBar? _keyboard;
    private bool _creatingKeyboard;
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
        if (on && _keyboard is null && !_creatingKeyboard)
        {
            // Building the bar can call back here (its first row asks the editor for the caret): without the guard a
            // second bar was built and both showed (capture 4-jogo, 27/11).
            _creatingKeyboard = true;
            try { _keyboard = new CodeBar(_surface, _shell.Methods, ProjectSources, () => _shell.OpenPalette(), () => HideKeyboard(), SetSystemKeyboard); }
            finally { _creatingKeyboard = false; }
            DockPanel.SetDock(_keyboard, Dock.Bottom);
            // Into the fixed frame, before the text: the editor itself does not move (its focus and the keyboard stay).
            AttachKeyboard();
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
        _openLabel.Text = IsDirty ? Path.GetFileName(OpenPath) + " •" : Path.GetFileName(OpenPath);
    }

    /// <summary>The open file's tab label as shown (tests).</summary>
    public string? OpenTabLabel => _openLabel?.Text;

    private Lunet.Core.EditorDocuments? _session;
    private string? _sessionFile;
    private Lunet.Core.LunetProject? _sessionProject;

    /// <summary>
    /// The editor session of the open game (M4): the files you opened, the current one and where the caret and the
    /// scroll were in each. Kept in the game itself (<c>.lunet/editor-session.json</c>), so it follows the game.
    /// </summary>
    public Lunet.Core.EditorDocuments? Session => _session;

    /// <summary>Opens the game's session: back to the file and the line you were on, or the entry point.</summary>
    public void OpenSession(string? fallback)
    {
        _session = null;
        _sessionProject = _shell.Workspace.Project;
        _sessionFile = _sessionProject is null ? null : _tab == WorkspaceTab.Code ? ".lunet/editor-session.json" : ".lunet/notes-session.json"; // inside the game, through its files
        var session = _sessionProject is null ? new Lunet.Core.EditorDocuments() : Lunet.Core.EditorDocuments.Load(_sessionProject, _sessionFile!);
        session.Reconcile(_shell.Workspace.Files().Where(f => ProjectFiles.For(_tab, [f]).Count > 0));
        OpenPath = null; // the previous game's file must not be saved into this one
        _saved = null;
        _session = session;
        Load(session.Current ?? fallback);
    }

    public void Load(string? path)
    {
        _analysis?.Cancel();
        if (!Save()) return;
        CaptureViewport();
        if (path is null) path = ProjectFiles.For(_tab, _shell.Workspace.Files()).FirstOrDefault();
        string? disk;
        try { disk = path is null ? null : _shell.Workspace.Read(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException)
        {
            _shell.Report($"Não foi possível abrir {path}: {ex.Message}");
            return;
        }
        ApplyDocument(path, disk, ReadDraft(_shell.Workspace.Project, path));
    }

    // Used by the Explorer after creation. The previous buffer stays mounted until both the save and read succeed.
    internal async Task<bool> LoadCreatedAsync(LunetProject project, string path)
    {
        var previousPath = OpenPath;
        if (!await SaveAsync() || !ReferenceEquals(project, _shell.Workspace.Project) || OpenPath != previousPath)
            return false;
        var recoveryPath = _shell.RecoveryPath;
        var document = await Task.Run(() => (project.ReadText(path), new AutosaveJournal(project, recoveryPath).ReadBuffer(path)));
        if (!ReferenceEquals(project, _shell.Workspace.Project) || OpenPath != previousPath || IsDirty) return false;
        _analysis?.Cancel();
        CaptureViewport();
        ApplyDocument(path, document.Item1, document.Item2);
        return true;
    }

    private AutosaveJournal.Draft? ReadDraft(LunetProject? project, string? path)
    {
        if (project is null || path is null) return null;
        try { return new AutosaveJournal(project, _shell.RecoveryPath).ReadBuffer(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            _shell.Report("Não foi possível ler o rascunho: " + ex.Message);
            return null;
        }
    }

    private void ApplyDocument(string? path, string? disk, AutosaveJournal.Draft? draft)
    {
        OpenPath = path;
        _saved = disk;
        _recoveryBase = null;
        _journalPending = draft is not null;
        var text = disk ?? "";
        if (draft is not null && draft.Text != disk)
        {
            _recoveryBase = draft.Previous ?? disk;
            text = draft.Text;
            _shell.Report("Rascunho recuperado: " + path);
            if (_recoveryBase == disk) _saveDelay.Start();
        }
        _loading = true;
        try { _surface.Text = text; }
        finally { _loading = false; }
        if (_surface is CodeSurface loaded) { loaded.SetDiagnostics([]); _analysisDelay.Stop(); _analysisDelay.Start(); }
        ApplyChangedLines();
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
        if (_session is null || _sessionFile is null || _sessionProject is null) return;
        CaptureViewport();
        try { _session.Save(_sessionProject, _sessionFile); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Lunet.Core.ProjectException) { }
    }

    /// <summary>Saves before a transition, waiting for any earlier write to finish.</summary>
    public bool Save()
    {
        _saveDelay.Stop();
        if (_pendingSave is { } pending)
        {
            var result = pending.GetAwaiter().GetResult();
            CompleteWrite(_writingText!, result);
        }
        if (OpenPath is not { } path || (!IsDirty && !_journalPending)) return true;
        var text = _surface.Text;
        var saved = StartWrite(path, text).GetAwaiter().GetResult();
        CompleteWrite(text, saved);
        return saved;
    }

    internal async Task<bool> SaveAsync()
    {
        var project = _shell.Workspace.Project;
        var path = OpenPath;
        while (ReferenceEquals(project, _shell.Workspace.Project) && path == OpenPath)
        {
            _saveDelay.Stop();
            if (_pendingSave is null && (path is null || (!IsDirty && !_journalPending))) return true;
            var pending = _pendingSave ??= StartWrite(path!, _surface.Text);
            var text = _writingText!;
            var saved = await pending;
            if (ReferenceEquals(pending, _pendingSave)) CompleteWrite(text, saved);
            if (!saved) return false;
        }
        return false;
    }

    private string? _writingText;
    private string? _writeError;

    private Task<bool> StartWrite(string path, string text)
    {
        var project = _shell.Workspace.Project!;
        var recovery = new AutosaveJournal(project, _shell.RecoveryPath);
        var previous = _recoveryBase ?? _saved!;
        var unchanged = text == _saved;
        _writingText = text;
        return Task.Run(() =>
        {
            try
            {
                if (unchanged) recovery.Discard(path);
                else recovery.SaveVerified(path, previous, text);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ProjectException)
            {
                _writeError = ex.Message;
                return false;
            }
        });
    }

    private void CompleteWrite(string text, bool saved)
    {
        _pendingSave = null;
        _journalPending = !saved;
        if (saved)
        {
            _saved = text;
            _recoveryBase = null;
        }
        else _shell.Report($"Não foi possível salvar {OpenPath}: {_writeError} Use Salvar cópia para preservar o texto em outro arquivo.");
        _writeError = null;
        UpdateDirtyMark();
    }

    // ---------- Jumps and the way back ----------

    private readonly Stack<(string Path, int Caret)> _positions = new();
    public int PositionsBack => _positions.Count;
    private readonly Stack<(string Path, int Caret)> _forward = new();
    public int PositionsForward => _forward.Count;

    /// <summary>Forward again after <see cref="BackToPosition"/> (Alt+→, VS Code's "Go Forward").</summary>
    public bool ForwardToPosition()
    {
        if (!_forward.TryPop(out var position)) return false;
        if (OpenPath is { } here) _positions.Push((here, _surface.Caret));
        if (position.Path != OpenPath && _shell.Workspace.Files().Contains(position.Path)) Load(position.Path);
        _surface.Caret = Math.Min(position.Caret, _surface.Text.Length);
        if (_surface is CodeSurface code) code.Focus();
        return true;
    }

    /// <summary>Goes to a line (in this file or another), remembering where you were for <see cref="BackToPosition"/>.</summary>
    public void JumpTo(string? file, int line, int column)
    {
        if (_tab == WorkspaceTab.Code && _shell.WorkspaceView.PreviewFull) _shell.ClosePreview();
        _forward.Clear(); // a new jump starts a new way forward, like a browser
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
        if (OpenPath is { } here) _forward.Push((here, _surface.Caret));
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
        if (!Save()) return null;
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
        var project = _shell.Workspace.Project!;
        var saved = Save();
        var text = _surface.Text;
        project.CreateFile(to, text);
        if (project.ReadText(to) != text) throw new IOException("A cópia gravada não confere.");
        if (!saved)
        {
            new AutosaveJournal(project, _shell.RecoveryPath).Discard(from);
            _journalPending = false;
            OpenPath = null;
        }
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
        if (!Save()) return;
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
            var add = LunetUi.Button("Novo arquivo", ButtonKind.Secondary, LunetIcons.Plus, () => { _fileMenu = false; Rebuild(); _shell.WorkspaceView.BeginFileCreation(); });
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
            var delete = LunetUi.Button(_confirmDelete ? "Apagar mesmo?" : "Apagar", ButtonKind.Destructive, LunetIcons.Trash, () =>
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

    /// <summary>Files in Notes/ that are not notes or mind maps (images, PDFs… imported).</summary>
    public IReadOnlyList<string> NoteAttachments() => _shell.Workspace.Files()
        .Where(f => f.StartsWith(Lunet.Core.ProjectNotes.Folder + "/", StringComparison.Ordinal)
            && !f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            && !f.EndsWith(Lunet.Core.ProjectMindMap.Extension, StringComparison.OrdinalIgnoreCase))
        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>
    /// Copies a file into Notes/ (never over another; atomic write; at most 64 MB, like the previous screen) and
    /// returns its project path. Notes/ travels with the game (ZIP, Git) and the AI can read it.
    /// </summary>
    public string ImportNote(string fileName, byte[] bytes)
    {
        if (bytes.LongLength > Lunet.Core.ProjectNotes.MaxImportBytes) throw new IOException("arquivo maior que 64 MB");
        var project = _shell.Workspace.Project ?? throw new InvalidOperationException("Nenhum jogo aberto.");
        var path = Lunet.Core.ProjectNotes.ImportPath(project, fileName);
        using (var source = new MemoryStream(bytes, writable: false))
            project.Import(Lunet.Core.ProjectNotes.Folder, Path.GetFileName(path), source); // atomic: temporary file, then move
        Rebuild();
        return path;
    }

    private void AttachToAi(string path)
    {
        _shell.State.ShowPanel(SupportPanel.Ai);
        _shell.WorkspaceView.Support.Ai.Attach(path);
    }

    private async Task PickAndImportNotes()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage) return;
        var picked = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions { Title = "Importar para Notas", AllowMultiple = true });
        var done = 0;
        foreach (var file in picked)
        {
            try
            {
                await using var stream = await file.OpenReadAsync();
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                ImportNote(file.Name, buffer.ToArray());
                done++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or Lunet.Core.ProjectException)
            {
                _shell.Report($"Não foi possível importar {file.Name}: {e.Message}");
            }
        }
        if (done > 0) _shell.Report(done == 1 ? "Importado em Notas." : $"{done} arquivos importados em Notas.");
    }

    /// <summary>Closes a tab (saving it first); the next tab opens, or the editor shows that nothing is open.</summary>
    public void CloseTab(string path)
    {
        if (path == OpenPath && !Save()) return;
        if (_session is null) return;
        _session.Close(path);
        SaveSession();
        if (path != OpenPath) { Rebuild(); return; }
        if (_session.Current is { } next) Load(next);
        else
        {
            OpenPath = null; _saved = null; _surface.Text = "";
            Rebuild();
        }
    }

    /// <summary>
    /// A file or folder was renamed (to) or deleted (null) in the Explorer: the open tab follows the file, or closes.
    /// </summary>
    public void FilesMoved(string from, string? to)
    {
        static bool Under(string path, string root) => path == root || path.StartsWith(root + "/", StringComparison.Ordinal);
        var open = OpenPath;
        if (_session is not null)
        {
            foreach (var tab in _session.Tabs.Where(t => Under(t, from)).ToArray())
            {
                if (to is not null && _session.Current == tab) { _session.Close(tab); _session.Open(to + tab[from.Length..], additional: true); }
                else _session.Close(tab);
            }
            SaveSession();
        }
        if (open is not null && Under(open, from))
        {
            OpenPath = null; _saved = null;
            var target = to is null ? _session?.Current : to + open[from.Length..];
            if (target is null) { _surface.Text = ""; Rebuild(); }
            else Load(target);
        }
        else Rebuild();
    }

    private Lunet.Ai.LunetAiCheckpoint? _review;
    private int _reviewIndex;

    /// <summary>Text files the AI changed in the run under review (code, data, notes), in its order.</summary>
    public IReadOnlyList<Lunet.Ai.LunetAiChange> ReviewChanges => _review is { IsReverted: false } r
        ? r.Changes().Where(c => c.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || c.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || c.Path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || c.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToArray()
        : [];

    /// <summary>
    /// Numa-like review (owner, 27/11: "o Numa faz isso bem"): what the AI changed shows in the file itself — the
    /// changed lines tinted, a bar to go through the changed files, keep them or undo the whole run.
    /// </summary>
    public bool Review(Lunet.Ai.LunetAiCheckpoint checkpoint)
    {
        _review = checkpoint;
        _reviewIndex = 0;
        if (ReviewChanges.Count == 0) { _review = null; return false; }
        Load(ReviewChanges[0].Path);
        return true;
    }

    /// <summary>Goes to the next (or previous) changed file of the review.</summary>
    public void ReviewStep(int delta)
    {
        var changes = ReviewChanges;
        if (changes.Count == 0) return;
        _reviewIndex = ((_reviewIndex + delta) % changes.Count + changes.Count) % changes.Count;
        Load(changes[_reviewIndex].Path);
    }

    /// <summary>Keeps the AI's changes (they are already in the files) and ends the review.</summary>
    public void KeepReview()
    {
        _review = null;
        ApplyChangedLines();
        Rebuild();
    }

    /// <summary>Undoes the whole run under review (the on-disk checkpoint) and reloads.</summary>
    public void UndoReview()
    {
        if (_review is null) return;
        _review.Revert();
        _review = null;
        _shell.WorkspaceView.ReloadFromDisk();
        _shell.Report("Alterações da IA desfeitas.");
        ApplyChangedLines();
        Rebuild();
    }

    /// <summary>The 1-based lines of the open file that the change under review added or rewrote.</summary>
    public static IReadOnlyList<int> ChangedLinesOf(string? before, string after)
    {
        var result = new List<int>();
        var line = 0;
        foreach (var d in Lunet.Ai.LunetAiDiff.Lines(before, after))
        {
            if (d.Kind == Lunet.Ai.DiffKind.Removed) continue;
            line++;
            if (d.Kind == Lunet.Ai.DiffKind.Added) result.Add(line);
        }
        return result;
    }

    private void ApplyChangedLines()
    {
        if (_surface is not CodeSurface code) return;
        var change = OpenPath is null ? null : ReviewChanges.FirstOrDefault(c => c.Path == OpenPath);
        code.SetChangedLines(change is null ? [] : ChangedLinesOf(change.Before, change.After));
    }

    private Control ReviewBar()
    {
        var changes = ReviewChanges;
        var index = Math.Max(0, changes.ToList().FindIndex(c => c.Path == OpenPath));
        var change = changes[index];
        var diff = Lunet.Ai.LunetAiDiff.Lines(change.Before, change.After);
        int added = diff.Count(l => l.Kind == Lunet.Ai.DiffKind.Added), removed = diff.Count(l => l.Kind == Lunet.Ai.DiffKind.Removed);
        var bar = new DockPanel { Margin = new Thickness(LunetTokens.Space.S, LunetTokens.Space.Xxs) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xxs };
        if (changes.Count > 1)
        {
            actions.Children.Add(LunetUi.IconButton(LunetIcons.Back, "Arquivo alterado anterior", () => ReviewStep(-1)));
            actions.Children.Add(LunetUi.IconButton(LunetIcons.ChevronRight, "Próximo arquivo alterado", () => ReviewStep(1)));
        }
        var undo = LunetUi.Button("Desfazer", ButtonKind.Ghost, LunetIcons.Undo, UndoReview);
        undo.Name = "ReviewUndo";
        var keep = LunetUi.Button("Manter", ButtonKind.Primary, LunetIcons.Check, KeepReview);
        keep.Name = "ReviewKeep";
        actions.Children.Add(undo);
        actions.Children.Add(keep);
        DockPanel.SetDock(actions, Dock.Right);
        bar.Children.Add(actions);
        var text = LunetUi.Text($"IA alterou {Path.GetFileName(change.Path)}  +{added} −{removed}" + (changes.Count > 1 ? $" · {index + 1} de {changes.Count}" : ""),
            LunetTokens.Type.Label, LunetTokens.Color.TextPrimary, FontWeight.SemiBold);
        text.VerticalAlignment = VerticalAlignment.Center;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        bar.Children.Add(text);
        return new Border { Child = bar, Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised), Name = "AiReviewBar" };
    }

    /// <summary>
    /// Where the open file is (VS Code's breadcrumbs): each folder shows it in the Explorer; the file's name lists its
    /// classes and methods to jump to (the palette's "@").
    /// </summary>
    private Control Breadcrumbs(string path)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(LunetTokens.Space.S, 0), Name = "Breadcrumbs" };
        var parts = path.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            var last = i == parts.Length - 1;
            var target = string.Join('/', parts[..(i + 1)]);
            var label = LunetUi.Text(parts[i], LunetTokens.Type.Caption, last ? LunetTokens.Color.TextPrimary : LunetTokens.Color.TextSecondary);
            var b = new Button
            {
                Content = label, MinHeight = LunetTokens.MinTouch, MinWidth = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.Xs, 0), Background = LunetTokens.Brush("#00000000"),
                BorderThickness = new Thickness(0), Name = last ? "BreadcrumbFile" : "BreadcrumbFolder",
            };
            AutomationProperties.SetName(b, last ? "Estrutura de " + parts[i] : "Mostrar a pasta " + target);
            b.Click += (_, _) => { if (last) _shell.OpenPalette("@"); else _shell.WorkspaceView.RevealInExplorer(target); };
            row.Children.Add(b);
            if (!last) row.Children.Add(new TextBlock { Text = "›", FontSize = LunetTokens.Type.Caption, Foreground = LunetTokens.Brush(LunetTokens.Color.TextTertiary), VerticalAlignment = VerticalAlignment.Center });
        }
        return new ScrollViewer
        {
            Content = row, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    private string? _tabMenuFor;
    private Border? _selectedTab;

    /// <summary>The tab whose actions are shown (long press / right click), or null.</summary>
    public string? TabMenuFor => _tabMenuFor;

    public void ShowTabMenu(string? path) { _tabMenuFor = path; Rebuild(); }

    private Control TabMenu(string path)
    {
        var panel = new WrapPanel { Margin = new Thickness(LunetTokens.Space.Xs, 0, LunetTokens.Space.Xs, LunetTokens.Space.Xxs), Name = "TabMenu" };
        void Add(string label, string? icon, Action run, string name)
        {
            var b = LunetUi.Button(label, ButtonKind.Ghost, icon, () => { _tabMenuFor = null; run(); });
            b.Name = name;
            panel.Children.Add(b);
        }
        var tabs = OpenTabs;
        var index = tabs.ToList().IndexOf(path);
        if (index > 0) Add("Mover para a esquerda", LunetIcons.Back, () => MoveTab(path, -1), "TabLeft");
        if (index >= 0 && index < tabs.Count - 1) Add("Mover para a direita", LunetIcons.ChevronRight, () => MoveTab(path, +1), "TabRight");
        if (tabs.Count > 1) Add("Fechar outras", null, () => CloseOthers(path), "TabCloseOthers");
        if (index >= 0 && index < tabs.Count - 1) Add("Fechar à direita", null, () => CloseToRight(path), "TabCloseRight");
        Add("Fechar todas", LunetIcons.Close, CloseAll, "TabCloseAll");
        Add("Mostrar no Explorador", LunetIcons.Folder, () => { Rebuild(); _shell.WorkspaceView.RevealInExplorer(path); }, "TabReveal");
        Add("Copiar caminho", LunetIcons.Copy, () => { Rebuild(); if (TopLevel.GetTopLevel(this)?.Clipboard is { } clip) _ = Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clip, path); }, "TabCopyPath");
        return new Border { Child = panel, Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised) };
    }

    /// <summary>Keeps only this tab (the open file is saved first).</summary>
    public void CloseOthers(string path)
    {
        if (!Save()) return;
        if (_session is null) return;
        _session.CloseOthers(path);
        SaveSession();
        if (OpenPath != path) Load(path); else Rebuild();
    }

    /// <summary>Closes the tabs to the right of this one.</summary>
    public void CloseToRight(string path)
    {
        if (!Save()) return;
        if (_session is null) return;
        var open = OpenPath;
        _session.CloseToRight(path);
        SaveSession();
        if (open is not null && !_session.Tabs.Contains(open)) Load(path); else Rebuild();
    }

    /// <summary>Closes every tab (saved first); the editor says nothing is open.</summary>
    public void CloseAll()
    {
        if (!Save()) return;
        if (_session is null) return;
        _session.CloseAll();
        SaveSession();
        OpenPath = null; _saved = null; _surface.Text = "";
        Rebuild();
    }

    /// <summary>Moves a tab one place left or right.</summary>
    public void MoveTab(string path, int delta)
    {
        if (_session?.Move(path, delta) == true) { SaveSession(); _tabMenuFor = path; }
        Rebuild();
    }

    /// <summary>The next (or previous) tab, wrapping (Ctrl+Tab, Ctrl+PageDown).</summary>
    public bool NextTab(int delta)
    {
        if (_session?.Neighbour(delta) is not { } next || next == OpenPath) return false;
        Load(next);
        return true;
    }

    /// <summary>The open tabs (tests).</summary>
    public IReadOnlyList<string> OpenTabs => _session?.Tabs.ToList() ?? [];

    private void NewNote()
    {
        var existing = _shell.Workspace.Files().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var n = 1;
        string path;
        do path = $"{Lunet.Core.ProjectNotes.Folder}/nota-{n++}.md"; while (existing.Contains(path));
        _shell.Workspace.Write(path, "# Nova nota\n\n");
        Load(path);
    }

    internal void RefreshFileTabs() => Rebuild();

    private void Rebuild()
    {
        var entries = new List<Control>();
        _selectedTab = null; _openLabel = null;
        // Only the files you opened are tabs (the Explorer lists every file): name, ● when unsaved, ✕ to close.
        var open = _session?.Tabs.ToList() ?? [];
        if (OpenPath is { } extra && !open.Contains(extra)) open.Insert(0, extra);
        foreach (var file in open)
        {
            var selected = file == OpenPath;
            var name = Path.GetFileName(file);
            var label = LunetUi.Text(selected && IsDirty ? name + " •" : name, LunetTokens.Type.Label, selected ? LunetTokens.Color.TextPrimary : LunetTokens.Color.TextSecondary,
                selected ? FontWeight.SemiBold : FontWeight.Normal);
            if (selected) _openLabel = label;
            label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis;
            label.VerticalAlignment = VerticalAlignment.Center;
            label.MaxWidth = 112;
            var tabLabel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xxs, VerticalAlignment = VerticalAlignment.Center };
            tabLabel.Children.Add(LunetIcons.Create(file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? LunetIcons.Code : LunetIcons.File, selected ? LunetTokens.Color.Primary : LunetTokens.Color.Info, 16));
            tabLabel.Children.Add(label);
            var b = new Button
            {
                Content = tabLabel, MinHeight = LunetTokens.MinTouch, Padding = new Thickness(LunetTokens.Space.Xs, 0, LunetTokens.Space.Xxs, 0),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            };
            AutomationProperties.SetName(b, "Abrir " + file);
            ToolTip.SetTip(b, file);
            b.Click += (_, _) => Load(file);
            var path = file;
            var close = LunetUi.IconButton(LunetIcons.Close, "Fechar " + name, () => CloseTab(path), LunetTokens.Color.TextSecondary);
            close.Name = "CloseTab";
            var tab = new StackPanel { Orientation = Orientation.Horizontal, Children = { b } };
            if (selected || _shell.State.Layout == ShellLayout.Expanded) tab.Children.Add(close);
            var border = new Border
            {
                Child = tab, Name = "EditorTab",
                // The open tab is marked by tone and a line under it, never a box (visual language §3).
                Background = LunetTokens.Brush(selected ? LunetTokens.Color.PrimarySoft : "#00000000"),
                BorderBrush = LunetTokens.Brush(selected ? LunetTokens.Color.Primary : "#00000000"), BorderThickness = new Thickness(0, 0, 0, 2),
            };
            // A long press (touch) or a right click opens the tab's actions, like VS Code's tab menu.
            border.ContextRequested += (_, e) => { e.Handled = true; _tabMenuFor = _tabMenuFor == path ? null : path; Rebuild(); };
            if (selected) _selectedTab = border;
            entries.Add(border);
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
            // Images and files imported into Notes/: a tap attaches them to the AI's next message.
            foreach (var file in NoteAttachments())
            {
                var path = file;
                var b = LunetUi.Button(Path.GetFileName(file), ButtonKind.Ghost, LunetIcons.Paperclip, () => AttachToAi(path));
                AutomationProperties.SetName(b, "Anexar " + Path.GetFileName(file) + " à IA");
                entries.Add(b);
            }
            entries.Add(LunetUi.Button("Nova nota", ButtonKind.Ghost, LunetIcons.Plus, NewNote));
            var import = LunetUi.Button("Importar", ButtonKind.Ghost, LunetIcons.Download, () => _ = PickAndImportNotes());
            import.Name = "ImportNotes";
            entries.Add(import);
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
            var start = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            start.Children.Add(LunetUi.Button("Nova nota", ButtonKind.Primary, LunetIcons.Plus, NewNote));
            start.Children.Add(LunetUi.Button("Novo mapa mental", ButtonKind.Secondary, LunetIcons.Map, () => _shell.WorkspaceView.NewMindMap()));
            start.Children.Add(LunetUi.Button("Importar", ButtonKind.Secondary, LunetIcons.Download, () => _ = PickAndImportNotes()));
            var imported = entries.Where(e => e is Button b && AutomationProperties.GetName(b)?.StartsWith("Anexar ", StringComparison.Ordinal) == true).ToList();
            foreach (var e in imported) { entries.Remove(e); start.Children.Add(e); }
            _empty.Content = LunetUi.EmptyState(LunetIcons.Notes, imported.Count == 0 ? "Nenhuma nota neste jogo" : "Arquivos nas Notas deste jogo",
                "Ideias, regras e roteiro ficam em Notes/ dentro do jogo, em Markdown ou como mapa mental, no seu disco.", start);
            _empty.IsVisible = true;
            _surface.View.IsVisible = false;
            UpdateKeyboard();
            return;
        }
        _empty.Content = null; _empty.IsVisible = false;
        _surface.View.IsVisible = true;
        // The open tabs on top, on every screen (the Explorer, not a side column, lists the game's files).
        {
            var strip = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Margin = new Thickness(LunetTokens.Space.Xxs, 0) },
            };
            var row = (StackPanel)strip.Content!;
            foreach (var e in entries) { e.HorizontalAlignment = HorizontalAlignment.Left; row.Children.Add(e); }
            _chrome.Children.Add(new Border { Child = strip, Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 0, 1) });
        }
        if (_tab == WorkspaceTab.Code && OpenPath is { } crumbsFor) _chrome.Children.Add(Breadcrumbs(crumbsFor));
        if (_tabMenuFor is { } menuFor && OpenTabs.Contains(menuFor)) _chrome.Children.Add(TabMenu(menuFor)); else _tabMenuFor = null;
        // The open tab is always in sight, even far to the right on a phone.
        if (_selectedTab is { } shown) Avalonia.Threading.Dispatcher.UIThread.Post(() => shown.BringIntoView(), Avalonia.Threading.DispatcherPriority.Background);
        if (ReviewChanges.Count > 0 && OpenPath is not null && ReviewChanges.Any(c => c.Path == OpenPath)) _chrome.Children.Add(ReviewBar());
        if (_findOpen && OpenPath is not null) _chrome.Children.Add(FindBar());
        if (_fileMenu) _chrome.Children.Add(FileMenu());
        AttachKeyboard();
        UpdateKeyboard();
    }

    private DockPanel? _frame;
    private readonly ContentControl _side = new();
    private readonly StackPanel _chrome = new();
    private readonly ContentControl _empty = new() { IsVisible = false };

    /// <summary>The fixed frame: list (left), file strip, find bar and menu (top), code bar (bottom), the text.</summary>
    /// <summary>Puts the code bar into the frame once (the same bar inserted twice showed two bars over the game, 27/11).</summary>
    private void AttachKeyboard()
    {
        if (_keyboard is null || _frame is null || _frame.Children.Contains(_keyboard)) return;
        if (_keyboard.Parent is Panel other) other.Children.Remove(_keyboard);
        _frame.Children.Insert(_frame.Children.Count - 1, _keyboard);
    }

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

}
