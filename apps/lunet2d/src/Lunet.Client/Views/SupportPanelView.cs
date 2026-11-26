using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Shell;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>Console and Problemas: what the game and the compiler said, next to the work, never in a dialog.</summary>
public sealed class SupportPanelView : UserControl
{
    /// <summary>Lines kept in the console (oldest dropped first).</summary>
    public const int MaxConsoleLines = 2000;

    private readonly LunetShell _shell;
    private readonly List<string> _console = [];
    private readonly SelectableTextBlock _consoleText = new()
    {
        FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Label,
        Foreground = LunetTokens.Brush(LunetTokens.Color.TextSecondary), TextWrapping = TextWrapping.Wrap,
    };
    private readonly ScrollViewer _consoleScroll = new();

    /// <summary>The command palette (M3), kept so its recents survive closing it.</summary>
    public CommandPaletteView Palette { get; }

    /// <summary>The AI of the game (M7), kept so a running request survives closing the sheet.</summary>
    public AiPanelView Ai { get; }

    /// <summary>The Inspector of the running game (M6).</summary>
    public InspectorView Inspector { get; }

    /// <summary>The frame graph of the running game (M6).</summary>
    public PerformanceView Performance { get; }

    /// <summary>The offline documentation (P3), kept so its page and history survive closing it.</summary>
    public DocsView Docs { get; } = new();
    public ObjectivesView Objectives => _objectives ??= new ObjectivesView(_shell);
    private ObjectivesView? _objectives;

