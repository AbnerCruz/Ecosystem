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

    public SupportPanelView(LunetShell shell)
    {
        _shell = shell;
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
        var title = LunetUi.Title(panel switch { SupportPanel.Console => "Console", SupportPanel.More => "Painéis", _ => "Problemas" });
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
        else root.Children.Add(Problems());
        Content = root;
    }

    /// <summary>The game's other tools, opened through the shell's route (same game, saved before leaving).</summary>
    private Control More()
    {
        var list = new StackPanel { Spacing = LunetTokens.Space.Xxs, Margin = new Thickness(LunetTokens.Space.S, 0, LunetTokens.Space.S, LunetTokens.Space.M) };
        var problems = _shell.Game.Problems.Count;
        foreach (var (panel, label, icon) in new[]
        {
            (SupportPanel.Console, "Console", LunetIcons.Console),
            (SupportPanel.Problems, problems > 0 ? $"Problemas ({problems})" : "Problemas", LunetIcons.Problems),
        })
        {
            var b = LunetUi.Button(label, ButtonKind.Ghost, icon, () => _shell.State.ShowPanel(panel));
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            list.Children.Add(b);
        }
        if (_shell.OpenRoute is not null) list.Children.Add(LunetUi.Divider());
        foreach (var (route, label, icon) in _shell.OpenRoute is null ? [] : LunetShell.GameRoutes)
        {
            var b = LunetUi.Button(label, ButtonKind.Ghost, icon, () => _shell.OpenRoute?.Invoke(route, _shell.State.Project));
            b.HorizontalAlignment = HorizontalAlignment.Stretch;
            b.HorizontalContentAlignment = HorizontalAlignment.Left;
            list.Children.Add(b);
        }
        return new ScrollViewer { Content = list };
    }

    private Control Problems()
    {
        var problems = _shell.Game.Problems;
        if (problems.Count == 0)
            return LunetUi.EmptyState(LunetIcons.Problems, "Nenhum problema", "Quando o jogo não compilar ou parar com erro, cada problema aparece aqui.");
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
