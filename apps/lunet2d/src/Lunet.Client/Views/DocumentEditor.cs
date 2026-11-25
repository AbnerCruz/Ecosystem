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
        // Using the code (focus, or a tap with the keyboard hidden) brings the Lunet keyboard.
        if (tab == WorkspaceTab.Code) _surface.Engaged += ShowKeyboard;
        if (_surface is CodeSurface code)
        {
            // M1: analyse a moment after typing stops (Roslyn runs off the UI thread; stale results are dropped).
            _analysisDelay.Tick += (_, _) => { _analysisDelay.Stop(); _ = AnalyzeNow(); };
            code.TextChanged += () => { _analysisDelay.Stop(); _analysisDelay.Start(); };
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
            _shell.SetLiveProblems(code.Diagnostics);
            _keyboard?.SetAnalysis(text, caret, result.Completions, result.Signature);
        }
        catch (OperationCanceledException) { }
    }

    private LunetKeyboard? _keyboard;
    private bool _keyboardWanted;

    /// <summary>The IDE keyboard of the Código tab (created on first use, kept across layouts so its tab survives).</summary>
    public LunetKeyboard? Keyboard => _keyboard;

    /// <summary>
    /// True when the Lunet keyboard is on screen: phone layout, Código tab, the editor in use, no hardware keyboard and
    /// the person did not switch to the system keyboard (study 25/11 §4).
    /// </summary>
    public bool KeyboardVisible => _keyboard?.IsVisible == true;

    private bool KeyboardApplies => _tab == WorkspaceTab.Code && _shell.State.Layout == ShellLayout.Compact
        && !_shell.HardwareKeyboard && !_shell.PreferSystemKeyboard;

    /// <summary>Shows the Lunet keyboard for the editor (the editor keeps focus; the system keyboard steps aside).</summary>
    public void ShowKeyboard()
    {
        _keyboardWanted = true;
        UpdateKeyboard();
        _keyboard?.RefreshSuggestions(); // the caret may have moved by a tap on the code
    }

    /// <summary>Hides the Lunet keyboard (Back, leaving the editor). Returns true when it was visible.</summary>
    public bool HideKeyboard()
    {
        var was = KeyboardVisible;
        _keyboardWanted = false;
        UpdateKeyboard();
        return was;
    }

    /// <summary>Applies the current rules: visibility, the system IME on the editor, and the shell's tab bar.</summary>
    public void UpdateKeyboard()
    {
        var on = _keyboardWanted && KeyboardApplies;
        if (on && _keyboard is null)
        {
            _keyboard = new LunetKeyboard(_surface, _shell.Methods, ProjectSources, UseSystemKeyboard, () => _shell.OpenPalette());
            DockPanel.SetDock(_keyboard, Dock.Bottom);
            var hadFocus = _surface.IsFocused;
            Rebuild(); // places it in the layout, then comes back here
            if (hadFocus) _surface.Focus(); // moving the editor into the new layout must not drop the caret or focus
            return;
        }
        if (_keyboard is not null)
        {
            _keyboard.IsVisible = on;
            if (on) _keyboard.Height = LunetKeyboard.PreferredHeight(_shell.Bounds.Height > 0 ? _shell.Bounds.Height : 800);
        }
        // Only the code editor loses the system keyboard, and only while the Lunet keyboard is there.
        InputMethod.SetIsInputMethodEnabled(_surface.InputTarget, !(_tab == WorkspaceTab.Code && on));
        if (_tab == WorkspaceTab.Code) _shell.WorkspaceView.SetKeyboardOpen(on || _shell.KeyboardOpen);
    }

    private IEnumerable<(string Path, string Text)> ProjectSources() =>
        _shell.Workspace.Project is null ? [] : _shell.Workspace.Files()
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => (f, _shell.Workspace.Read(f)));

    private void UseSystemKeyboard()
    {
        _shell.PreferSystemKeyboard = true;
        Rebuild();
        _surface.Focus();
    }

    private void UseLunetKeyboard()
    {
        _shell.PreferSystemKeyboard = false;
        _keyboardWanted = true;
        Rebuild();
        _surface.Focus();
    }
    public bool IsDirty => OpenPath is not null && _surface.Text != _saved;

    public void Load(string? path)
    {
        Save();
        if (path is null) path = ProjectFiles.For(_tab, _shell.Workspace.Files()).FirstOrDefault();
        OpenPath = path;
        _saved = path is null ? null : _shell.Workspace.Read(path);
        _surface.Text = _saved ?? "";
        if (_surface is CodeSurface loaded) { loaded.SetDiagnostics([]); _analysisDelay.Stop(); _analysisDelay.Start(); }
        Rebuild();
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
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _shell.Report($"Não foi possível salvar {OpenPath}: {ex.Message}. O texto continua aberto aqui.");
            return false;
        }
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
        foreach (var file in ProjectFiles.For(_tab, _shell.Workspace.Files()))
        {
            var selected = file == OpenPath;
            var label = LunetUi.Text(file, LunetTokens.Type.Label, selected ? LunetTokens.Color.Primary : LunetTokens.Color.TextSecondary,
                selected ? FontWeight.SemiBold : FontWeight.Normal);
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
            entries.Add(LunetUi.Button("Nova nota", ButtonKind.Ghost, LunetIcons.Plus, NewNote));

        if (OpenPath is null && _tab == WorkspaceTab.Notes)
        {
            Content = LunetUi.EmptyState(LunetIcons.Notes, "Nenhuma nota neste jogo",
                "Ideias, regras e roteiro ficam em Notes/ dentro do jogo, em Markdown, no seu disco.",
                LunetUi.Button("Nova nota", ButtonKind.Primary, LunetIcons.Plus, NewNote));
            return;
        }
        var column = new StackPanel { Spacing = 2, Margin = new Thickness(LunetTokens.Space.Xs) };
        var list = new ScrollViewer { Content = column };
        var listHost = new Border
        {
            Child = list, Width = 220, Background = LunetTokens.Brush(LunetTokens.Color.Surface),
            BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 1, 0),
        };
        var root = new DockPanel();
        // On a phone the file list collapses: the text gets the width; files are one tap away in the header.
        if (_shell.State.Layout == ShellLayout.Expanded && !_shell.State.CodeAndGameSideBySide)
        {
            foreach (var e in entries) { e.HorizontalAlignment = HorizontalAlignment.Stretch; column.Children.Add(e); }
            DockPanel.SetDock(listHost, Dock.Left);
            root.Children.Add(listHost);
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
            var stripHost = new Border { Child = strip, Background = LunetTokens.Brush(LunetTokens.Color.Surface), BorderBrush = LunetTokens.Brush(LunetTokens.Color.Border), BorderThickness = new Thickness(0, 0, 0, 1) };
            DockPanel.SetDock(stripHost, Dock.Top);
            root.Children.Add(stripHost);
        }
        if (_tab == WorkspaceTab.Code && _shell.State.Layout == ShellLayout.Compact && !_shell.HardwareKeyboard && _shell.PreferSystemKeyboard)
        {
            // Back to the Lunet keyboard after using the system's (dictation, another language).
            var back = LunetUi.Button("Teclado Lunet", ButtonKind.Ghost, LunetIcons.Code, UseLunetKeyboard);
            back.MinHeight = 40;
            DockPanel.SetDock(back, Dock.Bottom);
            root.Children.Add(back);
        }
        if (_keyboard?.Parent is Panel kp) kp.Children.Remove(_keyboard);
        if (_keyboard is not null) root.Children.Add(_keyboard);
        UpdateKeyboard();
        if (_surface.View.Parent is Panel p) p.Children.Remove(_surface.View);
        root.Children.Add(_surface.View);
        Content = root;
    }

    /// <summary>Re-lays out the file list for the current shell layout.</summary>
    public void Relayout() => Rebuild();
}