    public SupportPanelView(LunetShell shell)
    {
        _shell = shell;
        Palette = new CommandPaletteView(shell);
        Ai = new AiPanelView(shell);
        Inspector = new InspectorView(shell);
        Performance = new PerformanceView(shell);
        _consoleScroll.Content = _consoleText;
        _consoleScroll.Padding = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.M);
    }

    public IReadOnlyList<string> ConsoleLines => _console;

    public void Append(string line)
    {
        _console.Add(line);
        if (_console.Count > MaxConsoleLines) _console.RemoveRange(0, _console.Count - MaxConsoleLines);
        if (_shell.State.Panel == SupportPanel.Console)
        {
            _consoleText.Text = string.Join('\n', _console);
            _consoleScroll.ScrollToEnd();
        }
    }

    public void Refresh()
    {
        var panel = _shell.State.Panel;
        if (panel == SupportPanel.None) { Content = null; return; }
        var header = new DockPanel { Margin = new Thickness(LunetTokens.Space.M, LunetTokens.Space.Xs, LunetTokens.Space.Xs, LunetTokens.Space.Xs) };
        var close = LunetUi.IconButton(LunetIcons.Close, "Fechar painel", () => _shell.State.TogglePanel(panel));
        DockPanel.SetDock(close, Dock.Right);
        header.Children.Add(close);
        var title = LunetUi.Title(panel switch { SupportPanel.Console => "Console", SupportPanel.More => "Painéis", SupportPanel.Commands => "Comandos", SupportPanel.Ai => "IA do jogo", SupportPanel.Inspector => "Inspector", SupportPanel.Performance => "Desempenho", SupportPanel.Docs => "Documentação", SupportPanel.Objectives => "Objetivos", _ => "Problemas" });
        title.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(title);

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        if (_consoleScroll.Parent is Panel p) p.Children.Remove(_consoleScroll);
        if (panel == SupportPanel.Console)
        {
            _consoleText.Text = _console.Count == 0 ? "Nada por aqui ainda. O que o jogo escrever com Log aparece aqui." : string.Join('\n', _console);
            root.Children.Add(_consoleScroll);
            _consoleScroll.ScrollToEnd();
        }
        else if (panel == SupportPanel.More) root.Children.Add(More());
        else if (panel == SupportPanel.Ai)
        {
            if (Ai.Parent is Panel oldAi) oldAi.Children.Remove(Ai);
            Ai.Refresh();
            root.Children.Add(Ai);
        }
        else if (panel == SupportPanel.Objectives)
        {
            if (Objectives.Parent is Panel oldObjectives) oldObjectives.Children.Remove(Objectives);
            Objectives.Refresh();
            root.Children.Add(Objectives);
        }
        else if (panel is SupportPanel.Inspector or SupportPanel.Performance or SupportPanel.Docs)
        {
            Control view = panel switch { SupportPanel.Inspector => Inspector, SupportPanel.Performance => Performance, _ => Docs };
            if (view.Parent is Panel oldView) oldView.Children.Remove(view);
            root.Children.Add(view);
        }
        else if (panel == SupportPanel.Commands)
        {
            if (Palette.Parent is Panel old) old.Children.Remove(Palette);
            root.Children.Add(Palette);
        }
        else root.Children.Add(Problems());
        Content = root;
    }

    /// <summary>The game's other tools, opened through the shell's route (same game, saved before leaving).</summary>
    private Control More()
    {
        var list = new StackPanel { Spacing = LunetTokens.Space.Xxs, Margin = new Thickness(LunetTokens.Space.S, 0, LunetTokens.Space.S, LunetTokens.Space.M) };
        var problems = _shell.Game.Problems.Count;
        // Objetivos first when this game is a challenge of Aprender (H6: the mission stays beside the code).
        var objectives = ObjectivesView.ChallengesOf(_shell, out _).Count > 0;
        foreach (var (panel, label, icon) in new[]
        {
            (SupportPanel.Objectives, "Objetivos", LunetIcons.Book),
            (SupportPanel.Ai, "IA do jogo", LunetIcons.Function),
            (SupportPanel.Commands, "Comandos", LunetIcons.Search),
            (SupportPanel.Docs, "Documentação", LunetIcons.Book),
            (SupportPanel.Inspector, "Inspector", LunetIcons.Inspector),
            (SupportPanel.Performance, "Desempenho", LunetIcons.Pulse),
            (SupportPanel.Console, "Console", LunetIcons.Console),
            (SupportPanel.Problems, problems > 0 ? $"Problemas ({problems})" : "Problemas", LunetIcons.Problems),
        }.Where(p => p.Item1 != SupportPanel.Objectives || objectives))
        {
            var b = LunetUi.Button(label, ButtonKind.Ghost, icon, () => { if (panel == SupportPanel.Commands) _shell.OpenPalette(); else _shell.State.ShowPanel(panel); });
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            list.Children.Add(b);
        }
        list.Children.Add(LunetUi.Divider());
        // Everything a game needs lives in the shell: Versões (P6), Gerar APK (P5), Recursos and Notas (tabs), the
        // editor's folding, jumps, files and text files. No entry here opens a previous screen.
        var versions = LunetUi.Button("Versões (Git)", ButtonKind.Ghost, LunetIcons.Folder, () => { _shell.State.ShowPanel(SupportPanel.None); _shell.WorkspaceView.OpenGit(); });
        versions.HorizontalAlignment = HorizontalAlignment.Stretch;
        versions.HorizontalContentAlignment = HorizontalAlignment.Left;
        versions.Name = "OpenGit";
        list.Children.Add(versions);
        var apk = LunetUi.Button("Gerar APK", ButtonKind.Ghost, LunetIcons.Download, () => { _shell.State.ShowPanel(SupportPanel.None); _shell.WorkspaceView.OpenApk(); });
        apk.HorizontalAlignment = HorizontalAlignment.Stretch;
        apk.HorizontalContentAlignment = HorizontalAlignment.Left;
        apk.Name = "OpenApk";
        list.Children.Add(apk);
        var connections = LunetUi.Button("Conexões", ButtonKind.Ghost, LunetIcons.Map, () => { _shell.State.ShowPanel(SupportPanel.None); _shell.WorkspaceView.OpenConnections(); });
        connections.HorizontalAlignment = HorizontalAlignment.Stretch;
        connections.HorizontalContentAlignment = HorizontalAlignment.Left;
        connections.Name = "OpenConnections";
        list.Children.Add(connections);
        return new ScrollViewer { Content = list };
    }

    /// <summary>The analyzer's errors and warnings while typing (M1); a tap goes to the line in the code.</summary>
    private Control LiveProblems()
    {
        var list = new StackPanel { Spacing = LunetTokens.Space.Xs, Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.M) };
        foreach (var d in _shell.LiveProblems.OrderBy(d => d.Severity == Lunet.Compiler.DiagnosticSeverity.Error ? 0 : 1).ThenBy(d => d.Line))
        {
            var error = d.Severity == Lunet.Compiler.DiagnosticSeverity.Error;
            var body = new StackPanel { Spacing = 2 };
            body.Children.Add(LunetUi.Text($"Linha {d.Line} · {(error ? "erro" : "aviso")} {d.Id}", LunetTokens.Type.Caption, error ? LunetTokens.Color.Danger : LunetTokens.Color.Warning, FontWeight.SemiBold));
            body.Children.Add(LunetUi.Text(d.Message, LunetTokens.Type.Label));
            var row = new Button
            {
                Content = body, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(LunetTokens.Space.S), Background = LunetTokens.Brush(LunetTokens.Color.Surface),
                BorderBrush = LunetTokens.Brush(error ? LunetTokens.Color.Danger : LunetTokens.Color.Warning), BorderThickness = new Thickness(3, 0, 0, 0),
                CornerRadius = new CornerRadius(LunetTokens.Radius.S),
            };
            Avalonia.Automation.AutomationProperties.SetName(row, $"Ir para a linha {d.Line}: {d.Message}");
            var diagnostic = d;
            row.Click += (_, _) =>
            {
                _shell.State.ShowPanel(SupportPanel.None);
                _shell.State.SelectTab(WorkspaceTab.Code);
                _shell.WorkspaceView.Code.JumpTo(null, diagnostic.Line, diagnostic.Column);
            };
            list.Children.Add(row);
        }
        return new ScrollViewer { Content = list };
    }

    private Control Problems()
    {
        var problems = _shell.Game.Problems;
        if (problems.Count == 0 && _shell.LiveProblems.Count > 0) return LiveProblems();
        if (problems.Count == 0)
            return LunetUi.EmptyState(LunetIcons.Problems, "Nenhum problema", "Erros e avisos do código aparecem aqui enquanto você digita, e também quando o jogo não compila ou para com erro.");
        var list = new StackPanel { Spacing = LunetTokens.Space.Xs, Margin = new Thickness(LunetTokens.Space.M, 0, LunetTokens.Space.M, LunetTokens.Space.M) };
        foreach (var p in problems)
        {
            var text = new SelectableTextBlock
            {
                Text = p, FontFamily = LunetTokens.MonoFont, FontSize = LunetTokens.Type.Label,
                Foreground = LunetTokens.Brush(LunetTokens.Color.TextPrimary), TextWrapping = TextWrapping.Wrap,
            };
            list.Children.Add(new Border
            {
                Child = text, Padding = new Thickness(LunetTokens.Space.S),
                Background = LunetTokens.Brush(LunetTokens.Color.Surface), CornerRadius = new CornerRadius(LunetTokens.Radius.S),
                BorderBrush = LunetTokens.Brush(LunetTokens.Color.Danger), BorderThickness = new Thickness(3, 0, 0, 0),
            });
        }
        return new ScrollViewer { Content = list };
    }
}
