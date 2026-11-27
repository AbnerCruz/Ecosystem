using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Lunet.Client.Shell;
using Lunet.Client.Ui;
using Lunet.Core;

namespace Lunet.Client.Views;

/// <summary>
/// Search in the whole game (VS Code's Search view, made for a phone): one field, three toggles (Aa, palavra, .*),
/// results grouped by file with the match in its line, a tap goes to the line. Replace in all, with the count shown
/// before anything is written. The search runs off the UI thread, after a short pause in typing, and a new keystroke
/// cancels the one running, so typing never waits on the disk.
/// </summary>
public sealed class SearchView : UserControl
{
    /// <summary>Most rows drawn at once; more stay one tap away ("mostrar mais"), so a huge result never stalls.</summary>
    public const int RowsShown = 200;

    private readonly LunetShell _shell;
    private readonly Action _opened;
    private readonly TextBox _query = new() { PlaceholderText = "Buscar no jogo", Name = "SearchQuery", MinHeight = LunetTokens.MinTouch };
    private readonly TextBox _replace = new() { PlaceholderText = "Substituir por", Name = "SearchReplace", MinHeight = LunetTokens.MinTouch, IsVisible = false };
    private readonly ToggleButton _case = Toggle("Aa", "Diferenciar maiúsculas", "SearchCase");
    private readonly ToggleButton _word = Toggle("ab", "Palavra inteira", "SearchWord");
    private readonly ToggleButton _regex = Toggle(".*", "Expressão regular", "SearchRegex");
    private readonly StackPanel _results = new();
    private readonly TextBlock _summary = LunetUi.Text("", LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private CancellationTokenSource? _running;
    private int _limit = RowsShown;
    private bool _confirmReplace;

    public SearchView(LunetShell shell, Action opened)
    {
        _shell = shell;
        _opened = opened;
        _summary.TextWrapping = TextWrapping.Wrap;
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = RunAsync(); };
        _query.TextChanged += (_, _) => Schedule();
        _replace.TextChanged += (_, _) => { _confirmReplace = false; Draw(); };
        foreach (var t in new[] { _case, _word, _regex }) t.IsCheckedChanged += (_, _) => Schedule();
        AutomationProperties.SetName(_query, "Buscar no jogo");
        AutomationProperties.SetName(_replace, "Substituir por");

        var toggles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        toggles.Children.Add(_case); toggles.Children.Add(_word); toggles.Children.Add(_regex);
        var showReplace = LunetUi.IconButton(LunetIcons.ChevronRight, "Mostrar substituir", () => { _replace.IsVisible = !_replace.IsVisible; _confirmReplace = false; Draw(); },
            LunetTokens.Color.TextSecondary);
        showReplace.Name = "SearchShowReplace";
        var row = new DockPanel();
        DockPanel.SetDock(showReplace, Dock.Left);
        DockPanel.SetDock(toggles, Dock.Right);
        row.Children.Add(showReplace); row.Children.Add(toggles); row.Children.Add(_query);

        var top = new StackPanel { Spacing = LunetTokens.Space.Xs, Margin = new Thickness(LunetTokens.Space.S, LunetTokens.Space.S) };
        top.Children.Add(row);
        top.Children.Add(_replace);
        top.Children.Add(_summary);
        var root = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        root.Children.Add(new ScrollViewer { Content = _results });
        Content = root;
    }

    /// <summary>The matches of the last search (tests).</summary>
    public IReadOnlyList<SearchMatch> Matches { get; private set; } = [];

    public string Summary => _summary.Text ?? "";
    public string Query { get => _query.Text ?? ""; set => _query.Text = value; }
    public string Replacement { get => _replace.Text ?? ""; set { _replace.Text = value; _replace.IsVisible = true; } }

    private ProjectSearchOptions Options => new(_case.IsChecked == true, _word.IsChecked == true, _regex.IsChecked == true, MaxResults: 5000);

