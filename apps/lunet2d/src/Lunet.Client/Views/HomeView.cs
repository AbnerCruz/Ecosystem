using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Lunet.Client.Ui;

namespace Lunet.Client.Views;

/// <summary>
/// Level 1 of the shell: your games, and the action of making a new one. One place, no repeated destinations
/// (the old Home showed Projetos/Studios/Explorar/Aprender both as cards and in the bar).
/// </summary>
public sealed class HomeView : UserControl
{
    private readonly LunetShell _shell;
    private readonly WrapPanel _grid = new() { Orientation = Orientation.Horizontal };

    public HomeView(LunetShell shell)
    {
        _shell = shell;
        Background = LunetTokens.Brush(LunetTokens.Color.Background);
        Build();
    }

    /// <summary>Lays the Home out (again when the head sets <see cref="LunetShell.OpenRoute"/>).</summary>
    public void Build()
    {
        var shell = _shell;
        if (_grid.Parent is Panel old) old.Children.Remove(_grid);
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, LunetTokens.Space.L) };
        var create = LunetUi.Button("Novo jogo", ButtonKind.Primary, LunetIcons.Plus, () => _shell.CreateProject());
        create.Name = "NewProject";
        DockPanel.SetDock(create, Dock.Right);
        header.Children.Add(create);
        var titles = new StackPanel { Spacing = LunetTokens.Space.Xxs, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(LunetUi.Headline("Seus jogos"));
        var folder = LunetUi.Secondary(shell.Workspace.Root);
        folder.TextWrapping = TextWrapping.NoWrap;
        folder.TextTrimming = TextTrimming.PathSegmentEllipsis;
        titles.Children.Add(folder);
        header.Children.Add(titles);

        var content = new StackPanel { Margin = new Thickness(LunetTokens.Space.M, LunetTokens.Space.L) };
        content.Children.Add(header);
        if (shell.OpenRoute is not null) content.Children.Add(Destinations());
        content.Children.Add(_grid);
        Content = new ScrollViewer { Content = content };
        Refresh();
    }

    /// <summary>Where else to go from the Home (Jogar, Studios, Aprender…), one row that scrolls sideways.</summary>
    private Control Destinations()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = LunetTokens.Space.Xs };
        foreach (var (route, label, icon) in LunetShell.HomeRoutes)
        {
            var b = LunetUi.Button(label, ButtonKind.Secondary, icon, () => _shell.OpenRoute?.Invoke(route, null));
            b.Name = "Route-" + route;
            row.Children.Add(b);
        }
        return new ScrollViewer
        {
            Content = row, Margin = new Thickness(0, 0, 0, LunetTokens.Space.L),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
    }

    /// <summary>The project names currently shown, in order (for tests and screen readers).</summary>
    public IReadOnlyList<string> Shown { get; private set; } = [];

    public void Refresh()
    {
        _grid.Children.Clear();
        Shown = _shell.Workspace.Projects();
        if (Shown.Count == 0)
        {
            _grid.Children.Add(LunetUi.EmptyState(LunetIcons.Game, "Nenhum jogo ainda",
                "Crie um jogo: o código, a arte, o som e o jogo rodando ficam juntos, a um toque de distância.",
                LunetUi.Button("Criar o primeiro jogo", ButtonKind.Primary, LunetIcons.Plus, () => _shell.CreateProject())));
            return;
        }
        foreach (var name in Shown) _grid.Children.Add(ProjectCard(name));
    }

    private Control ProjectCard(string name)
    {
        var stack = new StackPanel { Spacing = LunetTokens.Space.Xs };
        stack.Children.Add(new Border
        {
            Height = 96, CornerRadius = new CornerRadius(LunetTokens.Radius.M),
            Background = LunetTokens.Brush(LunetTokens.Color.SurfaceRaised),
            Child = new Viewbox { Width = 40, Height = 40, Child = LunetIcons.Create(LunetIcons.Game, LunetTokens.Color.Primary, 40) },
        });
        stack.Children.Add(LunetUi.Text(name, LunetTokens.Type.Body, weight: FontWeight.SemiBold));
        var button = new Button
        {
            Content = LunetUi.Card(stack, LunetTokens.Space.S),
            Width = 200, Margin = new Thickness(0, 0, LunetTokens.Space.S, LunetTokens.Space.S),
            Padding = new Thickness(0), Background = LunetTokens.Brush("#00000000"), BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        button.Classes.Add("lunet-card");
        AutomationProperties.SetName(button, "Abrir " + name);
        button.Click += (_, _) => _shell.OpenProject(name);
        return button;
    }
}