    /// <summary>Puts the field in focus, with a word to search (the selection, in VS Code) when given.</summary>
    public void Focus(string? text)
    {
        if (!string.IsNullOrEmpty(text) && !text.Contains('\n')) _query.Text = text;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => { _query.Focus(); _query.SelectAll(); });
    }

    private string Key => $"{Query}\u0001{Options}";
    private string? _searched;

    private void Schedule()
    {
        if (Key == _searched) return; // TextChanged arrives after a search that already has this text
        _running?.Cancel();
        _confirmReplace = false;
        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>Runs the search now (the pause in typing elapsed; tests call it directly).</summary>
    public async Task RunAsync()
    {
        _debounce.Stop();
        _running?.Cancel();
        var cancel = _running = new CancellationTokenSource();
        _searched = Key;
        if (_shell.Workspace.Project is not { } project || Query.Length == 0) { Matches = []; Draw(); return; }
        _shell.WorkspaceView.SaveAll(); // what is in the editor is what is searched
        var query = Query;
        var options = Options;
        if (ProjectSearch.PatternFor(query, options) is null) { Matches = []; _summary.Text = "Expressão regular inválida."; _results.Children.Clear(); return; }
        _summary.Text = "Buscando…";
        IReadOnlyList<SearchMatch> found;
        try { found = await Task.Run(() => ProjectSearch.SearchCancellable(project, query, options, cancel.Token), cancel.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ProjectException) { _summary.Text = e.Message; return; }
        if (cancel.IsCancellationRequested || !ReferenceEquals(cancel, _running)) return;
        Matches = found;
        _limit = RowsShown;
        Draw();
    }

    private void Draw()
    {
        _results.Children.Clear();
        if (Query.Length == 0) { _summary.Text = "Busque um nome, um texto ou uma expressão em todos os arquivos do jogo."; return; }
        var files = Matches.GroupBy(m => m.Path).ToList();
        _summary.Text = Matches.Count == 0 ? "Nada encontrado." :
            $"{Matches.Count}{(Matches.Count >= Options.MaxResults ? "+" : "")} resultado{(Matches.Count == 1 ? "" : "s")} em {files.Count} arquivo{(files.Count == 1 ? "" : "s")}";
        if (_replace.IsVisible && Matches.Count > 0)
        {
            var label = _confirmReplace ? $"Confirmar: substituir {Matches.Count} em {files.Count} arquivo{(files.Count == 1 ? "" : "s")}" : "Substituir em todos";
            var all = LunetUi.Button(label, _confirmReplace ? ButtonKind.Primary : ButtonKind.Secondary, null, ReplaceAll);
            all.Name = "SearchReplaceAll";
            all.Margin = new Thickness(LunetTokens.Space.S, 0, LunetTokens.Space.S, LunetTokens.Space.S);
            _results.Children.Add(all);
        }
        var drawn = 0;
        foreach (var file in files)
        {
            if (drawn >= _limit) break;
            _results.Children.Add(FileHeader(file.Key, file.Count()));
            if (_collapsed.Contains(file.Key)) continue;
            foreach (var match in file)
            {
                if (drawn++ >= _limit) break;
                _results.Children.Add(MatchRow(match));
            }
        }
        if (drawn >= _limit && Matches.Count > _limit)
        {
            var more = LunetUi.Button($"Mostrar mais ({Matches.Count - _limit})", ButtonKind.Ghost, null, () => { _limit += RowsShown; Draw(); });
            more.Name = "SearchMore";
            _results.Children.Add(more);
        }
    }

    private Control FileHeader(string path, int count)
    {
        var line = new DockPanel { Margin = new Thickness(LunetTokens.Space.S, 0) };
        var badge = LunetUi.Text(count.ToString(), LunetTokens.Type.Caption, LunetTokens.Color.TextSecondary);
        badge.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(badge, Dock.Right);
        line.Children.Add(badge);
        var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
        inner.Children.Add(LunetIcons.Create(_collapsed.Contains(path) ? LunetIcons.ChevronRight : LunetIcons.ChevronDown, LunetTokens.Color.TextSecondary, 14));
        var name = LunetUi.Text(Path.GetFileName(path), LunetTokens.Type.Label, LunetTokens.Color.TextPrimary, FontWeight.SemiBold);
        var folder = LunetUi.Text(Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "", LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary);
        folder.TextTrimming = TextTrimming.CharacterEllipsis;
        foreach (var t in new[] { name, folder }) t.VerticalAlignment = VerticalAlignment.Center;
        inner.Children.Add(name); inner.Children.Add(folder);
        line.Children.Add(inner);
        var button = Row(line, "SearchFile");
        AutomationProperties.SetName(button, $"{path}, {count} resultados");
        button.Click += (_, _) => { if (!_collapsed.Remove(path)) _collapsed.Add(path); Draw(); };
        return button;
    }

    private Control MatchRow(SearchMatch match)
    {
        // The line, starting a little before the match so it is always seen on a narrow screen.
        var raw = match.Preview;
        var lineText = LineOf(match) ?? raw;
        var start = Math.Clamp(match.Column - 1, 0, lineText.Length);
        var length = Math.Clamp(match.Length, 0, lineText.Length - start);
        var lead = Math.Max(0, start - 18);
        var before = (lead > 0 ? "…" : "") + lineText[lead..start].TrimStart();
        var text = new TextBlock { FontSize = LunetTokens.Type.Caption, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        text.Inlines!.Add(new Avalonia.Controls.Documents.Run(before) { Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary) });
        text.Inlines.Add(new Avalonia.Controls.Documents.Run(lineText.Substring(start, length))
        {
            Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary), Background = LunetTokens.Brush(LunetTokens.Color.PrimarySoft), FontWeight = FontWeight.SemiBold,
        });
        text.Inlines.Add(new Avalonia.Controls.Documents.Run(lineText[(start + length)..]) { Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary) });
        var line = new DockPanel { Margin = new Thickness(LunetTokens.Space.L + LunetTokens.Space.S, 0, LunetTokens.Space.S, 0) };
        var number = LunetUi.Text(match.Line.ToString(), LunetTokens.Type.Caption, LunetTokens.Color.TextTertiary);
        number.VerticalAlignment = VerticalAlignment.Center;
        number.Margin = new Thickness(LunetTokens.Space.S, 0, 0, 0);
        DockPanel.SetDock(number, Dock.Right);
        line.Children.Add(number);
        line.Children.Add(text);
        var button = Row(line, "SearchMatch");
        AutomationProperties.SetName(button, $"{match.Path} linha {match.Line}: {raw}");
        button.Click += (_, _) => Go(match);
        return button;
    }

    private string? LineOf(SearchMatch match)
    {
        // Preview is trimmed; the column counts from the line's start, so read the real line for the highlight.
        try
        {
            var lines = _shell.Workspace.Project?.ReadText(match.Path).Split('\n');
            return lines is not null && match.Line - 1 < lines.Length ? lines[match.Line - 1].TrimEnd('\r') : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ProjectException) { return null; }
    }

    /// <summary>Opens the match's file at its line (a phone closes the drawer).</summary>
    public void Go(SearchMatch match)
    {
        var view = _shell.WorkspaceView;
        if (_shell.State.Tab is not (WorkspaceTab.Code or WorkspaceTab.Game)) _shell.State.SelectTab(WorkspaceTab.Code);
        if (view.Code.OpenPath != match.Path) view.OpenFile(match.Path);
        view.Code.JumpTo(match.Path, match.Line, match.Column);
        _opened();
    }

    /// <summary>Replace in all: the first tap shows how many, the second writes (the editors reload what changed).</summary>
    public void ReplaceAll()
    {
        if (!_confirmReplace) { _confirmReplace = true; Draw(); return; }
        _confirmReplace = false;
        if (_shell.Workspace.Project is not { } project) return;
        try
        {
            _shell.WorkspaceView.SaveAll();
            var (files, count) = ProjectSearch.ReplaceAll(project, Query, Replacement, Options);
            _shell.WorkspaceView.ReloadFromDisk();
            Matches = [];
            Draw();
            _summary.Text = $"{count} substituiç{(count == 1 ? "ão" : "ões")} em {files.Count} arquivo{(files.Count == 1 ? "" : "s")}.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ProjectException) { _summary.Text = e.Message; }
    }

    private static Button Row(Control content, string name) => new()
    {
        Content = content, MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Padding = new Thickness(0), CornerRadius = new CornerRadius(0), BorderThickness = new Thickness(0), Background = LunetTokens.Brush("#00000000"), Name = name,
    };

    private static ToggleButton Toggle(string text, string label, string name)
    {
        var t = new ToggleButton
        {
            Content = text, MinWidth = 36, MinHeight = LunetTokens.MinTouch, Padding = new Thickness(4, 0), FontSize = LunetTokens.Type.Caption,
            BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(LunetTokens.Radius.S), Name = name,
            Background = LunetTokens.Brush("#00000000"), Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary),
        };
        AutomationProperties.SetName(t, label);
        return t;
    }
}
